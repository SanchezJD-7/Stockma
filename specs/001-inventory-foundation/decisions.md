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
| [ADR-014](#adr-014--register-cerrado-antes-de-tiempo-y-bootstrap-cli-como-parche) | `register` cerrado antes de tiempo + bootstrap CLI | T064 |
| [ADR-015](#adr-015--autenticación-por-defecto-y-el-tenant-atado-al-token) | Autenticación por defecto y el tenant atado al token | T073 |
| [ADR-016](#adr-016--endurecimiento-del-login-enumeración-fuerza-bruta-del-otp-y-carreras) | Endurecimiento del login: enumeración, fuerza bruta del OTP y carreras | T074–T078 |
| [ADR-017](#adr-017--la-rls-tiene-que-aplicar-en-runtime-rol-separado-force-y-chequeo-de-arranque) | La RLS tiene que aplicar en runtime: rol separado, `FORCE` y chequeo de arranque | T079–T083 |
| [ADR-018](#adr-018--refresh-token-sha-256-familia-atada-al-dispositivo-y-reuso-que-corta-la-sesión) | Refresh token: SHA-256, familia atada al dispositivo y reuso que corta la sesión | T065 |

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

### La exención cubre DOS rutas, no una

Al implementar T055b apareció que el flujo no cerraba. `confirm-device` ocurre **antes** de que exista un token: el cliente nunca supo su tenant, porque la respuesta del login (`requiresDeviceConfirmation`) no se lo dice. Con el header obligatorio, **`confirm-device` era inalcanzable**.

| Opción | Por qué se descartó |
|---|---|
| El login devuelve el `tenantId` y el cliente lo repite | Expone el tenant a un llamador sin autenticar y le deja elegir el valor |
| Un "ticket de confirmación" opaco y efímero | Maquinaria nueva para un problema que el OTP ya resuelve: el OTP **ya** es un secreto no adivinable atado a (usuario, dispositivo) |
| **Eximir también `confirm-device`** ✅ | — |

La propiedad de seguridad no cambia: sigue habiendo **una sola** consulta acotada y auditada (`auth_find_user_by_email`), usada por las dos rutas. Lo que cambia es que el test de T055a ahora fija una lista de **exactamente dos** rutas exentas, en vez de una.

### Las tres reglas que la hacen segura

Esta es la **única** excepción al aislamiento entre tenants en todo el sistema. Por eso viene con candados:

| Regla | Por qué |
|---|---|
| La exención es **sólo** para `POST /api/auth/login` y `POST /api/auth/confirm-device`, con test de que la lista es exactamente esa | Una exención sin test se expande sola |
| La búsqueda usa `IgnoreQueryFilters()` **explícito y acotado**: una consulta, leyendo sólo lo necesario para autenticar y obtener el `TenantId` | Es el único punto que cruza la frontera a propósito |
| `401 AUTH_INVALID_CREDENTIALS` **idéntico** para email inexistente, contraseña incorrecta y usuario de otro tenant | Distinguirlos permite enumerar qué correos usan Stockma |

### La regla que nunca hay que romper

> El filtro global **NO DEBE** volverse permisivo cuando el `TenantContext` está vacío.

Es tentador: hacés que el filtro "no aplique si no hay contexto" y el login funciona sin excepciones especiales. Y desactivás el aislamiento **en silencio** en toda ruta donde el contexto no se haya poblado por un bug, un job en background o un endpoint nuevo.

La excepción va en el punto de uso. Nunca como default del filtro.

### Y después de resolver, aislamiento completo

Resuelto el tenant, el handler **puebla el `ITenantContext`** y todo lo que sigue —emitir el OTP, consultar dispositivos confiables— pasa por el filtro de EF y la RLS como cualquier otra ruta. La excepción se limita a **la búsqueda**: `Login_ActivatesTheResolvedTenantForTheRestOfTheRequest` lo verifica.

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

### Consecuencia en el índice: varias filas por dispositivo

Si `RevokedAt` y `ExpiresAt` conservan su significado, un mismo aparato acumula filas: una vencida hace un mes, una revocada por error, y la activa de hoy. Por eso el índice `(TenantId, UserId, DeviceId)` **no es único**.

La alternativa —índice único y *actualizar* la fila existente al reconfiar— obligaría a limpiar `RevokedAt`, y ahí se pierde exactamente el dato que hace útil la auditoría: que alguien revocó ese dispositivo y cuándo. El descubrimiento vino de un caso banal: el admin revoca la laptop por error, el empleado la vuelve a confirmar, y la inserción chocaba contra la unicidad.

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

## ADR-014 — `register` cerrado antes de tiempo, y bootstrap CLI como parche

**Estado**: decidido al implementar · **Tareas**: T064

### El hallazgo

Una revisión de seguridad encontró que `POST /api/auth/register` era anónimo y tomaba `Role` y
`PhoneNumber` del body. `X-Tenant-ID` **no es secreto** — viaja en cada request de cualquier
cliente del tenant. Cualquiera que lo conociera podía registrarse como `TenantAdmin` con su propio
celular, recibir el OTP y tomar el tenant entero.

Forzar `role = Member` en el servidor **no alcanza**: un `Member` ya puede leer y ajustar stock
(FR-013/FR-014). El problema no era el rol elegido, era la falta de autorización en el endpoint.

### La decisión

`register` pasa a exigir `[Authorize(Policy = AuthorizationPolicies.TenantAdmin)]` **más** una
verificación explícita en el controller: el claim `tid` del JWT debe ser igual al tenant resuelto
por `X-Tenant-ID`. La policy sola no alcanza — sin la segunda verificación, un `TenantAdmin` legítimo
de un tenant podría apuntar el header a otro tenant y registrar gente ahí. El chequeo corre **antes**
de tocar `RegisterUserCommand`: un intento cross-tenant no crea ningún usuario, ni siquiera en el
tenant equivocado.

### El problema que esto abre: el huevo y la gallina

Con `register` cerrado, nadie puede convertirse en el primer `TenantAdmin` de un tenant nuevo por
HTTP: hace falta ya ser `TenantAdmin` para crear un `TenantAdmin`. La superficie correcta para esto
es T063 (admin de plataforma), que todavía no existe.

### Alternativas evaluadas

| Opción | Por qué se descartó |
|---|---|
| Dejar `register` anónimo hasta T063 | Es exactamente el agujero que encontró la revisión; no es aceptable dejarlo abierto mientras se construye T063 |
| Adelantar T063 completo | Fuera de alcance: T063 es una superficie nueva (identidad y endpoints separados de plataforma), no un parche de una tarde |
| **Bootstrap CLI temporal** ✅ | — |

### Decisión: bootstrap CLI

Subcomando de `Stockma.Api`, fuera de HTTP:

```
dotnet run --project backend/src/Stockma.Api -- bootstrap-admin --tenant <guid> --email <email> --phone <telefono>
```

- La contraseña se lee de `STOCKMA_BOOTSTRAP_PASSWORD` (variable de entorno), **nunca** de un
  argumento de línea de comandos — un argv queda en el historial de shell y en `ps`.
- Corre sobre un tenant **existente**: hoy el único alta de tenants es el `INSERT` manual documentado
  como decisión abierta más abajo. El CLI no inventa una superficie de creación de tenants.
- Sólo crea el admin si el tenant tiene **cero usuarios**; si ya tiene alguno, falla con un error
  claro y código de salida distinto de cero, sin tocar la base.
- Reutiliza `RegisterUserCommandHandler` (vía `BootstrapAdminCommandHandler`, que valida el tenant
  y el conteo de usuarios y después delega la creación) en vez de duplicar la lógica de alta de
  `IUserAccounts`.
- Sale sin levantar el host web.

### Qué la devolvería a discusión

Este CLI es un parche explícitamente temporal. Cuando exista T063 (admin de plataforma), el alta del
primer `TenantAdmin` de un tenant debería migrar a esa superficie, y este subcomando puede retirarse.

---

## ADR-015 — Autenticación por defecto y el tenant atado al token

**Estado**: decidido al implementar · **Tareas**: T073

### El hallazgo

Una revisión de seguridad de la slice 3b encontró que `ProductsController`, `BatchesController` y
`TenantController` no tenían `[Authorize]` en ningún endpoint, y `AddAuthorization` en `Program.cs`
no traía `FallbackPolicy`. Peor: el `TenantMiddleware` tomaba el tenant **únicamente** del header
`X-Tenant-ID` y nunca lo comparaba contra el claim `tid` del JWT. `X-Tenant-ID` **no es secreto**
—el mismo argumento de ADR-014— así que cualquiera que conociera el GUID de un tenant podía leer y
ajustar su stock sin loguearse nunca. La RLS **no** ataja esto: el tenant que ve la sesión de
Postgres sale de ese header, elegido por el cliente.

### La decisión

Dos cambios, uno completa al otro:

| Cambio | Efecto |
|---|---|
| `FallbackPolicy` con `RequireAuthenticatedUser()` | Autenticado es el default. Un endpoint nuevo queda protegido aunque nadie le ponga `[Authorize]` — el error seguro es cerrado, no abierto |
| `TenantMiddleware` compara `X-Tenant-ID` contra el `tid` del JWT cuando hay usuario autenticado | El header deja de ser la única fuente de verdad del tenant. Un mismatch corta con `403 TENANT_MISMATCH` **antes** de `Authorization` y antes de tocar el handler o la base |

`Program.cs` corre `UseAuthentication()` **antes** de `UseMiddleware<TenantMiddleware>()`: sin eso,
`HttpContext.User` todavía no está poblado cuando el middleware necesita leer el `tid`.

### Por qué `RequireAuthenticatedUser()` y no `[Authorize]` por controller

Un `[Authorize]` que se agrega por controller es un opt-in: alguien tiene que acordarse de ponerlo en
cada endpoint nuevo. Con `FallbackPolicy`, el opt-in se invierte a `[AllowAnonymous]` — la superficie
pública queda **listada explícitamente**, no es lo que quedó sin decorar por olvido.

### Qué queda anónimo, y por qué cada uno

| Endpoint | Por qué |
|---|---|
| `POST /api/auth/login`, `POST /api/auth/confirm-device` | Ya exentos del `TenantMiddleware` desde T055a: ocurren antes de que exista un token |
| `MapOpenApi()` | Sólo se mapea en `Development`; sin `AllowAnonymous()` explícito el `FallbackPolicy` también lo alcanza y rompe Swagger en dev |

### `GET /api/tenant/branding` NO queda anónimo

Se evaluó dejarlo público para themear la pantalla de login y se descartó: el login es genérico y el
tenant recién se conoce **después** de loguearse (se resuelve desde el email, T055). Antes del login no
hay `X-Tenant-ID` que mandar, así que un branding anónimo no sirve a ningún flujo real y sólo expone un
oráculo para saber qué GUID de tenant existe y cuál es su configuración.

### Consecuencia: el chequeo ad hoc de `register` queda redundante

ADR-014 había agregado una verificación manual en `AuthController.Register` (`tid` del JWT contra
`tenantContext.TenantId`) porque en ese momento era el **único** lugar donde algo comparaba token
contra header. Con el `TenantMiddleware` haciendo esa comparación para **toda** ruta autenticada, el
chequeo del controller pasó a duplicar una garantía que ya corre antes en el pipeline. Se eliminó;
`[Authorize(Policy = AuthorizationPolicies.TenantAdmin)]` se mantiene sin cambios.

### Alternativas evaluadas

| Opción | Por qué se descartó |
|---|---|
| Sólo agregar `[Authorize]` a los tres controllers, sin `FallbackPolicy` | Dependía de que nadie se olvide en el próximo endpoint. Es exactamente el error que esto corrige |
| Resolver el tenant siempre del `tid` del JWT, ignorando el header por completo | Rompería el contrato ya publicado (`X-Tenant-ID` obligatorio en todas las rutas) y los endpoints anónimos, que no tienen `tid`, quedarían sin forma de resolver tenant |
| **`FallbackPolicy` + comparación en `TenantMiddleware`** ✅ | — |

### Qué la devolvería a discusión

Si algún día `X-Tenant-ID` deja de ser redundante con el `tid` (por ejemplo, un usuario con acceso a
más de un tenant), la comparación estricta de igualdad deja de alcanzar y hay que decidir cuál gana.

---

## ADR-016 — Endurecimiento del login: enumeración, fuerza bruta del OTP y carreras

**Estado**: decidido al implementar · **Tareas**: T074, T075, T076, T077, T078

### El hallazgo

La revisión adversarial de cierre de la slice 3b encontró que el flujo de login cumplía el contrato
funcional pero dejaba abiertas varias formas de atacarlo **sin romper ninguna regla escrita**:

| # | Agujero | Consecuencia |
|---|---|---|
| 1 | `confirm-device` respondía `AUTH_INVALID_CREDENTIALS` a un email inexistente y un error de OTP a uno existente | Enumerar qué correos usan Stockma, justo lo que T055c prohíbe en el login |
| 2 | El login volvía **sin** correr PBKDF2 para un email inexistente o un usuario bloqueado | La misma enumeración, medida con un reloj en vez de leyendo el `errorCode` |
| 3 | El OTP no tenía tope de intentos, un código errado no lo gastaba, los viejos seguían vivos y cada intento se comparaba contra todos | 10⁶ combinaciones sin límite, y cada SMS nuevo sumaba un blanco más |
| 4 | `DeviceOtp` no tenía token de concurrencia | Dos `confirm-device` en carrera gastaban el mismo código dos veces |
| 5 | `TryTrust` contaba y después insertaba sin lock | La carrera superaba `MaxTrustedDevices` y duplicaba filas activas del mismo aparato |
| 6 | `ConsoleSmsSender` se registraba en **todo** entorno | En producción, el código y el celular terminaban en el log |
| 7 | `deviceId` ausente → `500`; contraseña rechazada por Identity → `500`; `429` con cuerpo vacío | Contrato incumplido (`400 VALIDATION_FAILED`, `429 AUTH_RATE_LIMITED`) |
| 8 | El rate limit se particionaba por `RemoteIpAddress` sin forwarded headers | Detrás de un proxy, todos los clientes comparten una sola cuota |
| 9 | `DeviceOtp` recortaba el `deviceId` al guardar pero las búsquedas comparaban el valor sin recortar | `" abc"` nunca coincidía con `"abc"`; además, un `deviceId` de 3 caracteres se aceptaba |

### Decisión 1 — Un único error en `confirm-device` (T074)

**Toda** falla de `confirm-device` responde `401 AUTH_OTP_REJECTED`, con el mismo cuerpo: email
inexistente, ningún OTP vigente, código errado, vencido, quemado por intentos o perdedor de una
carrera. El email inexistente **no** usa `AUTH_INVALID_CREDENTIALS`: ese código sólo existe en el
login, así que verlo acá ya dice "este email no existe".

Esto resuelve el `[PENDIENTE]` del contrato a favor de un código único: separar
`AUTH_OTP_INVALID` / `AUTH_OTP_EXPIRED` filtra si había un OTP en vuelo, es decir, si alguien acaba
de loguearse con la contraseña correcta.

### Decisión 2 — Hash señuelo en el login (T074)

Todo camino de falla del login (email inexistente o en blanco, usuario sin contraseña, usuario
bloqueado) corre `IPasswordHasher.VerifyHashedPassword` contra un **hash señuelo estático**, generado
una vez por proceso con el mismo `PasswordHasher` por defecto que usa Identity (mismo formato, mismas
iteraciones). El resultado se descarta.

El test no mide tiempos —sería flaky—: cuenta con un hasher doble que cada camino de falla invoca la
verificación **exactamente una vez**, igual que la contraseña equivocada.

### Decisión 3 — Intentos y emisión del OTP (T075)

| Regla | Detalle |
|---|---|
| Emitir invalida lo anterior | Un OTP nuevo marca `InvalidatedAt` en todos los vivos de ese usuario+dispositivo. A lo sumo hay **un** código vivo por dispositivo |
| Sólo el último cuenta | Cada intento se compara contra el OTP vigente **más nuevo**, nunca contra todos |
| Tope de intentos | `DeviceOtp.FailedAttempts` persistido. `Otp:MaxAttempts` (def. 5) intentos fallidos **queman** el código (`InvalidatedAt`). El invariante vive en la entidad (`RegisterFailedAttempt`); quemado no se compara contra nada más |
| Tope de emisión | `Otp:MaxIssuesPerWindow` (def. 5) códigos por usuario en `Otp:IssueWindowMinutes` (def. 15, ventana deslizante) |
| Registro del intento (FR-008) | El contador persistido **más** un log estructurado `Warning` con `UserId`, intentos y si quedó quemado. **Nunca** el código ni el celular, y tampoco el `deviceId` (ver decisión 7) |

`InvalidatedAt` es una columna aparte de `ConsumedAt` por el mismo motivo que ADR-007 separa
`RevokedAt` de `ExpiresAt`: "lo usó alguien" y "lo quemamos nosotros" son hechos distintos y la
auditoría necesita distinguirlos.

**Pasado el tope de emisión, el login responde exactamente igual** (`200 requiresDeviceConfirmation`)
pero no manda SMS. Se descartó un `429`: sólo lo recibiría quien acertó la contraseña de un email
existente, así que el status mismo confirmaría la cuenta. El costo es de UX: un usuario legítimo que
pidió 6 códigos en 15 minutos no recibe el sexto y no sabe por qué. Se acepta porque el código
anterior **sigue vivo** (un pedido sobre el tope no invalida nada) y porque 5 en 15 minutos no es un
uso normal.

### Decisión 4 — Concurrencia: lock por usuario + token de concurrencia (T075, T076)

| Opción | Por qué se descartó |
|---|---|
| Sólo `xmin` (concurrencia optimista) | Evita el doble consumo, pero **no** el ataque de ráfaga: 100 intentos en paralelo leen `FailedAttempts = 0`, se **comparan los 100** y recién después chocan al guardar. El tope limitaría los intentos contados, no los evaluados |
| `SELECT … FOR UPDATE` sobre la fila del OTP | Serializa el consumo, pero no la emisión ni el tope por ventana, que no tienen una fila que bloquear |
| **`pg_advisory_xact_lock` por usuario + `xmin`** ✅ | — |

`Issue` y `Consume` corren dentro de una transacción que toma
`pg_advisory_xact_lock(hashtextextended('device_otps:{userId}', 0))`. Dentro del lock se cuenta la
ventana, se invalidan los anteriores, se compara el código y se registra el intento: una ráfaga en
paralelo evalúa **como máximo** `MaxAttempts` intentos. El lock se libera solo al terminar la
transacción; no toca tablas, así que convive con la RLS (la sesión ya tiene `app.tenant` seteado
por el interceptor al abrir la conexión). Una colisión del hash sólo agrega espera, nunca un error.

`DeviceOtp` suma además `xmin` como token de concurrencia (`IsRowVersion()`, el mismo patrón que
`Batch`). Con el lock no debería disparar nunca; si otra ruta futura toca la fila sin el lock, el
perdedor recibe `AUTH_OTP_REJECTED` en vez de gastar el código dos veces. El test de doble consumo
pasa **incluso sin el lock**, lo que prueba que el token defiende solo.

`TryTrust` toma el mismo tipo de lock con otro alcance (`trusted_devices:{userId}`) y, dentro, vuelve
a chequear si ese `(usuario, dispositivo)` ya tiene una fila activa —si la tiene, lo devuelve como
confiable sin insertar— y recién después cuenta los activos contra `MaxTrustedDevices`.

**No se agregó un índice único parcial** `(user_id, device_id) WHERE revoked_at IS NULL`. Un
dispositivo **vencido** tiene `revoked_at IS NULL`, así que reconfiarlo chocaría contra el índice
salvo que se le escriba `RevokedAt` a la fila vieja — y eso es exactamente lo que ADR-007 prohíbe:
`RevokedAt` significa "una persona lo dio de baja". El predicado tampoco puede usar `now()` (un
índice parcial exige funciones inmutables). La garantía de unicidad entre activos la da el lock.

### Decisión 5 — El sender de consola sólo en `Development` (T077)

`ConsoleSmsSender` se registra **únicamente** si el entorno es `Development`. En cualquier otro, y
mientras no exista un proveedor real (T049), la API **no arranca**: una validación de opciones con
`ValidateOnStart()` corta el arranque del host con un mensaje que dice qué falta.

Se eligió `ValidateOnStart()` y no tirar la excepción al registrar servicios porque el subcomando
`bootstrap-admin` (ADR-014) construye el host pero no lo arranca, y **tiene** que poder correr en
producción. El error seguro es cerrado: nunca se cae en silencio al sender que escribe códigos en el
log.

### Decisión 6 — Forwarded headers con proxies explícitos (T078)

`UseForwardedHeaders()` corre primero en el pipeline, antes del rate limiter. Los proxies de
confianza salen de `ForwardedHeaders:KnownProxies` (IPs) y `ForwardedHeaders:KnownNetworks` (CIDR),
**vacíos por defecto**.

Hay una trampa en ASP.NET Core: con las dos listas vacías, el middleware acepta `X-Forwarded-For` de
**cualquier** origen. Por eso, sin configuración, `ForwardedHeaders` queda en `None` y el header se
ignora: un cliente no puede inventarse una IP nueva por request para esquivar el límite. Detrás de un
proxy real hay que declararlo; si no, todos comparten una cuota — molesto, pero seguro.

### Decisión 7 — `deviceId` con entropía mínima; la huella es informativa (T078)

- El `deviceId` se normaliza en **un** lugar (`DeviceIdentifier`, recorta espacios) y ese valor se
  usa para emitir, consumir, confiar y buscar.
- Después de normalizar DEBE tener entre **16 y 128** caracteres (128 es el largo de la columna; un
  GUID tiene 36). Si no, `400 VALIDATION_FAILED` **antes** de mirar credenciales, así el `400` es el
  mismo para cualquier email.
- La validación vive en la entrada (los handlers de `login` y `confirm-device`); las entidades y los
  servicios sólo normalizan.
- **El `fingerprint` NO se compara.** Se guarda en el `TrustedDevice` como dato informativo para el
  listado de T067, pero no es un control de seguridad: las huellas de navegador cambian con cada
  actualización, y exigir coincidencia sacaría del estado confiable a usuarios legítimos cada pocas
  semanas sin detener a nadie que ya tenga el `deviceId`.
- La seguridad del dispositivo confiable descansa en **entropía del `deviceId` + contraseña + OTP**.
  Por eso el `deviceId` se trata como un secreto: no se loguea.

### Decisión 8 — `400 VALIDATION_FAILED` con una excepción dedicada (T078)

Se agregó `ValidationFailedException` (`DomainException` con `VALIDATION_FAILED`) en vez de mapear
`ArgumentException` a `400` en el middleware. Una `ArgumentException` también sale de invariantes
internas (un `Guid.Empty` que llegó donde no debía, una clave JWT corta): esos son bugs y tienen que
seguir siendo `500`, no disfrazarse de error del cliente. Lo que es input del usuario —`deviceId`,
email, celular, rol, contraseña rechazada por Identity— tira la excepción dedicada. El binding de MVC
(`[ApiController]`) responde con el mismo `errorCode`. Todos los errores de dominio y el `429` salen
como `application/problem+json`.

### Qué la devolvería a discusión

- **Un proveedor de SMS real (T049)**: la validación de arranque pasa a exigir su configuración en
  vez de fallar siempre fuera de `Development`.
- **Varias instancias de la API detrás de un balanceador**: el lock ya es de la base, así que sigue
  valiendo; el rate limit por IP es en memoria y **no** — cada instancia tendría su propia cuota.
- **Un `deviceId` generado por el servidor** (cookie firmada): la regla de entropía pasaría a ser una
  garantía propia en vez de una validación del input.
- **Tiempo en `confirm-device`**: un email con OTP vigente cuesta un PBKDF2 y uno inexistente no. Sólo
  distingue cuentas en las que alguien acaba de acertar la contraseña; se dejó fuera de este alcance.

---

## ADR-017 — La RLS tiene que aplicar en runtime: rol separado, `FORCE` y chequeo de arranque

**Estado**: decidido al implementar · **Tareas**: T079, T080, T081, T082, T083; tercera revisión: T084–T089

### El hallazgo

La segunda revisión adversarial de la slice 3b encontró que la capa 2 del aislamiento **no hacía
nada en runtime**. `InitialSchema` crea `app_user` y avisa que la app NO DEBE conectarse como
propietario, pero `appsettings.json` conectaba como `stockma` —el rol que migra, dueño de las
tablas— y ninguna tabla tenía `FORCE ROW LEVEL SECURITY`. PostgreSQL no aplica RLS al propietario.
Los tests no lo veían: la API y los fixtures de OTP y dispositivos corrían como el superusuario
`postgres`, que también la saltea.

| # | Agujero | Consecuencia |
|---|---|---|
| 1 | Runtime conectado como propietario, sin `FORCE` | La RLS era decorativa: todo el aislamiento colgaba del filtro de EF |
| 2 | `UseHttpsRedirection()` antes de `UseForwardedHeaders()` | Detrás de un proxy que termina TLS, cada request se redirige a sí misma |
| 3 | El OTP se commiteaba antes de `TryTrust`; la huella sólo se validaba en blanco | Una huella de 257 caracteres (o cualquier falla posterior) gastaba el código sin dar JWT |
| 4 | `confirm-device` ignoraba `LockoutEnd` | Un usuario deshabilitado con un OTP vigente recibía token |
| 5 | Bootstrap sin lock; usuario y rol en dos pasos | Dos admins en carrera, o un usuario sin rol que traba el bootstrap para siempre |
| 6 | Errores del `TenantMiddleware` como `application/json` | Contrato incumplido (`problem+json`) |
| 7 | Clave JWT corta u opciones de proxy inválidas | Fallaban en la primera request, o al arrancar con un mensaje que no decía qué clave |

### Decisión 1 — Dos roles, dos connection strings (T079)

| Clave | Rol | Uso |
|---|---|---|
| `ConnectionStrings:Postgres` | `app_user` (no propietario) | Runtime de la API y `bootstrap-admin` |
| `ConnectionStrings__PostgresMigrations` (env) | propietario | Sólo `dotnet ef` vía `StockmaDbContextFactory` |

El runtime **nunca** recibe la credencial del propietario: no está en `appsettings.json`. Se mantuvo
el nombre `Postgres` que ya usaban el código, los tests y el README; el `StockmaDb` de `plan.md`
nunca llegó al código y se reconcilió. `STOCKMA_CONNECTION_STRING` se reemplazó por
`ConnectionStrings__PostgresMigrations` para que las dos claves sigan la misma convención.

### Decisión 2 — `FORCE ROW LEVEL SECURITY` en las seis tablas (T079)

Segunda llave: aunque alguien vuelva a conectar el runtime con el propietario, la política le aplica.
Un superusuario o un rol con `BYPASSRLS` la siguen salteando; eso no lo puede frenar la base, lo
frena la decisión 4.

### Decisión 3 — La función del login pasa a un rol propio (T079)

Con `FORCE`, `auth_find_user_by_email` —`SECURITY DEFINER`, corre sin `app.tenant`— dejaría de
encontrar usuarios si su dueño fuera el propietario de las tablas.

| Opción | Por qué se descartó |
|---|---|
| `SET row_security = off` en la función | Sólo lo acepta un superusuario |
| Dueño con `BYPASSRLS` | Saltea la RLS en **toda** tabla, no sólo en `users`; un rol así es exactamente lo que el chequeo de arranque rechaza |
| Política permisiva cuando `app.tenant` está vacía | Lo que ADR-002 y ADR-013 prohíben |
| **Rol `auth_lookup` con una política propia** ✅ | — |

`auth_lookup` es `NOLOGIN`, no es dueño de ninguna tabla y no tiene `BYPASSRLS`. Tiene `SELECT`
**sólo** sobre las columnas de autenticación de `users` y una política
`auth_lookup_read FOR SELECT TO auth_lookup USING (true)` que no aplica a ningún otro rol. La
migración le concede al rol que migra la membresía **sin `INHERIT`** y le da a `auth_lookup`
`CREATE` sobre el schema sólo mientras cambia el dueño; las dos se revocan en el acto. Si el rol que
migra heredara `auth_lookup`, la política le abriría `users` entera, y el test
`TheOwner_WithoutAnActiveTenant_SeesNoUsers` lo detecta.

**Consecuencia aceptada**: un propietario **no superusuario** necesita `CREATEROLE`. Si `auth_lookup`
ya existe en el cluster (lo crea la primera base migrada), el rol que migra necesita `ADMIN` sobre él
—lo que PostgreSQL 16 le da solo a quien lo crea—. `ForceRowLevelSecurity` y
`HardenLoginLookupSearchPath` lo chequean primero (`pg_has_role(current_user, 'auth_lookup', 'USAGE
WITH ADMIN OPTION')`, que un superusuario siempre pasa) y, si falta, abortan con un mensaje que nombra
el rol y trae el `GRANT auth_lookup TO <rol> WITH ADMIN OPTION, INHERIT FALSE, SET FALSE` que tiene que
correr un superusuario (T086). Antes fallaba con un `permission denied to grant role` que no decía qué
hacer. Nunca queda a medias: cada migración corre en una transacción.

### Decisión 4 — La API no arranca con un rol que saltee la RLS (T079)

`RuntimeDatabaseRoleCheck` (`IHostedLifecycleService.StartingAsync`, antes que cualquier otro
servicio) consulta `pg_roles` y corta el arranque si el rol de runtime es, **o es miembro a cualquier
profundidad de**, un superusuario, un rol con `BYPASSRLS` o el propietario de alguna tabla de `public`.
Usa `pg_has_role(…, 'MEMBER')`, no `'USAGE'`: `USAGE` sólo ve las membresías con `INHERIT`, y un
miembro `NOINHERIT` igual puede hacer `SET ROLE` al dueño o al rol que saltea la RLS (T084).

La **única** excepción es `Development`: en local el `docker-compose` crea `stockma` como
superusuario, y exigir el chequeo ahí sólo empujaría a desactivarlo. Hasta la tercera revisión también
se exceptuaba `Testing`, pero ningún test lo necesitaba (`StockmaApiFactory` corre en `Development`) y
era un nombre de entorno que apagaba un control de seguridad; se quitó (T084).

`bootstrap-admin` corre **los mismos** chequeos antes de hacer nada: la validación de opciones
(`IStartupValidator`, lo que dispara `ValidateOnStart`) y `RuntimeDatabaseRole.EnsureRestrictedAsync`
con la misma excepción de `Development`. Si fallan, sale con código `1` y el motivo en `stderr`
(T085). Consecuencia aceptada: fuera de `Development`, sin proveedor de SMS (T049) el subcomando
tampoco corre, igual que la API. Un admin creado así no podría loguearse de todos modos: el login
exige OTP por SMS.

### Decisión 5 — `confirm-device` es una sola unidad (T080)

La huella se valida (1–256 después de recortar) junto al `deviceId`, antes de mirar el email.
`IDeviceOtpService.ConsumeAsync<T>` recibe **lo que sigue al consumo** —confiar el dispositivo y
emitir el JWT— y lo corre dentro de la misma transacción con lock por usuario; si falla, el consumo
se revierte y el usuario reintenta con el mismo código.

| Opción | Por qué se descartó |
|---|---|
| Transacción externa alrededor de consumo + confianza | Un código errado **registra el intento y tira**: la transacción de afuera revertiría el contador y reabriría la fuerza bruta que cierra T075 |
| Consumir después de confiar | Confía el dispositivo antes de saber si el código ganó la carrera |
| **Callback dentro del consumo** ✅ | Sólo corre si el código coincide; los intentos fallidos se siguen commiteando |

El orden de locks es siempre `device_otps:{userId}` → `trusted_devices:{userId}`; ninguna ruta los
toma al revés.

El usuario bloqueado (`LockoutEnabled` + `LockoutEnd` futuro) recibe en `confirm-device` el mismo
`401 AUTH_OTP_REJECTED` que un email inexistente, y no se toca su OTP: misma regla que el login.

### Decisión 6 — Bootstrap bajo lock y alta atómica (T083)

`ITenantAccounts.RunExclusivelyAsync` toma `pg_advisory_xact_lock('tenant_bootstrap:{tenantId}')`
y dentro corren el chequeo de "sin usuarios" y el alta. `UserAccounts.CreateAsync` crea usuario y
rol en una transacción, y se une a la del lock si ya hay una. Mismo mecanismo que ADR-016 decisión
4, con otro alcance.

### Decisión 7 — Fallar al arrancar, no en la primera request (T081, T082)

- `JwtOptions` con `ValidateOnStart`: clave ≥ 32 bytes, `Issuer` y `Audience` no vacíos,
  `ExpiresMinutes` entre 1 y 60.
- `ForwardedHeadersOptions` con `ValidateOnStart` y mensajes que nombran la clave. Las listas ya se
  resolvían al construir el pipeline, así que la revisión sobreestimó el problema: fallaba al
  arrancar, pero con `An invalid IP address was specified` y sin validar el largo del prefijo CIDR.
- `UseForwardedHeaders()` es el **primer** middleware; ADR-016 decisión 6 lo afirmaba pero el código
  corría antes `UseHttpsRedirection()`.
- Los errores del `TenantMiddleware` salen en `application/problem+json`.

### Decisión 8 — `search_path` de la función del login sin `pg_temp` primero (T086)

`SET search_path = public` no saca a `pg_temp`: si no figura explícito, PostgreSQL lo busca **primero**
para relaciones. `app_user` puede crear una tabla temporal `users`, y la función `SECURITY DEFINER`
leería esa en vez de la real. La migración nueva `HardenLoginLookupSearchPath` (nunca se edita una
migración aplicada) deja `SET search_path = pg_catalog, public, pg_temp`. Sólo hace `ALTER FUNCTION`,
no `CREATE OR REPLACE`: reemplazar el cuerpo exigiría darle `CREATE` en el schema a `auth_lookup` otra
vez. Como la función es de `auth_lookup`, el rol que migra repite la maniobra de `ForceRowLevelSecurity`
(membresía sin `INHERIT`, `SET ROLE`, revocar) y necesita el mismo `ADMIN`.

### Decisión 9 — La configuración base no trae credenciales (T087)

`appsettings.json` traía `app_user`/`app_user`: un deploy sin override probaba una contraseña
conocida. Ahora la base deja `ConnectionStrings:Postgres` vacía, la cadena de desarrollo vive en
`appsettings.Development.json` (versionado, sólo local) y `DatabaseOptions` con `ValidateOnStart` corta
el arranque con un mensaje que nombra la clave. `ConnectionStrings__PostgresMigrations` nunca se
define en el entorno de la API.

### Decisión 10 — La carrera de registro entre tenants es `409`, no `500` (T088)

El pre-chequeo de `register` usa la función del login (ve todos los tenants), pero Identity valida la
unicidad con el filtro y la RLS del tenant activo. Dos altas simultáneas del mismo email en tenants
distintos pasan las dos, y la segunda choca con `ux_users_normalized_email`. `UserAccounts.CreateAsync`
traduce ese `23505` (y el de `ux_users_normalized_user_name`, porque el usuario es el email), y los
errores `DuplicateEmail`/`DuplicateUserName` de Identity, a `EmailAlreadyRegisteredException`: el mismo
`409 AUTH_EMAIL_DUPLICATE` del pre-chequeo.

### El pool de conexiones es fail-closed (T089)

`app.tenant` se setea a nivel de sesión (`set_config(…, false)`) al abrir la conexión. Una conexión del
pool reusada sin tenant no ve filas porque Npgsql corre `DISCARD ALL` al devolverla. El test
`APooledConnection_ReusedWithoutATenant_SeesNoRows` lo fija y falla si la connection string lleva
`No Reset On Close=true`: esa opción queda **prohibida** mientras el tenant viva en la sesión.

### Brecha aceptada: tablas sin RLS (T089)

`user_roles`, `user_claims`, `user_tokens`, `user_logins`, `roles`, `role_claims` y `tenants` **no
tienen RLS**: no tienen `tenant_id` (las de Identity cuelgan de `user_id`/`role_id`; `roles` y
`tenants` son globales), y el login lee `user_roles` para armar el JWT **antes** de que exista un
tenant activo. Ahí el aislamiento depende sólo de la aplicación: toda lectura parte de un `user_id` que
ya salió de `users` (con RLS) o de la función del login. Una política que resolviera el tenant por
`user_id` → `users.tenant_id` rompería el login mismo. Queda en las decisiones abiertas.

### La duración de 3h28m de la suite

No era un lock ni un timeout. El registro de Windows muestra la máquina en *modern standby* de 17:48
a 21:15 (tapa cerrada), 3h27m. Corridas aisladas: `Stockma.Api.Tests` 21 s y
`Stockma.Infrastructure.Tests` 33 s, ningún test por encima de 4,7 s (el primero de cada clase, que
arranca el contenedor).

### Qué la devolvería a discusión

- **Un hosting con Postgres administrado** que no permita `CREATEROLE`: los roles se crearían en el
  aprovisionamiento y la migración sólo haría `GRANT`/`ALTER OWNER`.
- **Migraciones de datos sobre tablas con RLS**: con `FORCE`, el propietario no ve filas sin
  `app.tenant`. Una migración así DEBE setear el tenant por lote o correr como superusuario, y
  decirlo en su comentario.

---

## ADR-018 — Refresh token: SHA-256, familia atada al dispositivo y reuso que corta la sesión

**Estado**: decidido antes de implementar · **Tareas**: T065

### El hallazgo

T065 decía que el refresh token se hashea "con el mismo criterio que el OTP (`IPasswordHasher`)".
Eso **no funciona**. `IPasswordHasher` es PBKDF2 con salt aleatorio: el mismo token da un hash
distinto cada vez. El OTP se busca por usuario y dispositivo y **después** se verifica; el refresh
llega **solo**, en una cookie, sin nada más que lo identifique. Con un hash salado, la única forma de
encontrar su fila sería verificar contra todas.

### Decisión 1 — SHA-256 sin salt

El token son 32 bytes de un CSPRNG y se persiste `SHA-256(token)`, con índice único. Un hash rápido y
sin salt es seguro **acá** porque el secreto tiene 256 bits de entropía: no hay diccionario ni
fuerza bruta posible, a diferencia de una contraseña elegida por una persona. El hash lento existe
para compensar la baja entropía de las contraseñas; este token no la tiene.

| Alternativa | Por qué no |
|---|---|
| `IPasswordHasher` (PBKDF2 con salt) | La fila no se puede buscar por hash |
| Token con prefijo de id en claro + PBKDF2 | Resuelve la búsqueda, pero agrega costo de CPU en cada renovación sin sumar seguridad sobre 256 bits de entropía |
| JWT como refresh | No se puede revocar sin guardar estado igual, y es más grande que un token opaco |

### Decisión 2 — La familia queda atada al dispositivo del login

La familia guarda el `deviceId` normalizado. Un `refresh` con otro `deviceId` se trata como reuso y
revoca la familia. Es lo que hace cumplir la regla de T065: un refresh **no** sirve para entrar desde
un dispositivo nuevo sin pasar por el 2FA.

### Decisión 3 — El reuso corta la sesión, sin ventana de gracia

Un token consumido que vuelve a aparecer revoca **toda** la familia. Dos pestañas que renuevan a la
vez con el mismo token disparan esa detección: la solución va en el frontend, que serializa la
renovación entre pestañas (Web Locks API). No se agrega una ventana de gracia en el servidor: esa
ventana es justamente el hueco por el que pasaría una copia robada.

### Decisión 4 — `refresh` y `logout` no usan el header de tenant

Los dos son anónimos (el access token puede estar vencido) y el tenant sale de la fila del token, por
una función acotada propiedad de `auth_lookup`, igual que el login (ADR-017). Un `X-Tenant-ID`
presente se ignora.

### Consecuencias aceptadas

- Tras un `logout`, el access token ya emitido sigue valiendo hasta su `exp` (≤ 60 min). Revocarlo al
  instante exigiría consultar la base en cada request.
- Un usuario con dos pestañas en un frontend que no serialice la renovación pierde la sesión.

### Qué la devolvería a discusión

- Que el frontend no pueda serializar la renovación (navegadores sin Web Locks en el parque real de
  las droguerías).
- Que el mostrador se queje de la vigencia absoluta de 8 h: la salida es vigencia deslizante con tope
  absoluto, no una ventana de gracia.

---

## ADR-019 — Sesión: 30 minutos de inactividad en el servidor, access token de 15 minutos

**Estado**: decidido antes de implementar · **Tareas**: T065 · **Modifica**: ADR-018 y la vigencia de
T065

### El problema

La PC del mostrador es compartida. Con la vigencia absoluta de 8 h de ADR-018, una sesión que queda
abierta la usa el siguiente empleado que se sienta: la vigencia absoluta no protege contra eso. Hace
falta cortar por **inactividad**.

El servidor sólo ve actividad cuando el frontend le pide algo, y para la sesión eso es el `refresh`.
Con el access token de 60 minutos no hay forma de cortar a los 30:

- Si el frontend renueva cada 60 minutos, el servidor pasa 60 minutos sin noticias. Una regla de "30
  minutos sin renovar, la sesión muere" mataría también la sesión de quien está trabajando.
- Aunque el servidor dejara de aceptar el refresh a los 30 minutos, el access token ya emitido sigue
  valiendo hasta 60. El que se sienta después opera media hora más.

Un control de 30 minutos no puede apoyarse en una credencial que dura 60.

### Decisión 1 — Access token de 15 minutos

`Jwt:ExpiresMinutes` pasa a **15** por defecto y el arranque rechaza valores fuera de 1–15. Sigue dentro
del tope de 60 de NFR-004.

### Decisión 2 — La inactividad la controla el servidor

`SessionIdleTimeoutMinutes` (def. 30, `TenantSettings`). Cada refresh token vence esos minutos después
de **su** emisión, sin pasar nunca el tope absoluto de la familia:
`ExpiresAt = min(IssuedAt + SessionIdleTimeoutMinutes, FamilyExpiresAt)`. Como cada `refresh` emite un
token nuevo, no hace falta un campo de "último uso": el `IssuedAt` del token vivo **es** el último uso.

### Decisión 3 — El frontend renueva sólo si hubo actividad

Renueva cuando al access token le quedan menos de 2 minutos **y** hubo puntero o teclado desde la
última renovación. Sin actividad no renueva, y la sesión muere sola en el servidor.

El servidor corta entre los 30 y los 45 minutos de inactividad, según en qué punto del ciclo de 15
quedó el usuario. Para que la experiencia sea exacta, el frontend guarda `lastActivityAt` y cierra la
sesión en pantalla a los 30 minutos, también al volver a abrir la app. **El servidor es la barrera; el
frontend es la experiencia**: si el frontend fallara, el servidor igual corta.

### Decisión 4 — Cookie de sesión

La cookie `stockma_refresh` no lleva `Max-Age` ni `Expires`: muere al cerrar el navegador. El
vencimiento real lo decide la fila.

Cerrar la **pestaña** no es un mecanismo: el evento de cierre también se dispara al recargar y no
siempre se ejecuta. La inactividad cubre el mismo riesgo sin depender de él.

| Alternativa | Por qué no |
|---|---|
| Inactividad sólo en el frontend | Quien tenga la cookie sigue renovando: no es un control de seguridad |
| Access token de 60 minutos + inactividad de 60 | El mostrador queda abierto una hora |
| Consultar la base en cada request | Revoca al instante, pero agrega una consulta a cada request de negocio |
| Cerrar sesión al cerrar la pestaña | No es confiable en el navegador (ver Decisión 4) |

### Consecuencias aceptadas

- Un `refresh` cada 15 minutos por usuario activo, en vez de uno cada 60. No pasa por el SMS.
- Tras un `logout`, el access token ya emitido sirve como mucho **15 minutos**, no 60 como decía
  ADR-018.
- El access token sigue en `localStorage`: al volver a abrir el navegador puede quedar vigente unos
  minutos. `lastActivityAt` acota eso a la misma regla de 30 minutos.

### Qué la devolvería a discusión

- Que el mostrador pida otra ventana: se cambia `SessionIdleTimeoutMinutes` por tenant, no el diseño.
- Que la carga de `refresh` se note en el servidor, algo improbable con una fila por renovación.

### Enmienda (T092, 2026-10-06)

El **default** de `SessionIdleTimeoutMinutes` pasó de 30 a **120 minutos (2 h)**: el dueño del producto
quiere que la sesión se cierre a las 2 horas de inactividad. No cambia el diseño — ventana deslizante
controlada por el servidor, renovación silenciosa y precedencia servidor/front siguen idénticos; cambia
sólo el número por defecto (cada tenant sigue pudiendo personalizarlo). El front espeja el default en
`IDLE_TIMEOUT_MS` y la migración actualiza las filas que estaban en el default viejo.

**Revocada (2026-10-09)**: el dueño del producto volvió a la decisión original — el default vuelve a
**30 minutos** (migración `SetSessionIdleTimeoutDefaultTo30`, `IDLE_TIMEOUT_MS` del front espeja 30 min).
La enmienda queda en el registro como historia, pero la ADR-019 original vige tal cual: 30 min de
inactividad y access token de 15 min.

---

## Decisiones que siguen abiertas

| Tema | Estado |
|---|---|
| Alta de tenants | Sin tarea. Hoy el seed es un `INSERT` manual: no se puede dar de alta una droguería sin tocar la base |
| Proveedor de SMS | **Elegido: Twilio** (T049, cerrado). La API no arranca fuera de `Development` sin `Sms { AccountSid, ApiKey, Sender }` (ADR-016) |
| Rate limit con varias instancias | Es en memoria por instancia; con más de una réplica cada una tiene su propia cuota (ADR-016). Sigue abierto tras la segunda revisión de 3b |
| Password spraying | Una contraseña errada no incrementa `AccessFailedCount`; el único freno es el rate limit por IP. El lockout por intentos queda en los criterios de T068 |
| Tiempo de `confirm-device` | Un email con OTP vigente cuesta un PBKDF2 y uno inexistente o bloqueado no: unos milisegundos de diferencia. Sólo distingue cuentas en las que alguien acaba de acertar la contraseña (ADR-016) |
| ¿Sobrevive `TrustedDevice`? | A reevaluar después de T065 — ver ADR-006 |
| Tablas sin RLS | `user_roles`, `user_claims`, `user_tokens`, `user_logins`, `roles`, `role_claims` y `tenants` no tienen `tenant_id` ni política, y el login lee `user_roles` sin tenant. Ahí el aislamiento es sólo de la aplicación (ADR-017, brecha aceptada) |
