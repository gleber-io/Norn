using Microsoft.Extensions.Diagnostics.HealthChecks;
using StackExchange.Redis;

namespace Norn.API.Events;

/// <summary>
/// Cobre a falha silenciosa da Fase 10: "assinante caído com API no ar". Unhealthy se a conexão
/// Redis caiu ou se <see cref="PlatformEventRelay"/> nunca chegou a assinar/perdeu a assinatura —
/// nunca por ausência de tráfego (ver <see cref="NornEventRelayHealthState"/>).
/// </summary>
internal sealed class NornEventRelayHealthCheck(
    NornEventRelayHealthState healthState,
    IConnectionMultiplexer connectionMultiplexer) : IHealthCheck
{
    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        if (!connectionMultiplexer.IsConnected)
        {
            return Task.FromResult(HealthCheckResult.Unhealthy("Conexão Redis indisponível."));
        }

        return Task.FromResult(healthState.IsSubscribed
            ? HealthCheckResult.Healthy()
            : HealthCheckResult.Unhealthy("Assinante de norn:events não está ativo."));
    }
}
