using System.Globalization;

namespace Norn.Labeler.Csv;

/// <summary>
/// Lê o relatório do <c>Norn.LoadGenerator</c> (<c>bucket_end_utc,intended_requests,achieved_requests,failed_requests,achieved_ratio</c>,
/// ver <c>tools/Norn.LoadGenerator/LoadReport.cs</c>) — parser dedicado em vez de CsvHelper porque o
/// formato já é fixo e sem aspas/vírgulas em campo, e o único consumidor é este método.
///
/// Achado ao vivo (2º piloto F1/C, 18/09/2026): <c>achieved_requests</c> do LoadGenerator conta
/// **respostas bem-sucedidas**, não tentativas disparadas (`dispatched.Count(ok => ok)`,
/// `Norn.LoadGenerator/Program.cs`) — então a razão cai de verdade quando o alvo degrada, o que é
/// exatamente o sinal que F1/F2/F3 existem para causar. Calcular a razão sobre a janela inteira
/// (§3, intenção original: medir se o host tinha CPU para o gerador disparar a taxa pretendida)
/// descartaria como <c>InvalidInstrumentation</c> justamente as execuções em que o cenário
/// funcionou como projetado. Corrigido: a razão só considera os buckets **antes** da injeção do
/// caos (<paramref name="warmupEndUtc"/>) — janela em que o alvo ainda está saudável, então mede
/// só a capacidade do gerador, não a saúde do alvo.
/// </summary>
public static class LoadReportReader
{
    /// <summary>Razão agregada (achieved/intended) só sobre os buckets do warmup (antes de <paramref name="warmupEndUtc"/>), e a taxa correspondente em rps.</summary>
    public static (double AchievedRatio, double AchievedRps) Read(string path, double targetRps, DateTimeOffset warmupEndUtc)
    {
        var lines = File.ReadAllLines(path);
        if (lines.Length < 2)
        {
            throw new InvalidOperationException($"Relatório de carga vazio ou só com cabeçalho: {path}");
        }

        double totalIntended = 0;
        double totalAchieved = 0;
        var warmupBucketCount = 0;
        foreach (var line in lines.Skip(1))
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            var fields = line.Split(',');
            var bucketEndUtc = DateTimeOffset.Parse(fields[0], CultureInfo.InvariantCulture);
            if (bucketEndUtc > warmupEndUtc)
            {
                continue;
            }

            warmupBucketCount++;
            totalIntended += double.Parse(fields[1], CultureInfo.InvariantCulture);
            totalAchieved += double.Parse(fields[2], CultureInfo.InvariantCulture);
        }

        if (warmupBucketCount == 0)
        {
            throw new InvalidOperationException(
                $"Nenhum bucket do relatório de carga cai antes de warmup_end_utc={warmupEndUtc:o} — não dá para medir capacidade do gerador: {path}");
        }

        var ratio = totalIntended > 0 ? totalAchieved / totalIntended : 0;
        return (ratio, ratio * targetRps);
    }
}
