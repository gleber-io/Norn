using System.Diagnostics.Metrics;
using Norn.Contracts;
using Norn.Contracts.Ports;

namespace Norn.Executor.Telemetry;

/// <summary>
/// <c>norn_platform_actions_total{type,status}</c>, <c>norn_platform_mttr_seconds</c> e
/// <c>norn_platform_mode</c> (tarefa 9 da Fase 9). Meter próprio — registrado pelo host
/// (Norn.Worker) via <c>AddMeter("Norn.Executor")</c>, mesmo padrão de <c>PlannerMetrics</c>.
/// </summary>
public sealed class ExecutorMetrics : IDisposable
{
    public const string MeterName = "Norn.Executor";

    private readonly Meter meter = new(MeterName);
    private readonly Counter<long> actionsTotal;
    private readonly Histogram<double> mttrSeconds;
    private int currentMode = (int)PlatformMode.Observe;

    public ExecutorMetrics()
    {
        actionsTotal = meter.CreateCounter<long>("norn_platform_actions_total");
        mttrSeconds = meter.CreateHistogram<double>("norn_platform_mttr_seconds");
        meter.CreateObservableGauge("norn_platform_mode", () => Volatile.Read(ref currentMode));
    }

    public void RecordAction(HealingActionType type, HealingOutcomeStatus status) =>
        actionsTotal.Add(1,
            new KeyValuePair<string, object?>("type", type.ToString()),
            new KeyValuePair<string, object?>("status", status.ToString()));

    /// <summary>Só chamado quando <c>SloRestored</c> é verdadeiro — MTTR sem recuperação não tem significado (§3: mediana, nunca média, nunca sobre censura).</summary>
    public void RecordMttr(double seconds) => mttrSeconds.Record(seconds);

    public void RecordMode(PlatformMode mode) => Volatile.Write(ref currentMode, (int)mode);

    public void Dispose() => meter.Dispose();
}
