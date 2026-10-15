# Contrato: Auth API

**Base**: `/api/auth`
**Requerimientos**: FR-005 … FR-008 · NFR-004, NFR-005
**Fuentes**: [`../spec.md`](../spec.md) · [`../plan.md`](../plan.md) (sección 5) · [`../data-model.md`](../data-model.md)

---

## Reglas transversales

| Regla | Detalle |
|---|---|
| Header de tenant | `X-Tenant-ID: {guid}` DEBE estar presente en todas las rutas **salvo la superficie sin autenticar**: `POST /api/auth/login`, `POST /api/auth/confirm-device` (T055, T055a), `POST /api/auth/refresh` y `POST /api/auth/logout` (T065), que resuelven el tenant desde el email o desde el refresh token, nunca desde un header. Ausente o no parseable → `400` `TENANT_HEADER_MISSING` / `TENANT_HEADER_INVALID` (FR-001), en `application/problem+json` como el resto de los errores (T081) |
| Autenticación por defecto (T073) | Toda ruta que no sea `login`/`confirm-device`/`refresh`/`logout` exige un JWT válido (`FallbackPolicy.RequireAuthenticatedUser()`). Sin token → `401` |
| Tenant atado al token (T073) | Con JWT presente, el `X-Tenant-ID` DEBE coincidir con el claim `tid`. Si no coincide → `403` `TENANT_MISMATCH`, antes de tocar el controller o la base |
| Por qué son DOS rutas | `confirm-device` ocurre **antes** de que exista un token: el cliente todavía no sabe a qué tenant pertenece, y la respuesta del login (`requiresDeviceConfirmation`) no se lo dice. Exigirle el header lo volvería inalcanzable. Las dos rutas resuelven el tenant desde el email por la **misma** función acotada `auth_find_user_by_email` (T055b) |
| Header de dispositivo | `deviceId` es **obligatorio en las dos rutas** y viaja en el **body**, no en un header. Motivo: el OTP se persiste atado a un `deviceId` (`device_otps`), así que sin él no hay dónde clavarlo ni forma de decidir si el dispositivo es confiable — pedirle al usuario un código que nunca se envió es un callejón. `400` `VALIDATION_FAILED` si falta. **Normalización y entropía (T078)**: el servidor recorta espacios en un único lugar (`DeviceIdentifier`) y usa el valor normalizado para emitir, consumir y confiar. Después de recortar DEBE tener entre **16 y 128** caracteres (un GUID sirve); si no, `400` `VALIDATION_FAILED`, antes de mirar credenciales. La seguridad del dispositivo descansa en la entropía del `deviceId` + contraseña + OTP (ADR-016) |
| Huella (`fingerprint`) | Obligatoria en `confirm-device` y de **1 a 256** caracteres después de recortar espacios (el largo de la columna); si no, `400` `VALIDATION_FAILED` **antes** de mirar el email o tocar el OTP (T080). Se guarda en el `TrustedDevice`, pero es **informativa**: NO se compara en logins posteriores y NO es un control de seguridad — las huellas cambian con cada actualización del navegador (ADR-016) |
| Formato de error | `application/problem+json` (`ProblemDetails`) con extensión `errorCode`. Un body que no pasa la validación (campo obligatorio ausente, `deviceId` corto, contraseña que Identity rechaza) responde `400` `VALIDATION_FAILED`, nunca `500` (T078) |
| Rate limiting | La superficie de `auth` limita a 5 intentos por IP por minuto → `429` `AUTH_RATE_LIMITED` en `problem+json` (NFR-005, T078) |
| IP del cliente detrás de un proxy (T078) | La IP que particiona el rate limit sale de `X-Forwarded-For` **sólo** si el par inmediato está en `ForwardedHeaders:KnownProxies` o `ForwardedHeaders:KnownNetworks` (CIDR). Ambas vacías por defecto: sin configurarlas, `X-Forwarded-For` se ignora y un cliente no puede inventarse una cuota nueva por request (ADR-016). `UseForwardedHeaders()` es el **primer** middleware, antes de la redirección a HTTPS: detrás de un proxy conocido con `X-Forwarded-Proto: https` no hay redirección (T082). Una entrada que no parsea corta el **arranque** de la API con un mensaje que nombra la clave (T082) |
| Hash de contraseña | PBKDF2 vía ASP.NET Core Identity (NFR-004) |
| Vigencia del JWT | `exp` de **15 minutos** por defecto (`Jwt:ExpiresMinutes`), dentro del tope de 60 de NFR-004; claims `sub` (userId) y `tid` (tenantId) (FR-006). Tiene que ser más corto que la inactividad de la sesión: si durara más que ella, el corte por inactividad no cortaría nada (ADR-019). `Jwt:Key` de menos de 32 bytes, `Jwt:Issuer`/`Jwt:Audience` vacíos o `Jwt:ExpiresMinutes` fuera de 1–15 cortan el **arranque**, no la primera request (T082, ADR-019) |
| Canal del OTP | **SMS** al `ApplicationUser.PhoneNumber`. Ventana de vigencia **≤ 10 min** (NFR-004) — se mantiene sin cambios respecto del canal anterior |
| Límites del OTP (T075) | Emitir un OTP nuevo **invalida** los anteriores vivos de ese usuario+dispositivo; cada intento se compara sólo contra el **último** vigente. `Otp:MaxAttempts` (def. 5) intentos fallidos lo **queman**. `Otp:MaxIssuesPerWindow` (def. 5) emisiones por usuario en `Otp:IssueWindowMinutes` (def. 15, ventana deslizante): pasado el tope el login responde **exactamente igual** pero no manda SMS (ADR-016) |
| Sender de SMS (T077) | El sender de consola (escribe código y celular en el log) sólo se registra en `Development`. En cualquier otro entorno sin proveedor real (T049) la API **no arranca** y dice por qué. Nunca se cae en silencio al sender de consola |
| Origen del `PhoneNumber` | **Alta**: sólo un admin vía `PUT /api/admin/users/{userId}/phone-number` (T050). **Cambio**: el propio usuario vía `PUT /api/auth/phone-number`, confirmando un OTP enviado al número **actual** (T061). La superficie self-service de Identity DEBE seguir cerrada |

### Forma del error

```json
{
  "type": "https://stockma.co/errors/auth-invalid-credentials",
  "title": "Credenciales inválidas",
  "status": 401,
  "errorCode": "AUTH_INVALID_CREDENTIALS",
  "detail": "Email o contraseña incorrectos."
}
```

---

## `POST /api/auth/register` — FR-005

> **Cerrado por T064.** No es una ruta pública: exige un JWT de `TenantAdmin` **del mismo tenant**
> del header `X-Tenant-ID`. Antes de T064 cualquiera que conociera un `X-Tenant-ID` (no es secreto)
> podía registrarse a sí mismo como `TenantAdmin` y tomar el tenant. Ver ADR-014.

Registra un `ApplicationUser` asociado al `TenantId` del header. El `TenantAdmin` que da de alta
puede fijar el `role` del nuevo usuario (`TenantAdmin` o `Member`; por defecto `Member`).

**Command**: `RegisterUserCommand`
**Autorización**: `[Authorize(Policy = AuthorizationPolicies.TenantAdmin)]`. La verificación de que
el claim `tid` del JWT coincide con el tenant resuelto desde `X-Tenant-ID` la hace ahora el
`TenantMiddleware` para **toda** ruta autenticada (T073), no un chequeo ad hoc del controller — ver
ADR-015. Si no coincide, `403 TENANT_MISMATCH` **antes** de tocar el comando — no se crea usuario.

### Request

```http
POST /api/auth/register
X-Tenant-ID: 6f2c1b3a-8e41-4f2d-9c7a-1d5e8b0a3f77
Authorization: Bearer {jwt de un TenantAdmin de ese mismo tenant}
Content-Type: application/json
```

```json
{
  "email": "farmacia@ejemplo.co",
  "password": "S3gura#2026",
  "phoneNumber": "+573001234567",
  "role": "Member"
}
```

`phoneNumber` es **obligatorio** (T050/T064): un usuario creado sin celular no puede entrar desde
un dispositivo no confiable y queda inservible hasta que un admin lo complete.

### Respuesta `201 Created`

```json
{
  "userId": "9a1e4c2f-77b3-4a0e-b1d8-5c2f6e9a0d31",
  "email": "farmacia@ejemplo.co"
}
```

### Errores

| Status | `errorCode` | Cuándo |
|---|---|---|
| `401` | — | Sin JWT, o JWT inválido/expirado |
| `403` | — | JWT válido pero rol `Member` |
| `403` | `TENANT_MISMATCH` | `TenantAdmin` de OTRO tenant: el `tid` del JWT no coincide con `X-Tenant-ID` (rechazado por `TenantMiddleware`, T073). En ambos casos de `403` no se crea ningún usuario |
| `400` | `TENANT_HEADER_MISSING` / `TENANT_HEADER_INVALID` | Header `X-Tenant-ID` ausente o no es un GUID (FR-001) |
| `400` | `VALIDATION_FAILED` | Email, contraseña o celular no cumplen las reglas de Identity |
| `409` | `AUTH_EMAIL_DUPLICATE` | El email ya existe en **cualquier** tenant de la plataforma (FR-005, T055). El email es único global |

### Escenarios (Dado/Cuando/Entonces)

