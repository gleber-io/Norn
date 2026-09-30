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
- **Todas as análises complementares são restritas às 57 execuções válidas** (`valid_runs.py`, com o
  `labeled-runs.csv` como fronteira) e os intervalos sobre contextos/ações agrupados por execução usam
  bootstrap por agrupamento com semente fixa (`cluster_bootstrap.py`, 10.000 réplicas, semente
  `20260919`) — ver achado metodológico 8. Os números abaixo são os que a monografia relata.

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
      F1   C          0      5            0.0
      F2   A          5      5            1.0
      F2   B          5      5            1.0
      F2   C          5      5            1.0
      F3   A          0      5            0.0
      F3   B          0      4            0.0
      F3   C          0      3            0.0
      F5   A          2      5            0.4
      F5   B          3      5            0.6
      F5   C          2      5            0.4
```

**F1 e F3 nunca recuperaram dentro da janela, em nenhum braço** — todas as execuções válidas
saíram `CensoredAtWindowEnd`, e as curvas de Kaplan-Meier desses dois cenários só documentam a
censura. As causas não são as mesmas: **F1 tem mecanismo identificado** (a intensidade do caos não
reseta quando o `RestartPod` recria o Pod, e o Pod novo nasce acima do limiar de restauração — ver
armadilhas no `CLAUDE.md`), então a ausência de recuperação não se explica só pela duração da janela;
**em F3 nenhum mecanismo equivalente foi identificado**, e a janela curta é hipótese plausível, mas
não testada. H1 não pode ser confirmada nem refutada para F1/F3 a partir do *tempo* até a
recuperação.

**F2 fechou 100% de recuperação nos três braços** — inclusive o braço A (controle, sem atuação real
do Norn). Isso é esperado: a resiliência nativa do e-commerce (retry, circuit breaker) já absorve o
esgotamento de pool sozinha dentro da janela, então H1 não tem espaço para mostrar efeito em F2 — a
recuperação "sem atuação" já é o teto.

**F5 (controle negativo) mostrou o padrão esperado de controle**: recuperação parcial e mista nos
três braços (40%/60%/40%), sem o Norn precisar agir (ação de referência é "nenhuma").

Log-rank (tempo até a recuperação; global F2 p=0,102, F5 p=0,676, F1/F3 p=1,000 sem eventos) e
Fisher exato (taxa de recuperação, aplicado a cada par de braços A×B/A×C/B×C, sem emparelhamento por
bloco; p=1,000 em todas as comparações) — ver `docs/experiments/campaign-output/resumo.md` para a
tabela completa e os quatro gráficos de Kaplan-Meier. Nenhuma comparação atingiu p<0,05; com F1/F3
100% censurados e F2 100% recuperado em todos os braços, a variância disponível para o
log-rank/Fisher separar os braços é estruturalmente pequena neste dataset.

## H2 — decisão do LLM vs. decisão determinística

Recálculo pareado do `RuleEngine` sobre os **281 contextos das 19 execuções válidas do braço B**
(`Norn.PairedAnalysis`, tarefa 4a — o mesmo motor de regras do braço C, função pura, sem execução
nova; os 14 contextos da execução descartada F3/B/rep. 4 ficam fora):

| | RuleEngine acertou | RuleEngine errou | Total |
|---|---|---|---|
| LLM acertou | 34 | 42 | 76 (27,0%) |
| LLM errou | 46 | 159 | 205 |
| Total | 80 (28,5%) | 201 | 281 |

- **Concordância bruta LLM × RuleEngine: 40,93%** (115 de 281) — os dois decisores discordam na
  maioria das vezes.
- **McNemar exato: estatística=42, p=0,7493** — não significativo. Como os contextos estão agrupados
  por execução, a diferença de acerto (LLM − RuleEngine) vem com IC 95% por bootstrap agrupado por
  execução: **−1,4 ponto percentual [−8,6; +5,5]**.
- **Não significativo não é equivalente**: com concordância bruta abaixo de 50%, os dois acertam e
  erram, em grande parte, em contextos diferentes.
- Em **159 contextos (56,6%) nenhum dos dois decisores bateu com a ação de referência** — o
  desencontro mais informativo aqui não é LLM-vs-regra, é "os dois erram mais do que acertam contra o
  gabarito".

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
| B | ITT | 76/281 | 27,0% | [22,2%, 32,5%] |
| B | por protocolo | 66/243 | 27,2% | [22,0%, 33,1%] |
| C | ITT | 112/325 | 34,5% | [29,5%, 39,8%] |

As taxas de B e C referem-se a conjuntos distintos de contextos (execuções diferentes) — não são
comparação pareada; a comparação pareada é a tabela de H2 acima.

Ação eficaz (SLO restaurado ÷ ações executadas — `Succeeded`/`PartiallyApplied`/`Failed`, exclui
`Rejected` **e exclui `NoOp`**: ver achado metodológico 6 sobre por que NoOp não pode contar como
"executada"):

| Braço | Protocolo | Restauradas/Executadas | Taxa | IC 95% |
|---|---|---|---|---|
| A | ITT | 2/3 | 66,7%* | [20,8%, 93,9%] |
| B | ITT | 38/118 | 32,2% | [24,4%, 41,1%] |
| B | por protocolo | 35/108 | 32,4% | [24,3%, 41,7%] |
| C | ITT | 38/64 | 59,4% | [47,1%, 70,5%] |

*Braço A: n=3, e as 3 são contaminação entre execuções (achado metodológico 7) — não é
comportamento real do braço A, que por desenho (ADR-05, modo `Observe`) nunca deveria executar
ação nenhuma. Ler como **0/0, indefinido**, não como 66,7%.

**A diferença de ação eficaz é descritiva e condicionada à execução — não prova que o RuleEngine
cause recuperação superior.** C − B = 27,2 pontos percentuais, IC 95% por bootstrap agrupado por
execução [13,2; 41,7]. Mas os dois braços executaram conjuntos diferentes de ações, e a
"restauração" aqui é a verificação do Executor (leitura única ao fim da janela de verificação da
ação), não o critério de recuperação da execução do Labeler:

| Tipo de ação | B (LLM): restauradas/executadas | C (RuleEngine): restauradas/executadas |
|---|---|---|
| `RestartPod` | 2/58 (3,4%) | 3/16 (18,8%) |
| `ScaleUp` | 32/56 (57,1%) | 20/33 (60,6%) |
| `ToggleFeatureFlag` | 4/4 (100%) | 15/15 (100%) |
| Total | 38/118 (32,2%) | 38/64 (59,4%) |

O LLM escolheu `RestartPod` em 49,2% das ações executadas (58/118), contra 25,0% (16/64) do
RuleEngine, e o `RestartPod` quase nunca restaura (5 de 74, somados os braços — compatível com a
limitação estrutural do F1). Para `ScaleUp` e `ToggleFeatureFlag` as taxas são próximas. Parte
relevante da diferença é **composição das ações escolhidas**, não eficácia de cada ação isolada. Com
o total de decisões do braço como denominador, as proporções de decisões seguidas de restauração
verificada são 13,5% (38/281) em B e 11,7% (38/325) em C — o que não é inversão do resultado: esse
denominador depende de quantas decisões cada braço produz por execução (14,8 em B contra 18,1 em C).

## Taxa de fallback do LLM decomposta por motivo (braço B)

Sobre as 281 decisões do braço B nas execuções válidas, **38 (13,5%) recorreram à contingência**
(ao menos um `FailureReason`). Por motivo — uma decisão pode empilhar mais de um, então a soma das
ocorrências passa de 38:

| Motivo | Ocorrências | % das decisões de B |
|---|---|---|
| `Timeout` | 20 | 7,1% |
| `PreconditionViolation` | 18 | 6,4% |
| `PromptBudgetExceeded` | 3 | 1,1% |

Por cenário: F1 15/78, F2 9/75, F3 7/58, F5 7/70. Das decisões por contingência, 10 viraram ação
executada, 3 com SLO restaurado. Excluí-las (por protocolo) muda pouco: ação esperada 27,2% (66/243),
ação eficaz 32,4% (35/108).

Nenhuma ocorrência de `InvalidJson`, `ActionNotInCatalog` ou `ConnectorError` na campanha real —
esses modos de falha, embora cobertos por teste unitário desde a Fase 8, não se manifestaram aqui.

## Latência do loop (mediana + IC 95%, ms)

```
        etapa  mediana_ms   ic95_low  ic95_high    n
  deteccao_ms      4991.3     4990.8     4992.0  991
