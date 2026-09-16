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

## Onde encontrar
Contratos → C:\git\norn-plano\NORN-MASTER-PLAN.md §5 (fora do repo — nunca commitado)
ADRs → docs/adr/
Métricas → docs/metrics-matrix.md (nasce na Fase 4)
Tabela de regras (golden do teste) → docs/rule-table.md (nasce na Fase 8)
