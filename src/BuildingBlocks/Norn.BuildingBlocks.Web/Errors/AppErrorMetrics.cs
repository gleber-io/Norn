using System.Collections.Frozen;
using System.Diagnostics.Metrics;

namespace Norn.BuildingBlocks.Web.Errors;

/// <summary>
/// Sinal derivado de log, na forma contada (ADR-10, tarefa 5a da Fase 4): <c>norn_app_errors_total</c>,
/// fonte de <c>AnomalyContext.recentMetrics.errorsByType</c>. Cardinalidade fechada por
/// <c>exception_type</c> — tipo fora da lista cai em "Other" para nunca virar rótulo de alta cardinalidade
/// (mensagem, stack trace ou id de requisição derrubariam o Prometheus, §8 Fase 4).
/// A lista fechada é a mesma registrada em docs/metrics-matrix.md; mudar uma exige mudar a outra.
/// </summary>
internal static class AppErrorMetrics
{
    private static readonly FrozenSet<string> ExpectedExceptionTypes = new[]
    {
        "NpgsqlException",
        "DbUpdateException",
        "TimeoutException",
        "OperationCanceledException",
        "TaskCanceledException",
        "HttpRequestException",
        "InvalidOperationException",
        "InvalidPaymentTransitionException",
    }.ToFrozenSet();

    private static readonly Meter Meter = new("Norn.BuildingBlocks.Web");
    private static readonly Counter<long> ErrorsTotal = Meter.CreateCounter<long>("norn_app_errors_total");

    public static void RecordException(Exception exception)
    {
        var label = "Other";

        // Sobe a hierarquia de tipos: um Npgsql.PostgresException (erro do servidor) é subclasse de
        // NpgsqlException, e deve cair no mesmo balde — não em "Other" por não bater o tipo exato.
        for (var type = exception.GetType(); type is not null; type = type.BaseType)
        {
            if (ExpectedExceptionTypes.Contains(type.Name))
            {
                label = type.Name;
                break;
            }
        }

        ErrorsTotal.Add(1, new KeyValuePair<string, object?>("exception_type", label));
    }
}
