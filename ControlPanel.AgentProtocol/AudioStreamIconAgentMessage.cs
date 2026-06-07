using Mediator;

namespace ControlPanel.AgentProtocol;

public record AudioStreamIconAgentMessage(string Source, byte[] Icon, int IconHash)
    : AgentMessage(AgentMessageType.Icon), INotification;
