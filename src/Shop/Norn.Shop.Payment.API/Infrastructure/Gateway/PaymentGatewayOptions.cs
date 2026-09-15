using System.ComponentModel.DataAnnotations;

namespace Norn.Shop.Payment.API.Infrastructure.Gateway;

/// <summary>Configuração do simulador de gateway externo (Fase 3, tarefa 3) — a latência é a alavanca do cenário F3.</summary>
public sealed class PaymentGatewayOptions
{
    public const string SectionName = "Gateway";

    [Range(0, int.MaxValue)]
    public int LatencyMilliseconds { get; set; } = 200;

    // Não usar o overload Range(typeof(decimal), string, string): converte os limites via
    // TypeDescriptor com a cultura corrente, e "0.01" falha ao parsear em locale pt-BR (vírgula
    // decimal). O overload double é sempre invariante.
    [Range(0.01, double.MaxValue)]
    public decimal AuthorizationLimit { get; set; } = 100_000m;
}
