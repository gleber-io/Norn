using System.Reflection;
using NetArchTest.Rules;
using Norn.Contracts;
using Norn.Shop.Contracts;
using Shouldly;
using Xunit;

namespace Norn.ArchitectureTests;

/// <summary>
/// Regras de dependência entre projetos (§4). Cobre as arestas cujos dois lados já existem no
/// repositório: Shop.Contracts → Norn.Contracts (Fase 1) e Norn.Contracts → BuildingBlocks.Chaos
/// (Fase 5, ADR-13) — hoje só sobre <c>Norn.Contracts</c>, o único projeto de <c>Norn.Platform.*</c>
/// que existe; a asserção passa a valer também para Monitor/Analyzer/Planner/Executor assim que
/// eles existirem, sem precisar reescrever o teste. As demais proibições — Norn.Worker → Norn.API,
/// e Monitor/Analyzer/Planner/Executor → Norn.Knowledge — entram quando os projetos dos dois lados
/// existirem (Fases 7 e 10): uma regra sem assembly do lado proibido passa em verde sobre um
/// conjunto vazio, para sempre.
/// </summary>
public sealed class ProjectDependencyRulesTests
{
    private static readonly Assembly ShopContractsAssembly = typeof(IntegrationEvent).Assembly;
    private static readonly Assembly NornContractsAssembly = typeof(AnomalyContext).Assembly;

    [Fact]
    public void ShopContracts_Should_NotDependOn_PlatformContracts()
    {
        var result = Types.InAssembly(ShopContractsAssembly)
            .Should()
            .NotHaveDependencyOn("Norn.Contracts")
            .GetResult();

        result.IsSuccessful.ShouldBeTrue(string.Join(", ", result.FailingTypeNames ?? []));
    }

    [Fact]
    public void PlatformContracts_Should_NotDependOn_BuildingBlocksChaos()
    {
        var result = Types.InAssembly(NornContractsAssembly)
            .Should()
            .NotHaveDependencyOn("Norn.BuildingBlocks.Chaos")
            .GetResult();

        result.IsSuccessful.ShouldBeTrue(string.Join(", ", result.FailingTypeNames ?? []));
    }
}
