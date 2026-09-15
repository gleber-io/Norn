using Microsoft.Extensions.Options;

namespace Norn.BuildingBlocks.Chaos.Scenarios;

/// <summary>
/// F1 — alocação crescente e retida em Catalog. Rampa saturante (não linear): a intensidade se
/// aproxima de 1,0 e para de crescer, em vez de estourar sem teto. A DoD da Fase 5 exige isso —
/// sem platô, o <c>RestartPod</c> limpa a memória mas o vazamento recomeça na taxa corrente e
/// pode estourar de novo antes do fim da janela de 10 min, e o F1 só tem uma tentativa de cura.
/// </summary>
public sealed class F1MemoryRetentionScenario(IOptions<ChaosScenarioOptions> options) : IChaosScenario
{
    public string Id => ChaosScenarioIds.F1;

    public string TargetService => ChaosServiceNames.Catalog;

    public double Intensity(TimeSpan elapsed)
    {
        if (elapsed <= TimeSpan.Zero)
        {
            return 0;
        }

        var tau = options.Value.F1RampSeconds;
        return 1 - Math.Exp(-elapsed.TotalSeconds / tau);
    }
}
