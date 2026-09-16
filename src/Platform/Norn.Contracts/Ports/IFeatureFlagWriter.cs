namespace Norn.Contracts.Ports;

/// <summary>
/// Escrita no catálogo de flags do Shop, sob <c>shop:flags:</c> (§5.4, §5.7) — contraparte de
/// <c>IFeatureFlags</c> (Norn.BuildingBlocks.Web, leitura pelo Shop). Só o <c>ToggleFeatureFlag</c>
/// escreve aqui; nenhum outro caminho do sistema gerenciado grava nesta árvore. Implementada por
/// Norn.Knowledge (Fase 9) — mesmo prefixo e canal de invalidação que <c>RedisFeatureFlags</c>,
/// dono diferente.
/// </summary>
public interface IFeatureFlagWriter
{
    Task SetAsync(string flagName, bool value, CancellationToken cancellationToken);

    /// <summary>Verificação de startup (tarefa 1a da Fase 9) e pré-condição do <c>ToggleFeatureFlag</c>.</summary>
    Task<bool> ExistsAsync(string flagName, CancellationToken cancellationToken);

    /// <summary>Alcançabilidade do Redis — verificação de startup (tarefa 1a da Fase 9).</summary>
    Task<bool> PingAsync(CancellationToken cancellationToken);
}
