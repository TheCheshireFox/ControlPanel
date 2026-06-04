using Mediator;
using MessagePack;

namespace ControlPanel.Bridge.Device.DeviceProtocol;

[MessagePackObject(true)]
public record SetMuteDeviceMessage(
    [property: Key("id")] AudioStreamId Id,
    [property: Key("mute")] bool Mute)
    : DeviceMessage(MessageType.SetMute), INotification;
