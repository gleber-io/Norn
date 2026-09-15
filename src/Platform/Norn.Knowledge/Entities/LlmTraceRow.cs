namespace Norn.Knowledge.Entities;

/// <summary>
/// <c>llm_traces</c> (tarefa 1 da Fase 7). Extraído de <c>HealingPlan.LlmTrace</c> ao gravar o
/// plano — não há método de gravação separado no <c>IKnowledgeStore</c> (§5.3). Todo motivo de
/// falha aqui é dado experimental: a taxa de fallback decomposta por motivo é resultado de
/// primeira linha (§3).
/// </summary>
public sealed class LlmTraceRow
{
    public required Guid TraceId { get; init; }

    public required Guid PlanId { get; init; }

    public string? PromptHash { get; init; }

    public string? Model { get; init; }

    public required long LatencyMs { get; init; }

    public required int Attempts { get; init; }

    public required string Payload { get; init; }
}
