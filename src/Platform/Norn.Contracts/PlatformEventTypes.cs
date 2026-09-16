namespace Norn.Contracts;

/// <summary>
/// Os seis <c>eventType</c> do canal <c>norn:events</c> (§5.6, ADR-15) — exatamente estes, nem
/// mais nem menos. O <c>NornHub</c> de Norn.API (Fase 10) fecha o conjunto do lado do cliente
/// SignalR.
/// </summary>
public static class PlatformEventTypes
{
    public const string SignalDetected = nameof(SignalDetected);
    public const string PlanCreated = nameof(PlanCreated);
    public const string ActionApplied = nameof(ActionApplied);
    public const string OutcomeVerified = nameof(OutcomeVerified);
    public const string ModeChanged = nameof(ModeChanged);
    public const string TopologyUpdated = nameof(TopologyUpdated);
}
