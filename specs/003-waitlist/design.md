# Diseño: Registro de Lista de Espera (Spec 003)

> Basado en `proposal` (#696) y `spec` (#698) de Engram. Presupuesto: <800 palabras.

## Enfoque técnico

Flujo vertical por capas siguiendo el patrón existente de Products: `Controller → MediatR → Handler → Puerto (Application) → Infrastructure`. El frontend suma un provider de contexto + 2 modales + servicio sobre `postJson`. El interceptor global se refina en el middleware existente. Todo TDD, 4 PRs encadenados (ver `tasks.md`).

## Decisiones de arquitectura

### Decisión 1: Ubicación de `WaitlistEntry` vs `TenantArchitectureTests`

| Opción | Tradeoff | Decisión |
|---|---|---|
| (a) Mantener en `Stockma.Domain.Entities` + excluir por nombre en el test (patrón `IsolationRoots`/`DoNotHaveName` ya existente) | Excepción explícita y revisable, con comentario de motivo | ✅ **Elegida** |
| (b) Namespace nuevo (`Stockma.Domain.Waitlist`) | El test no se toca, pero esconde la excepción y dispersa entidades globales | Descartada |
| (c) Hacerla `ITenantEntity` con tenant ficticio | Rompe el sentido: la lista es pública, no pertenece a un tenant | Descartada |

`ApplyTenantFilters` (`StockmaDbContext.cs:41`) sólo aplica a `ITenantEntity` → sin interfaz, el filtro y RLS quedan fuera automáticamente. La migración **NO** habilita RLS en `waitlist_entries` (patrón: RLS se habilita por tabla donde hay política de tenant).

### Decisión 2: Envío asíncrono del correo (post-commit)

| Opción | Tradeoff | Decisión |
|---|---|---|
| (a) `Task.Run` fire-and-forget en el handler | Simple, pero pierde scope de DI, excepciones no observadas, cero control | Descartada |
| (b) Puerto `IEmailQueue` (Channel) + `BackgroundService` en la API | Enqueue síncrono barato tras `SaveChanges`, procesamiento async con scope propio, testeable en Application | ✅ **Elegida** |
| (c) Outbox en tabla + `Stockma.Worker` con reintentos | Duradero y con retry, pero tabla + job nuevos desproporcionados para una lista de espera | Futuro, si el correo se vuelve crítico |

El handler sólo hace `SaveAsync` → `Enqueue`; el `BackgroundService` envía y traga errores con log. Cumple "fallo de correo nunca letal" por construcción.

### Decisión 3: Catch-all en `DomainExceptionMiddleware`

| Opción | Tradeoff | Decisión |
|---|---|---|
| (a) Extender `DomainExceptionMiddleware` con `catch (Exception)` → `500 INTERNAL_ERROR` vía `ProblemResponses` | Un solo punto, patrón y harness de tests existentes | ✅ **Elegida** |
| (b) `IExceptionHandler` global de .NET | Estándar del framework, pero reparte el manejo en dos sitios y rompe el test existente de otra forma | Descartada |

Modifica `NonDomainException_IsNotSwallowed` (tarea 2.2): ahora espera `500` + `errorCode`, sin stack trace.

### Decisión 4: Estado de modales en el frontend

Provider único (`WaitlistModalProvider`) con máquina de estados `closed → form → success`: los 3 CTAs viven en componentes hermanos lejanos (Hero, DemoSection, WaitlistSection) — el contexto evita prop-drilling; `success` sólo es alcanzable desde `form` con `201`.

### Decisión 5: CORS (deferido)

Sin cambios en 003: en dev lo resuelve el proxy de Vite. **Trigger**: si en producción la landing se sirve de otro origen que la API, agregar `AddCors` con `Cors:AllowedOrigins` de configuración. Queda como pregunta abierta según el deploy real.

## Flujo de datos

```
CTA (Hero|Demo|WaitlistSection)
  └─→ useOpenWaitlist().open() ──→ Provider (fase: form)
        └─→ WaitlistModal.submit ──→ waitlist-service ──→ postJson
              └─→ POST /api/waitlist (público) ──→ WaitlistController
                    └─→ MediatR: RegisterWaitlistEntryCommand
                          ├─→ Handler: valida → IWaitlistRepository.SaveAsync (Postgres, índice único lower(email))
                          ├─→ IEmailQueue.Enqueue (post-commit)
                          └─→ 201 {id,email} ──→ Provider (fase: success) ──→ WaitlistSuccessModal
BackgroundService ──→ IEmailQueue ──→ IEmailSender (SendGrid) ──→ correo con nombre ──→ fallo: log, nada más

errores: ModelState / DomainException / Exception
  └─→ DomainExceptionMiddleware ──→ problem+json {errorCode} ──→ WaitlistModal muestra mensaje puntual
```

## Cambios de archivos

| Archivo | Acción | Descripción |
|---|---|---|
| `Stockma.Domain/Entities/WaitlistEntry.cs` | Create | Entidad global, GUID v7, normaliza email |
| `Stockma.Domain/Entities/TenantArchitectureTests` (test) | Modify | Excluir `WaitlistEntry` por nombre con motivo |
| `Stockma.Application/Waitlist/{RegisterWaitlistEntryCommand,Handler,WaitlistEntryDto,IWaitlistRepository,IEmailSender,IEmailQueue,WaitlistExceptions}.cs` | Create | Caso de uso + puertos |
| `Stockma.Api/Controllers/WaitlistController.cs` | Create | `[AllowAnonymous]`, `201` |
| `Stockma.Api/Middleware/DomainExceptionMiddleware.cs` | Modify | Catch-all → `500 INTERNAL_ERROR` |
| `Stockma.Api/Middleware/TenantMiddleware.cs` | Modify | `/api/waitlist` en `UnauthenticatedPaths` |
| `Stockma.Api/Program.cs` | Modify | Registrar `IEmailQueue` + `BackgroundService` |
| `Stockma.Infrastructure/Waitlist/WaitlistRepository.cs` | Create | 23505 → excepción de dominio |
| `Stockma.Infrastructure/Email/{EmailSender,EmailOptions,EmailQueue,EmailBackgroundService}.cs` | Create | SendGrid + `ValidateOnStart` (patrón `SmsOptions`) |
| `Stockma.Infrastructure/Persistence/Configurations/WaitlistEntryConfiguration.cs` + `Migrations/*_AddWaitlistEntries.cs` | Create | Tabla + índice único `lower(email)`; sin RLS |
| `Stockma.Api/appsettings.json` | Modify | Sección `SendGrid` vacía (gate de secretos) |
| `frontend .../components/{WaitlistModal,WaitlistSuccessModal,WaitlistModalProvider}/` | Create | Modales + contexto (`.module.css`, `.styles.ts`) |
| `frontend .../services/waitlist-service.ts` | Create | Wrapper de `postJson` |
| `frontend .../components/{Hero,DemoSection,WaitlistSection,LandingPage}/` | Modify | Conexión de CTAs + test modificado |

## Interfaces / Contratos

```csharp
// Application — puertos
public interface IEmailSender { Task SendAsync(EmailMessage message, CancellationToken ct = default); }
public sealed record EmailMessage(string ToEmail, string ToName, string Subject, string HtmlBody);
public interface IEmailQueue { void Enqueue(EmailMessage message); }
public interface IWaitlistRepository { Task AddAsync(WaitlistEntry entry, CancellationToken ct = default); }
```

Contrato HTTP completo: `contracts/waitlist-api.md`. Contrato React: `contracts/components-waitlist.md`.

## Estrategia de testing

| Capa | Qué | Cómo |
|---|---|---|
| Unit | Valida entidad, handler (duplicado/inválido/correo no letal), sender | xUnit + FluentAssertions + dobles a mano (sin Moq) |
| Integration | Endpoint 201/409/400/500 público; índice único real; catch-all | `StockmaApiFactory` (Testcontainers `postgres:16`) |
| Frontend | Servicio, spinner, transición form→success, errores 400/409/500, CTAs | Vitest + RTL + `mockApi` (axios adapter) |
| Arquitectura | Exclusión de `WaitlistEntry` | `TenantArchitectureTests` modificado |

Detalle y orden (rojo→verde): `tasks.md` fases 1-5.

## Migración / Rollout

1. `dotnet ef database update --project src/Stockma.Infrastructure --startup-project src/Stockma.Infrastructure` (manual, patrón del repo).
2. Secrets **antes** de desplegar: `SendGrid__ApiKey` (env) + remitente verificado — `ValidateOnStart` bloquea el arranque si faltan, consistente con `Jwt`/`Sms`.
3. Sin feature flag: los CTAs ya están visibles desde la 002; el PR-4 los conecta.
4. Rollback: revert de los 4 PRs; la tabla nueva no estorba a versiones anteriores.

## Preguntas abiertas

- [ ] Deploy real de la landing: ¿comparte origen con la API? (define si aplica CORS — Decisión 5)
- [ ] `EmailOptions`: ¿plantilla SendGrid (TemplateId) o HTML embebido en el backend? Recomendación: HTML embebido por simplicidad en 003.
