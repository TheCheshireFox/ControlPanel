using Mediator;

namespace ControlPanel.AgentProtocol;

public record StreamsAgentMessage(AgentAudioStream[] Streams)
    : AgentMessage(AgentMessageType.Streams), INotification;
