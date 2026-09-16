# CLAUDE.md — Norn

## Comandos
```
dotnet build
dotnet test
dotnet format --verify-no-changes
deploy/bootstrap.ps1            # infra + observabilidade + cluster k3d + Shop, do zero (Fase 6)
kubectl get pods -n norn-shop
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
Fase concluída: 8. Próxima: 9.

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

## Onde encontrar
Contratos → C:\git\norn-plano\NORN-MASTER-PLAN.md §5 (fora do repo — nunca commitado)
ADRs → docs/adr/
Métricas → docs/metrics-matrix.md (nasce na Fase 4)
Tabela de regras (golden do teste) → docs/rule-table.md (nasce na Fase 8)
