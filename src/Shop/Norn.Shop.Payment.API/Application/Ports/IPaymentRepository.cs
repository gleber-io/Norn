using Norn.Shop.Payment.API.Domain;

namespace Norn.Shop.Payment.API.Application.Ports;

public interface IPaymentRepository
{
    Task AddAsync(PaymentTransaction payment, CancellationToken cancellationToken);

    Task SaveChangesAsync(CancellationToken cancellationToken);
}
