namespace ControlPanel.Bridge.Protocol;

public record StreamsMessage(UartAudioStream[] Streams)
    : UartMessage(UartMessageType.Streams);