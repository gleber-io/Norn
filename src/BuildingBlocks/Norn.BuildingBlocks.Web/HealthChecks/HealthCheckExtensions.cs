using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Norn.BuildingBlocks.Web.HealthChecks;

/// <summary>
/// <c>/health/live</c> não avalia nenhum check registrado — só confirma que o processo responde.
/// <c>/health/ready</c> avalia os checks marcados com a tag <c>"ready"</c> (§5.2, §7.4).
/// </summary>
public static class HealthCheckExtensions
{
    public const string ReadyTag = "ready";

    public static IHealthChecksBuilder AddNornHealthChecks(this IServiceCollection services) =>
        services.AddHealthChecks();

    /// <summary>Registra a checagem genérica de <see cref="PostgresReadinessHealthCheck{TContext}"/> sob a tag <see cref="ReadyTag"/>.</summary>
    public static IHealthChecksBuilder AddPostgresReadiness<TContext>(this IHealthChecksBuilder builder, string name = "postgres")
        where TContext : DbContext =>
        builder.AddCheck<PostgresReadinessHealthCheck<TContext>>(name, tags: [ReadyTag]);

    public static IEndpointRouteBuilder MapNornHealthChecks(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapHealthChecks("/health/live", new HealthCheckOptions
        {
            Predicate = static _ => false,
        });

        endpoints.MapHealthChecks("/health/ready", new HealthCheckOptions
        {
            Predicate = static check => check.Tags.Contains(ReadyTag),
        });

        return endpoints;
    }
}
