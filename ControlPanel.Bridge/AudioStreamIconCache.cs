using ControlPanel.Bridge.Options;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace ControlPanel.Bridge;

public interface IAudioStreamIconCache
{
    bool TryGetIcon(string source, string agentId, out AudioCacheIcon icon);
    void AddIcon(string source, string agentId, AudioCacheIcon icon);
    void RemoveIcons(string agentId);
    void RemoveIcon(string agentId, string source);
}

public record AudioCacheIcon(int Size, byte[] Icon);

public class AudioStreamIconCache : IAudioStreamIconCache
{
    private readonly TimeSpan _cacheExpiry;
    private readonly MemoryCache _iconCache;

    public AudioStreamIconCache(IOptions<AudioStreamIconCacheOptions> options)
    {
        _iconCache = new MemoryCache(new MemoryCacheOptions
        {
            SizeLimit = options.Value.CacheSizeKb * 1024
        });

        _cacheExpiry =  options.Value.CacheExpiry;
    }

    public bool TryGetIcon(string source, string agentId, out AudioCacheIcon icon)
        => _iconCache.TryGetValue((source, agentId), out icon!);

    public void AddIcon(string source, string agentId, AudioCacheIcon icon)
        => _iconCache.Set((source, agentId), icon, new MemoryCacheEntryOptions { SlidingExpiration = _cacheExpiry, Size = icon.Icon.Length });
    
    public void RemoveIcons(string agentId)
    {
        foreach (var (s, a) in _iconCache.Keys.Cast<(string, string)>())
        {
            if (a == agentId)
                _iconCache.Remove((s, a));
        }
    }

    public void RemoveIcon(string agentId, string source)
    {
        _iconCache.Remove((source, agentId));
    }
}