using System.Globalization;
using Norn.LoadGenerator;

var catalogBaseUrl = GetSetting(args, "--catalog-url", "CATALOG_BASE_URL", "http://localhost:5081");
var orderBaseUrl = GetSetting(args, "--order-url", "ORDER_BASE_URL", "http://localhost:5082");
var durationMinutes = double.Parse(GetSetting(args, "--duration-minutes", "DURATION_MINUTES", "20"), CultureInfo.InvariantCulture);
var seed = int.Parse(GetSetting(args, "--seed", "SEED", "42"), CultureInfo.InvariantCulture);
var baseRps = double.Parse(GetSetting(args, "--base-rps", "BASE_RPS", "2"), CultureInfo.InvariantCulture);
var peakRps = double.Parse(GetSetting(args, "--peak-rps", "PEAK_RPS", "20"), CultureInfo.InvariantCulture);
var dayMinutes = double.Parse(GetSetting(args, "--day-minutes", "DAY_MINUTES", "15"), CultureInfo.InvariantCulture);
var sampleSeconds = double.Parse(GetSetting(args, "--sample-seconds", "SAMPLE_SECONDS", "5"), CultureInfo.InvariantCulture);
var orderShare = double.Parse(GetSetting(args, "--order-share", "ORDER_SHARE", "0.3"), CultureInfo.InvariantCulture);
var reportPath = GetSetting(args, "--out", "REPORT_PATH", "load-generator-report.csv");

Console.WriteLine($"Norn.LoadGenerator — catalog={catalogBaseUrl} order={orderBaseUrl} duration={durationMinutes}min " +
    $"seed={seed} base={baseRps}rps peak={peakRps}rps day={dayMinutes}min sample={sampleSeconds}s");

using var cts = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;
    cts.Cancel();
};

using var httpClient = new HttpClient();
var client = new ShopClient(httpClient, catalogBaseUrl, orderBaseUrl);
var profile = new LoadProfile(baseRps, peakRps, TimeSpan.FromMinutes(dayMinutes));
var random = new Random(seed);
var rows = new List<LoadReportRow>();

Console.WriteLine("Aguardando o Catalog responder para montar o carrinho de produtos...");
var productPool = await client.FetchProductPoolAsync(pageSize: 50, cts.Token);
Console.WriteLine($"Carrinho montado com {productPool.Count} produtos.");

var sampleInterval = TimeSpan.FromSeconds(sampleSeconds);
var duration = TimeSpan.FromMinutes(durationMinutes);
var start = DateTimeOffset.UtcNow;

using var timer = new PeriodicTimer(sampleInterval);

try
{
    while (await timer.WaitForNextTickAsync(cts.Token))
    {
        var elapsed = DateTimeOffset.UtcNow - start;
        if (elapsed >= duration)
        {
            break;
        }

        var rate = profile.RequestsPerSecond(elapsed);
        var intended = rate * sampleInterval.TotalSeconds;
        var intendedCount = (int)Math.Round(intended);

        var dispatched = await DispatchBucketAsync(client, productPool, random, orderShare, intendedCount, cts.Token);
        var achieved = dispatched.Count(ok => ok);
        var failed = dispatched.Length - achieved;

        rows.Add(new LoadReportRow(DateTimeOffset.UtcNow, intended, achieved, failed));
        Console.WriteLine($"{DateTimeOffset.UtcNow:HH:mm:ss} elapsed={elapsed:mm\\:ss} rate={rate:F1}rps intended={intendedCount} achieved={achieved} failed={failed}");
    }
}
catch (OperationCanceledException)
{
    Console.WriteLine("Interrompido — gravando o relatório parcial.");
}

await LoadReport.WriteAsync(reportPath, rows, CancellationToken.None);
Console.WriteLine($"Relatório gravado em {Path.GetFullPath(reportPath)}.");

return;

static async Task<bool[]> DispatchBucketAsync(
    ShopClient client,
    IReadOnlyList<ShopClient.ProductSummary> pool,
    Random random,
    double orderShare,
    int count,
    CancellationToken cancellationToken)
{
    // Todo o sorteio acontece aqui, sequencialmente — `Random` não é thread-safe, e as tarefas
    // abaixo rodam em paralelo. Nenhuma delas toca `random` depois deste ponto.
    var tasks = new Task<bool>[count];
    for (var i = 0; i < count; i++)
    {
        if (random.NextDouble() < orderShare)
        {
            var itemCount = random.Next(1, 4);
            var items = Enumerable.Range(0, itemCount)
                .Select(_ => pool[random.Next(pool.Count)])
                .Select(p => new ShopClient.CartItem(p.Id, random.Next(1, 3), p.Price))
                .ToList();

            tasks[i] = client.CreateOrderAsync(items, cancellationToken);
        }
        else
        {
            tasks[i] = client.BrowseProductsAsync(cancellationToken);
        }
    }

    return await Task.WhenAll(tasks);
}

static string GetSetting(string[] args, string flag, string envVar, string defaultValue)
{
    for (var i = 0; i < args.Length - 1; i++)
    {
        if (string.Equals(args[i], flag, StringComparison.OrdinalIgnoreCase))
        {
            return args[i + 1];
        }
    }

    return Environment.GetEnvironmentVariable(envVar) ?? defaultValue;
}
