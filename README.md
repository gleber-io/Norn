# Norn

Plataforma de self-healing baseada em MAPE-K para arquiteturas de microsserviços — Trabalho de Conclusão de Curso (AIOps / SRE).

O plano de execução completo — contratos, ADRs, convenções e as 14 fases — é mantido fora deste repositório (`NORN-MASTER-PLAN.md`, nunca commitado). Este README não o duplica; ADRs individuais ficam versionadas em [`docs/adr/`](docs/adr/).

## Requisitos da máquina

Ver Fase 00 do plano: driver NVIDIA, .NET SDK `10.0.4xx`, Node 22+/24 LTS, Docker Desktop (backend WSL2), kubectl, k3d, Ollama com CUDA, Python 3.12+.

## Comandos

```powershell
dotnet build
dotnet test
dotnet format --verify-no-changes

docker compose -f deploy/compose/compose.infra.yaml -f deploy/compose/compose.otel.yaml up -d
```

## Estado

Ver a seção "Estado atual" em [`CLAUDE.md`](CLAUDE.md) para a fase corrente.
