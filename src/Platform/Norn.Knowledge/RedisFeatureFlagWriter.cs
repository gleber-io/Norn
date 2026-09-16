using System.Globalization;
using Norn.Contracts.Ports;
using StackExchange.Redis;

namespace Norn.Knowledge;

/// <summary>
/// Escrita no catálogo de flags do Shop, sob <c>shop:flags:</c> (§5.4, §5.7) — implementa
/// <see cref="IFeatureFlagWriter"/>, consumida só pelo <c>ToggleFeatureFlag</c> (Norn.Executor,
/// Fase 9). Prefixo e canal de invalidação **precisam** ser os mesmos que
/// <c>Norn.BuildingBlocks.Web.FeatureFlags.RedisFeatureFlags</c>/<c>FeatureFlagInvalidationSubscriber</c>
/// leem — os dois lados não compartilham projeto (o Shop nunca referencia Norn.Contracts.Ports),
/// então a amarra é o literal da chave, documentada nos dois lugares.
/// </summary>
internal sealed class RedisFeatureFlagWriter(IConnectionMultiplexer connectionMultiplexer) : IFeatureFlagWriter
{
    internal const string KeyPrefix = "shop:flags:";
    internal const string InvalidationChannel = "shop:flags:invalidate";

    public async Task SetAsync(string flagName, bool value, CancellationToken cancellationToken)
    {
        var database = connectionMultiplexer.GetDatabase();
        await database.StringSetAsync(KeyPrefix + flagName, value.ToString(CultureInfo.InvariantCulture));

        var subscriber = connectionMultiplexer.GetSubscriber();
        await subscriber.PublishAsync(RedisChannel.Literal(InvalidationChannel), flagName);
    }

    public async Task<bool> ExistsAsync(string flagName, CancellationToken cancellationToken)
    {
        var database = connectionMultiplexer.GetDatabase();
        return await database.KeyExistsAsync(KeyPrefix + flagName);
    }

    public async Task<bool> PingAsync(CancellationToken cancellationToken)
    {
        try
        {
            var database = connectionMultiplexer.GetDatabase();
            await database.PingAsync();
            return true;
        }
        catch (RedisConnectionException)
        {
            return false;
        }
    }
}
