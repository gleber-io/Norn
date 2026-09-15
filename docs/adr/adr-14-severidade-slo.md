# ADR-14 — Severidade por distância do SLO e ordenação determinística do `AnomalyContext`

**Status:** aceito.

## Contexto
Quando vários sinais do mesmo alvo caem na janela de 60s, o contrato (§5.3) obriga eleger um `primarySignal`, que alimenta os dois braços de tratamento (prompt do LLM e tabela de regras). Mal resolvida, essa eleição vira confundidor de **H2**. `severity` existia como enum `Low|Medium|High|Critical` sem nenhuma definição de cálculo.

## Decisão
1. **Severidade = proximidade da violação de SLO**, régua única para todas as métricas, bandas parametrizadas pela matriz da Fase 4 via `IOptions`, calculadas em ponto único.
2. Ordenação **determinística** do contexto: severidade desc → `detectedAtUtc` asc → `confidence` desc → `metricName` asc.
3. `primarySignal` é **ordem de leitura**, não hipótese de causa raiz.
4. O prompt do LLM carrega **todos** os sinais do contexto.
5. A tabela de regras indexa o **conjunto** de métricas alteradas, não o primário.

## Alternativas descartadas
- Limiares fixos por métrica — "High" de memória e "High" de latência deixariam de ser comparáveis.
- Ordenar por `confidence` — comparar p-valores de hipóteses nulas diferentes não tem significado estatístico.
- Ordenar por mais antigo — favoreceria sempre o detector mais rápido, independente da falha real.

## Consequência
Severidade com origem rastreável na matriz de métricas; desempate por `metricName` garante reprodutibilidade do mesmo conjunto de sinais. Como o primário não decide sozinho em nenhum braço, erro de calibração degrada a leitura, não o experimento.

**Limitação declarada:** indicadores antecedentes sem SLO próprio (RSS, tempo de GC, profundidade de fila) usam desvio relativo a `expectedValue` como aproximação.

**Verificação:** tarefa 5a da Fase 7 confere, com `norn_chaos_active` como ground truth, se o primário eleito corresponde à falha injetada em F1, F2 e F3.