**Registro exitoso**
- **DADO** un `TenantAdmin` autenticado del mismo tenant del header, con email/password/celular válidos
- **CUANDO** `POST /api/auth/register`
- **ENTONCES** crea `ApplicationUser` con `TenantId` y `PhoneNumber`, y retorna `201`

**Sin token**
- **DADO** ninguna `Authorization`
- **CUANDO** `POST /api/auth/register`
- **ENTONCES** responde `401`

**Token de `Member`**
- **DADO** un JWT válido con rol `Member`
- **CUANDO** `POST /api/auth/register`
- **ENTONCES** responde `403` (un `Member` puede leer/ajustar stock pero no dar de alta usuarios)

**`TenantAdmin` de otro tenant**
- **DADO** un JWT de `TenantAdmin` cuyo `tid` es distinto del tenant resuelto por `X-Tenant-ID`
- **CUANDO** `POST /api/auth/register`
- **ENTONCES** responde `403` y no se crea ningún usuario en ningún tenant

**Email duplicado**
- **DADO** email ya registrado en la plataforma
- **CUANDO** registrar
- **ENTONCES** responde `409 Conflict`

### Bootstrap del primer `TenantAdmin`

Hasta que exista T063 (admin de plataforma), el primer `TenantAdmin` de un tenant se crea con un
subcomando CLI de `Stockma.Api`, fuera de HTTP:

```
dotnet run --project backend/src/Stockma.Api -- bootstrap-admin --tenant <guid> --email <email> --phone <telefono>
```

La contraseña se lee de la variable de entorno `STOCKMA_BOOTSTRAP_PASSWORD` (nunca de argv). Sólo
corre sobre un tenant **existente** y con **cero usuarios**; si el tenant no existe o ya tiene
usuarios, falla con un mensaje claro y código de salida distinto de cero, sin tocar la base. Ver
ADR-014.

El chequeo de "cero usuarios" y el alta corren bajo un `pg_advisory_xact_lock` por tenant: dos
bootstraps en paralelo dejan **exactamente un** `TenantAdmin` y el otro falla como "ya tiene
usuarios". El usuario y su rol se crean en **una** transacción: si asignar el rol falla, no queda un
usuario sin rol que trabe el bootstrap para siempre (T083, ADR-017).

---

## `POST /api/auth/login` — FR-006, FR-008

> **Excepción al header de tenant (T055).** Esta es la única ruta **exenta** del
> `TenantMiddleware`: el login es genérico (`stockma.app/login`, sin tenant en la URL),
> así que el cliente no tiene el GUID para mandar. El tenant se resuelve **desde el
> email**, que es único en toda la plataforma.
>
> La búsqueda del usuario por email DEBE usar `IgnoreQueryFilters()` de forma explícita
> y leer únicamente lo necesario para autenticar y obtener el `TenantId`. Es el único
> punto del sistema que cruza la frontera entre tenants a propósito.

Emite un JWT si el dispositivo es confiable; si no, dispara el 2FA por SMS.

> El OTP viaja por **SMS** al `ApplicationUser.PhoneNumber` del usuario que se loguea. El `email` del body sigue siendo la **credencial de identificación**, no el canal del segundo factor.

**Command**: `LoginCommand`

### Request

```http
POST /api/auth/login
Content-Type: application/json
```

Sin `X-Tenant-ID`: el tenant sale del email. El `deviceId` viaja en el body (ver reglas transversales).

```json
{
  "email": "farmacia@ejemplo.co",
  "password": "S3gura#2026",
  "deviceId": "web-chrome-a91f2c77"
}
```

### Respuesta `200 OK` — dispositivo confiable

```json
{
  "accessToken": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...",
  "expiresIn": 3600
}
```

`expiresIn` en segundos: `900` con la configuración por defecto (ADR-019).

> **2FA por tenant (T090)**: si `tenant_settings.require_second_factor = false`, `login` devuelve
> este mismo `200` con `accessToken` **tanto para dispositivo conocido como desconocido**, sin
> `requiresDeviceConfirmation`, sin emitir OTP y sin consultar `TrustedDevices`. El flag es del
> **tenant**, no del usuario, y se evalúa **después** de resolver el tenant desde el email.
> Default `true` en las tres capas —dominio, migración y provider— y también cuando **no existe**
> fila de `tenant_settings` (fail-closed). La apagada es una decisión del ambiente, no una
> condición del request: IP, user-agent y query params no se usan como señal porque cualquiera
> los fabrica.

### Respuesta `200 OK` — dispositivo desconocido (FR-008)

```json
{
  "requiresDeviceConfirmation": true
}
```

El sistema genera un OTP (hash con `IPasswordHasher`, expira en ≤ 10 min, persistido en `DeviceOtp`) y lo encola por **SMS** al `PhoneNumber` del usuario vía `ISmsSender`.

> `[PENDIENTE: las fuentes no definen si esta respuesta es 200 o 202, ni si incluye el número de celular enmascarado o un tiempo de expiración para la UI]`

> **Usuario sin `PhoneNumber` cargado**: el acceso DEBE bloquearse con `403 AUTH_PHONE_NOT_ENROLLED`, indicando que un admin del tenant debe cargar el número. NO DEBE permitirse el ingreso salteando el 2FA ni ofrecerle al usuario enrolar el suyo (T050, T061).

> **Proveedor y fallo de envío (T049)**: el canal real es **Twilio** (`Sms:Provider = "twilio"`, con
> `Sms { AccountSid, ApiKey, Sender }`). Fuera de `Development` la API **no arranca** sin proveedor y
> credenciales completas (ADR-016, T077); en `Development` se registra el sender de consola salvo que
> `Sms:Provider` esté cargado, y entonces también se mandan SMS reales.
>
> El envío se reintenta hasta `Sms:RetryCount` veces (def. `2`) con backoff, sólo ante errores
> transitorios (`429`, `408`, `5xx` o timeout). Un `4xx` de Twilio —número inválido, cuenta sin
> crédito— **no** se reintenta: su detalle queda en el log.
>
> **Si el proveedor falla igual, la respuesta NO cambia**: sigue siendo `200`
> `requiresDeviceConfirmation: true`, idéntica a la de un envío real, y el fallo queda en el log como
> `error`. Devolver `502`/`503` haría que, durante una caída del proveedor, los emails existentes
> respondieran distinto que los inexistentes: un oráculo de enumeración, que es exactamente lo que
> ADR-016 prohíbe para el tope de emisión.

### Errores

| Status | `errorCode` | Cuándo |
|---|---|---|
| `400` | `VALIDATION_FAILED` | Falta `email`, `password` o `deviceId`, o el `deviceId` tiene menos de 16 caracteres. Se decide antes de mirar credenciales: es el mismo `400` para cualquier email (T078) |
| `401` | `AUTH_INVALID_CREDENTIALS` | Email inexistente **o** contraseña incorrecta. La respuesta DEBE ser idéntica en los tres casos —email que no existe, contraseña equivocada, usuario de otro tenant— porque distinguirlos habilita enumerar qué correos usan Stockma (FR-006). También en **tiempo**: todo camino de falla (email inexistente, usuario sin contraseña, usuario bloqueado) corre una verificación PBKDF2 contra un hash señuelo (T074, ADR-016). Tras `Lockout:MaxFailedAccessAttempts` (5) contraseñas erradas —contadas **por cuenta**, no por IP (T068)— la cuenta se bloquea con `LockoutEnd` y la respuesta sigue siendo este mismo `401`: dentro del bloqueo ni la contraseña correcta entra, y el bloqueo no se distingue de un error cualquiera |
| `429` | `AUTH_RATE_LIMITED` | Más de 5 intentos por IP por minuto (NFR-005). Cuerpo `problem+json` |

> **Tope de emisión (T075)**: pasado `Otp:MaxIssuesPerWindow` en la ventana, la respuesta sigue siendo `200` `requiresDeviceConfirmation: true` — idéntica a la de un envío real — pero no se manda SMS. Un `429` o un error distinto revelaría que el email existe.

> **Cooldown de re-emisión (idempotencia)**: si el **mismo dispositivo** recibió un código hace menos de `Otp:ReissueCooldownMinutes` (def. `2`), el login no emite otro ni invalida el vigente — responde el mismo `200` `requiresDeviceConfirmation: true`, sin SMS. Un doble click produce el mismo efecto que un click: 1 código, 1 SMS. El chequeo corre **bajo el mismo candado** que la emisión, junto al tope por ventana (antes de descontarlo). Sólo aplica al scope de dispositivo de login: cambio de número (T061, `409` pendiente) y restablecimiento (T069) tienen su propia regla.

### Escenarios (Dado/Cuando/Entonces)

**Dispositivo confiable**
- **DADO** credenciales válidas desde dispositivo conocido
- **CUANDO** `POST /api/auth/login`
- **ENTONCES** retorna `accessToken` JWT

**Credenciales inválidas**
- **DADO** email/contraseña incorrectos
- **CUANDO** `POST /api/auth/login`
- **ENTONCES** responde `401` sin revelar cuál factor falló

**Login desde dispositivo nuevo**
- **DADO** credenciales válidas desde dispositivo desconocido
- **CUANDO** `POST /api/auth/login`
- **ENTONCES** responde `requiresDeviceConfirmation` y envía el OTP por SMS al `PhoneNumber` del usuario

