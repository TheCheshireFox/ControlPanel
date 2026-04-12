using MessagePack;

namespace ControlPanel.Bridge.Protocol;

[MessagePackObject(true)]
public record GetIconMessage(
    [property: Key("source")] string Source,
    [property: Key("agent_id")] string AgentId)
    : Message(MessageType.GetIcon);