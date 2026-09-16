using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Norn.Contracts;
using Norn.Contracts.Ports;
using Norn.Knowledge;
using Norn.Knowledge.Entities;
using Shouldly;
using Xunit;

namespace Norn.API.IntegrationTests;

/// <summary>Fase 10, tarefa 1 — os cinco GETs que leem o histórico persistido via <see cref="IKnowledgeReader"/> contra um Postgres real.</summary>
[Collection(nameof(NornApiCollectionDefinition))]
public sealed class KnowledgeReaderEndpointsTests(NornApiFactory factory)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    [Fact]
    public async Task GetTopology_AfterContextPersisted_ReturnsLatestTopologyForService()
    {
        var (signal, context) = await SeedSignalAndContextAsync();
        var client = factory.CreateClient();

        var response = await client.GetAsync("/api/v1/topology", TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var topologies = await response.Content.ReadFromJsonAsync<List<TopologyInfo>>(JsonOptions, TestContext.Current.CancellationToken);
        topologies.ShouldNotBeNull();
        topologies.ShouldContain(t => t.Service == context.Topology.Service);
        _ = signal;
    }

    [Fact]
    public async Task GetSignals_AfterSignalPersisted_ReturnsIt()
    {
        var (signal, _) = await SeedSignalAndContextAsync();
        var client = factory.CreateClient();

        var response = await client.GetAsync($"/api/v1/signals?limit=500", TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var signals = await response.Content.ReadFromJsonAsync<List<AnomalySignal>>(JsonOptions, TestContext.Current.CancellationToken);
        signals.ShouldNotBeNull();
        signals.ShouldContain(s => s.SignalId == signal.SignalId);
    }

    [Fact]
    public async Task GetExperimentRun_UnknownId_ReturnsNotFound()
    {
        var client = factory.CreateClient();

        var response = await client.GetAsync($"/api/v1/experiments/{Guid.NewGuid()}", TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetExperimentRun_KnownId_ReturnsSummary()
    {
        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<KnowledgeDbContext>();
        var runId = Guid.NewGuid();
        dbContext.ExperimentRuns.Add(new ExperimentRunRow
        {
            ExperimentRunId = runId,
            Scenario = "F1",
            Arm = "C",
            Repetition = 1,
            RunOrder = 1,
            RandomizationSeed = 42,
            TargetRps = 10,
            ChaosSeed = 1,
            LoadSeed = 1,
            InjectionPhase = 0,
            Mode = "Active",
            ForecastEnabled = false,
            StartedAtUtc = DateTimeOffset.UtcNow,
            WslMemoryGb = 8,
            WslProcessors = 10,
            GitCommitSha = new string('a', 40),
        });
        await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        var client = factory.CreateClient();
        var response = await client.GetAsync($"/api/v1/experiments/{runId}", TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var summary = await response.Content.ReadFromJsonAsync<ExperimentRunSummary>(JsonOptions, TestContext.Current.CancellationToken);
        summary.ShouldNotBeNull();
        summary.ExperimentRunId.ShouldBe(runId);
        summary.Scenario.ShouldBe("F1");
    }

    private async Task<(AnomalySignal Signal, AnomalyContext Context)> SeedSignalAndContextAsync()
    {
        using var scope = factory.Services.CreateScope();
        var store = scope.ServiceProvider.GetRequiredService<IKnowledgeStore>();

        var signal = new AnomalySignal
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
        var context = new AnomalyContext
        {
            ContextId = Guid.NewGuid(),
            CreatedAtUtc = DateTimeOffset.UtcNow,
            CorrelationId = Guid.NewGuid(),
            PrimarySignal = signal,
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

        await store.SaveAnomalySignalAsync(signal, TestContext.Current.CancellationToken);
        await store.SaveAnomalyContextAsync(context, TestContext.Current.CancellationToken);

        return (signal, context);
    }
}
