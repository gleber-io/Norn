using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Norn.BuildingBlocks.Chaos.Effects;
using Norn.BuildingBlocks.Chaos.Scenarios;
using StackExchange.Redis;

namespace Norn.BuildingBlocks.Chaos;

public static class ChaosServiceCollectionExtensions
{
    /// <summary>
    /// Registro idêntico nos três serviços do Shop (§8, Fase 5) — cada um recebe os quatro
    /// cenários e efeitos, e o <c>ChaosBackgroundService</c> descarta em runtime os que não miram
    /// <paramref name="serviceName"/>. Restrição do ADR-13: nunca chamar de <c>Norn.Platform.*</c>.
    /// </summary>
    public static IServiceCollection AddNornChaos(
        this IServiceCollection services,
        IConnectionMultiplexer connectionMultiplexer,
        IConfiguration configuration,
        string serviceName)
    {
        services.AddOptions<ChaosScenarioOptions>()
            .Bind(configuration.GetSection(ChaosScenarioOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddSingleton(connectionMultiplexer);
        services.AddSingleton<IChaosActivationStore, RedisChaosActivationStore>();
        services.AddSingleton<ChaosRuntimeState>();

        services.AddSingleton<IChaosScenario, F1MemoryRetentionScenario>();
        services.AddSingleton<IChaosScenario, F2ConnectionPoolScenario>();
        services.AddSingleton<IChaosScenario, F3GatewayLatencyScenario>();
        services.AddSingleton<IChaosScenario, F5AbruptKillScenario>();

        services.AddSingleton<IChaosEffect, MemoryRetentionEffect>();
        services.AddSingleton<IChaosEffect, ConcurrencyThrottleEffect>();
        services.AddSingleton<IChaosEffect, AbruptKillEffect>();

        // GatewayLatencyEffect precisa ser a MESMA instância sob os dois contratos: é o
        // ChaosBackgroundService quem atualiza o delay a cada tick (via IChaosEffect), e é o
        // SimulatedPaymentGateway do Payment.API quem lê o valor atual (via IChaosGatewayDelay,
        // ver o porquê nesse arquivo). Duas instâncias separadas deixariam o Payment.API sempre
        // lendo delay zero.
        services.AddSingleton<GatewayLatencyEffect>();
        services.AddSingleton<IChaosEffect>(sp => sp.GetRequiredService<GatewayLatencyEffect>());
        services.AddSingleton<IChaosGatewayDelay>(sp => sp.GetRequiredService<GatewayLatencyEffect>());

        services.AddHostedService(sp => new ChaosBackgroundService(
            sp.GetRequiredService<IChaosActivationStore>(),
            sp.GetServices<IChaosScenario>(),
            sp.GetServices<IChaosEffect>(),
            sp.GetRequiredService<ChaosRuntimeState>(),
            serviceName,
            sp.GetRequiredService<ILogger<ChaosBackgroundService>>()));

        return services;
    }

    public static IApplicationBuilder UseNornChaos(this IApplicationBuilder app) => app.UseMiddleware<ChaosMiddleware>();
}
