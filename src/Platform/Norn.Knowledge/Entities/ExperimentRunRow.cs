namespace Norn.Knowledge.Entities;

/// <summary>
/// <c>experiment_runs</c> — lista de colunas fechada na tarefa 1a da Fase 7. É a chave que a
/// campanha inteira (Fase 12) usa; criá-la incompleta significa descobrir a lacuna depois, com
/// execuções já perdidas. Sem escritor nesta fase (fora de escopo: Planner/Executor/campanha) —
/// o formato nasce aqui para não ser revisitado sob pressão na Fase 12.
/// </summary>
public sealed class ExperimentRunRow
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

    /// <summary>Colunas abaixo (até <see cref="GitCommitSha"/> exclusive) são preenchidas depois, por
    /// <c>ExperimentRunStore.UpdateLabelingResultAsync</c> (Fase 12) — <c>set</c>, não <c>init</c>,
    /// porque a linha já existe (criada no reset) quando o Labeler as calcula, depois do teardown.</summary>
    public double? AchievedRps { get; set; }

    public required int ChaosSeed { get; init; }

    public required int LoadSeed { get; init; }

    /// <summary>Ponto do ciclo senoidal em que a injeção começou — variável controlada, constante entre execuções.</summary>
    public required double InjectionPhase { get; init; }

    /// <summary>Modo lido após o reset, não o pedido (Fase 12, tarefa 2a).</summary>
    public required string Mode { get; init; }

    public required bool ForecastEnabled { get; init; }

    /// <summary>Nulo fora da Fase 13.</summary>
    public int? ForecastHorizonMinutes { get; init; }

    public string? LlmModelDigest { get; init; }

    public int? LlmTimeoutSeconds { get; init; }

    public int? LlmNumCtx { get; init; }

    public required DateTimeOffset StartedAtUtc { get; init; }

    /// <summary>Nulo se não houve onset; no F5, o instante do kill.</summary>
    public DateTimeOffset? OnsetAtUtc { get; set; }

    /// <summary><c>onset + 10 min</c>.</summary>
    public DateTimeOffset? WindowEndAtUtc { get; set; }

    /// <summary>Nulo quando censurado.</summary>
    public DateTimeOffset? RecoveredAtUtc { get; set; }

    /// <summary>
    /// <c>Recovered</c> | <c>CensoredAtWindowEnd</c> | <c>InvalidNoOnset</c> | <c>InvalidInstrumentation</c> —
    /// a coluna que distingue censura de descarte.
    /// </summary>
    public string? TerminationState { get; set; }

    public double? CpuTempMaxCelsius { get; set; }

    public double? CpuClockAvgMhz { get; set; }

    public required int WslMemoryGb { get; init; }

    public required int WslProcessors { get; init; }

    public required string GitCommitSha { get; init; }
}
