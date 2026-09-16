using k8s;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using Norn.Executor.Barriers;
using Norn.Executor.CircuitBreaker;
using Norn.Executor.FeatureFlags;
using Norn.Executor.Kubernetes;
using Norn.Executor.Rbac;
using Norn.Executor.Settings;
using Norn.Executor.Telemetry;

namespace Norn.Executor;

/// <summary>Composição do adaptador Norn.Executor — chamada pelo Norn.Worker (§4).</summary>
public static class ExecutorServiceCollectionExtensions
{
    public static IServiceCollection AddNornExecutor(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<ExecutorOptions>()
            // Barreiras do §5.4/ADR-04 (MaxReplicas, RestartPodCooldown, MaxActionsPerWindow,
            // ActionWindow) lidas da MESMA seção que Norn.Planner.Settings.PlannerOptions — "muda-se
            // num lugar só" (§5.4), nunca um segundo valor a sincronizar manualmente.
            .Bind(configuration.GetSection("Norn:Planner"))
            // ServiceToDeploymentName lido da mesma seção que Norn.Monitor.MonitorOptions, pelo
            // mesmo motivo.
            .Bind(configuration.GetSection("Norn:Monitor"))
            // Parâmetros exclusivos do Executor (cooldown geral, limiar do breaker, limiares de
            // restauração).
            .Bind(configuration.GetSection(ExecutorOptions.SectionName))
            .ValidateOnStart();

        services.AddSingleton(provider => provider.GetRequiredService<IOptions<ExecutorOptions>>().Value);

        // Mesma resolução de Norn.Monitor (BuildDefaultConfig): auto-detecta credencial do
        // ServiceAccount quando roda em Pod (in-cluster) e cai para o kubeconfig local quando roda
        // no host — o Worker opera nos dois cenários ao longo do projeto (ADR-09), e um
        // KubernetesClientConfiguration fixo em InClusterConfig() quebraria a execução local que
        // sustentou as Fases 7 e 8. TryAdd: mesma instância que Norn.Monitor já registra quando os
        // dois compõem o mesmo host (Norn.Worker).
        services.TryAddSingleton(KubernetesClientConfiguration.BuildDefaultConfig());
        services.TryAddSingleton<IKubernetes>(provider => new k8s.Kubernetes(provider.GetRequiredService<KubernetesClientConfiguration>()));

        services.AddSingleton<ExecutionPreconditionChecker>();
        services.AddSingleton<KubernetesActionApplier>();
        services.AddSingleton<FeatureFlagActionApplier>();
        services.AddSingleton<CircuitBreakerState>();
        services.AddSingleton<ExecutorMetrics>();
        services.AddSingleton<StartupCapabilityVerifier>();
        services.AddSingleton<HealingActionExecutor>();

        return services;
    }
}
