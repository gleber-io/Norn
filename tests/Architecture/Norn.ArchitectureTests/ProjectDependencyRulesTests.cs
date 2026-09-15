using System.Reflection;
using NetArchTest.Rules;
using Norn.Contracts;
using Norn.Shop.Contracts;
using Shouldly;
using Xunit;

namespace Norn.ArchitectureTests;

/// <summary>
/// Regras de dependência entre projetos (§4). Shop.Contracts → Norn.Contracts (Fase 1),
/// Norn.Platform.* → BuildingBlocks.Chaos (ADR-13) e Norn.Monitor/Norn.Analyzer → Norn.Knowledge
/// (ADR-17, ambos os lados existem desde a Fase 7) já são cobertos por assembly real. As demais
/// proibições — Norn.Worker → Norn.API, Planner/Executor → Norn.Knowledge — entram quando os
/// projetos dos dois lados existirem (Fases 8, 9 e 10): uma regra sem assembly do lado proibido
/// passa em verde sobre um conjunto vazio, para sempre.
/// </summary>
public sealed class ProjectDependencyRulesTests
{
    private static readonly Assembly ShopContractsAssembly = typeof(IntegrationEvent).Assembly;
    private static readonly Assembly NornContractsAssembly = typeof(AnomalyContext).Assembly;
    private static readonly Assembly NornMonitorAssembly = typeof(Norn.Monitor.MonitorOptions).Assembly;
    private static readonly Assembly NornAnalyzerAssembly = typeof(Norn.Analyzer.Detection.MetricDetectorEngine).Assembly;
    private static readonly Assembly NornKnowledgeAssembly = typeof(Norn.Knowledge.KnowledgeDbContext).Assembly;

    public static TheoryData<Assembly> PlatformAssemblies => new()
    {
        NornContractsAssembly,
        NornMonitorAssembly,
        NornAnalyzerAssembly,
    };

    [Fact]
    public void ShopContracts_Should_NotDependOn_PlatformContracts()
    {
        var result = Types.InAssembly(ShopContractsAssembly)
            .Should()
            .NotHaveDependencyOn("Norn.Contracts")
            .GetResult();

        result.IsSuccessful.ShouldBeTrue(string.Join(", ", result.FailingTypeNames ?? []));
    }

    [Theory]
    [MemberData(nameof(PlatformAssemblies))]
    public void PlatformProjects_Should_NotDependOn_BuildingBlocksChaos(Assembly assembly)
    {
        var result = Types.InAssembly(assembly)
            .Should()
            .NotHaveDependencyOn("Norn.BuildingBlocks.Chaos")
            .GetResult();

        result.IsSuccessful.ShouldBeTrue(string.Join(", ", result.FailingTypeNames ?? []));
    }

    [Theory]
    [MemberData(nameof(PlatformAssemblies))]
    public void PlatformCoreProjects_Should_NotDependOn_NornKnowledge(Assembly assembly)
    {
        var result = Types.InAssembly(assembly)
            .Should()
            .NotHaveDependencyOn("Norn.Knowledge")
            .GetResult();

        result.IsSuccessful.ShouldBeTrue(string.Join(", ", result.FailingTypeNames ?? []));
    }

    [Fact]
    public void NornKnowledge_Should_NotDependOn_BuildingBlocksChaos()
    {
        var result = Types.InAssembly(NornKnowledgeAssembly)
            .Should()
            .NotHaveDependencyOn("Norn.BuildingBlocks.Chaos")
            .GetResult();

        result.IsSuccessful.ShouldBeTrue(string.Join(", ", result.FailingTypeNames ?? []));
    }
}
