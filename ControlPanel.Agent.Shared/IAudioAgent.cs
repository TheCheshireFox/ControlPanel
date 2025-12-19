namespace ControlPanel.Agent.Shared;

public record AudioAgentDescription(byte[] AgentIcon);

public record AudioStreamIcon(byte[] Icon)
{
    public static AudioStreamIcon Default { get; } = new([]);
}

public record AudioStream(string Id, string Source, string Name, bool Mute, double Volume);

public interface IAudioAgent
{
    Task<AudioAgentDescription> GetAudioAgentDescription();
    Task<AudioStream[]> GetAudioStreamsAsync(CancellationToken cancellationToken);
    Task SetVolumeAsync(string id, double volume, CancellationToken cancellationToken);
    Task ToggleMuteAsync(string id, bool mute, CancellationToken cancellationToken);
    Task<AudioStreamIcon> GetAudioStreamIconAsync(string source, CancellationToken cancellationToken);
}