using MessagePack;

namespace ControlPanel.Bridge.Protocol;

[MessagePackObject(true)]
public record SetVolumeMessage(
    [property: Key("id")] AudioStreamId Id, 
    [property: Key("volume")] double Volume)
    : Message(MessageType.SetVolume);