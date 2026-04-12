using MessagePack;

namespace ControlPanel.Bridge.Protocol;

[MessagePackObject(true)]
public record IconMessage(
    [property: Key("source")] string Source,
    [property: Key("agent_id")] string AgentId,
    [property: Key("size")] int Size,
    [property: Key("icon")] byte[] Icon)
    : Message(MessageType.Icon);