using System.Globalization;
using Norn.Contracts;
using Norn.Planner.Settings;

namespace Norn.Planner.Barriers;

/// <summary>
/// Ponto único de checagem de pré-condição (§5.4) e barreira contra loop patológico (ADR-04),
/// reusado pelo <see cref="Norn.Planner.RuleEngine.RuleEngine"/> (depois de decidir) e pelo
/// <see cref="Norn.Planner.Validation.LlmOutputValidator"/> (passo 4 do §5.5) — a tabela de
/// pré-condições não muda por decisor, e mantê-la em dois lugares é o tipo de duplicação que
/// diverge silenciosamente quando uma ação nova entra no catálogo.
///
/// Função pura sobre o <see cref="AnomalyContext"/> recebido: os dados de cooldown
/// (<see cref="CooldownStatus"/>) e de histórico (<see cref="HealingHistoryEntry"/>) já chegam
/// resolvidos no contexto (Norn.Monitor, Fase 7) — nenhuma chamada nova a Redis/Postgres aqui.
/// O circuit breaker global (ADR-04, barreira c) não é checado neste componente: é responsabilidade
/// do Executor/Worker (Fase 9), que acompanha falhas ao longo de execuções, não de um único plano.
/// </summary>
public sealed class HealingActionPreconditionChecker(PlannerOptions options)
{
    public PreconditionResult Check(AnomalyContext context, HealingAction action)
    {
        // NoOp não atua — as barreiras do ADR-04 existem para conter ação, não abstenção.
        if (action.Type == HealingActionType.NoOp)
        {
            return PreconditionResult.Ok();
        }

        var cooldownResult = CheckCooldownBarriers(context);
        if (!cooldownResult.Accepted)
        {
            return cooldownResult;
        }

        return action.Type switch
        {
            HealingActionType.ScaleUp => CheckScaleUp(context, action),
            HealingActionType.RestartPod => CheckRestartPod(context, action),
            HealingActionType.ToggleFeatureFlag => CheckToggleFeatureFlag(action),
            _ => PreconditionResult.Reject($"Tipo de ação fora do catálogo fechado: '{action.Type}'."),
        };
    }

    /// <summary>ADR-04, barreiras (a) cooldown por alvo / (d) janela de verificação e (b) máximo de ações por janela.</summary>
    private PreconditionResult CheckCooldownBarriers(AnomalyContext context)
    {
        if (context.CooldownStatus.IsInCooldown)
        {
            return PreconditionResult.Reject("Alvo em cooldown (ADR-04, barreiras a/d).");
        }

        var windowStart = context.CreatedAtUtc - options.ActionWindow;
        var recentActions = context.HealingHistory.Count(entry => entry.AppliedAtUtc >= windowStart);
        if (recentActions >= options.MaxActionsPerWindow)
        {
            return PreconditionResult.Reject(
                $"Máximo de {options.MaxActionsPerWindow} ações por alvo em {options.ActionWindow.TotalMinutes:0} min atingido (ADR-04, barreira b).");
        }

        return PreconditionResult.Ok();
    }

    /// <summary>§5.4 — a pré-condição é sobre a soma, nunca sobre o valor atual isolado.</summary>
    private PreconditionResult CheckScaleUp(AnomalyContext context, HealingAction action)
    {
        if (!action.Parameters.TryGetValue("replicaDelta", out var deltaRaw) ||
            !int.TryParse(deltaRaw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var delta) ||
            delta is < 1 or > 3)
        {
            return PreconditionResult.Reject("replicaDelta ausente ou fora da faixa permitida (1-3).");
        }

        var projected = context.Topology.CurrentReplicas + delta;
        if (projected > options.MaxReplicas)
        {
            return PreconditionResult.Reject(
                $"réplicas atuais ({context.Topology.CurrentReplicas}) + replicaDelta ({delta}) = {projected} excede maxReplicas ({options.MaxReplicas}).");
        }

        return PreconditionResult.Ok();
    }

    /// <summary>
    /// §5.4 — duas pré-condições, e as duas importam: UID confere com o observado (corrida
    /// decisão↔atuação) e nenhum RestartPod do mesmo alvo nos últimos <see cref="PlannerOptions.RestartPodCooldown"/>
    /// (barreira mais estrita que o cooldown geral do ADR-04, porque é a única ação irreversível).
    /// </summary>
    private PreconditionResult CheckRestartPod(AnomalyContext context, HealingAction action)
    {
        var observedPodUid = context.PrimarySignal.Target.PodUid;
        if (string.IsNullOrEmpty(observedPodUid))
        {
            return PreconditionResult.Reject("Nenhum pod observado no contexto — RestartPod sem alvo confiável.");
        }

        if (!action.Parameters.TryGetValue("podUid", out var podUid) ||
            !string.Equals(podUid, observedPodUid, StringComparison.Ordinal))
        {
            return PreconditionResult.Reject("podUid não confere com o observado no contexto — o pod já foi recriado.");
        }

        var restartWindowStart = context.CreatedAtUtc - options.RestartPodCooldown;
        var recentRestart = context.HealingHistory.Any(entry =>
            string.Equals(entry.Action, nameof(HealingActionType.RestartPod), StringComparison.OrdinalIgnoreCase) &&
            entry.AppliedAtUtc >= restartWindowStart);

        return recentRestart
            ? PreconditionResult.Reject(
                $"RestartPod do mesmo alvo já aplicado nos últimos {options.RestartPodCooldown.TotalMinutes:0} min.")
            : PreconditionResult.Ok();
    }

    private static PreconditionResult CheckToggleFeatureFlag(HealingAction action)
    {
        if (!action.Parameters.TryGetValue("flagName", out var flagName) ||
            !ShopFlagCatalog.All.Contains(flagName))
        {
            return PreconditionResult.Reject("flagName fora do catálogo de flags do Shop (§5.7).");
        }

        return PreconditionResult.Ok();
    }
}
