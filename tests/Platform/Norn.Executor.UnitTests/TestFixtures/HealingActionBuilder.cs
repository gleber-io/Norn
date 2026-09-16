using System.Globalization;
using Norn.Contracts;

namespace Norn.Executor.UnitTests.TestFixtures;

/// <summary>Monta <see cref="HealingAction"/>/<see cref="HealingPlan"/> mínimos e válidos para os testes do Executor.</summary>
internal static class HealingActionBuilder
{
    public static readonly ServiceTarget DefaultTarget = new()
    {
        Service = "Norn.Shop.Catalog.API",
        Namespace = "norn-shop",
        Pod = "catalog-api-abc123",
        PodUid = "uid-1",
    };

    public static HealingAction ScaleUp(int replicaDelta = 1, ServiceTarget? target = null) => new()
    {
        ActionId = Guid.NewGuid(),
        Type = HealingActionType.ScaleUp,
        Target = target ?? DefaultTarget,
        Parameters = new Dictionary<string, string> { ["replicaDelta"] = replicaDelta.ToString(CultureInfo.InvariantCulture) },
        Order = 0,
    };

    public static HealingAction RestartPod(string podUid = "uid-1", ServiceTarget? target = null)
    {
        var effectiveTarget = target ?? DefaultTarget;
        return new HealingAction
        {
            ActionId = Guid.NewGuid(),
            Type = HealingActionType.RestartPod,
            Target = effectiveTarget,
            Parameters = new Dictionary<string, string>
            {
                ["podName"] = effectiveTarget.Pod ?? string.Empty,
                ["podUid"] = podUid,
            },
            Order = 0,
        };
    }

    public static HealingAction ToggleFeatureFlag(string flagName, bool value = true, ServiceTarget? target = null) => new()
    {
        ActionId = Guid.NewGuid(),
        Type = HealingActionType.ToggleFeatureFlag,
        Target = target ?? DefaultTarget,
        Parameters = new Dictionary<string, string> { ["flagName"] = flagName, ["value"] = value.ToString() },
        Order = 0,
    };

    public static HealingAction NoOp(ServiceTarget? target = null) => new()
    {
        ActionId = Guid.NewGuid(),
        Type = HealingActionType.NoOp,
        Target = target ?? DefaultTarget,
        Parameters = new Dictionary<string, string> { ["reason"] = "teste" },
        Order = 0,
    };

    public static HealingPlan Plan(HealingAction action, int verificationWindowSeconds = 0) => new()
    {
        PlanId = Guid.NewGuid(),
        ContextId = Guid.NewGuid(),
        CreatedAtUtc = DateTimeOffset.UtcNow,
        DecidedBy = DecidedBy.RuleEngine,
        Rationale = "teste",
        Confidence = 100,
        Actions = [action],
        ExpectedOutcome = "teste",
        VerificationWindowSeconds = verificationWindowSeconds,
        LlmTrace = new LlmTrace(),
    };

    public static RecentMetrics Metrics(
        double errorRatePct = 0,
        double latencyP99Ms = 100,
        long memoryWorkingSetBytes = 200_000_000) => new()
        {
            CpuUtilizationPct = 10,
            MemoryWorkingSetBytes = memoryWorkingSetBytes,
            RequestRatePerSecond = 5,
            ErrorRatePct = errorRatePct,
            LatencyP99Ms = latencyP99Ms,
            QueueDepth = 0,
        };

    public static TopologyInfo Topology(int currentReplicas = 1, string service = "Norn.Shop.Catalog.API") => new()
    {
        Service = service,
        CurrentReplicas = currentReplicas,
        DesiredReplicas = currentReplicas,
        ResourceRequests = new ResourceSpec { Cpu = "100m", Memory = "128Mi" },
        ResourceLimits = new ResourceSpec { Cpu = "500m", Memory = "256Mi" },
    };
}
