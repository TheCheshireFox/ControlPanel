namespace ControlPanel.Protocol;

public record GetIconMessage(string Source)
    : BridgeMessage(BridgeMessageType.GetIcon);