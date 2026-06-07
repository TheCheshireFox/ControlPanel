using Mediator;

namespace ControlPanel.Protocol;

public record GetIconAgentMessage(string Source)
    : AgentMessage(AgentMessageType.GetIcon), INotification;
