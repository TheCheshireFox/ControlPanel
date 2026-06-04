using MessagePack;

namespace ControlPanel.Bridge.Device.DeviceProtocol;

[MessagePackObject(true)]
public record IconDeviceMessage(
    [property: Key("source")] string Source,
    [property: Key("agent_id")] string AgentId,
    [property: Key("size")] int Size,
    [property: Key("icon")] byte[] Icon)
    : DeviceMessage(MessageType.Icon);