namespace Norn.Contracts.Ports;

/// <summary>
/// Estado quente de cooldown por alvo (ADR-04) — barreira contra loop patológico, separada
/// das pré-condições por ação da §5.4. Implementada por Norn.Knowledge (Fase 7).
/// </summary>
public interface ICooldownStore
{
    Task<bool> IsInCooldownAsync(string target, CancellationToken cancellationToken);

    Task SetCooldownAsync(string target, TimeSpan duration, CancellationToken cancellationToken);

    /// <summary>
    /// ADR-04, barreira (b) — registra uma ação aplicada sobre o alvo, para a contagem de
    /// <see cref="CountRecentActionsAsync"/>. Chamado pelo Executor (Fase 9) só após ação com
    /// efeito real (nunca em <c>DryRun</c>, nunca em ação rejeitada).
    /// </summary>
    Task RecordActionAsync(string target, CancellationToken cancellationToken);

    /// <summary>ADR-04, barreira (b) — máximo de ações por alvo numa janela (§5.4, "Executor").</summary>
    Task<int> CountRecentActionsAsync(string target, TimeSpan window, CancellationToken cancellationToken);
}
