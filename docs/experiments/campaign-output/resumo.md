# Resumo da análise da campanha

## H1 — taxa de recuperação e tempo até a recuperação

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

### F1
![Kaplan-Meier F1](kaplan-meier-F1.png)

Log-rank (tempo até a recuperação, primário):
- overall_p_value: p=1.0000
- A_vs_B_p_value: p=1.0000
- A_vs_C_p_value: p=1.0000
- B_vs_C_p_value: p=1.0000

Fisher exato (taxa de recuperação na janela, secundário):
- A_vs_B_p_value: p=1.0000
- A_vs_C_p_value: p=1.0000
- B_vs_C_p_value: p=1.0000

### F2
![Kaplan-Meier F2](kaplan-meier-F2.png)

Log-rank (tempo até a recuperação, primário):
- overall_p_value: p=0.1020
- A_vs_B_p_value: p=0.1106
- A_vs_C_p_value: p=0.1564
- B_vs_C_p_value: p=0.2259

Fisher exato (taxa de recuperação na janela, secundário):
- A_vs_B_p_value: p=1.0000
- A_vs_C_p_value: p=1.0000
- B_vs_C_p_value: p=1.0000

### F3
![Kaplan-Meier F3](kaplan-meier-F3.png)

Log-rank (tempo até a recuperação, primário):
- overall_p_value: p=1.0000
- A_vs_B_p_value: p=1.0000
- A_vs_C_p_value: p=1.0000
- B_vs_C_p_value: p=1.0000

Fisher exato (taxa de recuperação na janela, secundário):
- A_vs_B_p_value: p=1.0000
- A_vs_C_p_value: p=1.0000
- B_vs_C_p_value: p=1.0000

### F5
![Kaplan-Meier F5](kaplan-meier-F5.png)

Log-rank (tempo até a recuperação, primário):
- overall_p_value: p=0.6756
- A_vs_B_p_value: p=0.4713
- A_vs_C_p_value: p=0.9873
- B_vs_C_p_value: p=0.4713

Fisher exato (taxa de recuperação na janela, secundário):
- A_vs_B_p_value: p=1.0000
- A_vs_C_p_value: p=1.0000
- B_vs_C_p_value: p=1.0000

## H2 — recálculo pareado (LLM x RuleEngine, braço B)

_paired-analysis: 281 linhas de execuções válidas; 14 linhas de 1 execuções descartadas removidas._

- Pares: 281 (de 19 execuções)
- Acerto da ação de referência: LLM 76/281, RuleEngine 80/281
- Concordância bruta LLM x RuleEngine: 0.4093
- McNemar: estatística=42.0000, p=0.7493
- Tabela: ambos certos=34, só LLM certo=42, só regra certa=46, ambos errados=159
- Diferença de acerto (LLM − RuleEngine) com IC 95% por bootstrap agrupado por execução: -0.0142 [-0.0863; 0.0550] (19 execuções reamostradas)

## Taxa de ação esperada e taxa de ação eficaz (ITT x por protocolo)

_decisions: 991 linhas de execuções válidas; 54 linhas de 3 execuções descartadas removidas._

"Por protocolo" no braço B restringe às decisões sem nenhum FailureReason do LLM registrado (LlmTrace.FailureReasons vazio) — DecidedBy sozinho não serve de filtro porque também vale Fallback quando o RuleEngine do braço C rejeita a própria ação candidata, sem nenhuma falha de LLM envolvida.

Ação esperada (decisão bate com a ação de referência do cenário):
```
braco     protocolo  acertos  total     taxa  ic95_low  ic95_high
    A           ITT      111    385 0.288312  0.245331   0.335475
    B           ITT       76    281 0.270463  0.221880   0.325236
    B por_protocolo       66    243 0.271605  0.219559   0.330760
    C           ITT      112    325 0.344615  0.295033   0.397828
```

