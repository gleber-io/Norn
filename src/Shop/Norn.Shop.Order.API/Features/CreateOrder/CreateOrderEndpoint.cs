using MassTransit;
using Microsoft.AspNetCore.Http.HttpResults;
using Norn.BuildingBlocks.Web.Validation;
using Norn.Shop.Order.API.Application.Ports;

namespace Norn.Shop.Order.API.Features.CreateOrder;

public static class CreateOrderEndpoint
{
    public static RouteGroupBuilder MapCreateOrder(this RouteGroupBuilder group)
    {
        group.MapPost("/orders", async Task<Accepted<CreateOrderResponse>> (
                CreateOrderRequest request,
                IOrderRepository repository,
                IPublishEndpoint publishEndpoint,
                IOrderMetrics metrics,
                CancellationToken cancellationToken) =>
            {
                var order = await CreateOrderHandler.HandleAsync(request, repository, publishEndpoint, metrics, cancellationToken);

                var response = new CreateOrderResponse { OrderId = order.Id, Status = order.Status.ToString() };
                return TypedResults.Accepted($"/api/v1/orders/{order.Id}", response);
            })
            .WithValidation<CreateOrderRequest>()
            .WithName("CreateOrder");

        return group;
    }
}
