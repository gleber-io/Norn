using Microsoft.Extensions.Options;
using Norn.Contracts;
using Norn.Contracts.Ports;

namespace Norn.Monitor.Prometheus;

/// <summary>
/// Resolve <see cref="RecentMetrics"/> para um serviço via consultas instantâneas ao Prometheus
/// (tarefa 3 — enriquecimento do <see cref="AnomalyContext"/>). <c>errorsByType</c> exige
/// múltiplas séries (uma por <c>exception_type</c>), fora do alcance de
/// <see cref="IMetricSource.QueryInstantAsync"/> (retorna uma única amostra) — por isso usa
/// <c>QueryRangeAsync</c> numa janela mínima e agrupa por rótulo.
/// </summary>
public sealed class RecentMetricsReader(IMetricSource metricSource, IOptions<MonitorOptions> options)
{
    public async Task<RecentMetrics> ReadAsync(string service, CancellationToken cancellationToken)
    {
        var cpu = await QuerySingleAsync(
            $$"""100 * rate(dotnet_process_cpu_time_seconds_total{exported_job="{{service}}"}[1m])""", cancellationToken);
        var memory = await QuerySingleAsync(
            $$"""dotnet_process_memory_working_set_bytes{exported_job="{{service}}"}""", cancellationToken);
        var requestRate = await QuerySingleAsync(
            $$"""sum(rate(http_server_request_duration_seconds_count{exported_job="{{service}}"}[1m]))""", cancellationToken);
        var errorRate = await QuerySingleAsync(
            $$"""100 * sum(rate(http_server_request_duration_seconds_count{exported_job="{{service}}", http_response_status_code=~"5.."}[30s])) / sum(rate(http_server_request_duration_seconds_count{exported_job="{{service}}"}[30s]))""",
            cancellationToken);
        var latencyP99 = await QuerySingleAsync(
            $$"""1000 * histogram_quantile(0.99, sum by (le) (rate(http_server_request_duration_seconds_bucket{exported_job="{{service}}"}[1m])))""",
            cancellationToken);
        var queueDepth = await QuerySingleAsync("""rabbitmq_queue_messages_ready{queue="ReserveStock"}""", cancellationToken);
        var errorsByType = await QueryErrorsByTypeAsync(service, cancellationToken);

        return new RecentMetrics
        {
            CpuUtilizationPct = NanToZero(cpu),
            MemoryWorkingSetBytes = (long)NanToZero(memory),
            RequestRatePerSecond = NanToZero(requestRate),
            ErrorRatePct = NanToZero(errorRate),
            LatencyP99Ms = NanToZero(latencyP99),
            QueueDepth = (int)NanToZero(queueDepth),
            ErrorsByType = errorsByType,
        };
    }

    private async Task<double> QuerySingleAsync(string promQl, CancellationToken cancellationToken)
    {
        var sample = await metricSource.QueryInstantAsync(promQl, cancellationToken);
        return sample?.Value ?? 0;
    }

    private async Task<IReadOnlyDictionary<string, int>> QueryErrorsByTypeAsync(string service, CancellationToken cancellationToken)
    {
        var query = $$"""sum by (exception_type) (norn_app_errors_total{exported_job="{{service}}"})""";
        var now = DateTimeOffset.UtcNow;
        var samples = await metricSource.QueryRangeAsync(query, now - options.Value.PollInterval, now, cancellationToken);

        return samples
            .GroupBy(sample => sample.Labels.GetValueOrDefault("exception_type", "Other"))
            .ToDictionary(group => group.Key, group => (int)group.Max(sample => sample.Value));
    }

    private static double NanToZero(double value) => double.IsNaN(value) || double.IsInfinity(value) ? 0 : value;
}
