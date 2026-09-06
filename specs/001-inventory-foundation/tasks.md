# Tareas: Inventory Foundation

**Feature branch**: `001-inventory-foundation`
**Fuente**: desglose SDD original (reformateado a Spec Kit, sin pérdida)
**Referencias**: [`spec.md`](./spec.md) · [`plan.md`](./plan.md) · [`data-model.md`](./data-model.md) · [`contracts/`](./contracts/)

**Convención de IDs**: `T001` … `T050`. `[P]` = puede ejecutarse en paralelo con las otras tareas `[P]` de su misma fase (sin dependencia de archivo ni de orden).

---

## Pronóstico de Carga de Revisión

| Campo | Valor |
|-------|-------|
| Líneas cambiadas estimadas | ~8.700 (backend ~4.700, tests ~1.600, frontend ~2.000, ci/ops/contracts ~400) |
| Riesgo del presupuesto de 400 líneas | **High** |
| PRs encadenados recomendados | **Yes** |
| División sugerida | PR1 → PR2 → PR3 → PR4 → PR5 → PR6 → PR7 |
| Estrategia de entrega | `ask-on-risk` |
| Estrategia de encadenado | `pending` |

```
Decisión requerida antes de apply: Yes
PRs encadenados recomendados: Yes
Estrategia de encadenado: pending
Riesgo del presupuesto de 400 líneas: High
```

> **Guard**: con `delivery_strategy = ask-on-risk` y `Riesgo del presupuesto de 400 líneas = High`, el orquestador DEBE detenerse antes de `apply` y preguntar: PRs encadenados/stacked vs. `size:exception` aprobado por el maintainer.

## Unidades de trabajo → PR slices

| Unit | Objetivo | PR | Tareas | Depende de |
|------|----------|----|--------|------------|
| 1 | Scaffolding monorepo + CI + docker-compose | **PR 1** | T001–T007 | — (base) |
| 2 | Tenant isolation (Domain + Infra + RLS) | **PR 2** | T008–T014 | PR 1 |
| 3 | Identity + JWT + dispositivos confiables + 2FA por SMS | **PR 3** | T015–T022, T049, T050 | PR 2 |
| 4 | Product catalog (Domain + App + Api) | **PR 4** | T023–T030 | PR 2 |
| 5 | Batch inventory (Domain + App + Api) | **PR 5** | T031–T038 | PR 4 |
| 6 | Frontend auth + inventory + branding por tenant | **PR 6** | T039–T043, T051–T060 | PR 3, PR 4, PR 5 |
| 7 | Contracts (orval) + PWA + e2e mínimo | **PR 7** | T044–T048 | PR 6 |

### Grafo de dependencias entre PRs

```
                 PR1  (scaffolding + CI)
                  │
                 PR2  (tenant isolation)
                  ├──────────────┐
                 PR3            PR4  (products)
              (identity)         │
                  │             PR5  (batches)
                  │              │
                  └──────┬───────┘
                         │
                        PR6  (frontend auth + inventory)
                         │
                        PR7  (contracts + PWA + e2e)
```

Relaciones explícitas: **PR2 → PR1**; **PR3 → PR2**; **PR4 → PR2**; **PR5 → PR4**; **PR6 → PR3 + PR4 + PR5**; **PR7 → PR6**.

PR3 y PR4 son las únicas ramas paralelizables del encadenado (ambas parten de PR2). Cada PR DEBE declarar en su descripción: punto de inicio, punto de fin, dependencias, follow-up y qué queda fuera de alcance, con el diagrama de arriba marcando el PR actual con 📍.

---

## Fase 1 — Scaffolding + CI (PR 1)

Sin dependencias. T001–T006 son paralelizables entre sí (directorios y archivos disjuntos).

> **Estado: completada.** El scaffolding del monorepo ya está implementado y verificado.

- [x] **T001** `[P]` Crear `backend/src/{Domain,Application,Infrastructure,Api,Worker}`
- [x] **T002** `[P]` Crear `backend/tests/{Domain,Application,Infrastructure,Api,Architecture}.Tests`
- [x] **T003** `[P]` Scaffolding `frontend/web` (Vite + React 19 + TS + Material UI + CSS global)
- [x] **T004** `[P]` Crear `packages/contracts/` (placeholder)
- [x] **T005** `[P]` Crear `ops/docker-compose.yml` (`postgres:16-alpine` + healthcheck `pg_isready`)
- [x] **T006** `[P]` Crear `.github/workflows/ci.yml` (`dotnet build`/`test` + frontend `build`/`lint`)
- [x] **T007** `appsettings.json` base (`ConnectionStrings`, `Jwt`, `Sms { Provider, ApiKey, Sender }`, `Tenant:ExpiryThresholds`) — depende de T001

