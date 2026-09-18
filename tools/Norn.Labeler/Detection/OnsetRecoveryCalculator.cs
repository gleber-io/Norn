namespace Norn.Labeler.Detection;

/// <summary>Uma amostra de série temporal — valor em percentual (0-100) para as séries deste rotulador.</summary>
public readonly record struct MetricSample(DateTimeOffset TimestampUtc, double ValuePct);

public enum TerminationState
{
    Recovered,
    CensoredAtWindowEnd,
    InvalidNoOnset,
    InvalidInstrumentation,
}

public sealed record LabelingResult(
    DateTimeOffset? OnsetAtUtc,
    DateTimeOffset? WindowEndAtUtc,
    DateTimeOffset? RecoveredAtUtc,
    TerminationState TerminationState);

/// <summary>
/// Rotulador de onset e recuperação (Master Plan §3) — função pura, sem I/O, para ser testável sem
/// Prometheus real. As definições do §3: onset é o primeiro instante em que a taxa de erro 5xx do
/// alvo excede 1% por 30s consecutivos, **ou** ocorre <c>OOMKilled</c> — vale o que ocorrer primeiro.
/// Recuperação é o primeiro instante em que a taxa de erro fica abaixo de 0,1% por 60s consecutivos,
/// contado a partir do onset. F5 tem regra própria (§3, "O F5 tem regra própria"): a âncora é o
/// instante do kill, não o onset por taxa de erro, e <see cref="TerminationState.InvalidNoOnset"/>
/// não se aplica a ele.
/// </summary>
public static class OnsetRecoveryCalculator
{
    private const string F5Scenario = "F5";
    private const double OnsetErrorRateThresholdPct = 1.0;
    private const double RecoveryErrorRateThresholdPct = 0.1;
    private static readonly TimeSpan OnsetSustainedDuration = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan RecoverySustainedDuration = TimeSpan.FromSeconds(60);
    public static readonly TimeSpan ObservationWindow = TimeSpan.FromMinutes(10);

    /// <param name="scenario"><c>F1</c> | <c>F2</c> | <c>F3</c> | <c>F5</c>.</param>
    /// <param name="errorRatePctSamples">Taxa de erro 5xx do serviço alvo, em % (0-100), ordenada ascendente por timestamp.</param>
    /// <param name="oomKilledAtUtc">Instante do <c>OOMKilled</c> mais recente do Pod alvo dentro da janela observada, se houver (F1 — ver nota do metrics-matrix.md sobre <c>container_oom_events_total</c> não ser observável neste ambiente).</param>
    /// <param name="f5KillAtUtc">Só para F5: instante do kill abrupto, emitido pelo próprio injetor (<c>norn_chaos_active</c>).</param>
    public static LabelingResult Calculate(
        string scenario,
        IReadOnlyList<MetricSample> errorRatePctSamples,
        DateTimeOffset? oomKilledAtUtc,
        DateTimeOffset? f5KillAtUtc)
    {
        DateTimeOffset? onset;
        if (scenario == F5Scenario)
        {
            if (f5KillAtUtc is null)
            {
                return new LabelingResult(null, null, null, TerminationState.InvalidInstrumentation);
            }

            onset = f5KillAtUtc;
        }
        else
        {
            var errorOnset = FindSustainedThresholdCrossing(errorRatePctSamples, OnsetErrorRateThresholdPct, OnsetSustainedDuration, above: true);
            onset = EarliestNonNull(errorOnset, oomKilledAtUtc);
            if (onset is null)
            {
                return new LabelingResult(null, null, null, TerminationState.InvalidNoOnset);
            }
        }

        var windowEnd = onset.Value + ObservationWindow;
        var samplesInWindow = errorRatePctSamples
            .Where(s => s.TimestampUtc >= onset.Value && s.TimestampUtc <= windowEnd)
            .ToList();
        var recoveredAt = FindSustainedThresholdCrossing(samplesInWindow, RecoveryErrorRateThresholdPct, RecoverySustainedDuration, above: false);

        var terminationState = recoveredAt is not null ? TerminationState.Recovered : TerminationState.CensoredAtWindowEnd;
        return new LabelingResult(onset, windowEnd, recoveredAt, terminationState);
    }

    private static DateTimeOffset? EarliestNonNull(DateTimeOffset? a, DateTimeOffset? b)
    {
        if (a is null)
        {
            return b;
        }

        if (b is null)
        {
            return a;
        }

        return a < b ? a : b;
    }

    /// <summary>
    /// Primeiro instante em que <paramref name="samples"/> cruza o limiar e permanece do lado certo
    /// por toda a <paramref name="sustainedDuration"/> seguinte. Exige amostra depois do fim da
    /// janela sustentada para confirmar — sem isso, a borda final da série observada produziria
    /// falso positivo por falta de dado, não por comportamento real.
    /// </summary>
    private static DateTimeOffset? FindSustainedThresholdCrossing(
        IReadOnlyList<MetricSample> samples, double thresholdPct, TimeSpan sustainedDuration, bool above)
    {
        for (var i = 0; i < samples.Count; i++)
        {
            var candidate = samples[i];
            if (!Crosses(candidate.ValuePct, thresholdPct, above))
            {
                continue;
            }

            var sustainedEnd = candidate.TimestampUtc + sustainedDuration;
            var sustained = true;
            var reachedEnd = false;
            for (var j = i; j < samples.Count; j++)
            {
                if (samples[j].TimestampUtc > sustainedEnd)
                {
                    reachedEnd = true;
                    break;
                }

                if (!Crosses(samples[j].ValuePct, thresholdPct, above))
                {
                    sustained = false;
                    break;
                }
            }

            if (sustained && reachedEnd)
            {
                return candidate.TimestampUtc;
            }
        }

        return null;
    }

    private static bool Crosses(double valuePct, double thresholdPct, bool above) =>
        above ? valuePct > thresholdPct : valuePct < thresholdPct;
}
