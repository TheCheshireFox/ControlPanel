namespace ControlPanel.Bridge.Protocol;

public record SetVolumeMessage(string Id, string AgentId, double Volume)
    : UartMessage(UartMessageType.SetVolume);