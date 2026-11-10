# syntax=docker/dockerfile:1

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Project files first, so the restore layer is reused until a dependency changes.
COPY global.json Directory.Build.props Directory.Packages.props ./
COPY src/StockAlerts.Core/StockAlerts.Core.csproj src/StockAlerts.Core/
COPY src/StockAlerts.Infrastructure/StockAlerts.Infrastructure.csproj src/StockAlerts.Infrastructure/
COPY src/StockAlerts.Api/StockAlerts.Api.csproj src/StockAlerts.Api/
RUN dotnet restore src/StockAlerts.Api/StockAlerts.Api.csproj

COPY src/ src/
RUN dotnet publish src/StockAlerts.Api/StockAlerts.Api.csproj \
    --configuration Release \
    --no-restore \
    --output /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS final
WORKDIR /app
COPY --from=build /app/publish .

# The base image listens on 8080 and ships a non-root user; run as that user.
USER $APP_UID
EXPOSE 8080
ENTRYPOINT ["dotnet", "StockAlerts.Api.dll"]
