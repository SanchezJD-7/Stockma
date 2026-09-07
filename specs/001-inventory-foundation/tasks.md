# Tareas: Inventory Foundation

**Feature branch**: `001-inventory-foundation`
**Fuente**: desglose SDD original (reformateado a Spec Kit, sin pérdida)
**Referencias**: [`spec.md`](./spec.md) · [`plan.md`](./plan.md) · [`data-model.md`](./data-model.md) · [`contracts/`](./contracts/)

**Convención de IDs**: `T001` … `T069`. `[P]` = puede ejecutarse en paralelo con las otras tareas `[P]` de su misma fase (sin dependencia de archivo ni de orden).

---

## Pronóstico de Carga de Revisión

| Campo                                | Valor                                                                         |
| ------------------------------------ | ----------------------------------------------------------------------------- |
| Líneas cambiadas estimadas           | ~8.700 (backend ~4.700, tests ~1.600, frontend ~2.000, ci/ops/contracts ~400) |
| Riesgo del presupuesto de 400 líneas | **High**                                                                      |
| PRs encadenados recomendados         | **Yes**                                                                       |
| División sugerida                    | PR1 → PR2 → PR3 → PR4 → PR5 → PR6 → PR7                                       |
| Estrategia de entrega                | `ask-on-risk`                                                                 |
| Estrategia de encadenado             | `pending`                                                                     |

```
Decisión requerida antes de apply: Yes
PRs encadenados recomendados: Yes
Estrategia de encadenado: pending
Riesgo del presupuesto de 400 líneas: High
```

> **Guard**: con `delivery_strategy = ask-on-risk` y `Riesgo del presupuesto de 400 líneas = High`, el orquestador DEBE detenerse antes de `apply` y preguntar: PRs encadenados/stacked vs. `size:exception` aprobado por el maintainer.

## Unidades de trabajo → PR slices

| Unit | Objetivo                                               | PR       | Tareas                | Depende de       |
| ---- | ------------------------------------------------------ | -------- | --------------------- | ---------------- |
| 1    | Scaffolding monorepo + CI + docker-compose             | **PR 1** | T001–T007             | — (base)         |
| 2    | Tenant isolation (Domain + Infra + RLS)                | **PR 2** | T008–T014             | PR 1             |
| 3    | Identity + JWT + dispositivos confiables + 2FA por SMS | **PR 3** | T015–T022, T049, T050, T061–T065, T067–T069 | PR 2             |
| 4    | Product catalog (Domain + App + Api)                   | **PR 4** | T023–T030             | PR 2             |
| 5    | Batch inventory (Domain + App + Api)                   | **PR 5** | T031–T038             | PR 4             |
| 6    | Frontend auth + inventory + branding por tenant        | **PR 6** | T039–T043, T051–T060  | PR 3, PR 4, PR 5 |
| 7    | Contracts (orval) + PWA + e2e mínimo                   | **PR 7** | T044–T048             | PR 6             |

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
- [ ] **T061** `PUT /api/auth/phone-number` — cambio del propio `PhoneNumber` **autenticado y con OTP al número ACTUAL** (FR-008) — depende de T018, T019, T050
      Resuelve la fricción operativa de T050: sin esto, el admin del tenant que quiere cambiar su propio número tiene que escribirle al admin de plataforma.
      Distinción que sostiene la regla: **enrolar** el primer número con sólo la contraseña es inseguro (un factor se autoenrolaría); **cambiar** uno existente es seguro, porque exige demostrar posesión del factor actual. Un atacante con la contraseña robada no tiene el celular viejo y la cadena de confianza no se corta.
      Reglas: el OTP DEBE ir al `PhoneNumber` vigente, NUNCA al nuevo. El cambio NO DEBE aplicarse hasta confirmar ese OTP. Un usuario **sin** `PhoneNumber` cargado NO DEBE poder usar este endpoint — ese caso es alta por admin (T050), no cambio. Rate limiting igual que el resto de `auth` (NFR-005).
      Tests: cambio exitoso confirmando OTP; rechazo con OTP inválido o expirado; rechazo si el usuario no tiene número previo; verificación de que el OTP se envió al número viejo y no al nuevo.