**Tenant sin segundo factor (T090)**
- **DADO** un tenant con `require_second_factor = false` y credenciales válidas
- **CUANDO** `POST /api/auth/login` desde un dispositivo desconocido
- **ENTONCES** retorna `accessToken` y `refreshToken` sin `requiresDeviceConfirmation`, sin emitir SMS y sin tocar los dispositivos confiables

**Doble click en el login (idempotencia)**
- **DADO** credenciales válidas desde un dispositivo desconocido, y un código ya emitido hace menos de 2 minutos
- **CUANDO** `POST /api/auth/login` otra vez con el mismo `deviceId`
- **ENTONCES** responde byte a byte igual que la primera vez, sin emitir otro SMS y sin invalidar el código vigente

**Reintento pasado el cooldown**
- **DADO** credenciales válidas desde un dispositivo desconocido, y un código emitido hace 2 minutos o más
- **CUANDO** `POST /api/auth/login` otra vez con el mismo `deviceId`
- **ENTONCES** emite un código nuevo por SMS e invalida el anterior (sólo el último vale)

---

## `POST /api/auth/confirm-device` — FR-007, FR-008

Valida el OTP recibido por **SMS**, marca el dispositivo como confiable y emite el JWT.

> El `email` del body identifica al usuario; el OTP le llegó por SMS a su `PhoneNumber`.

**Command**: `ConfirmDeviceCommand`

### Request

```http
POST /api/auth/confirm-device
Content-Type: application/json
```

Sin `X-Tenant-ID`: igual que el login, el tenant sale del email (T055a).

```json
{
  "email": "farmacia@ejemplo.co",
  "deviceId": "web-chrome-a91f2c77",
  "fingerprint": "chrome-128-win11",
  "otp": "482913"
}
```

### Respuesta `200 OK`

```json
{
  "accessToken": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...",
  "expiresIn": 3600,
  "deviceTrusted": true
}
```

> `[PENDIENTE: el campo deviceTrusted no está en las fuentes; se propone para que el cliente pueda avisar "este dispositivo no quedó recordado, vas a necesitar el código en cada ingreso". Confirmar si forma parte del contrato]`

**Efectos — dos, separables**:

| # | Efecto | Condición |
|---|---|---|
| a | Valida el OTP y emite el JWT | **Siempre**, haya slot libre o no. El consumo del OTP, el alta del `TrustedDevice` y la emisión del JWT son **una** transacción: si algo falla después de aceptar el código, el OTP **no** queda gastado y el usuario puede reintentar con el mismo (T080) |
| b | Crea el `TrustedDevice` (`TrustedAt = now`, `RevokedAt = null`) y registra el evento de notificación al dispositivo trusted previo | **Sólo si** los `TrustedDevice` activos (`RevokedAt IS NULL`) del usuario son `< MaxTrustedDevices` (def. 2) |

- Sin slot libre: el efecto (a) ocurre igual — el usuario **entra**. El efecto (b) se omite: `deviceTrusted: false`, el dispositivo no queda recordado y deberá hacer OTP en **cada** login.
- El sistema NO DEBE revocar ningún `TrustedDevice` para hacer lugar. `RevokedAt` se setea SÓLO por acción manual de un admin (FR-007).
- La notificación real (SignalR / email al dispositivo anterior) queda **fuera de alcance** de este slice: sólo se registra el evento. Esa notificación es un aviso al dispositivo trusted previo, **no** el canal del segundo factor.

### Errores

| Status | `errorCode` | Cuándo |
|---|---|---|
| `400` | `VALIDATION_FAILED` | Falta `email`, `otp` o `fingerprint`, el `fingerprint` supera 256 caracteres, o el `deviceId` no cumple la regla de entropía (T078, T080) |
| `401` | `AUTH_OTP_REJECTED` | **Toda** falla del OTP, con el mismo cuerpo byte a byte: email inexistente, usuario bloqueado o deshabilitado (`LockoutEnd` futuro, misma regla que el login; no toca el OTP), ningún OTP vigente, código incorrecto, vencido (> 10 min), quemado por intentos, o perdedor de una carrera contra otro `confirm-device` con el mismo código (T074, T075, T080, ADR-016) |
| `429` | `AUTH_RATE_LIMITED` | Excedido el límite de `auth` (NFR-005) |

**Registro del intento (FR-008)**: cada código incorrecto incrementa `DeviceOtp.FailedAttempts` (persistido) y deja un log estructurado `Warning` con `UserId`, intentos y si el código quedó quemado — **nunca** el código ni el celular.

> **No hay error por límite de dispositivos.** `MaxTrustedDevices` NO DEBE producir un status de error: superarlo devuelve `200` con `deviceTrusted: false`. Ver la sección de abajo.

> **Resuelto (T074)**: un único `AUTH_OTP_REJECTED`. Separar `AUTH_OTP_INVALID` / `AUTH_OTP_EXPIRED` —o responder `AUTH_INVALID_CREDENTIALS` para el email inexistente— dice si el email existe y si hay un OTP en vuelo. Ver ADR-016.

---

## `POST /api/auth/refresh` — FR-006, NFR-004 (T065)

Renueva el access token usando el refresh token de la cookie. Rota el refresh: el presentado queda
consumido y se emite uno nuevo dentro de la **misma familia**.

**Quién puede llamarlo**: anónimo —justamente el access token puede estar vencido— pero sólo con una
cookie `stockma_refresh` válida. **Sin `X-Tenant-ID`**: el tenant y el usuario salen de la fila del
refresh token, resuelta por una función acotada propiedad de `auth_lookup`, igual que el login (T055b,
ADR-017). Un header de tenant presente se ignora; nunca decide a qué tenant se renueva.

### Request

```http
POST /api/auth/refresh
Content-Type: application/json
Cookie: stockma_refresh=<token opaco>
```

```json
{
  "deviceId": "3f9a2c1e-7b4d-4e8a-9c21-5d6e7f8a9b0c"
}
```

### Respuesta `200 OK`

```http
Set-Cookie: stockma_refresh=<token nuevo>; HttpOnly; Secure; SameSite=Strict; Path=/api/auth
```

```json
{
  "accessToken": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...",
  "expiresIn": 900
}
```

### Errores

| Status | `errorCode` | Cuándo |
|---|---|---|
| `400` | `VALIDATION_FAILED` | `deviceId` ausente o fuera de la regla de entropía (T078) |
| `401` | `AUTH_REFRESH_REJECTED` | **Toda** falla, con el mismo cuerpo byte a byte y borrando la cookie (`Max-Age=0`): sin cookie, token desconocido, vencido por inactividad o por el tope absoluto, revocado, ya consumido (reuso), `deviceId` distinto del de la familia, usuario bloqueado o deshabilitado |
| `429` | `AUTH_RATE_LIMITED` | Excedido el límite de `auth` (NFR-005) |

### Reglas

| Regla | Detalle |
|---|---|
| Token | 32 bytes de un CSPRNG, codificado base64url. Opaco: no es un JWT |
| Persistencia | Se guarda `SHA-256(token)`, nunca el token. **No** `IPasswordHasher`: con salt aleatorio el mismo token da un hash distinto cada vez y la fila no se puede buscar. SHA-256 sin salt es seguro acá porque el token tiene 256 bits de entropía: no hay diccionario posible, a diferencia de una contraseña (ADR-018) |
| Rotación | Cada `refresh` exitoso marca `ConsumedAt` en el presentado y emite uno nuevo con el mismo `FamilyId`, en **una** transacción bajo lock por familia |
| Detección de reuso | Presentar un token con `ConsumedAt` o `RevokedAt` → se revoca **toda la familia** y se registra el evento (log `Warning` con `UserId` y `FamilyId`, nunca el token). La sesión se cae para el legítimo y para quien tenga la copia |
| Inactividad | Cada token vence `SessionIdleTimeoutMinutes` (def. 30, `TenantSettings`) después de **su** emisión. Como cada `refresh` emite un token nuevo, eso equivale a "30 minutos sin renovar, la sesión muere". Lo controla el servidor: no depende de que el navegador colabore (ADR-019 original; la enmienda T092 a 2 h quedó revocada) |
| Vigencia absoluta | La familia vence al momento del login + `RefreshTokenLifetimeHours` (def. 8, `TenantSettings`). Ningún token de la familia vence después de ese tope, por más que se renueve: la inactividad acorta la sesión, nunca la alarga |
| Cookie de sesión | La cookie **no** lleva `Max-Age` ni `Expires`: muere al cerrar el navegador. El vencimiento real lo decide la fila, no la cookie (ADR-019) |
| Atado al dispositivo | La familia guarda el `deviceId` normalizado del login. Un `refresh` con otro `deviceId` se trata como reuso: revoca la familia. Así un refresh no sirve para entrar desde un dispositivo nuevo sin pasar por el 2FA |
| Usuario bloqueado | Con `LockoutEnd` futuro, `401` y se revoca la familia (misma regla que el login; T068 la usa para el offboarding) |
| CSRF | `SameSite=Strict` impide que otro sitio mande la cookie; además se exige `Content-Type: application/json`, que fuerza un preflight CORS en un pedido cross-origin |
| `Path=/api/auth` | La cookie sólo viaja a la superficie de auth, no a cada request de negocio |
| Renovación en el frontend | El frontend renueva cuando al access token le quedan menos de 2 minutos **y** hubo actividad del usuario (puntero o teclado) desde la última renovación. Sin actividad no renueva, y la sesión muere sola en el servidor. Además guarda `lastActivityAt` y a los 30 minutos sin actividad cierra la sesión en pantalla, también al volver a abrir la app: el servidor es la barrera, el front es la experiencia exacta (ADR-019) |
| Varias pestañas | Dos `refresh` en paralelo con el **mismo** token son, para el servidor, un reuso: el segundo revoca la familia. El frontend DEBE serializar la renovación entre pestañas (Web Locks API). Es a propósito: una ventana de gracia afloja justo la detección que se quiere tener |

