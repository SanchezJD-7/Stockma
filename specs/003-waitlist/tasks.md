# Tasks: Waitlist Registration (Spec 003)

> **Regla de gobernación**: TDD — en cada unidad de trabajo el test se escribe PRIMERO (rojo) y luego la implementación (verde). Las tareas de test preceden a su implementación. Copy sin voseo; sin estilos inline.

## Review Workload Forecast

| Campo | Valor |
|---|---|
| Líneas estimadas | ~2.200 (FE: provider + 2 modales + service + tests ≈ 860 · BE: controller + handler + entidad + infra + email + tests ≈ 1.340) |
| Riesgo 400 líneas | **High** |
| PRs encadenados | **Yes** |
| Entrega | `ask-on-risk` |

**Decision needed before apply**: Sí — (1) `WaitlistEntry` global vs `TenantArchitectureTests`; (2) envío de correo post-commit: fire-and-forget con catch+log vs cola en `Stockma.Worker`; (3) CORS si la landing publicada no comparte origen con la API.
**Chained PRs**: Yes (stacked sobre `main`, un work unit por PR).
**400-line budget risk**: High — respetar el presupuesto por PR; si una tarea lo excede, partirla.

## Suggested Work Units

| PR | Unidad | Contenido |
|---|---|---|
| PR-1 | Dominio + aplicación (TDD) | Entidad, comando/handler, DTOs, puertos, tests rojo→verde |
| PR-2 | API + interceptor + persistencia | Controller, catch-all, allowlist de tenant, repository + migración, tests de endpoint |
| PR-3 | Email Brevo + secretos | `IEmailSender`, opciones, `ValidateOnStart`, tests de sender y de "fallo no letal" |
| PR-4 | Frontend (TDD) | Servicio, provider, 2 modales, tests; conexión de CTAs y test modificado de LandingPage |

## Phase 1: Dominio y aplicación (TDD)

- [x] 1.1 `backend/tests/Stockma.Domain.Tests/WaitlistEntryTests.cs`: **RED** — ctor valida nombre (2-120), teléfono (7-15 dígitos) y email; normaliza `trim`+minúsculas; `Id` es GUID v7; email vacío lanza excepción de dominio.
- [x] 1.2 `backend/src/Stockma.Domain/Entities/WaitlistEntry.cs`: **GREEN** — entidad según 1.1 (decisión de namespace pendiente de design punto 1). + exclusión en `TenantArchitectureTests` (patrón `DoNotHaveName`, design decisión 1).
- [x] 1.3 `backend/tests/Stockma.Application.Tests/RegisterWaitlistEntryCommandTests.cs`: **RED** — happy path persiste vía doble fake y encola email con el nombre; duplicado lanza excepción `WAITLIST_EMAIL_DUPLICATE`; validación fallida `ValidationFailedException`; **fallo de email no revienta el registro**.
- [x] 1.4 `backend/src/Stockma.Application/Waitlist/`: **GREEN** — `RegisterWaitlistEntryCommand` + handler + `WaitlistEntryDto` + `IWaitlistRepository` + `IEmailSender` + `IEmailQueue` + excepciones (`WaitlistExceptions.cs`).
- [x] 1.5 refactor DRY (Opción A autorizada): `backend/src/Stockma.Domain/Waitlist/WaitlistRules.cs` — fuente única de reglas (nombre 2–120, teléfono 7–15, email ≤256, countryCode ≤8) consumida por entidad, handler, DataAnnotations y configuración EF. + invariantes nuevas email≤256 y countryCode≤8 (antes: correo largo = 500; ahora = 400). Tests: 2 nuevos en dominio, 1 en aplicación. Suite: 770/770.

## Phase 2: API, interceptor y persistencia (TDD)

- [x] 2.1 `backend/tests/Stockma.Api.Tests/WaitlistEndpointsTests.cs`: **RED** — `POST /api/waitlist` sin JWT ni `X-Tenant-ID` → 201 `{ id, email }`; duplicado → 409 `WAITLIST_EMAIL_DUPLICATE`; body inválido → 400 `VALIDATION_FAILED` con `errors`; sin stack trace en ningún error.
- [x] 2.2 `backend/tests/Stockma.Api.Tests/DomainExceptionMiddlewareTests.cs`: **RED/MODIFICADO** — `NonDomainException_IsNotSwallowed` pasa a esperar 500 `INTERNAL_ERROR` `problem+json` sin stack; nuevo caso de mapeo 409 waitlist.
- [x] 2.3 `backend/src/Stockma.Api/Controllers/WaitlistController.cs`: **GREEN** — `[AllowAnonymous]`, `ISender`, `201` + `Location`.
- [x] 2.4 `backend/src/Stockma.Api/Middleware/TenantMiddleware.cs`: agregar `/api/waitlist` a `UnauthenticatedPaths`.
- [x] 2.5 `backend/src/Stockma.Api/Middleware/DomainExceptionMiddleware.cs`: catch-all de excepciones no dominadas → 500 `INTERNAL_ERROR` (loguea la excepción real).
- [x] 2.6 `backend/tests/Stockma.Infrastructure.Tests/WaitlistRepositoryTests.cs`: **RED** — índice único de correo normalizado en Postgres real; violación 23505 se traduce a excepción de dominio.
- [x] 2.7 `backend/src/Stockma.Infrastructure/Waitlist/WaitlistRepository.cs` + `Persistence/Configurations/WaitlistEntryConfiguration.cs`: **GREEN** — traducción 23505 y configuración.
- [x] 2.8 `backend/src/Stockma.Infrastructure/Persistence/Migrations/`: generar migración `AddWaitlistEntries` (índice único sobre `lower(Email)`); verificar `dotnet ef database update`.

