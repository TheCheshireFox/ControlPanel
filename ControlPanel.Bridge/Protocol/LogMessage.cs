using MessagePack;

namespace ControlPanel.Bridge.Protocol;

[MessagePackObject(true)]
public record LogMessage([property: Key("line")] string Line)
    : Message(MessageType.Log);