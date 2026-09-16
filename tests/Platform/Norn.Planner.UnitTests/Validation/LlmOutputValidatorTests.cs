using Norn.Contracts;
using Norn.Planner.Barriers;
using Norn.Planner.Settings;
using Norn.Planner.UnitTests.TestFixtures;
using Norn.Planner.Validation;
using Shouldly;
using Xunit;

namespace Norn.Planner.UnitTests.Validation;

/// <summary>Tarefa 10 — pipeline de validação da saída do LLM, sem nenhuma chamada de rede.</summary>
public sealed class LlmOutputValidatorTests
{
    private static LlmOutputValidator CreateValidator() => new(new HealingActionPreconditionChecker(new PlannerOptions()));

    [Fact]
    public void Validate_MalformedJson_FailsWithInvalidJson()
    {
        var result = CreateValidator().Validate("{ isso não é json", AnomalyContextBuilder.Build());

        result.Success.ShouldBeFalse();
        result.FailureReason.ShouldBe(LlmFailureReasons.InvalidJson);
    }

    [Fact]
    public void Validate_ActionTypeOutsideCatalog_FailsWithActionNotInCatalog()
    {
        const string json = """{"rationale":"x","confidence":50,"actions":[{"type":"DeleteNamespace","parameters":{}}],"expectedOutcome":"x"}""";

        var result = CreateValidator().Validate(json, AnomalyContextBuilder.Build());

        result.Success.ShouldBeFalse();
        result.FailureReason.ShouldBe(LlmFailureReasons.ActionNotInCatalog);
    }

    [Fact]
    public void Validate_ScaleUpParameterOutOfRange_FailsWithPreconditionViolation()
    {
        const string json = """{"rationale":"x","confidence":50,"actions":[{"type":"ScaleUp","parameters":{"replicaDelta":"99"}}],"expectedOutcome":"x"}""";

        var result = CreateValidator().Validate(json, AnomalyContextBuilder.Build(currentReplicas: 1));

        result.Success.ShouldBeFalse();
        result.FailureReason.ShouldBe(LlmFailureReasons.PreconditionViolation);
    }

    [Fact]
    public void Validate_ResponseWrappedInMarkdownFence_StillParses()
    {
        const string json = "```json\n{\"rationale\":\"x\",\"confidence\":10,\"actions\":[{\"type\":\"NoOp\",\"parameters\":{\"reason\":\"contexto insuficiente\"}}],\"expectedOutcome\":\"x\"}\n```";

        var result = CreateValidator().Validate(json, AnomalyContextBuilder.Build());

        result.Success.ShouldBeTrue();
        result.Plan!.Actions.ShouldHaveSingleItem();
        result.Plan.Actions[0].Type.ShouldBe(HealingActionType.NoOp);
    }

    [Fact]
    public void Validate_ReplicaDeltaAsRawJsonNumber_NormalizesToStringAndSucceeds()
    {
        // O modelo às vezes emite replicaDelta como número JSON puro, não como string entre aspas
        // (confirmado na verificação manual contra o norn-qwen real) — isso não é InvalidJson.
        const string json = """{"rationale":"x","confidence":80,"actions":[{"type":"ScaleUp","parameters":{"replicaDelta":1}}],"expectedOutcome":"x"}""";

        var result = CreateValidator().Validate(json, AnomalyContextBuilder.Build(currentReplicas: 1));

        result.Success.ShouldBeTrue();
        result.Plan!.Actions[0].Parameters["replicaDelta"].ShouldBe("1");
    }

    [Fact]
    public void Validate_ValidScaleUpWithinCeiling_Succeeds()
    {
        const string json = """{"rationale":"latência alta","confidence":80,"actions":[{"type":"ScaleUp","parameters":{"replicaDelta":"1"}}],"expectedOutcome":"réplicas aumentadas"}""";

        var result = CreateValidator().Validate(json, AnomalyContextBuilder.Build(currentReplicas: 1));

        result.Success.ShouldBeTrue();
        result.Plan!.Confidence.ShouldBe(80);
    }
}
