namespace Norn.Contracts;

/// <summary>
/// Catálogo fechado de ações de cura (§5.4). Qualquer valor fora deste enum é rejeitado na
/// validação de schema — o Planner só pode emitir ações daqui.
/// </summary>
public enum HealingActionType
{
    ScaleUp,
    RestartPod,
    ToggleFeatureFlag,
    NoOp,
}
