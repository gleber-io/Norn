namespace Norn.BuildingBlocks.Chaos;

/// <summary>Nomes de serviço usados como <see cref="IChaosScenario.TargetService"/> — os mesmos passados a <c>AddNornTelemetry</c>.</summary>
public static class ChaosServiceNames
{
    public const string Catalog = "Norn.Shop.Catalog.API";
    public const string Order = "Norn.Shop.Order.API";
    public const string Payment = "Norn.Shop.Payment.API";
}
