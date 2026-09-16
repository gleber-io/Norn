using System.Text.Json;

namespace Norn.Planner.LlmPlanning;

/// <summary>Forma crua da saída do LLM (§5.5) — "objeto HealingPlan parcial" antes da validação.</summary>
public sealed record LlmPlanResponseDto
{
    public string? Rationale { get; init; }

    public double Confidence { get; init; }

    public IReadOnlyList<LlmActionDto> Actions { get; init; } = [];

    public string? ExpectedOutcome { get; init; }
}

/// <summary>
/// <c>Parameters</c> vem como <see cref="JsonElement"/>, não <c>string</c>: apesar de o system
/// prompt pedir valores como texto, o modelo emite <c>replicaDelta</c> como número JSON puro com
/// alguma frequência (confirmado na verificação manual contra o <c>norn-qwen</c> real) —
/// desserializar direto para <c>Dictionary&lt;string, string&gt;</c> falharia com
/// <c>InvalidJson</c> num JSON que é, na prática, sintaticamente válido. A normalização para
/// <c>string</c> (formato exigido por <see cref="Norn.Contracts.HealingAction.Parameters"/>)
/// acontece em <see cref="Validation.LlmOutputValidator"/>, tolerando o tipo que o modelo mandar.
/// </summary>
public sealed record LlmActionDto
{
    public string? Type { get; init; }

    public Dictionary<string, JsonElement> Parameters { get; init; } = [];
}
