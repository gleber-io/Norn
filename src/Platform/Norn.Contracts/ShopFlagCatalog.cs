namespace Norn.Contracts;

/// <summary>
/// Catálogo fechado de flags do Shop sob <c>shop:flags:</c> (§5.7) — árvore separada da
/// configuração da plataforma (ADR-16). Fonte única: antes duplicada como literal local em
/// <c>Norn.Worker</c> (Fase 7) e de novo na pré-condição do <c>ToggleFeatureFlag</c> (Fase 8,
/// tarefa 9c) seria a terceira cópia do mesmo valor.
/// </summary>
public static class ShopFlagCatalog
{
    /// <summary>Contraparte do <c>ToggleFeatureFlag</c> (§5.4) — sem ela a ação de cura não tem efeito no F3.</summary>
    public const string PaymentGatewayBypass = "payment.gateway.bypass";

    public static readonly IReadOnlyList<string> All = [PaymentGatewayBypass];
}
