using k8s;
using k8s.Models;
using Norn.Contracts;
using Norn.Contracts.Ports;
using Norn.Executor.Settings;

namespace Norn.Executor.Rbac;

/// <summary>
/// Verificação de capacidade no startup (§5.4, ADR-03, tarefa 1a) — cada ação do catálogo contra o
/// recurso que ela realmente usa. Faltando qualquer uma, o Worker falha rápido e não sobe: um 403
/// só é evidência de contenção depois que esta verificação passou (ADR-03, invariante de
/// 13/09/2026). O <see cref="V1SelfSubjectAccessReview"/> não exige RBAC extra — qualquer
/// ServiceAccount pode criar a revisão para si mesma.
/// </summary>
public sealed class StartupCapabilityVerifier(
    IKubernetes kubernetesClient,
    IFeatureFlagWriter featureFlagWriter,
    ExecutorOptions options)
{
    public async Task VerifyOrThrowAsync(CancellationToken cancellationToken)
    {
        var failures = new List<string>();

        await CheckAsync(failures, "ScaleUp", () => CheckScaleUpAsync(cancellationToken));
        await CheckAsync(failures, "RestartPod", () => CheckRestartPodAsync(cancellationToken));
        await CheckAsync(failures, "ToggleFeatureFlag", () => CheckToggleFeatureFlagAsync(cancellationToken));

        if (failures.Count > 0)
        {
            throw new InvalidOperationException(
                "Verificação de capacidade de startup do Norn.Executor falhou (§5.4, ADR-03, tarefa 1a da Fase 9): " +
                string.Join(" | ", failures));
        }
    }

    private static async Task CheckAsync(List<string> failures, string actionName, Func<Task<string?>> check)
    {
        var failure = await check();
        if (failure is not null)
        {
            failures.Add($"{actionName}: {failure}");
        }
    }

    /// <summary>
    /// O subrecurso vai em campo próprio (<see cref="V1ResourceAttributes.Subresource"/>), nunca
    /// <c>Resource = "deployments/scale"</c> — verificado 14/09/2026, §11 item 10. Escrever errado
    /// faz a revisão responder sobre outro recurso, e uma trava que responde a pergunta errada é
    /// pior que trava nenhuma.
    /// </summary>
    private async Task<string?> CheckScaleUpAsync(CancellationToken cancellationToken)
    {
        var review = new V1SelfSubjectAccessReview
        {
            Spec = new V1SelfSubjectAccessReviewSpec
            {
                ResourceAttributes = new V1ResourceAttributes
                {
                    Group = "apps",
                    Resource = "deployments",
                    Subresource = "scale",
                    Verb = "patch",
                    NamespaceProperty = options.Namespace,
                },
            },
        };

        var result = await kubernetesClient.AuthorizationV1.CreateSelfSubjectAccessReviewAsync(review, cancellationToken: cancellationToken);
        return result.Status.Allowed
            ? null
            : $"patch em deployments/scale (ns {options.Namespace}) negado — Role divergente do catálogo (§5.4).";
    }

    private async Task<string?> CheckRestartPodAsync(CancellationToken cancellationToken)
    {
        var review = new V1SelfSubjectAccessReview
        {
            Spec = new V1SelfSubjectAccessReviewSpec
            {
                ResourceAttributes = new V1ResourceAttributes
                {
                    Group = "",
                    Resource = "pods",
                    Verb = "delete",
                    NamespaceProperty = options.Namespace,
                },
            },
        };

        var result = await kubernetesClient.AuthorizationV1.CreateSelfSubjectAccessReviewAsync(review, cancellationToken: cancellationToken);
        return result.Status.Allowed
            ? null
            : $"delete em pods (ns {options.Namespace}) negado — Role divergente do catálogo (§5.4).";
    }

    /// <summary>
    /// PING + EXISTS por chave (§5.4) — flag ausente no Redis é exatamente a falha que faria o F3
    /// rodar sem se recuperar, e é silenciosa em execução. Descobrir a lacuna ao subir o processo é
    /// outra coisa que descobrir na décima execução da campanha.
    /// </summary>
    private async Task<string?> CheckToggleFeatureFlagAsync(CancellationToken cancellationToken)
    {
        if (!await featureFlagWriter.PingAsync(cancellationToken))
        {
            return "Redis inalcançável.";
        }

        var missing = new List<string>();
        foreach (var flagName in ShopFlagCatalog.All)
        {
            if (!await featureFlagWriter.ExistsAsync(flagName, cancellationToken))
            {
                missing.Add(flagName);
            }
        }

        return missing.Count == 0
            ? null
            : $"chave(s) ausente(s) sob shop:flags: — {string.Join(", ", missing)}.";
    }
}
