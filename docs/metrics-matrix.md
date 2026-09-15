# Matriz de rastreabilidade de métricas (Fase 4)

> Cada métrica exigida pela §3 do plano, com nome Prometheus, query, origem, limiar/SLO e bandas de
> severidade (ADR-14). Bandas e limiares alimentam o `IOptions` do `Norn.Analyzer` na Fase 7 — são
> valor inicial, a confirmar/calibrar lá com dados reais de execução, não constante definitiva.
>
> **Rótulo de serviço.** O exporter `prometheus` do Collector renomeia os atributos de resource
> `service.name`/`service.instance.id` para `exported_job`/`exported_instance`, porque o próprio
> scrape do Prometheus já ocupa `job`/`instance` (job `otel-collector`, todas as métricas OTLP). Toda
> query abaixo filtra por `exported_job`, não por `service_name` — esse rótulo não existe neste
> pipeline. Verificado em 15/09/2026 rodando os três serviços do Shop e consultando
> `/api/v1/labels` do Prometheus.
>
> **Formato de severidade.** Duas fórmulas (ADR-14): métrica com SLO usa `distância = observado / limiar`
> (bandas por fração do limiar); indicador antecedente sem SLO usa `desvio = (observado − expectedValue) / expectedValue`
> (bandas por desvio relativo à baseline). Ambas de ponto único, calculadas pelo `Norn.Analyzer`.

## 1. Assinatura fechada (M = 7 ≤ 10) — tarefa 1a

As sete métricas abaixo são as candidatas naturais citadas no plano (§8, Fase 4): discriminam F1, F2
e F3 e cobrem o `errorsByType` do ADR-10. **Este é o conjunto que o `RuleEngine` indexa** — o teste de
totalidade da tarefa 9a da Fase 8 roda sobre 2⁷ = 128 casos. Nenhuma outra métrica da matriz compõe a
assinatura, mesmo que existam no Prometheus.

| # | Métrica | Cenário |
|---|---|---|
| 1 | RSS (`dotnet_process_memory_working_set_bytes`) | F1 |
| 2 | Tempo de GC (`dotnet_gc_pause_time_seconds_total`, taxa) | F1 |
| 3 | Latência p99 (`http_server_request_duration_seconds_bucket`) | F2 |
| 4 | Profundidade de fila (`rabbitmq_queue_messages_ready`) | F2 |
| 5 | Latência externa do gateway (`norn_shop_payments_gateway_latency_ms_bucket`) | F3 |
| 6 | Taxa de 5xx (`http_server_request_duration_seconds_count{http_response_status_code=~"5.."}`) | F3 / onset geral |
| 7 | `errorsByType` (`norn_app_errors_total{exception_type}`) | geral (ADR-10) |

## 2. Matriz completa

