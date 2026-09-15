using Microsoft.Extensions.Caching.Memory;
using StackExchange.Redis;

namespace Norn.BuildingBlocks.Web.FeatureFlags;

internal sealed class RedisFeatureFlags(IConnectionMultiplexer connectionMultiplexer, IMemoryCache cache) : IFeatureFlags
{
    internal const string KeyPrefix = "shop:flags:";
    private static readonly TimeSpan CacheDuration = TimeSpan.FromSeconds(5);

    public async Task<bool> IsEnabledAsync(string flagName, CancellationToken cancellationToken)
    {
        var cacheKey = CacheKey(flagName);
        if (cache.TryGetValue(cacheKey, out bool cached))
        {
            return cached;
        }

        var database = connectionMultiplexer.GetDatabase();
        var value = await database.StringGetAsync(KeyPrefix + flagName);
        var enabled = value.HasValue && bool.TryParse(value.ToString(), out var parsed) && parsed;

        using var entry = cache.CreateEntry(cacheKey);
        entry.AbsoluteExpirationRelativeToNow = CacheDuration;
        entry.Value = enabled;

        return enabled;
    }

    internal static string CacheKey(string flagName) => $"feature-flag:{flagName}";
}
