namespace Norn.Labeler.Detection;

/// <summary>
/// Uma amostra de série temporal — <see cref="Value"/> é percentual (0-100) para taxa de erro 5xx,
/// ou milissegundos para a latência do gateway (F3); o chamador e o limiar decidem a unidade.
/// </summary>
public readonly record struct MetricSample(DateTimeOffset TimestampUtc, double Value);

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
/// F3 (gateway lento) pode nunca elevar a taxa de erro — as requisições continuam com sucesso, só
/// mais devagar — então tem uma segunda via de onset por latência sustentada acima do SLO do
/// gateway (achado ao vivo, terceiro piloto da Fase 12: uma execução real de F3 saiu
/// <c>InvalidNoOnset</c> mesmo com o cenário funcionando). Onset ainda é o que ocorrer primeiro
/// entre as vias disponíveis; a recuperação é sempre calculada na mesma série que decidiu o onset,
/// nunca misturando taxa de erro com latência (grandezas diferentes tornariam
/// <c>tempo_até_recuperação</c> incomparável entre execuções, contaminando H1).
/// </summary>
public static class OnsetRecoveryCalculator
{
    /// <summary>Nome do cenário — <c>internal</c> pra <see cref="Commands.LabelCommand"/> não duplicar a string solta.</summary>
    internal const string F5Scenario = "F5";

    /// <summary>Nome do cenário — <c>internal</c> pra <see cref="Commands.LabelCommand"/> não duplicar a string solta.</summary>
    internal const string F3Scenario = "F3";
    private const double OnsetErrorRateThresholdPct = 1.0;
    private const double RecoveryErrorRateThresholdPct = 0.1;

    /// <summary>
    /// SLO do gateway de pagamento (docs/experiments/estabilidade-llm.md; mesmo valor de
    /// <c>SeverityBandOptions.Bands["norn_shop_payments_gateway_latency_ms"]</c>, Norn.Analyzer)
    /// duplicado aqui como constante local — Norn.Labeler não referencia Norn.Analyzer (§4), mesmo
    /// motivo pelo qual o limiar de 1% de erro acima já é local. Onset e recuperação usam o mesmo
    /// valor: a sustentação de 30s/60s já é a histerese, e não há dado de campanha ainda para
    /// calibrar uma margem separada — não é constante definitiva, como o resto das bandas.
    /// </summary>
    private const double GatewayLatencySloMs = 500.0;

    private static readonly TimeSpan OnsetSustainedDuration = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan RecoverySustainedDuration = TimeSpan.FromSeconds(60);
    public static readonly TimeSpan ObservationWindow = TimeSpan.FromMinutes(10);

    /// <param name="scenario"><c>F1</c> | <c>F2</c> | <c>F3</c> | <c>F5</c>.</param>
    /// <param name="errorRatePctSamples">Taxa de erro 5xx do serviço alvo, em % (0-100), ordenada ascendente por timestamp.</param>
    /// <param name="oomKilledAtUtc">Instante do <c>OOMKilled</c> mais recente do Pod alvo dentro da janela observada, se houver (F1 — ver nota do metrics-matrix.md sobre <c>container_oom_events_total</c> não ser observável neste ambiente).</param>
    /// <param name="f5KillAtUtc">Só para F5: instante do kill abrupto, emitido pelo próprio injetor (<c>norn_chaos_active</c>).</param>
    /// <param name="gatewayLatencyMsSamples">Só relevante para F3: p99 de <c>norn_shop_payments_gateway_latency_ms</c>, em ms, ordenada ascendente por timestamp. Ignorada em qualquer outro cenário.</param>
    public static LabelingResult Calculate(
        string scenario,
        IReadOnlyList<MetricSample> errorRatePctSamples,
        DateTimeOffset? oomKilledAtUtc,
        DateTimeOffset? f5KillAtUtc,
        IReadOnlyList<MetricSample>? gatewayLatencyMsSamples = null)
    {
        DateTimeOffset? onset;
        IReadOnlyList<MetricSample> recoverySeries = errorRatePctSamples;
        var recoveryThreshold = RecoveryErrorRateThresholdPct;

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

            DateTimeOffset? latencyOnset = null;
            if (scenario == F3Scenario && gatewayLatencyMsSamples is { Count: > 0 })
            {
                latencyOnset = FindSustainedThresholdCrossing(gatewayLatencyMsSamples, GatewayLatencySloMs, OnsetSustainedDuration, above: true);
            }

            onset = EarliestNonNull(errorOnset, EarliestNonNull(latencyOnset, oomKilledAtUtc));
            if (onset is null)
            {
                return new LabelingResult(null, null, null, TerminationState.InvalidNoOnset);
            }

            // Empate exato entre vias (plausível de verdade em F3: um timeout de gateway tende a
            // gerar 5xx e latência alta na mesma amostra) não pode cair na série de latência por
            // efeito colateral de EarliestNonNull (que resolve empate devolvendo o segundo
            // argumento) — taxa de erro é a via com histórico mais testado e vence qualquer empate,
            // então latência só assume a recuperação quando é estritamente a mais cedo das duas.
            if (latencyOnset is not null
                && (errorOnset is null || latencyOnset < errorOnset)
                && (oomKilledAtUtc is null || latencyOnset < oomKilledAtUtc))
            {
                recoverySeries = gatewayLatencyMsSamples!;
                recoveryThreshold = GatewayLatencySloMs;
            }
        }

        var windowEnd = onset.Value + ObservationWindow;
        var samplesInWindow = recoverySeries
            .Where(s => s.TimestampUtc >= onset.Value && s.TimestampUtc <= windowEnd)
            .ToList();
        var recoveredAt = FindSustainedThresholdCrossing(samplesInWindow, recoveryThreshold, RecoverySustainedDuration, above: false);

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
        IReadOnlyList<MetricSample> samples, double threshold, TimeSpan sustainedDuration, bool above)
    {
        for (var i = 0; i < samples.Count; i++)
        {
            var candidate = samples[i];
            if (!Crosses(candidate.Value, threshold, above))
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

                if (!Crosses(samples[j].Value, threshold, above))
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

    private static bool Crosses(double value, double threshold, bool above) =>
        above ? value > threshold : value < threshold;
}
