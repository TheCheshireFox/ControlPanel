using MessagePack;

namespace ControlPanel.Bridge.Device.DeviceProtocol;

[MessagePackObject(true)]
public record StreamsDeviceMessage(
    [property: Key("updated")] AudioStream[] Updated,
    [property: Key("deleted")] AudioStreamId[] Deleted)
    : DeviceMessage(MessageType.Streams);