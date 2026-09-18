using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Norn.Contracts.Ports;
using StackExchange.Redis;

namespace Norn.Knowledge;

/// <summary>
/// Composição do adaptador Norn.Knowledge (ADR-17) — chamada por Norn.Worker e Norn.API, os dois
/// composition roots do processo da plataforma, e por Norn.Labeler (Fase 12), composition root de
/// campanha com a mesma exceção declarada no §4 do Master Plan.
/// </summary>
public static class KnowledgeServiceCollectionExtensions
{
    public static IServiceCollection AddNornKnowledge(
        this IServiceCollection services,
        IConfiguration configuration,
        IConnectionMultiplexer connectionMultiplexer)
    {
        services.AddDbContext<KnowledgeDbContext>(options =>
            options.UseNpgsql(configuration.GetConnectionString("Knowledge")
                ?? "Host=localhost;Database=norn;Username=norn;Password=norn"));

        services.AddMemoryCache();
        services.AddSingleton(connectionMultiplexer);
        services.AddScoped<IKnowledgeStore, KnowledgeStore>();
        services.AddScoped<IKnowledgeReader, KnowledgeReader>();
        services.AddSingleton<ICooldownStore, RedisCooldownStore>();
        services.AddSingleton<IPlatformConfig, RedisPlatformConfig>();
        services.AddSingleton<IFeatureFlagWriter, RedisFeatureFlagWriter>();
        services.AddScoped<IExperimentRunStore, ExperimentRunStore>();
        services.AddHostedService<PlatformConfigInvalidationSubscriber>();

        return services;
    }
}
