namespace ControlPanel.Bridge.Options;

public class AudioStreamIconCacheOptions
{
    public required TimeSpan CacheExpiry { get; init; }
    public required int CacheSizeKb { get; init; }
}