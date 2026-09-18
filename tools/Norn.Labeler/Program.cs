using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Norn.Knowledge;
using Norn.Labeler.Commands;
using Norn.Labeler.Prometheus;
using StackExchange.Redis;

if (args.Length == 0)
{
    Console.Error.WriteLine("Uso: Norn.Labeler <reset|init-run|label> [flags]");
    return 1;
}

var builder = Host.CreateApplicationBuilder(args);

var redisConnectionString = builder.Configuration["Redis:ConnectionString"] ?? "localhost:6379";
var connectionMultiplexer = await ConnectionMultiplexer.ConnectAsync(redisConnectionString);
builder.Services.AddNornKnowledge(builder.Configuration, connectionMultiplexer);

var prometheusBaseUrl = builder.Configuration["Prometheus:BaseUrl"] ?? "http://localhost:9090";
builder.Services.AddHttpClient<PrometheusRangeClient>(client => client.BaseAddress = new Uri(prometheusBaseUrl))
    .AddStandardResilienceHandler();

using var host = builder.Build();

using (var scope = host.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<KnowledgeDbContext>();
    await dbContext.Database.MigrateAsync();
}

var command = args[0];
var commandArgs = args[1..];
using var cts = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;
    cts.Cancel();
};

using var commandScope = host.Services.CreateScope();
switch (command)
{
    case "reset":
        await ResetCommand.RunAsync(commandArgs, commandScope.ServiceProvider, cts.Token);
        break;
    case "init-run":
        await InitRunCommand.RunAsync(commandArgs, commandScope.ServiceProvider, cts.Token);
        break;
    case "label":
        await LabelCommand.RunAsync(commandArgs, commandScope.ServiceProvider, cts.Token);
        break;
    default:
        Console.Error.WriteLine($"Subcomando desconhecido: {command} (esperado reset, init-run ou label)");
        return 1;
}

return 0;
