using MessagePack;

namespace ControlPanel.Bridge.Protocol;

[MessagePackObject(true)]
public record SetMuteMessage(
    [property: Key("id")] AudioStreamId Id,
    [property: Key("mute")] bool Mute)
    : Message(MessageType.SetMute);