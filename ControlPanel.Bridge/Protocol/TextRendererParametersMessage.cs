using MessagePack;

namespace ControlPanel.Bridge.Protocol;

[MessagePackObject(true)]
public record TextRendererParametersMessage(
    [property: Key("dpi")] float Dpi,
    [property: Key("font_size")] int FontSize,
    [property: Key("max_sprite_width")] int MaxSpriteWidth)
    : Message(MessageType.TextRendererParameters);