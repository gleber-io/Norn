namespace Norn.BuildingBlocks.Chaos;

/// <summary>Ativação/cenário/seed lidos do Redis (ADR-13) — chave <c>norn:chaos:active</c>, fora tanto de <c>shop:flags:</c> quanto de <c>norn:platform:config:</c>: não é flag do sistema gerenciado nem configuração da plataforma de cura, é controle do experimento.</summary>
public interface IChaosActivationStore
{
    Task ActivateAsync(string scenarioId, int seed, DateTimeOffset activatedAtUtc, CancellationToken cancellationToken);

    Task DeactivateAsync(CancellationToken cancellationToken);

    Task<ChaosActivation?> GetActiveAsync(CancellationToken cancellationToken);

    /// <summary>Marca que o efeito do F5 já disparou para a ativação corrente — ver <see cref="ChaosActivation.FiredAtUtc"/>.</summary>
    Task MarkFiredAsync(DateTimeOffset firedAtUtc, CancellationToken cancellationToken);
}
