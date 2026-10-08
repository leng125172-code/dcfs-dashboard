ARG DOTNET_SDK_IMAGE=mcr.microsoft.com/dotnet/sdk:10.0.300
ARG ASPNET_IMAGE=mcr.microsoft.com/dotnet/aspnet:10.0.8

FROM ${DOTNET_SDK_IMAGE} AS build
WORKDIR /src

COPY ["Directory.Build.props", "."]
COPY ["global.json", "."]
COPY ["src/WhaleDeck.Domain/WhaleDeck.Domain.csproj", "src/WhaleDeck.Domain/"]
COPY ["src/WhaleDeck.Application/WhaleDeck.Application.csproj", "src/WhaleDeck.Application/"]
COPY ["src/WhaleDeck.Infrastructure/WhaleDeck.Infrastructure.csproj", "src/WhaleDeck.Infrastructure/"]
COPY ["src/WhaleDeck.Api/WhaleDeck.Api.csproj", "src/WhaleDeck.Api/"]
RUN dotnet restore "src/WhaleDeck.Api/WhaleDeck.Api.csproj"

COPY src/ src/
RUN dotnet publish "src/WhaleDeck.Api/WhaleDeck.Api.csproj" \
    --configuration Release \
    --no-restore \
    --output /app/publish \
    /p:UseAppHost=false

FROM ${ASPNET_IMAGE} AS final
WORKDIR /app
ENV ASPNETCORE_URLS=http://+:8080 \
    DOTNET_EnableDiagnostics=0
EXPOSE 8080
COPY --from=build /app/publish .
USER $APP_UID
ENTRYPOINT ["dotnet", "WhaleDeck.Api.dll"]
