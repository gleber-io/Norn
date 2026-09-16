using Norn.Contracts;
using Norn.Contracts.Ports;
using Norn.Executor.Barriers;
using Norn.Executor.Settings;
using Norn.Executor.UnitTests.TestFixtures;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Norn.Executor.UnitTests.Barriers;

/// <summary>
/// Tarefa 2 da Fase 9 — reavaliação de pré-condição ao vivo (não sobre o contexto congelado da
/// decisão, ver <c>Norn.Planner.Barriers.HealingActionPreconditionCheckerTests</c>, Fase 8).
/// </summary>
public sealed class ExecutionPreconditionCheckerTests
{
    private static (ExecutionPreconditionChecker Checker, ITopologyReader Topology, ICooldownStore Cooldown) CreateChecker(ExecutorOptions? options = null)
    {
        var topology = Substitute.For<ITopologyReader>();
        var cooldown = Substitute.For<ICooldownStore>();
        cooldown.IsInCooldownAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(false);
        cooldown.CountRecentActionsAsync(Arg.Any<string>(), Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>()).Returns(0);

        var checker = new ExecutionPreconditionChecker(topology, cooldown, options ?? new ExecutorOptions());
        return (checker, topology, cooldown);
    }

    [Fact]
    public async Task CheckAsync_TargetInCooldown_Rejected()
    {
        var (checker, _, cooldown) = CreateChecker();
        cooldown.IsInCooldownAsync(HealingActionBuilder.DefaultTarget.Service, Arg.Any<CancellationToken>()).Returns(true);

        var result = await checker.CheckAsync(HealingActionBuilder.ScaleUp(), CancellationToken.None);

        result.Accepted.ShouldBeFalse();
    }

    [Fact]
    public async Task CheckAsync_MaxActionsPerWindowReached_Rejected()
    {
        var options = new ExecutorOptions { MaxActionsPerWindow = 3 };
        var (checker, _, cooldown) = CreateChecker(options);
        cooldown.CountRecentActionsAsync(HealingActionBuilder.DefaultTarget.Service, Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>()).Returns(3);

        var result = await checker.CheckAsync(HealingActionBuilder.ScaleUp(), CancellationToken.None);

        result.Accepted.ShouldBeFalse();
    }

    [Fact]
    public async Task CheckAsync_ScaleUp_SumWithinCeiling_AcceptedWithProjectedReplicas()
    {
        var (checker, topology, _) = CreateChecker(new ExecutorOptions { MaxReplicas = 3 });
        topology.GetTopologyAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(HealingActionBuilder.Topology(currentReplicas: 1));

        var result = await checker.CheckAsync(HealingActionBuilder.ScaleUp(replicaDelta: 1), CancellationToken.None);

        result.Accepted.ShouldBeTrue();
        result.Saturated.ShouldBeFalse();
        result.TargetReplicas.ShouldBe(2);
    }

    /// <summary>§5.4 — exceção única: satura em vez de recusar, nunca <c>Reject</c>.</summary>
    [Fact]
    public async Task CheckAsync_ScaleUp_SumExceedsCeiling_AcceptedButSaturated()
    {
        var (checker, topology, _) = CreateChecker(new ExecutorOptions { MaxReplicas = 3 });
        topology.GetTopologyAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(HealingActionBuilder.Topology(currentReplicas: 1));

        var result = await checker.CheckAsync(HealingActionBuilder.ScaleUp(replicaDelta: 3), CancellationToken.None);

        result.Accepted.ShouldBeTrue();
        result.Saturated.ShouldBeTrue();
        result.TargetReplicas.ShouldBe(3);
    }

    [Fact]
    public async Task CheckAsync_RestartPod_LiveUidMatchesAction_Accepted()
    {
        var (checker, topology, _) = CreateChecker();
        topology.GetPodUidAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns("uid-1");

        var result = await checker.CheckAsync(HealingActionBuilder.RestartPod("uid-1"), CancellationToken.None);

        result.Accepted.ShouldBeTrue();
    }

    [Fact]
    public async Task CheckAsync_RestartPod_LiveUidDivergesFromAction_Rejected()
    {
        var (checker, topology, _) = CreateChecker();
        topology.GetPodUidAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns("uid-recriado");

        var result = await checker.CheckAsync(HealingActionBuilder.RestartPod("uid-1"), CancellationToken.None);

        result.Accepted.ShouldBeFalse();
    }

    [Fact]
    public async Task CheckAsync_RestartPod_PodNoLongerExists_Rejected()
    {
        var (checker, topology, _) = CreateChecker();
        topology.GetPodUidAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns((string?)null);

        var result = await checker.CheckAsync(HealingActionBuilder.RestartPod("uid-1"), CancellationToken.None);

        result.Accepted.ShouldBeFalse();
    }

    [Fact]
    public async Task CheckAsync_RestartPod_SameTargetCooldownActive_Rejected()
    {
        var (checker, topology, cooldown) = CreateChecker();
        topology.GetPodUidAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns("uid-1");
        cooldown.IsInCooldownAsync(
                ExecutionPreconditionChecker.RestartPodCooldownTarget(HealingActionBuilder.DefaultTarget.Service), Arg.Any<CancellationToken>())
            .Returns(true);

        var result = await checker.CheckAsync(HealingActionBuilder.RestartPod("uid-1"), CancellationToken.None);

        result.Accepted.ShouldBeFalse();
    }

    [Fact]
    public async Task CheckAsync_ToggleFeatureFlag_FlagOutsideCatalog_Rejected()
    {
        var (checker, _, _) = CreateChecker();

        var result = await checker.CheckAsync(HealingActionBuilder.ToggleFeatureFlag("flag.fora.do.catalogo"), CancellationToken.None);

        result.Accepted.ShouldBeFalse();
    }

    [Fact]
    public async Task CheckAsync_ToggleFeatureFlag_FlagInCatalog_Accepted()
    {
        var (checker, _, _) = CreateChecker();

        var result = await checker.CheckAsync(HealingActionBuilder.ToggleFeatureFlag(ShopFlagCatalog.PaymentGatewayBypass), CancellationToken.None);

        result.Accepted.ShouldBeTrue();
    }
}
