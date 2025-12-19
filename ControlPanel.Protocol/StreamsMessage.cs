namespace ControlPanel.Protocol;

public record StreamsMessage(BridgeAudioStream[] Streams)
    : BridgeMessage(BridgeMessageType.Streams);