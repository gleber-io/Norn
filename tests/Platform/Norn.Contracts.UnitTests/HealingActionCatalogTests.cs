using Shouldly;
using Xunit;

namespace Norn.Contracts.UnitTests;

/// <summary>
/// O enum é a fonte da verdade dos valores permitidos (§5.5, tarefa 3); este teste garante que
/// adicionar uma ação ao catálogo sem descrevê-la em <see cref="HealingActionCatalog"/> quebra o
/// build, em vez de o system prompt do LLM silenciosamente esquecer a ação nova.
/// </summary>
public sealed class HealingActionCatalogTests
{
    [Fact]
    public void Specs_ContainsEveryValueOfHealingActionType()
    {
        foreach (var action in Enum.GetValues<HealingActionType>())
        {
            HealingActionCatalog.Specs.ShouldContainKey(action);
        }
    }

    [Fact]
    public void Specs_NoEntryHasEmptyParameterNamesOrDescription()
    {
        foreach (var spec in HealingActionCatalog.Specs.Values)
        {
            spec.Description.ShouldNotBeNullOrWhiteSpace();
            spec.PreconditionDescription.ShouldNotBeNullOrWhiteSpace();
        }
    }
}
