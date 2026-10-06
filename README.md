# Stockma

SaaS B2B multi-tenant de gestión de inventarios con control de lotes y vencimientos (FEFO), para farmacias, tiendas y pequeños emprendedores.

Monorepo: **.NET 9** (Clean Architecture + CQRS), **React 19 + Vite** y **PostgreSQL 16**, con aislamiento entre tenants en dos capas: filtro global de EF Core y Row-Level Security.

## Requisitos

**Docker** · **.NET 9 SDK** · **Node 22**

## Comandos

```bash
# Base de datos → localhost:5434
docker compose -f ops/docker-compose.yml up -d

# Migraciones (desde backend/) — no se aplican solas al arrancar la API
dotnet ef database update --project src/Stockma.Infrastructure --startup-project src/Stockma.Infrastructure

# Contraseña del rol de runtime (una vez por base)
docker exec stockma-postgres psql -U stockma -d stockma -c "ALTER ROLE app_user PASSWORD 'app_user';"

# API (desde backend/) → http://localhost:5265
dotnet run --project src/Stockma.Api

# Frontend → http://localhost:5173
cd frontend/web && npm ci && npm run dev

# Tests de backend (requiere Docker: usan Testcontainers)
cd backend && dotnet test Stockma.sln

# Tests de frontend
cd frontend/web && npm run test

# Git hooks — una vez por clon; formatea con Prettier antes de commitear
git config core.hooksPath .githooks

# Ambiente demo (desde backend/, idempotente) — nunca correr contra un tenant productivo
dotnet run --project src/Stockma.Api -- bootstrap-demo
```

> La API corre con `app_user`; el rol propietario sólo recibe `dotnet ef`. Si la API se conecta como dueño, la RLS deja de aislar tenants.

## Ambiente demo

| Campo | Valor |
| --- | --- |
| `X-Tenant-ID` | `00000000-0000-0000-0000-000000000001` |
| Email | `demo@stockma.app` |
| Contraseña | `Demo#Stockma2026` |
| Rol | `Member` |

## Estructura

| Ruta                                 | Qué hay                                                          |
| ------------------------------------ | ---------------------------------------------------------------- |
| `backend/src/Stockma.Domain`         | Entidades, value objects, servicios de dominio                   |
| `backend/src/Stockma.Application`    | Casos de uso (CQRS/MediatR), puertos                             |
| `backend/src/Stockma.Infrastructure` | EF Core, repositorios, migraciones, RLS                          |
| `backend/src/Stockma.Api`            | Controladores, middleware, composición                           |
| `backend/src/Stockma.Worker`         | Jobs en background                                               |
| `backend/tests/`                     | Domain, Application, Infrastructure, Api, Architecture           |
| `frontend/web`                       | React 19 + Vite + MUI. Ver su [README](./frontend/web/README.md) |
| `packages/contracts`                 | OpenAPI + cliente TS generado con orval (Fase 7)                 |
| `ops/`                               | `docker-compose.yml`                                             |
| `specs/001-inventory-foundation`     | Spec, plan, data model, contratos y tareas                       |
