using Mediator;

namespace ControlPanel.Protocol;

public record AudioStreamIconMessage(string Source, byte[] Icon)
    : AgentMessage(AgentMessageType.Icon), INotification;
