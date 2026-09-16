namespace Norn.Contracts.Ports;

/// <summary>
/// Leitura do histórico persistido (ADR-06) — consumida só por Norn.API para os GETs REST da
/// Fase 10 (§5.6). Implementada por Norn.Knowledge; nunca por Monitor, Analyzer, Planner ou
/// Executor (mesma regra de <see cref="IKnowledgeStore"/>, ADR-17).
/// </summary>
public interface IKnowledgeReader
{
    /// <summary>
    /// Última topologia conhecida por serviço, extraída do <see cref="AnomalyContext"/> mais
    /// recente de cada serviço distinto — Norn.API não lê o cluster ao vivo (não referencia
    /// Norn.Monitor, §4). Serviço sem nenhum contexto persistido não aparece na lista.
    /// </summary>
    Task<IReadOnlyList<TopologyInfo>> GetLatestTopologyAsync(CancellationToken cancellationToken);

    Task<IReadOnlyList<AnomalySignal>> GetRecentSignalsAsync(int limit, Guid? experimentRunId, CancellationToken cancellationToken);

    Task<IReadOnlyList<HealingPlan>> GetRecentPlansAsync(int limit, CancellationToken cancellationToken);

    Task<IReadOnlyList<HealingOutcome>> GetRecentOutcomesAsync(int limit, CancellationToken cancellationToken);

    Task<ExperimentRunSummary?> GetExperimentRunAsync(Guid experimentRunId, CancellationToken cancellationToken);
}
