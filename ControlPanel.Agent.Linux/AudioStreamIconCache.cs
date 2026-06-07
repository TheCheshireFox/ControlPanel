using ControlPanel.Agent.Shared;
using Microsoft.Extensions.Caching.Memory;

namespace ControlPanel.Agent.Linux;

internal sealed class AudioStreamIconCache(TimeSpan expiration, int size) : IDisposable
{
    private readonly MemoryCache _iconCache = new(new MemoryCacheOptions
    {
        SizeLimit = size
    });

    public async Task<AudioStreamIcon> GetOrAddAsync(string source, Func<CancellationToken, Task<AudioStreamIcon>> iconFactory, CancellationToken cancellationToken)
    {
        if (_iconCache.TryGetValue<AudioStreamIcon>(source, out var cached) && cached != null)
            return cached;
        
        return _iconCache.Set(source, await iconFactory(cancellationToken), new MemoryCacheEntryOptions
        {
            SlidingExpiration = expiration,
            Size = size
        });
    }

    public void Dispose()
    {
        _iconCache.Dispose();
    }
}