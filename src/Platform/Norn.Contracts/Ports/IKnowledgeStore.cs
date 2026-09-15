namespace Norn.Contracts.Ports;

/// <summary>
/// Persistência de sinais, contextos, planos e resultados (ADR-17). O trace de LLM viaja
/// embutido em <see cref="HealingPlan.LlmTrace"/> — não há método de gravação separado para ele.
/// Implementada por Norn.Knowledge (Fase 7); nunca referenciada diretamente por Monitor,
/// Analyzer, Planner ou Executor.
/// </summary>
public interface IKnowledgeStore
{
    Task SaveAnomalySignalAsync(AnomalySignal signal, CancellationToken cancellationToken);

    Task SaveAnomalyContextAsync(AnomalyContext context, CancellationToken cancellationToken);

    Task SaveHealingPlanAsync(HealingPlan plan, CancellationToken cancellationToken);

    Task SaveHealingOutcomeAsync(HealingOutcome outcome, CancellationToken cancellationToken);
}