## Fase 2 — Tenant Isolation · spec `tenant-isolation` (PR 2, depende de PR 1)

Cubre FR-001 … FR-004, NFR-001 … NFR-003.

- [x] **T008** `[P]` `Domain/Common/ITenantEntity.cs` — contrato tenant (FR-002)
- [x] **T009** `[P]` `Api/Middleware/TenantMiddleware.cs` — header `X-Tenant-ID` → `400` si falta/inválido; `TenantContext` (FR-001, NFR-002)
- [x] **T010** `Infrastructure/Persistence/StockmaDbContext.cs` + `ApplyTenantFilters()` reflectivo (FR-002) — depende de T008
- [x] **T011** Migración `InitialSchema` + políticas RLS + rol `app_user` no-propietario (FR-003, NFR-003) — depende de T010
- [x] **T012** `AuditSaveChangesInterceptor` — rechaza el cambio de `TenantId` (FR-004) — depende de T010
- [x] **T013** `[P]` `Architecture.Tests/TenantArchitectureTests.cs` — el build falla si una entidad tenant no implementa `ITenantEntity` (FR-002)
- [x] **T014** `Api.IntegrationTests/TenantIsolationTests.cs` — tenant A no ve datos de B (Testcontainers) (NFR-001) — depende de T011

## Fase 3 — Identity + JWT · spec `identity-access` (PR 3, depende de PR 2)

Cubre FR-005 … FR-008, NFR-004, NFR-005. Contrato: [`contracts/auth-api.md`](./contracts/auth-api.md).

- [ ] **T015** Domain: `ApplicationUser`, `TrustedDevice`, `DeviceOtp` (FR-005, FR-007, FR-008)
- [ ] **T016** `[P]` `Infrastructure/Identity/JwtTokenService.cs` — claims `sub`, `tid`, `exp` ≤ 60 min (FR-006, NFR-004)
- [ ] **T017** Application: `RegisterUserCommand`, `LoginCommand`, `ConfirmDeviceCommand` — depende de T015
- [ ] **T018** `[P]` `ISmsSender` / `SmsOtpSender` (sender de consola en dev) + expiración de OTP a 10 min (FR-008, NFR-004)
      El OTP viaja por **SMS** al `ApplicationUser.PhoneNumber`, nunca por email. La ventana ≤ 10 min se mantiene.
