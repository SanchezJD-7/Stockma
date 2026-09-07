# Contrato: Auth API

**Base**: `/api/auth`
**Requerimientos**: FR-005 … FR-008 · NFR-004, NFR-005
**Fuentes**: [`../spec.md`](../spec.md) · [`../plan.md`](../plan.md) (sección 5) · [`../data-model.md`](../data-model.md)

---

## Reglas transversales

| Regla | Detalle |
|---|---|
| Header de tenant | `X-Tenant-ID: {guid}` DEBE estar presente en todas las rutas **salvo `POST /api/auth/login`** (T055). Ausente o no parseable → `400` `TENANT_HEADER_MISSING` / `TENANT_HEADER_INVALID` (FR-001) |
| Header de dispositivo | `X-Device-Id: {string}` — opcional en `login`, obligatorio en `confirm-device`. `[PENDIENTE: las fuentes lo llaman "deviceId header opcional" en login y campo del body en confirm-device; unificar nombre y ubicación]` |
| Formato de error | `application/problem+json` (`ProblemDetails`) con extensión `errorCode` |
| Rate limiting | `POST /login` DEBERÍA limitar a 5 intentos por IP por minuto → `429` (NFR-005) |
| Hash de contraseña | PBKDF2 vía ASP.NET Core Identity (NFR-004) |
| Vigencia del JWT | `exp` ≤ 60 minutos; claims `sub` (userId) y `tid` (tenantId) (NFR-004, FR-006) |
| Canal del OTP | **SMS** al `ApplicationUser.PhoneNumber`. Ventana de vigencia **≤ 10 min** (NFR-004) — se mantiene sin cambios respecto del canal anterior |
| Origen del `PhoneNumber` | Escribible sólo por un admin vía `PUT /api/admin/users/{userId}/phone-number`. El usuario NO DEBE poder modificar el suyo |

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

Registra un `ApplicationUser` asociado al `TenantId` del header.

**Command**: `RegisterUserCommand`

### Request

```http
POST /api/auth/register
X-Tenant-ID: 6f2c1b3a-8e41-4f2d-9c7a-1d5e8b0a3f77
Content-Type: application/json
```

```json
{
  "email": "farmacia@ejemplo.co",
  "password": "S3gura#2026"
}
```

### Respuesta `201 Created`

```json
{
  "userId": "9a1e4c2f-77b3-4a0e-b1d8-5c2f6e9a0d31"
}
```

### Errores

| Status | `errorCode` | Cuándo |
|---|---|---|
| `400` | `TENANT_HEADER_MISSING` / `TENANT_HEADER_INVALID` | Header `X-Tenant-ID` ausente o no es un GUID (FR-001) |
| `400` | `VALIDATION_FAILED` | Email o contraseña no cumplen las reglas de Identity |
| `409` | `AUTH_EMAIL_DUPLICATE` | El email ya existe en **cualquier** tenant de la plataforma (FR-005, T055). El email es único global |

### Escenarios (Dado/Cuando/Entonces)

**Registro exitoso**
- **DADO** email y contraseña válidos con header tenant
- **CUANDO** `POST /api/auth/register`
- **ENTONCES** crea `ApplicationUser` con `TenantId` y retorna `201`

