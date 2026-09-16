using Norn.Contracts;
using Norn.Contracts.Serialization;
using Norn.Planner.Settings;

namespace Norn.Planner.LlmPlanning;

/// <summary>
/// Monta o system prompt (§5.5) a partir de <see cref="HealingActionCatalog"/> — nunca hardcoded
/// no texto, o enum é a fonte da verdade — e serializa o contexto via
/// <see cref="CanonicalJson"/>, a mesma função que gera <c>context_hash</c> (Norn.Knowledge).
/// Mede o orçamento por caracteres antes de qualquer chamada ao conector (tarefa 4).
/// </summary>
public sealed class PromptBuilder(PromptBudgetOptions budgetOptions)
{
    public sealed record BuiltPrompt(string SystemPrompt, string ContextJson, string PromptHash, bool WithinBudget, int TotalChars);

    public BuiltPrompt Build(AnomalyContext context)
    {
        var contextJson = CanonicalJson.Serialize(context);
        var systemPrompt = BuildSystemPrompt();
        var totalChars = systemPrompt.Length + contextJson.Length;

        return new BuiltPrompt(
            systemPrompt,
            contextJson,
            CanonicalJson.ComputeHashOf(contextJson),
            totalChars <= budgetOptions.MaxPromptChars,
            totalChars);
    }

    private static string BuildSystemPrompt()
    {
        var catalog = string.Join('\n', HealingActionCatalog.Specs.Values.Select(spec =>
            $"- {spec.Type}({string.Join(", ", spec.ParameterNames)}): {spec.Description} Pré-condição: {spec.PreconditionDescription}."));

        return
            "Você é um SRE responsável por escolher a ação de cura para uma anomalia detectada em " +
            "um sistema de e-commerce rodando em Kubernetes.\n\n" +
            "Catálogo fechado de ações permitidas — nenhuma ação fora desta lista é válida:\n" +
            catalog + "\n\n" +
            "Restrições:\n" +
            "- Nunca proponha ação fora do catálogo acima.\n" +
            "- Respeite a pré-condição descrita de cada ação.\n" +
            "- Nunca proponha mais de uma ação por resposta.\n" +
            "- Se o contexto for insuficiente ou nenhuma ação for seguramente aplicável, responda com a ação NoOp e explique o motivo no parâmetro \"reason\".\n\n" +
            "Responda SOMENTE com um objeto JSON, sem cercas markdown, sem texto antes ou depois, no formato exato:\n" +
            "{\"rationale\": \"...\", \"confidence\": 0-100, \"actions\": [{\"type\": \"...\", \"parameters\": {\"nome\": \"valor\"}}], \"expectedOutcome\": \"...\"}";
    }
}
