using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using ControlPanel.Agent.Shared;
using ControlPanel.Shared;
using ControlPanel.Shared.Extensions;
using NAudio.CoreAudioApi;
using NAudio.CoreAudioApi.Interfaces;

namespace ControlPanel.Agent.Windows;

public sealed class WindowsAudioAgent : IAudioAgent, IDisposable
{
    private readonly SessionIdMapper _idMapper = new();
    private readonly Task _idMapperPruneTask;
    private readonly CancellationTokenSource _cts = new();

    public WindowsAudioAgent()
    {
        _idMapperPruneTask = Task.Run(PruneMapperAsync);
    }

    public Task<AudioAgentDescription> GetAudioAgentDescription()
    {
        return Task.FromResult(new AudioAgentDescription(
            AgentIcon: ResourceLoader.Load("Assets/win-logo.svg").ReadAllBytes()
        ));
    }
    
    public async Task<AudioStream[]> GetAudioStreamsAsync(CancellationToken cancellationToken)
    {
        var result = new Dictionary<string, AudioStream>();
        
        using var sessions = new AudioSessionsEnumerator();

        foreach (var session in sessions.Where(x => x is { IsSystemSoundsSession: false, State: AudioSessionState.AudioSessionStateActive }))
        {
            var id = await _idMapper.GetMappedIdAsync(session.GetSessionInstanceIdentifier, cancellationToken);
            var simpleVolume = session.SimpleAudioVolume;

            var name = GetSessionName(session);

            var stream = new AudioStream(
                Id: id,
                Source: ProcessUtility.GetBinaryPath((int)session.GetProcessID) ?? string.Empty,
                Name: name,
                Mute: simpleVolume.Mute,
                Volume: simpleVolume.Volume
            );

            result[stream.Id] = stream;
        }

        return result.Values.ToArray();
    }

    public async Task SetVolumeAsync(string id, double volume, CancellationToken cancellationToken)
    {
        using var sessions = new AudioSessionsEnumerator();
        
        var session = await FindSessionAsync(sessions, id, cancellationToken);
        if (session != null)
            session.SimpleAudioVolume.Volume = (float)Math.Clamp(volume, 0.0, 1.0);
    }

    public async Task ToggleMuteAsync(string id, bool mute, CancellationToken cancellationToken)
    {
        using var sessions = new AudioSessionsEnumerator();
        
        var session = await FindSessionAsync(sessions, id, cancellationToken);
        if (session != null)
            session.SimpleAudioVolume.Mute = mute;
    }

    public Task<AudioStreamIcon> GetAudioStreamIconAsync(string source, CancellationToken cancellationToken)
    {
        return Task.FromResult(IconLocator.FindIcon(source));
    }

    private async Task PruneMapperAsync()
    {
        while (!_cts.IsCancellationRequested)
        {
            await Task.Delay(TimeSpan.FromMinutes(10),  _cts.Token);

            try
            {
                using var sessions = new AudioSessionsEnumerator();
                await _idMapper.PruneAsync(sessions.Select(x => x.GetSessionInstanceIdentifier), _cts.Token);
            }
            catch (Exception) when (!_cts.IsCancellationRequested)
            {
                // NOP
            }
        }
    }
    
    private async Task<AudioSessionControl?> FindSessionAsync(AudioSessionsEnumerator sessions, string mapId, CancellationToken cancellationToken)
    {
        var sessionId = await _idMapper.FindSessionIdAsync(mapId, cancellationToken);
        if (sessionId == null)
            return null;

        var session = sessions.FirstOrDefault(x => string.Equals(x.GetSessionInstanceIdentifier, sessionId, StringComparison.Ordinal));
        if (session != null)
            return session;
        
        await _idMapper.RemoveAsync(mapId, cancellationToken);
        return null;
    }
    
    private static string GetSessionName(AudioSessionControl session)
    {
        var displayName = session.DisplayName;
        if (LooksLikeResourceString(displayName))
            displayName = TryResolveResourceString(displayName);
        
        if (!string.IsNullOrWhiteSpace(displayName))
            return displayName;
        
        try
        {
            var pid = session.GetProcessID;
            if (pid != 0)
            {
                using var process = Process.GetProcessById((int)pid);

                if (!string.IsNullOrWhiteSpace(process.MainWindowTitle))
                    return process.MainWindowTitle;

                if (!string.IsNullOrWhiteSpace(process.ProcessName))
                    return process.ProcessName;
            }
        }
        catch
        {
            // NOP
        }

        return "Unknown";
    }
    
    private static bool LooksLikeResourceString(string? s)
    {
        if (string.IsNullOrWhiteSpace(s))
            return false;

        if (!s.StartsWith('@'))
            return false;

        return s.Contains("%SystemRoot%", StringComparison.OrdinalIgnoreCase) ||
               s.Contains(".dll", StringComparison.OrdinalIgnoreCase) ||
               s.Contains(".exe", StringComparison.OrdinalIgnoreCase);
    }
    
    [DllImport("shlwapi.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern int SHLoadIndirectString(string pszSource, StringBuilder pszOutBuf, int cchOutBuf, IntPtr ppvReserved);
    
    private static string? TryResolveResourceString(string s)
    {
        var sb = new StringBuilder(260);
        var hr = SHLoadIndirectString(s, sb, sb.Capacity, IntPtr.Zero);
        return hr == 0 ? sb.ToString() : null;
    }

    public void Dispose()
    {
        _cts.Cancel();
        _idMapperPruneTask.ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing).GetAwaiter().GetResult();
        
        _cts.Dispose();
        _idMapperPruneTask.Dispose();
    }
}