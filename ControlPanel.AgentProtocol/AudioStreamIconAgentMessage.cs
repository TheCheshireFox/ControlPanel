using Mediator;

namespace ControlPanel.Protocol;

public record AudioStreamIconAgentMessage(string Source, byte[] Icon, int IconHash)
    : AgentMessage(AgentMessageType.Icon), INotification;
