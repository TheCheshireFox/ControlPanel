using Mediator;

namespace ControlPanel.Protocol;

public record SetVolumeAgentMessage(string Id, double Volume)
    : AgentMessage(AgentMessageType.SetVolume), INotification;
