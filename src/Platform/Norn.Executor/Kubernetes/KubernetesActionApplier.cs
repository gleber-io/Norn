using System.Globalization;
using System.Net;
using k8s;
using k8s.Autorest;
using k8s.Models;
using Norn.Contracts;
using Norn.Executor.Settings;

namespace Norn.Executor.Kubernetes;

/// <summary>
/// Aplica <c>ScaleUp</c> e <c>RestartPod</c> de verdade (§5.4, ADR-03 — só os dois verbos de
/// escrita concedidos à <c>Role</c>: <c>patch</c> em <c>deployments/scale</c>, <c>delete</c> em
/// <c>pods</c>). <c>MergePatch</c> no subrecurso <c>scale</c>, nunca <c>ReplaceNamespacedDeploymentScaleAsync</c>
/// nem patch no Deployment inteiro — perderia concorrência otimista e extrapolaria o escopo mínimo
/// do ADR-03 (verificado 14/09/2026, §11 item 10).
/// </summary>
public sealed class KubernetesActionApplier(IKubernetes kubernetesClient, ExecutorOptions options)
{
    public async Task<ActionApplyResult> ApplyScaleUpAsync(HealingAction action, int targetReplicas, CancellationToken cancellationToken)
    {
        var deploymentName = ResolveDeploymentName(action.Target.Service);
        var patchJson = $"{{\"spec\":{{\"replicas\":{targetReplicas.ToString(CultureInfo.InvariantCulture)}}}}}";
        var patch = new V1Patch(patchJson, V1Patch.PatchType.MergePatch);

        try
        {
            await kubernetesClient.AppsV1.PatchNamespacedDeploymentScaleAsync(
                patch, deploymentName, action.Target.Namespace, cancellationToken: cancellationToken);
            return ActionApplyResult.Succeeded();
        }
        catch (HttpOperationException ex) when (ex.Response.StatusCode == HttpStatusCode.Forbidden)
        {
            return ActionApplyResult.RbacDefect(ex.Message);
        }
        catch (HttpOperationException ex)
        {
            return ActionApplyResult.Failed(ex.Message);
        }
    }

    public async Task<ActionApplyResult> ApplyRestartPodAsync(HealingAction action, CancellationToken cancellationToken)
    {
        var podName = action.Parameters["podName"];

        try
        {
            // O ReplicaSet recria o pod sob outro nome/UID — a ação é o delete em si (§5.4).
            await kubernetesClient.CoreV1.DeleteNamespacedPodAsync(podName, action.Target.Namespace, cancellationToken: cancellationToken);
            return ActionApplyResult.Succeeded();
        }
        catch (HttpOperationException ex) when (ex.Response.StatusCode == HttpStatusCode.Forbidden)
        {
            return ActionApplyResult.RbacDefect(ex.Message);
        }
        catch (HttpOperationException ex) when (ex.Response.StatusCode == HttpStatusCode.NotFound)
        {
            // Corrida: o pod já não existe mais no instante do delete (recriado/removido entre a
            // checagem de pré-condição e a chamada). Sem efeito, não é falha.
            return ActionApplyResult.Rejected("Pod não encontrado no instante da atuação — corrida decisão↔atuação.");
        }
        catch (HttpOperationException ex)
        {
            return ActionApplyResult.Failed(ex.Message);
        }
    }

    private string ResolveDeploymentName(string service) =>
        options.ServiceToDeploymentName.GetValueOrDefault(service, service);
}
