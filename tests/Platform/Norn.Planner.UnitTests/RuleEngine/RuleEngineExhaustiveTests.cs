using Norn.Contracts;
using Shouldly;
using Xunit;
using RuleEngineImpl = Norn.Planner.RuleEngine.RuleEngine;

namespace Norn.Planner.UnitTests.RuleEngine;

/// <summary>
/// Tarefa 9a — totalidade do <c>RuleEngine.DecideActionType</c> sobre o domínio fechado. Golden em
/// <c>docs/rule-table.md</c>. 2^7 = 128 subconjuntos, gerados por bitmask, não escritos à mão.
/// </summary>
public sealed class RuleEngineExhaustiveTests
{
    private static readonly IReadOnlyList<string> SignatureMetrics = RuleEngineImpl.SignatureMetrics;

    public static TheoryData<int> AllSubsetMasks()
    {
        var data = new TheoryData<int>();
        for (var mask = 0; mask < (1 << SignatureMetrics.Count); mask++)
        {
            data.Add(mask);
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(AllSubsetMasks))]
    public void DecideActionType_AllSubsetsOfSignatureAndAllSeverities_NeverThrowsAndAlwaysDefined(int mask)
    {
        var alteredMetrics = ToSubset(mask);

        foreach (var severity in Enum.GetValues<Severity>())
        {
            var decision = RuleEngineImpl.DecideActionType(alteredMetrics, severity);

            Enum.IsDefined(decision.ActionType).ShouldBeTrue();
        }
    }

    [Fact]
    public void DecideActionType_F1Signature_MapsToRestartPod()
    {
        var f1 = new HashSet<string> { "dotnet_process_memory_working_set_bytes", "dotnet_gc_pause_time_seconds_total" };

        RuleEngineImpl.DecideActionType(f1, Severity.High).ActionType.ShouldBe(HealingActionType.RestartPod);
    }

    [Fact]
    public void DecideActionType_F2Signature_MapsToScaleUp()
    {
        var f2 = new HashSet<string> { "http_server_request_duration_seconds_bucket", "rabbitmq_queue_messages_ready" };

        RuleEngineImpl.DecideActionType(f2, Severity.High).ActionType.ShouldBe(HealingActionType.ScaleUp);
    }

    [Fact]
    public void DecideActionType_F3Signature_MapsToToggleFeatureFlag()
    {
        var f3 = new HashSet<string> { "norn_shop_payments_gateway_latency_ms", "http_server_request_duration_seconds_count" };

        RuleEngineImpl.DecideActionType(f3, Severity.High).ActionType.ShouldBe(HealingActionType.ToggleFeatureFlag);
    }

    [Fact]
    public void DecideActionType_F5EmptySignature_MapsToNoOp()
    {
        RuleEngineImpl.DecideActionType(new HashSet<string>(), Severity.Critical).ActionType.ShouldBe(HealingActionType.NoOp);
    }

    [Fact]
    public void DecideActionType_ErrorsByTypeAlone_NeverTriggersActionOnItsOwn()
    {
        var onlyErrorsByType = new HashSet<string> { "norn_app_errors_total" };

        RuleEngineImpl.DecideActionType(onlyErrorsByType, Severity.Critical).ActionType.ShouldBe(HealingActionType.NoOp);
    }

    [Fact]
    public void DecideActionType_OverAllSubsetsAndSeverities_EveryCatalogActionIsReachable()
    {
        var reachedActions = new HashSet<HealingActionType>();
        for (var mask = 0; mask < (1 << SignatureMetrics.Count); mask++)
        {
            var subset = ToSubset(mask);
            foreach (var severity in Enum.GetValues<Severity>())
            {
                reachedActions.Add(RuleEngineImpl.DecideActionType(subset, severity).ActionType);
            }
        }

        foreach (var action in Enum.GetValues<HealingActionType>())
        {
            reachedActions.ShouldContain(action);
        }
    }

    private static HashSet<string> ToSubset(int mask)
    {
        var subset = new HashSet<string>();
        for (var i = 0; i < SignatureMetrics.Count; i++)
        {
            if ((mask & (1 << i)) != 0)
            {
                subset.Add(SignatureMetrics[i]);
            }
        }

        return subset;
    }
}