correlacao_ms     65053.0    65049.8    65057.2  991
   decisao_ms         8.8        8.6        9.2  991
   atuacao_ms         6.1        6.0        6.4  606
```

`deteccao_ms` (~5s) bate com o intervalo de polling do Monitor. `correlacao_ms` (~65s) é a janela de
correlação de 60s da Fase 7 **por desenho**, não ineficiência — reportada separada do resto por
esse motivo (Master Plan §3).

**A etapa de decisão pooled (8,8ms) esconde a pergunta que mais importa aqui** — A e C (RuleEngine)
são 72% das 991 linhas e decidem quase instantaneamente, então dominam a mediana pooled. Separado
por braço:

| Braço | Mediana (ms) | IC 95% | n |
|---|---|---|---|
| A | 7,5 | [7,1; 7,8] | 385 |
| B | 7047,4 | [6807,9; 7326,3] | 281 |
| C | 7,8 | [7,4; 8,1] | 325 |

**O braço B paga ~7 segundos por decisão contra ~7,8 milissegundos do RuleEngine — cerca de
novecentas vezes, três ordens de grandeza.** Esse é provavelmente o achado de latência mais nítido
de toda a campanha, e é inteiramente esperado (round-trip real ao Ollama local vs. avaliação de
tabela em memória).

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
   excluir `NoOp` do denominador, o braço C saía com 12,6% de eficácia; excluindo, sai 59,7% (59,4%
   depois do filtro de execuções válidas do achado 8) — quase 5x de diferença por um único filtro.
   Corrigido em `campaign_metrics.py` antes de qualquer número deste documento ser calculado a
   partir dele.
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
   indefinida/artefato na tabela acima, não como 66,7% real. Conferido depois contra o
   `window_end_at_utc` de cada execução: os 8 registros ficam **entre 521 e 658 segundos após o fim da
   janela de observação**, então nenhum interferiu no desfecho de recuperação.
8. **As métricas complementares incluíam decisões das 3 execuções descartadas — corrigido na revisão
   da monografia.** `decisions.csv`, `loop-latency.csv` e `paired-analysis.csv` vêm direto do
   Knowledge/Norn.PairedAnalysis e trazem todas as execuções com `termination_state`, inclusive as
   `InvalidNoOnset` de F3 (54 decisões e 14 pares). A primeira versão deste documento relatava 295
   pares, 41,36% de concordância, ação eficaz de 31,4% × 59,7% e contingência de 16,0% (soma de
   motivos, não de decisões) com essas linhas dentro, e o log-rank de F5 (p=0,718) ainda com as 3
   execuções-piloto removidas do dataset depois. O `analyze.py` agora filtra tudo pelo
   `labeled-runs.csv` (`valid_runs.py`, fronteira do Labeler) e acompanha a comparação pareada e a
   diferença de ação eficaz com IC por bootstrap agrupado por execução (`cluster_bootstrap.py`) —
   contextos da mesma execução não são independentes. O bootstrap não é estratificado por cenário
   (escolha conservadora: estratificar estreitaria os intervalos). Nenhuma conclusão mudou de
   direção; os números deste documento são os da versão corrigida.

## Ameaças à validade

- **F1 e F3 nunca recuperaram dentro da janela em nenhum braço** — em F1 há mecanismo identificado
  (intensidade do caos que não reseta com o `RestartPod`); em F3 a janela curta é hipótese não
  testada. H1 não tem poder estatístico para esses dois cenários neste dataset; demonstrar diferença
  exigiria nova campanha com cenários recalibrados.
- **F2 saturou em 100% de recuperação em todos os braços, inclusive o controle** — a resiliência
  nativa do e-commerce já resolve esse cenário sozinha dentro da janela, então H1 não tem espaço
  para mostrar efeito ali por motivo oposto (teto, não falta de poder).
- **N pequeno por cenário×braço (3–5 execuções)** — intervalos de confiança amplos em quase toda
  métrica reportada aqui; qualquer leitura pontual de um número isolado (em vez do intervalo)
  sobre-interpreta o dado.
- **Concordância baixa entre LLM e RuleEngine (40,9%) mas nenhuma diferença estatística em H2** — os
  281 pares vêm de só 19 execuções e não são independentes; o IC agrupado da diferença de acerto
  ([−8,6; +5,5] p.p.) é o intervalo a ler, e a ausência de significância não é evidência de
  equivalência.
- **Assimetria de construção entre B e C** — a tabela de regras e a ação de referência foram
  definidas pelo mesmo autor, a partir do conhecimento das famílias de falha injetadas; o LLM precisou
  inferir a ação do contexto. Os resultados valem para esta configuração (modelo 4B quantizado,
  prompt e timeout deste trabalho), não para planejadores por LLM em geral.
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
