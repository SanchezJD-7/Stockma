# Stockma

SaaS B2B multi-tenant de gestión de inventarios con control de lotes y
vencimientos (FEFO), para farmacias, tiendas y pequeños emprendedores.

Monorepo: **.NET 9** (Clean Architecture + CQRS) y **React 19 + Vite**, sobre
**PostgreSQL 16**. El aislamiento entre tenants tiene dos capas independientes:
filtro global de EF Core y Row-Level Security de PostgreSQL.

## Levantar el ambiente

Requisitos: **Docker**, **.NET 9 SDK**, **Node 22**.

```bash
# 1. Base de datos
docker compose -f ops/docker-compose.yml up -d

# 2. Migraciones — NO se aplican solas al arrancar la API. Corren con el rol
#    propietario (stockma); por defecto usan la cadena de docker-compose.
cd backend
dotnet ef database update \
  --project src/Stockma.Infrastructure \
  --startup-project src/Stockma.Infrastructure

# 3. Contraseña del rol de runtime (una vez por base). La migración crea
#    app_user sin contraseña; la API se conecta con él, nunca con stockma.
docker exec stockma-postgres psql -U stockma -d stockma \
  -c "ALTER ROLE app_user PASSWORD 'app_user';"

# 4. API  →  http://localhost:5265
dotnet run --project src/Stockma.Api

# 5. Frontend (en otra terminal)  →  http://localhost:5173
cd frontend/web
npm ci
npm run dev
```

El `--startup-project` es `Stockma.Infrastructure`, **no** `Stockma.Api`: la API
no referencia `Microsoft.EntityFrameworkCore.Design`. Las herramientas de EF usan
`Persistence/DesignTime/StockmaDbContextFactory.cs`.

**Dos connection strings, dos roles** (ADR-017):

| Clave | Rol | Quién la usa |
| ----- | --- | ------------ |
| `ConnectionStrings:Postgres` (`appsettings.Development.json` en local, env `ConnectionStrings__Postgres` en cualquier otro entorno) | `app_user`, no propietario | La API en runtime y el subcomando `bootstrap-admin` |
| env `ConnectionStrings__PostgresMigrations` | propietario de las tablas | Sólo `dotnet ef` (por defecto `stockma`/`stockma` en `localhost`) |

`appsettings.json` **no trae** connection string: la de desarrollo vive en
`appsettings.Development.json`. Fuera de `Development`, sin `ConnectionStrings__Postgres`
la API (y `bootstrap-admin`) corta al arrancar con un mensaje que nombra la clave.
`ConnectionStrings__PostgresMigrations` **nunca** se define en el entorno de la API: es
la credencial del propietario y sólo la recibe el paso de migración.

Si la API se conecta con el propietario, la RLS deja de aislar tenants. Fuera de
`Development` —en cualquier otro entorno, `Testing` incluido— la API y `bootstrap-admin`
**no arrancan** si su rol es superusuario, tiene `BYPASSRLS`, es propietario de tablas o
es miembro (con o sin `INHERIT`) de un rol así.

**Requisitos del rol que migra** (ADR-017):

- Un superusuario migra sin más.
- Un propietario **no superusuario** necesita `CREATEROLE` (las migraciones crean
  `app_user` y `auth_lookup`) y **`ADMIN` sobre `auth_lookup`**. Quien crea el rol lo
  tiene solo; pero `auth_lookup` es de todo el cluster, así que si otra base u otro rol
  ya lo creó, la migración falla con este aviso y un superusuario tiene que correr, una
  vez:

  ```sql
  GRANT auth_lookup TO <rol_que_migra> WITH ADMIN OPTION, INHERIT FALSE, SET FALSE;
  ```

### Sembrar un tenant

La base arranca vacía y **todas** las rutas exigen el header `X-Tenant-ID` con un
tenant que exista. Todavía no hay onboarding, así que la primera fila va a mano:

```sql
INSERT INTO tenants (id) VALUES ('6f2c1b3a-8e41-4f2d-9c7a-1d5e8b0a3f77');
INSERT INTO tenant_settings
  (tenant_id, max_trusted_devices, green_months, yellow_months, next_sku_number)
VALUES ('6f2c1b3a-8e41-4f2d-9c7a-1d5e8b0a3f77', 2, 6, 3, 1);
```

Verificar que responde:

```bash
curl -H "X-Tenant-ID: 6f2c1b3a-8e41-4f2d-9c7a-1d5e8b0a3f77" \
  http://localhost:5265/api/tenant/branding
# → null   (correcto: el tenant no configuró colores)
```

### Activar los git hooks

Una vez por clon. Git no los activa solo:

```bash
git config core.hooksPath .githooks
```

Formatea con Prettier los archivos staged de `frontend/web` antes de commitear,
para que el CI no sea el primero en avisar. Ver [`.githooks/README.md`](./.githooks/README.md).

## Tests

```bash
cd backend && dotnet test Stockma.sln     # 128 tests
cd frontend/web && npm run test           # 14 tests
```

**Docker tiene que estar corriendo**: `Stockma.Infrastructure.Tests` y
`Stockma.Api.Tests` levantan PostgreSQL con Testcontainers.

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

## Estado

Slice en curso: `001-inventory-foundation`. El detalle vive en
[`specs/001-inventory-foundation/tasks.md`](./specs/001-inventory-foundation/tasks.md).

| PR  | Alcance                              | Estado                                         |
| --- | ------------------------------------ | ---------------------------------------------- |
| 1   | Scaffolding + CI + docker-compose    | ✅                                             |
| 2   | Tenant isolation (EF filter + RLS)   | ✅                                             |
| 3   | Identity + JWT + 2FA por SMS         | ⬜ desbloqueado (T055 resuelta)                |
| 4   | Product catalog                      | ✅                                             |
| 5   | Batch inventory                      | ✅                                             |
| 6   | Frontend auth + inventory + branding | 🚧 sistema de diseño y branding backend listos |
| 7   | Contracts (orval) + PWA + e2e        | ⬜                                             |
