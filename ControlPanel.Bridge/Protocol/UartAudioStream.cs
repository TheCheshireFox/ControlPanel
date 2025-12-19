using MessagePack;

namespace ControlPanel.Bridge.Protocol;

[MessagePackObject(true)]
public record UartAudioStreamId(
    [property: Key("id")] string Id,
    [property: Key("agent_id")] string AgentId);

[MessagePackObject(true)]
public record UartAudioStreamNameSprite(
    [property: Key("name")] string Name, 
    [property: Key("sprite")] byte[] Sprite, 
    [property: Key("width")] int Width, 
    [property: Key("height")] int Height);

[MessagePackObject(true)]
public record UartAudioStream(
    [property: Key("id")] UartAudioStreamId Id, 
    [property: Key("source")] string Source, 
    [property: Key("name")] UartAudioStreamNameSprite? Name, 
    [property: Key("mute")] bool? Mute, 
    [property: Key("volume")] double? Volume);