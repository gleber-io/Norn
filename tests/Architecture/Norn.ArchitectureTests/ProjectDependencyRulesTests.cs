using System.Reflection;
using NetArchTest.Rules;
using Norn.Contracts;
using Norn.Shop.Contracts;
using Shouldly;
using Xunit;

namespace Norn.ArchitectureTests;

/// <summary>
/// Regras de dependência entre projetos (§4). Shop.Contracts → Norn.Contracts (Fase 1),
/// Norn.Platform.* → BuildingBlocks.Chaos (ADR-13) e Norn.Monitor/Norn.Analyzer/Norn.Planner/
/// Norn.Executor → Norn.Knowledge (ADR-17, os quatro lados existem desde a Fase 9) já são cobertos
/// por assembly real. A proibição restante — Norn.Worker → Norn.API — entra quando o projeto do
/// outro lado existir (Fase 10): uma regra sem assembly do lado proibido passa em verde sobre um
/// conjunto vazio, para sempre.
/// </summary>
public sealed class ProjectDependencyRulesTests
{
    private static readonly Assembly ShopContractsAssembly = typeof(IntegrationEvent).Assembly;
    private static readonly Assembly NornContractsAssembly = typeof(AnomalyContext).Assembly;
    private static readonly Assembly NornMonitorAssembly = typeof(Norn.Monitor.MonitorOptions).Assembly;
    private static readonly Assembly NornAnalyzerAssembly = typeof(Norn.Analyzer.Detection.MetricDetectorEngine).Assembly;
    private static readonly Assembly NornPlannerAssembly = typeof(Norn.Planner.Settings.PlannerOptions).Assembly;
    private static readonly Assembly NornExecutorAssembly = typeof(Norn.Executor.Settings.ExecutorOptions).Assembly;
    private static readonly Assembly NornKnowledgeAssembly = typeof(Norn.Knowledge.KnowledgeDbContext).Assembly;

    public static TheoryData<Assembly> PlatformAssemblies => new()
    {
        NornContractsAssembly,
        NornMonitorAssembly,
        NornAnalyzerAssembly,
        NornPlannerAssembly,
        NornExecutorAssembly,
    };

    /// <summary>
    /// Isolamento entre pares (Fase 9): Monitor/Analyzer/Planner/Executor falam só com portas de
    /// Norn.Contracts, nunca entre si — só o Worker/API compõem. Sem isto, "muda-se num lugar só"
    /// (§5.4) viraria dois projetos acoplados por engano.
    /// </summary>
    [Fact]
    public void NornExecutor_Should_NotDependOn_SiblingPlatformProjects()
    {
        var result = Types.InAssembly(NornExecutorAssembly)
            .Should()
            .NotHaveDependencyOnAny("Norn.Monitor", "Norn.Analyzer", "Norn.Planner")
            .GetResult();

        result.IsSuccessful.ShouldBeTrue(string.Join(", ", result.FailingTypeNames ?? []));
    }

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
