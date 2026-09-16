# Tabela de regras — `RuleEngine` (Fase 8, braço C)

> Golden do teste exaustivo (`RuleEngineExhaustiveTests`, tarefa 9a). Alterar o motor
> (`Norn.Planner.RuleEngine.RuleEngine.DecideActionType`) sem atualizar este arquivo é o diff que a
> revisão precisa notar.

## Domínio

Assinatura fechada M = 7 (`docs/metrics-matrix.md`, seção 1) — o `RuleEngine` indexa **o conjunto de
métricas alteradas no contexto** (primário + correlacionados), nunca o `primarySignal` isolado
(ADR-14, senão a ordenação do Analyzer decidiria pelo braço C). Domínio: 2⁷ = 128 subconjuntos ×
4 bandas de severidade (`Severity.Low|Medium|High|Critical`).

| # | Métrica | Constante em código | Cenário |
|---|---|---|---|
| 1 | RSS | `dotnet_process_memory_working_set_bytes` | F1 |
| 2 | Tempo de GC | `dotnet_gc_pause_time_seconds_total` | F1 |
| 3 | Latência p99 | `http_server_request_duration_seconds_bucket` | F2 |
| 4 | Profundidade de fila | `rabbitmq_queue_messages_ready` | F2 |
| 5 | Latência do gateway | `norn_shop_payments_gateway_latency_ms` | F3 |
| 6 | Taxa de 5xx | `http_server_request_duration_seconds_count` | F3 / onset geral |
| 7 | `errorsByType` | `norn_app_errors_total` | geral (ADR-10) — nunca gatilho isolado |

## Banda de severidade de entrada

O domínio do plano mestre é "(conjunto de métricas alteradas, banda de severidade)" sem amarrar de
onde vem a banda. Decisão de implementação (Fase 8): **usa-se `context.PrimarySignal.Severity`** — a
ordenação do ADR-14 já coloca o primário como o de maior severidade do contexto (severidade desc é a
primeira chave de ordenação), então é literalmente a maior banda presente no conjunto, sem introduzir
uma segunda heurística de agregação.

## Regra de prioridade

Determinística, avaliada nesta ordem — não é uma tabela de 128 linhas escritas à mão, é a mesma regra
aplicada a qualquer subconjunto:

1. **Severidade `Low` → sempre `NoOp`.** O `RuleEngine` não age abaixo de `Medium`, em nenhuma
   assinatura. É o que dá conteúdo à fronteira testada na tarefa 9b (logo abaixo/no/logo acima do
   limiar `Low`↔`Medium`).
2. **Assinatura contém RSS ou tempo de GC (F1) → `RestartPod`.** Prioridade máxima: é a única ação
   irreversível do catálogo, e uma assinatura ambígua (que também contenha marcadores de F2/F3) deve
   resolver para a ação mais decisiva, não para uma reversível que deixaria o vazamento de memória
   sem tratamento.
3. **Senão, assinatura contém latência p99 ou profundidade de fila (F2) → `ScaleUp`.**
4. **Senão, assinatura contém latência do gateway ou taxa de 5xx (F3) → `ToggleFeatureFlag`.**
5. **Senão (inclusive `errorsByType` sozinho, sem nenhum dos discriminadores acima) → `NoOp`.**
   `errorsByType` é sinal de contagem geral (ADR-10), nunca gatilho de cenário próprio.

## Linhas canônicas (DoD da Fase 8)

| Assinatura | Severidade | Ação |
|---|---|---|
| F1 = {RSS, tempo de GC} | ≥ Medium | `RestartPod` |
| F2 = {latência p99, profundidade de fila} | ≥ Medium | `ScaleUp` |
| F3 = {latência do gateway, taxa de 5xx} | ≥ Medium | `ToggleFeatureFlag` |
| F5 = ∅ (nenhuma métrica alterada) | qualquer | `NoOp` |
| qualquer subconjunto, severidade `Low` | `Low` | `NoOp` |

## Asserções globais (tarefa 9a)

- **Totalidade:** todo um dos 128 subconjuntos × 4 bandas produz exatamente um `HealingActionType`
  do enum fechado — nenhum branch default silencioso, nenhuma exceção.
- **Nenhuma regra morta:** as quatro ações do catálogo (`ScaleUp`, `RestartPod`, `ToggleFeatureFlag`,
  `NoOp`) são alcançáveis por ao menos uma assinatura — trivialmente verdadeiro pelas linhas canônicas
  acima, mas o teste confere de verdade, não assume.

## Fora do `RuleEngine`

O resultado desta tabela é só o tipo de ação. Barreiras do ADR-04 (cooldown, máximo de ações por
janela) e pré-condições do §5.4 (soma de réplicas, UID do pod, catálogo de flags) são checadas depois,
por `Norn.Planner.Barriers.HealingActionPreconditionChecker` — a mesma checagem usada na validação da
saída do LLM (§5.5, passo 4). Uma ação que esta tabela escolhe pode ainda assim não sobreviver e virar
`NoOp`/`Fallback` — isso é esperado e coberto pela tarefa 9c, em arquivo de teste próprio.
