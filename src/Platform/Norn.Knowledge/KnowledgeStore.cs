using Microsoft.EntityFrameworkCore;
using Norn.Contracts;
using Norn.Contracts.Ports;
using Norn.Contracts.Serialization;
using Norn.Knowledge.Entities;

namespace Norn.Knowledge;

/// <summary>
/// Implementa <see cref="IKnowledgeStore"/> sobre <see cref="KnowledgeDbContext"/> (ADR-17,
/// ADR-06 — Postgres é a evidência). O <c>context_hash</c> de <see cref="AnomalyContext"/> é
/// calculado aqui, sobre a mesma <see cref="CanonicalJson"/> que a tarefa 4 da Fase 8 usa para
/// o prompt do LLM — dois serializadores divergentes produziriam hash que não corresponde ao
/// que o modelo viu.
/// </summary>
/// <summary>Público para ser testável diretamente contra um Postgres real (Testcontainers) sem carregar o resto de <c>AddNornKnowledge</c> (Redis incluso).</summary>
public sealed class KnowledgeStore(KnowledgeDbContext dbContext) : IKnowledgeStore
{
    public async Task SaveAnomalySignalAsync(AnomalySignal signal, CancellationToken cancellationToken)
    {
        dbContext.AnomalySignals.Add(new AnomalySignalRow
        {
            SignalId = signal.SignalId,
            DetectedAtUtc = signal.DetectedAtUtc,
            ExperimentRunId = signal.ExperimentRunId,
            Service = signal.Target.Service,
            MetricName = signal.MetricName,
            Severity = signal.Severity.ToString(),
            Payload = CanonicalJson.Serialize(signal),
        });

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task SaveAnomalyContextAsync(AnomalyContext context, CancellationToken cancellationToken)
    {
        dbContext.AnomalyContexts.Add(new AnomalyContextRow
        {
            ContextId = context.ContextId,
            CreatedAtUtc = context.CreatedAtUtc,
            CorrelationId = context.CorrelationId,
            ExperimentRunId = context.ExperimentRunId,
            Service = context.Topology.Service,
            PrimarySignalId = context.PrimarySignal.SignalId,
            ContextHash = CanonicalJson.ComputeHash(context),
            Payload = CanonicalJson.Serialize(context),
        });

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task SaveHealingPlanAsync(HealingPlan plan, CancellationToken cancellationToken)
    {
        dbContext.HealingPlans.Add(new HealingPlanRow
        {
            PlanId = plan.PlanId,
            ContextId = plan.ContextId,
            CreatedAtUtc = plan.CreatedAtUtc,
            DecidedBy = plan.DecidedBy.ToString(),
            Payload = CanonicalJson.Serialize(plan),
        });

        // O trace de LLM viaja embutido em HealingPlan.LlmTrace (§5.3) — não há método de
        // gravação separado no IKnowledgeStore, mas llm_traces existe como tabela própria
        // (tarefa 1) para consulta sem abrir o jsonb do plano inteiro.
        dbContext.LlmTraces.Add(new LlmTraceRow
        {
            TraceId = Guid.NewGuid(),
            PlanId = plan.PlanId,
            PromptHash = plan.LlmTrace.PromptHash,
            Model = plan.LlmTrace.Model,
            LatencyMs = plan.LlmTrace.LatencyMs,
            Attempts = plan.LlmTrace.Attempts,
            Payload = CanonicalJson.Serialize(plan.LlmTrace),
        });

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task SaveHealingOutcomeAsync(HealingOutcome outcome, CancellationToken cancellationToken)
    {
        dbContext.HealingOutcomes.Add(new HealingOutcomeRow
        {
            OutcomeId = outcome.OutcomeId,
            PlanId = outcome.PlanId,
            AppliedAtUtc = outcome.AppliedAtUtc,
            VerifiedAtUtc = outcome.VerifiedAtUtc,
            Status = outcome.Status.ToString(),
            Payload = CanonicalJson.Serialize(outcome),
        });

        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