- [ ] **T062** **Roles del tenant** (`TenantAdmin` / `Member`) — claim `role` en el JWT y policy de autorización — depende de T015, T016
      **Bloqueante, no mejora.** Toda la superficie de seguridad ya escrita dice "sólo admin del tenant" (T050, T053, T059, branding) y **el rol no existe en el modelo**: `data-model.md` sólo lo menciona de pasada en la lista de tablas de Identity. Sin esto, "sólo admin" no es implementable — se simula.
      Alcance: `IdentityRole<Guid>` con los dos roles sembrados; asignación por usuario (el usuario ya está acotado al tenant, así que el rol NO necesita `TenantId` propio); claim `role` emitido en el JWT junto a `sub` y `tid`; policy `TenantAdmin` aplicada a todo endpoint `/api/admin/*`.
      El **primer usuario de un tenant** DEBE nacer `TenantAdmin`; no puede haber un tenant sin admin.
      Un `TenantAdmin` NO DEBE poder quitarse el rol a sí mismo si es el último admin del tenant.
      Tests: `Member` recibe `403` en cada endpoint `/api/admin/*`; el claim `role` viaja en el JWT; el último admin no puede degradarse.
- [ ] **T063** **Admin de plataforma** — superficie separada, fuera del modelo de tenants — depende de T015, T062
      **Problema de modelo, no de permisos.** `ApplicationUser.TenantId` es obligatorio y hay filtro global por tenant: el dueño de la plataforma, que por definición no pertenece a ningún tenant, hoy **no tiene lugar donde existir**.
      **Enfoque recomendado**: identidad y endpoints **separados** (`/api/platform/*`), NO un rol más sobre `ApplicationUser`. Motivo: todo el aislamiento (filtro EF + RLS + test de arquitectura) se apoya en la invariante "todo `ApplicationUser` pertenece a exactamente un tenant". Meter un super-usuario exento reabre la misma clase de riesgo que cierra T055b — un filtro con excepciones deja de ser una garantía.
      Alternativas descartadas: (a) `TenantId` nullable + exención del filtro — vuelve el filtro permisivo, justo lo que la spec prohíbe; (b) tenant "de sistema" con `Guid` conocido — el filtro sigue aplicando, así que no resuelve operar sobre otros tenants.
      Tests: ningún `ApplicationUser` queda sin `TenantId`; la superficie de plataforma no es alcanzable con un JWT de tenant, y viceversa.
      `[PENDIENTE: enfoque recomendado, requiere confirmación del usuario antes de implementar]`
- [ ] **T064** **Cerrar `POST /api/auth/register`**: pasa a exigir JWT de `TenantAdmin` del mismo tenant — depende de T019, T062
      Hoy el contrato (`contracts/auth-api.md`) muestra el request con `X-Tenant-ID` y **sin** `Authorization`: cualquiera que conozca un tenant ID se crea un usuario adentro. Contradice la premisa de que los usuarios los da de alta el admin.
      El alta DEBE incluir el `PhoneNumber` en el mismo acto (T050): un usuario creado sin número no puede entrar desde un dispositivo no trusted y queda inservible hasta que el admin lo complete.
      Tests: `register` sin JWT responde `401`; con JWT de `Member` responde `403`; con JWT de `TenantAdmin` de OTRO tenant responde `403`; alta sin `phoneNumber` es rechazada.
