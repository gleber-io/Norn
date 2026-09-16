using k8s;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Norn.Contracts.Ports;
using Norn.Monitor.Kubernetes;
using Norn.Monitor.Prometheus;

namespace Norn.Monitor;

/// <summary>Composição do adaptador Norn.Monitor — chamada pelo Norn.Worker (§4).</summary>
public static class MonitorServiceCollectionExtensions
{
    public static IServiceCollection AddNornMonitor(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<MonitorOptions>()
            .Bind(configuration.GetSection(MonitorOptions.SectionName))
            .ValidateOnStart();

        services.AddHttpClient<IMetricSource, PrometheusMetricSource>((provider, client) =>
        {
            var options = provider.GetRequiredService<IOptions<MonitorOptions>>().Value;
            client.BaseAddress = new Uri(options.PrometheusBaseUrl);
        }).AddStandardResilienceHandler();

        services.AddSingleton(KubernetesClientConfiguration.BuildDefaultConfig());
        services.AddSingleton<IKubernetes>(provider => new k8s.Kubernetes(provider.GetRequiredService<KubernetesClientConfiguration>()));
        services.AddSingleton<ITopologyReader, KubernetesTopologyReader>();

        services.AddSingleton<IMetricSampleBuffer, MetricSampleBuffer>();
        services.AddSingleton<RecentMetricsReader>();
        // Fase 9: Norn.Executor consome a leitura pela porta, não pela classe concreta (ADR-17) —
        // mesma instância, registro adicional só para satisfazer o tipo da porta.
        services.AddSingleton<IRecentMetricsReader>(provider => provider.GetRequiredService<RecentMetricsReader>());
        services.AddHostedService<MonitorPollingBackgroundService>();

        return services;
    }
}
