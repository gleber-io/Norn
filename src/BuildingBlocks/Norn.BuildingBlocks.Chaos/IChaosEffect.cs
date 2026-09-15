using Microsoft.AspNetCore.Http;

namespace Norn.BuildingBlocks.Chaos;

/// <summary>
/// Lado com I/O de um cenário de caos — o par de <see cref="IChaosScenario"/>, que é a rampa pura.
/// <see cref="TickAsync"/> roda a cada ciclo do <c>ChaosBackgroundService</c> enquanto o cenário
/// está ativo e mira este serviço; <see cref="OnRequestAsync"/> roda por requisição HTTP quando
/// aplicável (F2, F3); <see cref="Reset"/> libera estado quando o cenário desativa ou troca.
/// </summary>
internal interface IChaosEffect
{
    string ScenarioId { get; }

    ValueTask TickAsync(ChaosActivation activation, double intensity, CancellationToken cancellationToken);

    ValueTask OnRequestAsync(HttpContext context, RequestDelegate next);

    void Reset();
}
