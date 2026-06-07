using Mediator;

namespace ControlPanel.Protocol;

public record InitAgentMessage(byte[] AgentIcon)
    : AgentMessage(AgentMessageType.AgentInit), INotification;
