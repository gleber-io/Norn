using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Hosting;
using StackExchange.Redis;

namespace Norn.Knowledge;

/// <summary>
/// A invalidação por pub/sub não é otimização (§5.7) — mesmo mecanismo do catálogo de flags do
/// Shop, mas em canal e prefixo separados: <c>mode</c> é lido por Norn.Worker e Norn.Executor,
/// <c>forecast</c> por Norn.Analyzer, e nenhum dos dois deve ver o outro invalidar por engano.
/// </summary>
internal sealed class PlatformConfigInvalidationSubscriber(IConnectionMultiplexer connectionMultiplexer, IMemoryCache cache)
    : IHostedService
{
    internal const string InvalidationChannel = "norn:platform:config:invalidate";

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var subscriber = connectionMultiplexer.GetSubscriber();
        await subscriber.SubscribeAsync(RedisChannel.Literal(InvalidationChannel), (_, message) =>
        {
            if (message.HasValue)
            {
                cache.Remove(RedisPlatformConfig.CacheKey(message.ToString()));
            }
        });
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        var subscriber = connectionMultiplexer.GetSubscriber();
        await subscriber.UnsubscribeAsync(RedisChannel.Literal(InvalidationChannel));
    }
}
