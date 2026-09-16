namespace Norn.Planner.Settings;

/// <summary>
/// Orçamento de prompt (§5.5, tarefa 4) — medido antes de enviar, nunca truncado. Estimativa por
/// caracteres: contar tokens exatos exigiria o tokenizador do modelo; um fator conservador (baixo)
/// super-estima tokens a partir do tamanho em caracteres, então dispara <c>PromptBudgetExceeded</c>
/// mais cedo do que o necessário — é alarme, não medida, como o plano exige.
/// </summary>
public sealed class PromptBudgetOptions
{
    public const string SectionName = "Norn:Planner:PromptBudget";

    /// <summary>Metade do <c>num_ctx</c> de 8192 (§5.5) — a outra metade fica para a resposta.</summary>
    public int MaxPromptTokens { get; init; } = 4096;

    public int CharsPerTokenEstimate { get; init; } = 3;

    public int MaxPromptChars => MaxPromptTokens * CharsPerTokenEstimate;
}
