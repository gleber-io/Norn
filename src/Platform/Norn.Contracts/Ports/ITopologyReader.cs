namespace Norn.Contracts.Ports;

/// <summary>
/// Leitura de topologia do cluster — réplicas, limites, UID de pod (ADR-03, verbos de leitura).
/// Implementada por Norn.Monitor (Fase 7) sobre KubernetesClient; é a fonte do
/// <c>TopologyUpdated</c> do NornHub (§5.6).
/// </summary>
public interface ITopologyReader
{
    Task<TopologyInfo> GetTopologyAsync(string service, string namespaceName, CancellationToken cancellationToken);

    Task<string?> GetPodUidAsync(string service, string namespaceName, string podName, CancellationToken cancellationToken);

    /// <summary>
    /// Nome do pod atual do Deployment (Fase 9, achado do replay ao vivo) — as métricas chegam ao
    /// Prometheus via OTel Collector sem enriquecimento <c>k8sattributes</c>, então nenhuma delas
    /// carrega o rótulo <c>pod</c> nativo do Kubernetes; o adaptador de métricas (Norn.Monitor) cai
    /// no <c>exported_instance</c> (UUID de instância OTel, nunca um pod), deixando
    /// <c>RestartPod</c> sem UID confiável em qualquer <see cref="AnomalyContext"/> construído ao
    /// vivo. Com o baseline de 1 réplica por serviço (Fase 6), o Deployment tem no máximo um pod
    /// <c>Running</c> — suficiente para resolver identidade sem ambiguidade. Retorna <c>null</c> se
    /// nenhum pod correspondente estiver <c>Running</c>.
    /// </summary>
    Task<string?> GetCurrentPodNameAsync(string service, string namespaceName, CancellationToken cancellationToken);

    /// <summary>
    /// <c>status.containerStatuses[].lastState.terminated.reason</c> do Pod (docs/metrics-matrix.md,
    /// "Consequência para a Fase 7"). Substitui <c>container_oom_events_total</c> como sinal de
    /// onset do F1 neste ambiente: o containerd remove o cgroup do container morto antes de o
    /// cAdvisor conseguir ler <c>oom_kill=1</c> nele, então a métrica do Prometheus fica sempre em
    /// zero mesmo com <c>OOMKilled</c> real e repetido. Retorna <c>null</c> se o pod não existe ou
    /// nunca terminou.
    /// </summary>
    Task<string?> GetLastTerminationReasonAsync(string namespaceName, string podName, CancellationToken cancellationToken);
}
