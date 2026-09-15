using System.Globalization;

namespace Norn.LoadGenerator;

/// <summary>
/// Taxa pretendida × alcançada por bucket de amostragem — §3: "carga entregue é dado, não
/// pressuposto". Execução fora de ±10% do alvo é <c>InvalidInstrumentation</c> (decisão tomada
/// fora deste tool, na análise da campanha, a partir deste CSV).
/// </summary>
public sealed record LoadReportRow(DateTimeOffset BucketEndUtc, double IntendedRequests, int AchievedRequests, int FailedRequests);

public static class LoadReport
{
    public static async Task WriteAsync(string path, IReadOnlyList<LoadReportRow> rows, CancellationToken cancellationToken)
    {
        await using var writer = new StreamWriter(path);
        await writer.WriteLineAsync("bucket_end_utc,intended_requests,achieved_requests,failed_requests,achieved_ratio");

        foreach (var row in rows)
        {
            var ratio = row.IntendedRequests > 0 ? row.AchievedRequests / row.IntendedRequests : 1.0;
            var line = string.Join(',',
                row.BucketEndUtc.ToString("O", CultureInfo.InvariantCulture),
                row.IntendedRequests.ToString("F2", CultureInfo.InvariantCulture),
                row.AchievedRequests.ToString(CultureInfo.InvariantCulture),
                row.FailedRequests.ToString(CultureInfo.InvariantCulture),
                ratio.ToString("F4", CultureInfo.InvariantCulture));

            await writer.WriteLineAsync(line.AsMemory(), cancellationToken);
        }
    }
}
