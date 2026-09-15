namespace Norn.Shop.Payment.API.Application;

/// <summary>Nomes das flags de <c>shop:flags:</c> (§5.7) que o Payment.API lê via <c>IFeatureFlags</c>.</summary>
public static class PaymentFeatureFlags
{
    /// <summary>Contraparte do <c>ToggleFeatureFlag</c> (§5.4) — sem ela a ação de cura não tem efeito no F3.</summary>
    public const string GatewayBypass = "payment.gateway.bypass";
}
