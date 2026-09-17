using Norn.Contracts;

namespace Norn.Planner.Settings;

/// <summary>
/// Parâmetros do Planner (§5.4, §5.5, ADR-04, ADR-12). Valores default aqui são os do plano
/// mestre; nenhum é constante no código de decisão — tudo chega via <c>IOptions</c> (mesmo padrão
/// de <c>Norn.Analyzer.Settings.SeverityBandOptions</c>).
/// </summary>
public sealed class PlannerOptions
{
    public const string SectionName = "Norn:Planner";

    /// <summary>§5.4 — vem do dimensionamento do nó único da Fase 6, não é constante de código.</summary>
    public int MaxReplicas { get; init; } = 3;

    /// <summary>Incremento que o <c>RuleEngine</c> propõe para <c>ScaleUp</c> — o LLM decide o próprio.</summary>
    public int DefaultReplicaDelta { get; init; } = 1;

    /// <summary>§5.4 — barreira específica do <c>RestartPod</c>, mais estrita que o cooldown geral do ADR-04.</summary>
    public TimeSpan RestartPodCooldown { get; init; } = TimeSpan.FromMinutes(10);

    /// <summary>ADR-04, barreira (b) — máximo de ações por alvo na janela abaixo.</summary>
    public int MaxActionsPerWindow { get; init; } = 3;

    public TimeSpan ActionWindow { get; init; } = TimeSpan.FromMinutes(15);

    /// <summary>§5.5, passo 0 — teto por tentativa de chamada ao Ollama.</summary>
    public TimeSpan OllamaTimeout { get; init; } = TimeSpan.FromSeconds(10);

    /// <summary>Modelo derivado do Modelfile (ADR-12, §5.5) — nunca o modelo base direto.</summary>
    public string ModelName { get; init; } = "norn-qwen";

    public string OllamaBaseUrl { get; init; } = "http://localhost:11434";

    /// <summary>
    /// Redundante com o Modelfile (§5.5) — uma chamada que esqueça de passar continua correta
    /// pelo lado do servidor, mas fixar nos dois lugares é o que o plano pede.
    /// </summary>
    public int Seed { get; init; } = 42;

    public int NumCtx { get; init; } = 8192;

    public int VerificationWindowSeconds { get; init; } = 120;

    /// <summary>
    /// Janela de verificação específica do <c>RestartPod</c> (Fase 9, residual de calibração) —
    /// 120s genérico não bastava: overhead de startup/JIT/GC de um pod .NET recém-criado empurra
    /// o RSS pra cima por um tempo mesmo sem vazamento nenhum rolando (achado do replay ao vivo,
    /// <c>PartiallyApplied</c> com o `RestartPod` já correto). É a única ação cujo próprio efeito
    /// colateral (processo novo) atrapalha a própria verificação — as demais não pagam esse custo.
    /// </summary>
    public int RestartPodVerificationWindowSeconds { get; init; } = 240;

    /// <summary>Fonte única da janela por tipo de ação — os dois braços (RuleEngine, LlmPlanner) chamam este método, nunca leem os campos acima diretamente.</summary>
    public int VerificationWindowSecondsFor(HealingActionType actionType) =>
        actionType == HealingActionType.RestartPod ? RestartPodVerificationWindowSeconds : VerificationWindowSeconds;
}
