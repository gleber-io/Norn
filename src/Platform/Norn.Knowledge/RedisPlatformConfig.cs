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
