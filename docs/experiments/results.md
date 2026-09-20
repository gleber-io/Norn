# Resultados da campanha experimental (Fase 12)

## Reprodutibilidade

- **Tag git**: `campaign-h1h2` (anotada) — congela o commit cujo código produziu este dataset.
- **MasterSeed**: `20260919` (sorteio dos blocos, gravado em `experiment_runs.randomization_seed`).
- **Modelo LLM**: `norn-qwen` (Qwen3 4B Instruct-2507, quantização Q4_K_M), digest
  `sha256:85e4a5b7b8ef0e48af0e8658f5aaab9c2324c76c1641493f4d1e25fce54b18b9`.
- **SDK .NET**: `10.0.401` (`rollForward: latestPatch`).
- **`tools/analysis/requirements.txt`**: `lifelines==0.30.0`, `scipy==1.15.2`, `statsmodels==0.14.4`,
  `pandas==2.2.3`, `matplotlib==3.10.1`.
- **Dataset**: `tools/analysis/data/labeled-runs.csv`, `discarded-runs.csv`, `paired-analysis.csv`,
  `decisions.csv`, `loop-latency.csv`, `mttd.csv` — os três últimos extraídos do Knowledge via
  `tools/analysis/export-campaign-metrics.sql` (versionado, reproduz os mesmos números — verificado
  byte a byte contra o dataset commitado antes de escrever este documento).
- **Backup**: verificado restaurando um dump real de `C:\git\norn-results\postgres-backups\` num
  banco de teste isolado (`norn_restore_test`, apagado depois) e reprocessando o `Norn.PairedAnalysis`
  inteiro contra ele antes de rodar contra produção — ver "Achados metodológicos" abaixo.
- Gráficos em `docs/experiments/campaign-output/` (`resumo.md` + 4 PNG de Kaplan-Meier), gerados por
  `tools/analysis/analyze.py --labeled ... --paired ... --decisions ... --loop-latency ... --mttd ...`.

## Resumo da campanha

60 execuções tentadas (4 cenários × 3 braços × 5 repetições), ordem aleatorizada em blocos (seed
acima). **57 válidas (95%)**, 3 descartadas (`InvalidNoOnset`, todas em F3). Nenhuma execução válida
ficou fora de ±10% da carga alvo; `wsl_memory_gb`/`wsl_processors` idênticos nas 60 linhas (sem
deriva de configuração de recurso no meio da campanha).

| Cenário | Válidas | Descartadas |
|---|---|---|
| F1 (vazamento de memória) | 15/15 | 0 |
| F2 (esgotamento de pool) | 15/15 | 0 |
| F3 (latência do gateway) | 12/15 | 3 |
| F5 (controle negativo) | 15/15 | 0 |

## H1 — o loop de self-healing recupera mais rápido que a ausência de atuação?

Taxa de recuperação na janela (10 min a partir do onset), por cenário × braço:

```
scenario arm  recovered  total  recovery_rate
      F1   A          0      5            0.0
      F1   B          0      5            0.0
      F1   C          0      6            0.0
      F2   A          5      5            1.0
      F2   B          5      5            1.0
      F2   C          5      5            1.0
      F3   A          0      5            0.0
      F3   B          0      4            0.0
      F3   C          0      4            0.0
      F5   A          2      5            0.4
      F5   B          3      5            0.6
      F5   C          3      6            0.5
