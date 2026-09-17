# Contrato da Norn.API (Fase 10 + ajustes da Fase 11)

> Registro do contrato real da Norn.API tal como ela existe hoje — é a referência que o dashboard
> (`web/norn-dashboard/`) usa para gerar tipos e schemas Zod, em vez de reconferir a API do zero a
> cada sessão. Rotas prefixadas por `/api/v1` (`MapApiVersion(1)`, sem pacote de versionamento).

## 1. Serialização

Desde a Fase 11, **todo enum sai como string camelCase**, tanto em REST quanto no evento do
SignalR — `ConfigureHttpJsonOptions` registra `JsonStringEnumConverter` globalmente em
`Program.cs`, e `Norn.Worker.Events.PlatformEventPublisher` já fazia o mesmo do lado do canal
`norn:events`. Antes da Fase 11 os dois canais divergiam (REST em inteiro, SignalR em string) —
corrigido, não há mais dois decoders de enum no front.

## 2. Endpoints REST

| Método | Rota | Query/Body | Resposta 200 | Notas |
|---|---|---|---|---|
| GET | `/api/v1/topology` | — | `TopologyInfo[]` | |
| GET | `/api/v1/signals` | `limit` (1–500, default 50), `experimentRunId?` | `AnomalySignal[]` | |
| GET | `/api/v1/plans` | `limit` (1–500, default 50) | `HealingPlan[]` | |
| GET | `/api/v1/outcomes` | `limit` (1–500, default 50) | `HealingOutcome[]` | |
| GET | `/api/v1/mode` | — | `{ mode: PlatformMode }` | |
| PUT | `/api/v1/mode` | body `{ mode: PlatformMode }` | `{ mode: PlatformMode }` | **Nunca publica em `norn:events`** — o dashboard só vê a mudança via `ModeChanged` do polling do Worker, não como resposta desta chamada |
| GET | `/api/v1/experiments/{runId:guid}` | — | `ExperimentRunSummary` | 404 se desconhecido |
| GET | `/api/v1/metrics/series` | `metricName`, `service`, `fromUtc`, `toUtc` (janela ≤ 1h) | `MetricSample[]` | Novo na Fase 11 — ver §5 |

Validação inválida → `400` com `ProblemDetails` (`Results.ValidationProblem`). Exceção não tratada
→ `500` `ProblemDetails` via `NornExceptionHandler`.

## 3. Hub SignalR

Rota: `/hubs/norn`. Push-only (`Hub<INornHubClient>`, sem método invocável pelo cliente). Seis
métodos, um por `eventType` do envelope publicado em `norn:events` (§5.6 do plano mestre):

| `eventType` | Método do hub | Payload real |
|---|---|---|
| `SignalDetected` | `SignalDetected(payload)` | `AnomalySignal` |
| `PlanCreated` | `PlanCreated(payload)` | `HealingPlan` |
| `ActionApplied` | `ActionApplied(payload)` | `HealingOutcome` (**mesmo objeto** de `OutcomeVerified`) |
| `OutcomeVerified` | `OutcomeVerified(payload)` | `HealingOutcome` |
| `ModeChanged` | `ModeChanged(payload)` | `PlatformMode` (enum puro, sem envelope de objeto) |
| `TopologyUpdated` | `TopologyUpdated(payload)` | `TopologyInfo` (**objeto único**, não lista — diferente de `GET /topology`) |

`correlationId` do envelope é o `AnomalyContext.CorrelationId` para os quatro primeiros;
`SignalDetected` carrega o `signalId` (contexto ainda não existe); `ModeChanged` carrega um `Guid`
novo a cada troca (sem semântica de correlação).

Entrega é best-effort, sem durabilidade — reidratar por REST ao conectar/reconectar é obrigatório
(ADR-15), não polimento.

## 4. Formas principais

Ver `src/Platform/Norn.Contracts/*.cs` para os records completos (`AnomalySignal`, `HealingPlan`,
`HealingOutcome`, `TopologyInfo`, `ServiceTarget`, `RecentMetrics`, `LlmTrace`, `HealingAction`,
`ExperimentRunSummary`, `MetricSample`). Enums fechados: `DetectorType`, `Severity`,
`HealingActionType` (`ScaleUp`\|`RestartPod`\|`ToggleFeatureFlag`\|`NoOp`), `HealingOutcomeStatus`,
`DecidedBy`, `PlatformMode` (`Observe`\|`DryRun`\|`Active`).

## 5. `GET /api/v1/metrics/series` — novo na Fase 11

Não existia na Fase 10 (só snapshots/listas, nenhuma série temporal). Criado para a feature
`metrics` do dashboard (gráfico Recharts). Implementação própria em
`Norn.API/Infrastructure/Prometheus/PrometheusMetricSource.cs`, adaptador de
`Norn.Contracts.Ports.IMetricSource` — **não referencia `Norn.Monitor`** (ADR-17: Norn.API só
referencia `Norn.Contracts`/`Norn.Knowledge`), então é uma implementação HTTP crua menor,
irmã da de `Norn.Monitor.Prometheus.PrometheusMetricSource`, não a mesma classe.

- Query monta `{metricName}{exported_job="{service}"}` e chama `query_range` do Prometheus
  (`Prometheus:BaseUrl` em `appsettings.json`, default `http://localhost:9090`).
- `metricName`/`service` validados por regex antes de entrar na query PromQL (nunca escapados —
  a validação de formato é a única defesa contra injeção).
- **Sem marcação de instante de sinal/ação no backend** — a resposta é só `{ timestampUtc, value,
  metricName, target, labels }[]`. O dashboard já tem `AnomalySignal.DetectedAtUtc`/
  `HealingOutcome.AppliedAtUtc` de outras chamadas e desenha `ReferenceLine`/`ReferenceDot` no
  Recharts do lado do cliente.

## 6. CORS e estático

Sem CORS fora de `Development` (só `http://localhost:5173`, dev server do Vite) — em produção o
dashboard é servido pela própria Norn.API (`UseStaticFiles()` + `MapFallbackToFile("index.html")`,
mesma origem, CLAUDE.md). `wwwroot/dist` **não é commitado** — ver residual de empacotamento no
`CLAUDE.md`, "Estado atual" da Fase 11.
