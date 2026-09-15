using Microsoft.AspNetCore.Http.HttpResults;
using Norn.Shop.Order.API.Application.Ports;

namespace Norn.Shop.Order.API.Features.GetOrderById;

public static class GetOrderByIdEndpoint
{
    public static RouteGroupBuilder MapGetOrderById(this RouteGroupBuilder group)
    {
        group.MapGet("/orders/{id:guid}", async Task<Results<Ok<OrderResponse>, NotFound>> (
                Guid id,
                IOrderRepository repository,
                CancellationToken cancellationToken) =>
            {
                var order = await GetOrderByIdHandler.HandleAsync(id, repository, cancellationToken);

                return order is null
                    ? TypedResults.NotFound()
                    : TypedResults.Ok(OrderResponse.FromDomain(order));
            })
            .WithName("GetOrderById");

        return group;
    }
}
