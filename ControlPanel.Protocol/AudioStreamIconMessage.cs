namespace ControlPanel.Protocol;

public record AudioStreamIconMessage(string Source, byte[] Icon)
    : BridgeMessage(BridgeMessageType.Icon);