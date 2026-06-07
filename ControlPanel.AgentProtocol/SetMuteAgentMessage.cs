using Mediator;

namespace ControlPanel.Protocol;

public record SetMuteAgentMessage(string Id, bool Mute)
    : AgentMessage(AgentMessageType.SetMute), INotification;
