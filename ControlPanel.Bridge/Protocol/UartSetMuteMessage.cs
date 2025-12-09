namespace ControlPanel.Bridge.Protocol;

public record SetMuteMessage(string Id, string AgentId, bool Mute)
    : UartMessage(UartMessageType.SetMute);