- [ ] **T065** **Refresh token con rotación y detección de reuso** — `POST /api/auth/refresh` + `POST /api/auth/logout` (FR-006, NFR-004) — depende de T015, T016, T019
      **Bloqueante para cualquier política de re-autenticación.** Hoy el JWT dura ≤ 60 min y **no hay forma de renovarlo**: un turno de 8 horas son **8 logins por persona**. Con OTP en cada login eso da ~1.200 SMS/mes por droguería y convierte al SMS en punto único de falla — si el proveedor se demora, el mostrador no trabaja. Con refresh, el login pasa a **1 por turno**.
      **Entidad** `RefreshToken : ITenantEntity`: `Id`, `TenantId`, `UserId`, `TokenHash`, `FamilyId`, `IssuedAt`, `ExpiresAt`, `ConsumedAt?`, `RevokedAt?`.
      El token DEBE persistirse **hasheado**, nunca en plano — mismo criterio que el OTP (`IPasswordHasher`) y la contraseña.
      **Rotación**: cada uso consume el refresh presentado y emite uno nuevo dentro de la misma `FamilyId`.
      **Detección de reuso (la propiedad que importa)**: si se presenta un token con `ConsumedAt != null`, el sistema DEBE revocar **toda la familia** y registrar el evento. Un refresh usado dos veces significa que alguien tiene una copia: la sesión se cae entera, para el legítimo y para el ladrón.
      **Vigencia**: el access token NO cambia, sigue en ≤ 60 min (NFR-004). El refresh define la sesión real. `RefreshTokenLifetimeHours` configurable por tenant, default propuesto **12 h** (un turno). `[PENDIENTE: confirmar default]`
      **Almacenamiento recomendado**: el refresh viaja en cookie `httpOnly` + `Secure` + `SameSite=Strict`, NO en `localStorage`. Es la credencial de larga vida: expuesta a XSS, entrega la sesión completa. El access token de 60 min puede seguir donde está. `[PENDIENTE: contradice parcialmente la decisión previa de guardar el JWT en localStorage — confirmar]`
      **Reglas**: el refresh DEBE estar acotado al tenant y al usuario; `logout` DEBE revocar la familia **del lado del servidor**, no sólo limpiar el cliente; un refresh NO DEBE servir para saltear el 2FA en un dispositivo nuevo — sólo renueva una sesión ya autenticada en ese dispositivo.
      Tests: rotación emite token nuevo e invalida el anterior; reusar un token consumido revoca la familia entera; el refresh de un tenant no sirve en otro; `logout` invalida del lado del servidor; el refresh caducado responde `401`.
      **Reevaluar después**: con 1 login por turno en vez de 8, el costo de exigir OTP en cada sesión cae de ~1.200 a ~150 SMS/mes por droguería. Ahí hay que decidir si `TrustedDevice` (FR-007) sigue haciendo falta o se elimina junto con `MaxTrustedDevices`, la caducidad y el endpoint de revocación.
- [ ] **T067** **Gestión y revocación de dispositivos y sesiones** (FR-007) — depende de T062, T065
      **El endpoint no existe.** `contracts/auth-api.md` lo dice textual: *"Endpoint admin de revocación/alta de dispositivo — no existe en las fuentes `[PENDIENTE: definir]`"*. Y FR-007 apoya todo su diseño en que `RevokedAt` lo setea un admin — con un botón que nadie construyó.
      **Superficie admin**: `GET /api/admin/users/{userId}/devices` (no se puede revocar lo que no se ve), `POST /api/admin/users/{userId}/devices/{deviceId}/revoke`, `POST /api/admin/users/{userId}/devices/revoke-all`.
      **Superficie propia**: `GET /api/auth/devices` y `POST /api/auth/devices/{deviceId}/revoke`. Que cada uno vea sus dispositivos NO es una comodidad: es cómo el usuario detecta uno que no reconoce. El admin no mira las sesiones de otro todos los días; el dueño de la cuenta sí.
      **Regla que no se puede omitir**: revocar un dispositivo DEBE revocar también las **familias de refresh token** asociadas (T065). Sin eso, el `RevokedAt` sólo impide saltear el 2FA a futuro mientras la sesión viva sigue funcionando hasta `RefreshTokenLifetimeHours` — revocar sin cerrar la sesión es teatro.
      La respuesta DEBE listar `DeviceId`, `TrustedAt`, último uso y si está activo. Revocar es idempotente. Un `Member` NO DEBE poder ver ni revocar dispositivos ajenos.
      Tests: revocar libera el slot de `MaxTrustedDevices`; revocar corta la sesión viva del dispositivo; un `Member` recibe `403` sobre dispositivos de otro usuario; revocar dos veces no falla.
- [ ] **T068** **Deshabilitar y rehabilitar usuario (offboarding real)** — depende de T062, T065, T067
      **T067 NO alcanza para el empleado que se va.** Revocarle el dispositivo no le saca el acceso: conserva su contraseña y su celular, así que vuelve a entrar con OTP. Lo único que corta el acceso es deshabilitar la cuenta.
      `POST /api/admin/users/{userId}/disable` y `/enable`, sólo `TenantAdmin`. ASP.NET Core Identity ya trae `LockoutEnabled` / `LockoutEnd`: se usa eso, no un flag nuevo.
      **Deshabilitar DEBE, en un solo acto**: rechazar el login con `401` uniforme, revocar **todas** las familias de refresh del usuario y revocar **todos** sus `TrustedDevice`. Un offboarding a medias no es un offboarding.
      El usuario deshabilitado NO DEBE poder pedir recuperación de contraseña (T069) ni cambiar su `PhoneNumber` (T061).
      Un `TenantAdmin` NO DEBE poder deshabilitarse a sí mismo si es el último admin del tenant — dejaría al tenant sin quien administre.
      Tests: el deshabilitado no entra ni con dispositivo trusted; su sesión viva muere en el acto; no puede pedir reset; el último admin no puede autodeshabilitarse; rehabilitar no resucita dispositivos ni sesiones viejas.
