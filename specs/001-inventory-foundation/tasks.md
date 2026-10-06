# Tareas: Inventory Foundation

**Feature branch**: `001-inventory-foundation`
**Fuente**: desglose SDD original (reformateado a Spec Kit, sin pérdida)
**Referencias**: [`spec.md`](./spec.md) · [`plan.md`](./plan.md) · [`data-model.md`](./data-model.md) · [`contracts/`](./contracts/)

**Convención de IDs**: `T001` … `T089`. `[P]` = puede ejecutarse en paralelo con las otras tareas `[P]` de su misma fase (sin dependencia de archivo ni de orden).

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
| 3    | Identity + JWT + dispositivos confiables + 2FA por SMS | **PR 3** | T015–T022, T049, T050, T061–T065, T067–T070 | PR 2             |
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

### Corte en PRs encadenados

La fase creció de 10 a 19 tareas y no cabe en un PR revisable: el presupuesto de esta guía es
400 líneas por PR. Se entrega en cinco slices, cada uno con sus tests y mergeable por sí mismo.

| Slice | Tareas | Qué entrega | Estado |
|---|---|---|---|
| **3a** Cimientos | T015, T016, T062 | Entidades, emisión del JWT, roles, persistencia con RLS y la función acotada del login | ✅ en `main` |
| **3b** Flujo de login | T017, T018, T019, T020, T055a, T055b, T055c, T021, T022, T064, T073, T074, T075, T076, T077, T078, T079, T080, T081, T082, T083, T084, T085, T086, T087, T088, T089 | `register`, `login`, `confirm-device`, OTP por SMS, las tres reglas de T055, el cierre de `register`, la autenticación por defecto de toda la superficie de negocio, el endurecimiento del login (anti-enumeración, fuerza bruta y carreras del OTP, sender de SMS, conformidad del contrato) y el cierre de la segunda revisión adversarial (RLS efectiva en runtime, `confirm-device` atómico, bootstrap sin carreras, arranque que falla temprano) y el de la tercera (chequeo de rol por membresía, `bootstrap-admin` con los chequeos de arranque, `auth_lookup` con requisito explícito y `search_path` seguro, configuración base sin credenciales, carrera de registro entre tenants, pool fail-closed) | ✅ |
| **3c** Sesión | T065 | Refresh token con rotación y detección de reuso | ⬜ |
| **3d** Ciclo de vida del acceso | T050, T061, T067, T068, T069, T070 | Alta y cambio del celular, revocación, offboarding, reset y caducidad | ⬜ |
| **3e** Plataforma | T063 | Superficie separada del admin de plataforma | ⬜ |

**T049** (proveedor de SMS concreto) quedó **diferida** durante la fase 3b y se cerró en esta fase con
**Twilio**: el sender de consola de T018 sigue siendo el default de `Development`.

**Orden dentro de 3b** (respeta las dependencias): T018 → T017 → T055b → T020 → T019 → T055a →
T055c → T021/T022. Se arranca por T018 y no por T017 porque el `LoginCommand` necesita poder
emitir un OTP para estar completo.

- [x] **T015** Domain: `ApplicationUser`, `TrustedDevice`, `DeviceOtp` (FR-005, FR-007, FR-008)
- [x] **T016** `[P]` `Infrastructure/Identity/JwtTokenService.cs` — claims `sub`, `tid`, `exp` ≤ 60 min (FR-006, NFR-004)
- [x] **T017** Application: `RegisterUserCommand`, `LoginCommand`, `ConfirmDeviceCommand` — depende de T015
- [x] **T018** `[P]` `ISmsSender` / `ConsoleSmsSender` (sender de consola en dev) + expiración de OTP a 10 min (FR-008, NFR-004)
      El OTP viaja por **SMS** al `ApplicationUser.PhoneNumber`, nunca por email. La ventana ≤ 10 min se mantiene.
