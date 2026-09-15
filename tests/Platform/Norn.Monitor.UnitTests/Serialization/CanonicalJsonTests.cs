using Norn.Contracts;
using Norn.Contracts.Serialization;
using Shouldly;
using Xunit;

namespace Norn.Monitor.UnitTests.Serialization;

/// <summary>
/// Serialização canônica (ADR-14, §5.3) — função pura, testada sem subir banco (escopo da Fase 7).
/// Determinismo do <c>context_hash</c> é o que sustenta o DoD "mesmo conjunto de sinais processado
/// duas vezes produz o mesmo context_hash" e a tarefa 11 da Fase 8 ("20 execuções do mesmo contexto").
/// </summary>
public sealed class CanonicalJsonTests
{
    [Fact]
    public void Serialize_DictionaryKeysInDifferentInsertionOrder_ProducesIdenticalJson()
    {
        var sampleA = Sample(new Dictionary<string, string> { ["z"] = "1", ["a"] = "2" });
        var sampleB = Sample(new Dictionary<string, string> { ["a"] = "2", ["z"] = "1" });

        CanonicalJson.Serialize(sampleA).ShouldBe(CanonicalJson.Serialize(sampleB));
    }

    [Fact]
    public void ComputeHash_CalledTwiceOnEquivalentValue_IsDeterministic()
    {
        var sample = Sample(new Dictionary<string, string> { ["a"] = "1" });

        var first = CanonicalJson.ComputeHash(sample);
        var second = CanonicalJson.ComputeHash(sample);

        first.ShouldBe(second);
        first.ShouldNotBeNullOrWhiteSpace();
    }

    [Fact]
    public void ComputeHash_DifferentContent_ProducesDifferentHash()
    {
        var sampleA = Sample(new Dictionary<string, string> { ["a"] = "1" });
        var sampleB = Sample(new Dictionary<string, string> { ["a"] = "2" });

        CanonicalJson.ComputeHash(sampleA).ShouldNotBe(CanonicalJson.ComputeHash(sampleB));
    }

    [Fact]
    public void ComputeHash_IsLowercaseHexSha256()
    {
        var sample = Sample(new Dictionary<string, string>());

        var hash = CanonicalJson.ComputeHash(sample);

        hash.Length.ShouldBe(64);
        hash.ShouldMatch("^[0-9a-f]{64}$");
    }

    [Fact]
    public void Serialize_TopLevelObject_OrdersKeysAlphabetically()
    {
        var sample = Sample(new Dictionary<string, string>());

        var json = CanonicalJson.Serialize(sample);

        // Ordem alfabética esperada: labels, metricName, target, timestampUtc, value.
        json.IndexOf("\"labels\"", StringComparison.Ordinal)
            .ShouldBeLessThan(json.IndexOf("\"metricName\"", StringComparison.Ordinal));
        json.IndexOf("\"metricName\"", StringComparison.Ordinal)
            .ShouldBeLessThan(json.IndexOf("\"target\"", StringComparison.Ordinal));
    }

    private static MetricSample Sample(IReadOnlyDictionary<string, string> labels) => new()
    {
        MetricName = "dotnet_process_memory_working_set_bytes",
        Target = new ServiceTarget { Service = "Norn.Shop.Catalog.API", Namespace = "norn-shop" },
        TimestampUtc = new DateTimeOffset(2026, 9, 15, 12, 0, 0, TimeSpan.Zero),
        Value = 1,
        Labels = labels,
    };
}
