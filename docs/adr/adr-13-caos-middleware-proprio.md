# ADR-13 — Injeção de caos por middleware próprio

**Status:** aceito.

## Contexto
A Fase 5 precisa de quatro cenários progressivos (F1, F2, F3, F5) com intensidade em função do tempo, seed reprodutível e a métrica `norn_chaos_active{scenario,intensity}` com semântica uniforme — é dela que o rotulador depende para marcar o *onset*. `Polly.Contrib.Simmy` foi absorvido pelo Polly v8 ("Monkey" → "Chaos": `ChaosFault`, `ChaosOutcome`, `ChaosLatency`, `ChaosBehavior`), então a rampa em si é expressável em Polly — o problema não é a rampa.

## Decisão
**Middleware próprio em `Norn.BuildingBlocks.Chaos`**, com `IChaosScenario` expondo `Intensity(TimeSpan elapsed)` como função pura, uma implementação por cenário, ativação/cenário/seed lidos do Redis e emissão da métrica em um único ponto. **Nenhum pacote de caos.**

## Alternativas descartadas
- Polly v8 chaos para todos os cenários — apenas F3 é injeção de falha em chamada; F1 é acúmulo de estado no processo, F2 é estrangulamento de concorrência, F5 é queda abrupta. Nenhum dos três é governado por taxa de injeção por requisição.
- Polly no F3 e middleware nos demais — duas semânticas de `intensity` na mesma métrica, complica o rotulador.
- Chaos Mesh / Litmus no cluster — não alcança memória nem pool de conexões de dentro do processo; componente a mais no orçamento de 16 GB (ADR-09).

## Consequência
A rampa vira função pura testável isolada (tarefa 6 da Fase 5). Determinismo por seed sob controle total do projeto. As quatro implementações de cenário são código próprio a manter e calibrar. **Restrição:** o middleware é registrado apenas pelos três serviços do Shop e nunca pela plataforma Norn (`Norn.ArchitectureTests`).