**Email duplicado**
- **DADO** email ya registrado en el tenant
- **CUANDO** registrar
- **ENTONCES** responde `409 Conflict`

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
X-Device-Id: web-chrome-a91f2c
Content-Type: application/json
```

Sin `X-Tenant-ID`: el tenant sale del email.

```json
{
  "email": "farmacia@ejemplo.co",
  "password": "S3gura#2026"
}
```

### Respuesta `200 OK` — dispositivo confiable

```json
{
  "accessToken": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...",
  "expiresIn": 3600
}
```

`expiresIn` en segundos, ≤ 3600 (NFR-004).

### Respuesta `200 OK` — dispositivo desconocido (FR-008)

```json
{
  "requiresDeviceConfirmation": true
}
```

El sistema genera un OTP (hash con `IPasswordHasher`, expira en ≤ 10 min, persistido en `DeviceOtp`) y lo encola por **SMS** al `PhoneNumber` del usuario vía `ISmsSender`.

> `[PENDIENTE: las fuentes no definen si esta respuesta es 200 o 202, ni si incluye el número de celular enmascarado o un tiempo de expiración para la UI]`

> `[PENDIENTE: usuario sin PhoneNumber cargado — definir si el primer login se permite sin 2FA, si el admin debe cargar el número antes de habilitar la cuenta, o si se bloquea]`

> `[PENDIENTE: proveedor de SMS no elegido]`

### Errores

| Status | `errorCode` | Cuándo |
|---|---|---|
| `401` | `AUTH_INVALID_CREDENTIALS` | Email inexistente **o** contraseña incorrecta. La respuesta DEBE ser idéntica en los tres casos —email que no existe, contraseña equivocada, usuario de otro tenant— porque distinguirlos habilita enumerar qué correos usan Stockma (FR-006) |
| `429` | `AUTH_RATE_LIMITED` | Más de 5 intentos por IP por minuto (NFR-005) |

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

---

## `POST /api/auth/confirm-device` — FR-007, FR-008

Valida el OTP recibido por **SMS**, marca el dispositivo como confiable y emite el JWT.

> El `email` del body identifica al usuario; el OTP le llegó por SMS a su `PhoneNumber`.

**Command**: `ConfirmDeviceCommand`

### Request

```http
POST /api/auth/confirm-device
X-Tenant-ID: 6f2c1b3a-8e41-4f2d-9c7a-1d5e8b0a3f77
Content-Type: application/json
```

```json
{
  "email": "farmacia@ejemplo.co",
  "deviceId": "web-chrome-a91f2c",
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
| a | Valida el OTP y emite el JWT | **Siempre**, haya slot libre o no |
| b | Crea el `TrustedDevice` (`TrustedAt = now`, `RevokedAt = null`) y registra el evento de notificación al dispositivo trusted previo | **Sólo si** los `TrustedDevice` activos (`RevokedAt IS NULL`) del usuario son `< MaxTrustedDevices` (def. 2) |

- Sin slot libre: el efecto (a) ocurre igual — el usuario **entra**. El efecto (b) se omite: `deviceTrusted: false`, el dispositivo no queda recordado y deberá hacer OTP en **cada** login.
- El sistema NO DEBE revocar ningún `TrustedDevice` para hacer lugar. `RevokedAt` se setea SÓLO por acción manual de un admin (FR-007).
- La notificación real (SignalR / email al dispositivo anterior) queda **fuera de alcance** de este slice: sólo se registra el evento. Esa notificación es un aviso al dispositivo trusted previo, **no** el canal del segundo factor.

### Errores

| Status | `errorCode` | Cuándo |
|---|---|---|
| `400` | `TENANT_HEADER_MISSING` / `TENANT_HEADER_INVALID` | Header inválido |
| `401` | `AUTH_OTP_INVALID` | OTP incorrecto. El intento DEBE quedar registrado (FR-008) |
| `401` | `AUTH_OTP_EXPIRED` | OTP vencido (> 10 min). El intento DEBE quedar registrado (FR-008) |

> **No hay error por límite de dispositivos.** `MaxTrustedDevices` NO DEBE producir un status de error: superarlo devuelve `200` con `deviceTrusted: false`. Ver la sección de abajo.

> `[PENDIENTE: las fuentes describen un único 401 para "OTP errado o vencido". La separación en AUTH_OTP_INVALID / AUTH_OTP_EXPIRED es una desagregación; confirmar si se prefiere un errorCode único (p. ej. AUTH_OTP_REJECTED) para no filtrar si el OTP existía]`

---

## `PUT /api/admin/users/{userId}/phone-number` — FR-008

`[PENDIENTE: propuesto, no está en las fuentes originales]`

Único camino para cargar o cambiar el `PhoneNumber` que recibe el OTP por SMS. **Sólo un admin del tenant.**

**Command**: `SetUserPhoneNumberCommand` `[PENDIENTE: propuesto]`

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
| `400` | `VALIDATION_FAILED` | `phoneNumber` no es un número móvil válido en formato E.164 `[PENDIENTE: formato/validación a confirmar]` |
| `401` | `AUTH_INVALID_CREDENTIALS` | JWT ausente o vencido |
| `403` | `AUTH_FORBIDDEN` | El llamante no es admin del tenant, o intenta modificar su propio número sin ser admin |
| `404` | `USER_NOT_FOUND` | El `userId` no existe **en ese tenant** |

### Reglas

- El usuario objetivo NO DEBE poder invocar este endpoint sobre sí mismo salvo que sea admin del tenant.
- Identity expone `SetPhoneNumberAsync` / `ChangePhoneNumberAsync` y los endpoints self-service de Identity por defecto: esa superficie DEBE cerrarse explícitamente (ver T050). Dejarla abierta permitiría al usuario desviar su propio segundo factor.
- El cambio DEBE quedar auditado (`AuditSaveChangesInterceptor`) con el admin que lo ejecutó.
- `[PENDIENTE: definir si el cambio de PhoneNumber revoca los OTP en vuelo y/o los TrustedDevice del usuario]`

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

## `MaxTrustedDevices` — gobierna el 2FA, no el acceso

**Un único comportamiento.** `MaxTrustedDevices` (def. 2, configurable por tenant) limita **cuántos dispositivos pueden saltear el 2FA**. NUNCA limita el acceso.

| Aspecto | Regla |
|---|---|
| Acceso (FR-008) | CUALQUIER dispositivo entra vía OTP por SMS. El login NO DEBE bloquearse por límite de dispositivos, **siempre** |
| Privilegio de saltear el 2FA (FR-007) | Máximo `MaxTrustedDevices` dispositivos trusted activos por usuario |
| Al superar el máximo | `confirm-device` responde **`200`** con `accessToken` y `deviceTrusted: false`. No se crea el `TrustedDevice`; ningún dispositivo existente se revoca |
| Alta de un slot | Manual por un admin. `RevokedAt` se setea SÓLO por acción manual de un admin — **nadie es expulsado automáticamente** y no hay selección del "más antiguo" |
| Superficie extra requerida | Endpoint admin de revocación/alta de dispositivo — **no existe en las fuentes**. `[PENDIENTE: definir]` |

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
- **DADO** un dispositivo trusted activo
- **CUANDO** un admin lo revoca manualmente
- **ENTONCES** `RevokedAt = now` y el slot queda libre

---

## Cobertura de requerimientos

| Requerimiento | Endpoint |
|---|---|
| FR-005 | `POST /api/auth/register` |
| FR-006 | `POST /api/auth/login` |
| FR-007 | `POST /api/auth/confirm-device` (efecto b: marcado trusted bajo `MaxTrustedDevices`) |
| FR-008 | `POST /api/auth/login` + `POST /api/auth/confirm-device` (OTP por SMS) + `PUT /api/admin/users/{userId}/phone-number` `[PENDIENTE: propuesto]` |
| NFR-004 | Transversal: PBKDF2, JWT ≤ 60 min, OTP ≤ 10 min |
| NFR-005 | `POST /api/auth/login` (`429`) |
