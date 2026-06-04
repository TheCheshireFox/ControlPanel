using MessagePack;

namespace ControlPanel.Bridge.Device.DeviceProtocol;

[MessagePackObject(true)]
public record AudioStreamId(
    [property: Key("id")] string Id,
    [property: Key("agent_id")] string AgentId);

[MessagePackObject(true)]
public record AudioStream(
    [property: Key("id")] AudioStreamId Id, 
    [property: Key("source")] string Source, 
    [property: Key("name")] string? Name, 
    [property: Key("mute")] bool? Mute, 
    [property: Key("volume")] double? Volume,
    [property: Key("icon_hash")] int IconHash);