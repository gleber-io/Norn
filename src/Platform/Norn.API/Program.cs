using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Norn.API.Events;
using Norn.API.Features.GetExperimentRun;
using Norn.API.Features.GetMode;
using Norn.API.Features.GetOutcomes;
using Norn.API.Features.GetPlans;
using Norn.API.Features.GetSignals;
using Norn.API.Features.GetTopology;
using Norn.API.Features.SetMode;
using Norn.API.Hubs;
using Norn.BuildingBlocks.Telemetry;
using Norn.BuildingBlocks.Web.Errors;
using Norn.BuildingBlocks.Web.HealthChecks;
using Norn.BuildingBlocks.Web.Routing;
using Norn.Knowledge;
using StackExchange.Redis;

const string DevCorsPolicy = "NornApiDev";

var builder = WebApplication.CreateBuilder(args);

builder.AddNornTelemetry("Norn.API");

var redisConnectionString = builder.Configuration["Redis:ConnectionString"] ?? "localhost:6379";
var connectionMultiplexer = await ConnectionMultiplexer.ConnectAsync(redisConnectionString);

builder.Services.AddNornKnowledge(builder.Configuration, connectionMultiplexer);
builder.Services.AddSignalR();
builder.Services.AddSingleton<NornEventRelayHealthState>();
builder.Services.AddHostedService<PlatformEventRelay>();

builder.Services.AddScoped<IValidator<GetSignalsRequest>, GetSignalsValidator>();
builder.Services.AddScoped<IValidator<GetPlansRequest>, GetPlansValidator>();
builder.Services.AddScoped<IValidator<GetOutcomesRequest>, GetOutcomesValidator>();
builder.Services.AddScoped<IValidator<SetModeRequest>, SetModeValidator>();

builder.Services.AddNornProblemDetails();
builder.Services.AddNornHealthChecks()
    .AddPostgresReadiness<KnowledgeDbContext>()
    .AddCheck<NornEventRelayHealthCheck>("norn-events-subscriber", tags: [HealthCheckExtensions.ReadyTag]);
builder.Services.AddOpenApi();

if (builder.Environment.IsDevelopment())
{
    builder.Services.AddCors(options => options.AddPolicy(DevCorsPolicy, policy =>
        policy.WithOrigins("http://localhost:5173")
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials())); // AllowCredentials é exigido pelo handshake de negotiate do SignalR sob CORS.
}

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<KnowledgeDbContext>();
    await dbContext.Database.MigrateAsync();
}

app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.UseCors(DevCorsPolicy);
}

var v1 = app.MapApiVersion(1);
v1.MapGetTopology();
v1.MapGetSignals();
v1.MapGetPlans();
v1.MapGetOutcomes();
v1.MapGetMode();
v1.MapSetMode();
v1.MapGetExperimentRun();

app.MapHub<NornHub>("/hubs/norn");
app.MapOpenApi();
app.MapNornHealthChecks();

app.Run();

/// <summary>Ponto de composição exposto para <c>WebApplicationFactory&lt;Program&gt;</c> nos testes de integração.</summary>
public partial class Program;
