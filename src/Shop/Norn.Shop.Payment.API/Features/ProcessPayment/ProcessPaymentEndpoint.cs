using MassTransit;
using Microsoft.AspNetCore.Http.HttpResults;
using Norn.BuildingBlocks.Telemetry;
using Norn.BuildingBlocks.Web.FeatureFlags;
using Norn.BuildingBlocks.Web.Validation;
using Norn.Shop.Payment.API.Application.Ports;

namespace Norn.Shop.Payment.API.Features.ProcessPayment;

public static class ProcessPaymentEndpoint
{
    public static RouteGroupBuilder MapProcessPayment(this RouteGroupBuilder group)
    {
        group.MapPost("/payments", async Task<Accepted<ProcessPaymentResponse>> (
                ProcessPaymentRequest request,
                IPaymentRepository repository,
                IPaymentGateway gateway,
                IFeatureFlags featureFlags,
                IPublishEndpoint publishEndpoint,
                IPaymentMetrics metrics,
                CancellationToken cancellationToken) =>
            {
                var payment = await ProcessPaymentHandler.HandleAsync(
                    request,
                    correlationId: Guid.NewGuid(),
                    experimentRunId: NornBaggage.GetExperimentRunId(),
                    repository,
                    gateway,
                    featureFlags,
                    publishEndpoint,
                    metrics,
                    cancellationToken);

                var response = new ProcessPaymentResponse { PaymentId = payment.Id, Status = payment.Status.ToString() };
                return TypedResults.Accepted((string?)null, response);
            })
            .WithValidation<ProcessPaymentRequest>()
            .WithName("ProcessPayment");

        return group;
    }
}