### Escenarios (Dado/Cuando/Entonces)

**Rotación**
- **DADO** un refresh vigente y sin consumir
- **CUANDO** `POST /api/auth/refresh` con el mismo `deviceId` del login
- **ENTONCES** responde `200` con un access token nuevo, rota la cookie y el token anterior queda consumido

**Reuso**
- **DADO** un refresh ya consumido
- **CUANDO** se presenta de nuevo
- **ENTONCES** responde `401 AUTH_REFRESH_REJECTED` y **toda** la familia queda revocada: el último token emitido tampoco sirve

**Otro dispositivo**
- **DADO** un refresh emitido para el dispositivo A
- **CUANDO** se presenta con el `deviceId` del dispositivo B
- **ENTONCES** responde `401` y revoca la familia

**Inactividad**
- **DADO** un refresh emitido hace más de `SessionIdleTimeoutMinutes`
- **CUANDO** se presenta, aunque la familia esté dentro de su tope absoluto
- **ENTONCES** responde `401 AUTH_REFRESH_REJECTED`

**Renovar no pasa el tope**
- **DADO** una familia a 10 minutos de su vencimiento absoluto
- **CUANDO** se renueva
- **ENTONCES** el token nuevo vence en 10 minutos, no en `SessionIdleTimeoutMinutes`

**Vencido**
- **DADO** una familia cuyo login fue hace más de `RefreshTokenLifetimeHours`
- **CUANDO** se presenta cualquiera de sus tokens
- **ENTONCES** responde `401`, aunque el token se haya rotado hace minutos

**Aislamiento**
- **DADO** un refresh del tenant A
- **CUANDO** se presenta con `X-Tenant-ID` del tenant B
- **ENTONCES** el access token emitido es del tenant A: el header no decide el tenant

### Emisión en `login` y `confirm-device`

Toda respuesta `200` que entrega un `accessToken` —`login` desde un dispositivo confiable y
`confirm-device`— DEBE también abrir una familia nueva y devolver el `Set-Cookie` de `stockma_refresh`.
Un login nunca reutiliza una familia existente.

---

## `POST /api/auth/logout` — FR-006 (T065)

Cierra la sesión **del lado del servidor**: revoca la familia de la cookie y la borra del cliente.

**Quién puede llamarlo**: anónimo, con o sin cookie —un access token vencido no debe impedir cerrar
sesión—. Sin `X-Tenant-ID`.

### Request

```http
POST /api/auth/logout
Content-Type: application/json
Cookie: stockma_refresh=<token opaco>
```

### Respuesta `204 No Content`

```http
Set-Cookie: stockma_refresh=; HttpOnly; Secure; SameSite=Strict; Path=/api/auth; Max-Age=0
```

**Siempre `204`**: sin cookie, con un token desconocido, vencido o ya revocado. Es idempotente y no
revela si la cookie era válida.

### Reglas

- Revoca **toda** la familia (`RevokedAt` en cada token vivo), no sólo el token presentado.
- **Consecuencia aceptada**: el access token ya emitido sigue siendo válido hasta su `exp` (15 min por defecto, ADR-019). El JWT no se consulta contra la base en cada request; revocarlo al instante exigiría esa consulta. El frontend lo descarta de `localStorage` al hacer logout.

---

## `PUT /api/admin/users/{userId}/phone-number` — FR-008

Único camino para cargar o cambiar el `PhoneNumber` que recibe el OTP por SMS. **Sólo un admin del tenant.**

**Command**: `SetUserPhoneNumberCommand`

### Request

```http
PUT /api/admin/users/9a1e4c2f-77b3-4a0e-b1d8-5c2f6e9a0d31/phone-number
X-Tenant-ID: 6f2c1b3a-8e41-4f2d-9c7a-1d5e8b0a3f77
Authorization: Bearer {jwt-de-admin}
Content-Type: application/json
```

```json
{
  "phoneNumber": "+573001234567"
}
```

### Respuesta `200 OK`

```json
{
  "userId": "9a1e4c2f-77b3-4a0e-b1d8-5c2f6e9a0d31",
  "phoneNumberMasked": "+57300*****67",
  "phoneNumberConfirmed": true
}
```

### Errores

| Status | `errorCode` | Cuándo |
|---|---|---|
| `400` | `TENANT_HEADER_MISSING` / `TENANT_HEADER_INVALID` | Header inválido |
| `400` | `VALIDATION_FAILED` | `phoneNumber` no cumple E.164: `^\+[1-9]\d{6,14}$`, sin espacios ni símbolos (`PhoneNumber.Parse`) |
| `401` | `AUTH_INVALID_CREDENTIALS` | JWT ausente o vencido |
| `403` | `AUTH_FORBIDDEN` | El llamante no es admin del tenant, o intenta modificar su propio número sin ser admin |
| `404` | `USER_NOT_FOUND` | El `userId` no existe **en ese tenant** |

### Reglas

- El usuario objetivo NO DEBE poder invocar este endpoint sobre sí mismo salvo que sea admin del tenant. Para cambiar el suyo tiene `PUT /api/auth/phone-number` (T061), que exige OTP al número actual.
- Identity expone `SetPhoneNumberAsync` / `ChangePhoneNumberAsync` y los endpoints self-service de Identity por defecto: esa superficie DEBE cerrarse explícitamente (ver T050). Dejarla abierta permitiría al usuario desviar su propio segundo factor.
- El cambio DEBE quedar auditado (`AuditSaveChangesInterceptor`) con el admin que lo ejecutó.
- **El cambio revoca TODO lo que dependía del número viejo** (decidido en T050): los `TrustedDevice` del usuario, todas sus familias de refresh (sesiones) y los OTP en vuelo. El número ES el segundo factor: cambiarlo sin cortar lo anterior dejaría el celular robado operando. El orden es fail-closed y no necesita transacción — primero dispositivos (que además dispara `USER_NOT_FOUND` si el usuario no existe), después sesiones, después OTP, recién entonces el número: la ventana "número nuevo con sesión u OTP viva" no existe. Un access token ya emitido sigue vivo hasta su `exp` (≤15 min, ADR-019), consecuencia aceptada.

### Escenarios (Dado/Cuando/Entonces)

**Admin carga el número**
- **DADO** un admin del tenant y un usuario sin `PhoneNumber`
- **CUANDO** `PUT /api/admin/users/{userId}/phone-number`
- **ENTONCES** responde `200`, persiste el `PhoneNumber` y el usuario ya puede recibir el OTP por SMS

**Usuario intenta cambiar su propio número**
- **DADO** un usuario no admin autenticado
- **CUANDO** intenta modificar su propio `PhoneNumber` (por este endpoint o por la superficie self-service de Identity)
- **ENTONCES** responde `403` y el `PhoneNumber` queda sin cambios

---

## `PUT /api/admin/users/{userId}/role` — FR-005 (T094)

Promueve (`Member` → `TenantAdmin`) o degrada (`TenantAdmin` → `Member`) a un usuario existente. **Sólo un admin del tenant.** Hoy `register` asigna rol sólo al crear: este endpoint es el único camino para cambiarlo después (rescate de admin, baja, reorganización).

**Command**: `SetUserRoleCommand`

### Request

```http
PUT /api/admin/users/9a1e4c2f-77b3-4a0e-b1d8-5c2f6e9a0d31/role
X-Tenant-ID: 6f2c1b3a-8e41-4f2d-9c7a-1d5e8b0a3f77
Authorization: Bearer {jwt-de-admin}
Content-Type: application/json
```

```json
{
  "role": "TenantAdmin"
}
```

### Respuesta `200 OK`

```json
{
  "userId": "9a1e4c2f-77b3-4a0e-b1d8-5c2f6e9a0d31",
  "role": "TenantAdmin"
}
```

### Errores

| Status | `errorCode` | Cuándo |
|---|---|---|
| `400` | `TENANT_HEADER_MISSING` / `TENANT_HEADER_INVALID` | Header inválido |
| `400` | `VALIDATION_FAILED` | `role` no es `TenantAdmin` ni `Member` |
| `400` | `VALIDATION_FAILED` | Degradar al **último** `TenantAdmin` vivo del tenant |
| `401` | `AUTH_INVALID_CREDENTIALS` | JWT ausente o vencido |
| `403` | `AUTH_FORBIDDEN` | El llamante no es admin del tenant |
| `404` | `USER_NOT_FOUND` | El `userId` no existe **en ese tenant** |

### Reglas

