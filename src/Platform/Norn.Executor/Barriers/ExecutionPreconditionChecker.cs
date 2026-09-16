using System.Globalization;
using Norn.Contracts;
using Norn.Contracts.Ports;
using Norn.Executor.Settings;

namespace Norn.Executor.Barriers;

/// <summary>
/// Reavaliação de pré-condição e barreiras do ADR-04 imediatamente antes de aplicar (§5.4, tarefa 2
/// da Fase 9) — não é redundância com <c>Norn.Planner.Barriers.HealingActionPreconditionChecker</c>
/// (Fase 8): aquele decide sobre o <see cref="AnomalyContext"/> congelado no instante da decisão;
/// este lê o estado atual do cluster/Redis pelas mesmas portas que o resto do sistema usa
/// (<see cref="ITopologyReader"/>, <see cref="ICooldownStore"/>), porque o par decisão↔atuação não
/// é atômico. A tabela de regras é a mesma; a fonte dos dados, não.
/// </summary>
public sealed class ExecutionPreconditionChecker(
    ITopologyReader topologyReader,
    ICooldownStore cooldownStore,
    ExecutorOptions options)
{
    public async Task<ExecutionPreconditionResult> CheckAsync(HealingAction action, CancellationToken cancellationToken)
    {
        var target = action.Target;

        if (await cooldownStore.IsInCooldownAsync(target.Service, cancellationToken))
        {
            return ExecutionPreconditionResult.Reject("Alvo em cooldown (ADR-04, barreiras a/d).");
        }

        var recentActions = await cooldownStore.CountRecentActionsAsync(target.Service, options.ActionWindow, cancellationToken);
        if (recentActions >= options.MaxActionsPerWindow)
        {
            return ExecutionPreconditionResult.Reject(
                $"Máximo de {options.MaxActionsPerWindow} ações por alvo em {options.ActionWindow.TotalMinutes:0} min atingido (ADR-04, barreira b).");
        }

        return action.Type switch
        {
            HealingActionType.ScaleUp => await CheckScaleUpAsync(action, cancellationToken),
            HealingActionType.RestartPod => await CheckRestartPodAsync(action, cancellationToken),
            HealingActionType.ToggleFeatureFlag => CheckToggleFeatureFlag(action),
            _ => ExecutionPreconditionResult.Reject($"Tipo de ação fora do catálogo fechado: '{action.Type}'."),
        };
    }

    /// <summary>
    /// §5.4 — exceção única e declarada: satura em vez de recusar. Se o estado do cluster mudou
    /// desde a decisão e a soma passou do teto, aplica-se <see cref="ExecutorOptions.MaxReplicas"/>
    /// e a saturação fica auditável em <see cref="ExecutionPreconditionResult.Saturated"/> — nunca
    /// um <c>Reject</c> aqui.
    /// </summary>
    private async Task<ExecutionPreconditionResult> CheckScaleUpAsync(HealingAction action, CancellationToken cancellationToken)
    {
        if (!action.Parameters.TryGetValue("replicaDelta", out var deltaRaw) ||
            !int.TryParse(deltaRaw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var delta) ||
            delta is < 1 or > 3)
        {
            return ExecutionPreconditionResult.Reject("replicaDelta ausente ou fora da faixa permitida (1-3).");
        }

        var topology = await topologyReader.GetTopologyAsync(action.Target.Service, action.Target.Namespace, cancellationToken);
        var projected = topology.CurrentReplicas + delta;

        return projected <= options.MaxReplicas
            ? ExecutionPreconditionResult.Ok(targetReplicas: projected)
            : ExecutionPreconditionResult.Ok(targetReplicas: options.MaxReplicas, saturated: true);
    }

    /// <summary>
    /// §5.4 — UID lido ao vivo do cluster, não o do contexto congelado: entre a decisão e a
    /// atuação o pod pode já ter sido recriado (OOMKilled, ou um <c>RestartPod</c> concorrente).
    /// Reiniciar "o mesmo pod" que já é outro é ação sem efeito contabilizada como ação — por isso
    /// vira <c>Rejected</c> aqui, não uma falha de execução.
    /// </summary>
    private async Task<ExecutionPreconditionResult> CheckRestartPodAsync(HealingAction action, CancellationToken cancellationToken)
    {
        if (!action.Parameters.TryGetValue("podName", out var podName) || string.IsNullOrEmpty(podName) ||
            !action.Parameters.TryGetValue("podUid", out var expectedUid) || string.IsNullOrEmpty(expectedUid))
        {
            return ExecutionPreconditionResult.Reject("podName ou podUid ausente na ação.");
        }

        var observedUid = await topologyReader.GetPodUidAsync(action.Target.Service, action.Target.Namespace, podName, cancellationToken);
        if (observedUid is null || !string.Equals(observedUid, expectedUid, StringComparison.Ordinal))
        {
            return ExecutionPreconditionResult.Reject("podUid não confere com o observado ao vivo — o pod já foi recriado.");
        }

        var restartTarget = RestartPodCooldownTarget(action.Target.Service);
        if (await cooldownStore.IsInCooldownAsync(restartTarget, cancellationToken))
        {
            return ExecutionPreconditionResult.Reject(
                $"RestartPod do mesmo alvo já aplicado nos últimos {options.RestartPodCooldown.TotalMinutes:0} min.");
        }

        return ExecutionPreconditionResult.Ok();
    }

    private static ExecutionPreconditionResult CheckToggleFeatureFlag(HealingAction action)
    {
        if (!action.Parameters.TryGetValue("flagName", out var flagName) || !ShopFlagCatalog.All.Contains(flagName))
        {
            return ExecutionPreconditionResult.Reject("flagName fora do catálogo de flags do Shop (§5.7).");
        }

        return ExecutionPreconditionResult.Ok();
    }

    /// <summary>
    /// Chave de cooldown dedicada ao <c>RestartPod</c> (§5.4 — barreira mais estrita que o
    /// cooldown geral do ADR-04, porque é a única ação irreversível do catálogo), reaproveitando
    /// <see cref="ICooldownStore"/> com um alvo distinto em vez de um mecanismo novo.
    /// </summary>
    public static string RestartPodCooldownTarget(string service) => $"restartpod:{service}";
}
