/** Assinatura fechada M=7 (docs/metrics-matrix.md) — não exposta pela Norn.API (vive em
 * Norn.Monitor, Fase 7, fora do alcance de Norn.API por ADR-17), então o seletor da feature
 * `metrics` replica a lista aqui, só os nomes (a query real é montada pelo backend). */
export const METRIC_CATALOG = [
  "dotnet_process_memory_working_set_bytes",
  "dotnet_gc_pause_time_seconds_total",
  "http_server_request_duration_seconds_bucket",
  "rabbitmq_queue_messages_ready",
  "norn_shop_payments_gateway_latency_ms_bucket",
  "http_server_request_duration_seconds_count",
  "norn_app_errors_total",
] as const;
