ARG DOTNET_SDK_IMAGE=mcr.microsoft.com/dotnet/sdk:10.0.300
ARG DOTNET_RUNTIME_IMAGE=mcr.microsoft.com/dotnet/runtime:10.0.8

FROM ${DOTNET_SDK_IMAGE} AS build
WORKDIR /src

COPY ["Directory.Build.props", "."]
COPY ["global.json", "."]
COPY ["src/WhaleDeck.Domain/WhaleDeck.Domain.csproj", "src/WhaleDeck.Domain/"]
COPY ["src/WhaleDeck.Application/WhaleDeck.Application.csproj", "src/WhaleDeck.Application/"]
COPY ["src/WhaleDeck.Infrastructure/WhaleDeck.Infrastructure.csproj", "src/WhaleDeck.Infrastructure/"]
COPY ["src/WhaleDeck.Worker/WhaleDeck.Worker.csproj", "src/WhaleDeck.Worker/"]
RUN dotnet restore "src/WhaleDeck.Worker/WhaleDeck.Worker.csproj"

COPY src/ src/
RUN dotnet publish "src/WhaleDeck.Worker/WhaleDeck.Worker.csproj" \
    --configuration Release \
    --no-restore \
    --output /app/publish \
    /p:UseAppHost=false

FROM ${DOTNET_RUNTIME_IMAGE} AS final
WORKDIR /app
ENV DOTNET_EnableDiagnostics=0
COPY --from=build /app/publish .
USER $APP_UID
ENTRYPOINT ["dotnet", "WhaleDeck.Worker.dll"]
