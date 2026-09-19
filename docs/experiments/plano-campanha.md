# Plano de ação — campanha principal (Fase 12, 60 execuções, ~25h)

Runbook para executar a campanha experimental completa e sair dela com tudo que o TCC precisa.
Escrito para ser seguido por uma sessão Claude Sonnet 5 em conjunto com o usuário, sem depender de
contexto de sessões anteriores. Onde há decisão de julgamento, a regra está escrita explicitamente.

**Pré-leitura obrigatória:** `CLAUDE.md` (seção "Estado atual", Fase 12) e §3 do Master Plan
(`C:\git\norn-plano\NORN-MASTER-PLAN.md`) — este plano não repete o desenho experimental, só a
operação.

---

## 1. O que a campanha produz

| Hipótese | Teste | Insumo |
|---|---|---|
| **H1** — o loop recupera mais e mais rápido que não atuar | Kaplan-Meier + log-rank por cenário; Fisher exato na taxa de recuperação | `labeled-runs.csv` |
| **H2** — LLM escolhe a ação esperada mais que a regra | McNemar pareado (recálculo offline do `RuleEngine` sobre os contextos do braço B) | Postgres → `paired-analysis.csv` |
| Métricas não censuradas (MTTD, latência do loop, ação eficaz, overhead) | média + IC 95% | Postgres (`anomaly_signals`, `healing_plans`, `healing_outcomes`) |
| Figuras ilustrativas (dashboard, Grafana, séries do Prometheus) | — | `C:\git\norn-results\screenshots\` |

**Destino único de tudo:** `C:\git\norn-results\` (fora do repositório git).

```
C:\git\norn-results\
  postgres-backups\      dump a cada bloco de 3 execuções + dump final
  logs\                  worker-campaign.log, campaign-console.log, api-campaign.log
  screenshots\
    executions\          2 PNGs por execução (dashboard + gráfico da assinatura no Prometheus)
    blocks\              3 PNGs por bloco (dashboard + 2 dashboards do Grafana)
  campaign-data\         labeled-runs.csv, discarded-runs.csv, campaign-manifest.csv, load-reports/
  analysis\              resumo.md + PNGs de Kaplan-Meier (gerados no fim, por analyze.py)
```

---

## 2. Orçamento de tempo

60 execuções × 25 min (`DurationMinutes` default, calibrado nos 3 pilotos) = **1.500 min = 25h**,
mais warmup inicial do Worker (150s), 20 dumps do Postgres e as capturas de tela.

**Reserve 26h de máquina dedicada.** A estimativa de "~20h" que aparece em textos antigos está
errada (nunca bateu com `DurationMinutes = 25`).

A campanha **pode ser interrompida e retomada** (§7) — não precisa ser 26h ininterruptas, mas cada
interrupção custa o warmup do detector de novo e introduz uma descontinuidade no estado adaptativo
do Analyzer. Preferir blocos longos.

---

## 3. Pré-requisitos — verificar TODOS antes de disparar

Rode e confira cada saída. Qualquer item fora do esperado: resolver antes, nunca "vai que dá".

```bash
# 1. Infra e cluster de pé (11 containers)
docker ps --format "table {{.Names}}\t{{.Status}}"
#    esperado: k3d-norn-server-0, k3d-norn-serverlb, k3d-norn-registry, k3d-norn-tools,
#              norn-postgres, norn-redis, norn-rabbitmq, norn-prometheus, norn-grafana,
#              norn-otel-collector, norn-tempo

# 2. Shop saudável, baseline de 1 réplica
kubectl get pods -n norn-shop          # 3 pods, todos 1/1 Running
kubectl get deployments -n norn-shop   # 1/1 em cada

# 3. Catalog com memória limpa (se estiver alto, ver §8, modo de falha "memória residual")
kubectl top pod -n norn-shop           # catalog-api esperado < 150Mi

# 4. LLM carregado na GPU (braço B depende disso; frio, a 1ª chamada estoura o timeout de 10s)
ollama ps                              # norn-qwen, "100% GPU"

