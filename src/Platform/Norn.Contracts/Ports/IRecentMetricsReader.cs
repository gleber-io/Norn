namespace Norn.Contracts.Ports;

/// <summary>
/// Leitura de métricas recentes agregadas de um serviço (§5.3, enriquecimento do
/// <see cref="AnomalyContext"/>). Porta extraída em Contratos na Fase 9 para que
/// <c>Norn.Executor</c> reaproveite exatamente a mesma leitura que o Worker usa para montar o
/// contexto (Fase 7) na verificação pós-atuação (tarefa 5) — sem isso, o verificador duplicaria as
/// seis consultas PromQL de <c>Norn.Monitor.Prometheus.RecentMetricsReader</c>. Implementada por
/// Norn.Monitor.
/// </summary>
public interface IRecentMetricsReader
{
    Task<RecentMetrics> ReadAsync(string service, CancellationToken cancellationToken);
}