| Métrica (§3) | Nome Prometheus | Query PromQL | Serviço | Fonte | SLO / limiar de referência | Bandas de severidade (Low\|Medium\|High\|Critical) | Assinatura? |
|---|---|---|---|---|---|---|---|
| RSS (working set) | `dotnet_process_memory_working_set_bytes` | `dotnet_process_memory_working_set_bytes{exported_job="Norn.Shop.Catalog.API"}` | Catalog.API | OTLP (`System.Runtime`, sem pacote — tarefa 4) | `expectedValue` = baseline pós-warmup (medido no piloto, Fase 12) | desvio: <25%\|25–75%\|75–150%\|≥150% acima da baseline | **Sim** |
| Tempo de GC | `dotnet_gc_pause_time_seconds_total` | `rate(dotnet_gc_pause_time_seconds_total{exported_job="Norn.Shop.Catalog.API"}[1m])` | Catalog.API | OTLP | `expectedValue` = fração de tempo em GC pós-warmup | desvio: <25%\|25–75%\|75–150%\|≥150% | **Sim** |
| Latência p99 | `http_server_request_duration_seconds_bucket` | `histogram_quantile(0.99, sum by (le) (rate(http_server_request_duration_seconds_bucket{exported_job="Norn.Shop.Order.API"}[1m])))` | Order.API | OTLP (`AddAspNetCoreInstrumentation`) | SLO = 300 ms | distância=obs/slo: <50%\|50–80%\|80–100%\|≥100% (breach) | **Sim** |
| Profundidade de fila | `rabbitmq_queue_messages_ready` | `rabbitmq_queue_messages_ready{queue="ReserveStock"}` | Order.API (fila alvo do F2) | rabbitmq_prometheus (tarefa 5) | `expectedValue` = profundidade em regime (piloto) | desvio: <50%\|50–150%\|150–400%\|≥400% | **Sim** |
| Consumer lag | `rabbitmq_queue_messages_unacked` | `rabbitmq_queue_messages_unacked{queue="ReserveStock"}` | Order.API | rabbitmq_prometheus (tarefa 5) | `expectedValue` ≈ 0 em regime | desvio absoluto: 0\|1–5\|6–20\|>20 mensagens não confirmadas | Não |
| Latência externa do gateway | `norn_shop_payments_gateway_latency_ms` | `histogram_quantile(0.99, sum by (le) (rate(norn_shop_payments_gateway_latency_ms_bucket[1m])))` | Payment.API | OTLP (métrica de negócio, Fase 3 tarefa 8) | SLO = 500 ms | distância=obs/slo: <50%\|50–80%\|80–100%\|≥100% | **Sim** |
| Taxa de 5xx | `http_server_request_duration_seconds_count` | `sum(rate(http_server_request_duration_seconds_count{exported_job="<serviço>", http_response_status_code=~"5.."}[30s])) / sum(rate(http_server_request_duration_seconds_count{exported_job="<serviço>"}[30s]))` | Catalog\|Order\|Payment | OTLP | SLO = 1% (definição de onset, §3) | distância=obs/slo: <50%\|50–80%\|80–100%\|≥100% (= onset) | **Sim** |
| `errorsByType` | `norn_app_errors_total` | `norn_app_errors_total{exported_job="<serviço>", exception_type="<tipo>"}` — sempre sobre a série crua (exemplar) | Catalog\|Order\|Payment | OTLP (ADR-10, tarefa 5a; `IExceptionHandler` compartilhado) | sem SLO — contagem | desvio de taxa vs. `expectedValue` (piloto): <25%\|25–100%\|100–300%\|≥300% | **Sim** |
| Requisições ativas | `http_server_active_requests` | `http_server_active_requests{exported_job="Norn.Shop.Order.API"}` | Order.API | OTLP | `expectedValue` = concorrência em regime | desvio: <50%\|50–150%\|150–300%\|≥300% | Não |
| Fila de thread pool | `dotnet_thread_pool_queue_length_total` | `dotnet_thread_pool_queue_length_total{exported_job="Norn.Shop.Order.API"}` | Order.API | OTLP | `expectedValue` ≈ 0 em regime | 0\|1–10\|11–50\|>50 itens | Não |
| CPU de processo | `dotnet_process_cpu_time_seconds_total` | `rate(dotnet_process_cpu_time_seconds_total{exported_job="<serviço>"}[1m])` | Catalog\|Order\|Payment | OTLP | informativo — sem SLO próprio | n/a | Não |
| Pedidos criados | `norn_shop_orders_created_total` | `rate(norn_shop_orders_created_total[1m])` | Order.API | OTLP (Fase 3, tarefa 8) | volume de negócio — sem SLO | n/a | Não |
| Mudança de status do pedido | `norn_shop_orders_status_changed_total` | `sum by (status) (rate(norn_shop_orders_status_changed_total[1m]))` | Order.API | OTLP | volume de negócio — sem SLO | n/a | Não |
| Produtos criados | `norn_shop_catalog_products_created_total` | `rate(norn_shop_catalog_products_created_total[1m])` | Catalog.API | OTLP (Fase 2, tarefa 7) | volume de negócio — sem SLO | n/a | Não |
| Reservas de estoque | `norn_shop_catalog_stock_reservations_total` | `sum by (outcome) (rate(norn_shop_catalog_stock_reservations_total[1m]))` | Catalog.API | OTLP | volume de negócio — sem SLO | n/a | Não |
| Pagamentos processados | `norn_shop_payments_processed_total` | `sum by (outcome) (rate(norn_shop_payments_processed_total[1m]))` | Payment.API | OTLP (Fase 3, tarefa 8) | volume de negócio — sem SLO | n/a | Não |
| Pagamentos degradados | `norn_shop_payments_degraded_total` | `rate(norn_shop_payments_degraded_total[1m])` | Payment.API | OTLP | conta ativações de `payment.gateway.bypass` (§5.4/§5.7) — sem SLO | n/a | Não |
| Memória de container | `container_memory_working_set_bytes` | `container_memory_working_set_bytes{namespace="norn-shop"}` | Catalog\|Order\|Payment (Pods) | **cAdvisor** — pendente de cluster (Fase 6, tarefa 5a) | overhead do Norn (§3) — sem SLO próprio | n/a | Não |
| CPU de container | `container_cpu_usage_seconds_total` | `rate(container_cpu_usage_seconds_total{namespace="norn-shop"}[1m])` | Catalog\|Order\|Payment (Pods) | **cAdvisor** — pendente de cluster (Fase 6, tarefa 5a) | overhead do Norn (§3) — sem SLO próprio | n/a | Não |
| Eventos de OOM | `container_oom_events_total` | `increase(container_oom_events_total{namespace="norn-shop"}[1m])` | Catalog.API (Pod, alvo de F1) | **cAdvisor** — pendente de cluster (Fase 6, tarefa 5a) | define onset do F1 junto com a taxa de 5xx (§3) | binário: 0 = sem evento; ≥1 = onset | Não — usado no rotulador, não no `RuleEngine` |