- Un usuario tiene UN rol (`TenantAdmin` o `Member`); `SetRoleAsync` garantiza el invariante: quita los roles viejos antes de asignar el nuevo.
- **El tenant nunca se queda sin admin**: degradar sólo pasa si queda OTRO `TenantAdmin` vivo (misma regla de T062). La validación corre ANTES de cualquier efecto — si se rechaza, no se tocó nada.
- **Degradar es fail-closed** (misma lógica que T050): primero se revocan todas las sesiones del usuario (familias de refresh) y sus dispositivos confiables, recién después se escribe el rol. Si algo falla a mitad de camino, la persona queda MENOS privilegiada, nunca más. El rol vive dentro del JWT: el token ya emitido conserva `role: TenantAdmin` hasta su `exp` (≤15 min, ADR-019), consecuencia aceptada — por eso además se mata la sesión, para que el re-login emita claims frescos con `Member`.
- **Promover no expulsa a nadie**: los roles se releen de la DB en cada rotación (`RefreshCommand` → `session.Roles`), así que el rol nuevo surte efecto en el próximo refresh o login (≤15 min con el token vivo).
- Idempotente: pedir el rol que ya tiene responde `200` sin efectos secundarios.
- El cambio DEBE quedar auditado (`AuditSaveChangesInterceptor`) con el admin que lo ejecutó.

### Escenarios (Dado/Cuando/Entonces)

**Promover a admin**
- **DADO** un admin del tenant, un `Member` con sesión activa
- **CUANDO** `PUT /api/admin/users/{userId}/role` con `"role": "TenantAdmin"`
- **ENTONCES** responde `200`, persiste el rol nuevo y la sesión del usuario sigue viva (el rol llega con el próximo refresh)

**Degradar al último admin**
- **DADO** un tenant con UN solo `TenantAdmin` (él es admin y llamante)
- **CUANDO** se intenta degradarlo a sí mismo
- **ENTONCES** responde `400 VALIDATION_FAILED` y el rol queda sin cambios

**Degradar un admin con respaldo**
- **DADO** un tenant con dos `TenantAdmin`; el objetivo tiene sesión y dispositivo confiable
- **CUANDO** el otro admin lo degrada a `Member`
- **ENTONCES** responde `200`, el rol pasa a `Member` y sus sesiones quedan revocadas

**Un Member intenta tocar roles**
- **DADO** un usuario `Member` autenticado
- **CUANDO** invoca el endpoint
- **ENTONCES** responde `403`

---

## `POST /api/admin/users/{userId}/disable` y `/enable` — T068

Offboarding real. T067 revoca dispositivos, pero **no alcanza para el empleado que se va**: conserva contraseña y celular, así que vuelve a entrar con OTP como si nada. Lo único que corta el acceso de verdad es deshabilitar la cuenta. Usa `LockoutEnabled`/`LockoutEnd` de ASP.NET Identity — no un flag nuevo, para no duplicar estado.

**Command**: `SetUserEnabledCommand` (T068)

```http
POST /api/admin/users/{userId}/disable
POST /api/admin/users/{userId}/enable
X-Tenant-ID: 6f2c1b3a-8e41-4f2d-9c7a-1d5e8b0a3f77
Authorization: Bearer {jwt-del-tenantadmin}
```

Sin body.

### Respuesta `200 OK`

```json
{ "userId": "9f0f2a64-…", "enabled": false }
```

### Errores

| Status | `errorCode` | Cuándo |
|---|---|---|
| `400` | `VALIDATION_FAILED` | Se intenta **deshabilitar al último `TenantAdmin`** del tenant: quedaría sin quien administre |
| `401` | `AUTH_INVALID_CREDENTIALS` | JWT ausente o vencido |
| `403` | `AUTH_FORBIDDEN` | El llamante no es `TenantAdmin` |
| `404` | `USER_NOT_FOUND` | El `userId` no existe en este tenant (cuentas aisladas, FR-002) |
| `429` | `RATE_LIMITED` | Excedido el límite de `auth` (NFR-005) |

### Reglas

- **Deshabilitar es un solo acto, fail-closed** (misma regla que T061), en este orden:
  1. revocar **todos** sus `TrustedDevice`;
  2. revocar **todas** sus familias de refresh;
  3. escribir `LockoutEnabled = true`, `LockoutEnd = infinito` y `AccessFailedCount = 0`.

  Si algo falla a mitad de camino, el peor caso es un usuario con las sesiones cortadas y la cuenta **todavía operativa** — nunca al revés. Un offboarding a medias no es un offboarding.
- **Login y refresh mueren en el acto**: `VerifyCredentialsAsync` y `FindByIdAsync` devuelven `null` con `LockoutEnd` futuro, así que el login responde el `401` uniforme (byte a byte igual a una contraseña errada) y el refresh revoca la familia borrando la cookie. Ni siquiera desde un dispositivo confiable ya sembrado entra.
- **El deshabilitado no opera sobre su propia cuenta**: no puede pedir cambio de número (`401 AUTH_ACCOUNT_DISABLED` en T061, chequeado después del lookup del número y antes de gastar el OTP) ni recuperación de contraseña (T069 usa `FindByEmailAsync`, que devuelve `null`), ni confirmar un OTP vigente en `confirm-device` (ya cubierto desde T080).
- **Último admin**: un `TenantAdmin` no puede deshabilitarse a sí mismo si es el único del tenant (mismo conteo por rol que T094). Un `Member` puede deshabilitarse sin reglas — sólo un admin llega a este endpoint.
- **Rehabilitar no resucita nada**: `enable` sólo setea `LockoutEnd = null` y limpia `AccessFailedCount`. Las sesiones y dispositivos viejos quedaron muertos en el disable; el usuario vuelve a entrar por el flujo completo de 2FA. Rehabilitar **no** revoca nada (lo viejo ya está muerto — nada que revocar).
- **Idempotente**: deshabilitar una cuenta ya deshabilitada, o rehabilitar una activa, responde `200` sin efectos secundarios.
- **Runbook de celular robado** (decidido 2026-10-09): deshabilitar → verificación por llamada → T050 con el número nuevo → rehabilitar. T050 opera sobre cuentas deshabilitadas **a propósito** (usa `SetPhoneNumberAsync`, sin lookups con lockout).

### Password spraying (cierre del hallazgo de la 2.ª revisión de 3b)

- Los fallos de contraseña se cuentan **por cuenta** (`AccessFailedCount`): el rate limit por IP sólo esquivable con un atacante distribuido que pruebe una contraseña contra miles de emails a la vez.
- Al llegar a `Lockout:MaxFailedAccessAttempts` (5) se setea `LockoutEnd = ahora + 5 min`. **Mientras el bloqueo esté activo no se cuentan intentos**: contarlo lo extendería y un atacante dejaría afuera al dueño para siempre (es lo que hace el `PreSignInCheck` de Identity).
- Un login exitoso pone el contador en cero: los fallos viejos no acumulan.
- La respuesta es siempre el mismo `401 AUTH_INVALID_CREDENTIALS`; el bloqueo no se delata en el cuerpo ni en el código.
- El hash señuelo sigue corriendo en **todos** los caminos de falla: una sola verificación PBKDF2 por camino (T074 intacto). Un email inexistente no tiene fila que contar — es imposible — y un usuario bloqueado tampoco se cuenta, por el DoS de arriba. Eso deja **un único** delta de tiempo residual: la escritura del conteo cuando la cuenta existe y tiene contraseña errada, inherente a la decisión de contar.

### Escenarios (Dado/Cuando/Entonces)

**Deshabilitar corta todo en un acto**
- **DADO** un usuario con sesión viva y dispositivo confiable sembrado
- **CUANDO** un `TenantAdmin` invoca `POST …/disable`
- **ENTONCES** responde `200`; sus familias de refresh y sus dispositivos quedan revocados, y su login responde `401 AUTH_INVALID_CREDENTIALS` — ni siquiera desde el dispositivo trusted

**Último admin**
- **DADO** un tenant con un único `TenantAdmin`
- **CUANDO** se deshabilita a sí mismo
- **ENTONCES** responde `400 VALIDATION_FAILED` y la cuenta queda intacta (sigue pudiendo entrar)

**Rehabilitar no resucita**
- **DADO** un usuario deshabilitado con sesiones y dispositivos revocados
- **CUANDO** se invoca `POST …/enable` y el usuario reintenta el login con su contraseña correcta
- **ENTONCES** el login responde `200` con `requiresDeviceConfirmation: true` (el 2FA arranca de cero) y en la DB **no** queda ninguna sesión vieja viva

**Un Member intenta deshabilitar**
- **DADO** un usuario `Member` autenticado
- **CUANDO** invoca el endpoint
- **ENTONCES** responde `403`

**El deshabilitado no toca su propio número**
- **DADO** un usuario deshabilitado con un JWT emitido antes del disable
- **CUANDO** invoca `PUT /api/auth/phone-number` o su confirm
- **ENTONCES** responde `401 AUTH_ACCOUNT_DISABLED` sin procesar nada

**Cinco contraseñas erradas (T068 — password spraying)**
- **DADO** un usuario con contraseña conocida y 4 intentos fallidos
- **CUANDO** falla el 5.º intento y después prueba la contraseña correcta
- **ENTONCES** ambos responden el mismo `401`, byte a byte idéntico: el bloqueo no se delata

---

## `PUT /api/auth/phone-number` — FR-008 (T061)

