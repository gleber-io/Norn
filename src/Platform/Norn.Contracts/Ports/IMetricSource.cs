namespace Norn.Contracts.Ports;

/// <summary>Consulta ao Prometheus. Implementada por Norn.Monitor (Fase 7).</summary>
public interface IMetricSource
{
    Task<IReadOnlyList<MetricSample>> QueryRangeAsync(
        string promQlQuery,
        DateTimeOffset fromUtc,
        DateTimeOffset toUtc,
        CancellationToken cancellationToken);

    Task<MetricSample?> QueryInstantAsync(string promQlQuery, CancellationToken cancellationToken);
}
