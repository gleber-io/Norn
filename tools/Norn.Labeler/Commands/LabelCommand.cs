using Microsoft.Extensions.DependencyInjection;
using Norn.Contracts;
using Norn.Contracts.Ports;
using Norn.Labeler.Cli;
using Norn.Labeler.Csv;
using Norn.Labeler.Detection;
using Norn.Labeler.Prometheus;

namespace Norn.Labeler.Commands;

/// <summary>
/// Fase 12, tarefa 1 — chamado por <c>run-experiment.ps1</c> depois do teardown de uma execução.
/// Único lugar que decide onset, recuperação e estado de término (§3) — a fronteira entre .NET e
/// Python é o CSV que este comando produz, e só ele.
/// </summary>
public static class LabelCommand
{
    private static readonly TimeSpan ScrapeStep = TimeSpan.FromSeconds(5);

    public static async Task RunAsync(string[] args, IServiceProvider services, CancellationToken cancellationToken)
    {
        var runId = ArgReader.GetRequiredGuid(args, "--run-id");
        var scenario = ArgReader.GetRequiredSetting(args, "--scenario");
        var arm = ArgReader.GetRequiredSetting(args, "--arm");
        var repetition = ArgReader.GetRequiredInt(args, "--repetition");
        var runOrder = ArgReader.GetRequiredInt(args, "--run-order");
        var targetRps = ArgReader.GetRequiredDouble(args, "--target-rps");
        var targetService = ArgReader.GetRequiredSetting(args, "--target-service");
        var windowStart = ArgReader.GetRequiredDateTimeOffset(args, "--window-start-utc");
        var observationEnd = ArgReader.GetRequiredDateTimeOffset(args, "--observation-end-utc");
        var loadReportPath = ArgReader.GetRequiredSetting(args, "--load-report");

        // Achado ao vivo (2º piloto F1/C): a capacidade do gerador só é medível enquanto o alvo
        // ainda está saudável — depois da injeção, uma razão baixa é o próprio cenário funcionando
        // (LoadReportReader.cs tem o detalhe completo).
        var injectionAtUtc = ArgReader.GetRequiredDateTimeOffset(args, "--injection-at-utc");

        // container_oom_events_total não é observável neste ambiente (docs/metrics-matrix.md) — o
        // instante do OOMKilled chega de fora, capturado pelo run-experiment.ps1 via kubectl logo
        // após o teardown, enquanto status.containerStatuses[].lastState ainda está fresco.
        var oomKilledAtUtc = ArgReader.GetOptionalDateTimeOffset(args, "--oom-killed-at-utc");
        var f5KillAtUtc = ArgReader.GetOptionalDateTimeOffset(args, "--f5-kill-at-utc");
        var cpuTempMaxCelsius = TryGetDouble(args, "--cpu-temp-max-celsius");
        var cpuClockAvgMhz = TryGetDouble(args, "--cpu-clock-avg-mhz");
        var labeledCsvPath = ArgReader.GetSetting(args, "--labeled-csv") ?? DefaultPath("labeled-runs.csv");
        var discardedCsvPath = ArgReader.GetSetting(args, "--discarded-csv") ?? DefaultPath("discarded-runs.csv");

        var promQl = $"100 * sum(rate(http_server_request_duration_seconds_count{{exported_job=\"{targetService}\", http_response_status_code=~\"5..\"}}[30s])) " +
                     $"/ sum(rate(http_server_request_duration_seconds_count{{exported_job=\"{targetService}\"}}[30s]))";
        var prometheusClient = services.GetRequiredService<PrometheusRangeClient>();
        var errorRateSamples = await prometheusClient.QueryRangeAsync(promQl, windowStart, observationEnd, ScrapeStep, cancellationToken);

        var labeling = OnsetRecoveryCalculator.Calculate(scenario, errorRateSamples, oomKilledAtUtc, f5KillAtUtc);

        var (achievedRatio, achievedRps) = LoadReportReader.Read(loadReportPath, targetRps, injectionAtUtc);
        var withinLoadTarget = LoadDeliveryChecker.IsWithinTarget(targetRps, achievedRps);

        // Carga fora de ±10% do alvo (§3) invalida a execução independentemente do que o onset e a
        // recuperação mostrarem — contenção de CPU do host vira variância inexplicada se não for
        // descartada aqui, e é o Labeler quem já calculou achieved_rps, então é aqui que se decide.
        var finalState = withinLoadTarget ? labeling.TerminationState : TerminationState.InvalidInstrumentation;

        var experimentRunStore = services.GetRequiredService<IExperimentRunStore>();
        await experimentRunStore.UpdateLabelingResultAsync(new ExperimentRunLabelingResult
        {
            ExperimentRunId = runId,
            AchievedRps = achievedRps,
            OnsetAtUtc = labeling.OnsetAtUtc,
            WindowEndAtUtc = labeling.WindowEndAtUtc,
            RecoveredAtUtc = labeling.RecoveredAtUtc,
            TerminationState = finalState.ToString(),
            CpuTempMaxCelsius = cpuTempMaxCelsius,
            CpuClockAvgMhz = cpuClockAvgMhz,
        }, cancellationToken);

        if (finalState is TerminationState.Recovered or TerminationState.CensoredAtWindowEnd)
        {
            var durationEnd = finalState == TerminationState.Recovered ? labeling.RecoveredAtUtc!.Value : labeling.WindowEndAtUtc!.Value;
            CampaignCsv.AppendLabeledRun(labeledCsvPath, new LabeledRunRow
            {
                ExperimentRunId = runId,
                Scenario = scenario,
                Arm = arm,
                Repetition = repetition,
                RunOrder = runOrder,
                TargetRps = targetRps,
                AchievedRps = achievedRps,
                TerminationState = finalState.ToString(),
                OnsetAtUtc = labeling.OnsetAtUtc,
                RecoveredAtUtc = labeling.RecoveredAtUtc,
                WindowEndAtUtc = labeling.WindowEndAtUtc,
                TempoAteRecuperacaoSegundos = (durationEnd - labeling.OnsetAtUtc!.Value).TotalSeconds,
                EventoObservado = finalState == TerminationState.Recovered ? 1 : 0,
            });
        }
        else
        {
            string detail;
            if (finalState == TerminationState.InvalidInstrumentation && labeling.TerminationState != TerminationState.InvalidInstrumentation)
            {
                detail = string.Create(System.Globalization.CultureInfo.InvariantCulture,
                    $"achieved_rps={achievedRps:F2} fora de ±{LoadDeliveryChecker.AllowedDeviation:P0} de target_rps={targetRps:F2}");
            }
            else if (scenario == "F5")
            {
                detail = "F5 sem instante de kill (--f5-kill-at-utc ausente ou 'none') — norn:chaos:active não gravou firedAtUtc.";
            }
            else
            {
                detail = "Nenhuma amostra cruzou o limiar de onset dentro do intervalo observado.";
            }
            CampaignCsv.AppendDiscardedRun(discardedCsvPath, new DiscardedRunRow
            {
                ExperimentRunId = runId,
                Scenario = scenario,
                Arm = arm,
                Repetition = repetition,
                Reason = finalState.ToString(),
                Detail = detail,
            });
        }

        Console.WriteLine($"termination_state={finalState}");
        Console.WriteLine($"onset_at_utc={labeling.OnsetAtUtc?.ToString("O") ?? "null"}");
        Console.WriteLine($"recovered_at_utc={labeling.RecoveredAtUtc?.ToString("O") ?? "null"}");
        Console.WriteLine(string.Create(System.Globalization.CultureInfo.InvariantCulture, $"achieved_rps={achievedRps:F2}"));
    }

    private static double? TryGetDouble(string[] args, string flag)
    {
        var raw = ArgReader.GetSetting(args, flag);
        return raw is null ? null : double.Parse(raw, System.Globalization.CultureInfo.InvariantCulture);
    }

    // Relativo ao diretório de trabalho, não ao binário: run-experiment.ps1 sempre invoca
    // `dotnet run --project tools/Norn.Labeler` a partir da raiz do repositório.
    private static string DefaultPath(string fileName) =>
        Path.Combine("tools", "analysis", "data", fileName);
}
