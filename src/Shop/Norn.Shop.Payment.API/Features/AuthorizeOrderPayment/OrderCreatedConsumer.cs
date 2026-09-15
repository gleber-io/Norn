using FluentValidation;
using MassTransit;
using Norn.BuildingBlocks.Web.FeatureFlags;
using Norn.Shop.Contracts.Events;
using Norn.Shop.Payment.API.Application.Ports;
using Norn.Shop.Payment.API.Features.ProcessPayment;

namespace Norn.Shop.Payment.API.Features.AuthorizeOrderPayment;

/// <summary>
/// Fronteira de entrada da feature — o gatilho assíncrono do saga (§5.1: <c>OrderCreated</c>
/// produzido por Order.API, consumido por Payment.API). Reusa <see cref="ProcessPaymentHandler"/>,
/// mesmo padrão do <c>ReserveStockConsumer</c> na Fase 2. Método fixo porque o evento não carrega
/// um — o endpoint HTTP de <c>ProcessPayment</c> aceita método explícito para chamadas diretas.
/// </summary>
public sealed class OrderCreatedConsumer(
    IValidator<ProcessPaymentRequest> validator,
    IPaymentRepository repository,
    IPaymentGateway gateway,
    IFeatureFlags featureFlags,
    IPaymentMetrics metrics) : IConsumer<OrderCreatedEvent>
{
    private const string DefaultMethod = "credit_card";

    public async Task Consume(ConsumeContext<OrderCreatedEvent> context)
    {
        var order = context.Message;

        var request = new ProcessPaymentRequest
        {
            OrderId = order.OrderId,
            Amount = order.TotalAmount,
            Currency = order.Currency,
            Method = DefaultMethod,
        };

        await validator.ValidateAndThrowAsync(request, context.CancellationToken);

        await ProcessPaymentHandler.HandleAsync(
            request,
            order.CorrelationId,
            order.ExperimentRunId,
            repository,
            gateway,
            featureFlags,
            context,
            metrics,
            context.CancellationToken);
    }
}
