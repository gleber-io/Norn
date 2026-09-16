namespace Norn.Contracts;

/// <summary>
/// Espelha <c>experiment_runs</c> (Norn.Knowledge, tarefa 1a da Fase 7) coluna a coluna — vive
/// aqui, não em Norn.Knowledge, porque <see cref="Ports.IKnowledgeReader"/> é um port de
/// Norn.Contracts e o núcleo nunca referencia o adaptador (ADR-17). Sem escritor ainda (a
/// campanha, Fase 12, é quem grava); só leitura por <c>GET /api/v1/experiments/{runId}</c>
/// (Fase 10).
/// </summary>
public sealed record ExperimentRunSummary
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

    public double? AchievedRps { get; init; }

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

    public DateTimeOffset? OnsetAtUtc { get; init; }

    public DateTimeOffset? WindowEndAtUtc { get; init; }

    public DateTimeOffset? RecoveredAtUtc { get; init; }

    /// <summary>
    /// <c>Recovered</c> | <c>CensoredAtWindowEnd</c> | <c>InvalidNoOnset</c> | <c>InvalidInstrumentation</c>.
    /// </summary>
    public string? TerminationState { get; init; }

    public double? CpuTempMaxCelsius { get; init; }

    public double? CpuClockAvgMhz { get; init; }

    public required int WslMemoryGb { get; init; }

    public required int WslProcessors { get; init; }

    public required string GitCommitSha { get; init; }
}
