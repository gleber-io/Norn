using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Norn.BuildingBlocks.Web.HealthChecks;

/// <summary>
/// Checagem de prontidão genérica por <c>CanConnectAsync</c> — os três serviços do Shop repetiam
/// a mesma classe byte a byte, diferindo só no tipo do <see cref="DbContext"/>.
/// </summary>
internal sealed class PostgresReadinessHealthCheck<TContext>(TContext dbContext) : IHealthCheck
    where TContext : DbContext
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        var canConnect = await dbContext.Database.CanConnectAsync(cancellationToken);

        return canConnect
            ? HealthCheckResult.Healthy()
            : HealthCheckResult.Unhealthy("Não foi possível conectar ao PostgreSQL.");
    }
}