Diseñado y cerrado en T061 (2026-10-09). Decisiones del titular revisadas y aplicadas (2026-10-10): `409` sobre reemplazo silencioso, revocación total al confirmar, aviso al viejo, E.164, y validación de móvil del país del tenant.

Único camino para que un usuario cambie **su propio** `PhoneNumber`. Exige estar autenticado **y** confirmar un OTP enviado al número **actual**.

> **Alta ≠ cambio.** Enrolar el primer número con sólo la contraseña sería dejar que un factor se autoenrole: quien tuviera la contraseña decidiría a dónde llegan los OTP. Cambiar uno existente es seguro porque exige demostrar posesión del canal vigente — un atacante con la contraseña robada no tiene el celular viejo. Por eso el **alta** es `PUT /api/admin/users/{userId}/phone-number` (admin, T050) y el **cambio** es este endpoint.

**Commands**: `RequestPhoneNumberChangeCommand` / `ConfirmPhoneNumberChangeCommand` (T061)

### Paso 1 — Request del cambio

```http
PUT /api/auth/phone-number
X-Tenant-ID: 6f2c1b3a-8e41-4f2d-9c7a-1d5e8b0a3f77
Authorization: Bearer {jwt-del-usuario}
Content-Type: application/json
```

```json
{
  "newPhoneNumber": "+573009876543"
}
```

### Respuesta `202 Accepted`

El OTP se envió al número **actual**. El cambio **no** se aplicó todavía.

```json
{
  "otpSentToMasked": "+57300*****67",
  "expiresInSeconds": 600
}
```

### Paso 2 — Confirmación

```http
POST /api/auth/phone-number/confirm
Authorization: Bearer {jwt-del-usuario}
Content-Type: application/json
```

```json
{
  "otp": "483920"
}
```

### Respuesta `200 OK`

```json
{
  "phoneNumberMasked": "+57300*****43",
  "phoneNumberConfirmed": true
}
```

### Errores

| Status | `errorCode` | Cuándo |
|---|---|---|
| `400` | `VALIDATION_FAILED` | `newPhoneNumber` no es E.164 válido (`PhoneNumber.Parse`: `^\+[1-9]\d{6,14}$`, sin espacios ni símbolos) |
| `400` | `VALIDATION_FAILED` | `newPhoneNumber` no corresponde a un móvil del país del tenant (`CountryPhoneRules`: hoy Colombia, `^\+573\d{9}$`) |
| `400` | `VALIDATION_FAILED` | `newPhoneNumber` es el mismo que el actual: no hay nada que cambiar |
| `401` | `AUTH_INVALID_CREDENTIALS` | JWT ausente o vencido |
| `401` | `AUTH_ACCOUNT_DISABLED` | La cuenta existe pero está **deshabilitada** (T068). El chequeo corre después del lookup del número y antes de gastar el OTP: ni el request ni el confirm se procesan, y un JWT emitido antes del disable no sirve |
| `401` | `AUTH_OTP_REJECTED` | OTP errado o vencido en el paso 2, o no hay cambio pendiente |
| `403` | `AUTH_PHONE_NOT_ENROLLED` | El usuario **no tiene** `PhoneNumber` cargado: ese caso es alta por admin (T050), no cambio |
| `404` | `USER_NOT_FOUND` | El `sub` del token no corresponde a un usuario del tenant |
| `409` | `AUTH_PHONE_CHANGE_PENDING` | Ya hay un cambio en vuelo sin confirmar. El `detail` indica cuántos segundos faltan para que el pendiente venza |
| `429` | `RATE_LIMITED` | Excedido el límite de `auth` (NFR-005) |

### Reglas

- El OTP DEBE enviarse al `PhoneNumber` **vigente**. NUNCA al número nuevo — mandarlo al nuevo destruye la garantía: cualquiera con la contraseña se autoconfirmaría el cambio.
- **Regla del país del tenant (decisión del titular, T061)**: el número nuevo DEBE corresponder a un móvil del país del tenant (`tenant_settings.country_code`, ISO 3166-1 alpha-2, def. `CO`). Las reglas viven en `CountryPhoneRules` (Domain): sólo se valida **formato** (prefijo del país + longitud + prefijo móvil), no la existencia del número. El rechazo ocurre EN EL REQUEST, antes de gastar un SMS. Un tenant sin `country_code` o un país sin regla cargada degrada a exigir únicamente E.164.
- El `PhoneNumber` NO DEBE modificarse hasta que el OTP sea confirmado. Mientras tanto el número nuevo vive aparte (pendiente), no en `ApplicationUser`: se persiste en la fila del OTP (`device_otps.new_phone_number`), de modo que la vida del pendiente = la del código (10 min) y no queda estado huérfano.
- Los OTP de cambio viven en el alcance interno `internal:phone-change` (no se amarran a un dispositivo): el usuario puede pedir el cambio desde cualquiera, y los OTP de login y de cambio nunca se pisan.
- **Un solo cambio en vuelo — `409` (decisión del titular, T061)**: si ya hay un cambio pendiente, un nuevo pedido responde `409 AUTH_PHONE_CHANGE_PENDING` sin emitir otro código ni gastar un SMS. El pendiente original sigue vigente hasta que venza (10 min) o se confirme. El `detail` informa los segundos restantes para que el front pueda mostrar "espera a que venza el código anterior". El rechazo no relaja la tasa de emisión (NFR-005).
- Un usuario **sin** `PhoneNumber` cargado NO DEBE poder usar este endpoint (`403 AUTH_PHONE_NOT_ENROLLED`).
- **Confirmar revoca TODO — fail-closed (decisión del titular, T061)**: al confirmar el OTP se revocan TODAS las sesiones del usuario (refresh tokens) y TODOS sus dispositivos confiables, y se queman los OTPs restantes. La revocación ocurre ANTES de escribir el número y dentro de la misma transacción del consume: si algo falla a mitad, no se aplica el cambio. El titular se desloguea en todos lados y su próximo login exige OTP al número **nuevo**.
- **Aviso al número viejo (decisión T061)**: aplicado el cambio se envía un SMS best-effort al número anterior ("Tu número de Stockma fue cambiado..."). Si el envío falla, se loguea y el cambio NO se revierte — ya estaba confirmado con OTP.
- Aplicado el cambio, `PhoneNumberConfirmed` DEBE quedar en `true` y el cambio DEBE quedar auditado con el usuario que lo ejecutó.
- Rate limiting igual que el resto de `auth` (NFR-005).

### Escenarios (Dado/Cuando/Entonces)

**Cambio exitoso**
- **DADO** un usuario autenticado con `PhoneNumber` cargado
- **CUANDO** pide el cambio y confirma el OTP que le llegó al número **actual**
- **ENTONCES** responde `200` y el `PhoneNumber` queda actualizado

**El OTP viaja al número viejo, no al nuevo**
- **DADO** un usuario que pide cambiar su número
- **CUANDO** el sistema envía el OTP
- **ENTONCES** el destino DEBE ser el `PhoneNumber` vigente y NUNCA el `newPhoneNumber`

**OTP inválido o vencido**
- **DADO** un cambio en vuelo
- **CUANDO** confirma con un OTP errado o vencido
- **ENTONCES** responde `401` y el `PhoneNumber` queda **sin cambios**

**Usuario sin número previo**
- **DADO** un usuario autenticado sin `PhoneNumber` cargado
- **CUANDO** `PUT /api/auth/phone-number`
- **ENTONCES** responde `403 AUTH_PHONE_NOT_ENROLLED` y el número queda sin cambios

**Cambio en vuelo**
- **DADO** un cambio pendiente sin confirmar
- **CUANDO** el usuario pide OTRO cambio con un número distinto
- **ENTONCES** responde `409 AUTH_PHONE_CHANGE_PENDING`, no se emite código nuevo y el pendiente original sigue vivo

**Confirmar revoca todo**
- **DADO** un usuario con sesiones abiertas y dispositivos confiables que confirma su cambio de número
- **CUANDO** se aplica el nuevo número
- **ENTONCES** todas sus sesiones y sus dispositivos confiables quedan revocados, y los OTPs restantes del usuario quedan quemados

**Aviso al número viejo**
- **DADO** un cambio confirmado con OTP
- **CUANDO** se aplica el nuevo número
- **ENTONCES** se envía un SMS de aviso al número VIEJO; si el envío falla, el cambio se mantiene y el fallo queda en el log

**Mismo número**
- **DADO** un usuario con `PhoneNumber` cargado
- **CUANDO** pide cambiarlo a su propio número actual
- **ENTONCES** responde `400 VALIDATION_FAILED` sin emitir OTP

---

## `MaxTrustedDevices` — gobierna el 2FA, no el acceso

**Un único comportamiento.** `MaxTrustedDevices` (def. 2, configurable por tenant) limita **cuántos dispositivos pueden saltear el 2FA**. NUNCA limita el acceso.

| Aspecto | Regla |
|---|---|
| Acceso (FR-008) | CUALQUIER dispositivo entra vía OTP por SMS. El login NO DEBE bloquearse por límite de dispositivos, **siempre** |
| Privilegio de saltear el 2FA (FR-007) | Máximo `MaxTrustedDevices` dispositivos trusted activos por usuario |
| Al superar el máximo | `confirm-device` responde **`200`** con `accessToken` y `deviceTrusted: false`. No se crea el `TrustedDevice`; ningún dispositivo existente se revoca |
| Alta de un slot | Manual por un admin. `RevokedAt` se setea SÓLO por acción manual de un admin — **nadie es expulsado automáticamente** y no hay selección del "más antiguo" |
| Superficie extra requerida | Endpoints de gestión y revocación de dispositivos (T067) — ver abajo |

