using Norn.Shop.Order.API.Domain;

namespace Norn.Shop.Order.API.Application.Ports;

public interface IOrderRepository
{
    /// <summary>Rastreado — para os consumidores que mutam o agregado antes de <see cref="SaveChangesAsync"/>.</summary>
    Task<CustomerOrder?> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>Sem tracking — para o único caminho de leitura pura (<c>GET /orders/{id}</c>).</summary>
    Task<CustomerOrder?> GetByIdReadOnlyAsync(Guid id, CancellationToken cancellationToken);

    Task AddAsync(CustomerOrder order, CancellationToken cancellationToken);

    Task SaveChangesAsync(CancellationToken cancellationToken);
}
