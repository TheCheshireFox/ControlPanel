using Mediator;

namespace ControlPanel.AgentProtocol;

public record InitAgentMessage(byte[] AgentIcon)
    : AgentMessage(AgentMessageType.AgentInit), INotification;
