namespace ControlPanel.Bridge.Options;

public class TextRendererOptions
{
    public required string FontFamily { get; init; }
    public required int FontSize { get; init; }
    public required int MaxWidth { get; init; }
    public required float Dpi { get; init; }
    public required TimeSpan CacheExpiry { get; init; }
    public required int CacheSizeKb { get; init; }
}