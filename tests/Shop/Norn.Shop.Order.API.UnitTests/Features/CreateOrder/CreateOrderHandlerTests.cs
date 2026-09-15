using MassTransit;
using Norn.Shop.Order.API.Application.Ports;
using Norn.Shop.Order.API.Domain;
using Norn.Shop.Order.API.Features.CreateOrder;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Norn.Shop.Order.API.UnitTests.Features.CreateOrder;

public sealed class CreateOrderHandlerTests
{
    [Fact]
    public async Task HandleAsync_ValidRequest_PersistsOrderPendingAndPublishesOrderCreated()
    {
        var repository = Substitute.For<IOrderRepository>();
        var publishEndpoint = Substitute.For<IPublishEndpoint>();
        var metrics = Substitute.For<IOrderMetrics>();

        var request = new CreateOrderRequest
        {
            CustomerId = Guid.NewGuid(),
            Currency = "BRL",
            Items = [new CreateOrderItem { ProductId = Guid.NewGuid(), Quantity = 2, UnitPrice = 10m }],
        };

        var order = await CreateOrderHandler.HandleAsync(request, repository, publishEndpoint, metrics, TestContext.Current.CancellationToken);

        order.Status.ShouldBe(OrderStatus.Pending);
        order.TotalAmount.ShouldBe(20m);
        await repository.Received(1).AddAsync(order, Arg.Any<CancellationToken>());
        await repository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
        await publishEndpoint.Received(1).Publish(
            Arg.Any<Norn.Shop.Contracts.Events.OrderCreatedEvent>(),
            Arg.Any<IPipe<PublishContext<Norn.Shop.Contracts.Events.OrderCreatedEvent>>>(),
            Arg.Any<CancellationToken>());
        metrics.Received(1).RecordOrderCreated();
    }
}