- [ ] **T019** `Api/Controllers/AuthController` — `register` / `login` / `confirm-device` + rate limiting 5/IP/min (NFR-005) — depende de T017
- [ ] **T020** Marcado condicional de `TrustedDevice` en `confirm-device` bajo `MaxTrustedDevices` (FR-007) — depende de T017
      El JWT se emite **siempre**; el `TrustedDevice` se crea **sólo si** el conteo de activos (`RevokedAt IS NULL`) es `< MaxTrustedDevices`. Sin slot: `200` con `deviceTrusted: false`, sin crear la fila y **sin revocar a nadie**. `RevokedAt` sólo cambia por acción manual de un admin. Ver [`plan.md`](./plan.md#5-autenticación) y [`contracts/auth-api.md`](./contracts/auth-api.md).
- [ ] **T021** `[P]` Unit tests: login desde dispositivo conocido/desconocido, OTP expirado
- [ ] **T022** API tests (`WebApplicationFactory`): `401` credenciales inválidas, `409` email duplicado, `requiresDeviceConfirmation` — depende de T019
- [ ] **T049** Integración con proveedor de SMS: implementación concreta de `ISmsSender`, config `Sms { Provider, ApiKey, Sender }`, reintento/fallo del envío y sender de consola para dev (FR-008) — depende de T018
      `[PENDIENTE: proveedor de SMS no elegido]`
- [ ] **T050** `PUT /api/admin/users/{userId}/phone-number` (sólo admin del tenant) **y cierre de la superficie self-service de Identity** sobre `PhoneNumber` (FR-008) — depende de T019
      El usuario NO DEBE poder registrar ni cambiar su propio `PhoneNumber`. Identity lo expone por defecto (`UserManager.SetPhoneNumberAsync`, `ChangePhoneNumberAsync`, `GenerateChangePhoneNumberTokenAsync` y los endpoints self-service del Identity UI/API): hay que cerrar esa superficie explícitamente, no alcanza con no usarla. Ver [`contracts/auth-api.md`](./contracts/auth-api.md) y [`data-model.md`](./data-model.md).
      `[PENDIENTE: propuesto, no está en las fuentes originales]`

## Fase 4 — Product Catalog · spec `product-catalog` (PR 4, depende de PR 2)

Cubre FR-009 … FR-011, NFR-006 … NFR-008. Contrato: [`contracts/products-api.md`](./contracts/products-api.md).

- [x] **T023** `Domain/Products/Product.cs` — `Sku` inmutable, `Barcode?`, `Category` (FR-009, FR-010)
- [x] **T024** `IProductRepository` — `Add`, `Update`, `GetByBarcode`, `Search` — depende de T023
- [x] **T025** `RegisterProductCommand` (`SKU-{n}` automático si no hay barcode) + `UpdateProductCommand` (rechaza cambio de SKU con `400`) (FR-009, FR-010) — depende de T024
- [x] **T026** `SearchProductsQuery` (trigram / `ILIKE`) + `GetProductByBarcodeQuery` (`404`) (FR-011, NFR-006) — depende de T024
- [x] **T027** `Api/Controllers/ProductsController` — depende de T025, T026
- [x] **T028** `[P]` Migración: unique `(TenantId, Sku)` y `(TenantId, Barcode)` (NFR-007)
- [x] **T029** `[P]` Unit tests: SKU automático, SKU inmutable, SKU duplicado `409`
- [x] **T030** API tests: CRUD + búsqueda por nombre / barcode — depende de T027

## Fase 5 — Batch Inventory · spec `batch-inventory` (PR 5, depende de PR 4)

Cubre FR-012 … FR-015, NFR-009, NFR-010. Contrato: [`contracts/batches-api.md`](./contracts/batches-api.md).

- [x] **T031** `Domain/Batches/Batch.cs` (`xmin`) + `BatchStatusCalculator` (semáforo) (FR-012, FR-014, FR-015)
- [x] **T032** `IBatchRepository` — `Adjust` con reintento `xmin` ×1 (FR-014) — depende de T031
- [x] **T033** `RegisterBatchCommand` + `AdjustBatchStockCommand` — rechazo de stock negativo con `422` / `BATCH_NEGATIVE_STOCK` y **rechazo total** del ajuste, nunca parcial (FR-012, FR-013) — depende de T032
- [x] **T034** `GetBatchesQuery` — `SemaphoreColor` computado con umbrales por tenant, no persistido (FR-015, NFR-010) — depende de T031
- [x] **T035** `Api/Controllers/BatchesController` — depende de T033, T034
- [x] **T036** `[P]` Migración: índice `(ProductId, ExpirationDate)` — soporte para FEFO futuro (NFR-009)
- [x] **T037** `[P]` Unit tests: stock negativo, semáforo default/custom, conflicto de concurrencia `409`
- [x] **T038** API tests: registrar y ajustar lote — depende de T035

## Fase 6 — Frontend auth + inventory (PR 6, depende de PR 3, PR 4, PR 5)

- [ ] **T039** `[P]` `features/auth`: `LoginPage`, `RegisterPage`, `DeviceOtpForm`, `auth-store` (Zustand)
- [ ] **T040** `[P]` `features/inventory`: `ProductListPage`, `ProductForm`, `BatchList`, `BarcodeScanner` (`@zxing/browser`)
- [ ] **T041** `shared/query-client` — keys de TanStack por tenant (`['tenant', tenantId, ...]`), el logout resetea la cache
- [x] **T042** Sistema de diseño: `styles/tokens.css` (única fuente de verdad), `styles/global-styles.tsx` (botones y textos reutilizables), `styles/mui-bridge.ts` (lee los tokens resueltos y arma el theme de MUI), `styles/branding.ts` (3 colores por tenant + contraste derivado). Tipografía única `Work Sans`
- [x] **T051** Persistir el branding del tenant: VO `TenantBranding` (`Primary`, `PrimaryActive`, `PrimaryBg`) en `TenantSettings` vía `OwnsOne` opcional + migración `AddTenantBranding`. Sin branding configurado se guarda `NULL`: los defaults viven en `tokens.css` y no se duplican en el backend
- [x] **T052** `GET /api/tenant/branding` — lo consume el front al arrancar. Responde `200` con cuerpo `null` cuando no hay branding (no `404`: la ausencia es un estado válido) — depende de T051
- [ ] **T053** `PUT /api/admin/tenant/branding` (sólo admin del tenant) — **depende de PR 3**: hoy la API no tiene autenticación configurada, así que un endpoint de escritura "sólo admin" sería un endpoint abierto
- [ ] **T054** Frontend: pedir `GET /api/tenant/branding` al arrancar y pasarle el resultado a `applyTenantBranding` antes de construir el theme — depende de T041, T052

### Entrada a la aplicación — login genérico y tenant en el path

Decidido: un solo origen, login genérico con logo de Stockma en `stockma.app/login`,
y el tenant en el path una vez autenticado (`stockma.app/{slug}/inventario`). No se
cambia de origen después del login: el JWT vive en `localStorage`, que es por origen,
y el usuario aterrizaría deslogueado.

- [ ] **T055** ⚠️ **BLOQUEANTE — decisión de usuario requerida.** El login genérico y el
  email único **por tenant** son incompatibles: `POST /api/auth/login` exige
  `X-Tenant-ID`, y sin tenant en la URL el frontend no lo tiene. Con el mismo email
  existiendo en varios tenants (`AUTH_EMAIL_DUPLICATE` hoy es por tenant), el email
  solo no alcanza para resolver a cuál pertenece. Salidas: (a) email **único global**
  en la plataforma y `/api/auth/login` exento del `TenantMiddleware`, resolviendo el
  tenant desde el email; (b) mantener email por tenant y volver a un discriminador en
  la URL o en la pantalla. **Nada de PR 3 se puede cerrar sin esto**
- [ ] **T056** `Tenant.Slug` (único en la plataforma) y `Tenant.Name` + migración. Hoy la
  entidad tiene SÓLO `Id` y `Settings`: sin esto no hay path por tenant ni forma de
  mostrar el nombre del cliente
- [ ] **T057** `POST /api/auth/login` devuelve `branding` en el cuerpo junto al
  `accessToken`. Evita el round-trip extra y el parpadeo de colores al entrar; sin esto
  el usuario ve el azul de Stockma y salta a su paleta — depende de T019, T051, T056
- [ ] **T058** Routing del frontend con el tenant en el path (`/{slug}/...`) tras el login
  — depende de T056
- [ ] **T059** `OnboardingCompletedAt` en `TenantSettings` (separado de `Branding`:
  completar el onboarding y quedarse con la paleta por defecto es válido) + wizard
  disparado por **primer login de un admin del tenant**, no por primer login a secas —
  depende de PR 3
- [ ] **T060** Logo del tenant — capacidad nueva, NO es un color más: almacenamiento de
  archivos, formatos y tamaño permitidos, servido y caché, y saneamiento de SVG subido.
  Sin decidir; el login genérico usa el logo de Stockma y no lo necesita
- [ ] **T043** Tests de componentes de `auth` e `inventory` — depende de T039, T040

## Fase 7 — Contracts + PWA (PR 7, depende de PR 6)

- [ ] **T044** `packages/contracts/openapi.json` real generado desde Api
- [ ] **T045** Configuración de orval → `api-client` TS generado — depende de T044
- [ ] **T046** `[P]` PWA manifest + `dotnet dev-certs` HTTPS documentado en el README
- [ ] **T047** CI: step de generación / verificación de contracts — depende de T045
- [ ] **T048** `[P]` Playwright e2e mínimo (scanner de barcode, manual/smoke)

---

## Trazabilidad requerimiento → tareas

| Requerimiento | Tareas | PR |
|---|---|---|
| FR-001 | T009 | PR 2 |
| FR-002 | T008, T010, T013 | PR 2 |
| FR-003 | T011 | PR 2 |
| FR-004 | T012 | PR 2 |
| FR-005 | T015, T017, T019, T022 | PR 3 |
| FR-006 | T016, T017, T019, T021 | PR 3 |
| FR-007 | T015, T020 | PR 3 |
| FR-008 | T015, T018, T019, T021, T049, T050 | PR 3 |
| FR-009 | T023, T025, T029 | PR 4 |
| FR-010 | T023, T025, T029 | PR 4 |
| FR-011 | T026, T027, T030 | PR 4 |
| FR-012 | T031, T033, T038 | PR 5 |
| FR-013 | T033, T037 | PR 5 |
| FR-014 | T031, T032, T037 | PR 5 |
| FR-015 | T031, T034, T037 | PR 5 |
| NFR-001 | T014 | PR 2 |
| NFR-002 | T009 | PR 2 |
| NFR-003 | T011 | PR 2 |
| NFR-004 | T016, T018, T049 | PR 3 |
| NFR-005 | T019 | PR 3 |
| NFR-006 | T026 | PR 4 |
| NFR-007 | T028 | PR 4 |
| NFR-008 | T023 — `Currency` del producto sobre el COP por defecto del tenant | PR 4 |
| NFR-009 | T036 | PR 5 |
| NFR-010 | T034 | PR 5 |

## Notas de ejecución

- **Commits por unidad de comportamiento**, nunca por tipo de archivo: los tests viajan con el comportamiento que verifican y la documentación con el cambio visible al usuario.
- Ningún PR DEBERÍA superar las 400 líneas cambiadas; si una fase excede el presupuesto, dividirla en slices encadenados dentro del mismo PR chain antes de implementar.
- Cada PR debe ser revisable en ≤ 60 minutos.
- `strict_tdd: false` según `.specify/config.yaml` — modo estándar; los tests de cada fase DEBE entrar en el mismo PR que su comportamiento.
