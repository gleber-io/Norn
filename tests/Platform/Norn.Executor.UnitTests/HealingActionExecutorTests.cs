using System.Net;
using k8s;
using k8s.Autorest;
using k8s.Models;
using Microsoft.Extensions.Logging.Abstractions;
using Norn.Contracts;
using Norn.Contracts.Ports;
using Norn.Executor.Barriers;
using Norn.Executor.CircuitBreaker;
using Norn.Executor.FeatureFlags;
using Norn.Executor.Kubernetes;
using Norn.Executor.Settings;
using Norn.Executor.Telemetry;
using Norn.Executor.UnitTests.TestFixtures;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Norn.Executor.UnitTests;

/// <summary>
/// Fecha o MAPE-K (tarefas 2, 4, 5, 6 da Fase 9): reavalia pré-condição, aplica (ou simula em
/// <c>DryRun</c>), verifica e produz <see cref="HealingOutcome"/>. Homólogo do teste de integração
/// 11 do plano mestre — os dois caminhos de <c>Rejected</c> (pré-condição / RBAC) produzem
/// <c>HealingOutcome.status = Rejected</c>, nunca exceção não tratada, e o caminho por
/// pré-condição não faz nenhuma chamada de escrita.
/// </summary>
public sealed class HealingActionExecutorTests
{
    private sealed record Harness(
        HealingActionExecutor Executor,
        ITopologyReader Topology,
        ICooldownStore Cooldown,
        IFeatureFlagWriter FeatureFlagWriter,
        IAppsV1Operations AppsV1,
        ICoreV1Operations CoreV1,
        IPlatformConfig PlatformConfig,
        CircuitBreakerState CircuitBreaker,
        IRecentMetricsReader RecentMetricsReader);

    private static Harness CreateHarness(ExecutorOptions? options = null)
    {
        var opts = options ?? new ExecutorOptions();

        var topology = Substitute.For<ITopologyReader>();
        topology.GetTopologyAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(HealingActionBuilder.Topology(currentReplicas: 1));
        topology.GetPodUidAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns("uid-1");

        var cooldown = Substitute.For<ICooldownStore>();
        cooldown.IsInCooldownAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(false);
        cooldown.CountRecentActionsAsync(Arg.Any<string>(), Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>()).Returns(0);

        var featureFlagWriter = Substitute.For<IFeatureFlagWriter>();

        var kubernetesClient = Substitute.For<IKubernetes>();
        var appsV1 = Substitute.For<IAppsV1Operations>();
        var coreV1 = Substitute.For<ICoreV1Operations>();
        kubernetesClient.AppsV1.Returns(appsV1);
        kubernetesClient.CoreV1.Returns(coreV1);
        // As convenientes *Async chamadas pelo código de produção são extension methods sobre as
        // *WithHttpMessagesAsync — NSubstitute só intercepta membro de interface de verdade, então
        // o dublê é configurado no nível baixo (mesmo padrão nas duas exceções abaixo).
        appsV1.PatchNamespacedDeploymentScaleWithHttpMessagesAsync(Arg.Any<V1Patch>(), Arg.Any<string>(), Arg.Any<string>(), cancellationToken: Arg.Any<CancellationToken>())
            .Returns(new HttpOperationResponse<V1Scale> { Body = new V1Scale() });
        coreV1.DeleteNamespacedPodWithHttpMessagesAsync(Arg.Any<string>(), Arg.Any<string>(), cancellationToken: Arg.Any<CancellationToken>())
            .Returns(new HttpOperationResponse<V1Pod> { Body = new V1Pod() });

        var recentMetricsReader = Substitute.For<IRecentMetricsReader>();
        recentMetricsReader.ReadAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(HealingActionBuilder.Metrics(latencyP99Ms: 50));

        var platformConfig = Substitute.For<IPlatformConfig>();
        var circuitBreaker = new CircuitBreakerState();

        var executor = new HealingActionExecutor(
            new ExecutionPreconditionChecker(topology, cooldown, opts),
            new KubernetesActionApplier(kubernetesClient, opts),
            new FeatureFlagActionApplier(featureFlagWriter),
            cooldown,
            recentMetricsReader,
            platformConfig,
            circuitBreaker,
            opts,
            new ExecutorMetrics(),
            TimeProvider.System,
            NullLogger<HealingActionExecutor>.Instance);

        return new Harness(executor, topology, cooldown, featureFlagWriter, appsV1, coreV1, platformConfig, circuitBreaker, recentMetricsReader);
    }

