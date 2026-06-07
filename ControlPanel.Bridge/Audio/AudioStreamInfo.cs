using ControlPanel.Protocol;

namespace ControlPanel.Bridge.Audio;

public record AudioStreamInfo(AudioStreamId Id, string Source, string Name, bool Mute, double Volume, int IconHash)
{
    public static AudioStreamInfo FromStream(AudioStreamId streamId, AgentAudioStream stream)
        => new(
            streamId,
            stream.Source,
            stream.Name,
            stream.Mute,
            stream.Volume,
            stream.IconHash
        );
}