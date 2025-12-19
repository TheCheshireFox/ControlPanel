using MessagePack;

namespace ControlPanel.Bridge.Protocol;

[MessagePackObject(true)]
public record UartIconMessage(
    [property: Key("source")] string Source, 
    [property: Key("agent_id")] string AgentId, 
    [property: Key("icon")] byte[] Icon)
    : UartMessage(UartMessageType.Icon);