- [x] **T019** `Api/Controllers/AuthController` — `register` / `login` / `confirm-device` + rate limiting 5/IP/min (NFR-005) — depende de T017
- [x] **T020** Marcado condicional de `TrustedDevice` en `confirm-device` bajo `MaxTrustedDevices` (FR-007) — depende de T017
      El JWT se emite **siempre**; el `TrustedDevice` se crea **sólo si** el conteo de activos (`RevokedAt IS NULL`) es `< MaxTrustedDevices`. Sin slot: `200` con `deviceTrusted: false`, sin crear la fila y **sin revocar a nadie**. `RevokedAt` sólo cambia por acción manual de un admin. Ver [`plan.md`](./plan.md#5-autenticación) y [`contracts/auth-api.md`](./contracts/auth-api.md).
- [x] **T021** `[P]` Unit tests: login desde dispositivo conocido/desconocido, OTP expirado
- [x] **T022** API tests (`WebApplicationFactory`): `401` credenciales inválidas, `409` email duplicado, `requiresDeviceConfirmation` — depende de T019
- [x] **T049** Integración con proveedor de SMS: implementación concreta de `ISmsSender`, config `Sms { Provider, AccountSid, ApiKey, Sender }`, reintento/fallo del envío y sender de consola para dev (FR-008) — depende de T018
      **Proveedor elegido: Twilio.** `TwilioSmsSender` habla con la API Messages con Basic auth y reintenta sólo los transitorios (`429`/`408`/`5xx`/timeout); un `4xx` no se reintenta. Si el envío falla igual, el login **responde igual** y el fallo queda en el log (ADR-016: una respuesta distinta revelaría que el email existe). Fuera de `Development` no arranca sin credenciales completas; en `Development` sigue primando el sender de consola salvo que `Sms:Provider` esté cargado.
- [ ] **T050** `PUT /api/admin/users/{userId}/phone-number` (sólo admin del tenant) **y cierre de la superficie self-service de Identity** sobre `PhoneNumber` (FR-008) — depende de T019
      El usuario NO DEBE poder registrar ni cambiar su propio `PhoneNumber`. Identity lo expone por defecto (`UserManager.SetPhoneNumberAsync`, `ChangePhoneNumberAsync`, `GenerateChangePhoneNumberTokenAsync` y los endpoints self-service del Identity UI/API): hay que cerrar esa superficie explícitamente, no alcanza con no usarla. Ver [`contracts/auth-api.md`](./contracts/auth-api.md) y [`data-model.md`](./data-model.md).
      `[PENDIENTE: propuesto, no está en las fuentes originales]`
- [ ] **T061** `PUT /api/auth/phone-number` — cambio del propio `PhoneNumber` **autenticado y con OTP al número ACTUAL** (FR-008) — depende de T018, T019, T050
      Resuelve la fricción operativa de T050: sin esto, el admin del tenant que quiere cambiar su propio número tiene que escribirle al admin de plataforma.
      Distinción que sostiene la regla: **enrolar** el primer número con sólo la contraseña es inseguro (un factor se autoenrolaría); **cambiar** uno existente es seguro, porque exige demostrar posesión del factor actual. Un atacante con la contraseña robada no tiene el celular viejo y la cadena de confianza no se corta.
      Reglas: el OTP DEBE ir al `PhoneNumber` vigente, NUNCA al nuevo. El cambio NO DEBE aplicarse hasta confirmar ese OTP. Un usuario **sin** `PhoneNumber` cargado NO DEBE poder usar este endpoint — ese caso es alta por admin (T050), no cambio. Rate limiting igual que el resto de `auth` (NFR-005).
      Tests: cambio exitoso confirmando OTP; rechazo con OTP inválido o expirado; rechazo si el usuario no tiene número previo; verificación de que el OTP se envió al número viejo y no al nuevo.
- [x] **T062** **Roles del tenant** (`TenantAdmin` / `Member`) — claim `role` en el JWT y policy de autorización — depende de T015, T016
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
      **Decidido por el usuario**: superficie separada.
- [x] **T064** **Cerrar `POST /api/auth/register`**: pasa a exigir JWT de `TenantAdmin` del mismo tenant — depende de T019, T062
      Hoy el contrato (`contracts/auth-api.md`) muestra el request con `X-Tenant-ID` y **sin** `Authorization`: cualquiera que conozca un tenant ID se crea un usuario adentro. Contradice la premisa de que los usuarios los da de alta el admin.
      El alta DEBE incluir el `PhoneNumber` en el mismo acto (T050): un usuario creado sin número no puede entrar desde un dispositivo no trusted y queda inservible hasta que el admin lo complete.
      Tests: `register` sin JWT responde `401`; con JWT de `Member` responde `403`; con JWT de `TenantAdmin` de OTRO tenant responde `403`; alta sin `phoneNumber` es rechazada.
      **Cerrado por hallazgo de revisión de seguridad, antes de tener T063.** Se agregó, además, un bootstrap CLI (`dotnet run --project backend/src/Stockma.Api -- bootstrap-admin`) para dar de alta al primer `TenantAdmin` de un tenant existente y vacío, como parche hasta que exista la superficie de admin de plataforma. Ver ADR-014.
- [x] **T073** **Autenticación por defecto + tenant atado al token**: `FallbackPolicy` con `RequireAuthenticatedUser()`, `TenantMiddleware` rechaza con `403 TENANT_MISMATCH` cuando el `X-Tenant-ID` no coincide con el `tid` del JWT — depende de T019, T062, T064
      **El agujero**: `ProductsController`, `BatchesController` y `TenantController` no tenían `[Authorize]` y `AddAuthorization` no traía `FallbackPolicy`. El `TenantMiddleware` tomaba el tenant **sólo** del header `X-Tenant-ID` y nunca lo comparaba contra el `tid` del JWT. Cualquiera que conociera el GUID de un tenant —no es secreto, viaja en cada request— podía leer y ajustar su stock sin loguearse nunca. La RLS no protege esto: el tenant de la sesión de base sale de un header que controla el cliente.
      **La decisión**: autenticado por defecto (`FallbackPolicy.RequireAuthenticatedUser()`); `[AllowAnonymous]` **sólo** en `POST /api/auth/login`, `POST /api/auth/confirm-device` (T055a) y en `MapOpenApi()` (sólo development). `GET /api/tenant/branding` **no** es anónimo: con login genérico no hay tenant antes de loguearse, así que se pide después, con el token. `Program.cs` corre `UseAuthentication()` **antes** de `TenantMiddleware`, para que el middleware pueda leer el claim `tid` cuando lo hay.
      Con el `tid` disponible, `TenantMiddleware` compara: si hay usuario autenticado y su `tid` no es igual al `X-Tenant-ID`, responde `403 TENANT_MISMATCH` **antes** de llegar a `Authorization` o al controller — ningún handler ni la base se tocan.
      **Consecuencia**: el chequeo manual de `AuthController.Register` (`tid` del JWT contra `tenantContext.TenantId`, agregado en T064) queda redundante — el `TenantMiddleware` ya lo cubre para toda ruta, no sólo `register` — y se eliminó. `[Authorize(Policy = AuthorizationPolicies.TenantAdmin)]` se mantiene.
      Tests: `GET` anónimo a `/api/products` o `/api/batches` → `401`; JWT del tenant A + header A → `2xx`; JWT del tenant A + header B → `403 TENANT_MISMATCH` sin tocar el handler (el ajuste de stock no se aplica); `login` y `confirm-device` siguen alcanzables sin token. Ver ADR-015.
- [x] **T074** **Anti-enumeración en `login` y `confirm-device`**: un único `401 AUTH_OTP_REJECTED` para toda falla de `confirm-device` y hash señuelo en todo camino de falla del login — depende de T019, T055c
      **El agujero**: `confirm-device` respondía `AUTH_INVALID_CREDENTIALS` a un email inexistente y un error de OTP a uno existente; el login volvía sin correr PBKDF2 para un email inexistente o bloqueado. Las dos cosas enumeran correos: una por el `errorCode`, la otra por el reloj. Ver ADR-016.
      **Seguridad**: `login` y `confirm-device` siguen **anónimos** y sin `X-Tenant-ID` (T055a); el tenant sale del email. `confirm-device` responde `401 AUTH_OTP_REJECTED` con el **mismo cuerpo** para email inexistente, sin OTP vigente, código errado, vencido, quemado o carrera perdida, y ante un email inexistente no activa ningún tenant ni toca un OTP. El login corre **una** verificación de contraseña en todo camino de falla.
      Tests: `Confirm_ForAnUnknownEmail_IsRejectedWithTheSingleOtpError`, `Confirm_ForAnUnknownEmail_NeverTouchesTheOtp`, `ConfirmDevice_ForAnUnknownEmailAndForAWrongCode_AnswersIdentically` (cuerpos idénticos end-to-end), `Verify_WithAnUnknownEmail_StillRunsOnePasswordVerification`, `Verify_WithABlankEmail_…`, `Verify_ForALockedOutUser_…`, `Verify_WithTheWrongPassword_RunsExactlyOnePasswordVerification` (hasher contador, sin medir tiempos).
- [x] **T075** **Fuerza bruta y carreras del OTP**: emitir invalida los anteriores, sólo cuenta el último, tope de intentos persistido, tope de emisión por ventana y consumo atómico — depende de T018, T074
      Columnas nuevas en `device_otps`: `failed_attempts`, `invalidated_at` y `xmin` como token de concurrencia (migración `HardenDeviceOtps`). El invariante de intentos vive en `DeviceOtp.RegisterFailedAttempt`. Config: `Otp:MaxAttempts` (5), `Otp:MaxIssuesPerWindow` (5), `Otp:IssueWindowMinutes` (15). `Issue` y `Consume` se serializan por usuario con `pg_advisory_xact_lock`. Ver ADR-016.
      **Seguridad**: sólo alcanzable desde `login` (emisión) y `confirm-device` (consumo), ambos anónimos, y siempre con el tenant ya resuelto desde el email (la RLS aplica). Pasado el tope de emisión el login responde **igual** que un envío real (`200 requiresDeviceConfirmation`), sin SMS y sin `429`. Agotados los intentos, ni el código correcto sirve: `401 AUTH_OTP_REJECTED`. Cada intento fallido queda registrado (contador + log `Warning` sin código, celular ni `deviceId`).
      Tests: `Issue_InvalidatesThePreviousLiveOtpsOfThatDevice`, `Consume_OnlyChecksTheNewestLiveOtp`, `Consume_WithTheWrongCode_RecordsTheFailedAttempt`, `Consume_ReachingTheAttemptLimit_BurnsTheOtp`, `Consume_AfterTheAttemptLimit_StopsCheckingGuesses`, `Consume_ParallelWrongGuesses_NeverEvaluateMoreThanTheLimit` y `Consume_TheRightCodeTwiceInParallel_SucceedsExactlyOnce` (Postgres real), `Issue_BeyondTheWindowCap_SendsNoSmsAndDoesNotFail`, `Issue_BeyondTheWindowCap_KeepsTheLastCodeUsable`, `Issue_OutsideTheWindow_DoesNotCountTowardsTheCap`, `Issue_ParallelRequests_NeverExceedTheWindowCap`, `Consume_AFailedAttempt_IsLoggedWithoutTheCodeNorThePhone`, y los de dominio de `DeviceOtpTests`.
- [x] **T076** **`MaxTrustedDevices` sin carreras**: `TryTrust` cuenta e inserta bajo un lock por usuario y re-chequea si ese dispositivo ya tiene una fila activa — depende de T020
      Sin índice único parcial: un dispositivo vencido conserva `revoked_at IS NULL`, y reconfiarlo obligaría a escribirle `RevokedAt`, que ADR-007 reserva para una baja manual. Ver ADR-016.
      **Seguridad**: sólo se llama desde `confirm-device` **después** de superar el OTP, con el tenant del usuario ya activo. Nunca hay más de `MaxTrustedDevices` activos ni dos filas activas del mismo `(usuario, dispositivo)`, ni en carrera; superar el tope sigue siendo `200` con `deviceTrusted: false` (FR-007).
      Tests: `TryTrust_InParallelForDistinctDevices_NeverExceedsTheLimit`, `TryTrust_InParallelForTheSameDevice_NeverDuplicatesTheActiveRow` (Postgres real, contextos paralelos).
- [x] **T077** **El sender de SMS de consola sólo en `Development`**, y la API no arranca en otro entorno sin proveedor real — depende de T018; T049 lo reemplaza
      **Seguridad**: fuera de `Development` nunca se registra el sender que escribe código y celular en el log; el host falla al arrancar con un mensaje claro (`ValidateOnStart`). El subcomando `bootstrap-admin` sigue funcionando porque no arranca el host (hasta T085: desde ahí corre los mismos chequeos de arranque, así que fuera de `Development` también necesita proveedor real).
      Tests: `Development_UsesTheConsoleSender`, `OutsideDevelopment_WithoutARealProvider_FailsAtStartup` (`Production` y `Staging`), `OutsideDevelopment_NeverRegistersTheConsoleSender`.
- [x] **T078** **Conformidad del contrato de auth**: `400 VALIDATION_FAILED` en vez de `500`, `429 AUTH_RATE_LIMITED` en `problem+json`, forwarded headers con proxies explícitos, y `deviceId` normalizado y con entropía mínima — depende de T019, T074
      `ValidationFailedException` dedicada (no se mapea `ArgumentException`: las internas siguen siendo `500`). `ForwardedHeaders:KnownProxies` / `KnownNetworks` vacíos por defecto → `X-Forwarded-For` ignorado; `UseForwardedHeaders()` corre antes del rate limiter. `deviceId` recortado en un único lugar (`DeviceIdentifier`) y de 16 a 128 caracteres. El `fingerprint` es informativo, no un control. Ver ADR-016.
      **Seguridad**: `login`/`confirm-device` anónimos, `register` sólo `TenantAdmin` del mismo tenant (T064/T073, sin cambios). Un `deviceId` inválido responde `400 VALIDATION_FAILED` **antes** de mirar credenciales, igual para cualquier email. Un `X-Forwarded-For` de un origen no declarado no cambia la IP que cuenta para el rate limit.
      Tests: `Login_WithoutADeviceId_Returns400ValidationFailed`, `Login_WithALowEntropyDeviceId_Returns400ValidationFailed`, `ConfirmDevice_WithoutAFingerprint_Returns400ValidationFailed`, `Register_WithAPasswordIdentityRejects_Returns400ValidationFailed`, `Auth_BeyondTheRateLimit_AnswersProblemJsonWithTheErrorCode`, `ForwardedFor_FromAnUnknownSource_IsIgnoredByTheRateLimiter`, `ForwardedFor_FromAKnownProxy_…`, `ForwardedFor_FromAKnownNetwork_…`, `Login_NormalizesTheDeviceIdBeforeUsingIt`, `Consume_NormalizesTheDeviceIdLikeIssue`, `IsTrusted_NormalizesTheDeviceIdLikeTryTrust`, `DeviceIdentifierTests`.
- [x] **T079** **RLS efectiva en runtime**: el runtime se conecta como `app_user`, `FORCE ROW LEVEL SECURITY` en toda tabla con RLS y la API no arranca con un rol que saltee la RLS — depende de T011, T055b, T072
      **El agujero**: `appsettings.json` conectaba como `stockma`, el rol que corre las migraciones y es **dueño** de las tablas. PostgreSQL no aplica RLS al propietario, y ninguna tabla tenía `FORCE`: la capa 2 del aislamiento no filtraba **nada** en runtime. Los tests no lo veían porque la API y los fixtures de `DeviceOtpService`/`TrustedDevices` corrían como el superusuario `postgres`. Ver ADR-017.
      Connection strings: `ConnectionStrings:Postgres` = `app_user` (runtime y `bootstrap-admin`); `ConnectionStrings__PostgresMigrations` = propietario, sólo para `dotnet ef` (reemplaza `STOCKMA_CONNECTION_STRING`). Migración `ForceRowLevelSecurity`: `FORCE` en `tenant_settings`, `products`, `batches`, `users`, `trusted_devices`, `device_otps`, y `auth_find_user_by_email` pasa a ser del rol `auth_lookup`. Chequeo de arranque `RuntimeDatabaseRoleCheck` fuera de `Development`/`Testing` (T084 quitó `Testing`).
      **Seguridad**: ninguna ruta cambia de quién la puede llamar. Con JWT del tenant A, la base misma oculta las filas del tenant B aunque se saltee el filtro de EF, y rechaza escribir con `tenant_id` de B (`42501`). Fuera de `Development`/`Testing` la API no arranca si su rol es superusuario, tiene `BYPASSRLS` o es (o hereda de) el propietario de una tabla de `public`. El login sigue resolviendo el tenant sin contexto, y el dueño de su función no tiene login, ni tablas, ni `BYPASSRLS`, y sólo lee las columnas de autenticación.
      Tests: `RowLevelSecurity_IsForcedOnEveryTableThatEnablesIt`, `TheTableOwner_WithoutAnActiveTenant_SeesNoRows`, `LoginLookup_RunsAsADedicatedRoleThatOwnsNoTableAndCannotBypassRls`, `LoginLookupRole_CanReadOnlyTheAuthenticationColumns`, `Migrations_ApplyAsANonSuperuserOwner_AndTheLoginLookupStillFindsTheUser`, `TheOwner_WithoutAnActiveTenant_SeesNoUsers`, `ForceRowLevelSecurity_RollsBackAndReappliesAsANonSuperuserOwner`, `RuntimeDatabaseRoleTests` (`app_user` pasa; superusuario, `BYPASSRLS`, propietario y miembro del propietario se rechazan), `TheRuntimeConnection_UsesTheRestrictedAppUser`, `WithTheEfFilterBypassed_RowLevelSecurityAloneHidesAnotherTenantsRows`, `WithTheEfFilterBypassed_RowLevelSecurityRejectsWritingIntoAnotherTenant`, `ATenantAToken_ListingProducts_NeverSeesTenantBRows`, `OutsideDevelopment_AsASuperuser_RefusesToStart` (`Production`, `Staging`), `OutsideDevelopment_AsTheRestrictedAppUser_Starts`, `InDevelopmentOrTesting_TheRuntimeRoleIsNotChecked` (hoy `InDevelopment_TheRuntimeRoleIsNotChecked`, T084), `TheServiceUnderTest_RunsAsTheRestrictedAppUser` (OTP y dispositivos). Toda la suite de API y los tests de `DeviceOtpService`, `TrustedDevices` y `UserAccounts` corren ahora como `app_user`.
- [x] **T080** **`confirm-device` no quema el OTP ante una falla posterior y rechaza al usuario bloqueado** — depende de T074, T075, T076
      **El agujero**: el `fingerprint` sólo se validaba en blanco (la columna es de 256) y el OTP se consumía y **commiteaba** antes de `TryTrust`: una huella larga o cualquier falla después del consumo dejaba al usuario con el código gastado y sin JWT. Además `FindByEmailAsync` ignoraba `LockoutEnd`, así que un usuario deshabilitado con un OTP vigente recibía token.
      **La decisión**: huella de 1 a 256 caracteres validada junto al `deviceId`, antes de mirar el email. `IDeviceOtpService.ConsumeAsync<T>` recibe lo que sigue al consumo (confiar el dispositivo y emitir el JWT) y lo corre **dentro** de la misma transacción con lock por usuario: si falla, se revierte el consumo. No es una transacción externa: un código errado registra el intento y tira, y una transacción de afuera borraría ese contador (T075). El bloqueo usa la misma regla que el login. Ver ADR-017.
      **Seguridad**: `confirm-device` sigue anónimo y sin `X-Tenant-ID`. Huella inválida → `400 VALIDATION_FAILED` igual para cualquier email y sin tocar el OTP. Usuario bloqueado → `401 AUTH_OTP_REJECTED`, byte a byte igual que un email inexistente, sin tocar el OTP. Un código errado sigue sumando `FailedAttempts` aunque la operación falle.
      Tests: `Confirm_WithAFingerprintLongerThanTheColumn_IsRejectedBeforeLookingUpTheUser`, `Confirm_WithAFingerprintOfExactly256Characters_IsAccepted`, `Confirm_WhenTrustingTheDeviceFails_DoesNotCommitTheOtpConsumption`, `Confirm_WhenTheTokenCannotBeIssued_DoesNotCommitTheOtpConsumption`, `Confirm_WithAValidOtp_CommitsTheConsumptionAfterIssuingTheToken`, `Consume_WhenTheFollowUpFails_RollsTheConsumptionBack`, `Consume_WhenTheFollowUpFails_RollsBackTheFollowUpsWritesToo`, `Consume_WithTheWrongCode_NeverRunsTheFollowUp_AndStillRecordsTheAttempt`, `FindByEmail_ForALockedOutUser_ReturnsNull`, `FindByEmail_ForAUserWhoseLockoutExpired_FindsTheUser`, `ConfirmDevice_WithAFingerprintLongerThan256_Returns400AndKeepsTheOtpUsable`, `ConfirmDevice_ForALockedOutUser_IsRejectedLikeAnUnknownEmail`, `ConfirmDevice_ForALockedOutUser_DoesNotSpendTheOtp`.
- [x] **T081** **Errores del `TenantMiddleware` en `application/problem+json`** — depende de T073
      `TENANT_HEADER_MISSING`, `TENANT_HEADER_INVALID` y `TENANT_MISMATCH` salían como `application/json`; ahora usan el mismo `Content-Type` que los errores de dominio y el `429`.
      **Seguridad**: sin cambios de acceso; mismos status (`400`/`403`) y `errorCode`.
      Tests: `EveryRejection_IsProblemJson` (header ausente, inválido y tenant distinto del token).
- [x] **T082** **La configuración inválida corta el arranque, y `UseForwardedHeaders` corre primero** — depende de T016, T078
      `JwtOptions` con `ValidateOnStart` (`Jwt:Key` ≥ 32 bytes, `Issuer`/`Audience` no vacíos, `ExpiresMinutes` 1–60) en vez de fallar en la primera request que emite un token. `ForwardedHeadersOptions` con `ValidateOnStart` y mensajes que nombran la clave (`KnownProxies` que no es IP, `KnownNetworks` fuera de CIDR o con prefijo inválido). Nota: las listas de proxies ya se resolvían al construir el pipeline, pero con mensajes opacos (`An invalid IP address was specified`) y sin validar el largo del prefijo. `UseForwardedHeaders()` pasa a ser el primer middleware, antes de `UseHttpsRedirection()`.
      **Seguridad**: una clave JWT corta nunca llega a firmar tokens. Detrás de un proxy conocido que termina TLS, `X-Forwarded-Proto: https` evita la redirección en bucle; desde un origen no declarado el header se sigue ignorando (T078).
      Tests: `AJwtKeyShorterThan32Bytes_FailsAtStartup`, `AMissingJwtIssuer_FailsAtStartup`, `AJwtLifetimeOutsideOneToSixtyMinutes_FailsAtStartup`, `AnUnparseableKnownProxy_FailsAtStartup`, `AnUnparseableKnownNetwork_FailsAtStartup`, `HttpsRedirection_OnPlainHttpWithoutForwardedProto_Redirects` (control), `HttpsRedirection_BehindAKnownProxyForwardingHttps_DoesNotRedirect`.
- [x] **T083** **Bootstrap sin carreras y alta de usuario atómica** — depende de T062, T064
      **El agujero**: `bootstrap-admin` chequeaba "sin usuarios" y después creaba, sin lock: dos corridas en paralelo dejaban dos admins. `CreateAsync` + `AddToRoleAsync` no eran atómicos: si fallaba el rol quedaba un usuario sin rol, y el bootstrap se negaba para siempre porque el tenant "ya tiene usuarios".
      **La decisión**: `ITenantAccounts.RunExclusivelyAsync` toma `pg_advisory_xact_lock(hashtextextended('tenant_bootstrap:{tenantId}', 0))` y dentro corre el chequeo y el alta. `UserAccounts.CreateAsync` crea usuario y rol en una transacción (se une a la del lock si ya hay una). Ver ADR-017.
      **Seguridad**: el subcomando sigue fuera de HTTP (ADR-014) y conectado como `app_user`, con el tenant destino activo. Nunca hay más de un `TenantAdmin` creado por bootstrap, ni un usuario sin rol.
      Tests (Postgres real, como `app_user`): `Bootstrap_InParallel_CreatesExactlyOneAdmin`, `Bootstrap_OnAnEmptyTenant_CreatesItsFirstTenantAdmin`, `Bootstrap_OnATenantThatAlreadyHasUsers_IsRejected`, `Create_WhenTheRoleCannotBeAssigned_LeavesNoUserBehind`, `TenantHasAnyUser_WithoutUsers_IsFalse`, `TenantHasAnyUser_WithAUser_IsTrue`, `TenantHasAnyUser_IgnoresUsersOfOtherTenants`, `TenantAccountsExists_ForASeededTenant_IsTrue`, `TenantAccountsExists_ForAnUnknownTenant_IsFalse`; unitario `Bootstrap_ChecksAndCreatesWhileHoldingTheTargetTenantsLock`.
- [x] **T084** **El chequeo de rol mira membresías, no sólo herencia, y la única excepción es `Development`** — depende de T079
      **El agujero**: `RuntimeDatabaseRole` usaba `pg_has_role(…, 'USAGE')`, que no ve una membresía `NOINHERIT`, y no miraba si el rol podía hacer `SET ROLE` a un superusuario o a un rol con `BYPASSRLS`. Además el entorno `Testing` apagaba el chequeo sin que ningún test lo necesitara. Ver ADR-017, decisión 4.
      **Seguridad**: fuera de `Development` (cualquier otro nombre de entorno, `Testing` incluido) la API no arranca si el rol de runtime es, o es miembro a cualquier profundidad (`pg_has_role(…, 'MEMBER')`) de, un superusuario, un rol con `BYPASSRLS` o el propietario de una tabla de `public`. `app_user` sigue pasando.
      Tests: `EnsureRestricted_AsANonInheritingMemberOfATableOwner_Throws`, `EnsureRestricted_AsAMemberOfABypassRlsRole_Throws`, `EnsureRestricted_AsAMemberOfASuperuser_Throws`, `OutsideDevelopment_AsASuperuser_RefusesToStart` (`Production`, `Staging`, `Testing`), `InDevelopment_TheRuntimeRoleIsNotChecked`.
- [x] **T085** **`bootstrap-admin` corre los chequeos de arranque antes de hacer nada** — depende de T064, T082, T084
      **El agujero**: `Program.cs` devolvía el subcomando antes de `app.Run()`, así que ni el chequeo de rol ni `ValidateOnStart` corrían: el CLI podía escribir en la base como superusuario o con opciones inválidas. Ver ADR-017, decisión 4.
      **La decisión**: `BootstrapAdminCommandLine.RunAsync` corre primero `IStartupValidator.Validate()` y `RuntimeDatabaseRole.EnsureRestrictedAsync` (misma excepción de `Development` que la API); si fallan, sale con código `1` y el motivo en `stderr`, sin mandar el comando. Consecuencia aceptada: fuera de `Development`, sin proveedor de SMS (T049) el subcomando tampoco corre.
      **Seguridad**: el subcomando sigue fuera de HTTP (ADR-014). Nunca escribe con un rol que saltee la RLS fuera de `Development`, ni con una configuración que la API rechazaría.
      Tests: `OutsideDevelopment_AsASuperuser_ExitsNonZeroBeforeSendingTheCommand` (`Production`, `Staging`), `WithAnInvalidJwtKey_ExitsNonZeroBeforeSendingTheCommand`, `WithoutARuntimeConnectionString_ExitsNonZeroBeforeSendingTheCommand`, `OutsideDevelopment_AsTheRestrictedAppUser_SendsTheCommand`, `InDevelopment_TheRuntimeRoleIsNotChecked` (`BootstrapAdminCommandLineTests`).
- [x] **T086** **`auth_lookup`: requisito de `ADMIN` explícito y `search_path` sin `pg_temp` primero** — depende de T079
      **El agujero**: si `auth_lookup` ya existía y lo había creado otro rol, un propietario `CREATEROLE` no superusuario abortaba `ForceRowLevelSecurity` con un `permission denied to grant role` sin pista. Y la función `SECURITY DEFINER` fijaba `SET search_path = public`, que deja a `pg_temp` primero: una tabla temporal `users` de `app_user` reemplazaba a la real. Ver ADR-017, decisiones 3 y 8.
      **La decisión**: `ForceRowLevelSecurity` chequea `pg_has_role(current_user, 'auth_lookup', 'USAGE WITH ADMIN OPTION')` y, si falta, aborta con un mensaje que nombra el rol y trae el `GRANT … WITH ADMIN OPTION, INHERIT FALSE, SET FALSE` a correr. Migración nueva `HardenLoginLookupSearchPath`: `ALTER FUNCTION public.auth_find_user_by_email(text) SET search_path = pg_catalog, public, pg_temp` (mismo chequeo y misma maniobra de membresía temporal). README y ADR-017 documentan el requisito.
      **Seguridad**: el login sigue anónimo y resuelve el tenant desde el email; ningún objeto de sesión de `app_user` cambia lo que lee la función. El rol que migra nunca queda miembro de `auth_lookup`.
      Tests: `Migrations_AsAnOwnerWithoutAdminOnAnExistingAuthLookup_FailWithAnActionableMessage`, `LoginLookup_IsNotShadowedByATempTableNamedUsers`; siguen en verde `Migrations_ApplyAsANonSuperuserOwner_AndTheLoginLookupStillFindsTheUser`, `TheOwner_WithoutAnActiveTenant_SeesNoUsers` y `ForceRowLevelSecurity_RollsBackAndReappliesAsANonSuperuserOwner` (ahora baja y sube también `HardenLoginLookupSearchPath`).
- [x] **T087** **La configuración base no trae credenciales** — depende de T079
      **El agujero**: `appsettings.json` traía `app_user`/`app_user`; un deploy sin override intentaba una contraseña conocida en vez de fallar. Ver ADR-017, decisión 9.
      **La decisión**: la cadena de desarrollo pasa a `appsettings.Development.json`; la base deja `ConnectionStrings:Postgres` vacía y `DatabaseOptions` con `ValidateOnStart` corta el arranque con un mensaje que nombra la clave. README: `ConnectionStrings__PostgresMigrations` nunca se define en el entorno de la API.
      **Seguridad**: fuera de `Development` la API y `bootstrap-admin` no arrancan sin `ConnectionStrings__Postgres` explícita. Los tests siguen poniendo su propia cadena.
      Tests: `AMissingRuntimeConnectionString_FailsAtStartup` (`Development`, `Production`), `TheBaseAppSettings_CarryNoRuntimeConnectionString`.
- [x] **T088** **La carrera de registro entre tenants responde `409`, no `500`** — depende de T062, T064
      **El agujero**: el pre-chequeo ve todos los tenants, pero Identity valida unicidad con la RLS del tenant activo. Dos altas simultáneas del mismo email en tenants distintos pasaban las dos y la segunda caía en el índice único como `500`. Ver ADR-017, decisión 10.
      **La decisión**: `UserAccounts.CreateAsync` traduce el `23505` de `ux_users_normalized_email` / `ux_users_normalized_user_name` y los errores `DuplicateEmail` / `DuplicateUserName` de Identity a `EmailAlreadyRegisteredException`.
      **Seguridad**: `register` sigue siendo sólo `TenantAdmin` del mismo tenant (T064/T073). El que pierde la carrera recibe el mismo `409 AUTH_EMAIL_DUPLICATE` que el pre-chequeo, sin datos del otro tenant.
      Tests (Postgres real, como `app_user`, sin pasar por el pre-chequeo): `Create_WhenAnotherTenantRegisteredTheEmailFirst_ThrowsEmailAlreadyRegistered`, `Create_WhenTheSameTenantAlreadyHasTheEmail_ThrowsEmailAlreadyRegistered`.
- [x] **T089** **El pool de conexiones es fail-closed, y la brecha de las tablas sin RLS queda escrita** — depende de T011, T079
      Test de caracterización: una conexión física usada con el tenant A y reusada sin tenant no ve filas, porque Npgsql corre `DISCARD ALL` al devolverla. El test falla si se agrega `No Reset On Close=true` (verificado a mano). Brecha aceptada en ADR-017: `user_roles`, `user_claims`, `user_tokens`, `user_logins`, `roles`, `role_claims` y `tenants` no tienen RLS (sin `tenant_id`; el login lee `user_roles` sin tenant), así que ahí el aislamiento es sólo de la aplicación. Queda en las decisiones abiertas.
      **Seguridad**: sin cambios de acceso. Una conexión reusada sin `app.tenant` sigue siendo fail-closed.
      Tests: `APooledConnection_ReusedWithoutATenant_SeesNoRows`.
- [x] **T065** **Refresh token con rotación y detección de reuso** — `POST /api/auth/refresh` + `POST /api/auth/logout` (FR-006, NFR-004) — depende de T015, T016, T019
      **Bloqueante para cualquier política de re-autenticación.** Hoy el JWT dura ≤ 60 min y **no hay forma de renovarlo**: un turno de 8 horas son **8 logins por persona**. Con OTP en cada login eso da ~1.200 SMS/mes por droguería y convierte al SMS en punto único de falla — si el proveedor se demora, el mostrador no trabaja. Con refresh, el login pasa a **1 por turno**.
      **Entidad** `RefreshToken : ITenantEntity`: `Id`, `TenantId`, `UserId`, `FamilyId`, `DeviceId`, `TokenHash`, `IssuedAt`, `ExpiresAt`, `FamilyExpiresAt`, `ConsumedAt?`, `RevokedAt?`. Ver [`data-model.md`](./data-model.md) y [`contracts/auth-api.md`](./contracts/auth-api.md).
      El token DEBE persistirse **hasheado**, nunca en plano, con **SHA-256** — NO con `IPasswordHasher`: con salt aleatorio la fila no se puede buscar por hash, y el token tiene 256 bits de entropía, así que no necesita un hash lento (ADR-018).
      **Rotación**: cada uso consume el refresh presentado y emite uno nuevo dentro de la misma `FamilyId`.
      **Detección de reuso (la propiedad que importa)**: si se presenta un token con `ConsumedAt != null`, el sistema DEBE revocar **toda la familia** y registrar el evento. Un refresh usado dos veces significa que alguien tiene una copia: la sesión se cae entera, para el legítimo y para el ladrón.
      **Vigencia (ADR-019, modifica lo anterior)**: el access token baja a **15 min** (`Jwt:ExpiresMinutes`, el arranque rechaza fuera de 1–15). La sesión muere tras **30 min sin renovar** (`SessionIdleTimeoutMinutes` por tenant), controlado por el servidor: cada token vence 30 min después de emitido y nunca después del tope de la familia. El frontend renueva sólo si hubo actividad y cierra la sesión en pantalla a los 30 min exactos. La cookie es de sesión: muere al cerrar el navegador.
      **Tope absoluto**: `RefreshTokenLifetimeHours` configurable por tenant, default **8 h** (decisión del usuario; la propuesta era 12 h con margen). Vigencia **absoluta** desde el login, no deslizante: da exactamente 1 login por turno. Consecuencia aceptada: quien empalma dos turnos o hace horas extra vuelve a loguearse en medio de la jornada. Si el mostrador se queja, la salida es vigencia deslizante con tope absoluto, no un número más grande.
      **Almacenamiento (decidido)**: el refresh viaja en cookie `httpOnly` + `Secure` + `SameSite=Strict`, **NO** en `localStorage`. Es la credencial de larga vida: en `localStorage` cualquier XSS —una dependencia npm comprometida alcanza— se lleva la sesión completa y renovable. JavaScript no puede leer una cookie `httpOnly`. El access token de 15 min **sí** sigue en `localStorage`, como estaba decidido: es corto y no renueva nada por sí solo.
      **Reglas**: el refresh DEBE estar acotado al tenant y al usuario; `logout` DEBE revocar la familia **del lado del servidor**, no sólo limpiar el cliente; un refresh NO DEBE servir para saltear el 2FA en un dispositivo nuevo — sólo renueva una sesión ya autenticada en ese dispositivo.
      **Criterios de seguridad** (cada uno con su test):
      - `refresh` y `logout` son anónimos y sin `X-Tenant-ID`; el tenant sale de la fila del token por una función acotada de `auth_lookup`. Un `X-Tenant-ID` de otro tenant no cambia el tenant del access token emitido.
      - Toda falla de `refresh` responde el mismo `401 AUTH_REFRESH_REJECTED` y borra la cookie: sin cookie, desconocido, vencido, revocado, reusado, otro `deviceId`, usuario bloqueado.
      - Reusar un token consumido revoca **toda** la familia; el último emitido deja de servir.
      - Un `refresh` con un `deviceId` distinto del login revoca la familia.
      - Rotar no extiende el vencimiento absoluto de la familia, y un token renovado cerca del tope vence en el tope.
      - Un refresh emitido hace más de `SessionIdleTimeoutMinutes` responde `401` aunque la familia esté dentro de su tope.
      - La cookie no lleva `Max-Age` ni `Expires`.
      - `Jwt:ExpiresMinutes` fuera de 1–15 corta el arranque.
      - Dos `refresh` en paralelo con el mismo token: exactamente uno rota y la familia queda revocada (test contra Postgres real).
      - `logout` responde siempre `204`, revoca la familia del lado del servidor y borra la cookie.
      - La cookie sale `HttpOnly`, `Secure`, `SameSite=Strict`, `Path=/api/auth`; `refresh` exige `Content-Type: application/json`.
      - El token en claro nunca se persiste ni aparece en logs.
      **Entrega en 4 sub-piezas**: 2a spec (esta) · 2b entidad `RefreshToken` + migración + búsqueda por hash vía `auth_lookup` · 2c cookie en `login`/`confirm-device` + `POST /refresh` con rotación, inactividad y tope · 2d renovación en el front por actividad, serializada entre pestañas. La detección de reuso y `logout` completan T065 en las piezas 3 y 4.
      **Reevaluar después**: con 1 login por turno en vez de 8, el costo de exigir OTP en cada sesión cae de ~1.200 a ~150 SMS/mes por droguería. Ahí hay que decidir si `TrustedDevice` (FR-007) sigue haciendo falta o se elimina junto con `MaxTrustedDevices`, la caducidad y el endpoint de revocación.
- [x] **T070** **Caducidad de `TrustedDevice`** — `ExpiresAt` + `TrustedDeviceLifetimeDays` (FR-007) — depende de T015, T067
      **El agujero**: hoy `TrustedDevice` no tiene campo de expiración y el invariante de `data-model.md` **prohíbe** la revocación automática. Un dispositivo queda confiable **para siempre**.
      **Campo nuevo `ExpiresAt`, NO reusar `RevokedAt`.** `RevokedAt` DEBE seguir significando "una persona lo dio de baja" — es auditoría, y mezclarlo con vencimiento automático arruina el registro de quién hizo qué.
      ```
      activo = RevokedAt IS NULL AND ExpiresAt > now()
      ```
      `TenantSettings.TrustedDeviceLifetimeDays`, default **15** (decisión del usuario; el estándar de industria es 30 y la propuesta inicial fue 8). `ExpiresAt` se fija en `TrustedAt + TrustedDeviceLifetimeDays` al confiar el dispositivo.
      **Beneficio lateral**: el slot de `MaxTrustedDevices` se libera solo. Hoy, con el límite en 2, llenar los dos slots requiere un admin para destrabar; los slots no se llenan por uso simultáneo sino por **acumulación** de aparatos viejos, y esto lo ataca en la causa.
      **Alcance de la caducidad**: obliga a rehacer el 2FA en ese dispositivo. NO corta la sesión viva ni bloquea el acceso — eso es T067 (revocación) y T068 (deshabilitar), que son inmediatos. La caducidad es la **red de seguridad** para lo que nadie se acordó de revocar.
      Tests: un dispositivo vencido exige OTP de nuevo; vencer libera el slot; `RevokedAt` sigue siendo exclusivamente manual; un dispositivo vencido y otro revocado se distinguen en el listado de T067.
- [x] **T067** **Gestión y revocación de dispositivos y sesiones** (FR-007) — depende de T062, T065
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
      **Password spraying (abierto, de la segunda revisión de 3b)**: hoy una contraseña errada **no** incrementa `AccessFailedCount`; el único freno es el rate limit por IP, que un atacante distribuido esquiva probando una contraseña contra muchos emails. El lockout automático por intentos se reservó para esta tarea: DEBE contar fallos por cuenta, bloquear con `LockoutEnd` y responder el mismo `401` uniforme (sin revelar el bloqueo), y el contador DEBE incrementarse también por el camino del hash señuelo para no abrir una diferencia de tiempo.
      Tests: el deshabilitado no entra ni con dispositivo trusted; su sesión viva muere en el acto; no puede pedir reset; el último admin no puede autodeshabilitarse; rehabilitar no resucita dispositivos ni sesiones viejas; N contraseñas erradas bloquean la cuenta con la misma respuesta que una contraseña errada.
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
- [x] **T055a** `TenantMiddleware`: excepción para la **superficie sin autenticar** —
      `POST /api/auth/login` y `POST /api/auth/confirm-device` — con test de que la lista de
      exentas es **exactamente esa** y ninguna ruta más quedó afuera del middleware.
      `confirm-device` entra porque ocurre ANTES de que exista un token: el cliente no conoce su
      tenant y la respuesta del login no se lo dice, así que exigirle el header lo volvería
      inalcanzable. Las dos resuelven el tenant por la misma función acotada (T055b) —
      depende de T055
- [x] **T055b** Búsqueda del usuario por email en el login por la función SQL acotada
      `auth_find_user_by_email`, leyendo sólo lo necesario para autenticar y obtener el `TenantId`.
      Test que demuestre que por ese camino no se puede leer nada más de otro tenant. El
      filtro **NO DEBE** volverse permisivo cuando el `TenantContext` está vacío — depende de T055a
- [x] **T055c** Respuesta uniforme `401 AUTH_INVALID_CREDENTIALS` para email inexistente,
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
- [ ] **T071** **Logo de Stockma** (el de la plataforma, NO el del tenant) — depende de T039
      No confundir con T060: este es el logo del producto, el que se ve en el login genérico
      antes de saber a qué tenant entra el usuario. Es un asset del repo, no un archivo subido.
      Alcance: SVG en `frontend/web/src/assets/`, favicon, y los tamaños de icono que pide el
      manifest de la PWA (T046). Si sólo hay PNG, generar las medidas desde la más grande.
- [ ] **T043** Tests de componentes de `auth` e `inventory` — depende de T039, T040

## Fase 7 — Contracts + PWA (PR 7, depende de PR 6)

- [ ] **T044** `packages/contracts/openapi.json` real generado desde Api
- [ ] **T045** Configuración de orval → `api-client` TS generado — depende de T044
- [ ] **T046** `[P]` PWA manifest + `dotnet dev-certs` HTTPS documentado en el README
- [ ] **T047** CI: step de generación / verificación de contracts — depende de T045
- [ ] **T048** `[P]` Playwright e2e mínimo (scanner de barcode, manual/smoke)
- [ ] **T072** **Workflow de deploy con paso de migraciones** — depende de T047
      Hoy `Program.cs` **no** llama a `Migrate()` y nadie más lo hace: para levantar contra una
      base real hay que correr `dotnet ef database update` a mano. No está automatizado ni
      documentado.
      Decidido: las migraciones corren como **paso explícito del deploy**, no en el arranque de
      la API. `Migrate()` en el startup hace que dos instancias se peleen por el lock y permite
      que un deploy reescriba políticas de RLS sin que nadie las revise.
      **Regla que no se puede omitir**: el paso de migración NO DEBE usar `app_user`. Ese rol es
      **no propietario** a propósito — es lo que hace que la RLS le aplique (ver `InitialSchema`).
      El DDL necesita el rol dueño. Hacen falta **dos connection strings**: el propietario para
      migrar, `app_user` para el runtime. Si por comodidad se usa el dueño en runtime, PostgreSQL
      deja de aplicar las políticas al propietario y **la RLS se vuelve decorativa**.
      Un test o chequeo de arranque DEBERÍA verificar que la app no está conectada como
      propietario de las tablas. **Hecho en T079** (`RuntimeDatabaseRoleCheck`, connection strings
      `ConnectionStrings:Postgres` / `ConnectionStrings__PostgresMigrations`); queda el workflow.
      `[PENDIENTE: hosting no elegido; el workflow depende de dónde se despliegue]`

---

## Trazabilidad requerimiento → tareas

| Requerimiento | Tareas                                                             | PR   |
| ------------- | ------------------------------------------------------------------ | ---- |
| FR-001        | T009                                                               | PR 2 |
| FR-002        | T008, T010, T013                                                   | PR 2 |
| FR-003        | T011, T079, T084, T085, T086, T087, T089                           | PR 2 |
| FR-004        | T012                                                               | PR 2 |
| FR-005        | T015, T017, T019, T022, T062, T064, T068, T069, T083, T088         | PR 3 |
| FR-006        | T016, T017, T019, T021, T062, T063, T065                           | PR 3 |
| FR-007        | T015, T020, T067, T068, T070, T080                                 | PR 3 |
| FR-008        | T015, T018, T019, T021, T049, T050, T061                                 | PR 3 |
| FR-009        | T023, T025, T029                                                   | PR 4 |
| FR-010        | T023, T025, T029                                                   | PR 4 |
| FR-011        | T026, T027, T030                                                   | PR 4 |
| FR-012        | T031, T033, T038                                                   | PR 5 |
| FR-013        | T033, T037, T066                                                   | PR 5 |
| FR-014        | T031, T032, T037, T066                                             | PR 5 |
| FR-015        | T031, T034, T037                                                   | PR 5 |
| NFR-001       | T014, T079                                                         | PR 2 |
| NFR-002       | T009                                                               | PR 2 |
| NFR-003       | T011, T079, T084, T086, T089                                       | PR 2 |
| NFR-004       | T016, T018, T049, T082                                             | PR 3 |
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
- **Revisión adversarial al cerrar cada slice**: antes del commit, una revisión de contexto fresco (sin el contexto de quien implementó) busca activamente romper la seguridad y el contrato. Sus hallazgos bloqueantes se cierran en el mismo slice. Una suite en verde prueba que el código hace lo especificado, no que la especificación sea completa.
- **Criterios de seguridad explícitos en toda tarea que exponga un endpoint**: quién puede llamarlo (anónimo, `Member`, `TenantAdmin`, admin de plataforma), de qué tenant, y qué responde si no cumple. Cada criterio DEBE tener su test. Lo que no está escrito no se testea.
- `strict_tdd: false` según `.specify/config.yaml` — modo estándar; los tests de cada fase DEBE entrar en el mismo PR que su comportamiento.
- **T065 — completo**: 2a (spec), 2b (entidad + migración + `auth_find_refresh_token_by_hash`), 2c (cookie en `login`/`confirm-device` + `POST /refresh` con rotación, inactividad y tope + `POST /logout`) y 2d (hook `useSessionRenewer` en el frontend: renovación por actividad, serializada entre pestañas con Web Locks API, cierre de sesión a los 30 min de inactividad). Verificado: 526 tests backend + 82 tests frontend en verde.
