namespace Norn.Executor.Barriers;

/// <summary>
/// Resultado da reavaliação de pré-condição imediatamente antes de aplicar (§5.4, "Executor" —
/// tarefa 2). Homólogo de <c>Norn.Planner.Barriers.PreconditionResult</c>, mas sobre estado lido
/// ao vivo do cluster/Redis, não sobre o <c>AnomalyContext</c> congelado da decisão — o par
/// decisão↔atuação não é atômico, e este é o ponto que existe por causa disso.
/// </summary>
public sealed record ExecutionPreconditionResult(
    bool Accepted,
    string? RejectionReason,
    int? TargetReplicas = null,
    bool Saturated = false)
{
    public static ExecutionPreconditionResult Ok(int? targetReplicas = null, bool saturated = false) =>
        new(true, null, targetReplicas, saturated);

    public static ExecutionPreconditionResult Reject(string reason) => new(false, reason);
}
