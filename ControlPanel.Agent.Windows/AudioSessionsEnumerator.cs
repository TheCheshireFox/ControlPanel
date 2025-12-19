using System.Collections;
using NAudio.CoreAudioApi;

namespace ControlPanel.Agent.Windows;

internal sealed class AudioSessionsEnumerator : IEnumerable<AudioSessionControl>, IDisposable
{
    private readonly List<AudioSessionControl> _sessions = [];

    public AudioSessionsEnumerator()
    {
        var devEnumerator = new MMDeviceEnumerator();

        foreach (var role in Enum.GetValues<Role>())
        {
            var sessions = devEnumerator
                .GetDefaultAudioEndpoint(DataFlow.Render, role)
                .AudioSessionManager.Sessions;
            
            for (var i = 0; i < sessions.Count; i++)
                _sessions.Add(sessions[i]);
        }
    }
    
    public IEnumerator<AudioSessionControl> GetEnumerator() => _sessions.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    public void Dispose()
    {
        foreach (var session in _sessions)
            session.Dispose();
    }
}