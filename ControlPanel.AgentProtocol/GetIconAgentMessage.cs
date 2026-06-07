using Mediator;

namespace ControlPanel.AgentProtocol;

public record GetIconAgentMessage(string Source)
    : AgentMessage(AgentMessageType.GetIcon), INotification;
