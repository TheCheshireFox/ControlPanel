using System.Diagnostics;
using NAudio.CoreAudioApi;

namespace ControlPanel.Agent;

public sealed class WindowsAudioAgent : IAudioAgent, IDisposable
{
    private readonly MMDeviceEnumerator _enumerator;
    private readonly DataFlow _dataFlow;
    private readonly Role _role;

    public WindowsAudioAgent(DataFlow dataFlow = DataFlow.Render, Role role = Role.Multimedia)
    {
        _enumerator = new MMDeviceEnumerator();
        _dataFlow = dataFlow;
        _role = role;
    }

    public void Dispose()
    {
        _enumerator.Dispose();
    }

    private MMDevice GetDefaultDevice()
        => _enumerator.GetDefaultAudioEndpoint(_dataFlow, _role);

    public Task<AudioStream[]> GetAudioStreamsAsync(CancellationToken cancellationToken)
    {
        var device = GetDefaultDevice();
        var sessionManager = device.AudioSessionManager;
        var sessions = sessionManager.Sessions;

        var result = new List<AudioStream>(sessions.Count);

        for (var i = 0; i < sessions.Count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            using var session = sessions[i];

            var id = session.GetSessionInstanceIdentifier; // unique per session instance
            var simpleVolume = session.SimpleAudioVolume;

            var name = GetSessionName(session);

            var stream = new AudioStream(
                Id: id,
                Name: name,
                Icon: AudioStreamIcon.Default,
                Mute: simpleVolume.Mute,
                Volume: simpleVolume.Volume
            );

            result.Add(stream);
        }

        return Task.FromResult(result.ToArray());
    }

    public Task SetVolumeAsync(string id, double volume, CancellationToken cancellationToken)
    {
        ApplyToSession(id, session =>
        {
            var sv = session.SimpleAudioVolume;
            sv.Volume = (float)Math.Clamp(volume, 0.0, 1.0);
        }, cancellationToken);

        return Task.CompletedTask;
    }

    public Task ToggleMuteAsync(string id, bool mute, CancellationToken cancellationToken)
    {
        ApplyToSession(id, session =>
        {
            var sv = session.SimpleAudioVolume;
            sv.Mute = mute;
        }, cancellationToken);

        return Task.CompletedTask;
    }

    private void ApplyToSession(string id, Action<AudioSessionControl> action, CancellationToken cancellationToken)
    {
        var device = GetDefaultDevice();
        var sessionManager = device.AudioSessionManager;
        var sessions = sessionManager.Sessions;

        for (var i = 0; i < sessions.Count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            using var session = sessions[i];
            
            if (!string.Equals(session.GetSessionInstanceIdentifier, id, StringComparison.Ordinal))
                continue;

            action(session);
            break;
        }
    }

    private static string GetSessionName(AudioSessionControl session)
    {
        var displayName = session.DisplayName;
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
}