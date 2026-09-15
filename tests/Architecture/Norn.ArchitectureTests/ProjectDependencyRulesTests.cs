using System.Reflection;
using NetArchTest.Rules;
using Norn.Contracts;
using Norn.Shop.Contracts;
using Shouldly;
using Xunit;

namespace Norn.ArchitectureTests;

/// <summary>
/// Regras de dependência entre projetos (§4). Só cobre, por ora, as arestas cujos dois lados já
/// existem no repositório (Fase 1): Shop.Contracts → Norn.Contracts. As demais proibições —
/// Platform.* → BuildingBlocks.Chaos, Norn.Worker → Norn.API, e Monitor/Analyzer/Planner/Executor
/// → Norn.Knowledge — entram quando os projetos dos dois lados existirem (Fases 7 e 10): uma
/// regra sem assembly do lado proibido passa em verde sobre um conjunto vazio, para sempre.
/// </summary>
public sealed class ProjectDependencyRulesTests
{
    private static readonly Assembly ShopContractsAssembly = typeof(IntegrationEvent).Assembly;

    [Fact]
    public void ShopContracts_Should_NotDependOn_PlatformContracts()
    {
        var result = Types.InAssembly(ShopContractsAssembly)
            .Should()
            .NotHaveDependencyOn("Norn.Contracts")
            .GetResult();

        result.IsSuccessful.ShouldBeTrue(string.Join(", ", result.FailingTypeNames ?? []));
    }
}
