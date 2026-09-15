using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using MassTransit;
using Microsoft.Extensions.DependencyInjection;
using Norn.Shop.Contracts;
using Norn.Shop.Contracts.Events;
using Norn.Shop.Order.API.Features;
using Norn.Shop.Order.API.Features.CreateOrder;
using Shouldly;
using Xunit;

namespace Norn.Shop.Order.API.IntegrationTests;

/// <summary>
/// Cobre a saga completa via RabbitMQ real (Fase 3, tarefa 10): criação, e as quatro transições
/// disparadas pelos consumidores de <c>StockReserved</c>/<c>StockRejected</c>/
/// <c>PaymentApproved</c>/<c>PaymentDeclined</c>. Testcontainers Postgres + RabbitMQ — sem banco
/// ou broker compartilhado.
/// </summary>
public sealed class OrderEndpointsTests(OrderApiFactory factory) : IClassFixture<OrderApiFactory>
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly TimeSpan PollTimeout = TimeSpan.FromSeconds(30);

    [Fact]
    public async Task CreateThenGetOrder_RoundTripsThroughRealPostgres_StartsPending()
    {
        var client = factory.CreateClient();
        var request = NewOrderRequest();

        var createResponse = await client.PostAsJsonAsync("/api/v1/orders", request, JsonOptions, TestContext.Current.CancellationToken);

        createResponse.StatusCode.ShouldBe(HttpStatusCode.Accepted);
        var created = await createResponse.Content.ReadFromJsonAsync<CreateOrderResponse>(JsonOptions, TestContext.Current.CancellationToken);
        created.ShouldNotBeNull();
        created.Status.ShouldBe("Pending");

        var getResponse = await client.GetAsync($"/api/v1/orders/{created.OrderId}", TestContext.Current.CancellationToken);

        getResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        var fetched = await getResponse.Content.ReadFromJsonAsync<OrderResponse>(JsonOptions, TestContext.Current.CancellationToken);
        fetched.ShouldNotBeNull();
        fetched.Status.ShouldBe("Pending");
        fetched.Items.ShouldHaveSingleItem();
    }

    [Fact]
    public async Task GetOrderById_UnknownId_ReturnsNotFound()
    {
        var client = factory.CreateClient();

        var response = await client.GetAsync($"/api/v1/orders/{Guid.NewGuid()}", TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task StockReservedEvent_ForPendingOrder_TransitionsToStockReserved()
    {
        var client = factory.CreateClient();
        var orderId = await CreateOrderAsync(client);

        await PublishAsync(new StockReservedEvent
        {
            EventId = Guid.NewGuid(),
            EventType = nameof(StockReservedEvent),
            OccurredAtUtc = DateTimeOffset.UtcNow,
            CorrelationId = Guid.NewGuid(),
            OrderId = orderId,
            Reservations = [new StockReservation { ProductId = Guid.NewGuid(), Quantity = 1 }],
        });

        var order = await PollUntilStatusAsync(client, orderId, "StockReserved");
        order.Status.ShouldBe("StockReserved");
    }

    [Fact]
    public async Task StockRejectedEvent_ForPendingOrder_TransitionsToRejected()
    {
        var client = factory.CreateClient();
        var orderId = await CreateOrderAsync(client);

        await PublishAsync(new StockRejectedEvent
        {
            EventId = Guid.NewGuid(),
            EventType = nameof(StockRejectedEvent),
            OccurredAtUtc = DateTimeOffset.UtcNow,
            CorrelationId = Guid.NewGuid(),
            OrderId = orderId,
            ProductId = Guid.NewGuid(),
            Available = 0,
            Requested = 1,
        });

        var order = await PollUntilStatusAsync(client, orderId, "Rejected");
        order.Status.ShouldBe("Rejected");
        order.RejectionReason.ShouldNotBeNullOrEmpty();
    }

    [Fact]
    public async Task PaymentApprovedEvent_AfterStockReserved_TransitionsToConfirmed()
    {
        var client = factory.CreateClient();
        var orderId = await CreateOrderAsync(client);
        await PublishAsync(new StockReservedEvent
        {
            EventId = Guid.NewGuid(),
            EventType = nameof(StockReservedEvent),
            OccurredAtUtc = DateTimeOffset.UtcNow,
            CorrelationId = Guid.NewGuid(),
            OrderId = orderId,
            Reservations = [new StockReservation { ProductId = Guid.NewGuid(), Quantity = 1 }],
        });
        await PollUntilStatusAsync(client, orderId, "StockReserved");

        await PublishAsync(new PaymentApprovedEvent
        {
            EventId = Guid.NewGuid(),
            EventType = nameof(PaymentApprovedEvent),
            OccurredAtUtc = DateTimeOffset.UtcNow,
            CorrelationId = Guid.NewGuid(),
            PaymentId = Guid.NewGuid(),
            OrderId = orderId,
            AuthorizationCode = "AUTH123",
            ApprovedAtUtc = DateTimeOffset.UtcNow,
        });

        var order = await PollUntilStatusAsync(client, orderId, "Confirmed");
        order.Status.ShouldBe("Confirmed");
    }

    [Fact]
    public async Task PaymentDeclinedEvent_AfterStockReserved_TransitionsToRejected()
    {
        var client = factory.CreateClient();
        var orderId = await CreateOrderAsync(client);
        await PublishAsync(new StockReservedEvent
        {
            EventId = Guid.NewGuid(),
            EventType = nameof(StockReservedEvent),
            OccurredAtUtc = DateTimeOffset.UtcNow,
            CorrelationId = Guid.NewGuid(),
            OrderId = orderId,
            Reservations = [new StockReservation { ProductId = Guid.NewGuid(), Quantity = 1 }],
        });
        await PollUntilStatusAsync(client, orderId, "StockReserved");

        await PublishAsync(new PaymentDeclinedEvent
        {
            EventId = Guid.NewGuid(),
            EventType = nameof(PaymentDeclinedEvent),
            OccurredAtUtc = DateTimeOffset.UtcNow,
            CorrelationId = Guid.NewGuid(),
            PaymentId = Guid.NewGuid(),
            OrderId = orderId,
            ReasonCode = "INSUFFICIENT_FUNDS",
            ReasonDescription = "Saldo insuficiente.",
        });

        var order = await PollUntilStatusAsync(client, orderId, "Rejected");
        order.Status.ShouldBe("Rejected");
    }

    private static CreateOrderRequest NewOrderRequest() => new()
    {
        CustomerId = Guid.NewGuid(),
        Currency = "BRL",
        Items = [new CreateOrderItem { ProductId = Guid.NewGuid(), Quantity = 2, UnitPrice = 15m }],
    };

    private static async Task<Guid> CreateOrderAsync(HttpClient client)
    {
        var response = await client.PostAsJsonAsync("/api/v1/orders", NewOrderRequest(), JsonOptions, TestContext.Current.CancellationToken);
        response.StatusCode.ShouldBe(HttpStatusCode.Accepted);
        var created = await response.Content.ReadFromJsonAsync<CreateOrderResponse>(JsonOptions, TestContext.Current.CancellationToken);
        return created!.OrderId;
    }

    /// <summary>
    /// Simula um evento chegando de outro serviço (Catalog.API/Payment.API). Publica via
    /// <see cref="IBus"/>, não <see cref="IPublishEndpoint"/> resolvido de um escopo: o bus
    /// outbox (Fase 3, tarefa 4) intercepta o segundo e só libera a mensagem quando o
    /// <c>SaveChangesAsync</c> do mesmo escopo é chamado — o que nunca ocorre aqui, então a
    /// mensagem ficaria retida, nunca publicada. Em produção, quem publica estes eventos é outro
    /// processo, sem o outbox de Order.API no caminho.
    /// </summary>
    private async Task PublishAsync<TEvent>(TEvent @event)
        where TEvent : IntegrationEvent
    {
        var bus = factory.Services.GetRequiredService<IBus>();
        await bus.Publish(@event, context => context.MessageId = @event.EventId, TestContext.Current.CancellationToken);
    }

    private static async Task<OrderResponse> PollUntilStatusAsync(HttpClient client, Guid orderId, string expectedStatus)
    {
        var deadline = DateTimeOffset.UtcNow + PollTimeout;
        OrderResponse? last = null;

        while (DateTimeOffset.UtcNow < deadline)
        {
            var response = await client.GetAsync($"/api/v1/orders/{orderId}", TestContext.Current.CancellationToken);
            response.StatusCode.ShouldBe(HttpStatusCode.OK);
            last = await response.Content.ReadFromJsonAsync<OrderResponse>(JsonOptions, TestContext.Current.CancellationToken);

            if (last?.Status == expectedStatus)
            {
                return last;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(200), TestContext.Current.CancellationToken);
        }

        last.ShouldNotBeNull();
        last.Status.ShouldBe(expectedStatus, $"Pedido não alcançou '{expectedStatus}' em {PollTimeout}.");
        return last;
    }
}
