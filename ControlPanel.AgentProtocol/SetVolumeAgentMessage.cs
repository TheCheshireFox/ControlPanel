using Mediator;

namespace ControlPanel.AgentProtocol;

public record SetVolumeAgentMessage(string Id, double Volume)
    : AgentMessage(AgentMessageType.SetVolume), INotification;
