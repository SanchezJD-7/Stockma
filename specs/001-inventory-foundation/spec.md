# Especificación: Inventory Foundation

**Feature branch**: `001-inventory-foundation`
**Estado**: Planificación completa — pendiente de implementación
**Capacidades**: `tenant-isolation`, `identity-access`, `product-catalog`, `batch-inventory`

---

## Resumen Ejecutivo

Primer slice de Stockma: levantar el monorepo completo (.NET 9 + React 19) con aislamiento multi-tenant, agregados `Product` y `Batch`, autenticación Identity+JWT y CI/CD. Es la base sobre la que se construyen todos los slices posteriores.

Stockma es un SaaS B2B multi-tenant de gestión de inventarios con control de lotes y vencimientos (FEFO), destinado a farmacias, tiendas y pequeños emprendedores en Colombia.

Las cuatro capacidades de este slice son **NUEVAS** (proyecto greenfield). No hay capacidades modificadas.

| Dominio | Tipo | Requerimientos | Escenarios |
|---|---|---|---|
| tenant-isolation | Nueva (full) | FR-001 … FR-004 | 6 |
| identity-access | Nueva (full) | FR-005 … FR-008 | 9 |
| product-catalog | Nueva (full) | FR-009 … FR-011 | 6 |
| batch-inventory | Nueva (full) | FR-012 … FR-015 | 8 |

## Alcance

### Incluye

- Scaffolding monorepo: `backend/src/{Domain,Application,Infrastructure,Api,Worker}`, `backend/tests/×4`, `frontend/web`, `packages/contracts`, `ops/docker-compose.yml`
- `ITenantEntity` + middleware `TenantContext` (header `X-Tenant-ID`) + `ApplyTenantFilters` reflectivo + PostgreSQL RLS de respaldo
- Agregados **Product** (Sku, Barcode?, Name, Category) y **Batch** (ProductId, LotNumber, ExpirationDate, CurrentQuantity, Location, `xmin` como token de concurrencia)
- DbContext + migración inicial + base de `AuditSaveChangesInterceptor`
- Identity + JWT + dispositivos confiables (máx 2, configurable) + 2FA por SMS en dispositivo nuevo
- Envío del OTP vía `ISmsSender` / `SmsOtpSender` `[PENDIENTE: proveedor de SMS no elegido]`
- `PUT /api/admin/users/{userId}/phone-number` — alta/edición del `PhoneNumber` por un admin del tenant, y cierre de la superficie self-service de Identity (`SetPhoneNumberAsync`) `[PENDIENTE: propuesto, no está en las fuentes originales]`
- Comandos/queries: RegisterProduct, UpdateProduct, RegisterBatch, AdjustBatchStock, SearchProducts, GetProductByBarcode
- Pipeline MediatR: Validation, Transaction, Audit behaviours
- Frontend: scaffolding Vite, features `auth` e `inventory` base, TanStack Query con keys por tenant, PWA manifest
- CI: build+test backend, build+lint frontend

### Fuera de alcance (slices posteriores)

- **Sales/FEFO (slice 2)** — `FefoBatchSelector`, agregado `Sale`. El selector FEFO queda DIFERIDO; este slice sólo deja el índice `(ProductId, ExpirationDate)` que lo soportará.
- **Alertas de vencimiento (slice 3)** — Hangfire, semaforización proactiva, emails
- **Reportes de auditoría (slice 4)** — QuestPDF/ClosedXML
- Suscripciones, branding por tenant, campos DIAN en uso

## Contexto y Restricciones

### Stack

- **Backend**: .NET 9, C#, EF Core, PostgreSQL, MediatR (CQRS), Clean Architecture, DDD
- **Frontend**: React 19, TypeScript, Vite, Material UI (Emotion), CSS puro con estilos globales, TanStack Query, Zustand
- **Testing**: xUnit + FluentAssertions (`dotnet test`), Playwright (pendiente) para e2e

### Decisiones ya cerradas

