namespace Norn.Contracts;

/// <summary>
/// Envelope do canal <c>norn:events</c> (§5.6, ADR-15). Genérico: Norn.Worker fecha
/// <typeparamref name="TPayload"/> sobre o DTO real ao serializar; Norn.API fecha sobre
/// <see cref="System.Text.Json.JsonElement"/> ao desserializar, para repassar o payload ao hub
/// sem remodelar — "o que o Worker publica é o que o navegador recebe".
/// </summary>
public sealed record PlatformEventEnvelope<TPayload>
{
    /// <summary>Um dos seis valores de <see cref="PlatformEventTypes"/>.</summary>
    public required string EventType { get; init; }

    public required DateTimeOffset OccurredAtUtc { get; init; }

    public Guid? RunId { get; init; }

    /// <summary>
    /// Para <c>TopologyUpdated</c>/<c>PlanCreated</c>/<c>ActionApplied</c>/<c>OutcomeVerified</c>,
    /// o mesmo <c>correlationId</c> do <see cref="AnomalyContext"/> que originou a cadeia. Dois
    /// eventos não têm cadeia real ainda neste ponto e usam um substituto sem esse significado:
    /// <c>SignalDetected</c> carrega o <c>signalId</c> (o contexto que vai nascer desta janela
    /// ainda não existe) e <c>ModeChanged</c> carrega um Guid novo a cada publicação (mudança de
    /// modo não é uma cadeia de anomalia). Um consumidor não deve tentar agrupar esses dois por
    /// este campo.
    /// </summary>
    public required Guid CorrelationId { get; init; }

    public required TPayload Payload { get; init; }
}
