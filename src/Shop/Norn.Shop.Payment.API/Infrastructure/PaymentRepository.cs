using Microsoft.EntityFrameworkCore;
using Norn.Shop.Payment.API.Application.Ports;
using Norn.Shop.Payment.API.Domain;

namespace Norn.Shop.Payment.API.Infrastructure;

internal sealed class PaymentRepository(PaymentDbContext dbContext) : IPaymentRepository
{
    public async Task AddAsync(PaymentTransaction payment, CancellationToken cancellationToken) =>
        await dbContext.Payments.AddAsync(payment, cancellationToken);

    public Task SaveChangesAsync(CancellationToken cancellationToken) =>
        dbContext.SaveChangesAsync(cancellationToken);
}
