using Microsoft.EntityFrameworkCore;
using Norn.Analyzer;
using Norn.BuildingBlocks.Telemetry;
using Norn.BuildingBlocks.Web.FeatureFlags;
using Norn.Executor;
using Norn.Executor.Rbac;
using Norn.Knowledge;
using Norn.Monitor;
using Norn.Planner;
using Norn.Worker;
using Norn.Worker.Events;
using StackExchange.Redis;

var builder = Host.CreateApplicationBuilder(args);

builder.AddNornTelemetry("Norn.Worker");

var redisConnectionString = builder.Configuration["Redis:ConnectionString"] ?? "localhost:6379";
var connectionMultiplexer = await ConnectionMultiplexer.ConnectAsync(redisConnectionString);

builder.Services.AddNornKnowledge(builder.Configuration, connectionMultiplexer);
builder.Services.AddNornMonitor(builder.Configuration);
builder.Services.AddNornAnalyzer(builder.Configuration);
builder.Services.AddNornPlanner(builder.Configuration);
builder.Services.AddNornExecutor(builder.Configuration);
builder.Services.AddNornFeatureFlags(connectionMultiplexer);
builder.Services.AddSingleton<PlatformEventPublisher>();

builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddHostedService<AnomalyPipelineBackgroundService>();

var host = builder.Build();

using (var scope = host.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<KnowledgeDbContext>();
    await dbContext.Database.MigrateAsync();

    // §5.4/ADR-03, tarefa 1a da Fase 9 — falha rápido e não sobe se a Role divergir do catálogo
    // fechado de ações ou se alguma chave de shop:flags: estiver ausente.
    var capabilityVerifier = scope.ServiceProvider.GetRequiredService<StartupCapabilityVerifier>();
    await capabilityVerifier.VerifyOrThrowAsync(CancellationToken.None);
}

await host.RunAsync();
