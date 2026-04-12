namespace ControlPanel.Agent.Windows.WindowsAudioSystem;

public interface IAudioSessionProvider
{
    IEnumerable<AudioSession> Sessions { get; }
}