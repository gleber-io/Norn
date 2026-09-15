# ADR-10 — Observabilidade reduzida: sem agregação de logs

**Status:** aceito.

## Contexto
A stack completa (Collector, Prometheus, Loki, Tempo, Grafana) não cabe confortavelmente no orçamento de memória e disco de uma máquina de 16 GB executando simultaneamente cluster, infraestrutura e carga.

## Decisão
Manter **Collector, Prometheus, Tempo e Grafana**; **remover Loki**. Logs ficam em console/arquivo, consultáveis via `kubectl logs`. Traces com amostragem de 10% na campanha e 100% em demonstração.

## Consequência
`AnomalyContext.recentLogPatterns` perde fonte e é **substituído por `recentMetrics.errorsByType`** — sinal derivado de log na forma **contada**, via `norn_app_errors_total{service, exception_type}` emitido pelo `IExceptionHandler` (§7.1). Ganho colateral: `ConnectionPoolExhausted` vira discriminador direto do F2, tornando o par F2×F3 mais nítido para H2.

**Fora de escopo, com estas palavras exatas na monografia:** agregação centralizada, busca textual e mineração de templates de log — não "sem logs". A latência do loop é medida por timestamps persistidos no Knowledge, não por traces (a amostragem de 10% descartaria a execução de interesse); o Tempo fica para inspeção qualitativa e demonstração de correlação métrica→trace na defesa.