# 5. Estado da plataforma limpo
docker exec norn-redis redis-cli GET norn:platform:config:mode              # Observe
docker exec norn-redis redis-cli HGETALL norn:chaos:active                  # vazio
docker exec norn-redis redis-cli GET shop:flags:payment.gateway.bypass      # False

# 6. Retenção do Prometheus com folga (limite: 15GB OU 15d, o que vier primeiro — subido de 3GB
#    pra 15GB numa sessão de acompanhamento, já refletido em compose.otel.yaml; recriar o
#    container manualmente só é necessário se este valor mudar nesta máquina de novo)
docker exec norn-prometheus sh -c "du -sh /prometheus"
#    < 10GB: seguro pras 25h. > 10GB: checar de novo no meio (§6) — a folga é grande, mas não infinita.

# 7. Ferramental de captura instalado (uma vez só)
ls tools/PanelCapture/node_modules > /dev/null && echo "PanelCapture OK"
#    se faltar: (cd tools/PanelCapture && npm install)
ls ~/AppData/Local/ms-playwright | grep chromium
#    se faltar: npx playwright install chromium

# 8. Ambiente Python da análise
tools/analysis/.venv/Scripts/python.exe --version

# 9. Dashboard buildado (senão os screenshots do dashboard saem sem conteúdo)
ls src/Platform/Norn.API/wwwroot/index.html

# 10. Nenhum Worker órfão rodando (o run-campaign.ps1 recusa iniciar se houver; uma Norn.API
#     órfã não é recusada, só reaproveitada — não custa conferir)
powershell -NoProfile -Command "Get-CimInstance Win32_Process -Filter \"Name='dotnet.exe'\" | Where-Object { \$_.CommandLine -like '*Norn.Worker*' -or \$_.CommandLine -like '*Norn.API*' } | Select-Object ProcessId, CommandLine"
#    esperado: vazio

# 11. Árvore de trabalho limpa (o git_commit_sha vai pra experiment_runs — precisa ser honesto)
git status

# 12. Nenhum manifesto de campanha anterior atravessado
ls docs/experiments/campaign-manifest.csv 2>/dev/null
#    Se EXISTIR e não for desta campanha: apagar antes de disparar. O run-campaign.ps1
#    reaproveita o manifesto existente em vez de sortear de novo (é o que permite retomar um
#    lote), então um manifesto velho faria a campanha nova rodar a ordem antiga — com o
#    -MasterSeed novo gravado nas linhas, que passaria a ser uma mentira no dado.
```

### Checklist de higiene da máquina (tarefa 3a — o script pergunta, mas quem confere é humano)

- [ ] Windows Update pausado pelo período do lote
- [ ] Suspensão/hibernação desativadas, inclusive com a tampa fechada
- [ ] Atualização automática do Docker Desktop desligada
- [ ] Plano de energia de alto desempenho, **na tomada** (o script recusa iniciar na bateria)
- [ ] IDE pesada e navegador fechados — ver §5, "o que NÃO fazer durante a campanha"

---

## 4. Preparação — o que ainda fica por conta de quem opera

O `run-campaign.ps1` sobe **`Norn.Worker` e `Norn.API`** sozinho (a API na porta 5080 — a 5000
default costuma estar ocupada pelo `wslrelay.exe` do WSL2), com o mesmo ciclo de vida do Worker:
sobe uma vez para o lote inteiro, aguarda `/health/ready` antes de prosseguir, encerra no fim
(`-SkipApiManagement` pula isso, para quando já houver uma rodando por fora). Sem a API, os
screenshots do dashboard falhariam em silêncio (best-effort por design em `capture.js`) — por
isso deixou de ser passo manual.

Resta só:

### 4.1 Aquecer o Ollama (se `ollama ps` não mostrou o modelo carregado)

```bash
ollama run norn-qwen "Responda apenas com a palavra ok."
```

---

## 5. Disparo

```bash
mkdir -p /c/git/norn-results/logs
cd /c/git/norn && echo "s" | nohup powershell -NoProfile -File deploy/run-campaign.ps1 \
  -MasterSeed 20260919 \
  > /c/git/norn-results/logs/campaign-console.log 2>&1 &
