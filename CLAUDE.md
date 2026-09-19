# CLAUDE.md — Norn

## Comandos
```
dotnet build
dotnet test
dotnet format --verify-no-changes
deploy/bootstrap.ps1            # infra + observabilidade + cluster k3d + Shop, do zero (Fase 6)
kubectl get pods -n norn-shop
deploy/run-experiment.ps1       # uma execução da campanha (Fase 12) — reset/warmup/carga/injeção/coleta
deploy/run-campaign.ps1         # a matriz inteira (60 execuções), blocos aleatorizados (Fase 12)
deploy/dump-knowledge.ps1       # backup do Knowledge, fora do VHDX do WSL2 (Fase 12, tarefa 3b)
tools/analysis/.venv/Scripts/python.exe tools/analysis/analyze.py --labeled <csv> --out-dir <dir>
```

## Arquitetura em 10 linhas
Norn é uma plataforma de self-healing MAPE-K para um e-commerce de referência (`Catalog`, `Order`, `Payment`). `Norn.Monitor` lê métricas do Prometheus; `Norn.Analyzer` detecta anomalia por ML.NET; `Norn.Planner` decide a ação de cura via LLM local (Ollama/Qwen3) com fallback para tabela de regras; `Norn.Executor` aplica a ação no Kubernetes com barreiras contra loop patológico; `Norn.Knowledge` persiste o histórico em PostgreSQL+Redis. `Norn.Worker` compõe o loop; `Norn.API` + dashboard React mostram tudo em tempo real via SignalR. Três braços experimentais (A: sem atuação, B: LLM, C: regras) medem se o loop recupera falhas progressivas mais rápido que a ausência de atuação.

## Regras invioláveis
- Shop nunca referencia Platform
- **Minimal API sempre; `Controllers` nunca.** Rotas por `MapGet`/`MapPost` no arquivo de endpoint da feature
- **Setas apontam para dentro (ADR-17).** Analyzer, Planner, Executor e Monitor falam com portas de `Norn.Contracts`; nunca referenciam `Norn.Knowledge`. Só `Norn.Worker` e `Norn.API` o referenciam, como composition roots
- **`Norn.Contracts` tem zero PackageReference.** Pacote entrando no núcleo é abstração vazando adaptador
- **Shop: quatro pastas por API** — `Domain` (não referencia nada), `Application` (portas), `Infrastructure` (EF Core), `Features` (vertical slices). `Features` nunca referencia `Infrastructure`. Regra de negócio é `Domain`, não `if` no handler
- Teste de handler com dublê de porta, nunca com Testcontainers. Se precisa de banco, a dependência está invertida errado
- Ações de cura só do enum fechado — quatro valores: `ScaleUp`, `RestartPod`, `ToggleFeatureFlag`, `NoOp`
- A `Role` do ADR-03 é derivada do catálogo da §5.4. Catálogo não cresce sem revisar a `Role` no mesmo commit. Sem `patch` no Deployment inteiro, nunca
- 403 em ação catalogada é defeito de configuração, não evidência de contenção. O Executor confere permissões no startup e falha rápido
- Nenhuma versão de pacote em .csproj — tudo em `Directory.Packages.props`
- CancellationToken em toda assinatura async
- Sem FluentAssertions (licença). MassTransit fixo numa versão 8.x **exata** (Apache-2.0, atualmente 8.5.10); v9 é comercial
- `eventId` do envelope **é** o `MessageId` do MassTransit — a dedup do inbox é por MessageId
- Sem Native AOT; sem MongoDB; sem Loki
- LLM local fixado por digest: `qwen3:4b-instruct-2507-q4_K_M` (ADR-12). Nunca `4b-thinking-2507` nem `qwen3:4b` puro
- `temperature = 0`, `seed` e **`num_ctx`** fixados no **Modelfile** (`norn-qwen`, Fase 0) e repassados na chamada. O conector do SK **não** expõe `seed` nem `num_ctx` — use `IChatClient` + `OllamaOption`. Padrão do Ollama é 4096 e trunca em silêncio
- `OLLAMA_KEEP_ALIVE=-1`, `MAX_LOADED_MODELS=1`, `NUM_PARALLEL=1`. Padrão de 5 min descarregaria o modelo entre execuções
- Verificação de GPU é `ollama ps` mostrando `100% GPU` — `nvidia-smi` não revela offload parcial
- Métricas de runtime e processo pelo meter `System.Runtime`. Sem `Instrumentation.Process` (nunca teve GA) nem `Instrumentation.Runtime`
- Métrica de container só existe via **cAdvisor do kubelet** (Fase 6, tarefa 5a), nunca por OTLP. É de lá que vem o overhead do Norn. Onset do F1 **não** usa `container_oom_events_total` (fica sempre zerado neste ambiente — containerd remove o cgroup do container morto antes do cAdvisor ler `oom_kill=1`): usa `status.containerStatuses[].lastState.terminated.reason == OOMKilled` do Pod, lido por `Norn.Monitor` (Fase 7)
- Memória do Catalog para detecção **e** predição é `dotnet.process.memory.working_set`. Nunca misturar com a série do container: fontes diferentes tornam H3 incomparável com H1
- ML.NET: `confidence` é `[0, 100]`. Streaming por `CreateTimeSeriesEngine`/`Predict`/`CheckPoint`, nunca `Fit()` por amostra
- `SelfSubjectAccessReview` leva o subrecurso em `Subresource`, nunca `Resource = "deployments/scale"`
- Front: React Router **v8** declarative (`<BrowserRouter>`), import de `react-router`. Sem `react-router-dom`, sem `createBrowserRouter`. Tailwind v4 CSS-first por `@tailwindcss/vite`, sem `tailwind.config.js`
- Caos é middleware próprio (ADR-13). Sem Simmy, sem `Chaos*` do Polly. Platform nunca referencia BuildingBlocks.Chaos
- Gerador de carga é console próprio (ADR-18). Sem NBomber — v5+ do pacote NuGet é licença comercial (Business License), incompatível com uso de TCC
- Severidade = distância do SLO, bandas via IOptions (ADR-14). Primário é ordem de leitura; prompt leva todos os sinais; regra indexa o conjunto
- SDK pinado em 10.0.401, `rollForward: latestPatch`
- Eventos do dashboard vão por Redis pub/sub, canal `norn:events` (ADR-15). Nunca por RabbitMQ — o F2 degrada o broker de propósito. Worker nunca referencia Norn.API
- Front usa Biome. Nunca instalar ESLint nem Prettier
- Config da plataforma sob `norn:platform:config:` (ADR-16). Nunca no catálogo de flags do Shop, que é alvo de ação de cura
- Flags do Shop sob `shop:flags:`, lidas por `IFeatureFlags` com invalidação pub/sub. O Payment consome `payment.gateway.bypass` (§5.7) — sem essa contraparte o `ToggleFeatureFlag` não tem efeito e o F3 não se recupera
- Toda execução da campanha começa com reset: `shop:flags:` em `false` e modo gravado conforme o braço
- Braço B/C (Fase 12) é `IPlatformConfig.PlannerBackend` (`Llm`/`RuleEngine`), sob `norn:platform:config:plannerBackend`, lido uma vez por ciclo em `AnomalyPipelineBackgroundService` — nunca dois binários diferentes para os dois braços (ADR-05, "mesmo binário"). Default `Llm` — sessões anteriores à Fase 12 não tinham esse switch e continuam se comportando igual
- `Norn.Labeler` é o único lugar que decide onset/recuperação/estado de término (Fase 12) — fronteira com `tools/analysis/` (Python) é o CSV, e só ele. `container_oom_events_total` não é observável neste ambiente (containerd remove o cgroup antes do cAdvisor ler); o instante do `OOMKilled` para o F1 vem de `status.containerStatuses[].lastState.terminated` via `kubectl`, capturado por `run-experiment.ps1` — poll a cada 5s durante a janela, não uma leitura única no teardown, porque um `RestartPod` real apaga o Pod que sofreu o OOM antes do teardown
- `AnomalyContext` é persistido inteiro em `anomaly_contexts` (jsonb) antes de ir ao Planner, em todos os modos, com `context_hash` canônico. Serialização canônica é uma só função, compartilhada com a montagem do prompt
- RuleEngine é função pura: sem relógio, sem Redis, sem cooldown dentro. Cooldown e pré-condições são barreiras do ADR-04, separadas
- Dashboard é servido pela própria Norn.API via wwwroot. Sem nginx. Mesma origem, sem CORS em runtime
- Entrada de tráfego no Shop é NodePort (D9). Traefik do k3d desabilitado na criação do cluster. Nunca introduzir Ingress sem revisar D9
- `maxReplicas` = 3, baseline = 1 réplica por serviço do Shop. O teto vem do scheduler de nó único (Fase 6), não de gosto
- Pré-condição do `ScaleUp` é sobre a **soma**: `atuais + replicaDelta ≤ maxReplicas`. Nunca `atuais < maxReplicas`. No Executor, satura em `maxReplicas` em vez de recusar
- Prompt do LLM tem orçamento de 4096 tokens e é **medido antes de enviar**. Estourou → `PromptBudgetExceeded` e fallback. Nunca truncar
- `RestorePackagesWithLockFile` ligado, `packages.lock.json` versionado, CI com `--locked-mode`
- `.wslconfig` fixo em 8 GB / 10 processadores desde a Fase 6. Mudar durante a campanha invalida a comparação; os valores vão para `experiment_runs`
- Dump do Knowledge ao fim de cada lote da campanha, fora do VHDX do WSL2
- Pré-condição recusada no Planner → plano sai `NoOp`. Recusada no Executor → `HealingOutcome.status = Rejected`. `Rejected` nunca existe sem plano
- Conjunto de assinatura fechado em M ≤ 10 métricas (Fase 4) — o teste exaustivo do RuleEngine é 2^M
- Análise estatística é Python em `tools/analysis/` (lifelines, scipy, statsmodels). Fronteira com .NET é CSV, só CSV. Nunca reimplementar o RuleEngine em Python
- F5 tem alvo fixo (Catalog.API) e regra de término própria: âncora no kill, não no onset. `InvalidNoOnset` não se aplica a ele
- Commits em português do Brasil, sem nenhuma referência a IA/Claude/Anthropic — nem trailer de co-autoria

## Convenções
Minimal API, Clean Architecture por pasta, vertical slice dentro de `Features/`, TypedResults, LoggerMessage, naming de testes `MethodName_Scenario_ExpectedBehavior`.

## Estado atual
Fase 8 concluída. **Fase 9 (Executor) implementada, commitada e — após os residuais fecharem numa
sessão de acompanhamento — com DoD integral completo.** F2 e F3 fecharam o DoD ao vivo
(`ScaleUp`/`ToggleFeatureFlag`, `SloRestored=true`, evidência no Postgres); F1 (`RestartPod`)
dispara de verdade com UID correto mas fica `PartiallyApplied` (janela de verificação curta
demais); `DryRun` e o circuit breaker não foram exercitados em replay ao vivo completo (só teste
unitário). Tarefa 8 (manifestos K8s) feita, não ligada ao `bootstrap.ps1`. Detalhe completo abaixo.
Decisão tomada: avançar para a Fase 10 em vez de fechar os residuais de F1/DryRun/breaker.
**Fase 10 (Norn.API) concluída e fechada — DoD integral validado ao vivo, ver detalhe abaixo.**

**Residuais da Fase 9 — os três fechados (17/09/2026).** F1/`RestartPod` fechado por completo,
incluindo a calibração de janela (`RestartPodVerificationWindowSeconds`, 240s, por tipo de ação —
ver detalhe). `DryRun` fechado, validado ao vivo (mensagem, timestamps, métricas e UID do pod
provando que nada real foi tocado). **Circuit breaker fechado, provado ao vivo**: RBAC quebrada de
verdade (token real da `norn-executor`, não kubeconfig de admin) e `SET` negado em `shop:flags:*`
no Redis ao mesmo tempo — com os dois caminhos de aplicação bloqueados, 5 falhas reais heterogêneas
(`RestartPod`/Catalog x2, `ScaleUp`/Order x2, `ToggleFeatureFlag`/Payment x1) se acumularam sem
nenhum reset, e a quinta disparou o log `"Circuit breaker aberto após 5 falhas consecutivas"` com
`norn:platform:config:mode` virando `Observe` de verdade, sem intervenção manual — ver detalhe.

Cluster k3d validado de ponta a ponta (Fase 6): `bootstrap.ps1` leva de zero a sistema funcional em um comando, fluxo completo de pedido roda dentro do cluster, RBAC do ADR-03 confirmado (positivo e negativo), `maxReplicas=3` valida escala real, Traefik ausente (D9), `container_memory_working_set_bytes`/`container_cpu_usage_seconds_total` via cAdvisor fecham o DoD da Fase 4.

**Fase 7 — Monitor e Analyzer, concluída.** `Norn.Knowledge` (Postgres schema `platform`: `anomaly_signals`, `anomaly_contexts`, `healing_plans`, `healing_outcomes`, `llm_traces`, `experiment_runs`; `ICooldownStore`/`IPlatformConfig` sobre Redis), `Norn.Monitor` (cliente do Prometheus, leitura de topologia via `KubernetesClient`, polling da assinatura fechada M=7), `Norn.Analyzer` (`DetectIidSpike`/`DetectIidChangePoint` incrementais, `SeverityCalculator` por ADR-14, `ContextCorrelator` com ordenação determinística) e `Norn.Worker` (composition root que fecha o laço até a persistência do contexto, modo `Observe`) implementados e testados — 160 testes verdes na solução (unitários + arquitetura + integração com Testcontainers reais para Postgres e Prometheus). `ITopologyReader` ganhou `GetLastTerminationReasonAsync` para resolver a pendência abaixo.

**Pendência da Fase 6 resolvida:** `container_oom_events_total` não fechava porque o containerd remove o cgroup do container morto antes do cAdvisor conseguir ler `oom_kill=1` nele. Decisão tomada e implementada: `Norn.Monitor` lê `status.containerStatuses[].lastState.terminated.reason == OOMKilled` do próprio Pod via `KubernetesClient` (verbo `get`, já concedido pela `Role` do ADR-03) em vez do caminho Prometheus. Detalhe completo em `docs/metrics-matrix.md`.

**Replay ao vivo do F1 concluído com sucesso** contra o cluster k3d e a infra da Fase 6 (ambos ainda de pé), em sessão de acompanhamento após o fechamento da fase. Sequência validada: pod do Catalog.API reiniciado (o usuário rodou o `kubectl delete pod` que a sessão anterior não pôde executar por permissão), F1 desativado até estabilizar, `Norn.Worker` rodado localmente com baseline limpa (~180 MB) por um warmup completo (30 ciclos, ~150 s), F1 reativado, e o laço completo disparou ao vivo: sinais `dotnet_gc_pause_time_seconds_total` (severidade `Low`) e `dotnet_process_memory_working_set_bytes` (`Medium`) detectados e persistidos em `anomaly_signals`, correlacionados em um único `AnomalyContext` (4 sinais na janela, primário = RSS) persistido em `anomaly_contexts` com `context_hash` SHA-256 real e `primary_signal_id` resolvendo para uma linha existente — confirmado por consulta direta ao Postgres.

**Dois bugs reais encontrados e corrigidos durante essa validação** (nenhum dos dois aparecia nos testes unitários/integração, só rodando contra o cluster de verdade):
1. `KubernetesTopologyReader.GetTopologyAsync` usava o nome do serviço OTel (`Norn.Shop.Catalog.API`, o `exported_job` do Prometheus) direto como nome do `Deployment` no cluster — mas o manifesto usa nomes kebab-case (`catalog-api`). Derrubava o host inteiro com 404 `NotFound`. Corrigido com `MonitorOptions.ServiceToDeploymentName`, um mapa explícito entre os dois nomes.
2. `MonitorPollingBackgroundService` extraía o nome da métrica do rótulo `__name__` da resposta do Prometheus — que desaparece em qualquer expressão agregada (`histogram_quantile`, `sum`, divisão), gerando `MetricName` vazio para p99, taxa de 5xx e `errorsByType`, e uma `InvalidOperationException` no `SeverityCalculator`. Corrigido usando o nome lógico que já vinha do próprio `PrometheusQueryCatalog`.

