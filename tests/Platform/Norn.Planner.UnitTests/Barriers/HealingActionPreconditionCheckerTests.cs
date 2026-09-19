using System.Globalization;
using Norn.Contracts;
using Norn.Planner.Barriers;
using Norn.Planner.Settings;
using Norn.Planner.UnitTests.TestFixtures;
using Shouldly;
using Xunit;

namespace Norn.Planner.UnitTests.Barriers;

/// <summary>
/// Tarefa 9c — os cinco casos negativos do §5.4 citados no plano mestre, mais os positivos que
/// provam que a barreira não é permissiva demais nem restritiva demais. Testa o checker, não o
/// RuleEngine — arquivo próprio, como o plano pede.
/// </summary>
public sealed class HealingActionPreconditionCheckerTests
{
    private static HealingActionPreconditionChecker CreateChecker() => new(new PlannerOptions());

    [Fact]
    public void Check_ScaleUp_ReplicasAlreadyAtCeiling_Rejected()
    {
        var context = AnomalyContextBuilder.Build(currentReplicas: 3);
        var action = ScaleUpAction(replicaDelta: 1, context);

        CreateChecker().Check(context, action).Accepted.ShouldBeFalse();
    }

    [Fact]
    public void Check_ScaleUp_CurrentBelowCeilingButSumExceedsIt_Rejected()
    {
        // atuais(1) < maxReplicas(3) seria aprovado pela formulação errada (§5.4) — a soma é 4.
        var context = AnomalyContextBuilder.Build(currentReplicas: 1);
        var action = ScaleUpAction(replicaDelta: 3, context);

        CreateChecker().Check(context, action).Accepted.ShouldBeFalse();
    }

    [Fact]
    public void Check_ScaleUp_SumWithinCeiling_Accepted()
    {
        var context = AnomalyContextBuilder.Build(currentReplicas: 1);
        var action = ScaleUpAction(replicaDelta: 1, context);

        CreateChecker().Check(context, action).Accepted.ShouldBeTrue();
    }

    [Fact]
    public void Check_RestartPod_SecondRestartOfSameTargetWithinTenMinutes_Rejected()
    {
        var history = new[]
        {
            new HealingHistoryEntry
            {
                Action = "RestartPod",
                AppliedAtUtc = AnomalyContextBuilder.DefaultCreatedAtUtc.AddMinutes(-5),
                Outcome = "Succeeded",
            },
        };
        var context = AnomalyContextBuilder.Build(healingHistory: history);
        var action = RestartPodAction(context.PrimarySignal.Target.PodUid!, context);

        CreateChecker().Check(context, action).Accepted.ShouldBeFalse();
    }

    [Fact]
    public void Check_RestartPod_PriorRestartOutsideTenMinuteWindow_Accepted()
    {
        var history = new[]
        {
            new HealingHistoryEntry
            {
                Action = "RestartPod",
                AppliedAtUtc = AnomalyContextBuilder.DefaultCreatedAtUtc.AddMinutes(-11),
                Outcome = "Succeeded",
            },
        };
        var context = AnomalyContextBuilder.Build(healingHistory: history);
        var action = RestartPodAction(context.PrimarySignal.Target.PodUid!, context);

        CreateChecker().Check(context, action).Accepted.ShouldBeTrue();
    }

    [Fact]
    public void Check_RestartPod_PodUidDivergesFromObserved_Rejected()
    {
        var context = AnomalyContextBuilder.Build(podUid: "uid-observado");
        var action = RestartPodAction("uid-diferente", context);

        CreateChecker().Check(context, action).Accepted.ShouldBeFalse();
    }

    [Fact]
    public void Check_RestartPod_NoPodObservedInContext_Rejected()
    {
        var context = AnomalyContextBuilder.Build(pod: null, podUid: null);
        var action = RestartPodAction("qualquer-uid", context);

        CreateChecker().Check(context, action).Accepted.ShouldBeFalse();
    }

    [Fact]
    public void Check_ToggleFeatureFlag_FlagOutsideShopCatalog_Rejected()
    {
        var context = AnomalyContextBuilder.Build();
        var action = ToggleFlagAction("flag.fora.do.catalogo", context);

        CreateChecker().Check(context, action).Accepted.ShouldBeFalse();
    }

