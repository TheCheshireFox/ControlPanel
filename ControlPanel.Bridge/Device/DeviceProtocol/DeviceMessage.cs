using MessagePack;

namespace ControlPanel.Bridge.Device.DeviceProtocol;

[Union(0, typeof(GetIconDeviceMessage))]
[Union(1, typeof(IconDeviceMessage))]
[Union(2, typeof(RequestRefreshDeviceMessage))]
[Union(3, typeof(SetMuteDeviceMessage))]
[Union(4, typeof(SetVolumeDeviceMessage))]
[Union(5, typeof(StreamsDeviceMessage))]
[MessagePackObject(true)]
public abstract record DeviceMessage([property: Key("type")] MessageType Type);