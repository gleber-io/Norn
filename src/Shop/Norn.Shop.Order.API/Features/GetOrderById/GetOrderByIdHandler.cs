using Norn.Shop.Order.API.Application.Ports;
using Norn.Shop.Order.API.Domain;

namespace Norn.Shop.Order.API.Features.GetOrderById;

public static class GetOrderByIdHandler
{
    public static Task<CustomerOrder?> HandleAsync(Guid id, IOrderRepository repository, CancellationToken cancellationToken) =>
        repository.GetByIdReadOnlyAsync(id, cancellationToken);
}
