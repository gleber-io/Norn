using System.Globalization;
using StackExchange.Redis;

namespace Norn.BuildingBlocks.Chaos;

internal sealed class RedisChaosActivationStore(IConnectionMultiplexer connectionMultiplexer) : IChaosActivationStore
{
    internal const string Key = "norn:chaos:active";

    private const string ScenarioIdField = "scenarioId";
    private const string SeedField = "seed";
    private const string ActivatedAtUtcField = "activatedAtUtc";
    private const string FiredAtUtcField = "firedAtUtc";

    public async Task ActivateAsync(string scenarioId, int seed, DateTimeOffset activatedAtUtc, CancellationToken cancellationToken)
    {
        var database = connectionMultiplexer.GetDatabase();
        await database.KeyDeleteAsync(Key);
        await database.HashSetAsync(Key,
        [
            new HashEntry(ScenarioIdField, scenarioId),
            new HashEntry(SeedField, seed),
            new HashEntry(ActivatedAtUtcField, activatedAtUtc.ToString("O", CultureInfo.InvariantCulture)),
        ]);
    }

    public async Task DeactivateAsync(CancellationToken cancellationToken)
    {
        var database = connectionMultiplexer.GetDatabase();
        await database.KeyDeleteAsync(Key);
    }

    public async Task<ChaosActivation?> GetActiveAsync(CancellationToken cancellationToken)
    {
        var database = connectionMultiplexer.GetDatabase();
        var entries = await database.HashGetAllAsync(Key);
        if (entries.Length == 0)
        {
            return null;
        }

        var map = entries.ToDictionary(e => e.Name.ToString(), e => e.Value);

        if (!map.TryGetValue(ScenarioIdField, out var scenarioIdValue) || !scenarioIdValue.HasValue)
        {
            return null;
        }

        var scenarioId = scenarioIdValue.ToString();
        var seed = map.TryGetValue(SeedField, out var seedValue) && seedValue.HasValue ? (int)seedValue : 0;
        var activatedAtUtc = map.TryGetValue(ActivatedAtUtcField, out var activatedValue) && activatedValue.HasValue
            ? DateTimeOffset.ParseExact(activatedValue.ToString(), "O", CultureInfo.InvariantCulture)
            : DateTimeOffset.UtcNow;
        DateTimeOffset? firedAtUtc = map.TryGetValue(FiredAtUtcField, out var firedValue) && firedValue.HasValue
            ? DateTimeOffset.ParseExact(firedValue.ToString(), "O", CultureInfo.InvariantCulture)
            : null;

        return new ChaosActivation(scenarioId, seed, activatedAtUtc, firedAtUtc);
    }

    public async Task MarkFiredAsync(DateTimeOffset firedAtUtc, CancellationToken cancellationToken)
    {
        var database = connectionMultiplexer.GetDatabase();
        await database.HashSetAsync(Key, FiredAtUtcField, firedAtUtc.ToString("O", CultureInfo.InvariantCulture));
    }
}
