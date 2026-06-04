using Mediator;
using MessagePack;

namespace ControlPanel.Bridge.Device.DeviceProtocol;

[MessagePackObject(true)]
public record RequestRefreshDeviceMessage()
    : DeviceMessage(MessageType.RequestRefresh), INotification;
