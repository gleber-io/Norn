using Microsoft.EntityFrameworkCore;
using Norn.Contracts;
using Norn.Contracts.Ports;
using Norn.Knowledge.Entities;

namespace Norn.Knowledge;

/// <summary>
/// Implementa <see cref="IExperimentRunStore"/> sobre <see cref="KnowledgeDbContext"/> (Fase 12).
/// Público pelo mesmo motivo que <see cref="KnowledgeStore"/>: testável direto contra Postgres real
/// sem carregar o resto de <c>AddNornKnowledge</c>.
/// </summary>
public sealed class ExperimentRunStore(KnowledgeDbContext dbContext) : IExperimentRunStore
{
    public async Task CreateAsync(ExperimentRunRecord record, CancellationToken cancellationToken)
    {
        dbContext.ExperimentRuns.Add(new ExperimentRunRow
        {
            ExperimentRunId = record.ExperimentRunId,
            Scenario = record.Scenario,
            Arm = record.Arm,
            Repetition = record.Repetition,
            RunOrder = record.RunOrder,
            RandomizationSeed = record.RandomizationSeed,
            TargetRps = record.TargetRps,
            ChaosSeed = record.ChaosSeed,
            LoadSeed = record.LoadSeed,
            InjectionPhase = record.InjectionPhase,
            Mode = record.Mode,
            ForecastEnabled = record.ForecastEnabled,
            ForecastHorizonMinutes = record.ForecastHorizonMinutes,
            LlmModelDigest = record.LlmModelDigest,
            LlmTimeoutSeconds = record.LlmTimeoutSeconds,
            LlmNumCtx = record.LlmNumCtx,
            StartedAtUtc = record.StartedAtUtc,
            WslMemoryGb = record.WslMemoryGb,
            WslProcessors = record.WslProcessors,
            GitCommitSha = record.GitCommitSha,
        });

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateLabelingResultAsync(ExperimentRunLabelingResult result, CancellationToken cancellationToken)
    {
        var row = await dbContext.ExperimentRuns.SingleAsync(r => r.ExperimentRunId == result.ExperimentRunId, cancellationToken);

        row.AchievedRps = result.AchievedRps;
        row.OnsetAtUtc = result.OnsetAtUtc;
        row.WindowEndAtUtc = result.WindowEndAtUtc;
        row.RecoveredAtUtc = result.RecoveredAtUtc;
        row.TerminationState = result.TerminationState;
        row.CpuTempMaxCelsius = result.CpuTempMaxCelsius;
        row.CpuClockAvgMhz = result.CpuClockAvgMhz;

        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
