using Microsoft.EntityFrameworkCore;
using Norn.Analyzer;
using Norn.BuildingBlocks.Telemetry;
using Norn.BuildingBlocks.Web.FeatureFlags;
using Norn.Knowledge;
using Norn.Monitor;
using Norn.Worker;
using StackExchange.Redis;

var builder = Host.CreateApplicationBuilder(args);

builder.AddNornTelemetry("Norn.Worker");

var redisConnectionString = builder.Configuration["Redis:ConnectionString"] ?? "localhost:6379";
var connectionMultiplexer = await ConnectionMultiplexer.ConnectAsync(redisConnectionString);

builder.Services.AddNornKnowledge(builder.Configuration, connectionMultiplexer);
builder.Services.AddNornMonitor(builder.Configuration);
builder.Services.AddNornAnalyzer(builder.Configuration);
builder.Services.AddNornFeatureFlags(connectionMultiplexer);

builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddHostedService<AnomalyPipelineBackgroundService>();

var host = builder.Build();

using (var scope = host.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<KnowledgeDbContext>();
    await dbContext.Database.MigrateAsync();
}

await host.RunAsync();
