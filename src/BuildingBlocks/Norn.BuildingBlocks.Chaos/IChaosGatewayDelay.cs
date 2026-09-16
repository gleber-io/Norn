namespace Norn.BuildingBlocks.Chaos;

/// <summary>
/// Único seam público que o cenário F3 expõe fora do módulo de caos (§8, Fase 5; corrigido na
/// Fase 8 ao tentar fechar o laço de verdade contra o F3). O alvo real do F3 é o Payment.API, mas
/// o tráfego de produção chega nele só por mensageria assíncrona
/// (<c>Order.API</c> → RabbitMQ → <c>OrderCreatedConsumer</c>) — caminho que nunca passa pelo
/// pipeline HTTP onde o restante do caos se injeta (F2 funciona porque o alvo dele, Order.API,
/// recebe tráfego real via HTTP direto, D9). Injetar o delay ali, como antes, deixava o F3
/// inatingível pelo tráfego real e ainda fora da janela medida por
/// <c>norn_shop_payments_gateway_latency_ms</c> (o <c>Stopwatch</c> de
/// <c>ProcessPaymentHandler</c> cronometra só a chamada ao gateway simulado). O consumidor deste
/// seam é o próprio <c>SimulatedPaymentGateway</c>, que soma o valor ao seu delay fixo de
/// configuração — assim o efeito chega por qualquer caminho de chamada (HTTP direto ou consumer da
/// fila) e é capturado pela métrica que já cronometra exatamente essa chamada.
/// </summary>
public interface IChaosGatewayDelay
{
    int CurrentAdditionalDelayMilliseconds { get; }
}
