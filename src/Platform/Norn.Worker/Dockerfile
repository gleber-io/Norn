# JIT — sem Native AOT (ADR-01). Publicação self-contained sobre runtime-deps, mesmo padrão do
# Dockerfile do Shop (src/Shop/Norn.Shop.Catalog.API/Dockerfile).
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY Directory.Build.props Directory.Packages.props nuget.config global.json .editorconfig ./
COPY src/Platform/Norn.Worker/Norn.Worker.csproj src/Platform/Norn.Worker/
COPY src/Platform/Norn.Contracts/Norn.Contracts.csproj src/Platform/Norn.Contracts/
COPY src/Platform/Norn.Knowledge/Norn.Knowledge.csproj src/Platform/Norn.Knowledge/
COPY src/Platform/Norn.Monitor/Norn.Monitor.csproj src/Platform/Norn.Monitor/
COPY src/Platform/Norn.Analyzer/Norn.Analyzer.csproj src/Platform/Norn.Analyzer/
COPY src/Platform/Norn.Planner/Norn.Planner.csproj src/Platform/Norn.Planner/
COPY src/Platform/Norn.Executor/Norn.Executor.csproj src/Platform/Norn.Executor/
COPY src/BuildingBlocks/Norn.BuildingBlocks.Telemetry/Norn.BuildingBlocks.Telemetry.csproj src/BuildingBlocks/Norn.BuildingBlocks.Telemetry/
COPY src/BuildingBlocks/Norn.BuildingBlocks.Web/Norn.BuildingBlocks.Web.csproj src/BuildingBlocks/Norn.BuildingBlocks.Web/
RUN dotnet restore src/Platform/Norn.Worker/Norn.Worker.csproj --locked-mode -r linux-x64

COPY src/Platform/Norn.Worker/ src/Platform/Norn.Worker/
COPY src/Platform/Norn.Contracts/ src/Platform/Norn.Contracts/
COPY src/Platform/Norn.Knowledge/ src/Platform/Norn.Knowledge/
COPY src/Platform/Norn.Monitor/ src/Platform/Norn.Monitor/
COPY src/Platform/Norn.Analyzer/ src/Platform/Norn.Analyzer/
COPY src/Platform/Norn.Planner/ src/Platform/Norn.Planner/
COPY src/Platform/Norn.Executor/ src/Platform/Norn.Executor/
COPY src/BuildingBlocks/Norn.BuildingBlocks.Telemetry/ src/BuildingBlocks/Norn.BuildingBlocks.Telemetry/
COPY src/BuildingBlocks/Norn.BuildingBlocks.Web/ src/BuildingBlocks/Norn.BuildingBlocks.Web/

RUN dotnet publish src/Platform/Norn.Worker/Norn.Worker.csproj \
    -c Release -r linux-x64 --self-contained true --no-restore -o /app

FROM mcr.microsoft.com/dotnet/runtime-deps:10.0 AS final
WORKDIR /app
RUN useradd --uid 5678 --create-home nornapp
COPY --from=build /app .
USER nornapp
ENTRYPOINT ["./Norn.Worker"]
