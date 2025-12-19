using MessagePack;

namespace ControlPanel.Bridge.Protocol;

[MessagePackObject(true)]
public record UartRequestRefreshMessage()
    : UartMessage(UartMessageType.RequestRefresh);