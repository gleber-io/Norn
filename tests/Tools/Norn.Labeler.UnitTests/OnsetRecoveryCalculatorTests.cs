using Norn.Labeler.Detection;
using Shouldly;
using Xunit;

namespace Norn.Labeler.UnitTests;

/// <summary>
/// §3: onset é o primeiro instante em que a taxa de erro 5xx excede 1% por 30s consecutivos, ou
/// ocorre OOMKilled — vale o que ocorrer primeiro. Recuperação é abaixo de 0,1% por 60s
/// consecutivos, contada a partir do onset. F5 tem regra própria (âncora no kill, não no onset).
/// </summary>
public sealed class OnsetRecoveryCalculatorTests
{
    private static readonly DateTimeOffset Epoch = new(2026, 9, 17, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Calculate_ErrorRateSustainedThenRecovers_ReturnsRecoveredWithCorrectTimestamps()
    {
        var samples = new List<MetricSample>();
        // 0-59s: saudável (0%). 60-95s: acima de 1% (36s sustentado — onset em 60s). Depois recupera.
        AddRange(samples, Epoch, 0, 60, 5, 0.0);
        AddRange(samples, Epoch.AddSeconds(60), 60, 100, 5, 5.0);
        AddRange(samples, Epoch.AddSeconds(100), 100, 400, 5, 0.0);

        var result = OnsetRecoveryCalculator.Calculate("F1", samples, oomKilledAtUtc: null, f5KillAtUtc: null);

        result.TerminationState.ShouldBe(TerminationState.Recovered);
        result.OnsetAtUtc.ShouldBe(Epoch.AddSeconds(60));
        // Recuperação exige 0,1% sustentado por 60s a partir de uma amostra abaixo do limiar — a
        // primeira candidata é a amostra de 0% em t=100s (primeira depois do onset já saudável).
        result.RecoveredAtUtc.ShouldBe(Epoch.AddSeconds(100));
    }

    [Fact]
    public void Calculate_ErrorRateNeverSustainsThirtySeconds_ReturnsInvalidNoOnset()
    {
        var samples = new List<MetricSample>();
        AddRange(samples, Epoch, 0, 60, 5, 0.0);
        // Pico isolado de 10s (duas amostras de 5s) — não sustenta os 30s exigidos.
        samples.Add(new MetricSample(Epoch.AddSeconds(60), 5.0));
        samples.Add(new MetricSample(Epoch.AddSeconds(65), 5.0));
        AddRange(samples, Epoch.AddSeconds(70), 70, 400, 5, 0.0);

        var result = OnsetRecoveryCalculator.Calculate("F2", samples, oomKilledAtUtc: null, f5KillAtUtc: null);

        result.TerminationState.ShouldBe(TerminationState.InvalidNoOnset);
        result.OnsetAtUtc.ShouldBeNull();
    }

    [Fact]
    public void Calculate_OnsetNeverRecoversWithinWindow_ReturnsCensoredAtWindowEnd()
    {
        var samples = new List<MetricSample>();
        AddRange(samples, Epoch, 0, 60, 5, 0.0);
        // Erro alto sustentado por toda a janela de observação e além — nunca recupera.
        AddRange(samples, Epoch.AddSeconds(60), 60, 900, 5, 5.0);

        var result = OnsetRecoveryCalculator.Calculate("F1", samples, oomKilledAtUtc: null, f5KillAtUtc: null);

        result.TerminationState.ShouldBe(TerminationState.CensoredAtWindowEnd);
        result.OnsetAtUtc.ShouldBe(Epoch.AddSeconds(60));
        result.RecoveredAtUtc.ShouldBeNull();
        result.WindowEndAtUtc.ShouldBe(Epoch.AddSeconds(60) + OnsetRecoveryCalculator.ObservationWindow);
    }

    [Fact]
    public void Calculate_F1_OomKilledBeforeErrorRateOnset_UsesEarlierOomInstant()
    {
        var samples = new List<MetricSample>();
        AddRange(samples, Epoch, 0, 120, 5, 0.0);
        AddRange(samples, Epoch.AddSeconds(120), 120, 400, 5, 5.0); // onset por taxa de erro em t=120s
        var oomKilledAt = Epoch.AddSeconds(40); // OOM ocorre bem antes

        var result = OnsetRecoveryCalculator.Calculate("F1", samples, oomKilledAt, f5KillAtUtc: null);

        result.OnsetAtUtc.ShouldBe(oomKilledAt);
    }

    [Fact]
    public void Calculate_F1_ErrorRateOnsetBeforeOom_UsesEarlierErrorRateInstant()
    {
        var samples = new List<MetricSample>();
        AddRange(samples, Epoch, 0, 60, 5, 0.0);
        AddRange(samples, Epoch.AddSeconds(60), 60, 400, 5, 5.0); // onset por taxa de erro em t=60s
        var oomKilledAt = Epoch.AddSeconds(200); // OOM ocorre bem depois

        var result = OnsetRecoveryCalculator.Calculate("F1", samples, oomKilledAt, f5KillAtUtc: null);

        result.OnsetAtUtc.ShouldBe(Epoch.AddSeconds(60));
    }

    [Fact]
    public void Calculate_F5WithKillInstant_AnchorsOnsetOnKillRegardlessOfErrorRate()
    {
        // F5 é kill abrupto: mesmo sem nenhuma amostra de erro sustentada, o kill é a âncora.
        var samples = new List<MetricSample> { new(Epoch, 0.0) };
        var killAt = Epoch.AddSeconds(10);

        var result = OnsetRecoveryCalculator.Calculate("F5", samples, oomKilledAtUtc: null, killAt);

        result.OnsetAtUtc.ShouldBe(killAt);
        result.TerminationState.ShouldNotBe(TerminationState.InvalidNoOnset);
    }

    [Fact]
    public void Calculate_F5WithoutKillInstant_ReturnsInvalidInstrumentation()
    {
        var samples = new List<MetricSample> { new(Epoch, 0.0) };

        var result = OnsetRecoveryCalculator.Calculate("F5", samples, oomKilledAtUtc: null, f5KillAtUtc: null);

        result.TerminationState.ShouldBe(TerminationState.InvalidInstrumentation);
    }

    [Fact]
    public void Calculate_OnsetCrossingWithoutFutureCoverage_IsNotCountedAsOnset()
    {
        // O erro sobe no fim da série observada, sem 30s de amostras depois para confirmar
        // sustentação — não pode contar como onset por falta de dado, não por comportamento real.
        var samples = new List<MetricSample>();
        AddRange(samples, Epoch, 0, 60, 5, 0.0);
        samples.Add(new MetricSample(Epoch.AddSeconds(60), 5.0));
        samples.Add(new MetricSample(Epoch.AddSeconds(65), 5.0));

        var result = OnsetRecoveryCalculator.Calculate("F3", samples, oomKilledAtUtc: null, f5KillAtUtc: null);

        result.TerminationState.ShouldBe(TerminationState.InvalidNoOnset);
    }

    [Fact]
    public void Calculate_F3_LatencyOnsetWithoutErrorRateRising_UsesLatencyTrackForOnsetAndRecovery()
    {
        // Caso real do terceiro piloto (F3/braço B): o gateway fica lento, mas as requisições
        // continuam retornando sucesso — a taxa de erro nunca cruza o limiar.
        var errorSamples = new List<MetricSample>();
        AddRange(errorSamples, Epoch, 0, 400, 5, 0.0);

        var latencySamples = new List<MetricSample>();
        AddRange(latencySamples, Epoch, 0, 60, 5, 100.0);
        AddRange(latencySamples, Epoch.AddSeconds(60), 60, 100, 5, 600.0); // acima do SLO de 500ms, 40s sustentado
        AddRange(latencySamples, Epoch.AddSeconds(100), 100, 400, 5, 100.0);

        var result = OnsetRecoveryCalculator.Calculate("F3", errorSamples, oomKilledAtUtc: null, f5KillAtUtc: null, latencySamples);

        result.TerminationState.ShouldBe(TerminationState.Recovered);
        result.OnsetAtUtc.ShouldBe(Epoch.AddSeconds(60));
        result.RecoveredAtUtc.ShouldBe(Epoch.AddSeconds(100));
    }

    [Fact]
    public void Calculate_F3_ErrorOnsetBeforeLatencyOnset_UsesEarlierErrorTrackForOnsetAndRecovery()
    {
        var errorSamples = new List<MetricSample>();
        AddRange(errorSamples, Epoch, 0, 60, 5, 0.0);
        AddRange(errorSamples, Epoch.AddSeconds(60), 60, 100, 5, 5.0); // onset por erro em t=60s
        AddRange(errorSamples, Epoch.AddSeconds(100), 100, 400, 5, 0.0);

        var latencySamples = new List<MetricSample>();
        AddRange(latencySamples, Epoch, 0, 150, 5, 100.0);
        // Cruza o SLO bem depois do erro (t=150s) e nunca recupera dentro da janela observada —
        // se o código usasse esta série por engano na recuperação, o resultado seria
        // CensoredAtWindowEnd em vez de Recovered.
        AddRange(latencySamples, Epoch.AddSeconds(150), 150, 400, 5, 600.0);

        var result = OnsetRecoveryCalculator.Calculate("F3", errorSamples, oomKilledAtUtc: null, f5KillAtUtc: null, latencySamples);

        result.OnsetAtUtc.ShouldBe(Epoch.AddSeconds(60));
        result.TerminationState.ShouldBe(TerminationState.Recovered);
        result.RecoveredAtUtc.ShouldBe(Epoch.AddSeconds(100));
    }

    [Fact]
    public void Calculate_ScenarioOtherThanF3_IgnoresGatewayLatencySamples()
    {
        var errorSamples = new List<MetricSample>();
        AddRange(errorSamples, Epoch, 0, 400, 5, 0.0);

        // Latência alta o suficiente para onsetar se fosse F3 — não deve ter efeito nenhum aqui.
        var latencySamples = new List<MetricSample>();
        AddRange(latencySamples, Epoch, 0, 400, 5, 600.0);

        var result = OnsetRecoveryCalculator.Calculate("F1", errorSamples, oomKilledAtUtc: null, f5KillAtUtc: null, latencySamples);

        result.TerminationState.ShouldBe(TerminationState.InvalidNoOnset);
    }

    [Fact]
    public void Calculate_F3_ErrorAndLatencyOnsetExactTie_ErrorTrackWinsRecovery()
    {
        // Empate exato plausível de verdade: um timeout de gateway tende a gerar 5xx e latência
        // alta na mesma amostra. Sem desempate explícito a favor do erro, EarliestNonNull devolveria
        // a via de latência (que aqui nunca recupera) e o resultado sairia CensoredAtWindowEnd por
        // engano — achado do code-reviewer antes do commit.
        var errorSamples = new List<MetricSample>();
        AddRange(errorSamples, Epoch, 0, 60, 5, 0.0);
        AddRange(errorSamples, Epoch.AddSeconds(60), 60, 100, 5, 5.0); // onset por erro em t=60s
        AddRange(errorSamples, Epoch.AddSeconds(100), 100, 400, 5, 0.0); // recupera em t=100s

        var latencySamples = new List<MetricSample>();
        AddRange(latencySamples, Epoch, 0, 60, 5, 100.0);
        AddRange(latencySamples, Epoch.AddSeconds(60), 60, 400, 5, 600.0); // onset também em t=60s, nunca recupera

        var result = OnsetRecoveryCalculator.Calculate("F3", errorSamples, oomKilledAtUtc: null, f5KillAtUtc: null, latencySamples);

        result.OnsetAtUtc.ShouldBe(Epoch.AddSeconds(60));
        result.TerminationState.ShouldBe(TerminationState.Recovered);
        result.RecoveredAtUtc.ShouldBe(Epoch.AddSeconds(100));
    }

    [Fact]
    public void Calculate_F2_LatencyOnsetWithoutErrorRateRising_UsesLatencyTrackForOnsetAndRecovery()
    {
        // Caso real da campanha da Fase 12: o Order.API enfileira sob esgotamento de pool, mas as
        // requisições continuam retornando sucesso — a taxa de erro nunca cruza o limiar.
        var errorSamples = new List<MetricSample>();
        AddRange(errorSamples, Epoch, 0, 400, 5, 0.0);

        var latencySamples = new List<MetricSample>();
        AddRange(latencySamples, Epoch, 0, 60, 5, 100.0);
        AddRange(latencySamples, Epoch.AddSeconds(60), 60, 100, 5, 450.0); // acima do SLO de 300ms, 40s sustentado
        AddRange(latencySamples, Epoch.AddSeconds(100), 100, 400, 5, 100.0);

        var result = OnsetRecoveryCalculator.Calculate("F2", errorSamples, oomKilledAtUtc: null, f5KillAtUtc: null, gatewayLatencyMsSamples: null, orderApiLatencyMsSamples: latencySamples);

        result.TerminationState.ShouldBe(TerminationState.Recovered);
        result.OnsetAtUtc.ShouldBe(Epoch.AddSeconds(60));
        result.RecoveredAtUtc.ShouldBe(Epoch.AddSeconds(100));
    }

    [Fact]
    public void Calculate_F2_ErrorOnsetBeforeLatencyOnset_UsesEarlierErrorTrackForOnsetAndRecovery()
    {
        var errorSamples = new List<MetricSample>();
        AddRange(errorSamples, Epoch, 0, 60, 5, 0.0);
        AddRange(errorSamples, Epoch.AddSeconds(60), 60, 100, 5, 5.0); // onset por erro em t=60s
        AddRange(errorSamples, Epoch.AddSeconds(100), 100, 400, 5, 0.0);

        var latencySamples = new List<MetricSample>();
        AddRange(latencySamples, Epoch, 0, 150, 5, 100.0);
        // Cruza o SLO bem depois do erro (t=150s) e nunca recupera dentro da janela observada — se
        // o código usasse esta série por engano na recuperação, o resultado seria
        // CensoredAtWindowEnd em vez de Recovered.
        AddRange(latencySamples, Epoch.AddSeconds(150), 150, 400, 5, 450.0);

        var result = OnsetRecoveryCalculator.Calculate("F2", errorSamples, oomKilledAtUtc: null, f5KillAtUtc: null, gatewayLatencyMsSamples: null, orderApiLatencyMsSamples: latencySamples);

        result.OnsetAtUtc.ShouldBe(Epoch.AddSeconds(60));
        result.TerminationState.ShouldBe(TerminationState.Recovered);
        result.RecoveredAtUtc.ShouldBe(Epoch.AddSeconds(100));
    }

    [Fact]
    public void Calculate_ScenarioOtherThanF2_IgnoresOrderApiLatencySamples()
    {
        var errorSamples = new List<MetricSample>();
        AddRange(errorSamples, Epoch, 0, 400, 5, 0.0);

        // Latência alta o suficiente para onsetar se fosse F2 — não deve ter efeito nenhum aqui.
        var latencySamples = new List<MetricSample>();
        AddRange(latencySamples, Epoch, 0, 400, 5, 450.0);

        var result = OnsetRecoveryCalculator.Calculate("F1", errorSamples, oomKilledAtUtc: null, f5KillAtUtc: null, gatewayLatencyMsSamples: null, orderApiLatencyMsSamples: latencySamples);

        result.TerminationState.ShouldBe(TerminationState.InvalidNoOnset);
    }

    [Fact]
    public void Calculate_F2_ErrorAndLatencyOnsetExactTie_ErrorTrackWinsRecovery()
    {
        // Mesmo empate exato do F3 (achado do code-reviewer: a lógica de desempate foi
        // generalizada para os dois cenários, e precisa da mesma cobertura para o F2 também).
        var errorSamples = new List<MetricSample>();
        AddRange(errorSamples, Epoch, 0, 60, 5, 0.0);
        AddRange(errorSamples, Epoch.AddSeconds(60), 60, 100, 5, 5.0); // onset por erro em t=60s
        AddRange(errorSamples, Epoch.AddSeconds(100), 100, 400, 5, 0.0); // recupera em t=100s

        var latencySamples = new List<MetricSample>();
        AddRange(latencySamples, Epoch, 0, 60, 5, 100.0);
        AddRange(latencySamples, Epoch.AddSeconds(60), 60, 400, 5, 450.0); // onset também em t=60s, nunca recupera

        var result = OnsetRecoveryCalculator.Calculate("F2", errorSamples, oomKilledAtUtc: null, f5KillAtUtc: null, gatewayLatencyMsSamples: null, orderApiLatencyMsSamples: latencySamples);

        result.OnsetAtUtc.ShouldBe(Epoch.AddSeconds(60));
        result.TerminationState.ShouldBe(TerminationState.Recovered);
        result.RecoveredAtUtc.ShouldBe(Epoch.AddSeconds(100));
    }

    private static void AddRange(List<MetricSample> samples, DateTimeOffset start, int fromSeconds, int toSeconds, int stepSeconds, double value)
    {
        for (var t = fromSeconds; t < toSeconds; t += stepSeconds)
        {
            samples.Add(new MetricSample(start.AddSeconds(t - fromSeconds), value));
        }
    }
}
