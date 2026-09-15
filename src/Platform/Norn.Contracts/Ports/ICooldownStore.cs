namespace Norn.Contracts.Ports;

/// <summary>
/// Estado quente de cooldown por alvo (ADR-04) — barreira contra loop patológico, separada
/// das pré-condições por ação da §5.4. Implementada por Norn.Knowledge (Fase 7).
/// </summary>
public interface ICooldownStore
{
    Task<bool> IsInCooldownAsync(string target, CancellationToken cancellationToken);

    Task SetCooldownAsync(string target, TimeSpan duration, CancellationToken cancellationToken);
}