| Decisión | Valor |
|---|---|
| Multi-tenant | Row-level: `TenantId` + EF `HasQueryFilter` + PostgreSQL RLS de respaldo |
| Autenticación | Identity + JWT, dispositivos confiables (máx 2, configurable por tenant) |
| Canal del 2FA | **SMS** al celular de cada usuario (`ApplicationUser.PhoneNumber`). El OTP NO DEBE viajar por email; el email es sólo el identificador de login |
| Origen del `PhoneNumber` | Escribible **únicamente por un admin del tenant**. El usuario NO DEBE poder registrar ni cambiar su propio `PhoneNumber` |
| Alta de dispositivos | Manual por admin, sin revocación automática por inactividad |
| Alcance de `MaxTrustedDevices` | Gobierna **sólo el privilegio de saltear el 2FA**, nunca el acceso. El login NO DEBE bloquearse por límite de dispositivos: cualquier dispositivo entra vía OTP por SMS |
| Expulsión de dispositivos | Nunca automática. `RevokedAt` se setea SÓLO por acción manual de un admin |
| Stock negativo | HTTP **422** con `errorCode: BATCH_NEGATIVE_STOCK` y rechazo total del ajuste (nunca parcial) |
| Semaforización | Computada en lectura, **no** persistida |
| SKU | Automático `SKU-{n}` por tenant cuando no hay barcode |
| Capa de estilos | **Material UI** (Emotion) para componentes complejos + **CSS puro con estilos globales** para layout, botones y textos. `CssBaseline` de MUI aporta el reset. Los tokens viven en `styles/tokens.css` y son la **única fuente de verdad**: el theme NO replica hex, `styles/mui-bridge.ts` lee de `:root` los valores ya resueltos y construye el theme con ellos. Pasarle `var()` a la paleta no alcanza — los componentes de MUI llaman `alpha()` al renderizar y revientan. Tipografía única: **Work Sans** |
| Idioma | Documentación, especificaciones y comentarios en **español** (keywords RFC 2119 incluidas). **Todo identificador de código en inglés, sin excepción**: variables, métodos, clases, propiedades, valores de enum, columnas de base de datos, claves de configuración, claves y clases CSS, y nombres de test. Sólo los comentarios y los mensajes de error van en español |
| Agregados | `Product`, `Batch` y `Sale` como agregados **separados** |

### Riesgos

| Riesgo | Prob. | Mitigación |
|---|---|---|
| Fuga cross-tenant por filtro olvidado | Media | RLS + test de arquitectura obligatorio |
| Integración Identity + multi-tenant | Media | `ApplicationUser.TenantId`; validar en spike temprano |
| Slice ≫400 líneas por PR | Alta | PRs encadenados por unidad de trabajo (ver `tasks.md`) |
| HTTPS en dev (PWA/scanner) | Baja | `dotnet dev-certs`, documentar en README |

### Rollback

Greenfield sin datos: revertir commits; la DB de dev se recrea con docker-compose (drop + migrate).

### Dependencias

.NET 9 SDK, Node 20+, Docker Desktop (PostgreSQL). Decisiones de negocio ya confirmadas.

---

## Escenarios de Usuario y Pruebas

