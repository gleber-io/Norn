using System.Diagnostics;
using MassTransit;
using Norn.BuildingBlocks.Messaging;
using Norn.BuildingBlocks.Web.FeatureFlags;
using Norn.Shop.Contracts.Events;
using Norn.Shop.Payment.API.Application;
using Norn.Shop.Payment.API.Application.Ports;
using Norn.Shop.Payment.API.Domain;

namespace Norn.Shop.Payment.API.Features.ProcessPayment;

/// <summary>
/// Núcleo da feature, compartilhado pelo endpoint HTTP (§5.2, <c>POST /api/v1/payments</c>) e pelo
/// consumidor de <c>OrderCreated</c> (§5.1) — mesmo padrão do <c>ReserveStockHandler</c> na Fase 2.
/// </summary>
public static class ProcessPaymentHandler
{
    public static async Task<PaymentTransaction> HandleAsync(
        ProcessPaymentRequest request,
        Guid correlationId,
        Guid? experimentRunId,
        IPaymentRepository repository,
        IPaymentGateway gateway,
        IFeatureFlags featureFlags,
        IPublishEndpoint publishEndpoint,
        IPaymentMetrics metrics,
        CancellationToken cancellationToken)
    {
        var payment = new PaymentTransaction(Guid.NewGuid(), request.OrderId, request.Amount, request.Currency, request.Method);
        await repository.AddAsync(payment, cancellationToken);

        var bypass = await featureFlags.IsEnabledAsync(PaymentFeatureFlags.GatewayBypass, cancellationToken);

        if (bypass)
        {
            // Caminho degradado (Fase 3, tarefa 3a): aprova localmente com liquidação diferida,
            // sem chamar o gateway — contraparte do ToggleFeatureFlag, sem a qual a ação de cura
            // do F3 não tem efeito. `Degraded` já é a fonte da verdade; o código de autorização
            // não precisa de um prefixo próprio para significar a mesma coisa duas vezes.
            payment.Approve(payment.Id.ToString("N"), degraded: true);
            metrics.RecordDegradedPayment();
        }
        else
        {
            var stopwatch = Stopwatch.StartNew();
            var result = await gateway.AuthorizeAsync(
                new GatewayAuthorizationRequest(payment.Id, payment.OrderId, payment.Amount, payment.Currency, payment.Method),
                cancellationToken);
            stopwatch.Stop();
            metrics.RecordGatewayLatency(stopwatch.Elapsed.TotalMilliseconds);

            if (result.Approved)
            {
                payment.Approve(result.AuthorizationCode!, degraded: false);
            }
            else
            {
                payment.Decline(result.ReasonCode!, result.ReasonDescription!);
            }
        }

        await PublishOutcomeAsync(payment, correlationId, experimentRunId, publishEndpoint, cancellationToken);
        await repository.SaveChangesAsync(cancellationToken);
        metrics.RecordPaymentProcessed(payment.Status.ToString());

        return payment;
    }

    private static Task PublishOutcomeAsync(
        PaymentTransaction payment,
        Guid correlationId,
        Guid? experimentRunId,
        IPublishEndpoint publishEndpoint,
        CancellationToken cancellationToken) => payment.Status switch
        {
            PaymentStatus.Approved => publishEndpoint.PublishIntegrationEventAsync(new PaymentApprovedEvent
            {
                EventId = Guid.NewGuid(),
                EventType = nameof(PaymentApprovedEvent),
                OccurredAtUtc = DateTimeOffset.UtcNow,
                CorrelationId = correlationId,
                ExperimentRunId = experimentRunId,
                PaymentId = payment.Id,
                OrderId = payment.OrderId,
                AuthorizationCode = payment.AuthorizationCode!,
                ApprovedAtUtc = DateTimeOffset.UtcNow,
            }, cancellationToken),
            PaymentStatus.Declined => publishEndpoint.PublishIntegrationEventAsync(new PaymentDeclinedEvent
            {
                EventId = Guid.NewGuid(),
                EventType = nameof(PaymentDeclinedEvent),
                OccurredAtUtc = DateTimeOffset.UtcNow,
                CorrelationId = correlationId,
                ExperimentRunId = experimentRunId,
                PaymentId = payment.Id,
                OrderId = payment.OrderId,
                ReasonCode = payment.DeclineReasonCode!,
                ReasonDescription = payment.DeclineReasonDescription!,
            }, cancellationToken),
            _ => throw new InvalidOperationException($"Estado inesperado após decisão: {payment.Status}."),
        };
}
