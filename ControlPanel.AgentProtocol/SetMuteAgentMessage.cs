using Mediator;

namespace ControlPanel.AgentProtocol;

public record SetMuteAgentMessage(string Id, bool Mute)
    : AgentMessage(AgentMessageType.SetMute), INotification;