Todos los escenarios usan Dado/Cuando/Entonces. Cada uno está trazado a su requerimiento en la sección [Requerimientos](#requerimientos).

### Dominio: tenant-isolation

#### FR-001 — Resolución de tenant por request

**Escenario: Tenant válido**
- **DADO** request autenticada con header `X-Tenant-ID` válido
- **CUANDO** llega al middleware
- **ENTONCES** `TenantContext` expone el `TenantId` durante toda la request

**Escenario: Header ausente o inválido**
- **DADO** request sin header `X-Tenant-ID`
- **CUANDO** llega al middleware
- **ENTONCES** responde `400 Bad Request` y no ejecuta el pipeline

#### FR-002 — Filtro global EF Core

**Escenario: Consulta filtrada**
- **DADO** dos tenants con datos
- **CUANDO** tenant A consulta productos/lotes
- **ENTONCES** sólo retorna registros de tenant A

**Escenario: Entidad sin ITenantEntity**
- **DADO** nueva entidad tenant sin `ITenantEntity`
- **CUANDO** corre el test de arquitectura
- **ENTONCES** el build falla

#### FR-003 — Respaldo RLS PostgreSQL

**Escenario: Query directa**
- **DADO** conexión con rol de aplicación
- **CUANDO** ejecuta query sin filtro de aplicación
- **ENTONCES** RLS retorna sólo filas del tenant activo (variable de sesión)

#### FR-004 — TenantId inmutable

**Escenario: Intento de cambio**
- **DADO** entidad tenant existente
- **CUANDO** un update intenta modificar `TenantId`
- **ENTONCES** es rechazada y persiste el `TenantId` original

### Dominio: identity-access

#### FR-005 — Registro de usuario

**Escenario: Registro exitoso**
- **DADO** email y contraseña válidos con header tenant
- **CUANDO** `POST /api/auth/register`
- **ENTONCES** crea `ApplicationUser` con `TenantId` y retorna `201`

**Escenario: Email duplicado**
- **DADO** email ya registrado en el tenant
- **CUANDO** registrar
- **ENTONCES** responde `409 Conflict`

#### FR-006 — Login JWT

**Escenario: Dispositivo confiable**
- **DADO** credenciales válidas desde un dispositivo trusted (`RevokedAt IS NULL`)
- **CUANDO** `POST /api/auth/login`
- **ENTONCES** el sistema DEBE saltear el OTP de FR-008 y retornar el `accessToken` JWT en un único paso

**Escenario: Credenciales inválidas**
- **DADO** email/contraseña incorrectos
- **CUANDO** `POST /api/auth/login`
- **ENTONCES** responde `401` sin revelar cuál factor falló

#### FR-007 — Dispositivos confiables (máx 2, configurable)

> `MaxTrustedDevices` gobierna **el privilegio de saltear el 2FA**, no el acceso. El acceso lo gobierna FR-008 y NO DEBE bloquearse nunca por este límite.

**Escenario: Nuevo dispositivo con slot libre**
- **DADO** usuario con menos de `MaxTrustedDevices` dispositivos trusted activos (`RevokedAt IS NULL`)
- **CUANDO** completa el OTP en `POST /api/auth/confirm-device`
- **ENTONCES** el sistema DEBE emitir el JWT **y** DEBE marcar el dispositivo como trusted (`TrustedAt = now`, `RevokedAt = null`), que en adelante saltea el OTP

**Escenario: Máximo alcanzado**
- **DADO** usuario que ya tiene `MaxTrustedDevices` dispositivos trusted activos
- **CUANDO** completa el OTP en `POST /api/auth/confirm-device` desde un dispositivo nuevo
- **ENTONCES** el sistema DEBE emitir el JWT igualmente (el acceso NUNCA se bloquea), NO DEBE marcar el dispositivo como trusted y NO DEBE revocar ningún dispositivo existente; ese dispositivo deberá hacer OTP en **cada** login hasta que un admin libere un slot manualmente

**Escenario: Revocación de un dispositivo trusted**
- **DADO** un dispositivo trusted activo
- **CUANDO** un admin lo revoca manualmente
- **ENTONCES** el sistema DEBE setear `RevokedAt = now` y liberar el slot; ninguna otra ruta PUEDE setear `RevokedAt`

#### FR-008 — 2FA por SMS en dispositivo nuevo

> El OTP se envía por **SMS** al celular de **cada usuario** que intenta loguearse (`ApplicationUser.PhoneNumber`). Ese número es escribible **sólo por un admin del tenant**: el usuario NO DEBE poder registrarlo ni cambiarlo por su cuenta. El email sigue siendo el identificador de login, nunca el canal del segundo factor.

**Escenario: Login desde dispositivo nuevo**
- **DADO** credenciales válidas desde dispositivo desconocido
- **CUANDO** `POST /api/auth/login`
- **ENTONCES** responde `requiresDeviceConfirmation` y envía el OTP por SMS al `PhoneNumber` del usuario

**Escenario: OTP válido**
- **DADO** OTP correcto recibido por SMS en el celular del usuario
- **CUANDO** `POST /api/auth/confirm-device`
- **ENTONCES** emite el JWT **siempre**, haya slot libre o no bajo `MaxTrustedDevices`; el marcado como trusted es un efecto separable que depende del slot (FR-007)

**Escenario: OTP inválido/expirado**
- **DADO** OTP errado o vencido
- **CUANDO** `confirm-device`
- **ENTONCES** responde `401` y registra el intento

**Escenario: Usuario sin `PhoneNumber` cargado**
- **DADO** un usuario cuyo `PhoneNumber` el admin todavía no cargó
- **CUANDO** intenta loguearse desde un dispositivo no trusted
- **ENTONCES** no existe destino al cual enviar el OTP
  `[PENDIENTE: usuario sin PhoneNumber cargado — definir si el primer login se permite sin 2FA, si el admin debe cargar el número antes de habilitar la cuenta, o si se bloquea]`

### Dominio: product-catalog

#### FR-009 — Registro de producto con SKU automático

**Escenario: Sin barcode**
- **DADO** `POST /api/products` con nombre/categoría sin barcode
- **CUANDO** se registra
- **ENTONCES** asigna SKU interno automático y retorna `201`

**Escenario: SKU duplicado**
- **DADO** SKU explícito ya existente en el tenant
- **CUANDO** registrar
- **ENTONCES** responde `409 Conflict`

#### FR-010 — Actualización de producto

**Escenario: Update válido**
- **DADO** producto existente
- **CUANDO** `PUT /api/products/{id}`
- **ENTONCES** actualiza campos editables conservando el SKU

**Escenario: Intento de cambiar SKU**
- **CUANDO** el update incluye SKU nuevo
- **ENTONCES** rechazada con `400`

#### FR-011 — Búsqueda por nombre y barcode

**Escenario: Autocompletado por nombre**
- **DADO** producto "Acetaminofén 500mg"
- **CUANDO** `GET /api/products?query=aceta`
- **ENTONCES** retorna coincidencias ordenadas por relevancia

**Escenario: Lookup por barcode**
- **CUANDO** `GET /api/products/by-barcode/{code}`
- **ENTONCES** retorna el producto o `404`

### Dominio: batch-inventory

#### FR-012 — Registro de lote

**Escenario: Lote válido**
- **DADO** producto existente y datos de lote válidos
- **CUANDO** `POST /api/batches`
- **ENTONCES** retorna `201` con `BatchDto`

**Escenario: Producto inexistente**
- **DADO** `ProductId` inválido
- **CUANDO** registrar lote
- **ENTONCES** rechazada con `404`/`400`

#### FR-013 — Ajuste de stock

**Escenario: Aumento de stock**
- **DADO** lote con stock 10
- **CUANDO** ajustar `+5`
- **ENTONCES** `CurrentQuantity = 15`

**Escenario: Salida excede stock**
- **DADO** lote con stock 3
- **CUANDO** ajustar `-5`
- **ENTONCES** rechazada (`422`, `errorCode: BATCH_NEGATIVE_STOCK`) y stock sin cambios

#### FR-014 — Concurrencia optimista

**Escenario: Conflicto concurrente**
- **DADO** dos updates simultáneos sobre el mismo lote
- **CUANDO** el segundo llega
- **ENTONCES** reintenta una vez; si persiste, responde `409`

#### FR-015 — Semaforización de vencimiento

**Escenario: Verde por defecto**
- **DADO** lote a 7 meses de vencer
- **CUANDO** se consulta
- **ENTONCES** `color = Green`

**Escenario: Vencido**
- **DADO** lote con `ExpirationDate` pasada
- **CUANDO** se consulta
- **ENTONCES** `color = Expired`

**Escenario: Umbrales personalizados**
- **DADO** tenant configura verde `>9 meses`
- **CUANDO** lote a 7 meses
- **ENTONCES** el color se evalúa con los umbrales del tenant (`Yellow`)

### Cobertura de testing

- **Happy paths**: cubiertos, al menos uno por requerimiento.
- **Edge cases / errores**: cubiertos (`400`/`401`/`404`/`409`/`422` según caso).
- **Desviación notada**: el briefing original pedía "FEFO selector"; el alcance lo excluye explícitamente (slice 2). Este slice documenta el soporte base (índice `(ProductId, ExpirationDate)`) y lo marca como DIFERIDO.

---

## Requerimientos

Keywords normativas en español (RFC 2119): DEBE, NO DEBE, DEBERÍA, PUEDE.

### Requerimientos Funcionales

#### tenant-isolation

| ID | Requerimiento |
|---|---|
| **FR-001** | **Resolución de tenant por request** — El sistema DEBE resolver el `TenantId` en cada request autenticada mediante middleware `TenantContext` a partir del header `X-Tenant-ID`. |
| **FR-002** | **Filtro global EF Core** — El sistema DEBE aplicar `HasQueryFilter` a toda entidad que implemente `ITenantEntity`; toda entidad tenant DEBE implementar `ITenantEntity`. |
| **FR-003** | **Respaldo RLS PostgreSQL** — El sistema DEBERÍA habilitar Row-Level Security como defensa en profundidad además del filtro EF. |
| **FR-004** | **TenantId inmutable** — El sistema NO DEBE permitir modificar `TenantId` en updates de entidades tenant. |

#### identity-access

| ID | Requerimiento |
|---|---|
| **FR-005** | **Registro de usuario** — El sistema DEBE registrar usuarios con ASP.NET Core Identity asociando el `TenantId` del header `X-Tenant-ID`. |
| **FR-006** | **Login JWT** — El sistema DEBE emitir JWT con claims de usuario y tenant tras credenciales válidas. |
| **FR-007** | **Dispositivos confiables (máx 2, configurable)** — El sistema DEBE limitar a `MaxTrustedDevices` (def. 2, configurable por tenant) los dispositivos que pueden **saltear el 2FA**, NO DEBE usar ese límite para negar el acceso y NO DEBE revocar dispositivos automáticamente; el alta de un slot ocupado se libera sólo por acción manual de un admin. DEBE notificar al dispositivo trusted existente cuando se confía uno nuevo. |
| **FR-008** | **2FA por SMS en dispositivo nuevo** — El sistema DEBE exigir OTP enviado por SMS al `ApplicationUser.PhoneNumber` cuando se detecta un dispositivo no conocido, y DEBE permitir el acceso a CUALQUIER dispositivo que supere ese OTP, sin importar `MaxTrustedDevices`. El `PhoneNumber` DEBE ser escribible sólo por un admin del tenant y el usuario NO DEBE poder modificar el suyo. `[PENDIENTE: usuario sin PhoneNumber cargado — definir si el primer login se permite sin 2FA, si el admin debe cargar el número antes de habilitar la cuenta, o si se bloquea]` |

#### product-catalog

| ID | Requerimiento |
|---|---|
| **FR-009** | **Registro de producto con SKU automático** — El sistema DEBE generar SKU interno único por tenant cuando el producto no tiene `Barcode`; `Barcode` PUEDE estar ausente. |
| **FR-010** | **Actualización de producto** — El sistema DEBE actualizar campos del catálogo; el SKU DEBE ser inmutable tras la creación. |
| **FR-011** | **Búsqueda por nombre y barcode** — El sistema DEBE soportar búsqueda por nombre (autocompletado) y lookup exacto por `Barcode`, ambos acotados al tenant. |

#### batch-inventory

| ID | Requerimiento |
|---|---|
| **FR-012** | **Registro de lote** — El sistema DEBE registrar lotes vinculados a un `Product` con `LotNumber`, `ExpirationDate`, `CurrentQuantity` y `LocationShelf`. |
| **FR-013** | **Ajuste de stock** — El sistema DEBE ajustar `CurrentQuantity` con delta con signo y NO DEBE permitir cantidad negativa. |
| **FR-014** | **Concurrencia optimista** — El sistema DEBE usar `xmin` de PostgreSQL como concurrency token y DEBERÍA reintentar conflictos una vez. |
| **FR-015** | **Semaforización de vencimiento** — El sistema DEBE clasificar cada lote con umbrales configurables por tenant: Verde `>6 meses`, Amarillo `3–6 meses`, Rojo `<3 meses`, Vencido (fecha pasada). |

### Requerimientos No Funcionales

| ID | Dominio | Requerimiento |
|---|---|---|
| **NFR-001** | tenant-isolation | Seguridad: el sistema DEBE resistir fuga cross-tenant con tests de integración y arquitectura obligatorios. |
| **NFR-002** | tenant-isolation | Performance: la resolución de tenant DEBERÍA ser O(1) (cacheada en request scope). |
| **NFR-003** | tenant-isolation | RLS DEBERÍA aplicarse con rol no-propietario en la migración inicial. |
| **NFR-004** | identity-access | Seguridad: hash PBKDF2 (Identity); el JWT DEBERÍA expirar en ≤60 min; el OTP DEBERÍA expirar en ≤10 min. |
| **NFR-005** | identity-access | El login DEBERÍA aplicar rate limiting (p. ej. 5 intentos por IP/minuto). |
| **NFR-006** | product-catalog | Performance: la búsqueda por nombre DEBERÍA responder en `<500ms` (índice trigram/full-text). |
| **NFR-007** | product-catalog | Seguridad: las consultas DEBE estar acotadas al `TenantId`. |
| **NFR-008** | product-catalog | Moneda: el `Currency` del producto PUEDE sobrescribir el COP por defecto del tenant. |
| **NFR-009** | batch-inventory | El índice `(ProductId, ExpirationDate)` DEBERÍA existir para soportar FEFO futuro. |
| **NFR-010** | batch-inventory | La semaforización DEBERÍA computarse al leer (no persistida). |

### Contratos de API

Detalle completo en [`contracts/`](./contracts/):

- [`contracts/auth-api.md`](./contracts/auth-api.md) — `/api/auth/*` (FR-005 … FR-008)
- [`contracts/products-api.md`](./contracts/products-api.md) — `/api/products/*` (FR-009 … FR-011)
- [`contracts/batches-api.md`](./contracts/batches-api.md) — `/api/batches/*` (FR-012 … FR-015)

Regla transversal (FR-001): el header `X-Tenant-ID: {guid}` DEBE estar presente en todas las requests `/api/*` autenticadas. Errores: `400` si el header está ausente o es inválido.

---

## Entidades Clave

Modelo detallado (campos, tipos, invariantes, índices) en [`data-model.md`](./data-model.md).

| Entidad | Rol | Notas clave |
|---|---|---|
| **Tenant** | Unidad de aislamiento | `TenantId` presente en toda entidad tenant vía `ITenantEntity`; inmutable (FR-004) |
| **TenantSettings** | Configuración por tenant | `MaxTrustedDevices` (def. 2), umbrales de semáforo (def. 6/3 meses) |
| **User** (`ApplicationUser`) | Identidad | `IdentityUser` + `TenantId`; `PhoneNumber` (destino del OTP por SMS, escribible sólo por admin); relación con dispositivos confiables |
| **TrustedDevice** | Dispositivo autorizado | `UserId`, `DeviceId`, `Fingerprint`, `TrustedAt`, `RevokedAt?` |
| **DeviceOtp** | OTP de 2FA por SMS | Persistido, expira ≤10 min (NFR-004); se envía al `PhoneNumber` del usuario |
| **Product** | Agregado raíz | `Sku` (inmutable), `Barcode?`, `Name`, `Category`, `Currency` |
| **Batch** | Agregado raíz separado | `ProductId`, `LotNumber`, `ExpirationDate`, `CurrentQuantity`, `LocationShelf`, `Status`, `xmin` |
| **SemaphoreColor** | Value object | `Green` \| `Yellow` \| `Red` \| `Expired`; computado en lectura (NFR-010) |

---

## Checklist de Revisión y Aceptación

Checklist detallado y trazable en [`checklists/requirements.md`](./checklists/requirements.md).

### Criterios de éxito del slice

- [ ] CI verde: `dotnet build` + `dotnet test` + frontend build/lint
- [ ] Test de integración: tenant A no ve productos/lotes de tenant B
- [ ] Test de arquitectura: toda entidad tenant implementa `ITenantEntity`
- [ ] Login JWT + 2FA por SMS en dispositivo nuevo (`[PENDIENTE: proveedor de SMS no elegido]` — sandbox del proveedor en dev)
- [ ] Registrar producto y lote vía API con header `X-Tenant-ID`

### Criterios de aceptación por dominio

**tenant-isolation**
- [ ] Tenant A no lee productos/lotes de tenant B (test de integración)
- [ ] Test de arquitectura: toda entidad tenant implementa `ITenantEntity`
- [ ] RLS activa en la migración inicial
- [ ] Update de `TenantId` rechazado

**identity-access**
- [ ] Login emite JWT con claims usuario+tenant
- [ ] Dispositivo nuevo exige OTP por SMS al `PhoneNumber` del usuario
- [ ] Máximo 2 dispositivos que saltean el 2FA, configurable por tenant
- [ ] Sin slot libre el login funciona igual (OTP en cada ingreso), sin revocar a nadie
- [ ] Notificación a dispositivo trusted para autorizar uno nuevo

**product-catalog**
- [ ] SKU automático cuando no hay barcode
- [ ] Autocompletado por nombre en `<500ms`
- [ ] Lookup por barcode exacto
- [ ] SKU inmutable en updates

**batch-inventory**
- [ ] Registrar lote vía API con header tenant
- [ ] El ajuste evita stock negativo
- [ ] `xmin` como concurrency token con 1 reintento
- [ ] Semáforo con umbrales por tenant (contrato listo para FEFO en slice 2)
