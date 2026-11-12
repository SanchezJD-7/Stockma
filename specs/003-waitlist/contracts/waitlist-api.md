# Contrato: Waitlist API

**Base**: `/api/waitlist`
**Spec**: [`../spec.md`](../spec.md)
**Capabilities**: `waitlist-capture`, `identity-access`, `api-error-handling`

---

## Reglas transversales

| Regla | Detalle |
|---|---|
| Autenticación | **Pública**: `[AllowAnonymous]` y path agregado a `TenantMiddleware.UnauthenticatedPaths`. Sin JWT |
| Tenant | Sin header `X-Tenant-ID`; el registro es global (no pertenece a ningún tenant) |
| Formato de error | `application/problem+json` (`ProblemDetails`) con extensión `errorCode`, siempre presente |
| Normalización de correo | `trim` + minúsculas antes de validar y persistir; la unicidad se evalúa sobre el valor normalizado |
| Validación de correo | **Sólo sintaxis** — no se verifica existencia del buzón (ver escenario edge) |
| Orden de operaciones | Persistir → responder `201` → encolar correo. El fallo del correo NUNCA altera la respuesta |

### `CreateWaitlistEntryRequest`

```json
{
  "fullName": "Juan Pérez",
  "countryCode": "+57",
  "phone": "3001234567",
  "email": "Juan@Ejemplo.com"
}
```

| Campo | Tipo | Reglas |
|---|---|---|
| `fullName` | `string` | Requerido, 2–120 caracteres, sin sólo-espacios |
| `countryCode` | `string` | Requerido, DEBE ser `"+57"` (prefijo fijo del formulario) |
| `phone` | `string` | Requerido, 7–15 dígitos; se normaliza sin espacios ni `+` |
| `email` | `string` | Requerido, formato válido; se normaliza `trim` + minúsculas |

### `WaitlistEntryDto`

```json
{
  "id": "3b8d1f60-2c14-4a9e-8f77-0a5b3e2d9c41",
  "email": "juan@ejemplo.com"
}
```

`id` es GUID version 7 (`Guid.CreateVersion7()`, patrón del dominio).

---

## `POST /api/waitlist`

Registra un interesado en la lista de espera. Endpoint **público**.

### Request

```http
POST /api/waitlist
Content-Type: application/json
```

```json
{
  "fullName": "Juan Pérez",
  "countryCode": "+57",
  "phone": "3001234567",
  "email": "Juan@Ejemplo.com"
}
```

### Respuesta `201 Created`

```http
Location: /api/waitlist/3b8d1f60-2c14-4a9e-8f77-0a5b3e2d9c41
```

```json
{
  "id": "3b8d1f60-2c14-4a9e-8f77-0a5b3e2d9c41",
  "email": "juan@ejemplo.com"
}
```

`email` retorna el valor **normalizado**.

### Errores

| Status | `errorCode` | Cuándo |
|---|---|---|
| `400` | `VALIDATION_FAILED` | Campo ausente/inválido (longitud, formato de correo, dígitos del teléfono) |
| `409` | `WAITLIST_EMAIL_DUPLICATE` | El correo normalizado ya existe en la tabla (índice único) |
| `500` | `INTERNAL_ERROR` | Excepción no dominada — **catch-all del interceptor global** |
| `429` | — | Rate limit (comportamiento existente de `ProblemResponses`, no se modifica) |

```json
{
  "type": "https://stockma.co/errors/waitlist-email-duplicate",
  "title": "Correo ya registrado",
  "status": 409,
  "errorCode": "WAITLIST_EMAIL_DUPLICATE",
  "detail": "El correo 'juan@ejemplo.com' ya está en la lista de espera."
}
```

`400 VALIDATION_FAILED` incluye el mapa de campos (patrón `ValidationProblemDetails` existente):

```json
{
  "title": "Datos inválidos",
  "status": 400,
  "errorCode": "VALIDATION_FAILED",
  "errors": { "email": ["El correo no tiene un formato válido."] }
}
```

`500 INTERNAL_ERROR` (catch-all — requisito aprobado en 003):

```json
{
  "type": "https://stockma.co/errors/internal-error",
  "title": "Error inesperado",
  "status": 500,
  "errorCode": "INTERNAL_ERROR",
  "detail": "Ocurrió un error procesando la solicitud."
}
```

> El catch-all **NO DEBE** filtrar stack trace ni tipo de excepción en el payload. Modifica el comportamiento fijado por `NonDomainException_IsNotSwallowed` (`Stockma.Api.Tests/DomainExceptionMiddlewareTests.cs`): ese test pasa a esperar `500` + `errorCode`, no la propagación de la excepción.

### Escenarios (Dado/Cuando/Entonces)

**Registro exitoso**
- **DADO** `POST /api/waitlist` con nombre, teléfono y correo válidos
- **CUANDO** se procesa
- **ENTONCES** persiste en PostgreSQL y retorna `201` con `{ id, email }`

**Correo duplicado**
- **DADO** un correo ya registrado (cualquiera sea el formato original: `Juan@Ejemplo.com` y `juan@ejemplo.com` son el mismo registro)
- **CUANDO** se registra de nuevo
- **ENTONCES** responde `409 WAITLIST_EMAIL_DUPLICATE`, sin crear un segundo registro

**Validación fallida**
- **DADO** un body con `fullName` de 1 carácter o `email` sin `@`
- **CUANDO** se procesa
- **ENTONCES** responde `400 VALIDATION_FAILED` con el mapa de campos y no persiste nada

**Excepción no dominada**
- **DADO** una falla inesperada (p. ej. PostgreSQL caído)
- **CUANDO** se procesa
- **ENTONCES** el interceptor global responde `500 INTERNAL_ERROR` sin stack trace

**Correo sintácticamente válido pero inexistente**
- **DADO** `email: "n0existe@dominioinexistente.com"` bien formado
- **CUANDO** se procesa
- **ENTONCES** responde `201` y persiste (no se verifica buzón); el rebote lo gestiona SendGrid con sus supresiones, fuera de alcance

**Fallo de SendGrid**
- **DADO** el registro persistido correctamente y la API de SendGrid no disponible
- **CUANDO** se intenta el envío asíncrono
- **ENTONCES** la respuesta al cliente sigue siendo `201` y el fallo queda en el log

---

## Manejo de errores global (modificación del interceptor)

| Comportamiento | Antes (001/002) | Después (003) |
|---|---|---|
| `DomainException` | Mapeada a status + `errorCode` | Sin cambios |
| `ValidationProblemDetails` (ModelState) | `InvalidModelStateResponseFactory` | Sin cambios |
| Excepción no dominada | Se propaga (test `NonDomainException_IsNotSwallowed`) | `500` `application/problem+json` con `INTERNAL_ERROR`, sin stack trace, logueada |

---

## Cobertura de requerimientos

| Requerimiento | Cobertura |
|---|---|
| Persistencia del registro | `POST /api/waitlist` → PostgreSQL |
| Errores claros en pantalla | 400 / 409 / 500 con `errorCode` + mapa de campos |
| Registro duplicado | 409 sobre índice único del correo normalizado |
| Interceptor global | Sección "Manejo de errores global" |
| Secretos | Fuera de este contrato — `SendGrid:ApiKey` vía User Secrets / env var `SendGrid__ApiKey` |

## Fuera de alcance

- `GET /api/waitlist` (admin de la lista), `DELETE`, exportación.
- Deduplicación/merge de registros y verificación de buzón.
- Webhooks/Event Webhook de SendGrid.
