ARG DOTNET_SDK_IMAGE=mcr.microsoft.com/dotnet/sdk:10.0.300
ARG DOTNET_RUNTIME_IMAGE=mcr.microsoft.com/dotnet/runtime:10.0.8
FROM ${DOTNET_SDK_IMAGE} AS build
WORKDIR /src
COPY Directory.Build.props global.json WhaleDeck.slnx ./
COPY src/WhaleDeck.Domain/WhaleDeck.Domain.csproj src/WhaleDeck.Domain/
COPY src/WhaleDeck.Application/WhaleDeck.Application.csproj src/WhaleDeck.Application/
COPY src/WhaleDeck.Contracts/WhaleDeck.Contracts.csproj src/WhaleDeck.Contracts/
COPY src/WhaleDeck.Infrastructure/WhaleDeck.Infrastructure.csproj src/WhaleDeck.Infrastructure/
COPY src/WhaleDeck.Migrator/WhaleDeck.Migrator.csproj src/WhaleDeck.Migrator/
RUN dotnet restore src/WhaleDeck.Migrator/WhaleDeck.Migrator.csproj
COPY src/ src/
RUN dotnet publish src/WhaleDeck.Migrator/WhaleDeck.Migrator.csproj -c Release -o /app --no-restore

FROM ${DOTNET_RUNTIME_IMAGE} AS final
WORKDIR /app
COPY --from=build /app .
RUN apt-get update \
    && apt-get install -y --no-install-recommends libgssapi-krb5-2 \
    && rm -rf /var/lib/apt/lists/*
USER $APP_UID
ENTRYPOINT ["dotnet", "WhaleDeck.Migrator.dll"]