    [Fact]
    public async Task ExecuteAsync_NoOpPlan_SucceedsWithoutAnyWrite()
    {
        var harness = CreateHarness();
        var plan = HealingActionBuilder.Plan(HealingActionBuilder.NoOp());

        var outcome = await harness.Executor.ExecuteAsync(plan, HealingActionBuilder.Metrics(), dryRun: false, TestContext.Current.CancellationToken);

        outcome.Status.ShouldBe(HealingOutcomeStatus.Succeeded);
        await harness.CoreV1.DidNotReceiveWithAnyArgs().DeleteNamespacedPodWithHttpMessagesAsync(default!, default!, cancellationToken: TestContext.Current.CancellationToken);
        await harness.AppsV1.DidNotReceiveWithAnyArgs().PatchNamespacedDeploymentScaleWithHttpMessagesAsync(default!, default!, default!, cancellationToken: TestContext.Current.CancellationToken);
    }

    /// <summary>Tarefa 11a — recusa por pré-condição não faz nenhuma chamada de escrita.</summary>
    [Fact]
    public async Task ExecuteAsync_PreconditionRejected_ProducesRejectedOutcomeWithNoWrites()
    {
        var harness = CreateHarness();
        harness.Cooldown.IsInCooldownAsync(HealingActionBuilder.DefaultTarget.Service, Arg.Any<CancellationToken>()).Returns(true);
        var plan = HealingActionBuilder.Plan(HealingActionBuilder.ScaleUp());

        var outcome = await harness.Executor.ExecuteAsync(plan, HealingActionBuilder.Metrics(), dryRun: false, TestContext.Current.CancellationToken);

        outcome.Status.ShouldBe(HealingOutcomeStatus.Rejected);
        outcome.ErrorMessage.ShouldNotBeNull();
        await harness.AppsV1.DidNotReceiveWithAnyArgs().PatchNamespacedDeploymentScaleWithHttpMessagesAsync(default!, default!, default!, cancellationToken: TestContext.Current.CancellationToken);
        await harness.Cooldown.DidNotReceiveWithAnyArgs().SetCooldownAsync(default!, default, TestContext.Current.CancellationToken);
        await harness.Cooldown.DidNotReceiveWithAnyArgs().RecordActionAsync(default!, TestContext.Current.CancellationToken);
    }

    /// <summary>Tarefa 11b — 403 numa ação permitida pelo catálogo vira Rejected, não exceção.</summary>
    [Fact]
    public async Task ExecuteAsync_KubernetesForbidden_ProducesRejectedOutcomeNotException()
    {
        var harness = CreateHarness();
        harness.AppsV1.PatchNamespacedDeploymentScaleWithHttpMessagesAsync(Arg.Any<V1Patch>(), Arg.Any<string>(), Arg.Any<string>(), cancellationToken: Arg.Any<CancellationToken>())
            .Returns(Task.FromException<HttpOperationResponse<V1Scale>>(new HttpOperationException("forbidden")
            {
                Response = new HttpResponseMessageWrapper(new HttpResponseMessage(HttpStatusCode.Forbidden), string.Empty),
            }));
        var plan = HealingActionBuilder.Plan(HealingActionBuilder.ScaleUp());

        var outcome = await harness.Executor.ExecuteAsync(plan, HealingActionBuilder.Metrics(), dryRun: false, TestContext.Current.CancellationToken);

        outcome.Status.ShouldBe(HealingOutcomeStatus.Rejected);
    }

