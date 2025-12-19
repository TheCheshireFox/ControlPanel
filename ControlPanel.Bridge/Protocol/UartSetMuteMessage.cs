using MessagePack;

namespace ControlPanel.Bridge.Protocol;

[MessagePackObject(true)]
public record UartSetMuteMessage(
    [property: Key("id")] UartAudioStreamId Id,
    [property: Key("mute")] bool Mute)
    : UartMessage(UartMessageType.SetMute);