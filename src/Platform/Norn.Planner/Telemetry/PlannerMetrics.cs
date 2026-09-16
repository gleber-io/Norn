using System.Diagnostics.Metrics;
using Norn.Contracts;

namespace Norn.Planner.Telemetry;

/// <summary>
/// <c>norn_platform_planning_latency_seconds</c>, <c>norn_platform_llm_repair_attempts_total</c> e
/// <c>norn_platform_fallback_total</c> (tarefa 8). Meter próprio — registrado pelo host
/// (Norn.Worker) via <c>AddMeter("Norn.Planner")</c>, mesmo padrão de <c>AnalyzerMetrics</c>.
/// </summary>
public sealed class PlannerMetrics : IDisposable
{
    public const string MeterName = "Norn.Planner";

    private readonly Meter meter = new(MeterName);
    private readonly Histogram<double> planningLatencySeconds;
    private readonly Counter<long> llmRepairAttemptsTotal;
    private readonly Counter<long> fallbackTotal;

    public PlannerMetrics()
    {
        planningLatencySeconds = meter.CreateHistogram<double>("norn_platform_planning_latency_seconds");
        llmRepairAttemptsTotal = meter.CreateCounter<long>("norn_platform_llm_repair_attempts_total");
        fallbackTotal = meter.CreateCounter<long>("norn_platform_fallback_total");
    }

    public void RecordPlanningLatency(TimeSpan latency, DecidedBy decidedBy) =>
        planningLatencySeconds.Record(latency.TotalSeconds, new KeyValuePair<string, object?>("planner", decidedBy.ToString()));

    public void RecordRepairAttempt() => llmRepairAttemptsTotal.Add(1);

    /// <summary>Chamado sempre que o braço B não decide sozinho — inclui os dois desfechos dos passos 6/7 do §5.5.</summary>
    public void RecordFallback(string reason) => fallbackTotal.Add(1, new KeyValuePair<string, object?>("reason", reason));

    public void Dispose() => meter.Dispose();
}