Ação eficaz (SLO restaurado ÷ ações executadas — Succeeded/PartiallyApplied/Failed, exclui Rejected):
```
braco     protocolo  restauradas  executadas     taxa  ic95_low  ic95_high
    A           ITT            2           3 0.666667  0.207660   0.938508
    B           ITT           38         118 0.322034  0.244488   0.410801
    B por_protocolo           35         108 0.324074  0.243167   0.417067
    C           ITT           38          64 0.593750  0.471452   0.705431
```

Diferença de ação eficaz C − B com IC 95% por bootstrap agrupado por execução: 0.2717 [0.1322; 0.4166]. Medida descritiva, condicionada às ações que cada braço escolheu executar:
```
braco              acao  restauradas  executadas     taxa
    B        RestartPod            2          58 0.034483
    B           ScaleUp           32          56 0.571429
    B ToggleFeatureFlag            4           4 1.000000
    C        RestartPod            3          16 0.187500
    C           ScaleUp           20          33 0.606061
    C ToggleFeatureFlag           15          15 1.000000
```

Restauração verificada ÷ total de decisões do braço — denominador independente da composição das ações, mas não do número de decisões por execução de cada braço; não é inversão da taxa acima:
```
braco  restauradas  decisoes     taxa
    B           38       281 0.135231
    C           38       325 0.116923
```

## Taxa de fallback do LLM decomposta por motivo (braço B)

```
               motivo  ocorrencias  pct_das_decisoes_b
              Timeout           20            0.071174
PreconditionViolation           18            0.064057
 PromptBudgetExceeded            3            0.010676
```

Decisões com contingência (ao menos um motivo), por cenário:
```
scenario  com_contingencia  decisoes
      F1                15        78
      F2                 9        75
      F3                 7        58
      F5                 7        70
   total                38       281
```

## Latência do loop, decomposta em quatro etapas (mediana + IC 95%, ms)

_loop-latency: 991 linhas de execuções válidas; 54 linhas de 3 execuções descartadas removidas._

A etapa de correlação inclui a janela de 60s da Fase 7 por desenho — é latência de projeto, não ineficiência do laço (Master Plan §3).

```
        etapa  mediana_ms  ic95_low  ic95_high   n
  deteccao_ms    4991.255  4990.819   4992.030 991
correlacao_ms   65053.010 65049.823  65057.214 991
   decisao_ms       8.833     8.562      9.233 991
   atuacao_ms       6.134     5.982      6.382 606
```

Etapa de decisão por braço — a mediana acima é pooled entre A/B/C, e A+C (RuleEngine quase instantâneo) são a maioria das linhas; a diferença real só aparece separando por braço:
```
braco  mediana_ms  ic95_low  ic95_high   n
    A       7.509     7.142      7.772 385
    B    7047.382  6807.852   7326.312 281
    C       7.758     7.447      8.051 325
```

## MTTD — tempo até a detecção (mediana + IC 95%, segundos)

_mttd: 57 linhas de execuções válidas; 0 linhas de 0 execuções descartadas removidas._

```
scenario  mttd_mediana_s  ic95_low  ic95_high  n
      F1        4.659273  2.671564  11.671442 15
      F2        7.043286  4.142723   8.668369 15
      F3        1.668903  0.933979   3.166714 12
      F5       12.793816  7.731426  17.958740 15
   geral        5.674447  3.672798  10.506601 57
```

## Overhead do Norn

Não mensurável nesta campanha: a métrica exige CPU/memória dos Pods do Norn via cAdvisor do kubelet (Master Plan §3), mas Norn.Worker/Norn.API rodaram como processos no host (`dotnet run`) durante toda a campanha real, não como Pods no cluster — decisão operacional registrada desde a Fase 9/10/11, nunca revertida. Não há série de cAdvisor para o Norn neste dataset. Medir isso exigiria rodar a campanha (ou parte dela) com os manifestos `norn-platform.yaml`/`norn-api.yaml` já empacotados, mas nunca ligados ao `bootstrap.ps1`.
