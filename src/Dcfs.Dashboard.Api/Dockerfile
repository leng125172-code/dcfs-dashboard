ARG DOTNET_SDK_IMAGE=mcr.microsoft.com/dotnet/sdk:10.0.300
ARG ASPNET_IMAGE=mcr.microsoft.com/dotnet/aspnet:10.0.8

FROM ${DOTNET_SDK_IMAGE} AS build
WORKDIR /src

COPY ["Directory.Build.props", "."]
COPY ["global.json", "."]
COPY ["src/Dcfs.Dashboard.Domain/Dcfs.Dashboard.Domain.csproj", "src/Dcfs.Dashboard.Domain/"]
COPY ["src/Dcfs.Dashboard.Application/Dcfs.Dashboard.Application.csproj", "src/Dcfs.Dashboard.Application/"]
COPY ["src/Dcfs.Dashboard.Infrastructure/Dcfs.Dashboard.Infrastructure.csproj", "src/Dcfs.Dashboard.Infrastructure/"]
COPY ["src/Dcfs.Dashboard.Api/Dcfs.Dashboard.Api.csproj", "src/Dcfs.Dashboard.Api/"]
RUN dotnet restore "src/Dcfs.Dashboard.Api/Dcfs.Dashboard.Api.csproj"

COPY src/ src/
RUN dotnet publish "src/Dcfs.Dashboard.Api/Dcfs.Dashboard.Api.csproj" \
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
ENTRYPOINT ["dotnet", "Dcfs.Dashboard.Api.dll"]
