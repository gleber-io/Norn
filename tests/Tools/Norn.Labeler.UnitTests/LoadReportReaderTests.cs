using Norn.Labeler.Csv;
using Shouldly;
using Xunit;

namespace Norn.Labeler.UnitTests;

/// <summary>
/// Achado ao vivo (2º piloto F1/C, 18/09/2026): <c>achieved_requests</c> do LoadGenerator conta
/// respostas bem-sucedidas, não tentativas disparadas — a razão cai de verdade quando o alvo
/// degrada sob F1/F2/F3, que é o próprio sinal que o cenário existe para produzir. Estes testes
/// provam que o corte por <c>warmupEndUtc</c> ignora os buckets pós-injeção.
/// </summary>
public sealed class LoadReportReaderTests
{
    private static readonly DateTimeOffset Epoch = new(2026, 9, 18, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Read_OnlyWarmupBucketsHealthy_ComputesRatioFromWarmupAlone()
    {
        // Pré-injeção (warmup): ~100% de sucesso. Pós-injeção: alvo degradado, quase tudo falha —
        // isso não pode arrastar a razão para baixo se o corte estiver certo.
        var path = WriteReport(
            (Epoch.AddSeconds(10), 10, 10),
            (Epoch.AddSeconds(20), 10, 10),
            (Epoch.AddSeconds(30), 10, 1), // já pós-injeção: alvo degradado
            (Epoch.AddSeconds(40), 10, 0));
        var warmupEndUtc = Epoch.AddSeconds(25); // injeção ocorreu entre os buckets de 20s e 30s

        try
        {
            var (ratio, achievedRps) = LoadReportReader.Read(path, targetRps: 10, warmupEndUtc);

            ratio.ShouldBe(1.0, tolerance: 0.001);
            achievedRps.ShouldBe(10.0, tolerance: 0.001);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Read_WarmupItselfUndersized_StillFlagsLowRatio()
    {
        // Se o próprio warmup já vier com razão baixa, isso É capacidade insuficiente do gerador —
        // o corte não deve mascarar esse caso.
        var path = WriteReport(
            (Epoch.AddSeconds(10), 10, 5),
            (Epoch.AddSeconds(20), 10, 5));
        var warmupEndUtc = Epoch.AddSeconds(25);

        var (ratio, achievedRps) = LoadReportReader.Read(path, targetRps: 10, warmupEndUtc);
        try
        {
            ratio.ShouldBe(0.5, tolerance: 0.001);
            achievedRps.ShouldBe(5.0, tolerance: 0.001);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Read_NoBucketBeforeWarmupEnd_Throws()
    {
        var path = WriteReport((Epoch.AddSeconds(100), 10, 10));
        var warmupEndUtc = Epoch.AddSeconds(25);

        try
        {
            Should.Throw<InvalidOperationException>(() => LoadReportReader.Read(path, targetRps: 10, warmupEndUtc));
        }
        finally
        {
            File.Delete(path);
        }
    }

    private static string WriteReport(params (DateTimeOffset BucketEndUtc, double Intended, int Achieved)[] rows)
    {
        var path = Path.GetTempFileName();
        var lines = new List<string> { "bucket_end_utc,intended_requests,achieved_requests,failed_requests,achieved_ratio" };
        lines.AddRange(rows.Select(r =>
            $"{r.BucketEndUtc:O},{r.Intended.ToString("F2", System.Globalization.CultureInfo.InvariantCulture)},{r.Achieved},0,0"));
        File.WriteAllLines(path, lines);
        return path;
    }
}
