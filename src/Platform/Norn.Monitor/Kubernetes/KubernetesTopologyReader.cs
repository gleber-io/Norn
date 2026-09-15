using k8s;
using Norn.Contracts;
using Norn.Contracts.Ports;

namespace Norn.Monitor.Kubernetes;

/// <summary>
/// Leitura de topologia do cluster (ADR-03: apenas <c>get</c>/<c>list</c>/<c>watch</c>, nunca
/// escrita — os verbos de escrita são do Norn.Executor). Fonte do <c>TopologyUpdated</c> do
/// NornHub (§5.6, Fase 10).
/// </summary>
internal sealed class KubernetesTopologyReader(IKubernetes kubernetesClient) : ITopologyReader
{
    public async Task<TopologyInfo> GetTopologyAsync(string service, string namespaceName, CancellationToken cancellationToken)
    {
        var deployment = await kubernetesClient.AppsV1.ReadNamespacedDeploymentAsync(service, namespaceName, cancellationToken: cancellationToken);

        var container = deployment.Spec.Template.Spec.Containers.FirstOrDefault();
        var requests = container?.Resources?.Requests;
        var limits = container?.Resources?.Limits;

        return new TopologyInfo
        {
            Service = service,
            CurrentReplicas = deployment.Status?.Replicas ?? 0,
            DesiredReplicas = deployment.Spec.Replicas ?? 0,
            ResourceRequests = new ResourceSpec
            {
                Cpu = ResourceQuantity(requests, "cpu"),
                Memory = ResourceQuantity(requests, "memory"),
            },
            ResourceLimits = new ResourceSpec
            {
                Cpu = ResourceQuantity(limits, "cpu"),
                Memory = ResourceQuantity(limits, "memory"),
            },
        };
    }

    private static string ResourceQuantity(IDictionary<string, k8s.Models.ResourceQuantity>? resources, string key) =>
        resources is not null && resources.TryGetValue(key, out var quantity) ? quantity.ToString() : "0";

    public async Task<string?> GetPodUidAsync(string service, string namespaceName, string podName, CancellationToken cancellationToken)
    {
        var pod = await TryReadPodAsync(namespaceName, podName, cancellationToken);
        return pod?.Metadata?.Uid;
    }

    public async Task<string?> GetLastTerminationReasonAsync(string namespaceName, string podName, CancellationToken cancellationToken)
    {
        var pod = await TryReadPodAsync(namespaceName, podName, cancellationToken);
        var containerStatus = pod?.Status?.ContainerStatuses?.FirstOrDefault();

        return containerStatus?.LastState?.Terminated?.Reason;
    }

    private async Task<k8s.Models.V1Pod?> TryReadPodAsync(string namespaceName, string podName, CancellationToken cancellationToken)
    {
        try
        {
            return await kubernetesClient.CoreV1.ReadNamespacedPodAsync(podName, namespaceName, cancellationToken: cancellationToken);
        }
        catch (k8s.Autorest.HttpOperationException ex) when (ex.Response.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }
    }
}
