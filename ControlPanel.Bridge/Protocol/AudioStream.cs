using MessagePack;

namespace ControlPanel.Bridge.Protocol;

[MessagePackObject(true)]
public record AudioStreamId(
    [property: Key("id")] string Id,
    [property: Key("agent_id")] string AgentId);

[MessagePackObject(true)]
public record AudioStreamNameSprite(
    [property: Key("name")] string Name, 
    [property: Key("sprite")] byte[] Sprite, 
    [property: Key("width")] int Width, 
    [property: Key("height")] int Height);

[MessagePackObject(true)]
public record AudioStream(
    [property: Key("id")] AudioStreamId Id, 
    [property: Key("source")] string Source, 
    [property: Key("name")] AudioStreamNameSprite? Name, 
    [property: Key("mute")] bool? Mute, 
    [property: Key("volume")] double? Volume);