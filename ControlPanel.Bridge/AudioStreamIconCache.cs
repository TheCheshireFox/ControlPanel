using ControlPanel.Bridge.Options;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace ControlPanel.Bridge;

public interface IAudioStreamIconCache
{
    bool TryGetIcon(string source, string agentId, out byte[] icon);
    void AddIcon(string source, string agentId, byte[] icon);
    void RemoveIcon(string source, string agentId);
}

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

    public bool TryGetIcon(string source, string agentId, out byte[] icon)
        => _iconCache.TryGetValue((source, agentId), out icon!);

    public void AddIcon(string source, string agentId, byte[] icon)
        => _iconCache.Set((source, agentId), icon, new MemoryCacheEntryOptions { SlidingExpiration = _cacheExpiry, Size = icon.Length });
    
    public void RemoveIcon(string source, string agentId)
        => _iconCache.Remove((source, agentId));
}