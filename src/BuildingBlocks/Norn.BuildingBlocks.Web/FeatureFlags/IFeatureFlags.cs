namespace Norn.BuildingBlocks.Web.FeatureFlags;

/// <summary>
/// Leitura do catálogo de flags do Shop, sob <c>shop:flags:</c> (§5.7). O Shop continua sem
/// saber que o Norn existe: lê uma chave de configuração, não uma ordem.
/// </summary>
public interface IFeatureFlags
{
    Task<bool> IsEnabledAsync(string flagName, CancellationToken cancellationToken);
}
