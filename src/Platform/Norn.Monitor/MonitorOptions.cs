namespace Norn.Monitor;

/// <summary>
/// Configuração do Monitor via <c>IOptions</c> (§7.1) — nunca hardcoded. Consultas PromQL
/// parametrizadas pela matriz da Fase 4 (docs/metrics-matrix.md), polling configurável (tarefa 2).
/// </summary>
public sealed class MonitorOptions
{
    public const string SectionName = "Norn:Monitor";

    public required string PrometheusBaseUrl { get; init; }

    public string Namespace { get; init; } = "norn-shop";

    /// <summary>Intervalo de polling do <see cref="MonitorPollingBackgroundService"/>.</summary>
    public TimeSpan PollInterval { get; init; } = TimeSpan.FromSeconds(5);

    /// <summary>
    /// Nome lógico do serviço (<c>exported_job</c> do Prometheus — o mesmo em <c>AnomalySignal.Target.Service</c>
    /// em todo o sistema) → nome do recurso <c>Deployment</c> no cluster. Os dois nomes divergem
    /// de propósito: o primeiro é <c>service.name</c> do OTel (identidade lógica do serviço), o
    /// segundo é o nome do manifesto Kubernetes (<c>deploy/k8s/base</c>) — sem este mapa,
    /// <see cref="Kubernetes.KubernetesTopologyReader.GetTopologyAsync"/> tenta ler um Deployment
    /// que nunca existe com esse nome exato.
    /// </summary>
    public Dictionary<string, string> ServiceToDeploymentName { get; init; } = new()
    {
        ["Norn.Shop.Catalog.API"] = "catalog-api",
        ["Norn.Shop.Order.API"] = "order-api",
        ["Norn.Shop.Payment.API"] = "payment-api",
    };
}