## Phase 3: Email Brevo y secretos (TDD)

- [x] 3.1 `backend/tests/Stockma.Infrastructure.Tests/BrevoEmailSenderTests.cs`: **RED** — envía con `templateId` (número) + `params.Name`; **el contenido vive EXCLUSIVAMENTE en la plantilla transaccional de la cuenta de Brevo — cero HTML en el código**; API que falla lanza error de envío (no de dominio); sin API key o sin TemplateId → error de configuración claro.
- [x] 3.2 `backend/src/Stockma.Infrastructure/Email/`: **GREEN** — `EmailSender` + `EmailOptions` (Brevo: ApiKey, FromEmail, FromName, **TemplateId obligatorio** — el de la plantilla que el usuario ya tiene en su cuenta —) con `AddHttpClient` y registro condicional siguiendo `AddSmsSender`.
- [x] 3.3 `backend/src/Stockma.Api/appsettings.json`: sección `Brevo` **sin valores**; `user-secrets set "Brevo:ApiKey" ...` en dev; env var `Brevo__ApiKey` en prod; `ValidateOnStart`.
- [x] 3.4 `backend/tests/Stockma.Application.Tests/RegisterWaitlistEntryCommandTests.cs`: **ampliar** — el handler encola SOLO destinatario + nombre (`EmailMessage` se achica: se eliminan Subject/HtmlBody, el contenido lo aporta la plantilla de Brevo) después de persistir; un fallo al encolar/enviar deja el `201` intacto y sólo loguea.
- [x] 3.5 `backend/tests/Stockma.Api.Tests/StartupValidationTests.cs`: verificar que `appsettings.json` sigue sin secretos reales (gate existente).

## Phase 4: Frontend (TDD)

- [x] 4.1 `frontend/web/src/services/waitlist-service.test.ts`: **RED** — `axios.post(\`${config.API_URL}/api/waitlist\`, payload, authConfig())` con payload correcto (el interceptor de `shared/http.ts` traduce la respuesta a `ApiError.errorCode`); usar `mockApi` de `src/shared/http.testkit.ts`.
- [x] 4.2 `frontend/web/src/services/waitlist-service.ts`: **GREEN** — clase `WaitlistService` (`export default`) que llama a `axios.post` directamente con `config.API_URL` + `authConfig()` (no usa `postJson`); inyecta `countryCode: '+57'` en un solo lugar.
- [x] 4.3 `frontend/web/src/features/landing/components/WaitlistModal/WaitlistModal.test.tsx`: **RED** — render con 3 campos; spinner + `disabled` durante envío; `400` marca campos; `409` muestra mensaje puntual sin cerrar; `500` mensaje genérico conservando datos; `201` dispara `onSuccess`; Escape y clic fuera cierran; `role="dialog"` + `aria-modal`.
- [x] 4.4 `frontend/web/src/features/landing/components/WaitlistModal/`: **GREEN** — `.tsx` + `.module.css` + `.styles.ts` (spinner, estados de error, sin estilos inline).
- [x] 4.5 `WaitlistSuccessModal.test.tsx` + `WaitlistSuccessModal/`: **RED→GREEN** — modal separada de confirmación; cierre por botón/Escape/fuera.
- [x] 4.6 `WaitlistModalProvider.test.tsx` + `WaitlistModalProvider/`: **RED→GREEN** — transición `form → success` sólo con `201`; `close()` resetea; una modal visible a la vez.

## Phase 5: Integración y gates

- [x] 5.1 `frontend/web/src/features/landing/components/{Hero,DemoSection,WaitlistSection}/`: conectar los 3 CTAs con `useOpenWaitlist().open()`; Pricing **sin cambios**.
- [x] 5.2 `frontend/web/src/features/landing/components/LandingPage/LandingPage.tsx`: envolver con `WaitlistModalProvider` y montar las modales.
- [x] 5.3 `frontend/web/src/features/landing/components/LandingPage/LandingPage.test.tsx`: **MODIFICAR** — los 3 CTAs abren el diálogo (reemplaza "CTA sin acción" y `queryByRole('dialog') === null` de 002); Pricing sigue sin acción.
- [x] 5.4 Mapeo `errorCode` → mensaje en pantalla (constantes de error en el componente, copy neutro sin voseo).
- [x] 5.5 Gates: `dotnet test Stockma.sln`, `npm run test`, `npm run typecheck`, `npm run lint`, `scripts/check-neutral-es.mjs`, `no-inline-styles.test.ts`.
- [x] 5.6 Documentación: actualizar `specs/003-waitlist/spec.md` criterios de aceptación y README de estructura de specs si aplica.
