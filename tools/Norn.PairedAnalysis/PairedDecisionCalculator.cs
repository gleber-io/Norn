using CsvHelper.Configuration.Attributes;
using Norn.Contracts;
using RuleEngineImpl = Norn.Planner.RuleEngine.RuleEngine;

namespace Norn.PairedAnalysis;

/// <summary>
/// Uma linha do CSV pareado (Fase 12, tarefa 4a) — um contexto do braço B, dois decisores, um
/// gabarito. Cabeçalho em snake_case (<see cref="NameAttribute"/>): quem lê é <c>tools/analysis/paired_mcnemar.py</c>.
/// </summary>
public sealed record PairedDecisionRow
{
    [Name("context_id")]
    public required Guid ContextId { get; init; }

    [Name("experiment_run_id")]
    public required Guid ExperimentRunId { get; init; }

    [Name("scenario")]
    public required string Scenario { get; init; }

    [Name("llm_action")]
    public required HealingActionType LlmAction { get; init; }

    [Name("rule_action")]
    public required HealingActionType RuleAction { get; init; }

    [Name("reference_action")]
    public required HealingActionType ReferenceAction { get; init; }

    [Name("llm_matches_reference")]
    public required int LlmMatchesReference { get; init; }

    [Name("rule_matches_reference")]
    public required int RuleMatchesReference { get; init; }
}

/// <summary>§3, tabela de cenários — ação de referência esperada, gabarito da taxa de ação esperada (H2).</summary>
public static class ScenarioReferenceActions
{
    public static HealingActionType For(string scenario) => scenario switch
    {
        "F1" => HealingActionType.RestartPod,
        "F2" => HealingActionType.ScaleUp,
        "F3" => HealingActionType.ToggleFeatureFlag,
        "F5" => HealingActionType.NoOp,
        _ => throw new ArgumentException($"Cenário sem ação de referência conhecida: {scenario}"),
    };
}

/// <summary>
/// Recálculo offline do <c>RuleEngine</c> sobre um contexto real do braço B (§3, "H2 é testada de
/// forma pareada"). Usa só <see cref="RuleEngineImpl.DecideActionType"/> — a função pura de verdade
/// (Fase 8, tarefa 1) — nunca <c>RuleEngineImpl.Decide</c>, que exige pré-condições e barreiras que
/// dependem de estado ao vivo (cooldown, topologia) e mediriam ação eficaz, não ação esperada.
/// </summary>
public static class PairedDecisionCalculator
{
    public static PairedDecisionRow Calculate(Guid experimentRunId, string scenario, AnomalyContext context, HealingActionType llmAction)
    {
        var alteredMetrics = BuildAlteredMetrics(context);
        var severityBand = context.PrimarySignal.Severity;
        var ruleDecision = RuleEngineImpl.DecideActionType(alteredMetrics, severityBand);
        var referenceAction = ScenarioReferenceActions.For(scenario);

        return new PairedDecisionRow
        {
            ContextId = context.ContextId,
            ExperimentRunId = experimentRunId,
            Scenario = scenario,
            LlmAction = llmAction,
            RuleAction = ruleDecision.ActionType,
            ReferenceAction = referenceAction,
            LlmMatchesReference = llmAction == referenceAction ? 1 : 0,
            RuleMatchesReference = ruleDecision.ActionType == referenceAction ? 1 : 0,
        };
    }

    /// <summary>Conjunto de métricas alteradas — primária mais correlacionadas (ADR-14), a mesma entrada que o <c>RuleEngine</c> online recebe.</summary>
    private static HashSet<string> BuildAlteredMetrics(AnomalyContext context)
    {
        var metrics = new HashSet<string>(StringComparer.Ordinal) { context.PrimarySignal.MetricName };
        foreach (var signal in context.CorrelatedSignals)
        {
            metrics.Add(signal.MetricName);
        }

        return metrics;
    }
}
