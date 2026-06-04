using Microsoft.Extensions.Caching.Memory;

namespace ControlPanel.Bridge.Extensions;

public static class MemoryCacheExtensions
{
    public static void RemoveAll<T>(this IMemoryCache memoryCache, IEnumerable<T> keys)
        where T : notnull
    {
        foreach (var key in keys)
        {
            memoryCache.Remove(key);
        }
    }
}