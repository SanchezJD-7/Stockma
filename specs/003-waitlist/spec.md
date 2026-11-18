# Spec 003 - Waitlist Registration

> **Regla de gobernación aplicada**: este documento gobierna _contratos, comportamiento, estado y manejo de errores_ del formulario de lista de espera. **No contiene copy literal de UI.** El copy versionado vive en los componentes de `frontend/web/src/features/landing/`; el prototipo HTML local (`C:\Users\User\Downloads\Stockma_landing.html`, fuera del repo e ignorado por Git) es referencia visual no normativa.

## Objetivo

Habilitar la publicación oficial de la landing: formulario de lista de espera con persistencia en PostgreSQL, manejo de errores global con razón exacta en pantalla y correo de confirmación asíncrono vía SendGrid.

## Contexto

- La spec 002 dejó los 3 CTAs de lista de espera activos y **sin acción** (`specs/002-product-introduction/spec.md:35`); esta spec los conecta. `LandingPage.test.tsx:162-182` fija ese comportamiento y se modifica acá.
- Proposal SDD `sdd/003-waitlist/proposal` (Engram): capabilities nuevas `waitlist-capture` y `waitlist-confirmation-email`; modificadas `api-error-handling` e `identity-access`.
- Ya existen `Stockma.Api/Middleware/DomainExceptionMiddleware.cs` (sólo captura `DomainException`), `ProblemResponses.cs` (`errorCode` siempre presente) y `TenantMiddleware.UnauthenticatedPaths`.
- El repo no tiene infraestructura de email; el patrón a replicar es `ISmsSender`/`SmsOptions`/`TwilioSmsSender` con `ValidateOnStart`.
- En dev el frontend llega a la API por proxy de Vite (`/api` → `http://localhost:5265`); no existe CORS configurado.

## Alcance

- Modal de registro de lista de espera que abre desde los 3 CTAs de lista de espera: Hero, DemoSection y WaitlistSection. Los 4 CTAs de Pricing siguen **sin acción**.
- Campos: nombre completo, teléfono móvil con prefijo fijo `+57`, correo electrónico.
- Spinner en el botón durante el envío; en éxito se cierra la modal de registro y abre una **modal de confirmación separada**.
- Endpoint público `POST /api/waitlist` con persistencia en PostgreSQL y errores `application/problem+json` con `errorCode` (400 / 409 / 500 catch-all).
- Correo de confirmación asíncrono personalizado con el nombre del usuario (SendGrid); su fallo **nunca** falla el registro.
- Secretos de entorno: User Secrets en dev, variables de entorno en prod, `appsettings.json` sin valores reales.

## Fuera de alcance

- CORS (en dev lo resuelve el proxy de Vite; evaluar en design si la landing publicada comparte origen).
- Verificación de existencia de buzón, doble opt-in, webhooks/Event Webhook y panel de supresiones de SendGrid.
- Modal de login, CTAs de Pricing, administración de la lista (visualización/exportación), deduplicación de registros.
- Migración automática de esquema al arrancar (se aplica con `dotnet ef database update`, como el resto).

## Modal de registro

**Apertura y cierre**

- Los 3 CTAs de lista de espera DEBEN abrir la modal; el botón de cierre (`×`), la tecla Escape y el clic fuera del diálogo la DEBEN cerrar.
- La modal DEBE exponer `role="dialog"`, `aria-modal="true"` y `aria-labelledby` a su título.

**Campos**

- Nombre completo: requerido, 2–120 caracteres.
- Teléfono móvil: requerido, 7–15 dígitos, con prefijo `+57` y bandera colombiana visibles; el campo oculto `countryCode` envía `+57`.
- Correo electrónico: requerido, con `autocomplete="email"`; se normaliza `trim` + minúsculas antes de enviar.

**Envío y estados**

- Durante la petición el botón DEBE mostrar un indicador de carga circular (spinner) y quedar deshabilitado; un doble submit DEBE ser imposible.
- En éxito: la modal de registro SE DEBE cerrar y la modal de confirmación SE DEBE abrir.
- En error: la modal de registro SE DEBE mantener abierta y mostrar un mensaje visible con la razón exacta del fallo (400 por campo, 409 correo duplicado, 500 inesperado).

