using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Norn.Contracts.Ports;
using StackExchange.Redis;

namespace Norn.Knowledge;

/// <summary>
/// Configuração da plataforma sob <c>norn:platform:config:</c> (§5.7, ADR-16) — árvore separada
/// do catálogo de flags do Shop, para que o Norn nunca desligue o próprio preditor como "ação de
/// cura". Cache curto invalidado por pub/sub, mesmo mecanismo do <c>ToggleFeatureFlag</c>
/// (<see cref="Norn.BuildingBlocks.Web.FeatureFlags.RedisFeatureFlags"/>), prefixos e donos
/// distintos.
/// </summary>
internal sealed partial class RedisPlatformConfig(
    IConnectionMultiplexer connectionMultiplexer,
    IMemoryCache cache,
    ILogger<RedisPlatformConfig> logger) : IPlatformConfig
{
    internal const string KeyPrefix = "norn:platform:config:";
    internal const string ModeKey = "mode";
    internal const string ForecastKey = "forecast";
    internal const string PlannerBackendKey = "plannerBackend";
    private static readonly TimeSpan CacheDuration = TimeSpan.FromSeconds(5);

    public async Task<PlatformMode> GetModeAsync(CancellationToken cancellationToken)
    {
        var cacheKey = CacheKey(ModeKey);
        if (cache.TryGetValue(cacheKey, out PlatformMode cached))
        {
            return cached;
        }

        var database = connectionMultiplexer.GetDatabase();
        var value = await database.StringGetAsync(KeyPrefix + ModeKey);

        // Sem intervenção, o comportamento é o mais conservador do catálogo: detecta e planeja,
        // nunca atua (ADR-05) — mesmo default que a ausência de configuração deve produzir.
        var mode = value.HasValue && Enum.TryParse<PlatformMode>(value.ToString(), out var parsed)
            ? parsed
            : PlatformMode.Observe;

        Store(cacheKey, mode);
        return mode;
    }

    public async Task<ForecastConfig> GetForecastConfigAsync(CancellationToken cancellationToken)
    {
        var cacheKey = CacheKey(ForecastKey);
        if (cache.TryGetValue(cacheKey, out ForecastConfig? cached) && cached is not null)
        {
            return cached;
        }

        var database = connectionMultiplexer.GetDatabase();
        var value = await database.StringGetAsync(KeyPrefix + ForecastKey);

        var forecast = ForecastConfigParser.ParseOrDefault(value.HasValue ? value.ToString() : null, out var wasInvalid);
        if (wasInvalid)
        {
            LogInvalidForecastConfig(logger, value.ToString());
        }

        Store(cacheKey, forecast);
        return forecast;
    }

    /// <summary>
    /// Fase 10 (<c>PUT /api/v1/mode</c>) e Fase 9 (circuit breaker, ADR-04 barreira c). Publica no
    /// mesmo canal que <see cref="PlatformConfigInvalidationSubscriber"/> assina — sem isso, o
    /// próprio processo que acabou de escrever continuaria servindo o modo antigo da cache local
    /// pelos próximos <see cref="CacheDuration"/>.
    /// </summary>
    public async Task SetModeAsync(PlatformMode mode, CancellationToken cancellationToken)
    {
        var database = connectionMultiplexer.GetDatabase();
        await database.StringSetAsync(KeyPrefix + ModeKey, mode.ToString());

        var subscriber = connectionMultiplexer.GetSubscriber();
        await subscriber.PublishAsync(RedisChannel.Literal(PlatformConfigInvalidationSubscriber.InvalidationChannel), ModeKey);
    }

    /// <summary>Fase 12 — reset de estado do <c>run-experiment.ps1</c> (defensivo, tarefa 2a).</summary>
    public async Task SetForecastConfigAsync(ForecastConfig config, CancellationToken cancellationToken)
    {
        var database = connectionMultiplexer.GetDatabase();
        await database.StringSetAsync(KeyPrefix + ForecastKey, System.Text.Json.JsonSerializer.Serialize(config));

        var subscriber = connectionMultiplexer.GetSubscriber();
        await subscriber.PublishAsync(RedisChannel.Literal(PlatformConfigInvalidationSubscriber.InvalidationChannel), ForecastKey);
    }

    public async Task<PlannerBackend> GetPlannerBackendAsync(CancellationToken cancellationToken)
    {
        var cacheKey = CacheKey(PlannerBackendKey);
        if (cache.TryGetValue(cacheKey, out PlannerBackend cached))
        {
            return cached;
        }

        var database = connectionMultiplexer.GetDatabase();
        var value = await database.StringGetAsync(KeyPrefix + PlannerBackendKey);

        // Sem intervenção, mantém o comportamento de todas as sessões anteriores à Fase 12: LLM
        // com fallback interno para RuleEngine (§5.5), nunca RuleEngine forçado.
        var backend = value.HasValue && Enum.TryParse<PlannerBackend>(value.ToString(), out var parsed)
            ? parsed
            : PlannerBackend.Llm;

        Store(cacheKey, backend);
        return backend;
    }

    /// <summary>Fase 12 — reset de estado do <c>run-experiment.ps1</c> antes de cada execução (tarefa 2a).</summary>
    public async Task SetPlannerBackendAsync(PlannerBackend backend, CancellationToken cancellationToken)
    {
        var database = connectionMultiplexer.GetDatabase();
        await database.StringSetAsync(KeyPrefix + PlannerBackendKey, backend.ToString());

        var subscriber = connectionMultiplexer.GetSubscriber();
        await subscriber.PublishAsync(RedisChannel.Literal(PlatformConfigInvalidationSubscriber.InvalidationChannel), PlannerBackendKey);
    }

    private void Store<T>(string cacheKey, T value)
    {
        using var entry = cache.CreateEntry(cacheKey);
        entry.AbsoluteExpirationRelativeToNow = CacheDuration;
        entry.Value = value;
    }

    internal static string CacheKey(string configKey) => $"platform-config:{configKey}";

    [LoggerMessage(Level = LogLevel.Error, Message = "Configuração de forecast inválida em norn:platform:config:forecast ({RawValue}) — mantendo desligado.")]
    private static partial void LogInvalidForecastConfig(ILogger logger, string rawValue);
}
