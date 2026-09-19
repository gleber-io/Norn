using Norn.Contracts;
using Norn.Planner.Barriers;
using Norn.Planner.Settings;
using Norn.Planner.UnitTests.TestFixtures;
using Shouldly;
using Xunit;
using RuleEngineImpl = Norn.Planner.RuleEngine.RuleEngine;

namespace Norn.Planner.UnitTests.RuleEngine;

/// <summary>
/// <see cref="RuleEngineImpl.Decide"/> ponta a ponta — decisão pura + barreira + montagem do
/// <see cref="HealingPlan"/>. Suporta o DoD da Fase 8: "os dois planners produzem HealingPlan
/// válido para F1/F2/F3 e NoOp para F5" e "cooldown ativo impede emissão de plano".
/// </summary>
public sealed class RuleEngineDecideTests
{
    private static RuleEngineImpl CreateEngine(PlannerOptions? options = null) => new(
        options ?? new PlannerOptions(),
        new HealingActionPreconditionChecker(options ?? new PlannerOptions()),
        TimeProvider.System);

    [Fact]
    public void Decide_F1Signature_ProducesRestartPodDecidedByRuleEngine()
    {
        var context = AnomalyContextBuilder.Build(
            primaryMetricName: "dotnet_process_memory_working_set_bytes",
            severity: Severity.High,
            correlatedSignals: [AnomalyContextBuilder.Correlated("dotnet_gc_pause_time_seconds_total")]);

        var plan = CreateEngine().Decide(context);

        plan.DecidedBy.ShouldBe(DecidedBy.RuleEngine);
        plan.Actions.ShouldHaveSingleItem();
        plan.Actions[0].Type.ShouldBe(HealingActionType.RestartPod);
        plan.VerificationWindowSeconds.ShouldBe(new PlannerOptions().RestartPodVerificationWindowSeconds);
    }

    [Fact]
    public void Decide_F5EmptySignature_ProducesNoOp()
    {
        var context = AnomalyContextBuilder.Build(primaryMetricName: "norn_app_errors_total", severity: Severity.Low);

        var plan = CreateEngine().Decide(context);

        plan.Actions.ShouldHaveSingleItem();
        plan.Actions[0].Type.ShouldBe(HealingActionType.NoOp);
        plan.VerificationWindowSeconds.ShouldBe(new PlannerOptions().VerificationWindowSeconds);
    }

    [Fact]
    public void Decide_F3SignatureFromPaymentService_ProducesToggleFeatureFlagDecidedByRuleEngine()
    {
        var context = AnomalyContextBuilder.Build(
            primaryMetricName: "norn_shop_payments_gateway_latency_ms",
            severity: Severity.High,
            service: "Norn.Shop.Payment.API");

        var plan = CreateEngine().Decide(context);

        plan.DecidedBy.ShouldBe(DecidedBy.RuleEngine);
        plan.Actions.ShouldHaveSingleItem();
        plan.Actions[0].Type.ShouldBe(HealingActionType.ToggleFeatureFlag);
    }

    [Fact]
    public void Decide_F3SignatureFromServiceThatDoesNotOwnTheFlag_DegradesToNoOpWithFallback()
    {
        // Achado ao vivo, Fase 12 (piloto F5): um sinal de 5xx do Catalog não deve ligar a flag do
        // Payment — a pré-condição de escopo rejeita, e o plano cai para NoOp/Fallback, não para
        // ToggleFeatureFlag aceito.
        var context = AnomalyContextBuilder.Build(
            primaryMetricName: "norn_shop_payments_gateway_latency_ms",
            severity: Severity.High,
            service: "Norn.Shop.Catalog.API");

        var plan = CreateEngine().Decide(context);

        plan.DecidedBy.ShouldBe(DecidedBy.Fallback);
        plan.Actions.ShouldHaveSingleItem();
        plan.Actions[0].Type.ShouldBe(HealingActionType.NoOp);
    }

    [Fact]
    public void Decide_TargetInCooldown_DegradesToNoOpWithFallback()
    {
        var context = AnomalyContextBuilder.Build(
            primaryMetricName: "dotnet_process_memory_working_set_bytes",
            severity: Severity.High,
            inCooldown: true);

        var plan = CreateEngine().Decide(context);

        plan.DecidedBy.ShouldBe(DecidedBy.Fallback);
        plan.Actions[0].Type.ShouldBe(HealingActionType.NoOp);
    }

    [Fact]
    public void Decide_RationaleNeverExceeds500Characters()
    {
        var context = AnomalyContextBuilder.Build(inCooldown: true);

        var plan = CreateEngine().Decide(context);

        plan.Rationale.Length.ShouldBeLessThanOrEqualTo(500);
    }
}
