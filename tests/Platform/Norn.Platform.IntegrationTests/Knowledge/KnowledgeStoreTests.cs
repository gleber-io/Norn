using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Norn.Contracts;
using Norn.Contracts.Ports;
using Norn.Contracts.Serialization;
using Norn.Knowledge;
using Shouldly;
using Testcontainers.PostgreSql;
using Xunit;

namespace Norn.Platform.IntegrationTests.Knowledge;

/// <summary>
/// KnowledgeStore contra Postgres real (Testcontainers) — sem banco compartilhado, sem
/// dependência de ordem entre testes (§7.2). Cobre o DoD "Executar F1 em Observe grava o
/// AnomalyContext em anomaly_contexts, e healing_plans.context_id resolve para uma linha
/// existente" (Fase 7) — a segunda parte só é verificável com um FK real, não com dublê.
/// </summary>
public sealed class KnowledgeStoreTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer postgresContainer = new PostgreSqlBuilder("postgres:17-alpine")
        .Build();

    private ServiceProvider serviceProvider = null!;

    public async ValueTask InitializeAsync()
    {
        await postgresContainer.StartAsync();

        var services = new ServiceCollection();
        services.AddDbContext<KnowledgeDbContext>(options => options.UseNpgsql(postgresContainer.GetConnectionString()));
        services.AddScoped<IKnowledgeStore, KnowledgeStore>();
        serviceProvider = services.BuildServiceProvider();

        using var scope = serviceProvider.CreateScope();
        await scope.ServiceProvider.GetRequiredService<KnowledgeDbContext>().Database.MigrateAsync();
    }

    public async ValueTask DisposeAsync()
    {
        await serviceProvider.DisposeAsync();
        await postgresContainer.DisposeAsync();
    }

    [Fact]
    public async Task SaveAnomalyContextAsync_ThenReadBack_PersistsCanonicalContextHash()
    {
        var signal = Signal();
        var context = Context(signal);
        using var scope = serviceProvider.CreateScope();
        var store = scope.ServiceProvider.GetRequiredService<IKnowledgeStore>();
        var dbContext = scope.ServiceProvider.GetRequiredService<KnowledgeDbContext>();

        await store.SaveAnomalySignalAsync(signal, TestContext.Current.CancellationToken);
        await store.SaveAnomalyContextAsync(context, TestContext.Current.CancellationToken);

        var row = await dbContext.AnomalyContexts.SingleAsync(r => r.ContextId == context.ContextId, TestContext.Current.CancellationToken);
        row.ContextHash.ShouldBe(CanonicalJson.ComputeHash(context));
        row.PrimarySignalId.ShouldBe(signal.SignalId);
    }

    [Fact]
    public async Task SaveHealingPlanAsync_ThenReadBack_ContextIdResolvesToExistingContext()
    {
        var signal = Signal();
        var context = Context(signal);
        var plan = Plan(context);
        using var scope = serviceProvider.CreateScope();
        var store = scope.ServiceProvider.GetRequiredService<IKnowledgeStore>();
        var dbContext = scope.ServiceProvider.GetRequiredService<KnowledgeDbContext>();

        await store.SaveAnomalySignalAsync(signal, TestContext.Current.CancellationToken);
        await store.SaveAnomalyContextAsync(context, TestContext.Current.CancellationToken);
        await store.SaveHealingPlanAsync(plan, TestContext.Current.CancellationToken);

        var planRow = await dbContext.HealingPlans.SingleAsync(r => r.PlanId == plan.PlanId, TestContext.Current.CancellationToken);
        var contextExists = await dbContext.AnomalyContexts.AnyAsync(r => r.ContextId == planRow.ContextId, TestContext.Current.CancellationToken);
        contextExists.ShouldBeTrue();

        var traceRow = await dbContext.LlmTraces.SingleAsync(r => r.PlanId == plan.PlanId, TestContext.Current.CancellationToken);
        traceRow.PromptHash.ShouldBe(plan.LlmTrace.PromptHash);
    }

    private static AnomalySignal Signal() => new()
    {
        SignalId = Guid.NewGuid(),
        DetectedAtUtc = DateTimeOffset.UtcNow,
        Target = new ServiceTarget { Service = "Norn.Shop.Catalog.API", Namespace = "norn-shop" },
        MetricName = "dotnet_process_memory_working_set_bytes",
        Detector = DetectorType.SpikeDetection,
        Severity = Severity.Critical,
        Confidence = 95,
        PValue = 0.001,
        ObservedValue = 500_000_000,
        ExpectedValue = 200_000_000,
        Window = new TimeWindow { FromUtc = DateTimeOffset.UtcNow.AddMinutes(-1), ToUtc = DateTimeOffset.UtcNow },
    };

    private static AnomalyContext Context(AnomalySignal primarySignal) => new()
    {
        ContextId = Guid.NewGuid(),
        CreatedAtUtc = DateTimeOffset.UtcNow,
        CorrelationId = Guid.NewGuid(),
        PrimarySignal = primarySignal,
        Topology = new TopologyInfo
        {
            Service = "Norn.Shop.Catalog.API",
            CurrentReplicas = 1,
            DesiredReplicas = 1,
            ResourceRequests = new ResourceSpec { Cpu = "100m", Memory = "128Mi" },
            ResourceLimits = new ResourceSpec { Cpu = "500m", Memory = "256Mi" },
        },
        RecentMetrics = new RecentMetrics
        {
            CpuUtilizationPct = 10,
            MemoryWorkingSetBytes = 500_000_000,
            RequestRatePerSecond = 5,
            ErrorRatePct = 0,
            LatencyP99Ms = 50,
            QueueDepth = 0,
        },
        CooldownStatus = new CooldownStatus { IsInCooldown = false },
    };

    private static HealingPlan Plan(AnomalyContext context) => new()
    {
        PlanId = Guid.NewGuid(),
        ContextId = context.ContextId,
        CreatedAtUtc = DateTimeOffset.UtcNow,
        DecidedBy = DecidedBy.RuleEngine,
        Rationale = "Teste de integração",
        Confidence = 90,
        ExpectedOutcome = "RSS volta ao regime",
        LlmTrace = new LlmTrace { PromptHash = "abc123", LatencyMs = 0, Attempts = 1 },
    };
}