### Respuesta `200` sin slot libre

```json
{
  "accessToken": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...",
  "expiresIn": 3600,
  "deviceTrusted": false
}
```

El cliente PUEDE usar `deviceTrusted: false` para avisar que el dispositivo no quedó recordado y que se pedirá el código en cada ingreso.

### Escenarios (FR-007)

**Nuevo dispositivo con slot libre**
- **DADO** usuario con menos de `MaxTrustedDevices` dispositivos trusted activos
- **CUANDO** completa el OTP en `POST /api/auth/confirm-device`
- **ENTONCES** responde `200` con `accessToken` y `deviceTrusted: true`; el dispositivo queda trusted y saltea el OTP en los próximos logins

**Máximo alcanzado**
- **DADO** usuario con `MaxTrustedDevices` dispositivos trusted activos
- **CUANDO** completa el OTP en `POST /api/auth/confirm-device` desde un dispositivo nuevo
- **ENTONCES** responde `200` con `accessToken` y `deviceTrusted: false`; NO DEBE crear el `TrustedDevice` y NO DEBE revocar ninguno existente

**Revocación**
- **DADO** un dispositivo trusted activo con una sesión abierta
- **CUANDO** un admin lo revoca manualmente
- **ENTONCES** `RevokedAt = now`, el slot queda libre y la familia de refresh token de ese dispositivo queda revocada en la misma transacción

### Vigencia del dispositivo confiado (T070)

Un `TrustedDevice` **no dura para siempre**: `ExpiresAt = TrustedAt + TenantSettings.TrustedDeviceLifetimeDays`
(def. **15**; ADR-007, `data-model.md` 1b).

| Aspecto | Regla |
|---|---|
| Activo | `RevokedAt IS NULL AND ExpiresAt > now()`. Fuera de esa condición, `confirm-device` exige OTP de nuevo |
| Vencer libera el slot | El vencido deja de contar para `MaxTrustedDevices`, así que el límite se destraba solo. Es el antídoto a que los slots se llenen por acumulación de aparatos viejos |
| Vencer **NO** es revocar | `RevokedAt` queda en `null`. SÓLO una acción manual lo setea: mezclarlos arruina el registro de quién dio de baja qué |
| Alcance | La caducidad obliga a rehacer el 2FA en ese dispositivo. **NO** corta la sesión viva ni bloquea el acceso — eso es T067/T068 |
| En el listado | Vencido: `isActive: false` con `revokedAt: null`. Revocado: `isActive: false` con `revokedAt` con fecha. Son estados distintos y el listado los distingue |

**Dispositivo vencido**
- **DADO** un `TrustedDevice` con `ExpiresAt <= now()`
- **CUANDO** el usuario vuelve a entrar desde ese mismo dispositivo
- **ENTONCES** `POST /api/auth/login` exige el 2FA otra vez y NO emite token en un solo paso

**Vencer libera el slot**
- **DADO** un usuario con los `MaxTrustedDevices` slots llenos, uno de ellos vencido
- **CUANDO** completa el OTP desde un dispositivo nuevo
- **ENTONCES** responde `deviceTrusted: true`, porque el vencido ya no cuenta

**Nadie es expulsado por vencer**
- **DADO** un dispositivo trusted cuya vigencia corre hasta `ExpiresAt`
- **CUANDO** pasa esa fecha
- **ENTONCES** `RevokedAt` sigue en `null` y la fila queda como histórica

---

### Último uso del dispositivo (T067)

`lastUsedAt` es **evidencia de lectura, nunca regla de negocio**. tasks.md T067 exige que la
respuesta liste el "último uso", y el access token es stateless: usar el sistema no deja rastro
en la base. El único heartbeat que sí lo deja es la **rotación del refresh token** (T065), que
corre cada 15 minutos y ya sabe qué `deviceId` la está pidiendo.

| Aspecto | Regla |
|---|---|
| Dónde se escribe | En el alta del dispositivo y en cada rotación del refresh |
| Piso inicial | `trustedAt`: para llegar al alta hubo OTP en esa misma máquina, así que el alta **ya es** un uso |
| Nunca retrocede | Un reloj que se atrasa (NTP) no puede borrar el último uso real |
| **NO** condiciona seguridad | La autorización la siguen mandando `ExpiresAt` (T070) y `RevokedAt` (manual). Si `lastUsedAt` miente, no se abre ni se cierra ninguna puerta |
| Es por registro | Cada fila guarda su propio uso: el vencido o revocado deja de acumular y el registro nuevo arranca de su `trustedAt` |

**El dueño detecta un dispositivo que no reconoce**
- **DADO** un usuario con dos dispositivos confiados, uno usado hace 2 minutos y otro hace 12 días
- **CUANDO** consulta `GET /api/auth/devices`
- **ENTONCES** ve `trustedAt` y `lastUsedAt` en ambos, y el segundo salta a la vista sin abrir nada más

---

## Cobertura de requerimientos

| Requerimiento | Endpoint |
|---|---|
| FR-005 | `POST /api/auth/register` |
| FR-006 | `POST /api/auth/login` |
| FR-007 | `POST /api/auth/confirm-device` (efecto b: marcado trusted bajo `MaxTrustedDevices`) |
| FR-008 | `POST /api/auth/login` + `POST /api/auth/confirm-device` (OTP por SMS) + `PUT /api/admin/users/{userId}/phone-number` (alta) + `PUT /api/auth/phone-number` (cambio con OTP al número actual) `[PENDIENTE: propuesto]` |
| NFR-004 | Transversal: PBKDF2, JWT de 15 min (≤ 60), OTP ≤ 10 min, sesión con 2 h de inactividad y tope de 8 h |
| NFR-005 | `POST /api/auth/login` (`429`) |

---

## `GET /api/auth/devices` — FR-007 (T067)

Lista los dispositivos confiables del usuario autenticado.

**Autenticación**: JWT válido. El `userId` sale del claim `sub` del token, nunca del body o la URL.

### Request

```http
GET /api/auth/devices
Authorization: Bearer {jwt}
X-Tenant-ID: {guid}
```

### Respuesta `200 OK`

```json
[
  {
    "id": "9a1e4c2f-77b3-4a0e-b1d8-5c2f6e9a0d31",
    "userId": "3f9a2c1e-7b4d-4e8a-9c21-5d6e7f8a9b0c",
    "deviceId": "web-chrome-a91f2c77",
    "trustedAt": "2026-10-04T10:30:00Z",
    "lastUsedAt": "2026-10-04T11:45:00Z",
    "expiresAt": "2026-10-19T10:30:00Z",
    "isActive": true,
    "revokedAt": null
  }
]
```

### Errores

| Status | `errorCode` | Cuándo |
|---|---|---|
| `401` | — | JWT ausente o inválido |
| `403` | `TENANT_MISMATCH` | `X-Tenant-ID` no coincide con `tid` del JWT |
| `404` | `USER_NOT_FOUND` | El `userId` del token ya no existe en ese tenant |

---

## `POST /api/auth/devices/{deviceId}/revoke` — FR-007 (T067)

Revoca un dispositivo confiable del propio usuario. Idempotente.

**Autenticación**: JWT válido. El `userId` sale del claim `sub` del token.

### Request

```http
POST /api/auth/devices/9a1e4c2f-77b3-4a0e-b1d8-5c2f6e9a0d31/revoke
Authorization: Bearer {jwt}
X-Tenant-ID: {guid}
```

### Respuesta `204 No Content`

### Errores

| Status | `errorCode` | Cuándo |
|---|---|---|
| `401` | — | JWT ausente o inválido |
| `403` | `TENANT_MISMATCH` | `X-Tenant-ID` no coincide con `tid` del JWT |
| `404` | `DEVICE_NOT_FOUND` | El `deviceId` no existe o no pertenece al usuario |

### Reglas

- Revocar un dispositivo DEBE liberar el slot de `MaxTrustedDevices`.
- Revocar DEBE revocar también las **familias de refresh token** de ese dispositivo (T065), en la misma transacción. Sin eso, el `RevokedAt` sólo impide saltear el 2FA a futuro mientras la sesión sigue viva hasta `FamilyExpiresAt`: revocar sin cerrar la sesión es teatro (tasks.md T067).
- NO toca los dispositivos ni las sesiones de otro usuario, aunque compartan el mismo `deviceId`.
- `404` **sólo cuando el recurso no existe**: un ID inexistente o un dispositivo de otro usuario. Un dispositivo ya revocado sigue en la tabla, así que la segunda revocación responde `204`, no `404`.
- Idempotente: revocar dos veces no falla.

---

## `GET /api/admin/users/{userId}/devices` — FR-007 (T067)

Lista los dispositivos confiables de un usuario del tenant. Solo `TenantAdmin`.

**Autenticación**: JWT con rol `TenantAdmin` del mismo tenant.

### Request

```http
GET /api/admin/users/3f9a2c1e-7b4d-4e8a-9c21-5d6e7f8a9b0c/devices
Authorization: Bearer {jwt de TenantAdmin}
X-Tenant-ID: {guid}
```

