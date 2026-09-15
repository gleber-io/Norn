# Workspace — em que máquina isto rodou

Transcrito de `C:\git\ambiente-fase00.txt` (Fase 00, tarefa 16), a saída literal dos comandos de verificação.

## Máquina

Dell G15 (D7) — Ryzen 7 5800H, 16 GB RAM, RTX 3060 Laptop 6 GB VRAM, Windows 11.

## Versões apuradas (14/09/2026)

| Ferramenta | Versão |
|---|---|
| Driver NVIDIA | 581.95 (CUDA 13.0) |
| Git | 2.36.1.windows.1 |
| .NET SDK | 10.0.401 (banda 10.0.4xx confirmada) |
| Node.js | v24.19.0 — **desvio do plano registrado abaixo** |
| npm | 11.17.0 |
| Docker Desktop | 28.3.2, backend WSL2 confirmado |
| kubectl | v1.32.2 |
| k3d | v5.7.1 (k3s v1.29.6-k3s1) |
| Ollama | 0.34.0 |
| Modelo LLM | `qwen3:4b-instruct-2507-q4_K_M`, 100% GPU, ~2,5 GB VRAM |
| Python | 3.13.3 |
| VS Code | 1.137.0 |
| Claude Code | 2.1.271 |

## `.wslconfig`

```ini
[wsl2]
memory=8GB
processors=10
sparseVhd=true
```

Aplicado via `wsl --shutdown` em 14/09/2026. Congela conforme ADR-09 — não muda até a campanha terminar.

## Variáveis de ambiente do Ollama (usuário, persistidas no registro)

| Variável | Valor |
|---|---|
| `OLLAMA_KEEP_ALIVE` | `-1` |
| `OLLAMA_MAX_LOADED_MODELS` | `1` |
| `OLLAMA_NUM_PARALLEL` | `1` |

Verificado com `ollama ps`: `UNTIL Forever`.

## MCPs ativos

Nenhum (`claude mcp list` vazio) — regra do projeto (Fase 00, tarefa 10).

## Desvio do plano: Node 22 → Node 24 LTS

O plano (§7.3) pinava Node 22 LTS. Em 14/09/2026, o pacote `OpenJS.NodeJS.LTS` do winget já resolve para a 24.19.0 — a linha LTS rotacionou desde a redação do plano (Node 22 é Maintenance LTS desde out/2025; Node 24 é a Active LTS atual). Decisão: seguir com Node 24 LTS. Atende o baseline do React Router v8 (§7.3: "Node 22.22+"), já que 24 é superset de requisitos.

## Ambiente de análise (Python)

`venv` isolado criado em `tools/analysis/.venv` (Fase 00, tarefa 4). Dependências (`lifelines`, `scipy`, `statsmodels`, `pandas`, `matplotlib`) entram na Fase 12.

## Tabela de datas por fase

| Fase | Início | Fim | Observação |
|---|---|---|---|
| 00 — Preparação da máquina | 14/09/2026 | 14/09/2026 | Máquina já tinha driver NVIDIA, Docker, kubectl, k3d, Python, VS Code e Claude Code instalados; faltavam .NET 10 SDK, Node atualizado e Ollama |
| 0 — Fundação do repositório | 14/09/2026 | | |
