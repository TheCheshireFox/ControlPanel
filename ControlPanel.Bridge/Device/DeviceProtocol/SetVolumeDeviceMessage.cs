using Mediator;
using MessagePack;

namespace ControlPanel.Bridge.Device.DeviceProtocol;

[MessagePackObject(true)]
public record SetVolumeDeviceMessage(
    [property: Key("id")] AudioStreamId Id, 
    [property: Key("volume")] float Volume)
    : DeviceMessage(MessageType.SetVolume), INotification;
