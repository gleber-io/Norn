namespace Norn.Monitor.Prometheus;

/// <summary>
/// Assinatura fechada M = 7 ≤ 10 (docs/metrics-matrix.md, seção 1) — as sete métricas que o
/// <c>RuleEngine</c> indexa (Fase 8) e que o Analyzer detecta (tarefa 4). Nenhuma outra métrica da
/// matriz compõe a assinatura, mesmo que exista no Prometheus. Consultas parametrizadas por
/// serviço; a query em si vem literal da matriz para não divergir do que foi validado em
/// 15/09/2026.
/// </summary>
public static class PrometheusQueryCatalog
{
    public sealed record SignatureQuery(string MetricName, string Service, string PromQl);

    public static IReadOnlyList<SignatureQuery> SignatureQueries() =>
    [
        new("dotnet_process_memory_working_set_bytes", "Norn.Shop.Catalog.API",
            """dotnet_process_memory_working_set_bytes{exported_job="Norn.Shop.Catalog.API"}"""),
        new("dotnet_gc_pause_time_seconds_total", "Norn.Shop.Catalog.API",
            """rate(dotnet_gc_pause_time_seconds_total{exported_job="Norn.Shop.Catalog.API"}[1m])"""),
        new("http_server_request_duration_seconds_bucket", "Norn.Shop.Order.API",
            """histogram_quantile(0.99, sum by (le) (rate(http_server_request_duration_seconds_bucket{exported_job="Norn.Shop.Order.API"}[1m])))"""),
        new("rabbitmq_queue_messages_ready", "Norn.Shop.Order.API",
            """rabbitmq_queue_messages_ready{queue="ReserveStock"}"""),
        new("norn_shop_payments_gateway_latency_ms", "Norn.Shop.Payment.API",
            """histogram_quantile(0.99, sum by (le) (rate(norn_shop_payments_gateway_latency_ms_bucket[1m])))"""),
        .. Fase4ErrorRateByService(),
        .. Fase4ErrorsByTypeByService(),
    ];

    private static IEnumerable<SignatureQuery> Fase4ErrorRateByService() =>
        Services.Select(service => new SignatureQuery(
            "http_server_request_duration_seconds_count",
            service,
            $$"""sum(rate(http_server_request_duration_seconds_count{exported_job="{{service}}", http_response_status_code=~"5.."}[30s])) / sum(rate(http_server_request_duration_seconds_count{exported_job="{{service}}"}[30s]))"""));

    private static IEnumerable<SignatureQuery> Fase4ErrorsByTypeByService() =>
        Services.Select(service => new SignatureQuery(
            "norn_app_errors_total",
            service,
            $$"""sum by (exception_type) (norn_app_errors_total{exported_job="{{service}}"})"""));

    private static readonly string[] Services =
    [
        "Norn.Shop.Catalog.API",
        "Norn.Shop.Order.API",
        "Norn.Shop.Payment.API",
    ];
}
