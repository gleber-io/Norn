using System.Globalization;
using System.Text.Json;
using CsvHelper;
using Norn.Contracts;
using Norn.PairedAnalysis;
using Npgsql;

var connectionString = GetSetting(args, "--connection-string")
    ?? Environment.GetEnvironmentVariable("NORN_KNOWLEDGE_CONNECTION")
    ?? "Host=localhost;Database=norn;Username=norn;Password=norn";
var outputPath = GetSetting(args, "--out") ?? Path.Combine("tools", "analysis", "data", "paired-analysis.csv");

// Mesmas opções que Norn.Knowledge.KnowledgeReader usa para desserializar payload (camelCase,
// sem conversor de enum — o jsonb grava enum como número). Duplicado aqui de propósito: Norn.PairedAnalysis
// não referencia Norn.Knowledge (§4 do Master Plan).
var deserializeOptions = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };

using var cts = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;
    cts.Cancel();
};
var cancellationToken = cts.Token;

await using var connection = new NpgsqlConnection(connectionString);
await connection.OpenAsync(cancellationToken);

// Só contextos do braço B: o recálculo do RuleEngine sobre B é o que sustenta H2 pareada (§3).
// O join com healing_plans presume no máximo um plano por contexto — verdade hoje porque
// AnomalyPipelineBackgroundService só decide um HealingPlan por AnomalyContext (Fase 9), mas
// IX_healing_plans_context_id não é um índice único no schema. Se isso mudar, este join faria
// fan-out silencioso.
const string sql = """
    select ac.context_id, ac.experiment_run_id, ac.payload, er.scenario, hp.payload as plan_payload
    from platform.anomaly_contexts ac
    join platform.experiment_runs er on er.experiment_run_id = ac.experiment_run_id
    join platform.healing_plans hp on hp.context_id = ac.context_id
    where er.arm = 'B'
    order by ac.created_at_utc
    """;

await using var command = new NpgsqlCommand(sql, connection);
await using var reader = await command.ExecuteReaderAsync(cancellationToken);

var rows = new List<PairedDecisionRow>();
while (await reader.ReadAsync(cancellationToken))
{
    var contextPayload = reader.GetString(2);
    var scenario = reader.GetString(3);
    var planPayload = reader.GetString(4);

    var context = JsonSerializer.Deserialize<AnomalyContext>(contextPayload, deserializeOptions)
        ?? throw new InvalidOperationException($"anomaly_contexts.payload nulo para context_id={reader.GetGuid(0)}");
    var plan = JsonSerializer.Deserialize<HealingPlan>(planPayload, deserializeOptions)
        ?? throw new InvalidOperationException($"healing_plans.payload nulo para context_id={reader.GetGuid(0)}");

    var llmAction = plan.Actions.Count > 0 ? plan.Actions[0].Type : HealingActionType.NoOp;
    rows.Add(PairedDecisionCalculator.Calculate(reader.GetGuid(1), scenario, context, llmAction));
}

var directory = Path.GetDirectoryName(outputPath);
if (!string.IsNullOrEmpty(directory))
{
    Directory.CreateDirectory(directory);
}

await using (var writer = new StreamWriter(outputPath))
await using (var csv = new CsvWriter(writer, CultureInfo.InvariantCulture))
{
    csv.WriteRecords(rows);
}

var rawAgreement = rows.Count == 0 ? 0.0 : (double)rows.Count(r => r.LlmAction == r.RuleAction) / rows.Count;
Console.WriteLine($"pares={rows.Count}");
Console.WriteLine(string.Create(CultureInfo.InvariantCulture, $"concordancia_bruta_llm_regra={rawAgreement:F4}"));
Console.WriteLine($"csv={Path.GetFullPath(outputPath)}");

static string? GetSetting(string[] args, string flag)
{
    for (var i = 0; i < args.Length - 1; i++)
    {
        if (string.Equals(args[i], flag, StringComparison.OrdinalIgnoreCase))
        {
            return args[i + 1];
        }
    }

    return null;
}
