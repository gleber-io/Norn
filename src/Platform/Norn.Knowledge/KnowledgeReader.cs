using Microsoft.EntityFrameworkCore;
using Norn.Contracts;
using Norn.Contracts.Ports;
using Norn.Contracts.Serialization;
using Norn.Knowledge.Entities;

namespace Norn.Knowledge;

/// <summary>
/// Implementa <see cref="IKnowledgeReader"/> sobre <see cref="KnowledgeDbContext"/> (ADR-17) —
/// lado de leitura de <see cref="KnowledgeStore"/>, consumido só por Norn.API (Fase 10).
/// Desserializa <c>payload</c> com as mesmas opções que <see cref="CanonicalJson"/> usa para
/// serializar (camelCase, sem conversor de enum — o jsonb grava enum como número, não string).
/// </summary>
public sealed class KnowledgeReader(KnowledgeDbContext dbContext) : IKnowledgeReader
{
    private static readonly System.Text.Json.JsonSerializerOptions DeserializeOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    public async Task<IReadOnlyList<TopologyInfo>> GetLatestTopologyAsync(CancellationToken cancellationToken)
    {
        var services = await dbContext.AnomalyContexts
            .Select(row => row.Service)
            .Distinct()
            .ToListAsync(cancellationToken);

        var result = new List<TopologyInfo>(services.Count);
        foreach (var service in services)
        {
            var latest = await dbContext.AnomalyContexts
                .Where(row => row.Service == service)
                .OrderByDescending(row => row.CreatedAtUtc)
                .FirstOrDefaultAsync(cancellationToken);

            if (latest is null)
            {
                continue;
            }

            var context = System.Text.Json.JsonSerializer.Deserialize<AnomalyContext>(latest.Payload, DeserializeOptions);
            if (context is not null)
            {
                result.Add(context.Topology);
            }
        }

        return result;
    }

    public async Task<IReadOnlyList<AnomalySignal>> GetRecentSignalsAsync(int limit, Guid? experimentRunId, CancellationToken cancellationToken)
    {
        var query = dbContext.AnomalySignals.AsQueryable();
        if (experimentRunId is not null)
        {
            query = query.Where(row => row.ExperimentRunId == experimentRunId);
        }

        var rows = await query
            .OrderByDescending(row => row.DetectedAtUtc)
            .Take(limit)
            .ToListAsync(cancellationToken);

        return Deserialize<AnomalySignal>(rows.Select(row => row.Payload));
    }

    public async Task<IReadOnlyList<HealingPlan>> GetRecentPlansAsync(int limit, CancellationToken cancellationToken)
    {
        var rows = await dbContext.HealingPlans
            .OrderByDescending(row => row.CreatedAtUtc)
            .Take(limit)
            .ToListAsync(cancellationToken);

        return Deserialize<HealingPlan>(rows.Select(row => row.Payload));
    }

    public async Task<IReadOnlyList<HealingOutcome>> GetRecentOutcomesAsync(int limit, CancellationToken cancellationToken)
    {
        var rows = await dbContext.HealingOutcomes
            .OrderByDescending(row => row.VerifiedAtUtc)
            .Take(limit)
            .ToListAsync(cancellationToken);

        return Deserialize<HealingOutcome>(rows.Select(row => row.Payload));
    }

    public async Task<ExperimentRunSummary?> GetExperimentRunAsync(Guid experimentRunId, CancellationToken cancellationToken)
    {
        var row = await dbContext.ExperimentRuns
            .SingleOrDefaultAsync(r => r.ExperimentRunId == experimentRunId, cancellationToken);

        return row is null ? null : ToSummary(row);
    }

    private static List<T> Deserialize<T>(IEnumerable<string> payloads) =>
        payloads
            .Select(payload => System.Text.Json.JsonSerializer.Deserialize<T>(payload, DeserializeOptions))
            .Where(value => value is not null)
            .Select(value => value!)
            .ToList();

    private static ExperimentRunSummary ToSummary(ExperimentRunRow row) => new()
    {
        ExperimentRunId = row.ExperimentRunId,
        Scenario = row.Scenario,
        Arm = row.Arm,
        Repetition = row.Repetition,
        RunOrder = row.RunOrder,
        RandomizationSeed = row.RandomizationSeed,
        TargetRps = row.TargetRps,
        AchievedRps = row.AchievedRps,
        ChaosSeed = row.ChaosSeed,
        LoadSeed = row.LoadSeed,
        InjectionPhase = row.InjectionPhase,
        Mode = row.Mode,
        ForecastEnabled = row.ForecastEnabled,
        ForecastHorizonMinutes = row.ForecastHorizonMinutes,
        LlmModelDigest = row.LlmModelDigest,
        LlmTimeoutSeconds = row.LlmTimeoutSeconds,
        LlmNumCtx = row.LlmNumCtx,
        StartedAtUtc = row.StartedAtUtc,
        OnsetAtUtc = row.OnsetAtUtc,
        WindowEndAtUtc = row.WindowEndAtUtc,
        RecoveredAtUtc = row.RecoveredAtUtc,
        TerminationState = row.TerminationState,
        CpuTempMaxCelsius = row.CpuTempMaxCelsius,
        CpuClockAvgMhz = row.CpuClockAvgMhz,
        WslMemoryGb = row.WslMemoryGb,
        WslProcessors = row.WslProcessors,
        GitCommitSha = row.GitCommitSha,
    };
}
