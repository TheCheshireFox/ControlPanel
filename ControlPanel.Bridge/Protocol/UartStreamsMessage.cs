using MessagePack;

namespace ControlPanel.Bridge.Protocol;

[MessagePackObject(true)]
public record UartStreamsMessage(
    [property: Key("updated")] UartAudioStream[] Updated,
    [property: Key("deleted")] UartAudioStreamId[] Deleted)
    : UartMessage(UartMessageType.Streams);