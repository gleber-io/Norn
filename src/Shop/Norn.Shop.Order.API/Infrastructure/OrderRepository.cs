using Microsoft.EntityFrameworkCore;
using Norn.Shop.Order.API.Application.Ports;
using Norn.Shop.Order.API.Domain;

namespace Norn.Shop.Order.API.Infrastructure;

internal sealed class OrderRepository(OrderDbContext dbContext) : IOrderRepository
{
    public Task<CustomerOrder?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
        dbContext.Orders.Include(o => o.Items).FirstOrDefaultAsync(o => o.Id == id, cancellationToken);

    public Task<CustomerOrder?> GetByIdReadOnlyAsync(Guid id, CancellationToken cancellationToken) =>
        dbContext.Orders.AsNoTracking().Include(o => o.Items).FirstOrDefaultAsync(o => o.Id == id, cancellationToken);

    public async Task AddAsync(CustomerOrder order, CancellationToken cancellationToken) =>
        await dbContext.Orders.AddAsync(order, cancellationToken);

    public Task SaveChangesAsync(CancellationToken cancellationToken) =>
        dbContext.SaveChangesAsync(cancellationToken);
}
