using Mediator;

namespace ControlPanel.Protocol;

public record GetIconMessage(string Source)
    : AgentMessage(AgentMessageType.GetIcon), INotification;
