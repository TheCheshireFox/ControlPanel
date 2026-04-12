namespace ControlPanel.Bridge.Options;

public class AudioStreamIconCacheOptions
{
    public required TimeSpan CacheExpiry { get; init; } = TimeSpan.FromHours(1);
    public required int CacheSizeKb { get; init; } = 1204;
}