# Estabilidade do braço B — LLM (Fase 8, tarefa 11)

Medido em 16/09/2026 contra o `norn-qwen` real (`qwen3:4b-instruct-2507-q4_K_M` derivado, `num_ctx=8192`,
`temperature=0`, `seed=42` — Modelfile da Fase 0), 100% residente em VRAM, Ollama nativo no Windows
(ADR-09). 20 execuções de `LlmPlanner.DecideAsync` sobre o **mesmo** `AnomalyContext`, sem nenhuma
mudança de estado entre chamadas.

## Procedência do contexto

`AnomalyContext` real, capturado ao vivo (não montado à mão): sessão de replay do cenário F3 contra o
`Norn.Worker` em `Observe`, com `Norn.Shop.Payment.API` rodando localmente (ver nota abaixo sobre por
quê) e o middleware de caos do ADR-13 ativado. O contexto é a **assinatura ambígua do F3**, exigida
pelo plano mestre para este teste — estabilidade em caso óbvio não informa nada:

- `topology.service`: `Norn.Shop.Payment.API`
- `primarySignal.metricName`: `norn_shop_payments_gateway_latency_ms`
- `primarySignal.severity`: `Critical` (observado ~4770ms contra SLO de 500ms)
- Nenhum sinal de taxa de 5xx correlacionado (`recentMetrics.errorRatePct = 0`) — a ambiguidade aqui é
  "latência isolada do gateway", que tanto o `RuleEngine` quanto o LLM precisam reconhecer como F3
  (e não confundir com um problema de capacidade genérico, que seria F2)

Fixture versionada em `tests/Platform/Norn.Planner.UnitTests/Fixtures/f3-gateway-latency-context.json`.
`context_hash` afirmado em `F3ContextFixtureTests.Load_ReturnsContext_WhoseCanonicalHash_MatchesRecordedContextHash`
— recarregar o arquivo e recalcular pela mesma função canônica (`CanonicalJson.ComputeHash`) reproduz
exatamente `e0ce576bb389cf111b56b7cf51d2dc3e0bcb00d840c92e0014695e2c9f5e304f`, o valor gravado em
`anomaly_contexts.context_hash` no momento da captura — a garantia de que "o mesmo contexto" é fato
verificado, não suposição.

**Nota sobre o ambiente da captura.** O cluster k3d real forneceu a topologia (réplicas, limites de
recurso do Deployment `payment-api`). O tráfego que gerou o sinal veio de uma instância local do
`Norn.Shop.Payment.API` (fora do container), não do pod em si — necessário porque a Fase 8 encontrou e
corrigiu, nesta mesma sessão, um defeito de fiação do F3 (o delay do caos vivia só no pipeline HTTP,
inatingível pelo tráfego real assíncrono de Order→RabbitMQ→Payment, e fora da janela que a métrica de
negócio cronometra — ver `IChaosGatewayDelay`) e reconstruir a imagem Docker do Payment.API não foi
possível na hora por uma falha de rede do Docker Desktop com o registry. O sinal capturado é real
(métrica de negócio de verdade, gerada por chamadas HTTP de verdade ao `SimulatedPaymentGateway`); só a
topologia de execução do processo que o gerou é híbrida. Isso não compromete a validade do contexto
para este teste, que mede a decisão do Planner sobre um `AnomalyContext` já persistido — o Planner não
tem visibilidade de onde a métrica se originou.

## Resultado — as 20 execuções

| Nível medido | Resultado |
|---|---|
| **Ação escolhida** | `NoOp` em 20/20 execuções |
| **Parâmetros da ação** | Idênticos em 20/20 (`reason` byte-a-byte igual) |
| **Rationale** (texto livre) | Idêntico em 20/20 — 1 texto distinto entre as 20 respostas |
| **`decidedBy`** | `Llm` em 20/20 — nenhuma queda para `RuleEngine`/`Fallback`, nenhuma tentativa de reparo |
| **Confidence** | `95` em 20/20 |
| Latência (`llmTrace.latencyMs`) | mín. 4930 ms · máx. 7384 ms (1ª chamada, custo de *cold start* do slot) · média 5068 ms |

**Estabilidade de 20/20 nos três níveis** — inclusive no rationale, o termômetro mais sensível. Com
`temperature=0`, `seed=42` e `num_ctx=8192` fixados por Modelfile (§5.5), a inferência em GPU não é
bit-reproduzível por garantia teórica, mas na prática, para este contexto e este hardware, o resultado
foi determinístico nas 20 repetições.

## Leitura para H2 — o resultado que importa declarar

O `LlmPlanner` escolheu **`NoOp`** nas 20 execuções; o `RuleEngine` (braço C), sobre a mesma
assinatura (`norn_shop_payments_gateway_latency_ms` isolado, severidade `Critical`), decide
**`ToggleFeatureFlag`** (docs/rule-table.md — F3 presente, sem nenhum outro discriminador). **Os dois
braços divergem nesta assinatura, de forma estável dos dois lados** — não é ruído de um decisor
instável perdendo para o outro por sorte; é uma diferença de critério repetível.

O rationale do LLM (idêntico nas 20 respostas) explica a divergência: o modelo justifica a abstenção
por "ausência de pod específico" e "sem evidência de falha no nível do pod", tratando a falta de
`primarySignal.target.pod`/`podUid` como impeditivo — mesmo o catálogo enviado no prompt descrevendo
`ToggleFeatureFlag` como a ação ligada a `flagName`/`value`, sem exigir pod algum. Isto é dado, não
defeito do teste: sugere que o modelo pesa a ausência de identificação de pod mais do que deveria para
uma ação que não depende dela, uma hipótese concreta para a leitura qualitativa de H2 na monografia —
"o LLM decidiu diferente da regra" e "por quê" ficam registrados juntos, não como número solto.

**Concordância bruta entre braços nesta assinatura: 0/1** (divergem). Isso é exatamente o tipo de dado
que a tarefa 4a da Fase 12 (recálculo pareado do `RuleEngine` sobre os contextos do braço B) precisa
agregar em escala — aqui é só o primeiro ponto, coletado com procedência verificada.

## Reprodutibilidade

Script de execução não faz parte da suíte automatizada (chama o Ollama real 20 vezes; a regra do
projeto é nenhum teste do Planner tocar a rede) — é o mesmo `LlmPlanner` de produção, instanciado
manualmente sobre a fixture acima, sem Semantic Kernel nem contêiner. Reexecutável por qualquer um que
tenha `norn-qwen` residente e o Postgres/Redis fora do caminho (o `LlmPlanner` não os usa — só o
`IChatClient` e a fixture).

## Pendência

Tarefa 11a (sensibilidade ao *thinking mode*, `qwen3:4b-thinking-2507-q4_K_M`) é **opcional** e não foi
executada — não sustenta nenhum resultado obrigatório da Fase 8.