**Linhas de fonte cAdvisor** (as três últimas) estão escritas e **pendentes de cluster** por
desenho — o DoD desta fase declara essa exceção explicitamente; validam-se no DoD da Fase 6.

## 3. Exemplars métrica → trace — validado ponta a ponta em 15/09/2026

Os quatro pontos da tarefa 3 (§8, Fase 4) checados rodando os três serviços do Shop localmente:

| Ponto | Estado | Evidência |
|---|---|---|
| SDK .NET | `SetExemplarFilter(ExemplarFilterType.TraceBased)` já presente (Fase 1) | exemplar aparece em `query_exemplars` do Prometheus |
| Collector | exporter `prometheus` (scrape/pull) com `enable_open_metrics: true` — **não** `prometheusremotewrite`, decisão já tomada na Fase 1 (arquitetura pull, sem `--web.enable-remote-write-receiver` no Prometheus) | `http_server_request_duration_seconds_bucket` carrega exemplar com `trace_id` |
| Prometheus | `--enable-feature=exemplar-storage` (sem remote-write-receiver, por não ser necessário no modelo pull) | `query_exemplars` retorna `{"span_id":..., "trace_id":...}` |
| Grafana | `exemplarTraceIdDestinations` com `name: trace_id` já configurado (`datasources.yaml`) | clique no exemplar resolve `trace_id` no datasource Tempo |

**Trace navegável confirmado:** `GET /api/traces/<trace_id>` no Tempo (porta 3200) retornou o span
correspondente ao `trace_id` de um exemplar de `http_server_request_duration_seconds_bucket`.

**Achado que confirma a nota do plano sobre amostragem dupla.** O SDK .NET amostra a 100%
(`Otel:TracesSamplingRatio = 1.0` em todo `appsettings.json` do Shop), mas o **Collector** reamostra
de novo via `probabilistic_sampler`, cujo padrão é `TRACE_SAMPLING_PERCENTAGE=10` — a amostragem "de
campanha" do §7.5. Rodando com o padrão, a maioria dos `trace_id` de exemplar **não** é encontrada no
Tempo (span descartado no Collector, não no SDK). A verificação ponta a ponta só fechou depois de subir
o Collector com `TRACE_SAMPLING_PERCENTAGE=100` — exatamente o modo "demonstração" do plano. **Em
campanha (10%), a navegação por exemplar é best-effort por desenho, nunca crítica** — nenhuma métrica
do experimento depende de trace (ADR-10); só a defesa usa o clique.

