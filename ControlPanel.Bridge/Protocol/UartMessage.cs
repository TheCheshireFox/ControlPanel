using MessagePack;

namespace ControlPanel.Bridge.Protocol;

[Union(0, typeof(UartGetIconMessage))]
[Union(1, typeof(UartIconMessage))]
[Union(2, typeof(UartRequestRefreshMessage))]
[Union(3, typeof(UartSetMuteMessage))]
[Union(4, typeof(UartSetVolumeMessage))]
[Union(5, typeof(UartStreamsMessage))]
[MessagePackObject(true)]
public abstract record UartMessage([property: Key("type")] UartMessageType Type);