#### Scenario: Registro exitoso

- GIVEN la modal abierta con nombre, teléfono y correo válidos
- WHEN se envía el formulario
- THEN el botón muestra el spinner y queda deshabilitado
- AND la API responde `201`
- AND la modal de registro se cierra y abre la modal de confirmación

#### Scenario: Correo ya registrado

- GIVEN un correo que ya existe en la lista
- WHEN se envía el formulario
- THEN la API responde `409` con `errorCode` `WAITLIST_EMAIL_DUPLICATE`
- AND la modal permanece abierta y muestra el motivo puntual

#### Scenario: Validación fallida

- GIVEN el nombre con 1 carácter o un correo sin `@`
- WHEN se envía el formulario
- THEN la API responde `400` con `errorCode` `VALIDATION_FAILED` y el mapa de campos
- AND la modal permanece abierta y señala el campo inválido

#### Scenario: Fallo inesperado

- GIVEN que la API lanza una excepción no dominada
- WHEN se envía el formulario
- THEN la respuesta es `500` `application/problem+json` sin stack trace
- AND la modal muestra un mensaje genérico de error y conserva los datos ingresados

## Modal de confirmación

- Modal separada, sin campos ni red: informa que el registro quedó guardado y que se avisará por correo.
- Se cierra con botón, Escape o clic fuera; al cerrarla no debe reabrirse la de registro.

#### Scenario: Cierre de confirmación

- GIVEN la modal de confirmación abierta tras un registro exitoso
- WHEN el usuario la cierra
- THEN desaparece y la landing vuelve a su estado normal

## API: POST /api/waitlist

Contrato detallado en `contracts/waitlist-api.md`. Resumen:

- **Público**: `[AllowAnonymous]` y path agregado a `TenantMiddleware.UnauthenticatedPaths`; no requiere `X-Tenant-ID` ni JWT.
- **Request**: `fullName` (string, 2–120), `countryCode` (`"+57"`), `phone` (7–15 dígitos), `email` (string, único tras normalizar).
- **Response** `201 Created`: `{ "id": "<guid v7>", "email": "<normalizado>" }`.
- **Errores** (`application/problem+json`, `errorCode` siempre presente):

| Status | `errorCode` | Causa |
|---|---|---|
| 400 | `VALIDATION_FAILED` | Validación de campos (ModelState o dominio), con mapa por campo |
| 409 | `WAITLIST_EMAIL_DUPLICATE` | Correo ya registrado (índice único en Postgres) |
| 500 | `INTERNAL_ERROR` | Excepción no dominada (catch-all del interceptor global) |

- El catch-all **modifica** el comportamiento fijado por `NonDomainException_IsNotSwallowed` en `backend/tests/Stockma.Api.Tests/DomainExceptionMiddlewareTests.cs`: es un requisito explícito de esta spec.

#### Scenario: Correo sintácticamente válido pero inexistente

- GIVEN un correo bien formado cuyo buzón no existe (ej. typo en el dominio)
- WHEN se envía el formulario
- THEN la API responde `201` y el registro se persiste (no se verifica existencia de buzón)
- AND el rebote posterior lo gestiona SendGrid con sus listas de supresión, fuera del alcance de esta spec

## Persistencia y correo de confirmación

- El registro DEBE persistirse en PostgreSQL **antes** de encolar el envío del correo.
- El correo DEBE ser personalizado con el nombre del usuario y enviarse de forma asíncrona.
- Un fallo de envío (API de SendGrid caída, remitente no verificado, rebote) DEBE loguearse y **NO** alterar la respuesta `201` ni el registro persistido.
- Las credenciales NO DEBEN figurar en `appsettings.json`; se leen de User Secrets (`user-secrets set "SendGrid:ApiKey" ...`) o variables de entorno (`SendGrid__ApiKey`) y DEBEN validarse al iniciar (`ValidateOnStart`, patrón de `JwtOptions`/`SmsOptions`).

