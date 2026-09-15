using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Hosting;
using StackExchange.Redis;

namespace Norn.BuildingBlocks.Web.FeatureFlags;

/// <summary>
/// A invalidação por pub/sub não é otimização: o intervalo entre o `SET` do Executor e o efeito
/// aqui entra no MTTR medido do F3 (§5.7). TTL de cache sozinho viraria parcela constante do MTTR.
/// </summary>
internal sealed class FeatureFlagInvalidationSubscriber(IConnectionMultiplexer connectionMultiplexer, IMemoryCache cache)
    : IHostedService
{
    internal const string InvalidationChannel = "shop:flags:invalidate";

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var subscriber = connectionMultiplexer.GetSubscriber();
        await subscriber.SubscribeAsync(RedisChannel.Literal(InvalidationChannel), (_, message) =>
        {
            if (message.HasValue)
            {
                cache.Remove(RedisFeatureFlags.CacheKey(message.ToString()));
            }
        });
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        var subscriber = connectionMultiplexer.GetSubscriber();
        await subscriber.UnsubscribeAsync(RedisChannel.Literal(InvalidationChannel));
    }
}
