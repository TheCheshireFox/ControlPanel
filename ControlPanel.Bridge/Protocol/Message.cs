using MessagePack;

namespace ControlPanel.Bridge.Protocol;

[Union(0, typeof(GetIconMessage))]
[Union(1, typeof(IconMessage))]
[Union(2, typeof(RequestRefreshMessage))]
[Union(3, typeof(SetMuteMessage))]
[Union(4, typeof(SetVolumeMessage))]
[Union(5, typeof(StreamsMessage))]
[Union(6, typeof(TextRendererParametersMessage))]
[MessagePackObject(true)]
public abstract record Message([property: Key("type")] MessageType Type);