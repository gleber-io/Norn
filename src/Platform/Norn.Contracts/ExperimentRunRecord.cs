namespace Norn.Contracts;

/// <summary>
/// Colunas de controle de uma execução da campanha (Fase 12, tarefa 1a — lista fechada no
/// Master Plan §7 "experiment_runs"), conhecidas no instante do reset, antes do onset existir.
/// Escrita por <c>Norn.Labeler init-run</c>, chamado pelo <c>run-experiment.ps1</c>.
/// </summary>
public sealed record ExperimentRunRecord
{
    public required Guid ExperimentRunId { get; init; }

    /// <summary><c>F1</c> | <c>F2</c> | <c>F3</c> | <c>F5</c>.</summary>
    public required string Scenario { get; init; }

    /// <summary><c>A</c> | <c>B</c> | <c>C</c>.</summary>
    public required string Arm { get; init; }

    public required int Repetition { get; init; }

    public required int RunOrder { get; init; }

    public required int RandomizationSeed { get; init; }

    public required double TargetRps { get; init; }

    public required int ChaosSeed { get; init; }

    public required int LoadSeed { get; init; }

    public required double InjectionPhase { get; init; }

    /// <summary>Modo lido após o reset, não o pedido (Fase 12, tarefa 2a).</summary>
    public required string Mode { get; init; }

    public required bool ForecastEnabled { get; init; }

    public int? ForecastHorizonMinutes { get; init; }

    public string? LlmModelDigest { get; init; }

    public int? LlmTimeoutSeconds { get; init; }

    public int? LlmNumCtx { get; init; }

    public required DateTimeOffset StartedAtUtc { get; init; }

    public required int WslMemoryGb { get; init; }

    public required int WslProcessors { get; init; }

    public required string GitCommitSha { get; init; }
}

/// <summary>
/// Colunas preenchidas depois da janela de observação fechar (Fase 12, tarefa 1 — Norn.Labeler),
/// quando onset, recuperação e estado de término já são conhecidos. Separado de
/// <see cref="ExperimentRunRecord"/> porque as duas escritas acontecem em instantes diferentes do
/// mesmo <c>experiment_run_id</c>: a primeira no reset, a segunda depois do teardown.
/// </summary>
public sealed record ExperimentRunLabelingResult
{
    public required Guid ExperimentRunId { get; init; }

    public double? AchievedRps { get; init; }

    /// <summary>Nulo se não houve onset; no F5, o instante do kill.</summary>
    public DateTimeOffset? OnsetAtUtc { get; init; }

    /// <summary><c>onset + 10 min</c>.</summary>
    public DateTimeOffset? WindowEndAtUtc { get; init; }

    /// <summary>Nulo quando censurado.</summary>
    public DateTimeOffset? RecoveredAtUtc { get; init; }

    /// <summary>
    /// <c>Recovered</c> | <c>CensoredAtWindowEnd</c> | <c>InvalidNoOnset</c> | <c>InvalidInstrumentation</c>.
    /// </summary>
    public required string TerminationState { get; init; }

    public double? CpuTempMaxCelsius { get; init; }

    public double? CpuClockAvgMhz { get; init; }
}