disown
```

- `-MasterSeed` é **obrigatório** e vai para `experiment_runs.randomization_seed` — escolha um
  número e **anote no TCC** (reprodutibilidade). Trocar o seed depois invalida a comparação.
- O `echo "s"` responde o checklist interativo. Só faça isso depois de ter conferido os itens de
  verdade (§3) — o script confia na resposta.
- Defaults que **não** devem ser alterados sem registrar o motivo: `-Repetitions 5`,
  `-DurationMinutes 25`, `-InjectionPhaseSeconds 300`, `-BackupAfterEachBlock $true`.

O manifesto é gerado em `docs/experiments/campaign-manifest.csv` na primeira execução e
**reaproveitado** depois — é o que permite retomar sem re-sortear a ordem (§7).

### Confirmar que realmente começou (2 min depois)

```bash
powershell -NoProfile -Command "Get-CimInstance Win32_Process -Filter \"Name='powershell.exe' or Name='dotnet.exe'\" | Where-Object { \$_.CommandLine -like '*run-campaign*' -or \$_.CommandLine -like '*Norn.Worker*' -or \$_.CommandLine -like '*Norn.API*' } | Select-Object ProcessId, Name"
tail -n 20 /c/git/norn-results/logs/campaign-console.log
curl -s -o /dev/null -w "Norn.API /health/ready -> %{http_code}\n" http://localhost:5080/health/ready
```

Esperado: um `powershell.exe` (run-campaign) e dois `dotnet.exe` (Norn.Worker e Norn.API), o log na
fase de warmup de 150s, e `200` no `/health/ready` (a API fica pronta bem antes do Worker, ~10-15s).

### O que NÃO fazer durante as 26h

Isto não é etiqueta — é validade experimental. A execução é **descartada** (`InvalidInstrumentation`)
se a carga entregue ficar fora de ±10% do alvo, e o gerador disputa CPU com o WSL2.

- ❌ `dotnet build`, `dotnet test`, `npm run build` — competem por CPU e enviesam `achieved_rps`
- ❌ abrir/manter o dashboard no navegador por longos períodos (os screenshots já cobrem isso)
- ❌ subir um segundo `Norn.Worker`, `Norn.LoadGenerator` ou rodar `run-experiment.ps1` avulso
- ❌ mexer em `.wslconfig`, flags do Redis, modo da plataforma ou RBAC
- ❌ `kubectl scale` manual, `kubectl delete pod`, reiniciar containers de infra
- ✅ consultas de leitura (SQL, `kubectl get`, `curl` pontual, `tail` de log) — custo desprezível

---

## 6. Monitoramento durante a campanha

**Cadência sugerida: a cada ~30-60 min.** Todos os comandos abaixo são leitura pura.

```bash
# A. Processos vivos (o mais importante — se o Worker morreu, o lote inteiro segue sem decisão)
powershell -NoProfile -Command "Get-CimInstance Win32_Process -Filter \"Name='powershell.exe' or Name='dotnet.exe'\" | Where-Object { \$_.CommandLine -like '*run-campaign*' -or \$_.CommandLine -like '*Norn.Worker*' } | Select-Object ProcessId, Name"

# B. Progresso (quantas execuções fecharam, e como)
docker exec norn-postgres psql -U norn -d norn -c \
  "select scenario, arm, termination_state, count(*) from platform.experiment_runs \
   where started_at_utc > now() - interval '30 hours' group by 1,2,3 order by 1,2,3;"

# C. Última linha útil do console e do Worker
tail -n 5 /c/git/norn-results/logs/campaign-console.log
grep -iE "circuit breaker|erro|exception|fatal" /c/git/norn-results/logs/worker-campaign.log | tail -n 10

# D. Artefatos acumulando
wc -l tools/analysis/data/labeled-runs.csv tools/analysis/data/discarded-runs.csv
ls /c/git/norn-results/screenshots/executions | wc -l   # ~2 por execução concluída
ls /c/git/norn-results/postgres-backups | wc -l         # 1 a cada 3 execuções

# E. Saúde do cluster
kubectl get pods -n norn-shop

