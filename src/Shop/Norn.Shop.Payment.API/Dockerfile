# JIT — sem Native AOT (ADR-01). Publicação self-contained sobre runtime-deps: a imagem final
# carrega o próprio runtime, então não precisa da imagem aspnet/runtime completa.
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY Directory.Build.props Directory.Packages.props nuget.config global.json .editorconfig ./
COPY src/Shop/Norn.Shop.Payment.API/Norn.Shop.Payment.API.csproj src/Shop/Norn.Shop.Payment.API/
COPY src/Shop/Norn.Shop.Contracts/Norn.Shop.Contracts.csproj src/Shop/Norn.Shop.Contracts/
COPY src/BuildingBlocks/Norn.BuildingBlocks.Telemetry/Norn.BuildingBlocks.Telemetry.csproj src/BuildingBlocks/Norn.BuildingBlocks.Telemetry/
COPY src/BuildingBlocks/Norn.BuildingBlocks.Web/Norn.BuildingBlocks.Web.csproj src/BuildingBlocks/Norn.BuildingBlocks.Web/
COPY src/BuildingBlocks/Norn.BuildingBlocks.Messaging/Norn.BuildingBlocks.Messaging.csproj src/BuildingBlocks/Norn.BuildingBlocks.Messaging/
RUN dotnet restore src/Shop/Norn.Shop.Payment.API/Norn.Shop.Payment.API.csproj --locked-mode -r linux-x64

COPY src/Shop/Norn.Shop.Payment.API/ src/Shop/Norn.Shop.Payment.API/
COPY src/Shop/Norn.Shop.Contracts/ src/Shop/Norn.Shop.Contracts/
COPY src/BuildingBlocks/Norn.BuildingBlocks.Telemetry/ src/BuildingBlocks/Norn.BuildingBlocks.Telemetry/
COPY src/BuildingBlocks/Norn.BuildingBlocks.Web/ src/BuildingBlocks/Norn.BuildingBlocks.Web/
COPY src/BuildingBlocks/Norn.BuildingBlocks.Messaging/ src/BuildingBlocks/Norn.BuildingBlocks.Messaging/

RUN dotnet publish src/Shop/Norn.Shop.Payment.API/Norn.Shop.Payment.API.csproj \
    -c Release -r linux-x64 --self-contained true --no-restore -o /app

FROM mcr.microsoft.com/dotnet/runtime-deps:10.0 AS final
WORKDIR /app
RUN useradd --uid 5678 --create-home nornapp
COPY --from=build /app .
USER nornapp
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080
ENTRYPOINT ["./Norn.Shop.Payment.API"]
