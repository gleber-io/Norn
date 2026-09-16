namespace Norn.Executor;

public enum ActionApplyOutcome
{
    Succeeded,

    /// <summary>Pré-condição recusada (checada de novo aqui, e não é a mesma coisa que RBAC).</summary>
    Rejected,

    /// <summary>
    /// 403 do Kubernetes numa ação que o catálogo declara permitida (§5.4/ADR-03, tarefa 5a) —
    /// defeito de configuração, não evidência de contenção. Nunca confundir com
    /// <see cref="Rejected"/>: a checagem de startup (tarefa 1a) deveria ter barrado isto antes.
    /// </summary>
    RbacDefect,

    /// <summary>Qualquer outra falha na chamada de escrita (conexão recusada, timeout, etc.).</summary>
    Failed,
}

/// <summary>Resultado de tentar aplicar uma <see cref="Norn.Contracts.HealingAction"/> de verdade (ou simulá-la em <c>DryRun</c>).</summary>
public sealed record ActionApplyResult(ActionApplyOutcome Outcome, string? Message)
{
    public static ActionApplyResult Succeeded(string? note = null) => new(ActionApplyOutcome.Succeeded, note);

    public static ActionApplyResult Rejected(string reason) => new(ActionApplyOutcome.Rejected, reason);

    public static ActionApplyResult RbacDefect(string detail) => new(ActionApplyOutcome.RbacDefect, detail);

    public static ActionApplyResult Failed(string detail) => new(ActionApplyOutcome.Failed, detail);
}
