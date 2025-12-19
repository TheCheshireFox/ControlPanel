namespace ControlPanel.Protocol;

public record AgentInitMessage(byte[] AgentIcon)
    : BridgeMessage(BridgeMessageType.AgentInit);