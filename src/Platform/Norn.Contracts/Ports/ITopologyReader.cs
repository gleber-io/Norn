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
    /// <c>status.containerStatuses[].lastState.terminated.reason</c> do Pod (docs/metrics-matrix.md,
    /// "Consequência para a Fase 7"). Substitui <c>container_oom_events_total</c> como sinal de
    /// onset do F1 neste ambiente: o containerd remove o cgroup do container morto antes de o
    /// cAdvisor conseguir ler <c>oom_kill=1</c> nele, então a métrica do Prometheus fica sempre em
    /// zero mesmo com <c>OOMKilled</c> real e repetido. Retorna <c>null</c> se o pod não existe ou
    /// nunca terminou.
    /// </summary>
    Task<string?> GetLastTerminationReasonAsync(string namespaceName, string podName, CancellationToken cancellationToken);
}
