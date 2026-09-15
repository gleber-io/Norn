namespace Norn.Knowledge.Entities;

/// <summary>
/// <c>anomaly_signals</c> — documento completo em <c>payload</c> (jsonb) mais colunas extraídas
/// para índice e junção, mesmo padrão de <see cref="AnomalyContextRow"/> (tarefa 1 da Fase 7).
/// </summary>
public sealed class AnomalySignalRow
{
    public required Guid SignalId { get; init; }

    public required DateTimeOffset DetectedAtUtc { get; init; }

    public required Guid? ExperimentRunId { get; init; }

    public required string Service { get; init; }

    public required string MetricName { get; init; }

    public required string Severity { get; init; }

    public required string Payload { get; init; }
}
