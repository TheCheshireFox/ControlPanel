using Mediator;

namespace ControlPanel.Protocol;

public record AgentInitMessage(byte[] AgentIcon)
    : AgentMessage(AgentMessageType.AgentInit), INotification;
