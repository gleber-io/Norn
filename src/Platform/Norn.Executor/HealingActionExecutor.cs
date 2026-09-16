using Microsoft.Extensions.Logging;
using Norn.Contracts;
using Norn.Contracts.Ports;
using Norn.Executor.Barriers;
using Norn.Executor.CircuitBreaker;
using Norn.Executor.FeatureFlags;
using Norn.Executor.Kubernetes;
using Norn.Executor.Settings;
using Norn.Executor.Telemetry;

namespace Norn.Executor;

/// <summary>
/// Fecha o MAPE-K: atua e verifica (Fase 9). Ponto único que aplica a ação de um
/// <see cref="HealingPlan"/> — reavaliando pré-condições ao vivo (tarefa 2), respeitando
/// <c>DryRun</c> (tarefa 4), atualizando as barreiras do ADR-04 e o circuit breaker (tarefa 6) e
/// produzindo o <see cref="HealingOutcome"/> depois da janela de verificação (tarefa 5). Recebe o
/// <see cref="AnomalyContext.RecentMetrics"/> original como <c>metricsBefore</c> — não lê Knowledge
/// (ADR-17): quem já tem o contexto (Norn.Worker) entrega.
/// </summary>
public sealed partial class HealingActionExecutor(
    ExecutionPreconditionChecker preconditionChecker,
    KubernetesActionApplier kubernetesApplier,
    FeatureFlagActionApplier featureFlagApplier,
    ICooldownStore cooldownStore,
    IRecentMetricsReader recentMetricsReader,
    IPlatformConfig platformConfig,
    CircuitBreakerState circuitBreaker,
    ExecutorOptions options,
    ExecutorMetrics metrics,
    TimeProvider timeProvider,
    ILogger<HealingActionExecutor> logger)
{
    public async Task<HealingOutcome> ExecuteAsync(HealingPlan plan, RecentMetrics metricsBefore, bool dryRun, CancellationToken cancellationToken)
    {
        var appliedAtUtc = timeProvider.GetUtcNow();

        // O catálogo atual (§5.4) sempre emite exatamente uma ação por plano — Order existe para
        // sequenciamento futuro, sem uso ainda.
        var action = plan.Actions.Count > 0 ? plan.Actions[0] : null;

        if (action is null || action.Type == HealingActionType.NoOp)
        {
            return BuildOutcome(plan, metricsBefore, metricsBefore, appliedAtUtc, appliedAtUtc,
                HealingOutcomeStatus.Succeeded, sloRestored: false, errorMessage: null);
        }

        var precondition = await preconditionChecker.CheckAsync(action, cancellationToken);
        if (!precondition.Accepted)
        {
            LogActionRejected(logger, action.Type, precondition.RejectionReason!);
            return BuildOutcome(plan, metricsBefore, metricsBefore, appliedAtUtc, appliedAtUtc,
                HealingOutcomeStatus.Rejected, sloRestored: false, precondition.RejectionReason);
        }

        var applyResult = await ApplyAsync(action, precondition, dryRun, cancellationToken);
        await RecordSideEffectsAsync(action, applyResult, dryRun, cancellationToken);

        if (applyResult.Outcome is ActionApplyOutcome.Rejected or ActionApplyOutcome.RbacDefect or ActionApplyOutcome.Failed)
        {
            var status = applyResult.Outcome == ActionApplyOutcome.Failed ? HealingOutcomeStatus.Failed : HealingOutcomeStatus.Rejected;
            metrics.RecordAction(action.Type, status);
            return BuildOutcome(plan, metricsBefore, metricsBefore, appliedAtUtc, appliedAtUtc,
                status, sloRestored: false, applyResult.Message);
        }

        if (dryRun)
        {
            metrics.RecordAction(action.Type, HealingOutcomeStatus.Succeeded);
            return BuildOutcome(plan, metricsBefore, metricsBefore, appliedAtUtc, appliedAtUtc,
                HealingOutcomeStatus.Succeeded, sloRestored: false, applyResult.Message);
        }

        await Task.Delay(TimeSpan.FromSeconds(plan.VerificationWindowSeconds), cancellationToken);
        var metricsAfter = await recentMetricsReader.ReadAsync(action.Target.Service, cancellationToken);
        var verifiedAtUtc = timeProvider.GetUtcNow();
        var sloRestored = EvaluateSloRestored(action.Type, metricsAfter);

        var finalStatus = sloRestored ? HealingOutcomeStatus.Succeeded : HealingOutcomeStatus.PartiallyApplied;
        var errorMessage = precondition.Saturated
            ? $"ScaleUp saturado em {precondition.TargetReplicas} réplicas (maxReplicas={options.MaxReplicas})."
            : applyResult.Message;

        metrics.RecordAction(action.Type, finalStatus);
        if (sloRestored)
        {
            metrics.RecordMttr((verifiedAtUtc - appliedAtUtc).TotalSeconds);
        }

        return BuildOutcome(plan, metricsBefore, metricsAfter, appliedAtUtc, verifiedAtUtc, finalStatus, sloRestored, errorMessage);
    }

    private async Task<ActionApplyResult> ApplyAsync(
        HealingAction action, ExecutionPreconditionResult precondition, bool dryRun, CancellationToken cancellationToken)
    {
        if (dryRun)
        {
            return ActionApplyResult.Succeeded($"[DryRun] {action.Type} não aplicado — apenas simulado (tarefa 4).");
        }

        return action.Type switch
        {
            HealingActionType.ScaleUp => await kubernetesApplier.ApplyScaleUpAsync(action, precondition.TargetReplicas!.Value, cancellationToken),
            HealingActionType.RestartPod => await kubernetesApplier.ApplyRestartPodAsync(action, cancellationToken),
            HealingActionType.ToggleFeatureFlag => await featureFlagApplier.ApplyAsync(action, cancellationToken),
            _ => ActionApplyResult.Failed($"Tipo de ação fora do catálogo fechado: '{action.Type}'."),
        };
    }

    /// <summary>ADR-04 (cooldown, contagem por janela) e barreira (c) do circuit breaker — só sobre ação real, nunca em <c>DryRun</c>.</summary>
    private async Task RecordSideEffectsAsync(HealingAction action, ActionApplyResult applyResult, bool dryRun, CancellationToken cancellationToken)
    {
        if (dryRun)
        {
            return;
        }

        if (applyResult.Outcome == ActionApplyOutcome.Succeeded)
        {
            circuitBreaker.RecordSuccess();
            await cooldownStore.SetCooldownAsync(action.Target.Service, options.GeneralCooldown, cancellationToken);
            await cooldownStore.RecordActionAsync(action.Target.Service, cancellationToken);

            if (action.Type == HealingActionType.RestartPod)
            {
                await cooldownStore.SetCooldownAsync(
                    ExecutionPreconditionChecker.RestartPodCooldownTarget(action.Target.Service), options.RestartPodCooldown, cancellationToken);
            }

            return;
        }

        if (applyResult.Outcome is ActionApplyOutcome.RbacDefect or ActionApplyOutcome.Failed)
        {
            if (applyResult.Outcome == ActionApplyOutcome.RbacDefect)
            {
                LogRbacDefectDetected(logger, action.Type, applyResult.Message ?? string.Empty);
            }

            if (circuitBreaker.RecordFailure(options.CircuitBreakerFailureThreshold))
            {
                LogCircuitBreakerTripped(logger, options.CircuitBreakerFailureThreshold);
                await platformConfig.SetModeAsync(PlatformMode.Observe, cancellationToken);
            }
        }
    }

    /// <summary>
    /// §5.4 DoD — pergunta "voltou a ficar bom o bastante", não a severidade de detecção
    /// (Norn.Analyzer, ADR-14). Um limiar por tipo de ação: o que cada uma promete restaurar.
    /// </summary>
    private bool EvaluateSloRestored(HealingActionType actionType, RecentMetrics after) => actionType switch
    {
        HealingActionType.RestartPod => after.MemoryWorkingSetBytes < options.MemoryRestoredThresholdBytes,
        HealingActionType.ScaleUp => after.LatencyP99Ms < options.LatencyP99RestoredMs,
        HealingActionType.ToggleFeatureFlag => after.ErrorRatePct < options.ErrorRateRestoredPct,
        _ => true,
    };

    private static HealingOutcome BuildOutcome(
        HealingPlan plan,
        RecentMetrics metricsBefore,
        RecentMetrics metricsAfter,
        DateTimeOffset appliedAtUtc,
        DateTimeOffset verifiedAtUtc,
        HealingOutcomeStatus status,
        bool sloRestored,
        string? errorMessage) => new()
        {
            OutcomeId = Guid.NewGuid(),
            PlanId = plan.PlanId,
            AppliedAtUtc = appliedAtUtc,
            VerifiedAtUtc = verifiedAtUtc,
            Status = status,
            SloRestored = sloRestored,
            TimeToRecoverySeconds = sloRestored ? (verifiedAtUtc - appliedAtUtc).TotalSeconds : 0,
            MetricsBefore = metricsBefore,
            MetricsAfter = metricsAfter,
            ErrorMessage = errorMessage,
        };

    [LoggerMessage(Level = LogLevel.Warning, Message = "Ação {ActionType} recusada na reavaliação de pré-condição: {Reason}")]
    private static partial void LogActionRejected(ILogger logger, HealingActionType actionType, string reason);

    [LoggerMessage(Level = LogLevel.Error, Message = "403 do Kubernetes em {ActionType}, ação permitida pelo catálogo (§5.4) — defeito de configuração, não contenção (ADR-03, tarefa 5a). Detalhe: {Detail}")]
    private static partial void LogRbacDefectDetected(ILogger logger, HealingActionType actionType, string detail);

    [LoggerMessage(Level = LogLevel.Error, Message = "Circuit breaker aberto após {Threshold} falhas consecutivas (ADR-04, barreira c) — modo forçado para Observe.")]
    private static partial void LogCircuitBreakerTripped(ILogger logger, int threshold);
}
