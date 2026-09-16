using Norn.Contracts;
using Shouldly;
using Xunit;
using RuleEngineImpl = Norn.Planner.RuleEngine.RuleEngine;

namespace Norn.Planner.UnitTests.RuleEngine;

/// <summary>
/// Tarefa 9b — fronteiras de severidade para cada assinatura mapeada. O enum <see cref="Severity"/>
/// só tem quatro valores discretos, então a única fronteira com significado de decisão é
/// Low↔Medium (abaixo dela o RuleEngine nunca age, docs/rule-table.md).
/// </summary>
public sealed class RuleEngineSeverityBoundaryTests
{
    private static readonly IReadOnlySet<string> F1 = new HashSet<string> { "dotnet_process_memory_working_set_bytes", "dotnet_gc_pause_time_seconds_total" };
    private static readonly IReadOnlySet<string> F2 = new HashSet<string> { "http_server_request_duration_seconds_bucket", "rabbitmq_queue_messages_ready" };
    private static readonly IReadOnlySet<string> F3 = new HashSet<string> { "norn_shop_payments_gateway_latency_ms", "http_server_request_duration_seconds_count" };

    public static TheoryData<IReadOnlySet<string>, HealingActionType> MappedSignatures => new()
    {
        { F1, HealingActionType.RestartPod },
        { F2, HealingActionType.ScaleUp },
        { F3, HealingActionType.ToggleFeatureFlag },
    };

    [Theory]
    [MemberData(nameof(MappedSignatures))]
    public void DecideActionType_SeverityLow_NeverActsRegardlessOfSignature(IReadOnlySet<string> signature, HealingActionType expected)
    {
        _ = expected;

        RuleEngineImpl.DecideActionType(signature, Severity.Low).ActionType.ShouldBe(HealingActionType.NoOp);
    }

    [Theory]
    [MemberData(nameof(MappedSignatures))]
    public void DecideActionType_SeverityMedium_JustAboveLowFloor_ActsAccordingToSignature(IReadOnlySet<string> signature, HealingActionType expected)
    {
        RuleEngineImpl.DecideActionType(signature, Severity.Medium).ActionType.ShouldBe(expected);
    }

    [Theory]
    [MemberData(nameof(MappedSignatures))]
    public void DecideActionType_SeverityHigh_ActsAccordingToSignature(IReadOnlySet<string> signature, HealingActionType expected)
    {
        RuleEngineImpl.DecideActionType(signature, Severity.High).ActionType.ShouldBe(expected);
    }

    [Theory]
    [MemberData(nameof(MappedSignatures))]
    public void DecideActionType_SeverityCritical_ActsAccordingToSignature(IReadOnlySet<string> signature, HealingActionType expected)
    {
        RuleEngineImpl.DecideActionType(signature, Severity.Critical).ActionType.ShouldBe(expected);
    }
}
