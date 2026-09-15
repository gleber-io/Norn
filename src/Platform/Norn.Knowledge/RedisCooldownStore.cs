using Norn.Contracts.Ports;
using StackExchange.Redis;

namespace Norn.Knowledge;

/// <summary>
/// Estado quente de cooldown por alvo, no Redis (ADR-06, ADR-04). Barreira contra loop
/// patológico, separada das pré-condições por ação da §5.4 — cooldown não tem lugar em cache
/// local: precisa ser consistente entre reinícios do processo, e o TTL nativo do Redis já
/// implementa a expiração sem varredura própria.
/// </summary>
internal sealed class RedisCooldownStore(IConnectionMultiplexer connectionMultiplexer) : ICooldownStore
{
    internal const string KeyPrefix = "norn:platform:cooldown:";

    public async Task<bool> IsInCooldownAsync(string target, CancellationToken cancellationToken)
    {
        var database = connectionMultiplexer.GetDatabase();
        return await database.KeyExistsAsync(KeyPrefix + target);
    }

    public async Task SetCooldownAsync(string target, TimeSpan duration, CancellationToken cancellationToken)
    {
        var database = connectionMultiplexer.GetDatabase();
        await database.StringSetAsync(KeyPrefix + target, value: "1", expiry: duration);
    }
}
