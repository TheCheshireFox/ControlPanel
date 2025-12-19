using MessagePack;

namespace ControlPanel.Bridge.Protocol;

[MessagePackObject(true)]
public record UartGetIconMessage(
    [property: Key("source")] string Source,
    [property: Key("agent_id")] string AgentId)
    : UartMessage(UartMessageType.GetIcon);