    [Fact]
    public void Check_ToggleFeatureFlag_FlagInShopCatalogAndServiceMatchesOwner_Accepted()
    {
        var context = AnomalyContextBuilder.Build(service: "Norn.Shop.Payment.API");
        var action = ToggleFlagAction(ShopFlagCatalog.PaymentGatewayBypass, context);

        CreateChecker().Check(context, action).Accepted.ShouldBeTrue();
    }

    [Fact]
    public void Check_ToggleFeatureFlag_SignalFromServiceThatDoesNotOwnTheFlag_Rejected()
    {
        // Achado ao vivo, Fase 12 (piloto F5): um sinal de 5xx do Catalog não pode ligar a flag do
        // Payment — a flag existe no catálogo, mas pertence a outro serviço.
        var context = AnomalyContextBuilder.Build(service: "Norn.Shop.Catalog.API");
        var action = ToggleFlagAction(ShopFlagCatalog.PaymentGatewayBypass, context);

        CreateChecker().Check(context, action).Accepted.ShouldBeFalse();
    }

    [Fact]
    public void Check_ActingAction_TargetInCooldown_Rejected()
    {
        var context = AnomalyContextBuilder.Build(inCooldown: true);
        var action = ScaleUpAction(1, context);

        CreateChecker().Check(context, action).Accepted.ShouldBeFalse();
    }

    [Fact]
    public void Check_NoOp_TargetInCooldown_StillAccepted()
    {
        // ADR-04 existe para conter ação, não abstenção — NoOp nunca é barrado por cooldown.
        var context = AnomalyContextBuilder.Build(inCooldown: true);
        var action = new HealingAction
        {
            ActionId = Guid.NewGuid(),
            Type = HealingActionType.NoOp,
            Target = context.PrimarySignal.Target,
            Parameters = new Dictionary<string, string> { ["reason"] = "teste" },
            Order = 0,
        };

        CreateChecker().Check(context, action).Accepted.ShouldBeTrue();
    }

    [Fact]
    public void Check_ActingAction_MaxActionsPerWindowReached_Rejected()
    {
        var windowStart = AnomalyContextBuilder.DefaultCreatedAtUtc.AddMinutes(-1);
        var history = new[]
        {
            new HealingHistoryEntry { Action = "ScaleUp", AppliedAtUtc = windowStart, Outcome = "Succeeded" },
            new HealingHistoryEntry { Action = "ScaleUp", AppliedAtUtc = windowStart, Outcome = "Succeeded" },
            new HealingHistoryEntry { Action = "ScaleUp", AppliedAtUtc = windowStart, Outcome = "Succeeded" },
        };
        var context = AnomalyContextBuilder.Build(healingHistory: history, currentReplicas: 1);
        var action = ScaleUpAction(1, context);

        CreateChecker().Check(context, action).Accepted.ShouldBeFalse();
    }

    private static HealingAction ScaleUpAction(int replicaDelta, AnomalyContext context) => new()
    {
        ActionId = Guid.NewGuid(),
        Type = HealingActionType.ScaleUp,
        Target = context.PrimarySignal.Target,
        Parameters = new Dictionary<string, string> { ["replicaDelta"] = replicaDelta.ToString(CultureInfo.InvariantCulture) },
        Order = 0,
    };

    private static HealingAction RestartPodAction(string podUid, AnomalyContext context) => new()
    {
        ActionId = Guid.NewGuid(),
        Type = HealingActionType.RestartPod,
        Target = context.PrimarySignal.Target,
        Parameters = new Dictionary<string, string>
        {
            ["podName"] = context.PrimarySignal.Target.Pod ?? string.Empty,
            ["podUid"] = podUid,
        },
        Order = 0,
    };

    private static HealingAction ToggleFlagAction(string flagName, AnomalyContext context) => new()
    {
        ActionId = Guid.NewGuid(),
        Type = HealingActionType.ToggleFeatureFlag,
        Target = context.PrimarySignal.Target,
        Parameters = new Dictionary<string, string> { ["flagName"] = flagName, ["value"] = "true" },
        Order = 0,
    };
}
