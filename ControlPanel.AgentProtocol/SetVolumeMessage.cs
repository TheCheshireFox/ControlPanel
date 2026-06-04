using Mediator;

namespace ControlPanel.Protocol;

public record SetVolumeMessage(string Id, double Volume)
    : AgentMessage(AgentMessageType.SetVolume), INotification;