    [Fact]
    public async Task ExecuteAsync_DryRun_NeverTouchesClusterOrRedis()
    {
        var harness = CreateHarness();
        var plan = HealingActionBuilder.Plan(HealingActionBuilder.ScaleUp());

        var outcome = await harness.Executor.ExecuteAsync(plan, HealingActionBuilder.Metrics(), dryRun: true, TestContext.Current.CancellationToken);

        outcome.Status.ShouldBe(HealingOutcomeStatus.Succeeded);
        outcome.SloRestored.ShouldBeFalse();
        await harness.AppsV1.DidNotReceiveWithAnyArgs().PatchNamespacedDeploymentScaleWithHttpMessagesAsync(default!, default!, default!, cancellationToken: TestContext.Current.CancellationToken);
        await harness.Cooldown.DidNotReceiveWithAnyArgs().SetCooldownAsync(default!, default, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task ExecuteAsync_DryRun_ToggleFeatureFlag_NeverCallsFeatureFlagWriter()
    {
        var harness = CreateHarness();
        var plan = HealingActionBuilder.Plan(HealingActionBuilder.ToggleFeatureFlag(ShopFlagCatalog.PaymentGatewayBypass));

        await harness.Executor.ExecuteAsync(plan, HealingActionBuilder.Metrics(), dryRun: true, TestContext.Current.CancellationToken);

        await harness.FeatureFlagWriter.DidNotReceiveWithAnyArgs().SetAsync(default!, default, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task ExecuteAsync_ScaleUpSucceedsAndSloRestored_RecordsCooldownAndSucceeds()
    {
        var harness = CreateHarness();
        var plan = HealingActionBuilder.Plan(HealingActionBuilder.ScaleUp(), verificationWindowSeconds: 0);

        var outcome = await harness.Executor.ExecuteAsync(plan, HealingActionBuilder.Metrics(latencyP99Ms: 500), dryRun: false, TestContext.Current.CancellationToken);

        outcome.Status.ShouldBe(HealingOutcomeStatus.Succeeded);
        outcome.SloRestored.ShouldBeTrue();
        await harness.Cooldown.Received(1).SetCooldownAsync(HealingActionBuilder.DefaultTarget.Service, Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>());
        await harness.Cooldown.Received(1).RecordActionAsync(HealingActionBuilder.DefaultTarget.Service, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_AppliedButSloNotRestoredWithinWindow_ReturnsPartiallyApplied()
    {
        var harness = CreateHarness(new ExecutorOptions { LatencyP99RestoredMs = 300 });
        var plan = HealingActionBuilder.Plan(HealingActionBuilder.ScaleUp(), verificationWindowSeconds: 0);

        // Latência ainda alta depois da janela — a ação foi aplicada, mas o SLO não voltou.
        harness.RecentMetricsReader.ReadAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(HealingActionBuilder.Metrics(latencyP99Ms: 900));

        var outcome = await harness.Executor.ExecuteAsync(plan, HealingActionBuilder.Metrics(latencyP99Ms: 900), dryRun: false, TestContext.Current.CancellationToken);

        outcome.Status.ShouldBe(HealingOutcomeStatus.PartiallyApplied);
        outcome.SloRestored.ShouldBeFalse();
    }

    /// <summary>ADR-04, barreira (c) — 5 falhas consecutivas de aplicação forçam o modo para Observe.</summary>
    [Fact]
    public async Task ExecuteAsync_FiveConsecutiveRbacFailures_TripsCircuitBreakerAndForcesObserve()
    {
        var harness = CreateHarness(new ExecutorOptions { CircuitBreakerFailureThreshold = 5 });
        harness.AppsV1.PatchNamespacedDeploymentScaleWithHttpMessagesAsync(Arg.Any<V1Patch>(), Arg.Any<string>(), Arg.Any<string>(), cancellationToken: Arg.Any<CancellationToken>())
            .Returns(Task.FromException<HttpOperationResponse<V1Scale>>(new HttpOperationException("forbidden")
            {
                Response = new HttpResponseMessageWrapper(new HttpResponseMessage(HttpStatusCode.Forbidden), string.Empty),
            }));
        var plan = HealingActionBuilder.Plan(HealingActionBuilder.ScaleUp());

        for (var i = 0; i < 5; i++)
        {
            await harness.Executor.ExecuteAsync(plan, HealingActionBuilder.Metrics(), dryRun: false, TestContext.Current.CancellationToken);
        }

        await harness.PlatformConfig.Received(1).SetModeAsync(PlatformMode.Observe, Arg.Any<CancellationToken>());
    }
}
