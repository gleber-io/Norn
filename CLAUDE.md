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
Fase concluída: 7. Próxima: 8.

Cluster k3d validado de ponta a ponta (Fase 6): `bootstrap.ps1` leva de zero a sistema funcional em um comando, fluxo completo de pedido roda dentro do cluster, RBAC do ADR-03 confirmado (positivo e negativo), `maxReplicas=3` valida escala real, Traefik ausente (D9), `container_memory_working_set_bytes`/`container_cpu_usage_seconds_total` via cAdvisor fecham o DoD da Fase 4.

**Fase 7 — Monitor e Analyzer, concluída.** `Norn.Knowledge` (Postgres schema `platform`: `anomaly_signals`, `anomaly_contexts`, `healing_plans`, `healing_outcomes`, `llm_traces`, `experiment_runs`; `ICooldownStore`/`IPlatformConfig` sobre Redis), `Norn.Monitor` (cliente do Prometheus, leitura de topologia via `KubernetesClient`, polling da assinatura fechada M=7), `Norn.Analyzer` (`DetectIidSpike`/`DetectIidChangePoint` incrementais, `SeverityCalculator` por ADR-14, `ContextCorrelator` com ordenação determinística) e `Norn.Worker` (composition root que fecha o laço até a persistência do contexto, modo `Observe`) implementados e testados — 160 testes verdes na solução (unitários + arquitetura + integração com Testcontainers reais para Postgres e Prometheus). `ITopologyReader` ganhou `GetLastTerminationReasonAsync` para resolver a pendência abaixo.

**Pendência da Fase 6 resolvida:** `container_oom_events_total` não fechava porque o containerd remove o cgroup do container morto antes do cAdvisor conseguir ler `oom_kill=1` nele. Decisão tomada e implementada: `Norn.Monitor` lê `status.containerStatuses[].lastState.terminated.reason == OOMKilled` do próprio Pod via `KubernetesClient` (verbo `get`, já concedido pela `Role` do ADR-03) em vez do caminho Prometheus. Detalhe completo em `docs/metrics-matrix.md`.

**Validado ao vivo nesta sessão** contra o cluster k3d e a infra da Fase 6 (ambos ainda de pé): F1 ativado no Catalog.API real via `/admin/chaos/activate`; `Norn.Worker` rodado localmente (`dotnet run`) consultou o Prometheus real repetidamente sem erro, RSS subiu de ~226 MB para ~325 MB (teto de `F1MaxRetainedBytes`) e a severidade calculada refletiu a banda esperada. **Não observado nesta sessão:** o alerta do detector ML.NET em cima do sinal ao vivo — o RSS já havia saturado no platô antes de o `MetricDetectorEngine` (buffer novo a cada `dotnet run`) completar o warmup de 30 amostras, e limpar a memória exige `RestartPod` real do Pod (ação bloqueada por permissão nesta sessão; não contornada). A detecção em si está confirmada por série sintética nos testes unitários (`MetricDetectorEngineTests`, mesma configuração de `confidence`/`historyLength`). Replay ao vivo fica pendente de um `kubectl delete pod -n norn-shop -l app=catalog-api` (ou equivalente) antes da Fase 12.

## Onde encontrar
Contratos → C:\git\norn-plano\NORN-MASTER-PLAN.md §5 (fora do repo — nunca commitado)
ADRs → docs/adr/
Métricas → docs/metrics-matrix.md (nasce na Fase 4)
Tabela de regras (golden do teste) → docs/rule-table.md (nasce na Fase 8)
