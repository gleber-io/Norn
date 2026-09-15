# ADR-16 — Predição como configuração de runtime, não como build

**Status:** aceito.

## Contexto
A Fase 13 acrescenta um detector de forecasting ao `Norn.Analyzer` e varre quatro horizontes de previsão × 5 repetições. Como ligar/desligar a predição e como variar o horizonte entre execuções, sem comprometer a rastreabilidade do binário que produziu H1/H2.

## Decisão
**Configuração de runtime no Redis**, chave `norn:platform:config:forecast` com `enabled` e `horizonMinutes` (§5.7), lida por `IPlatformConfig` com invalidação por pub/sub, padrão `false`, gravada em `experiment_runs` a cada execução.

## Alternativas descartadas
- Horizonte compilado (um build por horizonte) — drift que a §3 proíbe; impediria afirmar que todas as execuções vieram do mesmo binário.
- Flag da predição dentro do catálogo de flags do Shop — aquele catálogo é alvo de ação de cura; a plataforma poderia "curar" desligando o próprio preditor.
- Variável de ambiente — exigiria restart do Worker, quebrando a troca de modo em runtime do ADR-05.

## Consequência
H3 ganha a mesma proteção que o ADR-05 deu aos braços A/B/C: controle e tratamento são o mesmo binário. A varredura de horizontes da Fase 13 vira um laço no script de execução, sem rebuild.

**Limitação:** a flag desligada mantém comportamento idêntico, mas não torna o binário idêntico ao da campanha — a tag de congelamento do commit (DoD da Fase 12) é o que sustenta a rastreabilidade de H1/H2.

**Métrica:** a Fase 13 prevê sobre `dotnet.process.memory.working_set` — a mesma série que o detector reativo observa — para que H3 seja comparável com H1.