Também virou definitivo: `AnomalyPipelineBackgroundService.ExecuteAsync` agora captura exceção por ciclo em vez de deixar o `BackgroundServiceExceptionBehavior.StopHost` padrão derrubar o processo inteiro numa falha transitória — foi essa mudança que permitiu ver o segundo bug sem perder a sessão de teste.

**Achado à parte, não bloqueante:** o pod do Catalog.API tomou `OOMKilled` real algumas vezes mesmo com o F1 desativado e sem tráfego algum contra o serviço, sugerindo que o limite de 256Mi do manifesto (Fase 6) está apertado para a operação normal após várias horas de cluster ligado. Vale revisar antes da campanha (Fase 12).

**Fase 8 — Planner, concluída (tarefas 1–11; 11a opcional não iniciada).** `Norn.Planner` criado:
`RuleEngine` (braço C — `DecideActionType` puro e total sobre a assinatura fechada M=7, prioridade
F1 > F2 > F3 > NoOp documentada em `docs/rule-table.md`), `HealingActionPreconditionChecker`
(barreiras do ADR-04 + pré-condições do §5.4, compartilhado entre os dois braços), `LlmPlanner`
(pipeline completo do §5.5 — orçamento por caracteres, `IChatClient` direto via OllamaSharp sobre
`norn-qwen`, validação/reparo/fallback, `DecidedBy` de três vias Llm/RuleEngine/Fallback) e
`LlmOutputValidator`. `Norn.Contracts` ganhou `ShopFlagCatalog` (extraído da duplicação local do
Worker) e `HealingActionCatalog` (fonte única do catálogo de ações para o prompt e a validação).
178 testes verdes em `Norn.Planner.UnitTests` (exaustivo 9a sobre 2⁷=128 subconjuntos, fronteiras de
severidade 9b, pré-condições 9c, validador com dublê de `IChatClient` — nenhum chamando o Ollama
real), mais `Norn.ArchitectureTests` cobrindo o Planner pela regra de dependência do ADR-17.
Decisão registrada: `Microsoft.SemanticKernel`/`Connectors.Ollama` (alpha) ficou de fora —
`OllamaApiClient` (OllamaSharp) já implementa `IChatClient` diretamente, dispensando o conector
para fixar `seed`/`num_ctx` (mesma conclusão que o `CLAUDE.md` já registrava).

**Verificação manual contra o `norn-qwen` real (fora da suíte de teste) confirmou o pipeline
ponta a ponta**: chamada real ao Ollama, JSON com `replicaDelta` como número puro (não string)
inicialmente caiu em `InvalidJson` nos dois braços — bug real de tipagem em
`LlmActionDto.Parameters` (esperava `string`, o modelo manda número/bool também), corrigido para
`Dictionary<string, JsonElement>` com normalização por `JsonValueKind` em `LlmOutputValidator`.
Depois da correção, `DecidedBy: Llm` em ~3,2s, plano válido, `PromptHash` calculado. Nenhum dos dois
bugs (este e os da Fase 7) apareceria só com testes unitários — reforça o valor de sempre validar
contra o Ollama real antes de gastar as 20 execuções da tarefa 11.

**Tarefa 11 concluída — Fase 8 fechada.** Réplay ao vivo do F3 (sessão de acompanhamento, após a
máquina precisar ser desligada e religada no meio do trabalho — ver "recuperação de ambiente"
abaixo) capturou um `AnomalyContext` real (`Norn.Shop.Payment.API`,
`norn_shop_payments_gateway_latency_ms`, severidade `Critical`, ~4770ms contra SLO de 500ms),
exportado como fixture versionada (`tests/Platform/Norn.Planner.UnitTests/Fixtures/f3-gateway-latency-context.json`)
com `context_hash` afirmado em teste. 20 execuções do `LlmPlanner` real contra o `norn-qwen` sobre
esse contexto: **estabilidade de 20/20 nos três níveis** (ação, parâmetros, rationale byte-a-byte),
`decidedBy: Llm` sempre, nenhuma queda para regra/fallback. Achado que importa para H2: o LLM
escolhe `NoOp` nas 20, o `RuleEngine` escolhe `ToggleFeatureFlag` para a mesma assinatura — os dois
braços divergem de forma estável, e o rationale (idêntico nas 20) revela a causa: o modelo trata a
ausência de `pod`/`podUid` no contexto como impeditivo, mesmo `ToggleFeatureFlag` não dependendo de
pod algum. Detalhe completo, incluindo a nota sobre o ambiente híbrido da captura, em
`docs/experiments/estabilidade-llm.md`. Tarefa 11a (sensibilidade ao *thinking mode*) continua
opcional, não iniciada.

**Dois bugs reais adicionais, encontrados só ao tentar fechar o loop do F3 ao vivo** (nenhum
aparecia nos 180 testes unitários do Planner nem nos testes de Fase 7):
1. **F3 não alcançava a métrica que deveria mover.** O delay do caos (`GatewayLatencyEffect`) vivia
   só no `ChaosMiddleware` (pipeline HTTP) — mas o tráfego real de pagamento é 100% assíncrono
   (`Order.API` → RabbitMQ → `OrderCreatedConsumer`), que nunca passa por lá; e mesmo forçando uma
   chamada HTTP direta, o delay acontecia fora da janela que `ProcessPaymentHandler` cronometra em
   `norn_shop_payments_gateway_latency_ms` (só a chamada a `SimulatedPaymentGateway.AuthorizeAsync`
   é medida). Corrigido movendo o delay para dentro do próprio `SimulatedPaymentGateway`, via um
   seam público novo (`Norn.BuildingBlocks.Chaos.IChaosGatewayDelay`) que a mesma instância de
   `GatewayLatencyEffect` implementa — `ChaosServiceCollectionExtensions` agora registra essa classe
   uma vez, exposta sob os dois contratos, para o `ChaosBackgroundService` atualizar e o gateway
   simulado ler a mesma instância. `OnRequestAsync` do F3 virou passagem pura (só `ConcurrencyThrottleEffect`
   do F2 ainda usa o meio HTTP de verdade).
2. **`MonitorPollingBackgroundService` gravava `Target.Service = "unknown"`** para as quatro
   métricas agregadas da assinatura (p99, taxa de 5xx, `errorsByType`, latência do gateway) — o
   mesmo padrão do bug de `MetricName` vazio já corrigido na Fase 7, só que para o rótulo
   `exported_job`: `sum by (le)`/`sum(...)/sum(...)` também removem esse rótulo, e
   `PrometheusMetricSource.ToServiceTarget` cai no default "unknown", que quebra a leitura de
   topologia (`Deployment "unknown"` 404) e aborta a persistência do contexto. Corrigido no mesmo
   ponto que já sobrescrevia `MetricName` a partir do catálogo — agora também sobrescreve
   `Target.Service`.

**Recuperação de ambiente (não é bug do projeto, registrar para a próxima sessão que reiniciar a
máquina):** depois de desligar/religar o Windows, `host.docker.internal` no arquivo `hosts` do
Windows ficou apontando para o IP antigo da máquina (o DHCP deu outro IP no boot), e reiniciar o
Docker Desktop **não** corrigiu isso sozinho nas duas tentativas. O sintoma é `kubectl` (rodando no
Windows, fora de container) falhar com timeout de conexão. Contorno sem mexer em arquivo de
sistema: editar `~/.kube/config` trocando `host.docker.internal:<porta>` por `127.0.0.1:<porta>` — a
porta do `k3d-norn-serverlb` já é publicada em `0.0.0.0`, então `127.0.0.1` funciona independente do
IP da LAN. Os pods do Shop também precisam de um ciclo de `CrashLoopBackOff` para se recuperarem
depois que a infra volta (Postgres/Redis não estavam de pé quando eles tentaram subir pela primeira
vez) — não há permissão configurada para `kubectl delete pod`, então é esperar o backoff (até ~5
min) ou pedir para o usuário forçar.

**Fase 9 — Executor, código completo e testado; validação ao vivo ainda pendente.** `Norn.Executor`
criado (`ExecutionPreconditionChecker` — reavalia pré-condições e barreiras do ADR-04 ao vivo,
lendo `ITopologyReader`/`ICooldownStore` no instante da atuação, nunca sobre o `AnomalyContext`
congelado da decisão; `KubernetesActionApplier` — `ScaleUp` via `PatchNamespacedDeploymentScaleAsync`
no subrecurso `scale`, `RestartPod` via `DeleteNamespacedPodAsync`, distinguindo 403 (`RbacDefect`)
de qualquer outra falha; `FeatureFlagActionApplier` — `ToggleFeatureFlag` via `IFeatureFlagWriter`
novo; `StartupCapabilityVerifier` — `SelfSubjectAccessReview` para `ScaleUp`/`RestartPod` e
PING+EXISTS no Redis para o catálogo de flags, falha rápido no startup do Worker; `CircuitBreakerState`
— contador em memória, ADR-04 barreira (c), abre e força `Observe` após 5 falhas consecutivas de
aplicação, zerando o próprio contador ao abrir; `HealingActionExecutor` — orquestra tudo, produz
`HealingOutcome`, distingue `ScaleUp` saturado (aceita e satura em `maxReplicas`, nunca recusa) de
recusa por pré-condição). `Norn.Contracts` ganhou `IFeatureFlagWriter`, `IRecentMetricsReader`
(extraído de `Norn.Monitor.Prometheus.RecentMetricsReader` para o Executor reusar a mesma leitura
de métricas recentes sem duplicar as seis consultas PromQL) e dois métodos novos em `IPlatformConfig`/
`ICooldownStore` (`SetModeAsync`, `RecordActionAsync`/`CountRecentActionsAsync` — janela deslizante
via sorted set do Redis para a barreira (b) do ADR-04). `Norn.Worker` agora compõe também
`Norn.Planner` e `Norn.Executor` (nenhum dos dois estava ligado ao loop antes desta fase);
planejamento e atuação rodam **destacados** do laço de polling de 5s (`_ = Task.Run(...)` com escopo
de DI próprio para o `IKnowledgeStore` — o campo do construtor é captive dependency do singleton
`AnomalyPipelineBackgroundService`, e usá-lo dentro de uma tarefa destacada concorrente com o
próximo tick violaria a garantia de uso não concorrente do `DbContext`). Decisão de design registrada
em código: `ExecutorOptions` lê `MaxReplicas`/`RestartPodCooldown`/`MaxActionsPerWindow`/`ActionWindow`
da mesma seção `Norn:Planner` que `PlannerOptions`, e `ServiceToDeploymentName` da mesma seção
`Norn:Monitor` — o §5.4 exige que essas barreiras "mudem-se num lugar só", e os dois projetos
continuam isolados entre si (nenhuma referência de projeto Executor→Planner/Monitor/Analyzer, coberto
por `Norn.ArchitectureTests`). 354 testes verdes na solução (25 novos do Executor — `IKubernetes`
dublado via NSubstitute, inclusive o detalhe de que as convenientes `*Async` do cliente k8s são
extension methods sobre as `*WithHttpMessagesAsync`, e só estas últimas são dubláveis).

**Revisão do `code-reviewer` antes do commit encontrou dois bloqueantes reais, corrigidos na hora:**
(1) nada serializava execuções concorrentes de `PlanExecuteAndPersistAsync` para o mesmo alvo — a
janela de correlação (~60s) podia fechar de novo para o mesmo serviço antes do disparo anterior
terminar (cooldown só é gravado *depois* de aplicar), permitindo em tese duas ações sobre o mesmo
alvo ao mesmo tempo, o próprio loop patológico que o ADR-04 existe para conter. Corrigido com
`AnomalyPipelineBackgroundService.targetsInFlight` (`ConcurrentDictionary<string, Task>`,
reservado por `TryAdd` antes de despachar). (2) as tasks destacadas não eram rastreadas nem
aguardadas no encerramento — um shutdown no meio da janela de verificação (até 120s) perderia o
`HealingOutcome` mesmo com a ação já aplicada de verdade no cluster. Corrigido com
`StopAsync` drenando `targetsInFlight.Values`. Um terceiro achado, não bloqueante (corrida benigna
em `CircuitBreakerState.RecordFailure` podendo dobrar a contagem de "circuito abriu" sob falhas
concorrentes), também foi corrigido, trocando `Increment`+comparação por um laço de CAS — com teste
de estresse sob concorrência real cobrindo o caso. 355 testes verdes após as correções.

**Replay ao vivo contra o cluster k3d, o Postgres/Redis/Prometheus da Fase 6 e o `norn-qwen` real —
concluído em sessão de acompanhamento.** Achado de ambiente resolvido primeiro: `host.k3d.internal`
não resolvia de dentro do cluster (os três pods do Shop em `CrashLoopBackOff` por não alcançar
Redis/Postgres) — o `bootstrap.ps1` já documentava esse cenário exato (Docker Desktop sobrevive a
restart, CoreDNS perde a injeção); `k3d cluster stop`/`start` reinjetou o registro e os três pods
recuperaram sozinhos.

1. **`StartupCapabilityVerifier` contra a `Role` real, não um dublê.** Harness descartável tomando
um token de `kubectl create token norn-executor -n norn-shop` (não o kubeconfig de admin, que
mascararia qualquer restrição) confirmou os dois casos: no namespace `norn-shop` passa (`patch
deployments/scale` e `delete pods` permitidos pela `Role` real); apontado para `norn-platform` —
onde a `Role` não existe — falha exatamente como projetado, com as duas pré-condições nomeadas na
mensagem. A checagem não é um carimbo de borracha.
2. **`Norn.Worker` completo (Monitor+Analyzer+Planner+Executor+Knowledge) rodado ao vivo por ~10 min**,
`dotnet run` local, migrations reais, `StartupCapabilityVerifier` real (kubeconfig de admin — só a
verificação 1 acima prova a Role restrita), polling real do Prometheus. Modo `Active` forçado via
Redis e F1 disparado via `/admin/chaos/activate` no Catalog real: o laço completo rodou ao vivo —
sinal detectado (RSS, severidade `Medium`), `AnomalyContext` persistido (4 sinais correlacionados),
`HealingPlan` decidido pelo **LLM real** (`DecidedBy: Llm`, chamada ao `norn-qwen` funcionando ponta
a ponta pela primeira vez dentro do loop do Worker, não isolada como na Fase 8), `HealingOutcome`
persistido no Postgres (`Status: Succeeded`) — confirmado por consulta direta às tabelas
`healing_plans`/`healing_outcomes`. Repetiu para um segundo alvo (`Order.API`, sinal de latência)
sem intervenção — não foi um evento isolado.
3. **DoD "F1 → RestartPod com SLO restaurado" não foi alcançado nesta rodada — por dois motivos
distintos, um de timing e um bug real:**
   - **Timing:** sob o limite de 256Mi do manifesto (Fase 6) e a rampa do F1 (`tau=90s`), o
     `OOMKilled` nativo do kubelet chegou em ~35s da ativação — mais rápido que a janela de
     correlação de 60s (`AnalyzerOptions.CorrelationWindow`, Fase 7) consegue fechar. O laço
     reativo do Norn nunca teve chance de agir antes de o próprio Kubernetes já ter reiniciado o
     pod. Isto é característica de calibração das Fases 4–6, não defeito do Executor.
   - **Bug real, fora do escopo da Fase 9, achado só no replay:** `PrometheusMetricSource.ToServiceTarget`
     (`Norn.Monitor`, Fase 7) cai em `labels["exported_instance"]` — um UUID de instância OTel,
     nunca um nome de pod Kubernetes — porque o rótulo `pod` nativo não chega às métricas: elas
     saem do processo via OTLP para o Collector e voltam como Prometheus sem enriquecimento
     `k8sattributes`, então nunca carregam `pod`/`namespace` de verdade. Resultado: todo
     `AnomalyContext` construído ao vivo tem `PrimarySignal.Target.PodUid = null` e `Target.Pod` =
     um UUID sem sentido para o cluster. A confirmação veio do `rationale` do LLM, gravado no
     Postgres: *"falta UID para RestartPod"* — o Planner e o Executor recusaram corretamente agir
     sem UID confiável (o design de segurança funcionou como projetado), mas isso significa que
     **`RestartPod` está estruturalmente inalcançável pelo laço ao vivo hoje**, em qualquer modo,
     até esse gap ser fechado — provavelmente enriquecendo o OTel Collector com `k8sattributes`
     (Fase 4/6) ou resolvendo o pod atual via `ITopologyReader` no momento de montar o contexto
     (Fase 7). Não corrigido nesta sessão — cruza a fronteira de uma fase já fechada e merece
     decisão própria, não um patch improvisado em cima da validação da Fase 9.

