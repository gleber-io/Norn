namespace Norn.BuildingBlocks.Chaos.Scenarios;

/// <summary>
/// F5 — controle negativo: falha abrupta, sem rampa. Intensidade salta para 1,0 no instante da
/// ativação — o próprio kill, não uma taxa de erro crescente, é o evento (§3).
/// </summary>
public sealed class F5AbruptKillScenario : IChaosScenario
{
    public string Id => ChaosScenarioIds.F5;

    public string TargetService => ChaosServiceNames.Catalog;

    public double Intensity(TimeSpan elapsed) => elapsed >= TimeSpan.Zero ? 1.0 : 0.0;
}