- [ ] **T069** **Recuperación de contraseña por SMS al número enrolado** — depende de T018, T019, T062, T065
      **No existe en ningún contrato.** Ni `register`, ni `login`, ni `confirm-device`. Día uno de operación real alguien olvida la contraseña y no hay camino que no sea tocar la base.
      **Canal: SMS al `PhoneNumber` enrolado, NO email.** Es coherente con todo lo ya decidido — el email es el identificador de login, nunca un canal de confianza (FR-008), y el número es enrolado por un admin, así que un atacante no puede redirigirlo. Además reusa la infraestructura de OTP que ya se construye en T018.
      **Fallback**: `POST /api/admin/users/{userId}/reset-password` iniciado por un `TenantAdmin`, para el que perdió el celular Y la contraseña. Sin esto el usuario queda encerrado afuera.
      **Reglas de seguridad**:
      - Respuesta **uniforme** exista o no el email — mismo motivo que T055c: distinguir permite enumerar qué correos usan Stockma.
      - Token de reset de **un solo uso**, expiración corta, persistido **hasheado**.
      - Un reset exitoso DEBE revocar **todas** las familias de refresh del usuario (T065). Si el atacante ya tenía sesión, cambiar la contraseña sin cortarla no lo echa.
      - El reset **NO DEBE** saltear el 2FA: entrar desde un dispositivo nuevo sigue exigiendo OTP.
      - Un usuario sin `PhoneNumber` cargado o deshabilitado (T068) NO DEBE poder iniciar el flujo — ese caso es del admin.
      - Rate limiting igual que el resto de `auth` (NFR-005).
      Tests: respuesta idéntica para email existente e inexistente; token usado dos veces es rechazado; el reset mata las sesiones vivas; tras el reset, un dispositivo desconocido sigue pidiendo OTP; el deshabilitado no puede iniciar el flujo.

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

### Trazabilidad del movimiento de stock — **abierto, PR aparte (depende de PR 3)**

- [ ] **T066** **`StockMovement`: libro de movimientos append-only con autor y motivo** (FR-013, FR-014) — depende de T033, T035, T062
      **El agujero**: hoy `POST /api/batches/{id}/adjust` recibe `{ "delta": -5 }` y nada más. Sin motivo, sin autor, sin restricción de rol. Y el `[PENDIENTE]` del contrato dice que la trazabilidad la da el `AuditSaveChangesInterceptor` — pero ese interceptor **no audita**: sólo aborta el `SaveChanges` si se intenta modificar `TenantId` (`data-model.md`). Está mal nombrado. **Hoy la trazabilidad del ajuste de stock es CERO.**
      En una droguería el faltante no entra por el login: entra por el ajuste. "Se venció", "se rompió", el inventario cuadra y no queda nombre.
      **Entidad** `StockMovement : ITenantEntity`, **append-only, nunca se actualiza ni se borra**: `Id`, `TenantId`, `BatchId`, `Delta`, `Reason`, `Notes?`, `UserId`, `OccurredAt`.
      `Reason` (enum): `Sale`, `Reception`, `Return`, `Expiry`, `Damage`, `Loss`, `CountCorrection`.
      **Reglas**: todo cambio de `Batch.CurrentQuantity` DEBE nacer de un `StockMovement` — no DEBE existir camino que mueva stock sin dejar fila. `Reason` es obligatorio. `UserId` sale del claim `sub`, NUNCA del body. La fila es inmutable: corregir un error es **otro** movimiento compensatorio, no editar el anterior.
      **Rol** (depende de T062): un `Member` PUEDE registrar movimientos, incluidas mermas — bloquearle el paso sólo lograría que no registre nada. Los motivos de merma (`Expiry`, `Damage`, `Loss`) por encima de `MermaApprovalThreshold` (configurable por tenant) DEBEN requerir `TenantAdmin`.
      El control real no es prohibir el botón: es que **el nombre quede pegado al movimiento** y el admin lo vea.
      `GET /api/batches/{id}/movements` y reporte por usuario y motivo para el admin.
      **Renombrar `AuditSaveChangesInterceptor`**: hace de guardia de `TenantId`, no de auditoría. El nombre actual hace creer que hay un rastro que no existe.
      Tests: ajuste sin `reason` es rechazado; el `UserId` persistido es el del JWT y no el del body; ningún camino modifica `CurrentQuantity` sin crear la fila; merma sobre el umbral con `Member` responde `403`; el libro no admite `UPDATE` ni `DELETE`; la suma de movimientos reconcilia con `CurrentQuantity`.

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

