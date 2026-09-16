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

    internal const string ActionsKeyPrefix = "norn:platform:actions:";

    /// <summary>
    /// ADR-04, barreira (b) — janela deslizante via sorted set (score = instante Unix em ticks),
    /// para que <see cref="CountRecentActionsAsync"/> conte só o que ainda está dentro da janela
    /// sem varredura própria. TTL da chave acompanha a maior janela configurável (§5.4/ADR-04:
    /// 15 min) para não crescer sem limite entre execuções.
    /// </summary>
    public async Task RecordActionAsync(string target, CancellationToken cancellationToken)
    {
        var database = connectionMultiplexer.GetDatabase();
        var key = ActionsKeyPrefix + target;
        var now = DateTimeOffset.UtcNow;

        await database.SortedSetAddAsync(key, Guid.NewGuid().ToString(), now.ToUnixTimeMilliseconds());
        await database.KeyExpireAsync(key, TimeSpan.FromHours(1));
    }

    public async Task<int> CountRecentActionsAsync(string target, TimeSpan window, CancellationToken cancellationToken)
    {
        var database = connectionMultiplexer.GetDatabase();
        var key = ActionsKeyPrefix + target;
        var cutoff = DateTimeOffset.UtcNow - window;

        await database.SortedSetRemoveRangeByScoreAsync(key, double.NegativeInfinity, cutoff.ToUnixTimeMilliseconds());
        return (int)await database.SortedSetLengthAsync(key);
    }
}
