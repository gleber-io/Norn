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
deploy/dump-knowledge.ps1       # backup do Knowledge -> C:\git\norn-results\postgres-backups (Fase 12, tarefa 3b)
tools/analysis/.venv/Scripts/python.exe tools/analysis/analyze.py --labeled <csv> --out-dir <dir>
tools/PanelCapture/ npm install # uma vez, antes da campanha — screenshots (dashboard/Grafana/Prometheus) -> C:\git\norn-results
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

**Fase 12 concluída — campanha real fechada.** 57 de 60 execuções válidas (95%), tag anotada
`campaign-h1h2`, resultados de H1/H2 e métricas complementares em `docs/experiments/results.md`.
**Fase 13 (preditiva, opcional) não avaliada:** o portão de 4 condições exige H1 e H2 já escritos na
monografia e lidos pelo orientador ao menos uma vez — decisão de calendário da escrita, fora do
escopo de sessão de código.

**Histórico completo, sessão por sessão** (achados, bugs corrigidos, decisões de calibração, desde
o fechamento da Fase 9) está em `docs/experiments/historico-sessoes.md`. Carregue-o só quando a
tarefa exigir esse nível de detalhe — as armadilhas abaixo são o resumo do que se repetiu mais de
uma vez e vale ter à mão sem abrir o histórico inteiro.

**Armadilhas de ambiente conhecidas, que já custaram tempo real mais de uma vez:**
- **Recuperação pós-restart do Windows:** Docker Desktop não sobe sozinho — `Start-Process` resolve.
  `k3d cluster stop`/`start` costuma reinjetar `host.k3d.internal` no CoreDNS; se não reinjetar,
  `kubectl patch configmap coredns` manual. Se a conexão para o gateway da bridge (`172.19.0.1`)
  travar mesmo com DNS ok, é corrupção de NAT hairpin do Docker Desktop — `wsl --shutdown` (às vezes
  duas vezes, matando processos `docker` CLI presos primeiro) resolve; `host.docker.internal` tende
  a sobreviver quando o gateway não sobrevive, e serve de contorno em `~/.kube/config`.
- **`ACL SETUSER default reset` no Redis sempre com `on` explícito no mesmo comando** — sem isso o
  usuário fica desligado e todo cliente sem auth (inclusive o `redis-cli` para consertar) leva
  `NOAUTH`. `docker restart norn-redis` restaura o padrão de fábrica se acontecer.
- **`kubectl delete pod` é negado por permissão neste ambiente.** Para forçar baseline limpo de um
  Deployment, use `kubectl scale --replicas=0` seguido de `--replicas=1`, nunca delete direto.
- **`RestartPod` do F1 tem limitação estrutural, não é bug do Executor.** A intensidade do caos
  (`tau=90s`) não reseta quando o pod é recriado, e a latência do próprio laço (~90–100s até decidir)
  já deixa a intensidade em ~0,6–0,7 no momento da recriação — o pod novo nasce acima do limiar de
  restauração quase sempre, resultando em `PartiallyApplied`. Ver histórico, residuais das Fases 9/11.
- **`CircuitBreakerState` é um contador global**, não por alvo nem por tipo de ação — sucesso em
  qualquer ação de qualquer alvo reseta a sequência de falhas inteira.
- **Limite de memória do Catalog no cluster é 384Mi**, não os 256Mi do comentário original do
  manifesto da Fase 6 — recalibrado na Fase 9 para dar tempo real de detecção antes do `OOMKilled`.
- **`norn-api.yaml`/`norn-platform.yaml` (manifestos K8s do Worker/API) existem, mas não estão
  ligados ao `bootstrap.ps1`, por decisão** — a validação ao vivo continua via `dotnet run` local, e
  as imagens `:placeholder`/`:dev-session` não são construídas por nenhum passo automático.

## Onde encontrar
Contratos → C:\git\norn-plano\NORN-MASTER-PLAN.md §5 (fora do repo — nunca commitado)
ADRs → docs/adr/
Métricas → docs/metrics-matrix.md (nasce na Fase 4)
Tabela de regras (golden do teste) → docs/rule-table.md (nasce na Fase 8)
Contrato da Norn.API (rotas, DTOs, enums, eventos do hub) → docs/norn-api-contract.md (nasce na Fase 11)
Runbook da campanha de 25h (pré-requisitos, disparo, monitoramento, parada, coleta) → docs/experiments/plano-campanha.md
Resultados da campanha real (H1, H2, métricas complementares, achados metodológicos, ameaças à validade) → docs/experiments/results.md (nasce na Fase 12)
Calibração da eleição de `primarySignal` (ADR-14, tarefa 5a da Fase 7) → docs/experiments/calibracao-severidade.md (fechada em sessão de revisão pós-Fase 12, reaproveitando dado do braço A da campanha)
Histórico de sessões (achados e decisões desde a Fase 9, sessão por sessão) → docs/experiments/historico-sessoes.md