# F. Retenção do Prometheus (a cada ~6h)
docker exec norn-prometheus sh -c "du -sh /prometheus"
```

### Critérios de PARADA — decisão mecânica, sem julgamento

Pare a campanha (§7, e depois diagnostique) se **qualquer** um ocorrer:

| Condição | Por quê |
|---|---|
| As 3 primeiras execuções saírem todas em `Invalid*` | Algo sistêmico está errado; 25h produziriam lixo |
| Taxa de descarte > 30% depois de 12 execuções | Idem, com amostra suficiente para não ser azar |
| Processo do `Norn.Worker` ausente | Execuções seguintes rodam sem decisor nenhum — braços B e C viram braço A silenciosamente |
| Pods do Shop em `CrashLoopBackOff` por mais de 1 bloco | Alvo não está mais em condição de ser medido |
| `du /prometheus` > 2.5GB | Evicção iminente da janela inicial (limite 3GB) |
| `circuit breaker aberto` no log do Worker | Modo forçado para `Observe` — o braço deixou de atuar (ADR-04, barreira c) |

**Não pare** por: execuções individuais `CensoredAtWindowEnd` (é resultado legítimo, não falha),
`PartiallyApplied` em `RestartPod` (comportamento conhecido do F1), ou `FALHOU` de screenshot
(best-effort por design).

---

## 7. Interrupção e retomada

**Parar com segurança:**

```bash
# 1. Descobrir os PIDs
powershell -NoProfile -Command "Get-CimInstance Win32_Process -Filter \"Name='powershell.exe' or Name='dotnet.exe'\" | Where-Object { \$_.CommandLine -like '*run-campaign*' -or \$_.CommandLine -like '*Norn.Worker*' -or \$_.CommandLine -like '*LoadGenerator*' } | Select-Object ProcessId, Name, CommandLine"

# 2. Matar o run-campaign primeiro, depois o Worker
powershell -NoProfile -Command "Stop-Process -Id <PID_campaign> -Force"
powershell -NoProfile -Command "Stop-Process -Id <PID_worker> -Force"

# 3. SEMPRE conferir que o caos não ficou preso ativo (contaminaria a retomada)
docker exec norn-redis redis-cli HGETALL norn:chaos:active
#    se não estiver vazio: docker exec norn-redis redis-cli DEL norn:chaos:active

# 4. Estado seguro
cd /c/git/norn && dotnet run --project tools/Norn.Labeler -- reset --arm A   # mode=Observe, flags false
kubectl scale deployment/catalog-api deployment/order-api deployment/payment-api -n norn-shop --replicas=1
```

**Retomar** (o manifesto é reaproveitado; a ordem aleatorizada não muda):

```bash
# Descobrir a próxima posição: o maior run_order DESTA campanha + 1.
# Filtrar por randomization_seed, não por data: os 3 pilotos também gravaram linhas em
# experiment_runs com run_order baixo, e uma janela de tempo os pegaria junto.
docker exec norn-postgres psql -U norn -d norn -t -c \
  "select max(run_order) from platform.experiment_runs where randomization_seed = <MasterSeed>;"

cd /c/git/norn && echo "s" | nohup powershell -NoProfile -File deploy/run-campaign.ps1 \
  -MasterSeed 20260919 -StartFromRunOrder <N+1> \
  >> /c/git/norn-results/logs/campaign-console.log 2>&1 &
