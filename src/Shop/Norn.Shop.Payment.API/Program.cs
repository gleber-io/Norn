using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Norn.BuildingBlocks.Chaos;
using Norn.BuildingBlocks.Messaging;
using Norn.BuildingBlocks.Telemetry;
using Norn.BuildingBlocks.Web.Errors;
using Norn.BuildingBlocks.Web.FeatureFlags;
using Norn.BuildingBlocks.Web.HealthChecks;
using Norn.BuildingBlocks.Web.Routing;
using Norn.Shop.Payment.API.Application.Ports;
using Norn.Shop.Payment.API.Features.AdminChaos;
using Norn.Shop.Payment.API.Features.AuthorizeOrderPayment;
using Norn.Shop.Payment.API.Features.ProcessPayment;
using Norn.Shop.Payment.API.Infrastructure;
using Norn.Shop.Payment.API.Infrastructure.Gateway;
using Norn.Shop.Payment.API.Infrastructure.Telemetry;
using Scalar.AspNetCore;
using StackExchange.Redis;

var builder = WebApplication.CreateBuilder(args);

builder.AddNornTelemetry("Norn.Shop.Payment.API");

builder.Services.AddDbContext<PaymentDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("Payment")
        ?? "Host=localhost;Database=norn;Username=norn;Password=norn"));

builder.Services.AddOptions<PaymentGatewayOptions>()
    .Bind(builder.Configuration.GetSection(PaymentGatewayOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();

builder.Services.AddScoped<IPaymentRepository, PaymentRepository>();
builder.Services.AddSingleton<IPaymentGateway, SimulatedPaymentGateway>();
builder.Services.AddScoped<IValidator<ProcessPaymentRequest>, ProcessPaymentValidator>();
builder.Services.AddScoped<IValidator<AdminChaosActivateRequest>, AdminChaosActivateValidator>();
builder.Services.AddSingleton<IPaymentMetrics, PaymentMetrics>();

var redisConnectionString = builder.Configuration["Redis:ConnectionString"] ?? "localhost:6379";
var redisConnectionMultiplexer = await ConnectionMultiplexer.ConnectAsync(redisConnectionString);
builder.Services.AddNornFeatureFlags(redisConnectionMultiplexer);
builder.Services.AddNornChaos(redisConnectionMultiplexer, builder.Configuration, "Norn.Shop.Payment.API");

builder.Services.AddNornProblemDetails();
builder.Services.AddNornHealthChecks()
    .AddPostgresReadiness<PaymentDbContext>();
builder.Services.AddOpenApi();

builder.Services.AddNornMessaging<PaymentDbContext>(builder.Configuration, consumers =>
{
    consumers.AddConsumer<OrderCreatedConsumer>();
});

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<PaymentDbContext>();
    await dbContext.Database.MigrateAsync();
}

app.UseExceptionHandler();
app.UseNornChaos();

var payments = app.MapApiVersion(1);
payments.MapProcessPayment();

if (!app.Environment.IsProduction())
{
    app.MapAdminChaos();
}

app.MapOpenApi();
app.MapScalarApiReference();
app.MapNornHealthChecks();

app.Run();

/// <summary>Ponto de composição exposto para <c>WebApplicationFactory&lt;Program&gt;</c> nos testes de integração.</summary>
public partial class Program;
