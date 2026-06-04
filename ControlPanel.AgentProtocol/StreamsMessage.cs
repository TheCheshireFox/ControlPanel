using Mediator;

namespace ControlPanel.Protocol;

public record StreamsMessage(AgentAudioStream[] Streams)
    : AgentMessage(AgentMessageType.Streams), INotification;
