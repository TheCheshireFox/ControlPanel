using Mediator;
using MessagePack;

namespace ControlPanel.Bridge.Device.DeviceProtocol;

[MessagePackObject(true)]
public record GetIconDeviceMessage(
    [property: Key("source")] string Source,
    [property: Key("agent_id")] string AgentId)
    : DeviceMessage(MessageType.GetIcon), INotification;
