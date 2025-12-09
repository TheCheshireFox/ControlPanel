namespace ControlPanel.Agent;

public record AudioStreamIcon(string Name, byte[] Icon)
{
    public static AudioStreamIcon Default { get; } = new("", []);
}

public record AudioStream(string Id, string Name, AudioStreamIcon Icon, bool Mute, double Volume);

public interface IAudioAgent
{
    Task<AudioStream[]> GetAudioStreamsAsync(CancellationToken cancellationToken);
    Task SetVolumeAsync(string id, double volume, CancellationToken cancellationToken);
    Task ToggleMuteAsync(string id, bool mute, CancellationToken cancellationToken);
}