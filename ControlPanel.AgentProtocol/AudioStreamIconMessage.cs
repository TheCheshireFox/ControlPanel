using Mediator;

namespace ControlPanel.Protocol;

public record AudioStreamIconMessage(string Source, byte[] Icon, int IconHash)
    : AgentMessage(AgentMessageType.Icon), INotification;
