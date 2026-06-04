using Mediator;

namespace ControlPanel.Protocol;

public record SetMuteMessage(string Id, bool Mute)
    : AgentMessage(AgentMessageType.SetMute), INotification;
