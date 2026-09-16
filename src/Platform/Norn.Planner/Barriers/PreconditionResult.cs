namespace Norn.Planner.Barriers;

/// <summary>Resultado de uma checagem de pré-condição (§5.4) ou barreira (ADR-04).</summary>
public sealed record PreconditionResult(bool Accepted, string? RejectionReason)
{
    public static PreconditionResult Ok() => new(true, null);

    public static PreconditionResult Reject(string reason) => new(false, reason);
}