### Respuesta `200 OK`

```json
[
  {
    "id": "9a1e4c2f-77b3-4a0e-b1d8-5c2f6e9a0d31",
    "userId": "3f9a2c1e-7b4d-4e8a-9c21-5d6e7f8a9b0c",
    "deviceId": "web-chrome-a91f2c77",
    "trustedAt": "2026-10-04T10:30:00Z",
    "lastUsedAt": "2026-10-04T11:45:00Z",
    "expiresAt": "2026-10-19T10:30:00Z",
    "isActive": true,
    "revokedAt": null
  }
]
```

### Errores

| Status | `errorCode` | Cuándo |
|---|---|---|
| `401` | — | JWT ausente o inválido |
| `403` | — | El llamante no es `TenantAdmin` |
| `403` | `TENANT_MISMATCH` | `X-Tenant-ID` no coincide con `tid` del JWT |
| `404` | `USER_NOT_FOUND` | El `userId` no existe en ese tenant |

---

## `POST /api/admin/users/{userId}/devices/{deviceId}/revoke` — FR-007 (T067)

Revoca un dispositivo específico de un usuario. Solo `TenantAdmin`. Idempotente.

### Request

```http
POST /api/admin/users/3f9a2c1e-7b4d-4e8a-9c21-5d6e7f8a9b0c/devices/9a1e4c2f-77b3-4a0e-b1d8-5c2f6e9a0d31/revoke
Authorization: Bearer {jwt de TenantAdmin}
X-Tenant-ID: {guid}
```

### Respuesta `204 No Content`

### Errores

| Status | `errorCode` | Cuándo |
|---|---|---|
| `401` | — | JWT ausente o inválido |
| `403` | — | El llamante no es `TenantAdmin` |
| `403` | `TENANT_MISMATCH` | `X-Tenant-ID` no coincide con `tid` del JWT |
| `404` | `DEVICE_NOT_FOUND` | El `deviceId` no existe o no pertenece al usuario |

---

## `POST /api/admin/users/{userId}/devices/revoke-all` — FR-007 (T067)

Revoca todos los dispositivos confiables de un usuario. Solo `TenantAdmin`. Idempotente.

### Request

```http
POST /api/admin/users/3f9a2c1e-7b4d-4e8a-9c21-5d6e7f8a9b0c/devices/revoke-all
Authorization: Bearer {jwt de TenantAdmin}
X-Tenant-ID: {guid}
```

### Respuesta `204 No Content`

### Errores

| Status | `errorCode` | Cuándo |
|---|---|---|
| `401` | — | JWT ausente o inválido |
| `403` | — | El llamante no es `TenantAdmin` |
| `403` | `TENANT_MISMATCH` | `X-Tenant-ID` no coincide con `tid` del JWT |
| `404` | `USER_NOT_FOUND` | El `userId` no existe en ese tenant |

### Reglas

- Revocar todos los dispositivos DEBE liberar todos los slots de `MaxTrustedDevices`.
- `404` sólo cuando el `userId` no existe en el tenant. Un usuario que ya no tiene dispositivos sí existe: eso responde `204`.
- Revocar todos DEBE revocar las familias de refresh token de **cada** dispositivo del usuario: `revoke-all` lo deja sin ninguna sesión viva por dispositivo confiado. Las sesiones abiertas desde un dispositivo **no** confiado las mata T068 (deshabilitar la cuenta).
- Idempotente: revocar dos veces no falla.

---

## `POST /api/auth/forgot-password` y `POST /api/auth/reset-password` + fallback admin — T069

Recuperación de contraseña cuando el usuario perdió el acceso a ella. El canal de verificación es el **mismo del 2FA: el SMS al número enrolado** — no hay link por email ni pregunta de seguridad. Dos pasos anónimos + un fallback admin para quien perdió el celular Y la contraseña.

### Paso 1 — `POST /api/auth/forgot-password` (AllowAnonymous, sin `X-Tenant-ID`)

El email resuelve el tenant; no hay header. Rate limit por IP: la política `Auth` de clase.

#### Request

```json
{ "email": "duena@droga.co" }
```

#### Respuesta `202 Accepted` — siempre

Cuerpo vacío. Idéntica exista o no el email, esté o no deshabilitado, tenga o no teléfono. No se emite código si la cuenta no existe, está bloqueada (spray T090 / deshabilitada T068) o no tiene `PhoneNumber`.

### Paso 2 — `POST /api/auth/reset-password` (AllowAnonymous, sin `X-Tenant-ID`)

#### Request

```json
{ "email": "duena@droga.co", "code": "483920", "newPassword": "Nueva-Larga-2026" }
```

#### Respuesta `204 No Content`

#### Errores

| Status | errorCode | Cuándo |
|---|---|---|
| `401` | `AUTH_OTP_REJECTED` | email inexistente/deshabilitado, código errado, vencido o ya usado — misma respuesta en todos los casos |
| `400` | `VALIDATION_FAILED` | la nueva contraseña no cumple la política (T003: largo 12) |

#### Reglas

- **Uniformidad**: el paso 1 responde `202` vacío SIEMPRE; el paso 2 responde `401 AUTH_OTP_REJECTED` idéntico para email inexistente y para código errado (mismo motivo que T055c: enumerar correos).
- El código se envía por **SMS al `PhoneNumber` enrolado**, en el alcance propio `internal:password-reset` (aislado de login y de cambio de número), expira en 10 minutos y es de **un solo uso**.
- El consume corre dentro de la transacción del lock: **contraseña débil ⇒ `400` y el código NO se quema** (rollback); el usuario reintenta con el mismo código.
- Un reset exitoso **cambia la contraseña**, **revoca todas las familias de refresh** (T065) y **quema los OTPs vivos** del usuario.
- El reset **NO salta el 2FA**: tras el reset, un dispositivo desconocido sigue pidiendo OTP.
- El reset **NO toca los dispositivos confiables**: el ancla OTP (el número) no cambió, y sin la contraseña nueva un dispositivo no entra. *(Decisión a veto en review.)*
- **No rehabilita**: una cuenta deshabilitada (T068) tampoco inicia el flujo (mismo `202` uniforme, cero filas en `device_otps`).
- El lockout por spraying (T090) también bloquea iniciar el flujo por `LockoutEnd` — aceptado; el dueño espera a que venza.

### Fallback admin — `POST /api/admin/users/{userId}/reset-password` (TenantAdmin)

Para el que perdió el celular Y la contraseña. Cambia la contraseña directamente (sin OTP), corta todas las familias de refresh y quema los OTPs vivos. NO toca `LockoutEnd` (un reset no rehabilita) ni los dispositivos confiables.

#### Request

```json
{ "newPassword": "Nueva-Larga-2026" }
```

#### Respuesta `200 OK`

```json
{ "userId": "..." }
```

#### Errores

| Status | errorCode | Cuándo |
|---|---|---|
| `403` | — | caller Member (política `TenantAdmin`) |
| `404` | `USER_NOT_FOUND` | el `userId` no existe en el tenant del caller |
| `400` | `VALIDATION_FAILED` | la nueva contraseña no cumple la política |

#### Reglas del fallback

- Fail-closed al estilo T061/T068: **primero se corta el acceso** (familias + OTPs) y **después** se escribe la contraseña. Una contraseña débil responde `400` con las sesiones **ya cortadas**; la contraseña anterior queda intacta. *(Decisión a veto en review.)*
- Riesgo residual: el SMS al viejo número puede caer en manos de un tercero — mitigable con rate limit + aviso al número viejo (postergado, flaggeado).

### Escenarios (Dado/Cuando/Entonces)

**Respuesta uniforme**
- **DADO** una cuenta existente y un email inexistente
- **CUANDO** ambos piden `forgot-password`
- **ENTONCES** responden `202` byte a byte igual; sólo al existente se le emite código

**Código de un solo uso**
- **DADO** un reset completado
- **CUANDO** se repite con el mismo código
- **ENTONCES** responde `401 AUTH_OTP_REJECTED`

**El reset mata las sesiones vivas**
- **DADO** un usuario con una sesión abierta
- **CUANDO** completa el reset
- **ENTONCES** su refresh token viejo responde `401`

**El 2FA sigue vivo**
- **DADO** un usuario que completó el reset
- **CUANDO** entra con la contraseña nueva desde un dispositivo desconocido
- **ENTONCES** `200` con `requiresDeviceConfirmation: true` — la contraseña sola no alcanza

**Contraseña débil no quema el código**
- **DADO** un código vigente
- **CUANDO** se intenta resetear con una contraseña que no pasa la política
- **ENTONCES** responde `400 VALIDATION_FAILED` y el MISMO código sirve para el reintento

**El deshabilitado no inicia el flujo**
- **DADO** una cuenta deshabilitada (T068)
- **CUANDO** pide `forgot-password`
- **ENTONCES** responde `202` uniforme y no se inserta fila en `device_otps`

**Fallback admin**
- **DADO** un miembro con sesión viva
- **CUANDO** un TenantAdmin le resetea la contraseña
- **ENTONCES** responde `200`, las familias de refresh quedan revocadas y la contraseña vieja no entra

**Aislamiento de tenant en el fallback**
- **DADO** un admin del tenant A
- **CUANDO** resetea a un usuario del tenant B
- **ENTONCES** responde `404 USER_NOT_FOUND`
