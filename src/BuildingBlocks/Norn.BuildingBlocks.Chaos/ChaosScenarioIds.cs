namespace Norn.BuildingBlocks.Chaos;

/// <summary>Catálogo fechado de cenários (§8, Fase 5) — usado pelo validador de <c>/admin/chaos</c> nos três serviços do Shop.</summary>
public static class ChaosScenarioIds
{
    public const string F1 = "F1";
    public const string F2 = "F2";
    public const string F3 = "F3";
    public const string F5 = "F5";

    public static readonly IReadOnlyCollection<string> All = [F1, F2, F3, F5];
}
