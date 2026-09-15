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
}