disown
```

> **O `-MasterSeed` na retomada tem que ser o mesmo do disparo original.** Ele é gravado em cada
> linha de `experiment_runs`; divergir quebra a rastreabilidade da aleatorização.

Uma execução interrompida no meio fica sem `termination_state` — **descarte-a manualmente** do
`labeled-runs.csv` se tiver entrado lá, e registre no TCC quantas execuções foram perdidas por
interrupção operacional (é honestidade metodológica, não demérito).

---

## 8. Modos de falha conhecidos

| Sintoma | Causa | Ação |
|---|---|---|
| Catalog nasce com ~350Mi e entra em `CrashLoopBackOff` | Memória retida pelo F1 não é liberada só ao desativar o caos | Já automatizado no teardown (`scale 0→1`). Se persistir: `kubectl scale deployment/catalog-api -n norn-shop --replicas=0` e depois `--replicas=1` |
| `norn:chaos:active` não vazio entre execuções | `deactivate` via HTTP falhou (pod em crash loop) | O fallback (`DEL` no Redis) já roda sozinho. Conferir; se sobrou, apagar manualmente |
| Detecção para de disparar após muitas horas | Habituação de severidade (ADR-14) — a baseline adaptativa acompanha o valor alto | Esperado e **não é motivo de parada**: a aleatorização de blocos existe justamente para isso não virar confundidor. Registrar no TCC como fonte de variância |
| Séries do serviço somem do Prometheus | `OutOfMemoryException` interna quebra a exportação OTel do processo | Reiniciar o pod afetado (`scale 0→1`); se recorrente em blocos seguidos, parar a campanha |
| `DecidedBy: Fallback` em todo o braço B | Ollama frio ou descarregado | `ollama ps`; aquecer. Se recorrente, checar `OLLAMA_KEEP_ALIVE=-1` |
| `FALHOU norn-dashboard` em todo screenshot | `run-campaign.ps1` sobe a API sozinho, mas se `-SkipApiManagement` foi usado ou ela caiu, a captura falha em silêncio | Conferir `Get-CimInstance ... Norn.API`; subir manualmente se preciso — as execuções em si não são afetadas |
| `kubectl` com timeout depois de reboot | `host.docker.internal` aponta pro IP antigo | Trocar por `127.0.0.1:<porta>` em `~/.kube/config` |
| Pods do Shop em `CrashLoopBackOff` depois de reboot | CoreDNS perdeu `host.k3d.internal` | `k3d cluster stop norn && k3d cluster start norn` |
| Docker Desktop não subiu após reboot | Conhecido nesta máquina | `Start-Process "C:\Program Files\Docker\Docker\Docker Desktop.exe"` |

---

## 9. Encerramento e coleta final

Só depois da última execução (`Lote concluido: 60 execucoes.` no console).

```bash
# 1. Conferir que Worker e API já encerraram sozinhos (o run-campaign.ps1 derruba os dois no
#    próprio finally, ao concluir o lote) -- só derrubar manualmente se algum sobrou
powershell -NoProfile -Command "Get-CimInstance Win32_Process -Filter \"Name='dotnet.exe'\" | Where-Object { \$_.CommandLine -like '*Norn.Worker*' -or \$_.CommandLine -like '*Norn.API*' } | ForEach-Object { Write-Host \"Ainda vivo, encerrando: PID \$(\$_.ProcessId)\"; Stop-Process -Id \$_.ProcessId -Force }"

# 2. Estado seguro do ambiente
cd /c/git/norn && dotnet run --project tools/Norn.Labeler -- reset --arm A
kubectl get pods -n norn-shop

# 3. Dump final do Postgres
powershell -NoProfile -File deploy/dump-knowledge.ps1

# 4. H2 pareada (recalcula o RuleEngine offline sobre os contextos do braço B)
dotnet run --project tools/Norn.PairedAnalysis -- \
  --out "C:\git\norn-results\campaign-data\paired-analysis.csv"

# 5. Análise estatística (Kaplan-Meier, log-rank, Fisher, McNemar)
tools/analysis/.venv/Scripts/python.exe tools/analysis/analyze.py \
  --labeled tools/analysis/data/labeled-runs.csv \
  --paired "C:\git\norn-results\campaign-data\paired-analysis.csv" \
  --out-dir "C:\git\norn-results\analysis"
