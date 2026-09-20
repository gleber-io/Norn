# Empacotamento (residual das Fases 9/11, ver CLAUDE.md) — deliberadamente NÃO ligado ao
# bootstrap.ps1: a validação ao vivo continua via `dotnet run` local, este Dockerfile só prova
# que a imagem builda. Front (web/norn-dashboard) entra num estágio Node próprio, sem rodar
# `generate:api` (exigiria a API no ar durante o build) — `src/lib/api-types.ts` já é commitado.
#
# Divergência assumida do Master Plan (Fase 11, tarefa 13): o texto original previa `dist/`
# construído fora, pelo bootstrap.ps1 (dotnet build -> generate:api -> npm run build -> docker
# build copiando dist/ já pronto), com o Dockerfile "sem Node". Como o bootstrap.ps1 nunca chegou
# a orquestrar Worker/API (nota acima), esse desenho ficaria com um Dockerfile que não builda
# sozinho — construir o front aqui dentro é o que mantém "docker build" funcionando isolado, ao
# custo de reintroduzir Node na imagem. Sem efeito sobre H1/H2: este caminho não está em uso.
FROM node:22-slim AS frontend-build
WORKDIR /web
COPY web/norn-dashboard/package.json web/norn-dashboard/package-lock.json ./
RUN npm ci
COPY web/norn-dashboard/ ./
RUN npm run build

# JIT — sem Native AOT (ADR-01). Publicação self-contained sobre runtime-deps, mesmo padrão do
# Dockerfile do Worker (src/Platform/Norn.Worker/Dockerfile) e do Shop (Norn.Shop.Catalog.API).
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY Directory.Build.props Directory.Packages.props nuget.config global.json .editorconfig ./
COPY src/Platform/Norn.API/Norn.API.csproj src/Platform/Norn.API/
COPY src/Platform/Norn.Contracts/Norn.Contracts.csproj src/Platform/Norn.Contracts/
COPY src/Platform/Norn.Knowledge/Norn.Knowledge.csproj src/Platform/Norn.Knowledge/
COPY src/BuildingBlocks/Norn.BuildingBlocks.Telemetry/Norn.BuildingBlocks.Telemetry.csproj src/BuildingBlocks/Norn.BuildingBlocks.Telemetry/
COPY src/BuildingBlocks/Norn.BuildingBlocks.Web/Norn.BuildingBlocks.Web.csproj src/BuildingBlocks/Norn.BuildingBlocks.Web/
RUN dotnet restore src/Platform/Norn.API/Norn.API.csproj --locked-mode -r linux-x64

COPY src/Platform/Norn.API/ src/Platform/Norn.API/
COPY src/Platform/Norn.Contracts/ src/Platform/Norn.Contracts/
COPY src/Platform/Norn.Knowledge/ src/Platform/Norn.Knowledge/
COPY src/BuildingBlocks/Norn.BuildingBlocks.Telemetry/ src/BuildingBlocks/Norn.BuildingBlocks.Telemetry/
COPY src/BuildingBlocks/Norn.BuildingBlocks.Web/ src/BuildingBlocks/Norn.BuildingBlocks.Web/

RUN dotnet publish src/Platform/Norn.API/Norn.API.csproj \
    -c Release -r linux-x64 --self-contained true --no-restore -o /app

FROM mcr.microsoft.com/dotnet/runtime-deps:10.0 AS final
WORKDIR /app
RUN useradd --uid 5678 --create-home nornapp
COPY --from=build /app .
COPY --from=frontend-build /web/dist ./wwwroot
USER nornapp
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080
ENTRYPOINT ["./Norn.API"]
