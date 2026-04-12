using MessagePack;

namespace ControlPanel.Bridge.Protocol;

[MessagePackObject(true)]
public record StreamsMessage(
    [property: Key("updated")] AudioStream[] Updated,
    [property: Key("deleted")] AudioStreamId[] Deleted)
    : Message(MessageType.Streams);