```

### 6. Verificar o backup — "backup não verificado não é backup" (§3 do Master Plan)

```bash
docker exec norn-postgres psql -U norn -d postgres -c "CREATE DATABASE norn_restore_test;"
docker exec -i norn-postgres psql -U norn -d norn_restore_test < "C:/git/norn-results/postgres-backups/<arquivo-mais-recente>.sql"
docker exec norn-postgres psql -U norn -d norn_restore_test -c "select count(*), min(started_at_utc), max(started_at_utc) from platform.experiment_runs;"
#    conferir que a contagem bate com a do banco real
docker exec norn-postgres psql -U norn -d postgres -c "DROP DATABASE norn_restore_test;"
```

### 7. Versionar os CSVs (regra existente: "versionados manualmente quando a campanha fechar")

```bash
cd /c/git/norn && git add tools/analysis/data/labeled-runs.csv tools/analysis/data/discarded-runs.csv docs/experiments/campaign-manifest.csv
git commit -m "Fase 12: dados da campanha completa (60 execucoes, seed <MasterSeed>)"
```

### 8. Registrar no CLAUDE.md

Seção "Estado atual": data da campanha, seed, quantas execuções válidas × descartadas por motivo,
e qualquer desvio operacional (interrupções, execuções refeitas). Sem isso, a próxima sessão não
consegue distinguir dado bom de dado com ressalva.

---

## 10. Do artefato para o TCC

| Artefato | Onde | Serve para |
|---|---|---|
| `analysis/resumo.md` | gerado por `analyze.py` | Números do capítulo de resultados (medianas, p-valores, taxas) |
| `analysis/*.png` | idem | Curvas de Kaplan-Meier por cenário (figura principal de H1) |
| `campaign-data/paired-analysis.csv` | `Norn.PairedAnalysis` | Tabela de contingência do McNemar (H2) |
| `campaign-data/labeled-runs.csv` | `Norn.Labeler` | Dataset bruto — anexo de reprodutibilidade |
| `campaign-data/discarded-runs.csv` | idem | Transparência: quantas execuções caíram e por quê |
| `screenshots/executions/*-prometheus-signature.png` | `capture.js` | Figuras "o cenário funcionou": a assinatura subindo na janela real |
| `screenshots/executions/*-norn-dashboard.png` | idem | Figuras do sistema operando (topologia, timeline MAPE-K) |
| `screenshots/blocks/*-grafana-*.png` | idem | Overhead do Norn e visão de infra |
| `postgres-backups/*.sql` | `dump-knowledge.ps1` | Reprocessar a análise inteira sem o ambiente no ar |
| `logs/worker-campaign.log` | Worker | Evidência de decisões, fallbacks e barreiras do ADR-04 |
| `experiment_runs` (no dump) | Postgres | Seeds, digest do modelo, commit SHA, clock de CPU — reprodutibilidade |

---

## 11. Notas para a sessão Claude (Sonnet 5) que for operar

Aprendizados do harness, colhidos nas sessões de piloto. Seguir evita retrabalho:

1. **Processos longos precisam ser destacados.** Use `nohup ... > log 2>&1 &` seguido de `disown`.
   Sem isso o processo morre junto com a ferramenta Bash.
2. **A notificação "completed" é falsa para processos destacados.** Ela reflete o *lançador*, não o
   processo real. Confirme sempre com `Get-CimInstance Win32_Process`, nunca confie na notificação.
3. **Polling: no máximo ~9 min por chamada Bash em background** (limite da ferramenta). Use um laço
   `while` com deadline próprio e re-arme quantas vezes precisar. `sleep` longo em primeiro plano é
   bloqueado.
4. **Não use `ScheduleWakeup`** — é só para sessões `/loop`.
5. **Prompt interativo** do `run-campaign.ps1`: `echo "s" | powershell -NoProfile -File ...`.
6. **Cuidado com CPU** (§5): durante a campanha, nada de build/teste. Uma chamada `dotnet build`
   para "só conferir uma coisa" pode invalidar a execução em andamento.
7. **Aspas em PromQL** passadas a executável nativo pelo PowerShell: use aspas simples dentro da
   query. Aspas duplas se perdem na travessia (mesmo achado do `kubectl -o jsonpath`).
8. **Screenshots são best-effort por design** — `FALHOU` no log não é motivo para intervir na
   campanha.

---

## 12. Resumo executável (a ordem, sem as explicações)

1. Conferir os 12 pré-requisitos (§3) + checklist de higiene
2. Aquecer o Ollama se preciso (§4.1) — Worker e API sobem sozinhos com o `run-campaign.ps1`
3. Disparar com `-MasterSeed` anotado (§5); confirmar que começou
4. Monitorar a cada 30-60 min (§6); aplicar os critérios de parada mecanicamente
5. Ao fim: derrubar processos, reset seguro, dump final, `PairedAnalysis`, `analyze.py` (§9)
6. Verificar o backup restaurando um dump (§9.6)
7. Versionar CSVs e registrar no CLAUDE.md (§9.7, §9.8)
