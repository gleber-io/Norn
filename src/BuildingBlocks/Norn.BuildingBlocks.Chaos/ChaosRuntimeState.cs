namespace Norn.BuildingBlocks.Chaos;

/// <summary>
/// Ponte entre o <c>ChaosBackgroundService</c> (que só ele fala com o Redis e decide qual cenário
/// mira este serviço) e o <c>ChaosMiddleware</c> (que roda por requisição e não pode pagar um
/// round-trip ao Redis a cada uma). Atualizado uma vez por tick, lido a cada requisição.
/// </summary>
internal sealed class ChaosRuntimeState
{
    public IChaosEffect? ActiveEffect { get; set; }
}
