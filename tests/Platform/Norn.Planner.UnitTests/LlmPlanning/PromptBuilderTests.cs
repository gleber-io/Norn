using Norn.Contracts.Serialization;
using Norn.Planner.LlmPlanning;
using Norn.Planner.Settings;
using Norn.Planner.UnitTests.TestFixtures;
using Shouldly;
using Xunit;

namespace Norn.Planner.UnitTests.LlmPlanning;

/// <summary>Tarefa 4 — orçamento de prompt medido antes de enviar, nunca truncado.</summary>
public sealed class PromptBuilderTests
{
    private static PromptBuilder CreateBuilder() => new(new PromptBudgetOptions());

    [Fact]
    public void Build_SmallContext_WithinBudget()
    {
        var context = AnomalyContextBuilder.Build();

        var result = CreateBuilder().Build(context);

        result.WithinBudget.ShouldBeTrue();
    }

    [Fact]
    public void Build_ContextInflatedWithManyCorrelatedSignals_ExceedsBudget()
    {
        var manySignals = Enumerable.Range(0, 500)
            .Select(i => AnomalyContextBuilder.Correlated($"metric_{i}_muito_longa_para_estourar_o_orcamento_de_caracteres_do_prompt"))
            .ToList();
        var context = AnomalyContextBuilder.Build(correlatedSignals: manySignals);

        var result = CreateBuilder().Build(context);

        result.WithinBudget.ShouldBeFalse();
    }

    [Fact]
    public void Build_PromptHash_MatchesCanonicalJsonHashOfContext()
    {
        var context = AnomalyContextBuilder.Build();

        var result = CreateBuilder().Build(context);

        result.PromptHash.ShouldBe(CanonicalJson.ComputeHash(context));
    }

    [Fact]
    public void Build_ContextJson_NeverTruncated()
    {
        var context = AnomalyContextBuilder.Build();

        var result = CreateBuilder().Build(context);

        result.ContextJson.ShouldBe(CanonicalJson.Serialize(context));
    }
}
