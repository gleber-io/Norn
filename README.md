# Norn

Plataforma de autocura ("self-healing") baseada no laço MAPE-K para microsserviços em Kubernetes,
desenvolvida como Trabalho de Conclusão de Curso do MBA em Engenharia de Software da USP/ESALQ.

**Monografia:** *Autocura de microsserviços em Kubernetes: planejamento por modelo de linguagem
versus regras determinísticas* — Gléber Michel Alves Schiavo; orientação de Elaine Barbosa de
Figueiredo.

O trabalho compara, em experimento controlado com três braços (controle sem atuação, decisão por
modelo de linguagem local e decisão por tabela de regras) e quatro famílias de falha injetadas, se o
planejamento por modelo de linguagem recupera o serviço mais rápido e com mais eficácia do que a
tabela de regras. Em `docs/experiments/results.md`, essas perguntas aparecem como H1 (o laço contra
o controle) e H2 (modelo de linguagem contra regras).

## Organização

| Caminho | Conteúdo |
|---|---|
| `src/Platform/` | Norn: Monitor, Analyzer (detecção por ML.NET), Planner (modelo de linguagem local e RuleEngine), Executor, Knowledge, Worker, API e as portas do núcleo em `Norn.Contracts` |
| `src/Shop/` | E-commerce de referência monitorado: Catalog, Order e Payment |
| `src/BuildingBlocks/` | Infraestrutura comum e middleware de injeção de falhas |
| `web/norn-dashboard/` | Painel React que acompanha o laço em tempo real |
| `tools/` | Gerador de carga, Labeler (início da falha e recuperação), PairedAnalysis, captura de painéis e análise estatística em Python |
| `deploy/` | Infraestrutura local (Docker Compose), manifestos Kubernetes, Modelfile do modelo e scripts da campanha |
| `docs/adr/` | Registros de decisão de arquitetura |
| `docs/rule-table.md` | Tabela de regras do braço C (motor de regras) |
| `docs/metrics-matrix.md` | Matriz de métricas monitoradas e assinatura das famílias de falha |
| `docs/norn-api-contract.md` | Contrato da Norn.API (rotas, DTOs e eventos do painel) |
| `docs/experiments/` | Protocolo, runbook, calibrações e resultados da campanha |
| `tests/` | Testes de arquitetura, unidade e integração |

## Campanha experimental e resultados

- **Código que executou a campanha:** tag
  [`campaign-h1h2`](https://github.com/gleber-io/Norn/tree/campaign-h1h2).
- **Dataset curado e análise que reproduzem os números da monografia:** ramo `master`. Depois da tag
  vieram a remoção de 3 execuções-piloto do `labeled-runs.csv` e a restrição das métricas
  complementares às 57 execuções válidas, com intervalos por bootstrap agrupado (achado metodológico
  8 do `results.md`). Rodar a análise na tag reproduz a versão anterior desses números.
- **Dados da campanha:** [`tools/analysis/data/`](tools/analysis/data/) — `labeled-runs.csv`
  (57 execuções válidas), `discarded-runs.csv` (os 3 descartes da campanha e 3 execuções-piloto
  anteriores a ela, mantidas para comparação), `paired-analysis.csv`, `decisions.csv`,
  `loop-latency.csv` e `mttd.csv`; ordem sorteada em
  [`docs/experiments/campaign-manifest.csv`](docs/experiments/campaign-manifest.csv).
- **Resultados, achados metodológicos e ameaças à validade:**
  [`docs/experiments/results.md`](docs/experiments/results.md).
- **Resumo gerado pela análise e curvas de sobrevivência:**
  [`docs/experiments/campaign-output/`](docs/experiments/campaign-output/) — `resumo.md`, gráficos
  de Kaplan-Meier e a planilha `kaplan-meier.xlsx` usada nas figuras da monografia.

### Reproduzir a análise

Requer Python 3.12 ou 3.13 (as versões fixadas em `requirements.txt` não têm pacote pronto para o
3.14).

```powershell
python -m venv tools/analysis/.venv
tools/analysis/.venv/Scripts/pip install -r tools/analysis/requirements.txt

tools/analysis/.venv/Scripts/python tools/analysis/analyze.py `
  --labeled tools/analysis/data/labeled-runs.csv `
  --paired tools/analysis/data/paired-analysis.csv `
  --decisions tools/analysis/data/decisions.csv `
  --loop-latency tools/analysis/data/loop-latency.csv `
  --mttd tools/analysis/data/mttd.csv `
  --out-dir docs/experiments/campaign-output

tools/analysis/.venv/Scripts/python -m unittest discover -s tools/analysis/tests
```

## Executar a plataforma

Requisitos: driver NVIDIA, .NET SDK `10.0.4xx`, Node 22+/24 LTS, Docker Desktop (backend WSL2),
kubectl, k3d, Ollama com CUDA e Python 3.12 ou 3.13.

```powershell
dotnet build
dotnet test

ollama pull qwen3:4b-instruct-2507-q4_K_M
ollama create norn-qwen -f deploy/ollama/Modelfile   # modelo do braço B, com OLLAMA_KEEP_ALIVE=-1

deploy/bootstrap.ps1          # infraestrutura, observabilidade, cluster k3d e o e-commerce, do zero
```

Para executar a campanha — pré-requisitos, disparo, acompanhamento, parada e coleta — siga o runbook
[`docs/experiments/plano-campanha.md`](docs/experiments/plano-campanha.md). A matriz completa, em
blocos aleatorizados, é disparada por `deploy/run-campaign.ps1 -MasterSeed 20260919`, que sobe o
Norn.Worker e a Norn.API.

O plano de execução detalhado é mantido fora do repositório; as decisões que orientam o código estão
nos ADRs em [`docs/adr/`](docs/adr/).
