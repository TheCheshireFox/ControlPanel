namespace ControlPanel.Protocol;

public record SetVolumeMessage(string Id, double Volume)
    : BridgeMessage(BridgeMessageType.SetVolume);