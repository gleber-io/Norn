using System.Diagnostics.Metrics;

namespace Norn.BuildingBlocks.Chaos;

/// <summary>
/// <c>norn_chaos_active{scenario}</c> — valor da série é a própria intensidade (ADR-13). Emitida
/// neste único ponto para os quatro cenários, com a mesma semântica em todos: é dela que o
/// rotulador depende para marcar o onset.
/// </summary>
internal static class ChaosMetrics
{
    private static readonly Meter Meter = new("Norn.BuildingBlocks.Chaos");
    private static volatile ActiveReading? _active;

    static ChaosMetrics()
    {
        Meter.CreateObservableGauge("norn_chaos_active", Observe);
    }

    public static void Report(string scenarioId, double intensity) => _active = new ActiveReading(scenarioId, intensity);

    public static void Clear() => _active = null;

    private static IEnumerable<Measurement<double>> Observe()
    {
        var reading = _active;
        if (reading is not null)
        {
            yield return new Measurement<double>(reading.Intensity, new KeyValuePair<string, object?>("scenario", reading.ScenarioId));
        }
    }

    private sealed record ActiveReading(string ScenarioId, double Intensity);
}
