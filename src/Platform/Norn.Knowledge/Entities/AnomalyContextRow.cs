namespace Norn.Knowledge.Entities;

/// <summary>
/// <c>anomaly_contexts</c> — coluna a coluna conforme tarefa 1 da Fase 7. <c>jsonb</c>, não
/// colunas normalizadas: o valor do contexto é ser byte-exato reproduzível, e normalizar
/// introduziria arredondamento e campos esquecidos quando o contrato evoluir.
/// </summary>
public sealed class AnomalyContextRow
{
    public required Guid ContextId { get; init; }

    public required DateTimeOffset CreatedAtUtc { get; init; }

    public required Guid CorrelationId { get; init; }

    public required Guid? ExperimentRunId { get; init; }

    public required string Service { get; init; }

    public required Guid PrimarySignalId { get; init; }

    public required string ContextHash { get; init; }

    public required string Payload { get; init; }
}
