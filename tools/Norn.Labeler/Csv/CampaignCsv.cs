using System.Globalization;
using CsvHelper;
using CsvHelper.Configuration.Attributes;

namespace Norn.Labeler.Csv;

/// <summary>
/// Uma linha por execução válida — fronteira entre .NET e Python (§3, "A fronteira entre .NET e
/// Python é um arquivo, e só ele"). Já no formato exigido por Kaplan-Meier: <see cref="TempoAteRecuperacaoSegundos"/>
/// e <see cref="EventoObservado"/> por execução. Cabeçalho em snake_case (<see cref="NameAttribute"/>)
/// de propósito: quem lê este CSV é <c>tools/analysis/</c>, em Python — pandas, não C#.
/// </summary>
public sealed record LabeledRunRow
{
    [Name("experiment_run_id")]
    public required Guid ExperimentRunId { get; init; }

    [Name("scenario")]
    public required string Scenario { get; init; }

    [Name("arm")]
    public required string Arm { get; init; }

    [Name("repetition")]
    public required int Repetition { get; init; }

    [Name("run_order")]
    public required int RunOrder { get; init; }

    [Name("target_rps")]
    public required double TargetRps { get; init; }

    [Name("achieved_rps")]
    public required double AchievedRps { get; init; }

    /// <summary><c>Recovered</c> | <c>CensoredAtWindowEnd</c> — as duas únicas que chegam a este CSV.</summary>
    [Name("termination_state")]
    public required string TerminationState { get; init; }

    [Name("onset_at_utc")]
    public DateTimeOffset? OnsetAtUtc { get; init; }

    [Name("recovered_at_utc")]
    public DateTimeOffset? RecoveredAtUtc { get; init; }

    [Name("window_end_at_utc")]
    public DateTimeOffset? WindowEndAtUtc { get; init; }

    /// <summary>Segundos até o evento (recuperação) ou até a censura (fim da janela de 10 min).</summary>
    [Name("tempo_ate_recuperacao_segundos")]
    public required double TempoAteRecuperacaoSegundos { get; init; }

    /// <summary>1 = recuperação observada, 0 = censurado — formato exigido por <c>lifelines.KaplanMeierFitter</c>.</summary>
    [Name("evento_observado")]
    public required int EventoObservado { get; init; }
}

/// <summary>Execuções que não entram no dataset (§3): censura é resultado, descarte é defeito — os dois nunca se confundem.</summary>
public sealed record DiscardedRunRow
{
    [Name("experiment_run_id")]
    public required Guid ExperimentRunId { get; init; }

    [Name("scenario")]
    public required string Scenario { get; init; }

    [Name("arm")]
    public required string Arm { get; init; }

    [Name("repetition")]
    public required int Repetition { get; init; }

    /// <summary><c>InvalidNoOnset</c> | <c>InvalidInstrumentation</c>.</summary>
    [Name("reason")]
    public required string Reason { get; init; }

    [Name("detail")]
    public required string Detail { get; init; }
}

/// <summary>Escreve em modo apêndice, um cabeçalho só na primeira vez — cada execução da campanha chama isto uma vez.</summary>
public static class CampaignCsv
{
    public static void AppendLabeledRun(string path, LabeledRunRow row) => Append(path, row);

    public static void AppendDiscardedRun(string path, DiscardedRunRow row) => Append(path, row);

    private static void Append<T>(string path, T row)
    {
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var writeHeader = !File.Exists(path);
        using var stream = new FileStream(path, FileMode.Append, FileAccess.Write);
        using var writer = new StreamWriter(stream);
        using var csv = new CsvWriter(writer, CultureInfo.InvariantCulture);

        if (writeHeader)
        {
            csv.WriteHeader<T>();
            csv.NextRecord();
        }

        csv.WriteRecord(row);
        csv.NextRecord();
    }
}