```

**F1 e F3 nunca recuperaram dentro da janela, em nenhum braço** — todas as execuções válidas
saíram `CensoredAtWindowEnd`. Não é falha do loop: é o mesmo achado de calibração já documentado nas
Fases 9–11 (o `RestartPod` do F1 sofre com a janela de verificação curta relativa à intensidade do
vazamento; F3 tem padrão análogo). H1 não pode ser confirmada nem refutada para F1/F3 a partir do
*tempo* até a recuperação — só o log-rank (abaixo) e a comparação com o braço A (que também nunca
recupera nesses dois cenários) ficam disponíveis, e nenhum dos dois separa os braços.

**F2 fechou 100% de recuperação nos três braços** — inclusive o braço A (controle, sem atuação real
do Norn). Isso é esperado: a resiliência nativa do e-commerce (retry, circuit breaker) já absorve o
esgotamento de pool sozinha dentro da janela, então H1 não tem espaço para mostrar efeito em F2 — a
recuperação "sem atuação" já é o teto.

**F5 (controle negativo) mostrou o padrão esperado de controle**: recuperação parcial e mista nos
três braços (40%/60%/50%), sem o Norn precisar agir (ação de referência é "nenhuma"). Confirma que o
sistema não trata falha abrupta como algo a curar às cegas.

Log-rank (tempo até a recuperação) e Fisher exato (taxa de recuperação), pareado A×B/A×C/B×C, por
cenário — ver `docs/experiments/campaign-output/resumo.md` para a tabela completa e os quatro
gráficos de Kaplan-Meier. Nenhuma comparação atingiu p<0,05; com F1/F3 100% censurados e F2 100%
recuperado em todos os braços, a variância disponível para o log-rank/Fisher separar os braços é
estruturalmente pequena neste dataset.

## H2 — decisão do LLM vs. decisão determinística

Recálculo pareado do `RuleEngine` sobre os 295 contextos reais do braço B (`Norn.PairedAnalysis`,
tarefa 4a — o mesmo motor de regras do braço C, função pura, sem execução nova):

- **Concordância bruta LLM × RuleEngine: 41,36%** — os dois decisores discordam na maioria das
  vezes.
- **McNemar: estatística=42,00, p=0,7493** — não significativo. A diferença entre "só o LLM acerta"
  (42 casos) e "só a regra acerta" (46 casos) não é estatisticamente distinguível de acaso.
- De 295 contextos, **173 (58,6%) nenhum dos dois decisores bateu com a ação de referência do
  cenário** — o desencontro mais informativo aqui não é LLM-vs-regra, é "os dois erram mais do que
  acertam contra o gabarito".

## Taxa de ação esperada e taxa de ação eficaz (ITT × por protocolo)

"Ação esperada" compara a decisão contra a ação de referência do cenário, verificável em *shadow*
(sustenta H2 acima). "Ação eficaz" exige que a ação tenha sido de fato executada e o SLO restaurado.
"Por protocolo" no braço B restringe às decisões sem nenhum `LlmTrace.FailureReasons` registrado —
**`DecidedBy == Fallback` não serve como filtro**: o braço C também usa esse valor quando o
`RuleEngine` rejeita a própria ação candidata por pré-condição, sem LLM nenhum envolvido (achado ao
construir esta análise — ver "Achados metodológicos").

Ação esperada:

| Braço | Protocolo | Acertos/Total | Taxa | IC 95% |
|---|---|---|---|---|
| A | ITT | 111/385 | 28,8% | [24,5%, 33,5%] |
| B | ITT | 76/295 | 25,8% | [21,1%, 31,0%] |
| B | por protocolo | 66/251 | 26,3% | [21,2%, 32,1%] |
| C | ITT | 112/365 | 30,7% | [26,2%, 35,6%] |

Ação eficaz (SLO restaurado ÷ ações executadas — `Succeeded`/`PartiallyApplied`/`Failed`, exclui
`Rejected` **e exclui `NoOp`**: ver achado metodológico 6 sobre por que NoOp não pode contar como
"executada"):

| Braço | Protocolo | Restauradas/Executadas | Taxa | IC 95% |
|---|---|---|---|---|
| A | ITT | 2/3 | 66,7%* | [20,8%, 93,9%] |
| B | ITT | 38/121 | 31,4% | [23,8%, 40,1%] |
| B | por protocolo | 35/111 | 31,5% | [23,6%, 40,7%] |
| C | ITT | 43/72 | 59,7% | [48,2%, 70,3%] |

*Braço A: n=3, e as 3 são contaminação entre execuções (achado metodológico 7) — não é
comportamento real do braço A, que por desenho (ADR-05, modo `Observe`) nunca deveria executar
ação nenhuma. Ler como **0/0, indefinido**, não como 66,7%.

**Achado real, não artefato: o RuleEngine (braço C) é quase 2x mais eficaz que o LLM (braço B) nas
ações que de fato chegam a ser executadas — 59,7% contra 31,4%.** Isso é consistente com H2 (baixa
concordância entre os dois decisores) e complementa a "ação esperada" acima: não é só que os dois
decidem coisas diferentes, é que a decisão do RuleEngine, quando executada, restaura o SLO com o
dobro da frequência. Combinado com F1/F3 saindo 100% `CensoredAtWindowEnd` em H1 (que achata as
taxas de recuperação por tempo, não por eficácia da ação em si), o quadro sugere que a diferença
B×C não está em *se* a ação é a certa — a taxa de ação esperada dos dois é parecida (25,8%/26,3% x
30,7%) — está em *como* ela se comporta depois de aplicada.

## Taxa de fallback do LLM decomposta por motivo (braço B)

Sobre as 295 decisões do braço B:

| Motivo | Ocorrências | % das decisões de B |
|---|---|---|
| `PreconditionViolation` | 23 | 7,8% |
| `Timeout` | 20 | 6,8% |
| `PromptBudgetExceeded` | 4 | 1,4% |

Nenhuma ocorrência de `InvalidJson`, `ActionNotInCatalog` ou `ConnectorError` na campanha real —
esses modos de falha, embora cobertos por teste unitário desde a Fase 8, não se manifestaram aqui.

## Latência do loop (mediana + IC 95%, ms)

```
        etapa  mediana_ms   ic95_low  ic95_high    n
  deteccao_ms      4991.2     4990.7     4991.9 1045
