using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Norn.BuildingBlocks.Messaging;
using Norn.BuildingBlocks.Telemetry;
using Norn.BuildingBlocks.Web.Errors;
using Norn.BuildingBlocks.Web.HealthChecks;
using Norn.BuildingBlocks.Web.Routing;
using Norn.Shop.Order.API.Application.Ports;
using Norn.Shop.Order.API.Features.ConfirmOrderPayment;
using Norn.Shop.Order.API.Features.CreateOrder;
using Norn.Shop.Order.API.Features.DeclineOrderPayment;
using Norn.Shop.Order.API.Features.GetOrderById;
using Norn.Shop.Order.API.Features.RejectOrderStock;
using Norn.Shop.Order.API.Features.ReserveOrderStock;
using Norn.Shop.Order.API.Infrastructure;
using Norn.Shop.Order.API.Infrastructure.Telemetry;

var builder = WebApplication.CreateBuilder(args);

builder.AddNornTelemetry("Norn.Shop.Order.API");

builder.Services.AddDbContext<OrderDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("Order")
        ?? "Host=localhost;Database=norn;Username=norn;Password=norn"));

builder.Services.AddScoped<IOrderRepository, OrderRepository>();
builder.Services.AddScoped<IValidator<CreateOrderRequest>, CreateOrderValidator>();
builder.Services.AddSingleton<IOrderMetrics, OrderMetrics>();

builder.Services.AddNornProblemDetails();
builder.Services.AddNornHealthChecks()
    .AddPostgresReadiness<OrderDbContext>();

builder.Services.AddNornMessaging<OrderDbContext>(builder.Configuration, consumers =>
{
    consumers.AddConsumer<StockReservedConsumer>();
    consumers.AddConsumer<StockRejectedConsumer>();
    consumers.AddConsumer<PaymentApprovedConsumer>();
    consumers.AddConsumer<PaymentDeclinedConsumer>();
});

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<OrderDbContext>();
    await dbContext.Database.MigrateAsync();
}

app.UseExceptionHandler();

var orders = app.MapApiVersion(1);
orders.MapCreateOrder();
orders.MapGetOrderById();

app.MapNornHealthChecks();

app.Run();

/// <summary>Ponto de composição exposto para <c>WebApplicationFactory&lt;Program&gt;</c> nos testes de integração.</summary>
public partial class Program;
