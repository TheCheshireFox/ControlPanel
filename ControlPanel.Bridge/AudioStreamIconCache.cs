using ControlPanel.Bridge.Extensions;
using ControlPanel.Bridge.Options;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace ControlPanel.Bridge;

public interface IAudioStreamIconCache
{
    bool TryGetIcon(string source, string agentId, int iconHash, out AudioCacheIcon icon);
    void AddIcon(string source, string agentId, int iconHash, AudioCacheIcon icon);
    void RemoveIcons(string agentId);
    void RemoveIcon(string source, string agentId);
}

public record AudioCacheIcon(int Size, byte[] Icon);

public class AudioStreamIconCache(IOptions<AudioStreamIconCacheOptions> options) : IAudioStreamIconCache
{
    private readonly TimeSpan _cacheExpiry = options.Value.CacheExpiry;
    private readonly MemoryCache _iconCache = new(new MemoryCacheOptions
    {
        SizeLimit = options.Value.CacheSizeKb * 1024
    });

    public bool TryGetIcon(string source, string agentId, int iconHash, out AudioCacheIcon icon)
        => _iconCache.TryGetValue(new IconKey(source, agentId, iconHash), out icon!);

    public void AddIcon(string source, string agentId, int iconHash, AudioCacheIcon icon)
        => _iconCache.Set(new IconKey(source, agentId, iconHash), icon, new MemoryCacheEntryOptions
        {
            SlidingExpiration = _cacheExpiry,
            Size = icon.Icon.Length
        });

    public void RemoveIcons(string agentId)
        => _iconCache.RemoveAll(_iconCache.Keys.Cast<IconKey>().Where(x => x.AgentId == agentId));

    public void RemoveIcon(string source, string agentId)
        => _iconCache.RemoveAll(_iconCache.Keys.Cast<IconKey>().Where(x => x.AgentId == agentId && x.Source == source));

    private record IconKey(string Source, string AgentId, int IconHash);
}