namespace Norn.BuildingBlocks.Chaos;

/// <summary>
/// Rampa de um cenário de caos (ADR-13): função pura de intensidade em função do tempo decorrido
/// desde a ativação — sem I/O, sem aleatoriedade interna, sem relógio próprio. Retorna valor em
/// [0, 1]. A aplicação prática (alocar memória, segurar concorrência, atrasar resposta, matar o
/// processo) fica em <see cref="IChaosEffect"/>, que é o lado com I/O.
/// </summary>
public interface IChaosScenario
{
    /// <summary>Identificador do catálogo fechado de cenários: <c>F1</c>, <c>F2</c>, <c>F3</c>, <c>F5</c>.</summary>
    string Id { get; }

    /// <summary>Serviço do Shop ao qual este cenário se aplica (§8, Fase 5).</summary>
    string TargetService { get; }

    double Intensity(TimeSpan elapsed);
}
