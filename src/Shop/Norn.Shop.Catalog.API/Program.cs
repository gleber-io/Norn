using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Norn.BuildingBlocks.Chaos;
using Norn.BuildingBlocks.Messaging;
using Norn.BuildingBlocks.Telemetry;
using Norn.BuildingBlocks.Web.Errors;
using Norn.BuildingBlocks.Web.HealthChecks;
using Norn.BuildingBlocks.Web.Routing;
using Norn.Shop.Catalog.API.Application.Ports;
using Norn.Shop.Catalog.API.Features.AdminChaos;
using Norn.Shop.Catalog.API.Features.CreateProduct;
using Norn.Shop.Catalog.API.Features.GetProductById;
using Norn.Shop.Catalog.API.Features.GetProducts;
using Norn.Shop.Catalog.API.Features.ReserveStock;
using Norn.Shop.Catalog.API.Infrastructure;
using Norn.Shop.Catalog.API.Infrastructure.Seed;
using Norn.Shop.Catalog.API.Infrastructure.Telemetry;
using StackExchange.Redis;

var builder = WebApplication.CreateBuilder(args);

builder.AddNornTelemetry("Norn.Shop.Catalog.API");

builder.Services.AddDbContext<CatalogDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("Catalog")
        ?? "Host=localhost;Database=norn;Username=norn;Password=norn"));

builder.Services.AddScoped<IProductRepository, ProductRepository>();
builder.Services.AddScoped<IValidator<GetProductsRequest>, GetProductsValidator>();
builder.Services.AddScoped<IValidator<CreateProductRequest>, CreateProductValidator>();
builder.Services.AddScoped<IValidator<ReserveStockRequest>, ReserveStockValidator>();
builder.Services.AddScoped<IValidator<AdminChaosActivateRequest>, AdminChaosActivateValidator>();
builder.Services.AddSingleton<ICatalogMetrics, CatalogMetrics>();

var redisConnectionString = builder.Configuration["Redis:ConnectionString"] ?? "localhost:6379";
builder.Services.AddNornChaos(await ConnectionMultiplexer.ConnectAsync(redisConnectionString), builder.Configuration, "Norn.Shop.Catalog.API");

builder.Services.AddNornProblemDetails();
builder.Services.AddNornHealthChecks()
    .AddPostgresReadiness<CatalogDbContext>();

builder.Services.AddNornMessaging<CatalogDbContext>(builder.Configuration, consumers =>
{
    consumers.AddConsumer<ReserveStockConsumer>();
});

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<CatalogDbContext>();
    await dbContext.Database.MigrateAsync();
    await ProductSeeder.SeedAsync(dbContext, CancellationToken.None);
}

app.UseExceptionHandler();
app.UseNornChaos();

var products = app.MapApiVersion(1);
products.MapGetProducts();
products.MapGetProductById();
products.MapCreateProduct();

if (!app.Environment.IsProduction())
{
    app.MapAdminChaos();
}

app.MapNornHealthChecks();

app.Run();

/// <summary>Ponto de composição exposto para <c>WebApplicationFactory&lt;Program&gt;</c> nos testes de integração.</summary>
public partial class Program;