#### Scenario: SendGrid no disponible

- GIVEN el registro persistido correctamente
- WHEN la llamada a SendGrid falla
- THEN la respuesta al cliente sigue siendo `201` con la modal de confirmación
- AND el fallo queda registrado en el log

## Diseño

Las decisiones de implementación (ubicación de la entidad `WaitlistEntry` frente a `TenantArchitectureTests`, estrategia de envío asíncrono post-commit, esquema de modales y provider en React) se documentan en `design.md`, fase siguiente del workflow SDD.

## Reglas de copy

- Copy corto, directo, en español neutro latinoamericano: **sin voseo** (gate `scripts/check-neutral-es.mjs`).
- El copy versionado vive en los componentes, no en esta spec (regla de gobernación).

## Archivos involucrados

- `frontend/web/src/features/landing/components/WaitlistModal/` (modal de registro: `.tsx`, `.module.css`, `.styles.ts`, `.test.tsx`)
- `frontend/web/src/features/landing/components/WaitlistSuccessModal/` (modal de confirmación)
- `frontend/web/src/features/landing/components/WaitlistModalProvider/` (contexto + hook `useOpenWaitlist`)
- `frontend/web/src/features/landing/components/{Hero,DemoSection,WaitlistSection}/` (conexión de los 3 CTAs)
- `frontend/web/src/features/landing/components/LandingPage/LandingPage.tsx` y `.test.tsx` (envuelve con el provider; modifica el test de "CTA sin acción")
- `frontend/web/src/services/waitlist-service.ts` (llamada directa `axios.post` con `config.API_URL` + `authConfig()` de `shared/http.ts`)
- `backend/src/Stockma.Api/Controllers/WaitlistController.cs`
- `backend/src/Stockma.Api/Middleware/DomainExceptionMiddleware.cs` (catch-all 500)
- `backend/src/Stockma.Api/Middleware/TenantMiddleware.cs` (path público)
- `backend/src/Stockma.Application/Waitlist/` (comando, handler, DTO, puerto de repositorio, puerto `IEmailSender`)
- `backend/src/Stockma.Domain/Entities/WaitlistEntry.cs`
- `backend/src/Stockma.Infrastructure/Waitlist/` (repositorio, `EmailSender` SendGrid, opciones)
- `backend/src/Stockma.Infrastructure/Persistence/Configurations/` + `Migrations/` (tabla e índice único de correo)
- `backend/src/Stockma.Api/appsettings.json` (sección `SendGrid` sin valores) + User Secrets
- Tests: `Stockma.Domain.Tests/WaitlistEntryTests.cs`, `Stockma.Application.Tests/RegisterWaitlistEntryCommandTests.cs`, `Stockma.Api.Tests/WaitlistEndpointsTests.cs`, `Stockma.Infrastructure.Tests/{WaitlistRepositoryTests,SendGridEmailSenderTests}.cs`, `frontend .../WaitlistModal.test.tsx`, `WaitlistSuccessModal.test.tsx`, `waitlist-service.test.ts`

## Criterios de aceptación

- [x] Los 3 CTAs de lista de espera abren la modal de registro; Pricing sigue sin acción
- [x] Spinner visible y botón deshabilitado durante el envío; sin doble submit
- [x] Éxito cierra la modal de registro y abre la de confirmación separada
- [x] Errores 400/409/500 muestran la razón exacta en pantalla; 500 sin stack trace
- [x] `POST /api/waitlist` público persiste en PostgreSQL y devuelve `201 { id, email }`
- [x] Correo de confirmación asíncrono personalizado con el nombre; su fallo no afecta el `201`
- [x] Secretos fuera de `appsettings.json`; `ValidateOnStart` para `SendGrid`
- [x] Sin voseo (gate) y sin estilos inline (gate `no-inline-styles.test.ts`)
- [x] Cobertura de tests TDD de la tabla de `tasks.md` en verde