- [x] **T055** Resuelta: **el email es único en toda la plataforma**, no por tenant.
      `POST /api/auth/login` queda **exento** del `TenantMiddleware` y resuelve el `TenantId`
      desde el email. Es lo único compatible con el login genérico ya decidido. Costo
      aceptado: una persona no puede tener cuenta en dos tenants con el mismo correo; si
      algún día hace falta, la salida es una tabla `UserTenant` con selección post-login.
      Actualizados `spec.md` (FR-005, FR-006), `data-model.md` y `contracts/auth-api.md`
- [ ] **T055a** `TenantMiddleware`: excepción para `POST /api/auth/login` + test de que
      **ninguna otra ruta** quedó exenta — depende de T055
- [ ] **T055b** Búsqueda del usuario por email en el login con `IgnoreQueryFilters()`
      explícito y acotado, leyendo sólo lo necesario para autenticar y obtener el `TenantId`.
      Test que demuestre que por ese camino no se puede leer nada más de otro tenant. El
      filtro **NO DEBE** volverse permisivo cuando el `TenantContext` está vacío — depende de T055a
- [ ] **T055c** Respuesta uniforme `401 AUTH_INVALID_CREDENTIALS` para email inexistente,
      contraseña incorrecta y usuario de otro tenant, con test de los tres casos. Con email
      único global, distinguirlos permite enumerar qué correos usan Stockma — depende de T055a
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

| Requerimiento | Tareas                                                             | PR   |
| ------------- | ------------------------------------------------------------------ | ---- |
| FR-001        | T009                                                               | PR 2 |
| FR-002        | T008, T010, T013                                                   | PR 2 |
| FR-003        | T011                                                               | PR 2 |
| FR-004        | T012                                                               | PR 2 |
| FR-005        | T015, T017, T019, T022, T062, T064, T068, T069                     | PR 3 |
| FR-006        | T016, T017, T019, T021, T062, T063, T065                           | PR 3 |
| FR-007        | T015, T020, T067, T068                                             | PR 3 |
| FR-008        | T015, T018, T019, T021, T049, T050, T061                                 | PR 3 |
| FR-009        | T023, T025, T029                                                   | PR 4 |
| FR-010        | T023, T025, T029                                                   | PR 4 |
| FR-011        | T026, T027, T030                                                   | PR 4 |
| FR-012        | T031, T033, T038                                                   | PR 5 |
| FR-013        | T033, T037, T066                                                   | PR 5 |
| FR-014        | T031, T032, T037, T066                                             | PR 5 |
| FR-015        | T031, T034, T037                                                   | PR 5 |
| NFR-001       | T014                                                               | PR 2 |
| NFR-002       | T009                                                               | PR 2 |
| NFR-003       | T011                                                               | PR 2 |
| NFR-004       | T016, T018, T049                                                   | PR 3 |
| NFR-005       | T019                                                               | PR 3 |
| NFR-006       | T026                                                               | PR 4 |
| NFR-007       | T028                                                               | PR 4 |
| NFR-008       | T023 — `Currency` del producto sobre el COP por defecto del tenant | PR 4 |
| NFR-009       | T036                                                               | PR 5 |
| NFR-010       | T034                                                               | PR 5 |

## Notas de ejecución

- **Commits por unidad de comportamiento**, nunca por tipo de archivo: los tests viajan con el comportamiento que verifican y la documentación con el cambio visible al usuario.
- Ningún PR DEBERÍA superar las 400 líneas cambiadas; si una fase excede el presupuesto, dividirla en slices encadenados dentro del mismo PR chain antes de implementar.
- Cada PR debe ser revisable en ≤ 60 minutos.
- `strict_tdd: false` según `.specify/config.yaml` — modo estándar; los tests de cada fase DEBE entrar en el mismo PR que su comportamiento.
