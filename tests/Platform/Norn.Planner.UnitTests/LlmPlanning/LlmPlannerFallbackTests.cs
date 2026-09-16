using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging.Abstractions;
using Norn.Contracts;
using Norn.Planner.Barriers;
using Norn.Planner.LlmPlanning;
using Norn.Planner.Settings;
using Norn.Planner.Telemetry;
using Norn.Planner.UnitTests.TestFixtures;
using Norn.Planner.Validation;
using NSubstitute;
using Shouldly;
using Xunit;
using RuleEngineImpl = Norn.Planner.RuleEngine.RuleEngine;

namespace Norn.Planner.UnitTests.LlmPlanning;

/// <summary>
/// Pipeline completo do §5.5 (passos 0-7) com <see cref="IChatClient"/> dublê — nenhum teste do
/// Planner chama o Ollama real (tarefa 10). A chamada de verdade só acontece na segunda etapa, ao
/// vivo, fora deste projeto de teste.
/// </summary>
public sealed class LlmPlannerFallbackTests
{
    private static LlmPlanner CreatePlanner(IChatClient chatClient, PlannerOptions? options = null)
    {
        var opts = options ?? new PlannerOptions();
        var checker = new HealingActionPreconditionChecker(opts);

        return new LlmPlanner(
            chatClient,
            new PromptBuilder(new PromptBudgetOptions()),
            new LlmOutputValidator(checker),
            new RuleEngineImpl(opts, checker, TimeProvider.System),
            opts,
            new PlannerMetrics(),
            TimeProvider.System,
            NullLogger<LlmPlanner>.Instance);
    }

    private static ChatResponse ResponseWithText(string text) => new(new ChatMessage(ChatRole.Assistant, text));

    [Fact]
    public async Task DecideAsync_ValidJsonOnFirstAttempt_ReturnsLlmDecidedPlanWithOneAttempt()
    {
        var chatClient = Substitute.For<IChatClient>();
        chatClient.GetResponseAsync(Arg.Any<IEnumerable<ChatMessage>>(), Arg.Any<ChatOptions?>(), Arg.Any<CancellationToken>())
            .Returns(ResponseWithText(
                """{"rationale":"ok","confidence":90,"actions":[{"type":"NoOp","parameters":{"reason":"contexto ambíguo"}}],"expectedOutcome":"nenhum"}"""));

        var plan = await CreatePlanner(chatClient).DecideAsync(AnomalyContextBuilder.Build(), CancellationToken.None);

        plan.DecidedBy.ShouldBe(DecidedBy.Llm);
        plan.LlmTrace.Attempts.ShouldBe(1);
        plan.LlmTrace.FailureReasons.ShouldBeEmpty();
        await chatClient.Received(1).GetResponseAsync(Arg.Any<IEnumerable<ChatMessage>>(), Arg.Any<ChatOptions?>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DecideAsync_InvalidJsonOnBothAttempts_FallsBackAfterExactlyOneRepair()
    {
        var chatClient = Substitute.For<IChatClient>();
        chatClient.GetResponseAsync(Arg.Any<IEnumerable<ChatMessage>>(), Arg.Any<ChatOptions?>(), Arg.Any<CancellationToken>())
            .Returns(ResponseWithText("isso não é json"));

        var plan = await CreatePlanner(chatClient).DecideAsync(AnomalyContextBuilder.Build(), CancellationToken.None);

        plan.LlmTrace.Attempts.ShouldBe(2);
        plan.LlmTrace.FailureReasons.Count.ShouldBe(2);
        plan.LlmTrace.FailureReasons.ShouldAllBe(reason => reason == LlmFailureReasons.InvalidJson);
        await chatClient.Received(2).GetResponseAsync(Arg.Any<IEnumerable<ChatMessage>>(), Arg.Any<ChatOptions?>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DecideAsync_RepairSucceedsOnSecondAttempt_ReturnsLlmDecidedPlan()
    {
        var chatClient = Substitute.For<IChatClient>();
        chatClient.GetResponseAsync(Arg.Any<IEnumerable<ChatMessage>>(), Arg.Any<ChatOptions?>(), Arg.Any<CancellationToken>())
            .Returns(
                ResponseWithText("isso não é json"),
                ResponseWithText("""{"rationale":"ok","confidence":70,"actions":[{"type":"NoOp","parameters":{"reason":"corrigido"}}],"expectedOutcome":"nenhum"}"""));

        var plan = await CreatePlanner(chatClient).DecideAsync(AnomalyContextBuilder.Build(), CancellationToken.None);

        plan.DecidedBy.ShouldBe(DecidedBy.Llm);
        plan.LlmTrace.Attempts.ShouldBe(2);
        plan.LlmTrace.FailureReasons.ShouldBe([LlmFailureReasons.InvalidJson]);
    }

    [Fact]
    public async Task DecideAsync_ConnectorThrowsOperationCanceled_FallsBackWithTimeoutReason()
    {
        var chatClient = Substitute.For<IChatClient>();
        chatClient.GetResponseAsync(Arg.Any<IEnumerable<ChatMessage>>(), Arg.Any<ChatOptions?>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException<ChatResponse>(new OperationCanceledException()));

        var options = new PlannerOptions { OllamaTimeout = TimeSpan.FromMilliseconds(50) };
        var plan = await CreatePlanner(chatClient, options).DecideAsync(AnomalyContextBuilder.Build(), CancellationToken.None);

        plan.LlmTrace.FailureReasons.ShouldContain(LlmFailureReasons.Timeout);
    }

    [Fact]
    public async Task DecideAsync_ConnectorThrowsConnectionError_FallsBackWithConnectorErrorReason()
    {
        var chatClient = Substitute.For<IChatClient>();
        chatClient.GetResponseAsync(Arg.Any<IEnumerable<ChatMessage>>(), Arg.Any<ChatOptions?>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException<ChatResponse>(new HttpRequestException("conexão recusada")));

        var plan = await CreatePlanner(chatClient).DecideAsync(AnomalyContextBuilder.Build(), CancellationToken.None);

        plan.LlmTrace.FailureReasons.ShouldContain(LlmFailureReasons.ConnectorError);
    }

    [Fact]
    public async Task DecideAsync_ContextExceedsPromptBudget_NeverCallsConnector()
    {
        var chatClient = Substitute.For<IChatClient>();
        var manySignals = Enumerable.Range(0, 500)
            .Select(i => AnomalyContextBuilder.Correlated($"metric_{i}_muito_longa_para_estourar_o_orcamento_de_caracteres_do_prompt"))
            .ToList();
        var context = AnomalyContextBuilder.Build(correlatedSignals: manySignals);

        var plan = await CreatePlanner(chatClient).DecideAsync(context, CancellationToken.None);

        plan.LlmTrace.FailureReasons.ShouldContain(LlmFailureReasons.PromptBudgetExceeded);
        await chatClient.DidNotReceive().GetResponseAsync(Arg.Any<IEnumerable<ChatMessage>>(), Arg.Any<ChatOptions?>(), Arg.Any<CancellationToken>());
    }
}