**Duas limitações já documentadas no plano, confirmadas na prática:**
- Consulta agregada (`sum`, `rate`, `avg`) sobre a série crua descarta exemplars — os dois painéis do
  Grafana que os exibem (`shop-overview.json`, painel 4) consultam `norn_app_errors_total` sem agregação.
- `norn_app_errors_total` é counter (reservoir de 1 por série) — para séries de alta cardinalidade de
  status/rota, prefira ler o exemplar pelo histograma de latência (`http_server_request_duration_seconds_bucket`),
  que tem um exemplar por bucket.

## 4. `norn_app_errors_total` — validação e lista fechada de `exception_type`

Implementado em `Norn.BuildingBlocks.Web.Errors.AppErrorMetrics`, chamado de dentro do
`NornExceptionHandler` compartilhado pelas três APIs do Shop (tarefa 5a). Lista fechada — tipo fora
dela cai em `"Other"`, nunca vira rótulo novo:

| `exception_type` esperado | Cenário típico |
|---|---|
| `NpgsqlException` | Falha de conexão/timeout do Postgres |
| `DbUpdateException` | Conflito de escrita no EF Core |
| `TimeoutException` | Timeout genérico (HTTP, pool) |
| `OperationCanceledException` | Cancelamento por `CancellationToken` |
| `TaskCanceledException` | Timeout de `HttpClient` (subclasse de `OperationCanceledException`, listada à parte porque é o tipo concreto observado) |
| `HttpRequestException` | Falha de rede para o gateway externo (F3) |
| `InvalidOperationException` | Estado inválido — inclusive falha transitória do EF Core sem `EnableRetryOnFailure` |
| `InvalidPaymentTransitionException` | Transição de estado inválida no domínio de pagamento |
| `Other` | Qualquer tipo fora da lista — bucket de contenção de cardinalidade |

**Validado rodando o Catalog.API com o Postgres parado**: a chamada a `POST /api/v1/products` retornou
500 pelo `NornExceptionHandler`, e `norn_app_errors_total{exported_job="Norn.Shop.Catalog.API",
exception_type="InvalidOperationException"}` apareceu no Prometheus após o próximo ciclo de export
(~60s, intervalo padrão do `PeriodicExportingMetricReader`).

## 5. Contagem de séries distintas — linha de base

`count({__name__=~".+"})` em 15/09/2026, com os três serviços do Shop rodando localmente (fora de
cluster), infra mínima exercitada (um produto, um pedido completo até `Confirmed`, uma falha forçada
de banco): **3.011 séries**.

Este número **não** é o teto da campanha — nasce de três processos e tráfego mínimo, sem os até 11
Pods da Fase 6 nem o volume do gerador de carga. Serve como linha de base para detectar salto de
cardinalidade mais tarde: repetir a mesma consulta ao final da Fase 6 (com cluster) e de novo no
piloto da Fase 12, comparando contra este valor.

## 6. Retenção do Prometheus (tarefa 8)

`--storage.tsdb.retention.time=15d` **e** `--storage.tsdb.retention.size=3GB` (`compose.otel.yaml`) —
os dois limites coexistem, o que disparar primeiro vence. Tempo cobre semanas de desenvolvimento entre
esta fase e a campanha (Fase 12); o teto de tamanho é a salvaguarda de disco, dentro do orçamento de
3–6 GB para Prometheus + Tempo somados (Fase 00, ADR-09). Reavaliar no piloto da Fase 12 se a
cardinalidade real da campanha (11 Pods, scrape de 5s, ~20h de execução) aproximar o teto antes do
prazo — não descoberto, só reservado.
