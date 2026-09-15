namespace Norn.Analyzer.Settings;

/// <summary>
/// Parametrização dos detectores ML.NET (tarefa 4) — nunca hardcoded. Confiança em [0, 100]
/// (não [0, 1] — §11, item 9: <c>0.95</c> em vez de <c>95.0</c> produz falso positivo altíssimo
/// que pareceria problema de calibração). Teto rígido de 60 amostras: <c>changeHistoryLength</c>
/// (e seu equivalente de spike) × 5 s de scrape não pode exceder o warmup de 5 min da campanha (§3).
/// </summary>
public sealed class AnalyzerOptions
{
    public const string SectionName = "Norn:Analyzer";

    public double Confidence { get; init; } = 95.0;

    public int PValueHistoryLength { get; init; } = 30;

    public int ChangeHistoryLength { get; init; } = 30;

    /// <summary>Janela de correlação da tarefa 5 — sinais do mesmo alvo dentro dela viram um único <c>AnomalyContext</c>.</summary>
    public TimeSpan CorrelationWindow { get; init; } = TimeSpan.FromSeconds(60);
}
