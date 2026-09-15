using Norn.Analyzer.Settings;
using Norn.Contracts;

namespace Norn.Analyzer.SeverityCalculation;

/// <summary>
/// Cálculo de severidade em ponto único (ADR-14, tarefa 4a) — proximidade da violação de SLO,
/// não magnitude bruta do desvio, a mesma régua para toda métrica. Função pura: sem relógio, sem
/// I/O, testável sem infraestrutura.
/// </summary>
public sealed class SeverityCalculator(SeverityBandOptions options)
{
    public sealed record Result(Severity Severity, double ExpectedValue);

    public Result Calculate(string metricName, double observedValue)
    {
        if (!options.Bands.TryGetValue(metricName, out var band))
        {
            throw new InvalidOperationException(
                $"Nenhuma banda de severidade configurada para '{metricName}' — a assinatura fechada da Fase 7 exige uma entrada por métrica.");
        }

        var ratio = band.IsSloBased
            ? observedValue / band.ReferenceValue
            : (observedValue - band.ReferenceValue) / band.ReferenceValue;

        // Desvio negativo (abaixo da baseline) nunca eleva severidade — só o excesso importa (§ADR-14).
        var effectiveRatio = Math.Max(0, ratio);

        var severity = effectiveRatio switch
        {
            _ when effectiveRatio >= band.CriticalThreshold => Severity.Critical,
            _ when effectiveRatio >= band.HighThreshold => Severity.High,
            _ when effectiveRatio >= band.MediumThreshold => Severity.Medium,
            _ => Severity.Low,
        };

        return new Result(severity, band.ReferenceValue);
    }
}
