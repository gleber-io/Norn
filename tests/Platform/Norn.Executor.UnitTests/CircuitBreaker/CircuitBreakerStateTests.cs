using Norn.Executor.CircuitBreaker;
using Shouldly;
using Xunit;

namespace Norn.Executor.UnitTests.CircuitBreaker;

/// <summary>ADR-04, barreira (c) — 5 falhas consecutivas abrem o circuito; sucesso zera o contador.</summary>
public sealed class CircuitBreakerStateTests
{
    [Fact]
    public void RecordFailure_BelowThreshold_DoesNotTrip()
    {
        var breaker = new CircuitBreakerState();

        breaker.RecordFailure(threshold: 5).ShouldBeFalse();
        breaker.RecordFailure(threshold: 5).ShouldBeFalse();
        breaker.RecordFailure(threshold: 5).ShouldBeFalse();
        breaker.RecordFailure(threshold: 5).ShouldBeFalse();

        breaker.ConsecutiveFailures.ShouldBe(4);
    }

    [Fact]
    public void RecordFailure_ReachesThreshold_TripsAndResetsCounter()
    {
        var breaker = new CircuitBreakerState();

        for (var i = 0; i < 4; i++)
        {
            breaker.RecordFailure(threshold: 5).ShouldBeFalse();
        }

        breaker.RecordFailure(threshold: 5).ShouldBeTrue();
        breaker.ConsecutiveFailures.ShouldBe(0);
    }

    [Fact]
    public void RecordSuccess_ResetsConsecutiveFailures()
    {
        var breaker = new CircuitBreakerState();
        breaker.RecordFailure(threshold: 5);
        breaker.RecordFailure(threshold: 5);

        breaker.RecordSuccess();

        breaker.ConsecutiveFailures.ShouldBe(0);
    }

    /// <summary>
    /// Achado do code-reviewer na Fase 9: Increment seguido de comparação não atômica deixava duas
    /// chamadas concorrentes cruzarem o limiar juntas e as duas reportarem abertura, dobrando a
    /// contagem de "circuito abriu". Sob concorrência real (100 falhas em paralelo, threshold 5),
    /// o número de aberturas relatadas tem que ser exatamente o número de vezes que o contador de
    /// fato voltou a zero — nunca mais.
    /// </summary>
    [Fact]
    public async Task RecordFailure_UnderConcurrency_ReportsTripExactlyOncePerThresholdCrossing()
    {
        const int threshold = 5;
        const int totalFailures = 200;
        var breaker = new CircuitBreakerState();

        var tasks = Enumerable.Range(0, totalFailures)
            .Select(_ => Task.Run(() => breaker.RecordFailure(threshold)));
        var results = await Task.WhenAll(tasks);

        var tripCount = results.Count(tripped => tripped);
        tripCount.ShouldBe(totalFailures / threshold);
    }
}
