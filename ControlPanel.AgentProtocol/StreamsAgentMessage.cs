using Mediator;

namespace ControlPanel.Protocol;

public record StreamsAgentMessage(AgentAudioStream[] Streams)
    : AgentMessage(AgentMessageType.Streams), INotification;
