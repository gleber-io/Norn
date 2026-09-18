using Microsoft.Extensions.DependencyInjection;
using Norn.Contracts;
using Norn.Contracts.Ports;
using Norn.Labeler.Cli;

namespace Norn.Labeler.Commands;

/// <summary>
/// Fase 12, tarefa 1a — grava as colunas de controle de <c>experiment_runs</c>, conhecidas no
/// instante do reset (antes do onset existir). Chamado por <c>run-experiment.ps1</c> logo depois do
/// <c>reset</c>, com o modo já confirmado (não o pedido) passado como <c>--mode</c>.
/// </summary>
public static class InitRunCommand
{
    public static async Task RunAsync(string[] args, IServiceProvider services, CancellationToken cancellationToken)
    {
        var record = new ExperimentRunRecord
        {
            ExperimentRunId = ArgReader.GetRequiredGuid(args, "--run-id"),
            Scenario = ArgReader.GetRequiredSetting(args, "--scenario"),
            Arm = ArgReader.GetRequiredSetting(args, "--arm"),
            Repetition = ArgReader.GetRequiredInt(args, "--repetition"),
            RunOrder = ArgReader.GetRequiredInt(args, "--run-order"),
            RandomizationSeed = ArgReader.GetRequiredInt(args, "--randomization-seed"),
            TargetRps = ArgReader.GetRequiredDouble(args, "--target-rps"),
            ChaosSeed = ArgReader.GetRequiredInt(args, "--chaos-seed"),
            LoadSeed = ArgReader.GetRequiredInt(args, "--load-seed"),
            InjectionPhase = ArgReader.GetRequiredDouble(args, "--injection-phase"),
            Mode = ArgReader.GetRequiredSetting(args, "--mode"),
            ForecastEnabled = string.Equals(ArgReader.GetSetting(args, "--forecast-enabled") ?? "false", "true", StringComparison.OrdinalIgnoreCase),
            ForecastHorizonMinutes = TryGetInt(args, "--forecast-horizon-minutes"),
            LlmModelDigest = ArgReader.GetSetting(args, "--llm-model-digest"),
            LlmTimeoutSeconds = TryGetInt(args, "--llm-timeout-seconds"),
            LlmNumCtx = TryGetInt(args, "--llm-num-ctx"),
            StartedAtUtc = ArgReader.GetRequiredDateTimeOffset(args, "--started-at-utc"),
            WslMemoryGb = ArgReader.GetRequiredInt(args, "--wsl-memory-gb"),
            WslProcessors = ArgReader.GetRequiredInt(args, "--wsl-processors"),
            GitCommitSha = ArgReader.GetRequiredSetting(args, "--git-commit-sha"),
        };

        var store = services.GetRequiredService<IExperimentRunStore>();
        await store.CreateAsync(record, cancellationToken);
        Console.WriteLine($"experiment_run_id={record.ExperimentRunId}");
    }

    private static int? TryGetInt(string[] args, string flag)
    {
        var raw = ArgReader.GetSetting(args, flag);
        return raw is null ? null : int.Parse(raw, System.Globalization.CultureInfo.InvariantCulture);
    }
}
