using MessagePack;

namespace ControlPanel.Bridge.Protocol;

[MessagePackObject(true)]
public record UartLogMessage([property: Key("line")] string Line)
    : UartMessage(UartMessageType.Log);