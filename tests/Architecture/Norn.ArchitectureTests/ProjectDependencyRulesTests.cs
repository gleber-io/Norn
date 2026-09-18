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
/// por assembly real. Norn.Worker → Norn.API (Fase 10) também: o link entre os dois processos é
/// só o canal Redis norn:events (ADR-15), nunca uma referência de projeto.
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
    private static readonly Assembly NornWorkerAssembly = typeof(Norn.Worker.Events.PlatformEventPublisher).Assembly;
    private static readonly Assembly NornApiAssembly = typeof(Norn.API.Hubs.NornHub).Assembly;
    private static readonly Assembly NornLabelerAssembly = typeof(Norn.Labeler.Detection.OnsetRecoveryCalculator).Assembly;
    private static readonly Assembly NornPairedAnalysisAssembly = typeof(Norn.PairedAnalysis.PairedDecisionCalculator).Assembly;

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

    /// <summary>
    /// O link entre Worker e API é o canal Redis norn:events (ADR-15) — o Worker publica uma
    /// mensagem, nunca conhece o hub nem o IHubContext (Fase 10).
    /// </summary>
    [Fact]
    public void NornWorker_Should_NotDependOn_NornApi()
    {
        var result = Types.InAssembly(NornWorkerAssembly)
            .Should()
            .NotHaveDependencyOn("Norn.API")
            .GetResult();

        result.IsSuccessful.ShouldBeTrue(string.Join(", ", result.FailingTypeNames ?? []));
    }

    /// <summary>
    /// Norn.API é composition root do processo BFF (ADR-17, §4) — referencia só Norn.Contracts e
    /// Norn.Knowledge. Ler estado ao vivo do cluster ou compor o loop MAPE-K não é papel dele
    /// (Fase 10).
    /// </summary>
    [Fact]
    public void NornApi_Should_NotDependOn_MapeKProjects()
    {
        var result = Types.InAssembly(NornApiAssembly)
            .Should()
            .NotHaveDependencyOnAny("Norn.Monitor", "Norn.Analyzer", "Norn.Planner", "Norn.Executor", "Norn.Worker")
            .GetResult();

        result.IsSuccessful.ShouldBeTrue(string.Join(", ", result.FailingTypeNames ?? []));
    }

    /// <summary>
    /// Exceção declarada do §4 (Fase 12): Norn.Labeler é composition root de campanha, referencia
    /// Norn.Knowledge de propósito — mas continua fora do loop MAPE-K, sem acoplar a nenhum dos
    /// projetos que o compõem.
    /// </summary>
    [Fact]
    public void NornLabeler_Should_NotDependOn_MapeKProjects()
    {
        var result = Types.InAssembly(NornLabelerAssembly)
            .Should()
            .NotHaveDependencyOnAny("Norn.Monitor", "Norn.Analyzer", "Norn.Planner", "Norn.Executor", "Norn.Worker", "Norn.API")
            .GetResult();

        result.IsSuccessful.ShouldBeTrue(string.Join(", ", result.FailingTypeNames ?? []));
    }

    /// <summary>
    /// Norn.PairedAnalysis (Fase 12, tarefa 4a) usa o RuleEngine só como função pura — nunca
    /// referencia Norn.Knowledge (lê o Postgres com Npgsql cru, §4), diferente de Norn.Labeler.
    /// </summary>
    [Fact]
    public void NornPairedAnalysis_Should_NotDependOn_NornKnowledge()
    {
        var result = Types.InAssembly(NornPairedAnalysisAssembly)
            .Should()
            .NotHaveDependencyOnAny("Norn.Knowledge", "Norn.Monitor", "Norn.Analyzer", "Norn.Executor", "Norn.Worker", "Norn.API")
            .GetResult();

        result.IsSuccessful.ShouldBeTrue(string.Join(", ", result.FailingTypeNames ?? []));
    }
}
