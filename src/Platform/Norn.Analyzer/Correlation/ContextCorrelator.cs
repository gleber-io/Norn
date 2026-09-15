using Norn.Contracts;

namespace Norn.Analyzer.Correlation;

/// <summary>
/// Correlação (tarefa 5): sinais do mesmo alvo numa janela viram um único <see cref="AnomalyContext"/>.
/// Ordenação determinística (ADR-14): severidade desc → <c>detectedAtUtc</c> asc → <c>confidence</c>
/// desc → <c>metricName</c> asc. O primeiro vira <c>primarySignal</c> — ordem de leitura, não
/// hipótese de causa raiz. Função pura: sem relógio, sem I/O — <c>createdAtUtc</c> é parâmetro, e
/// o enriquecimento (topologia, métricas recentes, histórico, flags, cooldown) é resolvido pelo
/// chamador (Norn.Worker) antes de chegar aqui.
/// </summary>
public static class ContextCorrelator
{
    public static AnomalyContext BuildContext(
        IReadOnlyList<AnomalySignal> signalsInWindow,
        Guid contextId,
        Guid correlationId,
        Guid? experimentRunId,
        DateTimeOffset createdAtUtc,
        TopologyInfo topology,
        RecentMetrics recentMetrics,
        IReadOnlyList<HealingHistoryEntry> healingHistory,
        IReadOnlyDictionary<string, bool> activeFeatureFlags,
        CooldownStatus cooldownStatus)
    {
        if (signalsInWindow.Count == 0)
        {
            throw new ArgumentException("Um AnomalyContext exige ao menos um sinal.", nameof(signalsInWindow));
        }

        var ordered = OrderDeterministically(signalsInWindow);

        return new AnomalyContext
        {
            ContextId = contextId,
            CreatedAtUtc = createdAtUtc,
            CorrelationId = correlationId,
            ExperimentRunId = experimentRunId,
            PrimarySignal = ordered[0],
            CorrelatedSignals = ordered.Skip(1).ToArray(),
            Topology = topology,
            RecentMetrics = recentMetrics,
            HealingHistory = healingHistory,
            ActiveFeatureFlags = activeFeatureFlags,
            CooldownStatus = cooldownStatus,
        };
    }

    /// <summary>Exposto à parte para a calibração da tarefa 5a — testa a eleição sem montar o contexto inteiro.</summary>
    public static IReadOnlyList<AnomalySignal> OrderDeterministically(IReadOnlyList<AnomalySignal> signals) =>
        signals
            .OrderByDescending(signal => signal.Severity)
            .ThenBy(signal => signal.DetectedAtUtc)
            .ThenByDescending(signal => signal.Confidence)
            .ThenBy(signal => signal.MetricName, StringComparer.Ordinal)
            .ToArray();
}
