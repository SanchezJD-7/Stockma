# Registro de decisiones — identidad, sesión y trazabilidad

Este archivo guarda **por qué** Stockma está construido así. La spec dice QUÉ construir; esto dice **qué NO deshacer, y bajo qué condición sí**.

Cada decisión trae las alternativas que se evaluaron y se descartaron. Si una te parece molesta, leé primero la sección "Qué la devolvería a discusión" antes de cambiarla: es probable que ya la hayamos discutido.

## Índice

| # | Decisión | Tareas |
|---|---|---|
| [ADR-001](#adr-001--el-email-es-único-en-toda-la-plataforma) | El email es único en toda la plataforma | T055 |
| [ADR-002](#adr-002--el-login-queda-exento-del-tenantmiddleware) | El login queda exento del `TenantMiddleware` | T055a–c |
| [ADR-003](#adr-003--el-alta-del-celular-la-hace-un-admin-el-cambio-lo-hace-el-usuario) | Alta del celular por admin, cambio por el usuario | T050, T061 |
| [ADR-004](#adr-004--usuario-sin-celular-cargado-se-bloquea-nunca-se-saltea-el-2fa) | Usuario sin celular: bloqueo, nunca bypass | T050, T064 |
| [ADR-005](#adr-005--el-admin-de-plataforma-vive-en-una-superficie-separada) | Admin de plataforma en superficie separada | T062, T063 |
| [ADR-006](#adr-006--refresh-token-con-rotación-y-detección-de-reuso) | Refresh token con rotación y detección de reuso | T065 |
| [ADR-007](#adr-007--los-dispositivos-caducan-por-expiresat-no-por-revokedat) | Los dispositivos caducan por `ExpiresAt` | T070 |
| [ADR-008](#adr-008--revocar-un-dispositivo-no-es-dar-de-baja-a-una-persona) | Revocar ≠ deshabilitar | T067, T068 |
| [ADR-009](#adr-009--la-recuperación-de-contraseña-va-por-sms-no-por-email) | Reset de contraseña por SMS, no por email | T069 |
| [ADR-010](#adr-010--el-control-sobre-el-stock-es-atribución-no-prohibición) | Trazabilidad de stock: atribución, no prohibición | T066 |
| [ADR-011](#adr-011--maxtrusteddevices-se-queda-en-2) | `MaxTrustedDevices` se queda en 2 | T070 |
| [ADR-012](#adr-012--applicationuser-vive-en-infrastructure-no-en-domain) | `ApplicationUser` vive en Infrastructure | T015 |
| [ADR-013](#adr-013--la-rls-sobre-users-y-la-única-excepción-del-login) | RLS sobre `users` + función del login | T015, T055b |

---

## Los tres principios que sostienen todo

Casi todas las decisiones de abajo son aplicaciones de uno de estos tres. Si entendés estos, el resto se deduce.

| Principio | Qué significa |
|---|---|
| **Un factor no se autoenrola** | Si con la contraseña se decide a dónde llegan los códigos, quien tenga la contraseña controla ambos factores. Eso es 1FA con un paso extra. |
| **Un filtro con excepciones deja de ser una garantía** | El aislamiento entre tenants vale por lo que prohíbe **siempre**. Una excepción "por conveniencia" lo desactiva en silencio en todas las rutas donde nadie miró. |
| **El control es atribución, no prohibición** | Prohibir la acción incómoda no la evita: evita que quede registrada. Es peor no saber que saber y no haber impedido. |

---

## ADR-001 — El email es único en toda la plataforma

**Estado**: decidido · **Tareas**: T055

### Contexto

Ya estaba decidido que el login es genérico: `stockma.app/login`, con el logo de Stockma y **sin tenant en la URL**. Con el email único *por tenant*, el mismo correo puede pertenecer a varias droguerías y el sistema no tiene forma de saber a cuál entrar.

### Alternativas evaluadas

| Opción | Por qué se descartó |
|---|---|
| Tenant en la URL (`stockma.app/{slug}/login`) | Contradice el login genérico ya decidido |
| Campo "código de tenant" en el formulario | Fricción en cada login, y el usuario no se acuerda del código |
| **Email único global** ✅ | — |

### Consecuencia aceptada

Una persona **no puede** tener cuenta en dos tenants con el mismo correo.

### Qué la devolvería a discusión

Que aparezca la necesidad real de una persona operando en varias droguerías. La salida entonces es una tabla `UserTenant` con selección post-login (modelo Slack) — un refactor de Identity, **no** un campo más.

### Simplificaciones que trajo

- Desaparece el índice compuesto `(TenantId, NormalizedEmail)`: se usa el default de ASP.NET Core Identity y la migración no toca nada.
- Se acota el riesgo de interferencia del `HasQueryFilter` con `SignInManager`: el login corre sin `TenantContext`, así que no hay ambigüedad.

---

## ADR-002 — El login queda exento del `TenantMiddleware`

**Estado**: decidido · **Tareas**: T055a, T055b, T055c

### Contexto

Corolario directo de ADR-001. Si el email resuelve el tenant, el login no puede exigir el header `X-Tenant-ID` — todavía no sabe cuál es.

### Las tres reglas que la hacen segura

Esta es la **única** excepción al aislamiento entre tenants en todo el sistema. Por eso viene con candados:

| Regla | Por qué |
|---|---|
| La exención es **sólo** para `POST /api/auth/login`, con test de que ninguna otra ruta quedó exenta | Una exención sin test se expande sola |
| La búsqueda usa `IgnoreQueryFilters()` **explícito y acotado**: una consulta, leyendo sólo lo necesario para autenticar y obtener el `TenantId` | Es el único punto que cruza la frontera a propósito |
| `401 AUTH_INVALID_CREDENTIALS` **idéntico** para email inexistente, contraseña incorrecta y usuario de otro tenant | Distinguirlos permite enumerar qué correos usan Stockma |

### La regla que nunca hay que romper

> El filtro global **NO DEBE** volverse permisivo cuando el `TenantContext` está vacío.

Es tentador: hacés que el filtro "no aplique si no hay contexto" y el login funciona sin excepciones especiales. Y desactivás el aislamiento **en silencio** en toda ruta donde el contexto no se haya poblado por un bug, un job en background o un endpoint nuevo.

La excepción va en el punto de uso. Nunca como default del filtro.

---

## ADR-003 — El alta del celular la hace un admin, el cambio lo hace el usuario

**Estado**: decidido · **Tareas**: T050 (alta), T061 (cambio)

### Contexto

El OTP del 2FA viaja por SMS al celular de cada usuario. La pregunta es quién decide cuál es ese número.

### La distinción que resuelve todo

**Alta y cambio no son el mismo acto y no se gobiernan igual.**

| | Quién | Por qué es seguro |
|---|---|---|
| **Alta** del primer número | Sólo un admin del tenant | En ese momento hay **un solo** factor. Si con él se decide el destino del OTP, quien tenga la contraseña controla ambos |
| **Cambio** de uno existente | El propio usuario, con OTP al número **ACTUAL** | Exige demostrar posesión del canal vigente. El atacante con la contraseña robada no tiene el celular viejo |

### Alternativas evaluadas

| Opción | Por qué se descartó |
|---|---|
| Un solo celular por tenant (el del dueño) | El dueño se vuelve cuello de botella; el código no identifica quién entró |
| Celular auto-registrable por el usuario | Cuenta comprometida → el atacante cambia su número → se autoenvía el OTP |
| **TOFU** (enrolar en el primer login, bloqueado después) | Cierra el ataque de redirección, pero abre la carrera: si el atacante llega a la credencial inicial antes que el usuario, enrola SU número y queda dueño para siempre |
| **Alta por admin + cambio con OTP al viejo** ✅ | — |

### Por qué TOFU perdió, en una línea

El admin **ya** crea al usuario y **ya** le entrega la contraseña inicial. Ese canal existe igual. Pedirle el número en el mismo acto no agrega ningún paso — y elimina la carrera. TOFU costaba una ventana de ataque a cambio de nada.

### Qué la devolvería a discusión

Que la entrega de la credencial inicial deje de pasar por el admin (por ejemplo, auto-registro con invitación por link). Ahí el argumento se cae y TOFU vuelve a la mesa.

### Fricción operativa: resuelta

La preocupación era *"¿el cliente me tiene que llamar cada vez que quiere cambiar un número?"*. No:

| Quién cambia su número | Quién lo hace | ¿Escala al admin de plataforma? |
|---|---|---|
| Un empleado cualquiera | El admin **de su tenant** | No |
| El admin del tenant | Él mismo, vía T061 | No |

---

## ADR-004 — Usuario sin celular cargado: se bloquea, nunca se saltea el 2FA

**Estado**: decidido · **Tareas**: T050, T064

### Decisión

Un usuario sin `PhoneNumber` que intenta entrar desde un dispositivo no confiable recibe **`403 AUTH_PHONE_NOT_ENROLLED`** y no ingresa.

### Por qué no se permite pasar "sólo esa vez"

Porque convierte **"no cargar el número"** en un bypass permanente del segundo factor. Cualquiera que quiera evitar el 2FA sólo tiene que asegurarse de que su número nunca se cargue.

### Consecuencia de diseño

Por eso T064 exige que el alta de usuario incluya el `PhoneNumber` **en el mismo acto**: un usuario creado sin número queda inservible hasta que el admin lo complete.

---

## ADR-005 — El admin de plataforma vive en una superficie separada

**Estado**: decidido · **Tareas**: T062 (roles del tenant), T063 (admin de plataforma)

### Contexto

`ApplicationUser.TenantId` es obligatorio y hay filtro global por tenant. El dueño de la plataforma, que por definición **no pertenece a ningún tenant**, no tiene lugar donde existir.

### Alternativas evaluadas

| Opción | Por qué se descartó |
|---|---|
| `TenantId` nullable + exención del filtro para ese rol | Vuelve el filtro permisivo — exactamente lo que prohíbe ADR-002 |
| Tenant "de sistema" con `Guid` conocido | El filtro le sigue aplicando, así que no resuelve el problema real: seguiría sin poder operar sobre otros tenants |
| **Superficie separada** (`/api/platform/*`) ✅ | — |

### Por qué

Todo el aislamiento —filtro EF, RLS, test de arquitectura— se apoya en una invariante: **todo `ApplicationUser` pertenece a exactamente un tenant**. Un super-usuario exento la rompe, y con ella la garantía.

Cuesta más trabajo. No tiene excepciones.

### Roles del tenant (T062)

`TenantAdmin` / `Member`, claim `role` en el JWT, policy sobre `/api/admin/*`.

Esto **no es una mejora, es un bloqueante**: toda la superficie de seguridad ya escrita dice "sólo admin del tenant" apoyada en un rol que no existía en el modelo. Sin él, "sólo admin" no se implementa: se simula.

Dos invariantes que evitan un desastre operativo:
- El **primer usuario** de un tenant nace `TenantAdmin`. No puede haber un tenant sin admin.
- El **último admin** no puede degradarse ni deshabilitarse a sí mismo.

---

## ADR-006 — Refresh token con rotación y detección de reuso

**Estado**: decidido · **Tareas**: T065

### El problema que destapó esta decisión

Se propuso eliminar los dispositivos confiables y exigir OTP en **cada** login. Al ir a verificarlo apareció que **no existe refresh token** y el JWT dura ≤ 60 min.

| | Sin refresh | Con refresh |
|---|---|---|
| Logins por turno de 8 h | **8** | **1** |
| SMS/mes por droguería de 5 empleados, con OTP por login | ~1.200 | ~150 |

Y algo peor que el costo: sin refresh, **el SMS es punto único de falla**. Proveedor demorado, mostrador parado.

La pieza que faltaba no eran los dispositivos confiables. Era el refresh.

### Decisiones

| Aspecto | Decisión |
|---|---|
| Vigencia | **8 h**, **absoluta** desde el login (no deslizante) → 1 login por turno |
| Almacenamiento | Cookie `httpOnly` + `Secure` + `SameSite=Strict` |
| Access token | Sin cambios: ≤ 60 min, sigue en `localStorage` |

**Por qué la cookie**: el refresh es la credencial de larga vida y renovable. En `localStorage`, cualquier XSS —una dependencia npm comprometida alcanza— se lleva la sesión completa. JavaScript no puede leer una cookie `httpOnly`. El access token de 60 min puede quedarse donde está: es corto y no renueva nada por sí solo.

### La propiedad que importa: detección de reuso

> Si se presenta un refresh con `ConsumedAt != null`, se revoca **toda la familia**.

Un token usado dos veces sólo puede significar que **alguien tiene una copia**, y no hay forma de saber si es el legítimo o el ladrón. Se cae la sesión para los dos: el legítimo vuelve a loguearse, el ladrón se queda con un token muerto.

Es lo que convierte un refresh token de "credencial colgada por ahí" en algo que **avisa cuando lo roban**.

### Consecuencia aceptada

Con 8 h absolutas, quien empalma dos turnos o hace horas extra vuelve a loguearse en medio de la jornada.

### Qué la devolvería a discusión

Si el mostrador se queja, la salida es **vigencia deslizante con tope absoluto**, no un número más grande. Deslizante mantiene la sesión mientras haya actividad y mata la abandonada.

### Reevaluación pendiente

Con 1 login por turno, exigir OTP por sesión cuesta ~150 SMS/mes en vez de ~1.200. **Ahí hay que decidir si `TrustedDevice` sigue haciendo falta** — eliminarlo se llevaría también `MaxTrustedDevices`, la caducidad (ADR-007) y buena parte de T067.

---

## ADR-007 — Los dispositivos caducan por `ExpiresAt`, no por `RevokedAt`

**Estado**: decidido · **Tareas**: T070

### El agujero que cerró

`TrustedDevice` no tenía campo de expiración, y un invariante del modelo decía:

> Ninguna ruta automática PUEDE revocar un dispositivo

Resultado: un dispositivo quedaba confiable **para siempre**, y la caducidad estaba literalmente prohibida por la spec.

### Decisión

Campo nuevo `ExpiresAt`. **`RevokedAt` no se toca.**

```
activo = RevokedAt IS NULL AND ExpiresAt > now()
```

`TenantSettings.TrustedDeviceLifetimeDays`, default **15**.

### Por qué dos campos y no uno

`RevokedAt` significa **"una persona lo dio de baja"**. Es auditoría. Si le escribís ahí el vencimiento automático, perdés la diferencia entre *lo revocaron* y *se venció solo* — que es justamente la información por la que existe el registro.

### Alcance: qué hace y qué no

| Hace | No hace |
|---|---|
| Obliga a rehacer el 2FA en ese dispositivo | Cortar la sesión viva |
| Libera el slot de `MaxTrustedDevices` solo | Bloquear el acceso |

Cortar es inmediato y es otra cosa: ADR-008.

**La caducidad es la red de seguridad para lo que nadie se acordó de revocar.** No es el control urgente.

---

## ADR-008 — Revocar un dispositivo no es dar de baja a una persona

**Estado**: decidido · **Tareas**: T067 (revocación), T068 (deshabilitar)

### La confusión que hay que evitar

> "El empleado renuncia el viernes: le revoco el dispositivo y listo."

**No.** Conserva su contraseña y su celular, así que vuelve a entrar — sólo que ahora con OTP. **Revocar un dispositivo no le saca el acceso: le agrega un paso.**

| Necesidad | Qué la resuelve |
|---|---|
| "No quiero que este aparato saltee el 2FA" | **Revocar** (T067) |
| "No quiero que esta persona entre nunca más" | **Deshabilitar** (T068) |
| "Que se caiga solo lo que nadie revocó" | **Caducar** (ADR-007) |

### T067 — Revocación

Superficie admin **y superficie propia** (`/api/auth/devices`). Que cada uno vea sus dispositivos no es comodidad: es **cómo se detecta uno desconocido**. El admin no mira sesiones ajenas todos los días; el dueño de la cuenta sí.

> Revocar un dispositivo DEBE revocar también las familias de refresh asociadas.

Sin eso, `RevokedAt` sólo impide saltear el 2FA a futuro mientras la sesión viva sigue andando hasta 8 h. **Revocar sin cerrar la sesión es teatro.**

### T068 — Deshabilitar (el offboarding real)

Usa `LockoutEnabled` / `LockoutEnd`, que Identity ya trae. Y hace tres cosas **en un solo acto**:

1. Rechaza el login con `401` uniforme
2. Revoca **todas** las familias de refresh
3. Revoca **todos** los `TrustedDevice`

Un offboarding a medias no es un offboarding. Cortar el login y dejar la sesión viva regala 8 horas de acceso justo el día que más importa.

---

## ADR-009 — La recuperación de contraseña va por SMS, no por email

**Estado**: decidido · **Tareas**: T069

### Por qué no email

Sería incoherente. El 2FA por email **se descartó explícitamente**: el email es el identificador de login, nunca un canal de confianza.

> **El canal débil no puede ser la puerta de atrás del canal fuerte.**

### Decisión

OTP por SMS al `PhoneNumber` enrolado. Reusa la infraestructura de T018 — no hay ningún canal nuevo que asegurar.

**Fallback**: `POST /api/admin/users/{userId}/reset-password` iniciado por un `TenantAdmin`, para quien perdió el celular **y** la contraseña. Sin eso queda encerrado afuera para siempre.

### Las dos reglas que se suelen olvidar

| Regla | Por qué |
|---|---|
| Respuesta **uniforme** exista o no el email | Igual que ADR-002: distinguir permite enumerar qué correos usan Stockma |
| Un reset exitoso revoca **todas** las familias de refresh | Si el atacante ya estaba adentro, cambiar la contraseña sin cortarle la sesión **no lo echa** |

Además: el reset **no saltea el 2FA**. Entrar desde un dispositivo nuevo sigue exigiendo OTP.

---

## ADR-010 — El control sobre el stock es atribución, no prohibición

**Estado**: decidido · **Tareas**: T066

### El agujero que cerró

`POST /api/batches/{id}/adjust` recibía **sólo** `{ "delta": -5 }`. Sin motivo, sin autor, sin rol.

El contrato decía que la trazabilidad la daba el `AuditSaveChangesInterceptor`. **Ese interceptor no audita**: sólo aborta el `SaveChanges` si se intenta modificar `TenantId`. Está mal nombrado, y el nombre hacía creer que existía un rastro que no existía — la peor combinación posible: un hueco que se ve tapado.

En una droguería el faltante no entra por el login. Entra por el ajuste.

### Decisión

`StockMovement`, **append-only**: `BatchId`, `Delta`, `Reason`, `Notes?`, `UserId`, `OccurredAt`.

| Regla | Por qué |
|---|---|
| Todo cambio de `CurrentQuantity` nace de un `StockMovement` | No puede existir camino que mueva stock sin dejar fila |
| `UserId` sale del claim `sub`, **nunca del body** | Si el cliente manda el autor, el autor no significa nada |
| La fila es inmutable: corregir es **otro** movimiento compensatorio | Un libro que se puede editar no es un libro |
| `Reason` obligatorio | Un delta sin motivo no dice nada dentro de un mes |

### Por qué NO se le prohíbe la merma al empleado

Va contra el instinto, y es la parte importante.

Si le prohibís al empleado registrar una merma, **no evitás la merma: evitás el registro**. Se rompió un frasco, anotarlo requiere molestar al jefe, entonces no se anota. A fin de mes el inventario no cuadra y nadie sabe por qué.

Entonces: el `Member` **puede** registrar mermas. El rol interviene sólo por encima de `MermaApprovalThreshold`, donde se exige `TenantAdmin`.

**La gente no roba menos porque le saques el botón. Roba menos cuando sabe que su nombre queda pegado al movimiento** — y que el admin lo ve en un reporte por usuario y motivo.

---

## ADR-011 — `MaxTrustedDevices` se queda en 2

**Estado**: decidido · **Tareas**: T070

### Contexto

Se propuso subirlo a 4: una empleada con la PC del mostrador + su celular ya está en el tope, y un tercer aparato le pediría OTP cada vez.

### Por qué se descartó subirlo

Los slots no se llenan por uso **simultáneo**. Se llenan por **acumulación**: la PC que se cambió, el celular que se rompió, siguen ocupando lugar para siempre.

Subir el número trataba el síntoma. **La caducidad (ADR-007) trata la causa** y libera el slot sola.

### Y el costo de quedarse corto es reversible

| | Costo |
|---|---|
| Límite bajo | Fricción: más OTP. **Nunca bloquea el acceso** — está garantizado en FR-007 |
| Límite alto | Más superficie donde un aparato robado entra sin segundo factor |

Asimétrico: conviene errar por abajo. Es configurable por tenant, así que si las droguerías se quejan se sube **sabiendo por qué**.

---

## ADR-012 — `ApplicationUser` vive en Infrastructure, no en Domain

**Estado**: decidido al implementar · **Tareas**: T015

### Contexto

T015 decía "Domain: `ApplicationUser`, `TrustedDevice`, `DeviceOtp`". Al escribirlo apareció que
`Stockma.Domain.csproj` **no tiene ni una referencia a paquetes**: es dominio puro. `IdentityUser<T>`
arrastra `Microsoft.AspNetCore.Identity`.

### Decisión

| Entidad | Dónde | Por qué |
|---|---|---|
| `TrustedDevice`, `DeviceOtp` | `Stockma.Domain/Entities/` | Dominio puro. El test de arquitectura obliga a que implementen `ITenantEntity` |
| `ApplicationUser`, `ApplicationRole` | `Stockma.Infrastructure/Identity/` | Traen dependencia de framework |

`ApplicationUser` **sí** implementa `ITenantEntity`, así que el filtro global de EF lo alcanza igual.

### Consecuencia

Meterlo en Domain habría cambiado una propiedad estructural del proyecto —el dominio no depende de
nada— por comodidad de ubicación. La spec describía la intención; la arquitectura del repo mandó.

---

## ADR-013 — La RLS sobre `users` y la única excepción del login

**Estado**: decidido al implementar · **Tareas**: T015, T055b

### El problema que apareció al escribir la migración

La política RLS del proyecto es **fail-closed**: sin `app.tenant` seteada no devuelve **ninguna** fila.

Y por ADR-002, el login corre **sin contexto de tenant**.

> `IgnoreQueryFilters()` esquiva el filtro de **EF**. **No** esquiva la RLS, que vive en Postgres.

Con RLS sobre `users`, el login no encuentra a nadie. Nunca.

### Alternativas evaluadas

| Opción | Por qué se descartó |
|---|---|
| No poner RLS sobre `users` | Deja la tabla más sensible del sistema con una sola capa de defensa |
| Política permisiva cuando `app.tenant` está vacía | Es exactamente lo que ADR-002 prohíbe: desactiva el aislamiento en silencio en toda ruta sin contexto |
| **Función `SECURITY DEFINER` acotada** ✅ | — |

### Decisión

RLS sobre `users`, `trusted_devices` y `device_otps`, más una función que es la **única** puerta:

```sql
auth_find_user_by_email(p_normalized_email text)
RETURNS TABLE (id, tenant_id, password_hash, security_stamp, lockout_end, lockout_enabled)
SECURITY DEFINER
```

Está acotada **por construcción**, no por disciplina:

- Devuelve como máximo **una** fila, la del email exacto
- Devuelve **sólo** las columnas para autenticar y resolver el tenant. No expone teléfono ni email
- No acepta filtros arbitrarios: la firma es un único email normalizado
- `REVOKE ALL FROM PUBLIC` + `GRANT EXECUTE TO app_user`

Un test verifica la lista exacta de columnas expuestas: **agregar una amplía el único agujero
deliberado del aislamiento, y el test obliga a que sea un acto consciente.**

### Qué la devolvería a discusión

Nada la elimina; sí puede cambiar de forma. Si el login llegara a necesitar más datos del usuario,
la respuesta correcta es una segunda consulta **ya con el tenant resuelto**, no ampliar la función.

---

## Decisiones que siguen abiertas

| Tema | Estado |
|---|---|
| Tope de intentos **por código** de OTP | Sin tarea. El rate limiting es por IP, está escrito sólo para `/login` y no para `confirm-device`, y NFR-005 dice "DEBERÍA" |
| Alta de tenants | Sin tarea. Hoy el seed es un `INSERT` manual: no se puede dar de alta una droguería sin tocar la base |
| Proveedor de SMS | Sin elegir (T049) |
| ¿Sobrevive `TrustedDevice`? | A reevaluar después de T065 — ver ADR-006 |