correlacao_ms     65052.5    65049.3    65056.8 1045
   decisao_ms         8.7        8.4        9.2 1045
   atuacao_ms         6.1        6.0        6.3  660
```

`deteccao_ms` (~5s) bate com o intervalo de polling do Monitor. `correlacao_ms` (~65s) é a janela de
correlação de 60s da Fase 7 **por desenho**, não ineficiência — reportada separada do resto por
esse motivo (Master Plan §3).

**A etapa de decisão pooled (8,7ms) esconde a pergunta que mais importa aqui** — A e C (RuleEngine)
são 72% das 1045 linhas e decidem quase instantaneamente, então dominam a mediana pooled. Separado
por braço:

| Braço | Mediana (ms) | IC 95% | n |
|---|---|---|---|
| A | 7,5 | [7,1; 7,8] | 385 |
| B | 7070,5 | [6816,7; 7375,8] | 295 |
| C | 7,6 | [7,3; 7,8] | 365 |

**O braço B paga ~7 segundos por decisão contra ~7,5 milissegundos do RuleEngine — três ordens de
magnitude.** Esse é provavelmente o achado de latência mais nítido de toda a campanha, e é
inteiramente esperado (round-trip real ao Ollama local vs. avaliação de tabela em memória).

## MTTD — tempo até a detecção (mediana + IC 95%, segundos)

```
scenario  mttd_mediana_s  ic95_low  ic95_high   n
      F1             4.7       2.7       11.7  15
      F2             7.0       4.1        8.7  15
      F3             1.7       0.9        3.2  12
      F5            12.8       7.7       18.0  15
   geral             5.7       3.7       10.5  57
