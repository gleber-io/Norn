using System.Text.Json;

namespace Norn.API.Hubs;

/// <summary>
/// Os seis métodos servidor→cliente do canal <c>norn:events</c> (§5.6, ADR-15), um por
/// <see cref="Norn.Contracts.PlatformEventTypes"/>. Fecha o conjunto em tempo de compilação — um
/// <c>eventType</c> desconhecido nunca vira uma chamada de método aqui (ver
/// <see cref="Norn.API.Events.PlatformEventRelay"/>). O payload trafega como
/// <see cref="JsonElement"/> cru, sem remodelagem: "o que o Worker publica é o que o navegador
/// recebe".
/// </summary>
public interface INornHubClient
{
    Task SignalDetected(JsonElement payload);

    Task PlanCreated(JsonElement payload);

    Task ActionApplied(JsonElement payload);

    Task OutcomeVerified(JsonElement payload);

    Task ModeChanged(JsonElement payload);

    Task TopologyUpdated(JsonElement payload);
}