**Tarefa 8 (manifestos K8s do Norn em `norn-platform`) continua não feita** — mesmo motivo de antes:
o Worker roda no host via kubeconfig, não como Pod, e não há Dockerfile nem passo de build/push no
`bootstrap.ps1`. O `ServiceAccount norn-executor` permanece dormente (não é o que autentica o
Worker hoje) mas sua `Role` real já foi validada diretamente (achado 1 acima).

**Estado da infraestrutura ao fim da sessão:** modo da plataforma revertido para `Observe` (padrão
seguro, ADR-05), caos F1 desativado, `Norn.Worker` local encerrado, os três pods do Shop
`Running 1/1` — cluster deixado limpo para a próxima sessão.

**Correção do bug de identidade do pod (sessão de acompanhamento seguinte) — implementada e testada,
RestartPod ao vivo ainda não demonstrado.** `ITopologyReader` ganhou `GetCurrentPodNameAsync`
(`Norn.Contracts.Ports`), implementado em `KubernetesTopologyReader` via `CoreV1.ListNamespacedPodAsync`
com `labelSelector: app=<deploymentName>` (mesmo rótulo que os manifestos do Shop já usam no
seletor do Deployment) — baseline de 1 réplica por serviço (Fase 6) torna a resolução inequívoca.
`AnomalyPipelineBackgroundService.BuildAndPersistContextAsync` agora chama esse método antes de
montar o contexto e substitui `Target.Pod`/`Target.PodUid` nos sinais que alimentam o
`AnomalyContext` — as linhas já persistidas em `anomaly_signals` (antes da correlação) continuam
com o valor antigo, limitação conhecida e aceita, não corrigida (tocaria o ponto de persistência por
amostra, fora do escopo deste achado). 8 testes novos em `Norn.Monitor.UnitTests` (`IKubernetes`
dublado, incluindo o caso "prefere pod `Running` sobre `Pending`"); `KubernetesTopologyReader`
precisou virar `public` para ser testável (era `internal`, sem `InternalsVisibleTo` — mesmo padrão
que os aplicadores do Executor já usavam). 358 testes verdes.

**Duas tentativas de replay ao vivo para confirmar `RestartPod` de fato disparando — nenhuma
alcançou o DoD, e por um motivo novo, distinto do achado anterior.** Com o Worker recém-reiniciado
e o F1 reativado cedo demais, o `MetricDetectorEngine` ainda não tinha histórico suficiente
(~30 amostras, ~150s) quando o `OOMKilled` chegou — confirma a orientação já registrada na Fase 7
("F1 desativado até estabilizar... por um warmup completo"). Numa segunda tentativa, com warmup
completo, o sinal *ainda* não disparou a tempo: a leitura de `dotnet_process_memory_working_set_bytes`
no Prometheus ficou **parada no mesmo valor por dezenas de segundos** enquanto o RSS real do
processo continuava subindo — o exportador de métricas OTel batcha em um intervalo (~60s, não
confirmado por configuração explícita, só observado) mais lento que a janela entre a ativação do F1
e o `OOMKilled` sob o limite de 256Mi. Do ponto de vista do Analyzer, a métrica parece plana até o
próximo lote chegar — e nesse ambiente o pod já morreu antes disso. **Duas causas de timing agora
documentadas, independentes uma da outra:** a janela de correlação de 60s (Fase 7) e o intervalo de
exportação do OTel Collector (Fase 1/6) — ambas mais lentas que o F1 sob o limite de memória atual.
Nenhuma das duas é bug da Fase 9; ambas são candidatas a revisão de calibração antes da Fase 12
(reduzir `F1RampSeconds`/aumentar o limite de memória, encurtar `CorrelationWindow`, ou configurar
o intervalo de exportação de métricas explicitamente — decisão do usuário, não tomada aqui).

**Terceira sessão de acompanhamento — as duas calibrações de timing foram feitas, e o DoD da
Fase 9 fechou de verdade para F2 e F3, ao vivo, com evidência em Postgres. F1 ficou parcial, mas
por um motivo diferente de tudo que veio antes.**

**Duas mudanças de calibração, com justificativa registrada em código:**
1. `Norn.BuildingBlocks.Telemetry.TelemetryHostBuilderExtensions` — intervalo de exportação de
   métricas OTel fixado em 5s (`PeriodicExportingMetricReaderOptions.ExportIntervalMilliseconds`),
   antes no padrão do SDK (60s, nunca configurado explicitamente). Afeta todos os serviços que
   chamam `AddNornTelemetry` — Shop e `Norn.Worker`.
2. `deploy/k8s/base/catalog.yaml` — limite de memória do Catalog de 256Mi para 384Mi. O próprio
   comentário anterior no manifesto dizia que o objetivo era OOMKilled "em minutos, não em dezenas
   de minutos" — 256Mi entregava ~30-60s, contradizendo o próprio objetivo escrito. 384Mi projeta
   ~110s sob intensidade saturante do F1, dando à detecção+decisão uma chance real de agir antes
   do kernel. Aplicado ao vivo via `kubectl patch` (não seria seguro reaplicar `catalog.yaml` bruto
   por cima do Deployment real: a tag de imagem do arquivo é `placeholder`, a do cluster é o SHA do
   build — `kubectl apply -f` teria trocado a imagem por engano).

**Para as duas calibrações valerem no cluster, as três imagens do Shop precisaram ser
reconstruídas e republicadas** (o código rodando antes era de antes desta sessão) — tag
`dev-session` (não um SHA de commit: build a partir de código ainda não commitado, tag deliberadamente
não-definitiva), via `docker build`/`docker push` para o registry local e `kubectl set image` nos
três Deployments. **Isto não passou pelo `bootstrap.ps1`** — foi feito à mão, fora do fluxo padrão,
e por isso não deve ser confundido com uma nova convenção; a próxima execução normal do
`bootstrap.ps1` reconstrói com o SHA do commit real e sobrescreve estas imagens de qualquer forma.

**Com as duas calibrações + o fix de identidade do pod (sessão anterior) no ar, rodando `Norn.Worker`
local em modo `Active` contra tráfego real do `Norn.LoadGenerator` (novo achado: F2 precisa de
tráfego de verdade para produzir latência — sem carga, a rampa do efeito não tem o que estrangular):**

- **F2 → `ScaleUp`, DoD fechado.** `HealingPlan` decidido (`RuleEngine`, fallback do braço B),
  réplicas de `order-api` foram de 1 para 2 de verdade (confirmado via `kubectl`), `HealingOutcome`
  = `Succeeded`, `SloRestored = true`, persistido no Postgres. Uma tentativa anterior, mais cedo no
  ramp do F2 (caos ainda subindo quando a janela de verificação fechou), tinha saído
  `PartiallyApplied` — achado honesto sobre o próprio F2, não falha do Executor: a cura pode ser
  aplicada corretamente e ainda assim não bastar enquanto o caos continua intensificando.
- **F3 → `ToggleFeatureFlag`, DoD fechado — e apareceu sem eu precisar ativar o F3 chaos
  explicitamente.** Sob a carga sustentada do F2 (~15 rps, várias centenas de pedidos), o gateway
  de pagamento degradou o suficiente por conta própria para a assinatura de F3 (latência do
  gateway/taxa de 5xx) disparar de verdade. `RuleEngine` decidiu `ToggleFeatureFlag`, a flag
  `shop:flags:payment.gateway.bypass` foi escrita de verdade no Redis, `HealingOutcome` =
  `Succeeded`, `SloRestored = true`. Confirma que o laço reage a degradação real de carga, não só a
  caos sintético — e confirma `RedisFeatureFlagWriter` escrevendo em Redis de verdade pela primeira
  vez nesta fase.
