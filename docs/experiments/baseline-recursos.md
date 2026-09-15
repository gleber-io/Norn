# Linha de base de recursos (Fase 0, tarefa 12)

Medido em 14/09/2026 com infra (`compose.infra.yaml`) + observabilidade (`compose.otel.yaml`) no ar, Ollama com `norn-qwen` carregado, **cluster k3d ainda não criado** (nasce na Fase 6).

## Por container (`docker stats --no-stream`)

| Container | Memória | CPU % |
|---|---|---|
| norn-grafana | 68 MiB | 0,05% |
| norn-otel-collector | 30 MiB | 0,11% |
| norn-postgres | 30 MiB | 0,01% |
| norn-tempo | 29 MiB | 0,11% |
| norn-redis | 10 MiB | 2,71% |
| norn-rabbitmq | 109 MiB | 0,15% |
| norn-prometheus | 23 MiB | 2,46% |
| **Total containers** | **~300 MiB** | — |

## Portão de memória da Fase 00/0 — as três linhas

| Medir | Valor observado | Portão | Resultado |
|---|---|---|---|
| `vmmemWSL` (tudo que roda em Docker/WSL2) | **2,95 GB** | ≤ 4 GB | **Passa** — sobram ~5 GB dos 8 do `.wslconfig` para o cluster da Fase 6 |
| Ollama (VRAM) | modelo `norn-qwen` residente, GPU total em uso **4,77 GB / 6 GB** (inclui outros processos do sistema que também usam a GPU) | ~2,5 GB de VRAM, < 0,5 GB RAM | **Passa** para o modelo em si — ver nota abaixo sobre o restante da VRAM |
| Windows, tudo no ar, antes do cluster | **15,1 GB / 15,4 GB usados (0,3 GB livre)** | ≤ 9 GB de 16 | **Não passa** — ver nota abaixo |

## Nota sobre a terceira linha (não é o Norn)

O estouro do portão de Windows **não vem da pilha do Norn** — `vmmemWSL` (WSL2 + Docker + toda a infra acima) usa só 2,95 GB, e o Ollama fica em VRAM. O consumo vem de software pessoal da máquina rodando em paralelo no momento da medição: múltiplas janelas de VS Code e sessões do Claude Code, Steam e jogos instalados, Riot Vanguard, WhatsApp, antivírus, utilitários da Dell/Alienware/AMD, navegador. Isso confirma a nota da Fase 00 sobre o Visual Studio ("maior competidor por RAM... mantenha-o fechado durante a campanha") — a mesma disciplina precisa se estender a este conjunto mais amplo de software não relacionado ao projeto.

**Ação necessária antes da campanha (Fase 12), não antes da Fase 0:** fechar aplicativos pessoais (jogos, launchers, chat) e reduzir o número de janelas de IDE/Claude Code abertas simultaneamente. Isto é disciplina de operador, não código — não foi automatizado aqui.

**Efeito colateral na GPU:** a VRAM "em uso" de 4,77 GB inclui outros consumidores do sistema (compositor de janelas, overlays da NVIDIA App, etc.) além dos ~2,5–3,2 GB do modelo. Isso ainda deixa margem para a campanha, mas vale reconferir com o mínimo de software gráfico aberto antes das execuções.

## CPU

WSL2 limitado a 10 processadores lógicos (`.wslconfig`), deixando 6 ao host — Ollama e, a partir da Fase 5, o `Norn.LoadGenerator`.

## Disco

179 GB livres no momento da Fase 00, folga ampla acima dos 60 GB do orçamento (§ Fase 00).
