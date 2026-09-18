using System.Globalization;

namespace Norn.Labeler.Csv;

/// <summary>
/// Lê o relatório do <c>Norn.LoadGenerator</c> (<c>bucket_end_utc,intended_requests,achieved_requests,failed_requests,achieved_ratio</c>,
/// ver <c>tools/Norn.LoadGenerator/LoadReport.cs</c>) — parser dedicado em vez de CsvHelper porque o
/// formato já é fixo e sem aspas/vírgulas em campo, e o único consumidor é este método.
/// </summary>
public static class LoadReportReader
{
    /// <summary>Razão agregada (achieved/intended) sobre todos os buckets do relatório, e a taxa correspondente em rps.</summary>
    public static (double AchievedRatio, double AchievedRps) Read(string path, double targetRps)
    {
        var lines = File.ReadAllLines(path);
        if (lines.Length < 2)
        {
            throw new InvalidOperationException($"Relatório de carga vazio ou só com cabeçalho: {path}");
        }

        double totalIntended = 0;
        double totalAchieved = 0;
        foreach (var line in lines.Skip(1))
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            var fields = line.Split(',');
            totalIntended += double.Parse(fields[1], CultureInfo.InvariantCulture);
            totalAchieved += double.Parse(fields[2], CultureInfo.InvariantCulture);
        }

        var ratio = totalIntended > 0 ? totalAchieved / totalIntended : 0;
        return (ratio, ratio * targetRps);
    }
}
