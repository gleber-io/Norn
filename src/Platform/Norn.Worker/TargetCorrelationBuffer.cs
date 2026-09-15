using Norn.Contracts;

namespace Norn.Worker;

/// <summary>
/// Agrupa <see cref="AnomalySignal"/> por alvo dentro da janela de correlação (tarefa 5 da Fase 7)
/// até a janela fechar, quando é drenado para virar um <see cref="AnomalyContext"/> via
/// <c>Norn.Analyzer.Correlation.ContextCorrelator</c>. Janela tumbling ancorada no primeiro sinal
/// do alvo — não thread-safe, uso restrito ao laço único do <see cref="AnomalyPipelineBackgroundService"/>.
/// </summary>
internal sealed class TargetCorrelationBuffer
{
    private readonly Dictionary<string, List<AnomalySignal>> pendingByTarget = [];
    private readonly Dictionary<string, DateTimeOffset> windowStartByTarget = [];

    public void Add(AnomalySignal signal, DateTimeOffset now)
    {
        var key = TargetKey(signal.Target);
        if (!pendingByTarget.TryGetValue(key, out var signals))
        {
            signals = [];
            pendingByTarget[key] = signals;
            windowStartByTarget[key] = now;
        }

        signals.Add(signal);
    }

    /// <summary>Drena e remove todo alvo cuja janela já se fechou, em relação a <paramref name="now"/>.</summary>
    public IReadOnlyList<(string Service, IReadOnlyList<AnomalySignal> Signals)> DrainClosedWindows(DateTimeOffset now, TimeSpan window)
    {
        var closedKeys = windowStartByTarget
            .Where(entry => now - entry.Value >= window)
            .Select(entry => entry.Key)
            .ToArray();

        var drained = new List<(string, IReadOnlyList<AnomalySignal>)>();
        foreach (var key in closedKeys)
        {
            drained.Add((key, pendingByTarget[key]));
            pendingByTarget.Remove(key);
            windowStartByTarget.Remove(key);
        }

        return drained;
    }

    internal static string TargetKey(ServiceTarget target) => target.Service;
}
