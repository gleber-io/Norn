using Norn.Contracts;
using Norn.Contracts.Serialization;
using Shouldly;
using Xunit;

namespace Norn.Planner.UnitTests.TestFixtures;

/// <summary>
/// Tarefa 11 — afirma que a fixture é, de fato, o contexto real capturado: recarregar do arquivo
/// e recalcular o hash pela mesma função canônica que o Knowledge usou ao persistir deve
/// reproduzir exatamente o <c>context_hash</c> gravado em <c>anomaly_contexts</c> na captura.
/// </summary>
public sealed class F3ContextFixtureTests
{
    [Fact]
    public void Load_ReturnsContext_WhoseCanonicalHash_MatchesRecordedContextHash()
    {
        var context = F3ContextFixture.Load();

        CanonicalJson.ComputeHash(context).ShouldBe(F3ContextFixture.ExpectedContextHash);
    }

    [Fact]
    public void Load_IsTheAmbiguousF3Signature_GatewayLatencyOnly()
    {
        var context = F3ContextFixture.Load();

        context.Topology.Service.ShouldBe("Norn.Shop.Payment.API");
        context.PrimarySignal.MetricName.ShouldBe("norn_shop_payments_gateway_latency_ms");
        context.PrimarySignal.Severity.ShouldBe(Severity.Critical);
    }
}
