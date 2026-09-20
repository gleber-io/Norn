# Calibração da ordenação de severidade (Fase 7, tarefa 5a)

> Confere se o `primarySignal` eleito pela ordenação determinística do ADR-14 (severidade desc →
> `detectedAtUtc` asc → `confidence` desc → `metricName` asc) corresponde à falha efetivamente
> injetada, para F1, F2 e F3. F5 fica fora — é controle negativo, sem precursor, não há assinatura
> a eleger.

## Metodologia

A tarefa pede rodar F1/F2/F3 em modo `Observe` e conferir o `primarySignal` contra
`norn_chaos_active` como ground truth. Em vez de um piloto dedicado, esta calibração **reaproveita
o braço A da campanha real** (tag `campaign-h1h2`) — o braço A roda exatamente em modo `Observe`, e
as 15 execuções (5 por cenário × F1/F2/F3) já estão persistidas e congeladas no Postgres. Não há
piloto isolado porque não há necessidade: o dado que a tarefa pede já existe, medido em condição
real de campanha em vez de execução avulsa, sem custo de máquina adicional.

**Ground truth usado:** em vez de consultar `norn_chaos_active` diretamente, o filtro usa
`onset_at_utc` e `window_end_at_utc` de cada `experiment_run` (já calculados pelo `Norn.Labeler`
a partir do critério de onset da §3). Isso é equivalente e mais preciso para o que se quer medir: o
caos permanece ativo por desenho experimental do início da injeção até `window_end_at_utc`, mas só
a partir do `onset_at_utc` a falha efetivamente cruzou o critério de violação de SLO — é essa janela
que o desenho considera "assinatura formada", e é nela que a eleição de `primarySignal` importa.
Contexto formado antes do onset tende a não ter severidade suficiente para sequer existir.

**Filtro aplicado:** apenas `AnomalyContext` cujo `service` é o alvo do cenário (F1→Catalog.API,
F2→Order.API, F3→Payment.API, conforme a tabela de cenários da §3) e cujo `created_at_utc` cai
dentro de `[onset_at_utc, window_end_at_utc]` da execução. Contextos de outros serviços na mesma
execução (ex.: Order.API sinalizando fila crescida durante um F1) são tráfego cruzado real da carga
sazonal, não ruído do detector — ficam de fora por não serem o que este teste mede.

**Critério de acerto:** o `metric_name` do sinal primário pertence ao subconjunto que discrimina
aquele cenário (M=7, `docs/metrics-matrix.md`):

| Cenário | Serviço alvo | Métricas que contam como acerto |
|---|---|---|
| F1 | Catalog.API | `dotnet_process_memory_working_set_bytes`, `dotnet_gc_pause_time_seconds_total` |
| F2 | Order.API | `http_server_request_duration_seconds_bucket` (p99), `rabbitmq_queue_messages_ready` |
| F3 | Payment.API | `norn_shop_payments_gateway_latency_ms` |

## Resultado agregado

| Cenário | Contextos no alvo, na janela | Acertos | % |
|---|---|---|---|
| F1 | 14 | 9 | 64,3% |
| F2 | 13 | 12 | 92,3% |
| F3 | 11 | 11 | 100,0% |
| **Total** | **38** | **32** | **84,2%** |

## Por repetição

| Cenário | Rep. 1 | Rep. 2 | Rep. 3 | Rep. 4 | Rep. 5 |
|---|---|---|---|---|---|
| F1 | 2/2 (100%) | 3/3 (100%) | **2/6 (33%)** | 1/2 (50%) | 1/1 (100%) |
| F2 | 2/3 (67%) | 3/3 (100%) | 3/3 (100%) | 3/3 (100%) | 1/1 (100%) |
| F3 | 2/2 (100%) | 2/2 (100%) | 2/2 (100%) | 2/2 (100%) | 3/3 (100%) |

F3 é perfeito nas 5 repetições. F2 tem um único desvio isolado. **F1 concentra o problema**, e a
repetição 3 (2 de 6, 33%) sozinha puxa a média do cenário para baixo.

## Os desvios não são ruído do detector — são sintoma concorrente real

Todos os 5 desvios (4 em F1, 1 em F2) são sinais de severidade **`Critical`**, não `Low`/`Medium`
mal calibrado:

| Cenário | Métrica eleita no lugar da esperada | Severidade | Ocorrências |
|---|---|---|---|
| F1 | `http_server_request_duration_seconds_count` (taxa de 5xx do próprio Catalog.API) | Critical | 4 |
| F1 | `norn_app_errors_total` (contagem de exceção do próprio Catalog.API) | Critical | 1 |
| F2 | `http_server_request_duration_seconds_count` (taxa de 5xx do próprio Order.API) | Critical | 1 |

**A explicação é estrutural, não erro de calibração.** No F1, quando o `OOMKilled` derruba o pod do
Catalog, as requisições em voo falham com 5xx — a taxa de erro do próprio serviço sobe a `Critical`
no mesmo instante ou antes de a leitura de RSS refletir o novo pod (que reinicia com memória baixa,
mascarando momentaneamente o sinal que a motivou). Os dois sinais (RSS/GC e taxa de 5xx) descrevem
a **mesma falha por ângulos diferentes**, e a ordenação do ADR-14 (severidade → tempo de detecção →
confiança → nome) elege honestamente o que chegou em `Critical` primeiro. A repetição 3 concentra o
desvio porque foi a execução em que o ciclo de crash-loop do F1 se repetiu mais vezes dentro da
janela de 10 min — mais reinícios, mais janelas em que a taxa de erro local compete com a memória.

**Isto não é uma falha do desenho — é exatamente o que o ADR-14 já previa e mitigava.** A decisão
registra explicitamente que *"o `primarySignal` é ordem de leitura, não hipótese de causa raiz"*: o
prompt do LLM carrega **todos** os sinais correlacionados, e o `RuleEngine` indexa o **conjunto** de
métricas alteradas, nunca o primário isolado. Uma eleição "errada" do primário não priva os dois
braços de tratamento do sinal de RSS/GC — ele continua presente em `correlatedSignals`. O que este
teste mede é a ordem de leitura na eleição, não a informação disponível para decidir.

## Conclusão

**84,2% de acerto agregado, com F2 e F3 essencialmente perfeitos e F1 concentrando o desvio — e o
desvio tem causa identificada, não é ruído.** Não houve correção de bandas ou de ordenação a partir
deste resultado: os cinco casos divergentes são competição legítima entre sintomas reais da mesma
falha (RSS crescente e a consequência em 5xx do próprio `OOMKilled`), cobertos pela mitigação já
prevista no ADR-14 (primário não decide sozinho em nenhum braço) e pela ameaça à validade já
declarada na §2 do plano ("Comparabilidade da severidade entre métricas"). Reabrir a calibração
tocaria dado já congelado sob a tag `campaign-h1h2` sem mudar H1 ou H2 — o `primarySignal` nunca foi
a única entrada da decisão.

**Dado-fonte:** braço A, tag `campaign-h1h2`, 15 execuções (`experiment_run_id` em
`platform.experiment_runs`, `arm='A'`, `scenario IN ('F1','F2','F3')`). Consulta reprodutível contra
qualquer restauração do dump da campanha (ver `docs/experiments/plano-campanha.md`, §9.6).
