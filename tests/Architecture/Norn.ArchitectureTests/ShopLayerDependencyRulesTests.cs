using System.Reflection;
using NetArchTest.Rules;
using Shouldly;
using Xunit;

namespace Norn.ArchitectureTests;

/// <summary>
/// Regras de camada do ADR-17 dentro de cada API do Shop (§4): <c>Domain</c> não referencia
/// nada, <c>Features</c> nunca referencia <c>Infrastructure</c>. Parametrizada por assembly —
/// Order.API e Payment.API entram como novas linhas em <see cref="ShopApiAssemblyNames"/> nas
/// Fases 3+, sem copiar o teste.
/// </summary>
public sealed class ShopLayerDependencyRulesTests
{
    public static TheoryData<string> ShopApiAssemblyNames => new()
    {
        "Norn.Shop.Catalog.API",
        "Norn.Shop.Order.API",
        "Norn.Shop.Payment.API",
    };

    [Theory]
    [MemberData(nameof(ShopApiAssemblyNames))]
    public void Domain_Should_NotDependOn_ApplicationInfrastructureOrFeatures(string assemblyName)
    {
        var assembly = Assembly.Load(assemblyName);

        var result = Types.InAssembly(assembly)
            .That().ResideInNamespace($"{assemblyName}.Domain")
            .Should().NotHaveDependencyOnAny(
                $"{assemblyName}.Application",
                $"{assemblyName}.Infrastructure",
                $"{assemblyName}.Features")
            .GetResult();

        result.IsSuccessful.ShouldBeTrue(string.Join(", ", result.FailingTypeNames ?? []));
    }

    [Theory]
    [MemberData(nameof(ShopApiAssemblyNames))]
    public void Features_Should_NotDependOn_Infrastructure(string assemblyName)
    {
        var assembly = Assembly.Load(assemblyName);

        var result = Types.InAssembly(assembly)
            .That().ResideInNamespace($"{assemblyName}.Features")
            .Should().NotHaveDependencyOn($"{assemblyName}.Infrastructure")
            .GetResult();

        result.IsSuccessful.ShouldBeTrue(string.Join(", ", result.FailingTypeNames ?? []));
    }
}