```

Detecção em ordem de segundos após o onset real em todos os cenários — consistente com o intervalo
de polling de 5s do `Norn.Monitor`.

## Overhead do Norn

**Não mensurável nesta campanha.** A definição operacional (Master Plan §3) exige CPU/memória dos
*Pods* do Norn via cAdvisor do kubelet — mas `Norn.Worker`/`Norn.API` rodaram como processos no host
(`dotnet run`) durante toda a campanha real, decisão operacional tomada desde a Fase 9 e nunca
revertida (os manifestos `norn-platform.yaml`/`norn-api.yaml` existem e foram empacotados nas Fases
9/11, mas deliberadamente nunca ligados ao `bootstrap.ps1`). Não há série de cAdvisor para o Norn
neste dataset — medir isso exigiria repetir a campanha (ou uma amostra dela) com o Norn rodando de
fato como Pods no cluster.

## Achados metodológicos (relevantes para interpretar os números acima)

1. **F2 nunca esteve mal calibrado — era um ponto cego de detecção.** Na primeira passada da
   campanha, 13 das 15 execuções de F2 saíram `InvalidNoOnset` (87%). Investigação encontrou o
   mesmo bug estrutural que o F3 já tinha antes de ser corrigido: o `Norn.Labeler` só detectava
   onset via taxa de erro 5xx sustentada, mas a assinatura fechada do F2 (latência p99/profundidade
   de fila) nunca necessariamente eleva a taxa de erro — esgotamento de pool faz a requisição
   esperar, não necessariamente falhar. Corrigido generalizando para o F2 a mesma via de onset por
   latência sustentada que o F3 já tinha, e **as 13 execuções descartadas foram re-rotuladas contra
   o dado já retido no Prometheus, sem nenhuma execução nova** — todas as 13 fecharam `Recovered`.
   F2 foi de 13% para 100% de dado válido. A tag `campaign-h1h2` foi movida para o commit que inclui
   essa correção.
2. **`HealingPlan.DecidedBy == Fallback` não é um sinal confiável de "o LLM falhou".** O mesmo valor
   é usado pelo `RuleEngine` (braço C) quando sua própria ação candidata é rejeitada por
   pré-condição — sem LLM nenhum no caminho. O sinal correto de falha do LLM é
   `LlmTrace.FailureReasons` não vazio, independente do `DecidedBy` final (confirmado lendo
   `LlmPlanner.FallBackToRuleEngine`, que preserva o `DecidedBy` da chamada interna a
   `RuleEngine.Decide`). Toda a distinção ITT/por-protocolo deste documento usa esse critério, não
   `decided_by`.
3. **Uma fração pequena de decisões do braço A (7 de 385) e do braço C aparece com `DecidedBy=Llm`
   mesmo com `plannerBackend=RuleEngine` configurado para esses braços.** Padrão consistente com o
   cache de 5s do `IPlatformConfig.PlannerBackend` (Redis) não ter expirado ainda no exato instante
   de uma transição de braço dentro de um bloco — uma janela de corrida de poucos segundos, já
   documentada como comportamento esperado do cache (a invalidação por pub/sub é otimização de
   latência, o TTL é a garantia real). Não afeta a validade de H1 (o braço A nunca executa ação
   nenhuma, independente de quem decidiu) nem de H2 (que já restringe explicitamente ao braço B).
4. **Backup verificado com um passo extra além do runbook**: em vez de só restaurar e conferir
   contagens (§9.6 do runbook), o dump real foi restaurado num banco de teste isolado e o
   `Norn.PairedAnalysis` rodou contra ele de ponta a ponta antes de tocar o banco de produção —
   provando não só que o backup restaura, mas que **a análise inteira é reprocessável a partir
   dele**. Achado colateral: um bug real de cultura (`Console.WriteLine` sem `InvariantCulture` na
   taxa de concordância) foi encontrado e corrigido nesse processo — mesma classe de bug já
   corrigida antes no `Norn.Labeler`.
5. **As 3 execuções `InvalidNoOnset` remanescentes (todas F3) não mostraram o mesmo padrão do
   achado 1.** A série de latência do gateway nessas janelas tem poucas amostras não-`NaN` (indício
   de tráfego insuficiente ao Payment.API nessa execução específica, não um sinal ignorado) — não
   foi investigado mais a fundo por retorno decrescente depois do ganho grande do achado 1.
6. **`NoOp` não pode contar como "ação executada" na taxa de ação eficaz — achado do
   `code-reviewer` antes deste documento.** `HealingActionExecutor` trata `NoOp` como curto-circuito
   e devolve `Succeeded`/`SloRestored=false` sempre, mesmo sem tocar o cluster (o próprio Master
   Plan §5.4 já nomeia esse risco: "ação sem efeito real... contamina a taxa de ação eficaz"). Sem
   excluir `NoOp` do denominador, o braço C saía com 12,6% de eficácia; excluindo, sai 59,7% — quase
   5x de diferença por um único filtro. Corrigido em `campaign_metrics.py` antes de qualquer número
   deste documento ser calculado a partir dele.
7. **8 decisões atribuídas ao braço A (`Observe`, nunca deveria executar nada) produziram
   `HealingOutcome` real — contaminação sistemática entre execuções, não ruído aleatório.** As 8
   ocorrem entre 1575–1594s após o início de suas execuções — 75–94s **depois** da janela nominal
   de 25 min (1500s), todas na cauda. Mecanismo: o sinal/contexto é carimbado com o
   `ExperimentRunId` da execução A no instante da detecção (dentro da janela real de A), mas a
   correlação (~60s) e a decisão fecham **depois** que o próximo `reset` já reconfigurou o Redis
   para a execução seguinte — a maioria decidida por `Llm` confirma que a execução seguinte era
   braço B. Verificado que isso **não contamina H1**: nos cenários onde isso ocorreu (F1, F3), a
   taxa de recuperação do braço A já era 0% independente da contaminação; em F2 (100% de
   recuperação em todos os braços, resiliência nativa já basta) a ação vazada aplica tarde demais
   pra ser a causa real. Contamina só a taxa de ação eficaz do braço A, tratada como
   indefinida/artefato na tabela acima, não como 66,7% real.

## Ameaças à validade

- **F1 e F3 nunca recuperaram dentro da janela em nenhum braço** — a janela de verificação
  (120–240s) parece curta demais relativa à dinâmica desses dois cenários especificamente, um
  achado de calibração já apontado nas Fases 9–11 e reconfirmado aqui em escala real. H1 não tem
  poder estatístico para esses dois cenários neste dataset.
- **F2 saturou em 100% de recuperação em todos os braços, inclusive o controle** — a resiliência
  nativa do e-commerce já resolve esse cenário sozinha dentro da janela, então H1 não tem espaço
  para mostrar efeito ali por motivo oposto (teto, não falta de poder).
- **N pequeno por cenário×braço (4–6 execuções)** — intervalos de confiança amplos em quase toda
  métrica reportada aqui; qualquer leitura pontual de um número isolado (em vez do intervalo)
  sobre-interpreta o dado.
- **Concordância baixa entre LLM e RuleEngine (41%) mas nenhuma diferença estatística em H2** — com
  n=295 pares e uma concordância tão baixa, a ausência de significância não é evidência forte de
  equivalência; é o resultado honesto deste tamanho de amostra.
- **Overhead do Norn não mensurável** (ver seção própria acima) — omissão estrutural do desenho
  operacional desta campanha, não um resultado nulo.
- **Contaminação entre execuções na fronteira de blocos (achado metodológico 7)** — um número
  pequeno (8, todos no braço A) de decisões cruza a fronteira de reset entre execuções consecutivas.
  Verificado que não afeta H1; afeta só a taxa de ação eficaz do braço A, já tratada como artefato
  na tabela correspondente. Não investigado se o mesmo mecanismo afeta B×C entre si (blocos sempre
  trocam de braço dentro do mesmo cenário, então um vazamento B→C ou C→B teria o mesmo efeito
  estrutural, só que entre dois braços que já atuam de verdade — mais difícil de distinguir de
  comportamento genuíno sem o mesmo tipo de verificação de timestamp feita aqui para o braço A).
- **Máquina única, laptop, ~25h de execução real com blocos aleatorizados e temperatura/clock
  registrados por execução** — mitiga mas não elimina deriva térmica como confundidor; não
  investigado further neste documento além do registro exigido pelo DoD.
