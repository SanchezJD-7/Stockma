# ── Etapa 1: build ────────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

COPY backend/Directory.Build.props backend/
COPY backend/src/Stockma.Api/Stockma.Api.csproj backend/src/Stockma.Api/
COPY backend/src/Stockma.Application/Stockma.Application.csproj backend/src/Stockma.Application/
COPY backend/src/Stockma.Domain/Stockma.Domain.csproj backend/src/Stockma.Domain/
COPY backend/src/Stockma.Infrastructure/Stockma.Infrastructure.csproj backend/src/Stockma.Infrastructure/

RUN dotnet restore backend/src/Stockma.Api/Stockma.Api.csproj

COPY backend/src/ backend/src/

RUN dotnet publish backend/src/Stockma.Api/Stockma.Api.csproj \
    -c Release \
    -o /app/publish \
    /p:UseAppHost=false \
    --no-restore

# ── Etapa 2: runtime ─────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime
WORKDIR /app

COPY --from=build /app/publish .

EXPOSE 10000
USER app

ENTRYPOINT ["sh", "-c", "exec dotnet Stockma.Api.dll --urls http://0.0.0.0:${PORT:-10000}"]
