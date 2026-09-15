using System.ComponentModel.DataAnnotations;

namespace Norn.BuildingBlocks.Chaos;

/// <summary>Parâmetros de calibração das rampas (§8, Fase 5, "riscos": 2-3 iterações de calibração por cenário).</summary>
public sealed class ChaosScenarioOptions
{
    public const string SectionName = "Chaos";

    /// <summary>Constante de tempo da saturação exponencial do F1, em segundos — ver DoD da Fase 5 sobre o platô.</summary>
    [Range(1, double.MaxValue)]
    public double F1RampSeconds { get; set; } = 90;

    /// <summary>Bytes retidos quando a intensidade do F1 chega a 1,0.</summary>
    [Range(1, long.MaxValue)]
    public long F1MaxRetainedBytes { get; set; } = 300_000_000;

    /// <summary>Duração da rampa linear do F2, em segundos, até intensidade 1,0.</summary>
    [Range(1, double.MaxValue)]
    public double F2RampSeconds { get; set; } = 180;

    /// <summary>Permits do limitador de concorrência com intensidade 0 (regime normal).</summary>
    [Range(1, int.MaxValue)]
    public int F2BaselineConcurrency { get; set; } = 64;

    /// <summary>Piso de permits com intensidade 1,0 — nunca chega a zero para não travar o serviço por completo.</summary>
    [Range(1, int.MaxValue)]
    public int F2MinConcurrency { get; set; } = 2;

    /// <summary>Duração da rampa linear do F3, em segundos, até intensidade 1,0.</summary>
    [Range(1, double.MaxValue)]
    public double F3RampSeconds { get; set; } = 180;

    /// <summary>Latência adicional, em ms, injetada no gateway simulado quando a intensidade do F3 chega a 1,0.</summary>
    [Range(1, int.MaxValue)]
    public int F3MaxAdditionalLatencyMilliseconds { get; set; } = 3000;
}
