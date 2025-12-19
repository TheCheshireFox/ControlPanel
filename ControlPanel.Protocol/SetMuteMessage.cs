namespace ControlPanel.Protocol;

public record SetMuteMessage(string Id, bool Mute)
    : BridgeMessage(BridgeMessageType.SetMute);