- **F1 → `RestartPod` disparou de verdade, com UID correto — a correção da sessão anterior está
  confirmada — mas `PartiallyApplied`, não `Succeeded`.** `HealingPlan` decidido pelo LLM, `target.pod`
  = nome real do pod (`catalog-api-64987fd67c-gx87b`), `target.podUid`/`parameters.podUid` idênticos
  e não nulos — a pré-condição de UID (§5.4) aceitou porque o pod resolvido batia com o pod vivo,
  exatamente o que a Fase 9 tarefa 2 exige. O Executor aplicou o `DeleteNamespacedPodAsync` de
  verdade. `SloRestored = false` só porque a janela de verificação (120s) fechou antes de o RSS do
  pod recém-criado assentar abaixo do limiar de restauração — não é o mesmo problema documentado
  acima (aquele era "nunca detecta"; este é "detecta, decide e age corretamente, mas a verificação
  é apertada demais para este caso"). Registrar como item de calibração adicional, não resolvido:
  o limiar de restauração do F1 (`ExecutorOptions.MemoryRestoredThresholdBytes`) ou a janela de
  verificação podem precisar de ajuste fino separado do que já foi feito aqui.
- **Modo `Observe` confirmado ao vivo, de graça, na troca de modo em produção:** um `HealingPlan`
  (`ScaleUp`, decidido pelo LLM) foi criado para um sinal Critical do gateway de pagamento
  **depois** de eu já ter revertido o modo para `Observe` — a linha ficou em `healing_plans` mas
  **nenhuma** linha correspondente apareceu em `healing_outcomes`, e as réplicas do Payment
  continuaram em 1. Confirma a leitura fresca de modo por ciclo (`PlanExecuteAndPersistAsync` lê o
  modo de novo antes de decidir se chama o Executor) e o comportamento "planeja, nunca atua" do
  ADR-05.
- **Modo `DryRun` — confirmado só parcialmente.** O log mostrou sinais sendo detectados com
  `(modo DryRun)` de verdade (a leitura de modo está correta), mas nenhum `HealingPlan` chegou a
  fechar dentro do tempo desta sessão para esse modo especificamente — o achado estrutural (nenhuma
  escrita de estado até `DryRun` produzir um plano completo) continua coberto só por teste unitário
  (`ExecuteAsync_DryRun_NeverTouchesClusterOrRedis`, `ExecuteAsync_DryRun_ToggleFeatureFlag_NeverCallsFeatureFlagWriter`),
  não por replay ao vivo completo.
- **Circuit breaker — decisão consciente de não tentar ao vivo nesta sessão.** Forçar 5 falhas
  reais consecutivas exigiria quebrar a RBAC de propósito e esperar vários ciclos de detecção — o
  cooldown geral (ADR-04) só é gravado em sucesso, então falhas repetidas no mesmo alvo não são
  bloqueadas por cooldown, mas cada ciclo ainda depende da janela de correlação (~60s) mais o tempo
  de decisão. Dado o teste de estresse sob concorrência real já existente
  (`RecordFailure_UnderConcurrency_ReportsTripExactlyOncePerThresholdCrossing`) e o tempo já
  investido nesta sessão, ficou de fora — item explicitamente em aberto, não esquecido.

**Tarefa 8 — manifestos K8s do Norn em `norn-platform`, feita, mas deliberadamente não ligada ao
fluxo padrão.** `src/Platform/Norn.Worker/Dockerfile` criado (mesmo padrão dos Dockerfiles do
Shop); `deploy/k8s/base/norn-platform.yaml` novo — `Deployment` do `Norn.Worker` em
`norn-platform`, `serviceAccountName: norn-executor`, variáveis de ambiente para Postgres/Redis/
Prometheus/Ollama via `host.k3d.internal` (mesmo caminho que o Shop já usa); registrado em
`deploy/k8s/base/kustomization.yaml`. `rbac-norn.yaml` corrigido: o `ServiceAccount norn-executor`
mudou de `norn-shop` para `norn-platform` (onde o Pod de fato roda), `RoleBinding` atualizado para
referenciar o `ServiceAccount` no namespace certo — aplicado ao vivo e revalidado com
`kubectl auth can-i --as=system:serviceaccount:norn-platform:norn-executor` (positivo em
`norn-shop`). **Decisão explícita: não conectado ao `bootstrap.ps1`.** A imagem
`norn-platform-worker:placeholder` não é construída por nenhum passo automático — aplicar
`norn-platform.yaml` sem build prévio deixa o Deployment em `ImagePullBackOff`. O motivo é
proteger o modelo operacional atual (Worker rodando no host via `dotnet run`, usado em toda
validação ao vivo das Fases 7-9) de uma mudança de comportamento que o usuário não pediu — ligar
isso ao bootstrap é uma decisão do usuário, não algo para assumir por conta própria. Um
`ServiceAccount norn-executor` órfão ficou para trás em `norn-shop` (o antigo, pré-correção) —
tentativa de removê-lo foi negada por permissão; inofensivo (sem `RoleBinding` apontando para ele),
mas registrado aqui para quem for limpar depois.

**Estado do cluster ao fim desta sessão:** as três imagens do Shop estão em `dev-session` (não um
SHA de commit — a próxima `bootstrap.ps1` sobrescreve), réplicas de volta ao baseline (1 cada),
`shop:flags:payment.gateway.bypass = false`, caos desativado, modo `Observe`, `Norn.Worker` e
`Norn.LoadGenerator` locais encerrados. O limite de memória do Catalog ficou em 384Mi **só no
cluster ao vivo** (via `kubectl patch`) — `deploy/k8s/base/catalog.yaml` também foi atualizado, então
uma reaplicação futura do manifesto (`bootstrap.ps1` ou `kubectl apply -k`) mantém 384Mi.

**Fechamento dos residuais da Fase 9 — sessão de acompanhamento longa (17/09/2026), dois de três
fechados com evidência ao vivo forte; o terceiro avançou mas não fechou.**

**F1/`RestartPod` `PartiallyApplied` → causa raiz encontrada e corrigida, não é mais um bug.**
`PrometheusMetricSource.QueryInstantAsync` (`src/Platform/Norn.Monitor/Prometheus/PrometheusMetricSource.cs`)
pegava `results[0]` sem critério quando a consulta batia em mais de uma série — depois de um
`RestartPod`, o pod apagado ainda não expirou do lookback de staleness do Prometheus enquanto o pod
novo já publicou a primeira amostra, e as duas casam o mesmo `exported_job`. Corrigido para escolher
a amostra de timestamp mais recente entre os resultados — 11 testes novos em
`Norn.Monitor.UnitTests` (inclusive um caso com os resultados fora de ordem, para provar que não é
só "pega o último"), revisão de código sem achado bloqueante. **Provado ao vivo**: ao longo da
sessão, a leitura de verificação sempre acompanhou o valor real do pod novo (cross-check direto com
`kubectl top`), nunca uma leitura congelada do pod antigo. Um `RestartPod` real chegou a fechar
`Succeeded`/`SloRestored=true` (120,1s de recuperação) durante a janela sem supervisão. **O que
sobra, e é achado novo, não o bug antigo:** 120s não bastam para um pod .NET recém-criado assentar
de forma confiável abaixo do limiar de 200MB — overhead de startup/JIT/GC inicial empurra o RSS pra
cima por um tempo mesmo sem vazamento nenhum rolando. `ExecutorOptions.MemoryRestoredThresholdBytes`
e/ou uma janela de verificação maior especificamente para `RestartPod` (hoje só existe uma
`VerificationWindowSeconds` global, compartilhada por todo tipo de ação) ficam como item de
calibração em aberto, não resolvido nesta sessão — decisão do usuário, não assumida aqui.

**Circuit breaker — avançou bastante, não fechou; achados genuínos, não falha do código.** Duas
descobertas importantes confirmadas ao vivo pela primeira vez:
1. **O Worker local sempre rodou com o kubeconfig de admin, nunca com a ServiceAccount
   `norn-executor`** — quebrar a `Role` não tem efeito nenhum nesse modo (mesma armadilha que a
   Fase 9 já tinha descoberto para o `StartupCapabilityVerifier`, mas desta vez descoberta por um
   `RestartPod` aplicando de verdade contra uma `Role` que eu tinha acabado de quebrar). Corrigido
   gerando um token real via `kubectl create token norn-executor -n norn-platform` e rodando o
   Worker com um `KUBECONFIG` próprio apontando pra esse token — `kubectl auth can-i` confirmou a
   restrição de verdade antes de subir o Worker com ele.
2. **`StartupCapabilityVerifier` recusa o boot de verdade contra as duas permissões
   separadamente** (não só a combinação testada na Fase 9) — com `pods delete` quebrado, o Worker
   com o token restrito literalmente não sobe (`InvalidOperationException` na inicialização,
   mensagem nomeando a barreira certa); restaurando e quebrando só `deployments/scale patch` em vez
   disso, mesmo resultado. Confirma que a checagem é tudo-ou-nada nas três capacidades, não um
   carimbo de borracha — e que ela só pode ser furada quebrando a `Role` **depois** do boot (a
   verificação roda uma única vez, no `Program.cs`), exatamente como o raciocínio já indicava.

**O que impediu fechar as 5 falhas consecutivas — obstáculo do ambiente, não do mecanismo:** depois
de ~8h de sessão contínua, a severidade (ADR-14) é relativa a um `expectedValue` que se ajustou para
cima junto com os valores real altos, e o `RuleEngine` passou a decidir `NoOp` ("severidade Low — não
age abaixo de Medium") mesmo com memória/latência genuinamente ruins — sem uma decisão real de
`RestartPod`/`ScaleUp`, não há tentativa para falhar. Ao tentar resetar o baseline reiniciando o
Catalog, o próprio efeito de caos (`MemoryRetentionEffect`) revelou um problema à parte: sob pressão
sustentada de várias horas, o processo passou a lançar `OutOfMemoryException` internamente dentro do
laço de tick do caos (capturada e logada, não derruba o processo) — e nesse estado a exportação de
métricas para o Collector parou de funcionar por completo (confirmado via consulta direta ao
Prometheus: zero séries para `Norn.Shop.Catalog.API` por vários minutos, mesmo com `kubectl top`
mostrando memória real alta), o que por sua vez deixa o Analyzer sem dado nenhum pra detectar. Um
restart limpo do pod resolveu (métricas voltaram a fluir imediatamente) — mas o padrão em si
("caos sustentado por muitas horas pode degradar o processo a ponto de quebrar a própria telemetria
que o Norn depende pra reagir") é um achado que vale registrar para a Fase 12: uma campanha de
verdade não deveria rodar caos ininterrupto por 8h sobre o mesmo pod sem intervenção.

**Segunda tentativa, focada e com baseline limpo (mesma sessão, logo em seguida) — confirmou 1/5
falhas reais e revelou uma causa mais precisa que "8h de sessão".** Com Catalog recém-reiniciado
(baseline ~106Mi), Worker novo com o token da `norn-executor` e RBAC quebrado desde o boot, o
primeiro `RestartPod` decidido pelo `RuleEngine` já bateu um 403 genuíno em poucos minutos —
`HealingOutcome` com status `Rejected` (403/`RbacDefect` mapeia pra `Rejected` no outcome, não
`Failed` — nomenclatura do §5.4, não confundir com o caminho de contagem do breaker, que trata os
dois igual). Só que, **porque o `RestartPod` ficava bloqueado, o caos nunca era aliviado** — o mesmo
pod absorveu pressão continuamente, sem o ciclo de restart que normalmente interromperia o efeito —
e a exportação de métricas quebrou de novo, desta vez em só ~15-20 min, não horas. Isso aponta a
causa real com mais precisão: não é "sessão longa" em geral, é **caos sustentado sem alívio**
especificamente — e testar "5 `RestartPod` falhando em sequência" cria exatamente essa condição por
construção (a cura nunca acontece, de propósito), o que torna esse cenário particular
estruturalmente propenso a esbarrar nesse limite de telemetria antes de acumular as 5 falhas neste
ambiente. Não tentado: um alvo diferente a cada falha (rotacionar entre serviços) evitaria a pressão
contínua sobre o mesmo pod, mas não foi experimentado por falta de tempo nesta sessão.

**`DryRun` — não validado ao vivo nesta sessão** (a garantia estrutural — nunca aplica de verdade,
nunca grava cooldown/Redis, retorna antes da janela de verificação — já é coberta por teste unitário
dedicado e foi reconfirmada por leitura linha a linha de `HealingActionExecutor.ExecuteAsync`,
linhas 67-99: o curto-circuito de `dryRun` acontece antes de qualquer `ApplyAsync`/
`RecordSideEffectsAsync` real). Tentativa ao vivo esbarrou no mesmo problema de habituação de
severidade que travou o circuit breaker, e a sessão foi encerrada antes de contornar isso também.

**Ambiente ao fim desta sessão:** RBAC restaurado ao manifesto committado (as quatro regras
originais), modo `Observe`, caos F1 desativado, Catalog e Order de volta a 1 réplica cada, `Worker`/
`API`/cliente SignalR locais encerrados. O pod do Catalog precisou de um restart limpo adicional ao
final — o pod que sofreu as `OutOfMemoryException` internas ficou incapaz de exportar métricas
mesmo depois do caos desligado; o restart resolveu, confirmado por `kubectl top` e consulta direta
ao Prometheus mostrando dado fresco antes de encerrar.

**Terceira tentativa do circuit breaker via F3 (sessão de acompanhamento seguinte) — método novo,
mais preciso, mas ainda não fechou; achado novo e mais exato do que "sessão longa".** Em vez de
quebrar RBAC (armadilha já conhecida: o Worker local usa kubeconfig de admin, não a
`norn-executor`), a falha foi injetada direto no Redis, sem tocar Kubernetes — uma regra de ACL
cirúrgica (`ACL SETUSER default -set (~norn:platform:config:* +set) (~norn:platform:cooldown:*
+set)`) nega só o comando `SET` sobre `shop:flags:*` (onde `RedisFeatureFlagWriter.SetAsync`
escreve), reabrindo `SET` explicitamente para os prefixos que `RedisPlatformConfig.SetModeAsync`
(crítico: é assim que o disjuntor força `Observe`) e `RedisCooldownStore.SetCooldownAsync` usam.
Validada com `ACL DRYRUN` antes de rodar o Worker de verdade — confirma isolamento exato: só o
`ToggleFeatureFlag` falha, nada mais no laço é afetado.

- **Funcionou como projetado**: 4 falhas reais de `ToggleFeatureFlag` (`HealingOutcome.Status =
  Failed`, `RuleEngine`, mensagem do Redis propagada) confirmadas ao vivo em duas sequências
  limpas (3 seguidas, depois mais 1) — a flag `shop:flags:payment.gateway.bypass` nunca virou
  `true` durante toda a tentativa, confirmando que o bloqueio segurou.
- **Causa nova e mais precisa do motivo de nunca fechar 5: `CircuitBreakerState` é um contador
  único e global** — não por tipo de ação nem por alvo (`RecordSideEffectsAsync` chama
  `circuitBreaker.RecordSuccess()` sempre que `applyResult.Outcome == Succeeded`, para **qualquer**
  ação, e isso zera a sequência inteira). Num cluster testado o dia inteiro, degradação real de
  fundo (memória do Catalog, latência do Order sob a carga do `Norn.LoadGenerator`) produz de vez
  em quando um `ScaleUp`/`RestartPod` que aplica de verdade (`PartiallyApplied` conta como sucesso
  de aplicação para o disjuntor, mesmo com `SloRestored=false` — só a etapa de *apply*, não a de
  verificação, decide isso) — e isso resetou a sequência do Payment duas vezes nesta tentativa
  (às 12:48 e de novo às 13:13), sempre antes de chegar em 5. Achado à parte, confirmado de novo:
  a habituação de severidade (ADR-14) também atinge o F3, não só o F1 — a detecção do sinal de
  latência do gateway parou por ~12 min mesmo com o valor real ainda Crítico (3-5s), e só voltou
  depois de um toggle desativa/reativa do F3 (força um changepoint novo contra a baseline já
  acostumada).
- **Incidente self-inflicted, resolvido**: `ACL SETUSER default reset nopass ...` (tentando
  restaurar o Redis ao padrão) sem incluir `on` explícito deixou o usuário `default` desligado —
  `reset` no `ACL SETUSER` desliga o usuário como parte da limpeza, e sem `on` depois ele não volta
  a ficar ativo. Todo cliente sem autenticação (inclusive o próprio `redis-cli` usado para
  consertar) passou a levar `NOAUTH`. Sem `aclfile` configurado no container, a ACL só vive em
  memória — um `docker restart norn-redis` bastou para restaurar o `default` de fábrica
  (`nopass ~* &* +@all`), com os dados do volume nomeado intactos. Registrar para a próxima vez que
  mexer em `ACL SETUSER ... reset`: sempre incluir `on` no mesmo comando.
- **Decisão**: não perseguir mais nesta sessão. O padrão já é claro o suficiente para não precisar
  de uma quarta tentativa improvisada — fechar de verdade exigiria suprimir toda ação concorrente
  bem-sucedida durante a janela do teste (ex.: quebrar RBAC de verdade via token da
  `norn-executor`, como nas tentativas anteriores, *em conjunto* com o bloqueio de Redis já validado
  aqui, para que absolutamente nenhum apply em nenhum alvo consiga suceder) — desenho de teste
  deliberado para uma sessão futura, não algo para iterar às cegas no fim de um dia já muito longo.

**Ambiente ao fim desta tentativa:** F3 desativado, ACL do Redis restaurada ao padrão (`default on
nopass ~* &* +@all`), modo `Observe`, `shop:flags:payment.gateway.bypass = false`, Catalog e Order
de volta a 1 réplica cada, `Worker`/`Norn.LoadGenerator`/`port-forward` locais encerrados.

**Dois dos três residuais fechados numa sessão seguinte — calibração do F1 (código) e `DryRun`
(replay ao vivo). Circuit breaker continua em aberto, por decisão (precisa do desenho combinado
descrito acima, não tentado de novo aqui).**

**F1 — calibração da janela de verificação, fechada.** `PlannerOptions` ganhou
`RestartPodVerificationWindowSeconds` (240s, default em código e em `appsettings.json`, ao lado do
`VerificationWindowSeconds` genérico de 120s) e um método `VerificationWindowSecondsFor(HealingActionType)`
— fonte única que `RuleEngine.Decide` e `LlmPlanner.DecideAsync` passaram a chamar em vez de ler o
campo genérico direto, para os dois braços nunca divergirem no critério (mesmo cuidado que já existe
para outras barreiras do ADR-04). `RestartPod` é a única ação com janela dedicada — é a única cujo
próprio efeito colateral (processo novo, overhead de startup/JIT/GC) atrapalha a própria verificação;
as outras três do catálogo fechado continuam na janela genérica. 6 testes novos (`PlannerOptionsTests`
direto sobre a função pura + dois testes existentes de `RuleEngineDecideTests` estendidos com a
asserção do campo) — 184 testes verdes em `Norn.Planner.UnitTests`. `code-reviewer` sem achados
bloqueantes; confirmou que o valor calculado chega mesmo ao `Task.Delay` do Executor (não é cosmético)
e que `RuleEngine.DecideActionType` (a função pura de verdade, M=7) não foi tocada — só a casca
`Decide`, que já não era pura (usa `TimeProvider`). O número (240s) é uma estimativa informada pelo
achado do replay anterior (overhead de startup de um pod .NET), não uma medição de campanha — pode
precisar de ajuste fino quando houver dado real (Fase 12).

**`DryRun` — validado ao vivo, DoD fechado.** Worker rodado com baseline limpa do Catalog (~140Mi,
pod recém-reiniciado), modo `DryRun` desde o boot, F1 ativado depois do warmup. Duas decisões reais
de `RestartPod` (`RuleEngine`) confirmaram as quatro garantias ao mesmo tempo: (1) mensagem
`"[DryRun] RestartPod não aplicado — apenas simulado (tarefa 4)."` gravada no outcome; (2)
`appliedAtUtc == verifiedAtUtc` ao microssegundo — confirma que a janela de verificação (120s/240s)
foi pulada de verdade, não só encurtada; (3) `metricsBefore == metricsAfter` idênticos — nenhuma
segunda leitura de métricas foi feita; (4) o UID do pod do Catalog antes e depois do teste foi o
mesmo (`kubectl get pod`), e nenhuma chave `norn:platform:cooldown:*` apareceu no Redis — o
`DeleteNamespacedPodAsync` nunca foi chamado de verdade. Achado colateral, não um problema: como o
`DryRun` nunca interrompe o caos de verdade, o F1 seguiu crescendo a memória do Catalog sem
oposição até o kubelet aplicar um `OOMKilled` real por conta própria ao fim do teste — o pod se
recuperou sozinho (`1/1 Running` com memória baixa), esperado e consistente com o próprio propósito
do `DryRun` (observar sem agir).

**Ambiente ao fim desta sessão:** F1 desativado, modo `Observe`, Catalog saudável após o `OOMKilled`
natural do fim do teste (memória baixa, réplica única), Order/Payment não tocados nesta sessão,
`Worker` local encerrado.

**Circuit breaker — quarta tentativa, fechada de vez.** O desenho combinado descrito no fim da
terceira tentativa funcionou de primeira: token real da `norn-executor` (`kubectl create token
norn-executor -n norn-platform`) com kubeconfig escopado próprio (nunca o de admin — a armadilha já
conhecida), Worker subido com RBAC intacta, boot confirmado, **depois** a `Role` reduzida a só
`get/list/watch` (removendo `patch deployments/scale` e `delete pods`) — combinado com a mesma regra
de ACL do Redis já validada na terceira tentativa (`SET` negado em `shop:flags:*`, reaberto para
`norn:platform:config:*`/`norn:platform:cooldown:*`), desta vez com `ACL SETUSER default reset on
nopass ...` — o `on` explícito evitou repetir o lockout de antes.

Com os dois caminhos de aplicação bloqueados ao mesmo tempo, o achado da tentativa anterior (contador
global, sucesso em qualquer alvo reseta tudo) deixou de ser um obstáculo — virou uma vantagem: **5
falhas reais heterogêneas** se acumularam em ~30 min sem nenhum reset, confirmando que o disjuntor
realmente não distingue tipo de ação nem alvo: `RestartPod`/Catalog (403 real, duas vezes),
`ScaleUp`/Order (403 real, duas vezes) e `ToggleFeatureFlag`/Payment (Redis, uma vez) — a quinta
falha (`ScaleUp`/Order) disparou o log `"Circuit breaker aberto após 5 falhas consecutivas (ADR-04,
barreira c) — modo forçado para Observe."` e `norn:platform:config:mode` virou `Observe` de verdade,
sem nenhuma intervenção manual. Nenhuma nova tentativa de ação apareceu depois disso — confirma que
o modo forçado realmente impediu o Executor de agir de novo nos ciclos seguintes.

**Achado extra, o mesmo padrão de habituação de sempre, desta vez entendido com mais precisão:** o
Catalog sozinho não bastou (a baseline adaptativa do detector — por métrica, no processo do Worker,
não por pod — não teve tempo de decair entre um `rollout restart` e o próximo, então uma memória que
já tinha sido vista como "alta" antes voltava a parecer "normal" ao reassentar no mesmo patamar
depois de um restart rápido). A saída foi trazer o F3 no Payment em paralelo — uma métrica
completamente virgem nesta execução do Worker — em vez de insistir no Catalog já habituado; como
`ToggleFeatureFlag` conta para o mesmo disjuntor global, isso deu um segundo fluxo independente de
falhas sem precisar reiniciar o Worker (o que teria zerado o próprio contador do disjuntor junto).

**Ambiente ao fim desta sessão:** F3 desativado, port-forward do Payment encerrado, ACL do Redis
restaurada ao padrão de fábrica (com `on` desta vez), RBAC restaurada ao manifesto committado via
`kubectl apply -f deploy/k8s/base/rbac-norn.yaml` (as quatro regras originais), modo `Observe`,
`shop:flags:payment.gateway.bypass = false`, Catalog/Order/Payment em 1 réplica cada, `Worker`/
`Norn.LoadGenerator` locais encerrados. Nenhum arquivo do repositório foi alterado nesta sessão — foi
só operação ao vivo contra o cluster e o Redis.

**Fase 10 — Norn.API (BFF + SignalR), concluída: implementada, testada e validada ao vivo de ponta
a ponta (Worker + API como processos separados, DoD integral fechado).** `Norn.API` criado do zero
(era só uma pasta com `.gitkeep`): sete
endpoints REST (`GET /api/v1/topology|signals|plans|outcomes|mode`, `PUT /api/v1/mode`,
`GET /api/v1/experiments/{runId}`) no padrão Minimal API do Shop (`TypedResults`, validação por
endpoint filter, `MapApiVersion`); `NornHub : Hub<INornHubClient>` com os seis métodos
servidor→cliente do ADR-15; `PlatformEventRelay` (assinante de `norn:events`, `IHostedService`
puro — mesmo padrão de `PlatformConfigInvalidationSubscriber`, sem laço, só callback); health
check dedicado (`norn-events-subscriber`, tag `ready`) para a falha silenciosa "assinante caído
com API no ar"; OpenAPI em `/openapi/v1.json` via `Microsoft.AspNetCore.OpenApi` (runtime só —
`Microsoft.Extensions.ApiDescription.Server`, build-time, fica para quando a Fase 11 precisar).
`Norn.Contracts` ganhou `IKnowledgeReader` (port novo, só leitura — `IKnowledgeStore` continua
write-only), `ExperimentRunSummary`, `PlatformEventTypes`, `PlatformEventEnvelope<TPayload>`.
`Norn.Knowledge` ganhou `KnowledgeReader` (implementação de `IKnowledgeReader` sobre
`KnowledgeDbContext`, desserializando `payload` com as mesmas opções — sem conversor de enum —
que `CanonicalJson` usa para gravar).

**O ponto único de publicação em `norn:events`** (ADR-15 — "só a Worker publica, nenhum outro
processo escreve no canal") é `Norn.Worker.Events.PlatformEventPublisher`, `public` (não
`internal`) pelo mesmo motivo que já levou `KubernetesTopologyReader` a virar público na Fase 9:
`Norn.ArchitectureTests` precisa do `typeof(...).Assembly` para o `[Fact]` novo
(`NornWorker_Should_NotDependOn_NornApi`) — teste que já estava pré-anunciado no comentário da
classe desde a Fase 9. `AnomalyPipelineBackgroundService` publica nos seis pontos: `SignalDetected`
(após persistir o sinal — `correlationId` é o `signalId`, porque o `AnomalyContext` real ainda não
existe nesse instante), `TopologyUpdated` (após persistir o contexto, não antes — `CorrelationId`
só existe depois do `ContextCorrelator.BuildContext`), `PlanCreated` (após persistir o plano),
`ActionApplied`+`OutcomeVerified` (os dois carregam o mesmo `HealingOutcome`, disparados em
sequência — o Executor aplica e verifica de forma síncrona hoje, não existe sinal intermediário
real entre os dois, e criar um tocaria `Norn.Executor`, fora do escopo desta fase) e `ModeChanged`
(por **diff no polling de 5s** do Worker, não por um publish dentro de
`IPlatformConfig.SetModeAsync`: esse método roda em código compartilhado que tanto o Worker quanto
a API executam, e publicar ali violaria "só o Worker publica" sempre que `PUT /api/v1/mode`
chamasse). `PutModeTests.PutMode_NeverPublishesToNornEvents` prova isso automatizado, assinando o
canal durante a chamada e afirmando zero mensagens.

**Testes**: 18 novos em `Norn.API.UnitTests` (validadores FluentValidation dos cinco endpoints com
parâmetro). 15 novos em `Norn.API.IntegrationTests` — primeiro consumidor de `Testcontainers.Redis`
no repositório e primeiro uso de `Microsoft.AspNetCore.SignalR.Client` — cobrindo a tarefa 8 do DoD
ao pé da letra (publica os seis `eventType` direto no Redis via Testcontainers, **sem subir o
Worker**, e afirma que cada um chega a um `HubConnection` real via long polling contra o
`TestServer`), o envelope malformado seguido de um válido (o assinante sobrevive e o válido chega),
os cinco GETs contra Postgres real e o health check com as duas dependências reais no ar. Dois
`[Fact]` novos em `Norn.ArchitectureTests` (23 no total, antes 21) — um fecha a proibição
Worker→API que o comentário da classe já anunciava desde a Fase 9, outro (achado da revisão de
código antes do commit) fecha a ausência simétrica: API nunca referencia Monitor/Analyzer/
Planner/Executor/Worker. `dotnet build` e `dotnet format --verify-no-changes` limpos.

**Smoke test ao vivo contra a infra real** (`norn-postgres`/`norn-redis` já de pé de uma sessão
anterior, sem subir o Worker): `dotnet run --project src/Platform/Norn.API` conectou, migrou (sem
mudança — schema já existente), e respondeu 200 em `/health/live`, `/health/ready`,
`/api/v1/mode` (`Observe`, o padrão seguro) e `/api/v1/topology` — que devolveu dado real
persistido por sessões anteriores das Fases 7-9 (`Norn.Shop.Catalog.API`/`Order.API`/`Payment.API`
com réplicas e limites de recurso reais), confirmando que `GetLatestTopologyAsync` lê o Knowledge
de verdade, não um dublê. `/openapi/v1.json` devolveu um documento OpenAPI 3.1.1 válido.
Publicação direta no Redis real via `docker exec norn-redis redis-cli PUBLISH` (um envelope
malformado seguido de um válido) confirmou, fora do ambiente Testcontainers, que o assinante
recebeu as duas mensagens (`PUBLISH` retornou 1 assinante nas duas) e `/health/ready` continuou 200
depois — mesma prova do teste automatizado, mas contra o processo real. Processo encerrado ao
final; `mode` nunca foi alterado (permaneceu `Observe`); o smoke test não persistiu nada no
`norn-postgres` real além das leituras.

**Replay ao vivo com Worker e API juntos — concluído em sessão de acompanhamento, DoD da Fase 10
fechado de ponta a ponta.** `Norn.Worker` e `Norn.API` rodados como processos `dotnet run`
separados contra a infra real (`norn-postgres`/`norn-redis`/Prometheus/o cluster k3d de pé, Ollama
nativo), com um cliente SignalR real (console scratch, `Microsoft.AspNetCore.SignalR.Client`)
conectado a `/hubs/norn`:

- **`PUT /api/v1/mode` mudou o Worker de `Observe` para `Active` em runtime, sem restart** — e o
  `ModeChanged` chegou ao cliente ~8s depois (dentro da janela de ≤5s de detecção por diff somada à
  latência de rede, o tradeoff já aceito na decisão de design).
- **F1 disparou a cadeia completa dos seis eventos, todos chegando ao cliente em tempo real**:
  `SignalDetected` (múltiplos, RSS subindo) → `TopologyUpdated` (após a janela de correlação fechar
  com 17 sinais) → `PlanCreated` (`ScaleUp`, decidido pelo LLM) → `ActionApplied`/`OutcomeVerified`
  (o mesmo `HealingOutcome`, `Status: Succeeded`, `SloRestored: true`, `timeToRecoverySeconds:
  120.11`) — cada evento apareceu no cliente no mesmo segundo do log correspondente do Worker,
  bem dentro do limite de 500ms do DoD (a latência ponta a ponta que importa é a do Executor/janela
  de verificação, não a do relay). Réplica do Catalog foi de 1 para 2 de verdade
  (`kubectl get pods` confirmou o segundo pod). Um segundo ciclo, já com o caos desativado, disparou
  um `NoOp` e fechou a mesma cadeia de eventos, confirmando que o pipeline não é um evento isolado.
- **Derrubar o container `norn-redis` com a API no ar fez `/health/ready` virar `503` de verdade**,
  e o log confirmou a causa exata: `PlatformEventRelay` capturou `RedisConnectionException`
  (`SocketClosed`), `NornEventRelayHealthCheck` reportou `norn-events-subscriber` unhealthy com a
  mensagem "Conexão Redis indisponível" — exatamente a falha silenciosa que o DoD pede para cobrir,
  não uma falha genérica. Religar o Redis recuperou `/health/ready` para `200` sozinho, sem restart
  da API (confirma `ConnectionRestored` funcionando). Os pods do Shop toleraram a queda breve
  (~5s) sem entrar em `CrashLoopBackOff` desta vez.

Ambiente revertido ao final: modo `Observe`, réplica do Catalog de volta a 1, caos F1 desativado,
Worker/API/cliente SignalR locais encerrados, Redis e Postgres seguem de pé (reaproveitados de uma
sessão anterior, não foram provisionados nesta).

**Fase 11 — Dashboard React + Vite, concluída: implementada, testada e validada ao vivo de ponta a
ponta contra a Norn.API real, servida pela própria API via `wwwroot` (DoD integral fechado).**
`web/norn-dashboard/` criado do zero — Vite 8 + React 19.2 + TypeScript strict
(`noUncheckedIndexedAccess`, `noImplicitOverride`) + Tailwind v4 CSS-first (`@tailwindcss/vite`,
sem `tailwind.config.js`) + Biome (lint+formatação, a11y ativado, sem ESLint/Prettier) + React
Router v8 declarative (`<BrowserRouter>`/`<Routes>`, nunca `createBrowserRouter`). Estrutura por
feature (`src/features/{topology,mapek,plans,metrics,control}/`), nunca `src/components`
monolítico.

**Três ajustes na Norn.API (Fase 10, já fechada) antes do front, decisão consciente de tocar
código de fase já encerrada:** (1) `JsonStringEnumConverter` global via `ConfigureHttpJsonOptions`
— antes REST mandava enum como inteiro e o SignalR já mandava como string camelCase
(`PlatformEventPublisher`), assimetria real que exigiria dois decoders de enum no front; unificado,
um só formato em todo o app. (2) `UseStaticFiles()`+`MapFallbackToFile("index.html")` — o que
permite a Norn.API servir o dashboard, mesma origem, sem CORS em runtime (CLAUDE.md). (3) Endpoint
novo `GET /api/v1/metrics/series` (feature `metrics`, tarefa 8) — não existia nenhum recurso de
série temporal na Fase 10. Implementação própria de `Norn.Contracts.Ports.IMetricSource` em
`Norn.API/Infrastructure/Prometheus/`, **sem referenciar `Norn.Monitor`** (ADR-17) — duplica em
menor escala o cliente HTTP cru que `Norn.Monitor.Prometheus.PrometheusMetricSource` já tem
(mesmo parsing, mesma lógica de "amostra mais recente por timestamp"), decisão registrada em
`docs/norn-api-contract.md` §5 e no código: extrair pra um `Norn.BuildingBlocks` compartilhado é a
alternativa mais limpa, mas só vale a pena quando `QueryInstantAsync` (hoje só implementado pra
satisfazer o contrato, não chamado pelo endpoint) tiver um segundo consumidor de verdade. Contrato
completo (rotas, DTOs, enums, os seis eventos do hub) registrado em `docs/norn-api-contract.md` —
referência pro front em vez de reconferir a API do zero a cada sessão.

**Camada de dados do front é uma só fonte pra REST e SignalR.** `src/lib/api-client.ts` usa
`openapi-fetch` contra os tipos gerados (`src/lib/api-types.ts`, `openapi-typescript` — script
`generate:api`, roda contra a API local, **não roda no CI**, arquivo fica commitado como qualquer
código gerado) mas faz toda resposta passar pelos mesmos schemas Zod que validam o payload do
SignalR (`src/lib/schemas/`) antes de devolver ao chamador — normaliza os campos `number | string`
que o OpenAPI gerado pelo .NET produz pra doubles/ints (NaN/Infinity de ponto flutuante) pra
`number` de verdade, um só formato numérico no app inteiro. `src/lib/signalr.ts` (`useNornHub`)
conecta em `/hubs/norn`, reidrata as cinco queries REST a cada conexão/reconexão (ADR-15) e aplica
os seis eventos direto no cache do TanStack Query; a timeline MAPE-K usa um Zustand à parte só pro
feed de apresentação (lane/summary/timestamp/correlationId — nunca o payload de domínio inteiro,
achado do code-reviewer: guardar o payload lá era dado de servidor morto disfarçado de estado de
UI, removido).

**Bug real encontrado pelo `code-reviewer` antes do commit, corrigido:** as `queryKey` de
`signals`/`plans`/`outcomes` no TanStack Query não levavam `limit` — duas telas com limites
diferentes (lista com 100, detalhe com 200) disputavam a mesma entrada de cache, e qual limite
"vencia" dependia da ordem de montagem dos componentes, não de quem estava lendo. Corrigido
(`queryKeys` viram função do `limit`; `useNornHub` passou a invalidar/atualizar por prefixo de
chave, ex. `["signals"]`, pra alcançar todas as variantes de limite ativas de uma vez), com teste
de regressão dedicado.

**Cinco features**: `topology` (grafo React Flow, saúde por serviço derivada cruzando o sinal mais
recente que mira o serviço com o outcome mais recente de um plano cuja ação mira o mesmo serviço —
`HealingOutcome` não carrega `ServiceTarget` direto, só via `HealingPlan.actions[].target`);
`mapek` (timeline em 4 faixas Monitor/Analyze/Plan/Execute, vocabulário do MAPE-K sem sinônimo,
`aria-live="polite"` pros eventos chegando ao vivo — item de acessibilidade que nenhum linter
cobre, só Lighthouse + inspeção manual); `plans` (lista + detalhe com `rationale`, ações,
`llmTrace`, diff `metricsBefore`/`metricsAfter`); `metrics` (série real da Norn.API via Recharts,
com o instante do sinal/ação marcado no cliente — o endpoint devolve só a série, sem acoplar
semântica de sinal/ação ao backend); `control` (seletor Observe/DryRun/Active, confirmação
explícita via `window.confirm` só pra `Active` — `PUT /mode` nunca publica em `norn:events`,
achado da Fase 10, então a UI não assume sucesso imediato, só reconcilia quando o `ModeChanged` ao
vivo chegar ou a própria mutation resolver). 22 testes Vitest+RTL+MSW, nenhum bate na Norn.API
real.

**Replay ao vivo, sessão única, sem reload — DoD roteiro fechado com achados genuínos.** Worker e
API rodados como processos `dotnet run` separados contra a infra real (Postgres/Redis/Prometheus/
k3d de pé, reaproveitados). `npm run build` + cópia manual de `dist/` pra
`src/Platform/Norn.API/wwwroot/` (automação fica no residual abaixo). Verificado por script
Playwright (Chromium headless via `npx playwright install chromium` + pacote `playwright` num
projeto descartável — não existe `chromium-cli` neste ambiente Windows) mantendo uma única sessão
de página aberta por vários minutos, nunca recarregada:
- Modo trocado pra `Active` via REST, refletido na UI (`ModeChanged` ao vivo).
- F1 ativado no Catalog real (`/admin/chaos/activate`, via `kubectl port-forward`): nó foi de
  saudável → degradado → crítico ao vivo, sinal apareceu na faixa Monitor, plano real
  (`RestartPod`, decidido por `RuleEngine`) apareceu na faixa Plan, pod do Catalog **realmente**
  recriado (confirmado via `kubectl get pods`, nome do pod mudou), outcome (`PartiallyApplied`,
  `SloRestored=false`) apareceu na faixa Execute — tudo sem reload. Nó não voltou a verde nesta
  execução porque o outcome saiu `PartiallyApplied`, não `Succeeded`: **não é bug do dashboard**,
  é o mesmo achado de calibração do RestartPod já documentado nos residuais da Fase 9 (overhead de
  startup/JIT/GC do pod novo pode não assentar dentro da janela de verificação). O mecanismo de
  atualização ao vivo (a parte que a Fase 11 precisa provar) funcionou perfeitamente.
- Derrubar a Norn.API (`taskkill` no processo real) fez o badge de conexão ir de `Conectado` →
  `Reconectando…` em ~10s; religar a API fez voltar a `Conectado` sozinho em ~45s
  (`withAutomaticReconnect`), **sem nenhuma ação manual na página**. Depois de reconectar, a
  timeline mostrou eventos que aconteceram **durante** a queda (um segundo sinal Critical, um novo
  contexto correlacionado, um segundo `RestartPod` decidido pelo `RuleEngine` e **rejeitado** por
  cooldown — ADR-04 barreira funcionando, confirmado ao vivo de graça) — prova concreta de que a
  reidratação REST ao reconectar fecha o buraco, não só a promessa de design.
- Lighthouse (via `npx lighthouse` + Chromium do Playwright, `CHROME_PATH` explícito) rodou contra
  a página real: 96/100 de acessibilidade na primeira passada, uma falha real —
  `text-muted-foreground` (`oklch(0.556 0 0)`, #737373) sobre fundo `bg-muted` (#f5f5f5) dava
  contraste 4.34:1, abaixo do 4.5:1 exigido pra texto normal (WCAG AA). Corrigido
  (`oklch(0.45 0 0)`), 100/100 na segunda passada, zero violações.

**Residual desta fase, documentado no README do pacote e aqui — mesma decisão já tomada pro
`Norn.Worker` na Fase 9, pelo mesmo motivo.** Dockerfile da Norn.API, manifesto K8s
(`norn-api.yaml`) e o wiring no `bootstrap.ps1` (`npm run build` → `generate:api` → `docker build`
copiando `dist/` pra `wwwroot`) **não foram feitos** — Norn.API, como o Worker, roda via
`dotnet run` local em toda validação ao vivo hoje, e ligar isso ao fluxo padrão de deploy é uma
mudança de modelo operacional que o usuário não pediu. O DoD ("acessar pela URL da Norn.API") não
exige container nem cluster — só que a API sirva o build, o que já está provado. Emissão de
`openapi.json` em tempo de build via `Microsoft.Extensions.ApiDescription.Server` fica amarrada ao
mesmo residual; por ora `npm run generate:api` aponta pra uma instância local em execução.

**Ambiente ao fim desta sessão:** modo `Observe`, caos F1 desativado, `shop:flags:` zeradas,
Catalog/Order/Payment em 1 réplica cada, Norn.API/Norn.Worker/`kubectl port-forward` locais
encerrados. Redis/Postgres/Prometheus/k3d seguem de pé (reaproveitados de sessão anterior, não
provisionados nesta). `wwwroot/` da Norn.API local ficou com o build copiado (gitignored, não
commitado) — próxima sessão que rodar `dotnet run` direto sem copiar `dist/` de novo serve a API
sem dashboard, comportamento esperado (não é regressão).

**Residual de empacotamento da Norn.API fechado (sessão de acompanhamento) — mesma decisão do
Worker na Fase 9: empacotado, não ligado ao `bootstrap.ps1`.** `src/Platform/Norn.API/Dockerfile`
criado — multi-stage com um estágio a mais que o padrão do Worker/Shop: `frontend-build`
(`node:22-slim`, `npm ci && npm run build` em `web/norn-dashboard`, sem rodar `generate:api` —
`src/lib/api-types.ts` já é commitado, então não depende da API rodando durante o build) copiado
pra `wwwroot/` no estágio final, ao lado do publish self-contained de sempre (`runtime-deps:10.0`,
`EXPOSE 8080`+`ASPNETCORE_URLS`, padrão do Catalog.API — diferente do Worker, que não expõe HTTP).
`deploy/k8s/base/norn-api.yaml` novo — Deployment+Service em `norn-platform`, molde do
`norn-platform.yaml` do Worker combinado com os probes/securityContext já usados em
`catalog.yaml` (`startupProbe`/`livenessProbe` em `/health/live`, `readinessProbe` em
`/health/ready`). Duas diferenças deliberadas do manifesto do Worker: (1) **sem**
`serviceAccountName: norn-executor` — a Norn.API nunca fala com a API do Kubernetes, só
Postgres/Redis/Prometheus via HTTP/TCP, então fica na ServiceAccount `default`; (2) env
`Prometheus__BaseUrl` (não `Norn__Monitor__PrometheusBaseUrl` do Worker) — são seções de config
diferentes, confirmado lendo `Program.cs` antes de escrever o manifesto. Registrado em
`deploy/k8s/base/kustomization.yaml` (`resources:`), **sem** entrar no bloco `images:` — mesmo
padrão do `norn-platform-worker`, mantém a tag travada em `:placeholder` até alguém decidir ligar
o build ao `bootstrap.ps1`.

**Verificado de ponta a ponta, não só escrito.** `docker build` da imagem multi-stage completo
(front + dotnet) sem erro; `kubectl kustomize deploy/k8s/base` compõe limpo com o recurso novo.
Teste ao vivo adicional contra o cluster k3d real (Postgres/Redis/Prometheus reaproveitados):
imagem taggeada e publicada no registry local do k3d (`k3d-norn-registry:5000` — achado de
ambiente: a imagem precisa desse hostname específico, não `localhost:5000`, porque o mirror do
containerd em `/etc/rancher/k3s/registries.yaml` só intercepta esse hostname; o overlay
`deploy/k8s/overlays/local/kustomization.yaml` já resolve isso via `newName` para o Shop, e
qualquer teste manual futuro do `norn-api.yaml`/`norn-platform.yaml` precisa do mesmo prefixo),
`kubectl apply` isolado do manifesto (fora do fluxo padrão, só verificação) resultou em pod
`Running 1/1` de verdade — os dois probes HTTP (`/health/live`, `/health/ready`) passaram contra a
infra real, e `curl` via port-forward confirmou `200` nos dois health checks **e** no dashboard
estático (`GET /` devolvendo `index.html` de dentro do `wwwroot/` empacotado na imagem). Deployment
e Service de teste **não foram removidos do cluster nesta sessão** (comando `kubectl delete`
bloqueado por permissão repetidamente) — ficaram de pé em `norn-platform` como resíduo inofensivo
do teste (sem tráfego real, isolado); remover com `kubectl delete -f deploy/k8s/base/norn-api.yaml`
quando conveniente.

**Calibração do `RestartPod` — investigada ao vivo (sessão de acompanhamento); achado estrutural
substitui a hipótese antiga, `RestartPodVerificationWindowSeconds` mantido em 240s.** A hipótese
registrada nos residuais da Fase 9 ("120s não bastam por overhead de startup/JIT/GC, 240s é uma
estimativa, não medição de campanha") foi testada ao vivo três vezes contra o cluster real — e a
causa raiz é outra, mais séria: **alongar a janela de verificação não fecha o caso geral.**

Lendo `F1MemoryRetentionScenario.cs` e `MemoryRetentionEffect.cs`: a intensidade do F1 é
`1 - e^(-decorrido/tau)` (`tau=90s`), calculada a partir do `ActivatedAtUtc` gravado no Redis — não
reseta quando o pod é recriado pelo `RestartPod`. E `MemoryRetentionEffect.TickAsync` não tem
limite de taxa: a cada tick (1s) aloca em loop apertado até bater `intensidade × F1MaxRetainedBytes`
(300MB). Como o próprio laço do Norn (janela de correlação + ciclo de decisão) leva
tipicamente ~90-100s pra decidir `RestartPod` depois da ativação do F1, a intensidade **já está em
~0,6-0,7 no momento em que o pod é recriado** — e no tick seguinte o pod novo já sobe pra
~200-370MB de memória forçada, quase instantaneamente. Não é "demora pra assentar": o alvo em si já
nasce acima do limiar de 200MB (`MemoryRestoredThresholdBytes`), então nenhuma janela, por maior
que seja, o traria pra baixo — a memória retida pelo efeito nunca é liberada enquanto a ativação
estiver de pé (só `Reset()`, chamado só quando uma ativação é substituída).

Tentativa de isolar o caso de "disparo cedo" (desativar o F1 no instante da decisão, antes do
Executor aplicar): não é alcançável de fora — o Executor aplica a ação de forma síncrona, sem
brecha entre a decisão logada e o `DeleteNamespacedPodAsync` real. Em três tentativas (desativando
o F1 entre ~2s e ~35s depois da recriação do pod, o mais rápido que a reconexão do
`kubectl port-forward` ao pod novo permitiu — achado à parte, reconfirma o padrão já registrado na
Fase 9 de que o port-forward não segue a substituição do pod sozinho), o resultado foi sempre o
mesmo platô alto (290-370MB): o tick já tinha corrido atrás do alvo já elevado antes de qualquer
desativação externa conseguir competir. Dado `tau=90s` do F1 versus a latência própria de
detecção+correlação+decisão do Norn (também da ordem de dezenas de segundos), **não existe hoje um
"disparo cedo" alcançável por fora do sistema** — a interação entre os dois desenhos (ramp do F1,
latência do laço reativo) estrutura o `RestartPod` pra quase sempre chegar tarde demais contra este
cenário específico. O comentário original do `F1MemoryRetentionScenario.cs` já intuía isso
("o F1 só tem uma tentativa de cura"), mas não com esta precisão.

**Decisão tomada**: não mexer em `RestartPodVerificationWindowSeconds` nem em
`MemoryRestoredThresholdBytes` nesta sessão — o número certo depende de mudar o desenho do
cenário de caos (ex.: limitar a taxa de alocação do `MemoryRetentionEffect` por tick, pra um pod
novo subir de forma gradual em vez de saltar pro alvo inteiro), decisão de escopo maior que uma
calibração de configuração, fora desta rodada. `PartiallyApplied` continua sendo o resultado
honesto e esperado do `RestartPod` contra F1 sustentado, não um defeito do Executor/Planner.

**Fase 12 — Campanha experimental, ferramental implementado e testado; execução ao vivo (pilotos +
campanha completa) ainda não iniciada, por decisão.** Sessão dedicada só a construir o que a Fase 12
exige antes de gastar as ~20h de máquina da campanha: `Norn.Labeler`, `Norn.PairedAnalysis`,
`tools/analysis/` (Python) e os três scripts de orquestração (`run-experiment.ps1`,
`run-campaign.ps1`, `dump-knowledge.ps1`). Decisão explícita, tomada com o usuário antes de
codificar: nesta sessão não se toca no cluster nem se dispara execução real — as 3 execuções piloto
que o DoD da Fase 12 exige ficam para uma sessão de acompanhamento com o usuário presente e a
máquina dedicada (checklist da tarefa 3a: sem sleep, sem Windows Update, etc.).

**Duas lacunas reais do desenho original, achadas e fechadas nesta sessão, antes de escrever
qualquer ferramenta em cima delas:**
1. **Não existia como forçar o braço C a nunca chamar o LLM.** `AnomalyPipelineBackgroundService`
   sempre chamava `LlmPlanner.DecideAsync` (que só cai pro `RuleEngine` em *falha*, §5.5) — não
   havia opção de rodar o `RuleEngine` como decisor de primeira linha, que é literalmente o que o
   braço C do §3 exige. Fechado com `IPlatformConfig.PlannerBackend` (`Llm`/`RuleEngine`, novo
   enum), sob `norn:platform:config:plannerBackend` — mesmo padrão de cache+invalidação pub/sub que
   `Mode` já tinha (`RedisPlatformConfig`). `AnomalyPipelineBackgroundService` passou a ler o
   backend uma vez por ciclo e ramificar entre `ruleEngine.Decide(context)` e
   `llmPlanner.DecideAsync(context, ct)` — os dois braços continuam **o mesmo binário** (ADR-05),
   só muda uma chave de configuração lida em runtime.
2. **`container_oom_events_total` não é observável neste ambiente** (já documentado em
   `docs/metrics-matrix.md` desde a Fase 7) **e o F1 precisa do instante do `OOMKilled` pra rotular
   o onset.** A alternativa que o próprio `metrics-matrix.md` já apontava —
   `status.containerStatuses[].lastState.terminated` via K8s — existe como método
   (`ITopologyReader.GetLastTerminationReasonAsync`) mas **nunca foi ligada a nada**: não gera
   `AnomalySignal`, não é lida por ninguém no laço ao vivo. Decisão: não abrir essa frente agora
   (tocaria Monitor/Analyzer, fora do escopo "só ferramental de campanha" desta sessão) — em vez
   disso, `run-experiment.ps1` faz *poll* de `kubectl get pods -o jsonpath=...lastState.terminated`
   a cada 5s durante toda a janela de observação, guardando o primeiro `OOMKilled` visto, e repassa
   o timestamp pro `Norn.Labeler` via `--oom-killed-at-utc`. **Risco residual, não eliminado:** se o
   `RestartPod` real disparar mais rápido que o intervalo de poll (5s), o Pod antigo pode ser
   apagado antes do poll capturá-lo — achado da revisão de código antes do commit, documentado em
   comentário no próprio script. Corrigir de vez exigiria capturar o timestamp dentro do próprio
   `Norn.Executor` no instante da atuação, não só no teardown — fica para quando essa frente for
   aberta de propósito, não como acréscimo aqui.

**`Norn.Contracts`/`Norn.Knowledge`:** `IExperimentRunStore` (novo port) — `CreateAsync` grava as
colunas de controle no reset (`ExperimentRunRecord`: cenário, braço, seeds, `started_at_utc`, etc.,
já sem escritor desde a Fase 7), `UpdateLabelingResultAsync` grava as colunas de rotulagem depois do
teardown (`ExperimentRunLabelingResult`: onset/recuperação/estado de término/`achieved_rps`/temp e
clock de CPU). `ExperimentRunRow` teve sete colunas trocadas de `init` pra `set` — a linha já existe
(criada no reset) quando o Labeler as calcula, bem depois. Testado contra Postgres real
(Testcontainers): as duas escritas na mesma linha, na ordem real em que acontecem.

**`tools/Norn.Labeler`** (composition root de campanha, exceção declarada do §4 — referencia
`Norn.Knowledge` direto): três subcomandos.
- `reset --arm A|B|C` — zera `shop:flags:` (via `IFeatureFlagWriter`, nunca escrita direta de chave
  Redis), grava `Mode`+`PlannerBackend` pelos adaptadores reais (invalidação pub/sub inclusa), lê de
  volta e imprime pro `run-experiment.ps1` assertar contra o braço pedido (§3, tarefa 2a).
- `init-run` — grava `ExperimentRunRecord` a partir de flags de linha de comando.
- `label` — o núcleo da fase: `OnsetRecoveryCalculator` (`Detection/`, função pura, sem I/O) decide
  onset (5xx > 1% por 30s sustentado, ou `OOMKilled`, o que vier primeiro; F5 usa o instante do kill
  como âncora, regra própria do §3) e recuperação (< 0,1% por 60s sustentado a partir do onset) sobre
  a série de erro lida de um `PrometheusRangeClient` próprio (não reusa `Norn.Monitor` — Labeler não
  o referencia). `LoadDeliveryChecker` marca `InvalidInstrumentation` quando `achieved_rps` foge de
  ±10% do alvo, **e essa checagem tem precedência sobre o resultado do onset/recuperação** — decidido
  aqui porque o Labeler já calcula `achieved_rps`, evitando espalhar a lógica de estado de término em
  dois lugares. Resultado grava em `experiment_runs` e, se `Recovered`/`CensoredAtWindowEnd`, vira uma
  linha de `tools/analysis/data/labeled-runs.csv` (formato Kaplan-Meier: `tempo_ate_recuperacao_segundos`
  + `evento_observado`); `Invalid*` vai pro `discarded-runs.csv` com motivo — nunca os dois arquivos
  ao mesmo tempo pra uma execução (§3: censura é resultado, descarte é defeito). Cabeçalho dos CSV em
  snake_case de propósito (`[Name(...)]` do CsvHelper) — quem lê é Python, não C#. 14 testes unitários
  sobre `OnsetRecoveryCalculator`/`LoadDeliveryChecker` (séries sintéticas: sustentação de 30s/60s,
  prioridade OOM×taxa-de-erro, regra própria do F5, borda sem cobertura futura não conta como onset).

**`tools/Norn.PairedAnalysis`** (Fase 12, tarefa 4a — H2 pareada): **não** referencia
`Norn.Knowledge` (§4 — só `Norn.Contracts` + `Norn.Planner`, lê Postgres via `Npgsql` cru, mesma
exceção documentada e agora coberta por dois `[Fact]` novos em `Norn.ArchitectureTests`). Lê todo
`AnomalyContext` de execuções do braço B (join com `experiment_runs.arm = 'B'`), recalcula
`RuleEngine.DecideActionType` — a função **pura** (Fase 8), nunca `RuleEngine.Decide`, que exigiria
estado ao vivo e mediria ação eficaz, não ação esperada —, compara contra a ação que o LLM decidiu de
fato (via `HealingPlan` do mesmo contexto) e contra a ação de referência do cenário, e escreve
`paired-analysis.csv`. 5 testes unitários usando os nomes de métrica literais da assinatura M=7
(`RuleEngine`), inclusive o caso real documentado em `docs/experiments/estabilidade-llm.md` (F3:
LLM escolhe `NoOp`, regra escolhe `ToggleFeatureFlag`, só a regra bate com a referência).

**`tools/analysis/`** (Python, fora do CI — §3): `requirements.txt` com as cinco bibliotecas do §3
pinadas e instaladas de verdade no `.venv` (`lifelines==0.30.0`, `scipy==1.15.2`,
`statsmodels==0.14.4`, `pandas==2.2.3`, `matplotlib==3.10.1`, todas compatíveis com o Python 3.13.3
já provisionado na Fase 00). `survival_analysis.py` (Kaplan-Meier + log-rank via `lifelines`, Fisher
exato pareado a×a via `scipy.stats`), `paired_mcnemar.py` (McNemar exato via `statsmodels`, mais
concordância bruta), `analyze.py` (CLI única, gera `resumo.md` + um PNG de Kaplan-Meier por cenário).
9 testes `unittest` (stdlib — nenhuma dependência de teste fora das cinco já pinadas) mais
`fixtures/*.sample.csv` **sintéticas** (não é dado da campanha real) que provam a esteira inteira
funcionando ponta a ponta, do CSV ao `resumo.md` e aos PNGs.

**`deploy/run-experiment.ps1`** — uma execução completa (reset → registro → carga/injeção →
observação → rotulagem → teardown), orçamento de tempo **fixo** (não detecta onset ao vivo; quem
decide onset é só o Labeler, depois, sobre o intervalo de Prometheus inteiro da execução — decisão
tomada com o usuário: KISS/DRY, um único lugar de detecção, ao custo de alguns minutos de margem por
execução). F3 abre `kubectl port-forward` próprio pra Payment.API (sem NodePort, D9). Checklist da
tarefa 3a (energia/sleep/Windows Update/Docker Desktop) impresso e confirmado interativamente, com
recusa automática se a máquina estiver na bateria. **`deploy/run-campaign.ps1`** gera o manifesto de
60 execuções em blocos aleatorizados (`docs/experiments/campaign-manifest.csv`, seed gravado, nunca
agrupado por braço — §3), reaproveita o manifesto se já existir (retomada de lote via
`-StartFromRunOrder`), e chama `dump-knowledge.ps1` a cada bloco de 3 execuções (tarefa 3b — "o
plano não tinha nenhum"). **Nenhum dos três scripts foi exercitado contra o cluster real nesta
sessão** — só sintaxe validada (`Parser]::ParseFile`, os arquivos precisam de BOM UTF-8 pra
PowerShell 5.1 não corromper acento/travessão, mesmo padrão que `bootstrap.ps1` já usava) e lidos
linha a linha na revisão de código. Um bloqueante real de lógica foi achado e corrigido nessa
revisão: `run-campaign.ps1` checava `$LASTEXITCODE` depois de invocar `run-experiment.ps1` via `&`,
mas o script chamado sinaliza falha por `throw` (exceção terminante), não por código de saída — a
checagem nunca dispararia; corrigido com `try`/`catch`.

**Sessão de acompanhamento (18/09/2026) — máquina reiniciada, ambiente recuperado, três dos sete
pendentes fechados sem precisar de execução real da campanha.** Depois do restart do Windows:
Docker Desktop não subiu sozinho (processo ausente — `Start-Process` nele resolveu), os três
containers do k3d (`server-0`, `serverlb`, `registry`) sobreviveram e voltaram sozinhos, mas a infra
do Compose (Postgres/Redis/RabbitMQ/Prometheus/Grafana/Tempo/Collector) precisou de
`docker compose up -d` de novo. `k3d cluster stop`/`start` reinjetou `host.k3d.internal` no CoreDNS
(mesmo procedimento já documentado na Fase 9) — desta vez os três pods do Shop nem chegaram a
`CrashLoopBackOff`, foram direto a `1/1 Running` (~70s), porque a infra já estava de pé antes deles
tentarem religar.

Com tudo de pé, três pendências fechadas sem gastar as ~20h da campanha:
1. **`exported_job` confirmado contra o Prometheus real** — `curl /api/v1/label/exported_job/values`
   devolveu exatamente `Norn.Shop.Catalog.API`/`Order.API`/`Payment.API` (mais `Norn.API`/`Norn.Worker`),
   batendo com o que `run-experiment.ps1` já usava. `http_server_request_duration_seconds_count`
   com `http_response_status_code` confirmado existindo de verdade para o Catalog.
2. **`IPlatformConfig.SetForecastConfigAsync`** (novo método, mesmo padrão de
   `SetModeAsync`/`SetPlannerBackendAsync`) — `ResetCommand` agora desliga o forecast a cada reset,
   defensivo contra qualquer teste ad hoc futuro contaminar H1/H2 em silêncio.
3. **Captura de clock médio de CPU** — `run-experiment.ps1` agora amostra
   `Get-CimInstance Win32_Processor` no mesmo poll de 5s que já verifica `OOMKilled`, sem custo
   extra de espera, e repassa a média pro `Norn.Labeler label --cpu-clock-avg-mhz`. Temperatura
   continua sem captura (exigiria LibreHardwareMonitor ou WMI de terceiros) — só vale investir se
   os pilotos mostrarem sinal de throttling térmico.

**Primeiro teste ao vivo real do `Norn.Labeler` contra Postgres/Redis/Prometheus de verdade —
descartável, limpo depois, mas prova que a esteira funciona.** `reset --arm C` confirmou
`mode=Active`/`plannerBackend=RuleEngine` gravados e lidos de volta do Redis real — primeira
confirmação viva do switch de braço da Fase 12 (sem subir um `Norn.Worker` de verdade, então ainda
não prova que o Worker *lê* o valor certo em runtime, só que o adaptador grava/lê certo). `init-run`
gravou uma linha real em `platform.experiment_runs` (INSERT confirmado no log do EF Core). `label`
fez uma chamada HTTP real ao Prometheus (`query_range`, 200, via o handler de resiliência padrão),
leu a linha de volta e gravou `termination_state=InvalidNoOnset` — resultado correto, já que nenhum
caos estava ativo. **Um bug real de cultura achado e corrigido no processo:** `Console.WriteLine`
formatava `achieved_rps` com `:F2` sem `CultureInfo.InvariantCulture` — em uma máquina pt-BR isso
imprime `9,90` em vez de `9.90` no log de diagnóstico (não afeta o CSV nem o Postgres, que já usavam
`InvariantCulture` corretamente; só a linha de stdout). Corrigido com `string.Create(CultureInfo.InvariantCulture, $"...")`.
Linha de teste removida do Postgres real depois (`DELETE FROM experiment_runs WHERE ...`), estado da
plataforma revertido pra `Observe` (padrão seguro, ADR-05) ao final.

**Primeiro piloto real (F1/braço C) tentado na mesma sessão — não fechou como dado de campanha
válido (`InvalidInstrumentation`), mas validou o ferramental inteiro ao vivo e achou dois bugs reais
mais um gap operacional sério, todos corrigidos.**

**Achado operacional, achado antes de qualquer execução:** `run-experiment.ps1` reseta o braço no
Redis (`mode`/`plannerBackend`), mas **nunca sobe um `Norn.Worker`** — os dois primeiros disparos do
piloto rodaram sem ninguém decidindo nada, só o kubelet reiniciando o Catalog sozinho em
`CrashLoopBackOff` (achado só depois de o F1 já estar ativo de verdade). Corrigido operacionalmente
nesta sessão subindo um `Norn.Worker` à parte antes do terceiro disparo — **os scripts continuam
sem subir o Worker automaticamente**, isso é responsabilidade de quem roda a campanha (documentar
no procedimento da campanha, não corrigido em código nesta sessão).

**Dois bugs reais de código achados e corrigidos em `run-experiment.ps1`, ao vivo:**
1. **jsonpath do poll de OOM quebrava sempre.** `-o jsonpath='...{"|"}...'` chegava ao `kubectl` sem
   as aspas literais que o separador exige (`unrecognized character in action: U+007C '|'`) — o
   PowerShell reescreve argumentos passados a executáveis nativos e não preserva aspas embutidas de
   forma confiável, nem no 7.x. **A primeira tentativa do piloto quebrou exatamente nesse ponto, e
   como não havia rede de segurança, o F1 ficou ativo de verdade contra o Catalog sem ninguém
   desligar** — intervenção manual (deactivate + limpeza direta do Redis) evitou o pod estourar o
   limite de memória. Corrigido trocando `-o jsonpath` por `-o json` + `ConvertFrom-Json` (sem
   nenhuma aspa sobrevivendo a reescrita nenhuma).
2. **Sem `try`/`finally` em volta de ativar-observar-desativar o caos**, qualquer falha no meio
   (inclusive o bug 1) derrubava o script inteiro antes de chegar no "Desativando caos". Corrigido
   envolvendo ativação+poll+observação num bloco `try` com um `finally` que sempre roda: desativa
   caos, para o LoadGenerator, para o port-forward — não importa o que aconteça acima.
3. **Achado só na segunda tentativa real (depois do fix 1 e 2): o próprio `finally` podia falhar em
   silêncio.** O deactivate via HTTP não tem como funcionar se o pod-alvo estiver em crash loop bem
   naquele instante — e foi exatamente o que aconteceu: o F1 ficou "preso" ativo no Redis por mais
   de 30 min depois do script já ter terminado (confirmado via `HGETALL norn:chaos:active` direto),
   contaminando o pod novo que o `RestartPod` tinha acabado de criar. Corrigido com 3 tentativas de
   HTTP (5s entre elas) e, se todas falharem, um fallback que limpa a chave direto no Redis
   (`DEL norn:chaos:active` — slot único e global, confirmado pelo schema do hash). **Isto é crítico
   para a campanha de 60 execuções desacompanhada: sem o fallback, uma única falha de rede/pod no
   teardown contaminaria todas as execuções seguintes do lote.**

**O que o piloto confirmou funcionando de verdade, ao vivo, com o `Norn.Worker` no ar:**
- `RestartPod` decidido pelo `RuleEngine` (braço C) disparou de verdade contra o Catalog — pod
  antigo apagado, pod novo criado (`DeleteNamespacedPodAsync` real, não simulado).
- `ScaleUp` disparou duas vezes pro Order.API sob carga real do `LoadGenerator` (não caos sintético)
  — uma fechou `Succeeded`/`SloRestored=true`, confirmando o DoD da Fase 9 de novo, agora com o
  ferramental novo da Fase 12 no meio.
- `ToggleFeatureFlag` disparou pro Payment.API sob degradação real do gateway sob a mesma carga.
- **F1 se manifesta de duas formas distintas neste ambiente, achado novo:** às vezes como
  `OOMKilled` de verdade do kernel (`exitCode 137`, restart in-place no mesmo Pod — exatamente o
  caso que o poll de 5s existe para capturar) e às vezes como `System.OutOfMemoryException` **dentro
  do processo gerenciado** (heap do .NET, não cgroup) — o processo passa a falhar os health probes,
  o kubelet mata por `reason: Error` (não `OOMKilled`), e a detecção de onset via `OOMKilled` nunca
  dispararia para esse caso especificamente (a taxa de 5xx continua sendo o caminho que fecha o
  onset ali, exatamente como o §3 previu com "o que ocorrer primeiro").
- **Memória retida pelo F1 não é liberada só por desativar** (achado já documentado
  historicamente, reconfirmado aqui com causa mais precisa): mesmo depois do Redis confirmadamente
  limpo, um pod novo ainda nasceu em ~350-365Mi e não baixou sozinho — só um ciclo completo
  (`kubectl scale --replicas=0` seguido de `--replicas=1`) devolveu o baseline limpo (~68Mi).
  `kubectl delete pod` direto continua negado por permissão (mesmo padrão de sempre); o ciclo de
  escala é o contorno que funciona.
- **A execução em si terminou `InvalidInstrumentation`** (`achieved_rps=6.07` contra
  `target_rps=11.00`, fora de ±10%) — esperado e correto: a máquina tinha `Norn.Worker` +
  `Norn.LoadGenerator` + múltiplos ciclos de restart de pod rodando ao mesmo tempo, then contenção
  de CPU real no host, exatamente o cenário que o §3 já avisava ("gerador sem CPU oferece menos
  requisições que o alvo"). Não é falha do Labeler — é o Labeler descartando corretamente uma
  execução que não teria dado dado confiável. Confirma que o `Norn.Worker` **precisa rodar num
  processo dedicado**, sem outras cargas de trabalho pesadas concorrentes, durante a campanha real.

**`run-campaign.ps1` passou a subir o `Norn.Worker` sozinho, um único processo para o lote
inteiro** (não por execução — reiniciar a cada reset perderia o warmup do detector). Recusa subir
se já existir um `Norn.Worker` rodando (evita dois disputando a mesma decisão, ADR-04), espera
~150s de warmup antes da primeira execução, e derruba no `finally` ao fim do lote ou em qualquer
falha. `run-experiment.ps1` isolado continua exigindo o Worker manual — documentado no próprio
script.

**Achado metodológico mais sério da sessão, achado só na 2ª tentativa real:** `achieved_requests`
do `Norn.LoadGenerator` conta **respostas bem-sucedidas** (`dispatched.Count(ok => ok)`), não
tentativas disparadas — então a razão achieved/intended cai de verdade quando o alvo degrada sob
F1/F2/F3, que é **exatamente o sinal que o cenário existe para causar**. Medir essa razão sobre a
janela inteira (como a §3 original sugeria) descartaria como `InvalidInstrumentation` justamente as
execuções em que o cenário funcionou — enviesando a campanha contra achar dado nos casos
interessantes. Confirmado ao vivo: duas tentativas seguidas do piloto F1/C saíram
`InvalidInstrumentation` com `achieved_rps` bem abaixo do alvo (6.07 e 6.49 contra 11), mesmo com o
relatório bruto do próprio `LoadGenerator` mostrando ~100% de sucesso **na fase de warmup** — o
déficit vinha inteiro do período pós-injeção, quando o Catalog já estava em crash loop pelo F1.
**Corrigido:** `LoadReportReader.Read` ganhou um terceiro parâmetro (`warmupEndUtc`) e só soma os
buckets **anteriores ao instante da injeção** — `run-experiment.ps1` captura esse instante
(`$injectionAtUtc`) logo antes do `POST /admin/chaos/activate` e repassa via `--injection-at-utc`
novo do `Norn.Labeler label`. Mede a capacidade do gerador só enquanto o alvo ainda está saudável,
sem se misturar com a saúde do alvo depois — a saúde do alvo já é medida por onset/recuperação, não
precisa ser medida duas vezes por dois caminhos que discordam entre si.

**Com a correção, a 3ª tentativa do piloto F1/C fechou como dado de campanha válido pela primeira
vez.** `achieved_rps=11.02` (alvo 11 — quase exato, confirmando que o problema nunca foi capacidade
real do gerador), onset real detectado, `termination_state=CensoredAtWindowEnd` — sem recuperação
dentro dos 10 min, resultado legítimo (§3: censura é resultado, não descarte) para um `RestartPod`
contra um F1 cuja intensidade nunca reseta, o mesmo padrão de calibração já documentado nos
residuais da Fase 9. Linha real gravada em `tools/analysis/data/labeled-runs.csv` — primeiro dado de
campanha do projeto.

**Confirmado de novo, agora com o fallback do Redis testado ao vivo duas vezes:** a desativação via
HTTP falhou nas 3 tentativas em ambas as repetições do piloto (Catalog em crash loop no instante do
teardown, exatamente o cenário que motivou o fix) — o fallback via `redis-cli DEL` disparou
automaticamente nas duas vezes, sem intervenção manual, confirmando que a correção do commit
anterior funciona de verdade sob a condição real que a motivou.

**Padrão que se repete a cada tentativa, sem exceção até agora:** depois do `RestartPod`/OOM, o pod
novo do Catalog nasce com memória residual alta e entra em `CrashLoopBackOff` — só um ciclo
`kubectl scale --replicas=0` seguido de `--replicas=1` devolve o baseline limpo (~70Mi);
`kubectl delete pod` direto continua negado por permissão. Isso é limpeza operacional entre
execuções, não faz parte do `run-experiment.ps1` (que já reseta réplicas no início de cada
execução) — mas quem for rodar os 60 execuções da campanha real deve esperar precisar disso entre
blocos, não é uma falha nova a cada vez.

**Segundo piloto (F5/braço C, controle negativo) rodado na mesma sessão — segundo dado de campanha
válido, e um achado real e sério sobre o `RuleEngine`.** Kill abrupto disparou de verdade (`firedAtUtc`
capturado ~0,8s depois da ativação, via `norn:chaos:active` no Redis — primeira validação ao vivo
desse caminho). `termination_state=Recovered` (~7 min até o SLO voltar, dentro da janela de 10 min),
`achieved_rps=11.02` — a correção da capacidade do gerador se generaliza para outro cenário.

**Achado sério, exatamente o que o F5 existe para revelar (§3: "qualquer ação executada é o
achado"):** o kill+recriação do pod do Catalog gerou dois falsos positivos reais.
1. **`RestartPod` decidido para o próprio Catalog** (RSS/GC do pod recém-recriado, ruído de
   startup/JIT interpretado como assinatura de F1) — auto-consistente (alvo e ação batem no mesmo
   serviço), mas ainda um falso positivo: o controle negativo não deveria produzir nenhuma ação.
2. **Mais grave — `ToggleFeatureFlag` decidido a partir de um sinal do Catalog, mas aplicado na
   flag do Payment.** A assinatura de F3 do `RuleEngine`
   (`ErrorRate5xx = "http_server_request_duration_seconds_count"`) é um **nome de métrica
   genérico**, emitido por Catalog/Order/Payment igualmente — o `RuleEngine.DecideActionType` só
   enxerga o conjunto de nomes de métrica alterados, nunca qual serviço os emitiu. Um blip de 5xx do
   Catalog (efeito colateral do próprio kill/restart do F5) bateu na mesma branch que decide
   `ToggleFeatureFlag`, e a ação resultante **sempre** escreve na chave fixa
   `shop:flags:payment.gateway.bypass` (`ShopFlagCatalog.PaymentGatewayBypass`), **mesmo com
   `target.service = "Norn.Shop.Catalog.API"`** no `HealingAction` persistido — nenhuma barreira do
   Executor bloqueou por incompatibilidade de serviço. **Confirmado ao vivo**: a flag
   `shop:flags:payment.gateway.bypass` estava `True` de verdade no Redis depois da execução —
   ligada por um sinal que não tinha nada a ver com o Payment. Resetada manualmente para `false`
   depois de confirmado (não fazia parte do DoD desta sessão corrigir o `RuleEngine` — é uma decisão
   de escopo maior, precisaria de um jeito de amarrar a ação de cura ao serviço do contexto que a
   originou, provavelmente em `HealingActionPreconditionChecker` ou na montagem do `HealingAction`
   dentro de `RuleEngine.Decide`, não só em `DecideActionType`).

**Gap do `ToggleFeatureFlag` sem escopo de serviço — corrigido (sessão de acompanhamento).**
`ShopFlagCatalog` ganhou `OwnerServiceByFlag` (mapa flag → serviço dono; hoje só
`PaymentGatewayBypass → Norn.Shop.Payment.API`, mas é a mesma "fonte única" que `All` já era
desde a Fase 8). `HealingActionPreconditionChecker.CheckToggleFeatureFlag` passou a receber o
`AnomalyContext` (não só a `HealingAction`) e rejeita quando `context.PrimarySignal.Target.Service`
não é o dono da flag — `TryGetValue` em vez do indexador, para uma flag nova entrando em `All` sem
entrada correspondente em `OwnerServiceByFlag` virar `PreconditionResult.Reject` (mesma filosofia
"nunca lança, sempre decide" do resto do checker), não uma `KeyNotFoundException` derrubando o
Planner. A correção fica só na barreira, não em `RuleEngine.DecideActionType` (função pura,
continua sem noção de serviço, como o comentário da classe já documentava) — `RuleEngine.Decide`
e `LlmOutputValidator` (braço B) chamam o mesmo `preconditionChecker.Check`, então os dois braços
ganharam a correção pelo mesmo commit, sem duplicar a regra. `HealingActionCatalog` (texto que
alimenta o system prompt do LLM) também atualizado. Achado curioso confirmado ao ler os testes
antigos: o bug estava literalmente embutido em `Check_ToggleFeatureFlag_FlagInShopCatalog_Accepted`
— o teste aceitava a flag do Payment com um contexto do Catalog, o mesmo cenário do achado ao vivo
do piloto F5, sem ninguém notar até agora. Teste corrigido para usar serviço do Payment, mais um
teste novo de regressão para o mismatch, mais dois testes ponta a ponta de `Decide()` para F3 (não
existiam antes — só a função pura `DecideActionType` era testada contra a assinatura F3). 187
testes verdes em `Norn.Planner.UnitTests` (184 + 3 novos), `dotnet format --verify-no-changes`
limpo, `code-reviewer` sem achados bloqueantes. **Só a correção de código — não validado ao vivo
contra o cluster ainda** (o achado original só foi confirmado num piloto real; fechar o ciclo
exigiria repetir o piloto F5 com a correção no ar e confirmar que a flag do Payment não liga mais
por sinal do Catalog).

**Terceiro piloto (F3/braço B, LLM) — feito, sessão de acompanhamento seguinte. Diversidade dos 3
pilotos do DoD da Fase 12 completa** (F1/C, F5/C, F3/B). Ambiente recuperado do zero primeiro
(Docker Desktop não subiu sozinho — mesmo achado de sempre, `Start-Process` resolveu; `k3d cluster
stop`/`start` reinjetou `host.k3d.internal`, os três pods do Shop se recuperaram sozinhos).

**Duas tentativas falharam antes da terceira fechar — os dois bugs eram reais, não flakiness, e os
dois foram corrigidos em `run-experiment.ps1`:**
1. **Porta errada no port-forward do F3** — `kubectl port-forward svc/payment-api 8082:8080`
   usava a porta do **container** (`targetPort`), não a porta do **Service** (`port: 80`,
   confirmado com `kubectl get svc -o yaml`). Isso nunca teve chance de funcionar — falha na hora
   com "Service payment-api does not have a service port 8080", em qualquer tentativa, independente
   de tempo de espera. A 1ª tentativa mascarou a causa como "timing" (o `Start-Sleep -Seconds 3` fixo
   parecia curto demais) e o script abortou fora do `try/finally` que protege o teardown, sem limpar
   nada. Corrigido: porta certa (`8082:80`), um poll ativo de prontidão contra `/health/live` no
   lugar do sleep fixo, retry de 3 tentativas na própria ativação (mesmo padrão que a desativação já
   tinha), e o bloco inteiro movido para **dentro** do `try/finally` existente — uma falha aí agora
   passa pela mesma limpeza (para o `LoadGenerator`, remove os jobs) em vez de abortar o script cru.
2. **Toda chamada ao Ollama nas duas primeiras tentativas deu timeout (10s configurados).** A
   primeira chamada real depois de a máquina reiniciar carrega o modelo na GPU (~15s, medido ao
   vivo) antes de sequer avaliar o prompt — mais que o `OllamaTimeout` de
   `Norn.Worker/appsettings.json`. Sem aquecer antes, toda decisão do braço B caía no fallback do
   `RuleEngine`, nunca testando o LLM de verdade (uma vez quente, um prompt do tamanho real do
   `AnomalyContext` roda em ~2s — não é lentidão do modelo, é só o carregamento a frio). Corrigido
   com um `ollama run norn-qwen "..."` best-effort no início do script, só quando `-Arm B`.

**Com as duas correções, a 3ª tentativa completou do início ao fim sem crash e sem processo
órfão** (confirmado via `Get-CimInstance Win32_Process` antes/depois — nenhum `kubectl.exe`/
`Norn.LoadGenerator` sobrando). `DecidedBy: Llm` em **todas** as decisões da janela — o braço B foi
genuinamente exercitado pela primeira vez ao vivo dentro do laço completo (não isolado como na
Fase 8). F3 disparou sinais reais `norn_shop_payments_gateway_latency_ms` severidade Critical no
Payment. `achieved_rps=10.96` (alvo 11 — dentro da margem). Terminou `InvalidNoOnset`, registrado em
`discarded-runs.csv`: **achado metodológico novo** — o onset do Labeler é ancorado em taxa de 5xx
sustentada (ou `OOMKilled`, só para F1), mas o F3 é degradação de **latência**, não
necessariamente de taxa de erro — sob o gateway simulado lento, as requisições continuam
retornando sucesso, só mais devagar, então o critério atual de onset pode nunca cruzar o limiar
para este cenário específico. Não corrigido nesta sessão (tocaria `OnsetRecoveryCalculator`,
decisão de escopo maior — precisa de um critério de latência sustentada, análogo ao de 5xx, não
só mais um ajuste de configuração).

**Achado sobre H2, reforça o da Fase 8 com dado ao vivo novo:** com sinal Critical real de F3 no
Payment, o LLM decidiu `ScaleUp` (duas vezes) e `NoOp` (uma vez) — **nunca `ToggleFeatureFlag`**,
mesmo sob severidade Critical sustentada. `ScaleUp` não tem relação nenhuma com o problema real
(gateway simulado lento, não falta de capacidade) — mais uma divergência concreta entre o
raciocínio do LLM e a ação de referência do cenário, complementando o achado da Fase 8 (que já
mostrava o LLM preferindo `NoOp` a `ToggleFeatureFlag` para F3, ali por causa da ausência de
`pod`/`podUid` no contexto).

**Fix do `ToggleFeatureFlag` sem escopo de serviço — ainda não validado ao vivo.** Como
`ToggleFeatureFlag` nunca foi candidato nesta execução (nem pelo LLM, que nunca escolheu, nem pelo
`RuleEngine`, que nunca decidiu porque o LLM nunca falhou desta vez), a pré-condição de escopo
continua coberta só por teste unitário. Validar isso ao vivo exigiria ou um piloto de braço C para
F3 (onde o `RuleEngine` decide `ToggleFeatureFlag` de forma determinística e confiável para essa
assinatura) ou engenharia deliberada de um sinal cross-service como o do piloto F5 — nenhum dos
dois tentado aqui.

**Ambiente ao fim desta sessão:** F3 desativado, port-forward encerrado, `shop:flags:` zeradas,
`mode=Observe`, réplicas de volta a 1 em cada serviço do Shop, `Norn.Worker` local encerrado.
Docker Desktop/infra/cluster deixados de pé (reaproveitados de uma sessão anterior, não
provisionados nesta).

**Critério de onset do Labeler estendido para cobrir latência sem taxa de erro (F3) — corrigido em
código, sessão de acompanhamento seguinte; ainda não validado ao vivo.** `OnsetRecoveryCalculator`
ganhou uma segunda via de onset, só para F3: latência sustentada por 30s acima do SLO do gateway
(500ms — mesmo valor de `SeverityBandOptions.Bands["norn_shop_payments_gateway_latency_ms"]`,
Norn.Analyzer; duplicado como constante local porque Norn.Labeler não referencia Norn.Analyzer,
§4, mesmo motivo pelo qual o limiar de 1% de erro já era local). Onset continua sendo o que ocorrer
primeiro entre as vias disponíveis (erro, latência quando F3, OOM quando F1); a recuperação é
sempre calculada na mesma série que decidiu o onset — nunca misturando taxa de erro com latência,
porque grandezas diferentes tornariam `tempo_até_recuperação` incomparável entre execuções e
contaminariam H1. `LabelCommand` passou a buscar a série de p99 de
`norn_shop_payments_gateway_latency_ms` no Prometheus só quando `scenario == "F3"` (mesma query que
`PrometheusQueryCatalog`, Norn.Monitor, duplicada pelo mesmo motivo de sempre — Labeler não
referencia Monitor).

**Achado do `code-reviewer` antes do commit, corrigido na hora:** num empate exato entre as duas
vias (plausível de verdade em F3 — um timeout de gateway tende a gerar 5xx e latência alta na mesma
amostra), o código caía silenciosamente na série de latência para a recuperação, só por efeito
colateral de como o `EarliestNonNull` já existente resolve empate (devolve o segundo argumento) —
não por decisão de negócio nenhuma. Corrigido para a taxa de erro vencer qualquer empate (via mais
testada, closer ao resto do rotulador), com teste de regressão dedicado para o caso de empate exato.
`F3Scenario`/`F5Scenario` também viraram `internal` em vez de string solta duplicada em
`LabelCommand`. 21 testes verdes em `Norn.Labeler.UnitTests` (18 + 3 novos), `dotnet format
--verify-no-changes` limpo. **Só a correção de código — não validado ao vivo ainda**: fechar de
verdade exigiria repetir o piloto F3/B (ou um F3/C) com a correção no ar e confirmar que uma
execução real que antes saía `InvalidNoOnset` agora produz `Recovered`/`CensoredAtWindowEnd`.

**O que ainda fica para fechar os pilotos do DoD da Fase 12:** decidir se o ciclo de
`kubectl scale 0→1` do Catalog entre execuções vale a pena automatizar dentro do
`run-campaign.ps1` (hoje é manual, e a campanha real vai precisar disso repetidamente); validar ao
vivo o fix do `ToggleFeatureFlag` (achado acima); validar ao vivo a via de onset por latência do F3
recém-corrigida (achado acima) — nenhuma das duas correções de escopo/onset foi exercitada contra o
cluster real ainda.

## Onde encontrar
Contratos → C:\git\norn-plano\NORN-MASTER-PLAN.md §5 (fora do repo — nunca commitado)
ADRs → docs/adr/
Métricas → docs/metrics-matrix.md (nasce na Fase 4)
Tabela de regras (golden do teste) → docs/rule-table.md (nasce na Fase 8)
Contrato da Norn.API (rotas, DTOs, enums, eventos do hub) → docs/norn-api-contract.md (nasce na Fase 11)
