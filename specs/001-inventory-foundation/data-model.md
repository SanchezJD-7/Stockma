# Modelo de Datos: Inventory Foundation

**Feature branch**: `001-inventory-foundation`
**Fuentes**: [`spec.md`](./spec.md) (Entidades Clave, FR-001 … FR-015) · diseño técnico SDD original
**Plan técnico**: [`plan.md`](./plan.md)

---

## Resumen

Cinco entidades principales — `Tenant`, `User`, `TrustedDevice`, `Product`, `Batch` — más tres auxiliares (`TenantSettings`, `DeviceOtp`, VO `SemaphoreColor`).

Todas las entidades de negocio implementan `ITenantEntity` y quedan aisladas por `TenantId` mediante **dos capas independientes**: filtro global de EF Core y Row-Level Security de PostgreSQL.

### Diagrama de relaciones

```
Tenant (1) ──── (1) TenantSettings
   │
   ├── (N) User (ApplicationUser : IdentityUser)
   │         │
   │         ├── (N) TrustedDevice   [máx. MaxTrustedDevices, def. 2]
   │         └── (N) DeviceOtp       [expira ≤ 10 min]
   │
   ├── (N) Product  ◄── AR separado
   │         │
   │         └── (N) Batch  ◄── AR separado (referencia por ProductId, sin navegación fuerte)
   │
   └── (N) Batch
```

`Product`, `Batch` y `Sale` (slice 2) son **agregados raíz separados** — decisión cerrada. `Batch` referencia a `Product` por `ProductId` con FK a nivel de base de datos, no por navegación de agregado.

---

## Aplicación de `TenantId` (transversal)

### Contrato `ITenantEntity`

```csharp
public interface ITenantEntity
{
    Guid TenantId { get; }   // inmutable tras la creación — FR-004
}
```

Toda entidad tenant DEBE implementar `ITenantEntity`. El test de arquitectura (`Architecture.Tests/TenantArchitectureTests.cs`, NetArchTest) hace **fallar el build** si una entidad tenant no lo implementa — FR-002.

### Capa 1 — EF Core `HasQueryFilter`

`StockmaDbContext.ApplyTenantFilters()` es **reflectivo**: itera el modelo, detecta los tipos que implementan `ITenantEntity` y aplica:

```csharp
modelBuilder.Entity(entityType.ClrType)
    .HasQueryFilter(e => EF.Property<Guid>(e, "TenantId") == _tenantContext.TenantId);
```

`_tenantContext` es `ITenantContext` (scoped), poblado por `TenantMiddleware` a partir del header `X-Tenant-ID` (FR-001).

### Capa 2 — PostgreSQL RLS (defensa en profundidad)

Aplicada en la migración `InitialSchema` (FR-003, NFR-003):

```sql
ALTER TABLE products ENABLE ROW LEVEL SECURITY;
CREATE POLICY tenant_isolation ON products
    USING (TenantId = current_setting('app.tenant')::uuid);
-- ídem para batches, trusted_devices, tenant_settings, device_otps y tablas Identity
```

- La aplicación se conecta con el rol **no-propietario** `app_user` (un rol propietario haría bypass de RLS).
- `TenantMiddleware` ejecuta `SET app.tenant = '{tenantId}'` sobre la `NpgsqlConnection` en request-scope.

### Inmutabilidad de `TenantId` (FR-004)

`AuditSaveChangesInterceptor` inspecciona las entradas `Modified` del `ChangeTracker`; si la propiedad `TenantId` está marcada como modificada, lanza excepción y el `SaveChanges` se aborta. Los handlers de `Update` nunca tocan `TenantId`.

---

## 1. `Tenant`

Unidad de aislamiento. Es la única entidad que **no** implementa `ITenantEntity` (es la raíz del aislamiento, no un sujeto de él).

| Campo | Tipo | Notas |
|---|---|---|
| `Id` | `Guid` | PK. Es el valor que viaja en el header `X-Tenant-ID` |
| *resto de campos* | — | `[PENDIENTE: las fuentes no definen los campos de negocio del Tenant (nombre, NIT, estado, moneda por defecto). Confirmar antes de la migración InitialSchema]` |

**Invariantes**

- `Id` DEBE ser un `Guid` válido; el middleware rechaza con `400` cualquier header no parseable (FR-001).
- Un `Tenant` DEBE existir antes de registrar usuarios, productos o lotes bajo su `Id`. `[PENDIENTE: el alta de tenants (onboarding) está fuera de alcance de este slice; el seed inicial es un INSERT manual del admin-tenant]`

**Relaciones**

- `1 : 1` con `TenantSettings`
- `1 : N` con `User`, `Product`, `Batch`, `TrustedDevice`, `DeviceOtp`

**Índices**

- PK sobre `Id`.
- `[PENDIENTE: no hay índices secundarios definidos en las fuentes]`

**`TenantId`**: N/A — es la raíz. No lleva `HasQueryFilter` ni política RLS propia. `[PENDIENTE: confirmar si la tabla tenants debe quedar accesible sin RLS o restringida a un rol administrativo]`

---

## 1b. `TenantSettings`

Configuración por tenant. `TenantSettings : ITenantEntity`.

| Campo | Tipo | Default | Notas |
|---|---|---|---|
| `TenantId` | `Guid` | — | PK y FK a `Tenant` (relación 1:1) |
| `MaxTrustedDevices` | `int` | `2` | Límite de dispositivos confiables por usuario (FR-007) |
| `TrustedDeviceLifetimeDays` | `int` | `15` | Vigencia de un `TrustedDevice` antes de volver a exigir 2FA (FR-007, T070) |
| `RefreshTokenLifetimeHours` | `int` | `8` | Vigencia **absoluta** de la familia de refresh — la sesión real (FR-006, T065) |
| `ExpiryThresholds.GreenMonths` | `int` | `6` | Verde si faltan **más** de N meses (FR-015) |
| `ExpiryThresholds.YellowMonths` | `int` | `3` | Amarillo entre `YellowMonths` y `GreenMonths` (FR-015) |
| `NextSkuNumber` | `int` | `1` | Contador de la secuencia de SKU por tenant (FR-009). Se incrementa con `UPDATE ... RETURNING` dentro de la transacción del alta |

**Invariantes**

- `MaxTrustedDevices` DEBE ser ≥ 1.
- `TrustedDeviceLifetimeDays` DEBE ser ≥ 1.
- `RefreshTokenLifetimeHours` DEBE ser ≥ 1 y NO DEBE ser menor que la vigencia del access token (≤ 60 min, NFR-004): un refresh más corto que el access token no renueva nada.
- `GreenMonths` DEBE ser > `YellowMonths`.
- `YellowMonths` DEBE ser > 0.

**Aplicación de `TenantId`**: filtro EF + política RLS. El seed inicial queda fuera de este slice (INSERT manual).

---

## 2. `User` (`ApplicationUser`)

`ApplicationUser : IdentityUser, ITenantEntity` — ASP.NET Core Identity extendido con `TenantId` (FR-005).

| Campo | Tipo | Notas |
|---|---|---|
| `Id` | `string` | PK heredada de `IdentityUser` |
| `TenantId` | `Guid` | Tomado del header `X-Tenant-ID` en el registro. Inmutable (FR-004) |
| `Email` | `string` | Heredado. Único **global** en toda la plataforma (T055) — es lo que permite resolver el tenant en el login genérico |
| `PasswordHash` | `string` | PBKDF2 vía Identity (NFR-004) |
| `PhoneNumber` | `string?` | Heredado de `IdentityUser`. **Celular destino del OTP por SMS** — requerido para el 2FA (FR-008). **Alta**: sólo un admin del tenant (T050). **Cambio**: el propio usuario, con OTP al número actual (T061) |
| `PhoneNumberConfirmed` | `bool` | Heredado de `IdentityUser`. Se setea al cargar/validar el número; sólo un admin lo altera |
| `UserName`, `NormalizedEmail`, `SecurityStamp`, … | — | Campos estándar de `IdentityUser` |
| `DeviceFingerprint` | — | Relación con dispositivos confiables. `[PENDIENTE: las fuentes lo nombran como "relación", no como columna escalar; confirmar si es navegación a TrustedDevice o una columna de último fingerprint]` |

**Invariantes**

- `NormalizedEmail` DEBE ser único **en toda la plataforma**; un email ya registrado en cualquier tenant responde `409 Conflict` (FR-005).
- El mismo email NO PUEDE existir en dos tenants. Es el precio del login genérico: sin tenant en la URL, el email es lo único que identifica a qué tenant pertenece quien se loguea (T055). Si en el futuro una persona necesita operar en varios tenants, la salida es una tabla `UserTenant` y un paso de selección post-login — refactor de Identity, no un campo.
- `TenantId` NO DEBE cambiar tras la creación (FR-004).
- El JWT emitido DEBE llevar los claims `sub` (userId) y `tid` (tenantId) — FR-006.

**Invariantes de `PhoneNumber` (canal del 2FA — FR-008)**

- `PhoneNumber` DEBE estar cargado para que el usuario pueda recibir el OTP por SMS desde un dispositivo no trusted.
- El usuario **NO DEBE** poder enrolar ni modificar su `PhoneNumber` por la superficie **self-service de Identity**. ASP.NET Core Identity la expone **por defecto** (`UserManager.SetPhoneNumberAsync`, `ChangePhoneNumberAsync`, `GenerateChangePhoneNumberTokenAsync` y los endpoints self-service del Identity UI/API): hay que **cerrarla explícitamente** — no alcanza con no usarla. Ver T050.
- El **alta** del primer número es acto exclusivo de un admin del tenant (T050). El usuario NO DEBE poder enrolarlo: en ese momento sólo hay **un** factor, y si con él se decidiera el destino del OTP, quien tuviera la contraseña controlaría ambos.
- El **cambio** de un número ya cargado PUEDE hacerlo el propio usuario vía `PUT /api/auth/phone-number` (T061), confirmando un OTP enviado al número **actual**. Ese OTP NUNCA DEBE enviarse al número nuevo, y el cambio NO DEBE aplicarse hasta confirmarlo.
- El único camino de escritura DEBE ser el endpoint admin `PUT /api/admin/users/{userId}/phone-number` (`[PENDIENTE: propuesto, no está en las fuentes originales]`), autorizado sólo para un admin del tenant.
- Toda escritura de `PhoneNumber` DEBE quedar auditada con el admin que la ejecutó.
- Un usuario **sin** `PhoneNumber` cargado DEBE ser **bloqueado** al intentar entrar desde un dispositivo no trusted (`403 AUTH_PHONE_NOT_ENROLLED`). NO DEBE permitirse el ingreso salteando el 2FA: eso convertiría "no cargar el número" en un bypass permanente del segundo factor.
- `[PENDIENTE: formato/validación del PhoneNumber (E.164, sólo móviles colombianos, unicidad por tenant) no definido en las fuentes]`

**Relaciones**

- `N : 1` con `Tenant`
- `1 : N` con `TrustedDevice`
- `1 : N` con `DeviceOtp`

**Índices**

- PK sobre `Id`.
- Único **global** sobre `NormalizedEmail` — es el índice que ASP.NET Core Identity crea por defecto, así que no hay nada que reemplazar en la migración.

**Aplicación de `TenantId`**

- EF: `HasQueryFilter` sobre `ApplicationUser`.
- **Excepción del login (T055)**: `POST /api/auth/login` corre SIN `TenantContext`, así que la búsqueda del usuario por email DEBE usar `IgnoreQueryFilters()` de forma explícita y acotada — una sola consulta, que lee únicamente lo necesario para autenticar y obtener el `TenantId`.
- El filtro NO DEBE volverse permisivo cuando el `TenantContext` está vacío. Hacerlo desactivaría el aislamiento en silencio en cualquier ruta donde el contexto no se haya poblado; la excepción tiene que ser explícita en el punto de uso, nunca un default del filtro.
- Este es el ÚNICO lugar del sistema que cruza la frontera entre tenants a propósito, y DEBE tener un test que demuestre que por ahí no se puede leer nada más.
- RLS: política `tenant_isolation` sobre la tabla de usuarios de Identity.

---

## 3. `TrustedDevice`

`TrustedDevice : ITenantEntity` — dispositivo autorizado para saltear el 2FA (FR-007, FR-008).

| Campo | Tipo | Notas |
|---|---|---|
| `Id` | `Guid` | PK. `[PENDIENTE: las fuentes no nombran la PK explícitamente]` |
| `TenantId` | `Guid` | Inmutable (FR-004) |
| `UserId` | `string` | FK a `ApplicationUser` |
| `DeviceId` | `string` | Identificador del dispositivo enviado por el cliente en el login |
| `Fingerprint` | `string` | Huella del dispositivo. `[PENDIENTE: algoritmo de fingerprint no definido en las fuentes]` |
| `TrustedAt` | `DateTime` | Momento en que el dispositivo quedó confiable |
| `ExpiresAt` | `DateTime` | `TrustedAt + TenantSettings.TrustedDeviceLifetimeDays`. Vencido = vuelve a exigir 2FA (T070) |
| `RevokedAt` | `DateTime?` | `null` = no revocado. Un valor marca baja **manual por un admin** — es auditoría de una acción humana, NO se usa para el vencimiento |

**Invariantes**

- Un dispositivo se cuenta como **activo** si `RevokedAt IS NULL AND ExpiresAt > now()`.
- El número de dispositivos activos por usuario DEBE ser ≤ `TenantSettings.MaxTrustedDevices` (def. 2) — FR-007. La fila `TrustedDevice` **no se crea** cuando el conteo ya alcanzó el máximo.
- El límite gobierna **sólo el privilegio de saltear el 2FA**, no el acceso: la ausencia de un `TrustedDevice` NO DEBE impedir el login (FR-008), sólo obliga al OTP en cada ingreso.
- `RevokedAt` DEBE setearse **únicamente** por acción manual de un admin o del propio usuario sobre su dispositivo (T067). Ninguna ruta automática PUEDE escribir `RevokedAt`, y no existe selección del "más antiguo".
- El **vencimiento** es un mecanismo separado: se resuelve por `ExpiresAt`, nunca escribiendo `RevokedAt` (T070). Mezclarlos borraría la diferencia entre "alguien lo dio de baja" y "se venció solo", que es justamente lo que hace útil la auditoría.
- Sólo un dispositivo trusted existente PUEDE autorizar uno nuevo (FR-007).
- `TrustedAt` DEBE ser ≤ `RevokedAt` cuando ambos existen, y `TrustedAt` DEBE ser < `ExpiresAt`.
- Un dispositivo vencido DEBE volver a exigir OTP, pero NO DEBE bloquear el acceso ni cortar una sesión viva: cortar es T067 (revocación) y T068 (deshabilitar), que son inmediatos.

**Relaciones**

- `N : 1` con `ApplicationUser` (`UserId`)
- `N : 1` con `Tenant` (`TenantId`)

**Índices**

- PK sobre `Id`.
- `(TenantId, UserId, DeviceId)` — lookup del login para decidir si el dispositivo es conocido. `[PENDIENTE: unicidad no explicitada en las fuentes; debería ser único si un DeviceId no puede repetirse por usuario]`
- El índice `(UserId, TrustedAt)` filtrado por `RevokedAt IS NULL` **se elimina**: existía para seleccionar el dispositivo más antiguo y ya no hay selección automática. El único acceso restante es **contar** los activos (`WHERE TenantId = @t AND UserId = @u AND RevokedAt IS NULL`), que se resuelve con el prefijo `(TenantId, UserId)` del índice anterior sobre una cardinalidad de pocas filas por usuario. No se requiere índice adicional.

**Aplicación de `TenantId`**: filtro EF + política RLS.

---

## 3b. `DeviceOtp`

OTP de 2FA **por SMS**, persistido (FR-008, NFR-004). El código se envía al `ApplicationUser.PhoneNumber` vía `ISmsSender`; el email NO es el canal del segundo factor.

| Campo | Tipo | Notas |
|---|---|---|
| `Id` | `Guid` | PK. `[PENDIENTE: no explicitada en las fuentes]` |
| `TenantId` | `Guid` | Inmutable |
| `UserId` | `string` | FK a `ApplicationUser` |
| `DeviceId` | `string` | Dispositivo que se pretende confiar |
| `Channel` | `enum` | `Sms` — único canal soportado. `[PENDIENTE: propuesto; las fuentes no modelaban el canal explícitamente]` |
| `CodeHash` | `string` | Generado con `IPasswordHasher` sobre un valor aleatorio — no se almacena en claro |
| `ExpiresAt` | `DateTime` | `TrustedAt + 10 min` como máximo (NFR-004) |
| `ConsumedAt` | `DateTime?` | `[PENDIENTE: las fuentes no definen si el OTP se marca como consumido o se borra tras el uso]` |

**Invariantes**

- El OTP DEBE expirar en ≤ 10 minutos (NFR-004) — la ventana **se mantiene** con el canal SMS.
- El OTP DEBE enviarse por SMS al `ApplicationUser.PhoneNumber` del usuario que se loguea, nunca por email (FR-008).
- Sin `PhoneNumber` cargado no hay destino de envío: el acceso desde un dispositivo no trusted DEBE **bloquearse** con `403 AUTH_PHONE_NOT_ENROLLED` hasta que un admin del tenant cargue el número (T050). Nunca se saltea el 2FA.
- Un OTP vencido o incorrecto DEBE responder `401` y el intento DEBE quedar registrado (FR-008).

**Índices**: `(TenantId, UserId, DeviceId)`. `[PENDIENTE: política de purga de OTPs vencidos no definida]`

---

## 4. `Product`

`Product : ITenantEntity, IAggregateRoot` — agregado raíz del catálogo (FR-009 … FR-011).

| Campo | Tipo | Nulable | Notas |
|---|---|---|---|
| `Id` | `Guid` | No | PK |
| `TenantId` | `Guid` | No | Inmutable (FR-004) |
| `Sku` | `string` | No | **Inmutable** tras la creación (FR-010). Automático `SKU-{n}` por tenant cuando no hay `Barcode` (FR-009) |
| `Barcode` | `string?` | Sí | PUEDE estar ausente (FR-009) |
| `Name` | `string` | No | Base del autocompletado (FR-011) |
| `Category` | `enum` | No | `Medication` \| `Supplement` \| `PersonalCare` |
| `ActiveIngredient` | `string?` | Sí | Principio activo |
| `Presentation` | `string?` | Sí | Presentación |
| `StorageConditions` | `string?` | Sí | Condiciones de almacenamiento |
| `Currency` | `string` | No | Default `COP`; PUEDE sobrescribir el default del tenant (NFR-008) |

**Invariantes**

- `Sku` DEBE ser único por tenant. Un SKU explícito duplicado responde `409 Conflict` con `errorCode: PRODUCT_SKU_DUPLICATE` (FR-009).
- `Sku` NO DEBE cambiar en un update; el intento responde `400` (FR-010).
- Cuando el request no trae `Sku` explícito, el sistema DEBE generar `SKU-{n}` con una secuencia **por tenant** (FR-009). **Mecanismo decidido: contador `NextSkuNumber` en `tenant_settings`**, incrementado con `UPDATE ... RETURNING` en la misma transacción del alta. El `UPDATE` toma lock de fila, así que dos altas concurrentes del mismo tenant se serializan y NO DEBE haber colisión. Descartados: SEQUENCE por tenant (un objeto de BD por tenant, DDL en runtime y huecos por rollback al no ser transaccional) y `MAX+1` (race condition real entre requests concurrentes).
- **Supuesto**: el SKU se genera siempre que no venga explícito, tenga `Barcode` o no. FR-009 menciona el caso sin barcode, pero `Sku` es NOT NULL: un producto con barcode y sin SKU explícito también necesita uno. El `Barcode` nunca se usa como identificador interno.
- `Barcode`, cuando está presente, DEBE ser único por tenant.
- `Currency` DEBE ser un código ISO-4217. `[PENDIENTE: validación de currency no especificada]`

**Relaciones**

- `N : 1` con `Tenant`
- `1 : N` con `Batch` — por `ProductId`, **sin** navegación de agregado (son ARs separados). Sí hay FK a nivel de base de datos.

**Índices**

| Índice | Tipo | Para qué |
|---|---|---|
| `Id` | PK | — |
| `(TenantId, Sku)` | Único | Unicidad de SKU por tenant (FR-009) |
| `(TenantId, Barcode)` | Único, parcial `WHERE Barcode IS NOT NULL` | Lookup exacto por barcode (FR-011) y unicidad |
| `Name` | Trigram (`pg_trgm` GIN) o full-text | Autocompletado `ILIKE` en `<500ms` (NFR-006) — `[PENDIENTE: las fuentes dicen "trigram/full-text"; elegir uno]` |

**Aplicación de `TenantId`**: filtro EF (`HasQueryFilter`) + política RLS `tenant_isolation ON products USING (TenantId = current_setting('app.tenant')::uuid)`. Todas las queries del catálogo DEBE estar acotadas al tenant (NFR-007).

---

## 5. `Batch`

`Batch : ITenantEntity, IAggregateRoot` — agregado raíz de lote, separado de `Product` para evitar locks en la contención de ajuste y habilitar FEFO futuro (FR-012 … FR-015).

| Campo | Tipo | Nulable | Notas |
|---|---|---|---|
| `Id` | `Guid` | No | PK |
| `TenantId` | `Guid` | No | Inmutable (FR-004) |
| `ProductId` | `Guid` | No | FK a `Product`. Un `ProductId` inexistente rechaza el alta con `404`/`400` (FR-012) |
| `LotNumber` | `string` | No | Número de lote del proveedor |
| `ExpirationDate` | `DateOnly` | No | Base de la semaforización (FR-015) y del FEFO futuro. `[PENDIENTE: las fuentes no precisan si es DateOnly o DateTime]` |
| `CurrentQuantity` | `int` | No | Stock actual. NO DEBE ser negativo (FR-013). `[PENDIENTE: las fuentes no precisan si la cantidad admite decimales]` |
| `LocationShelf` | `string` | No | Ubicación / estante |
| `Status` | `enum` | No | `Active` \| `Depleted` \| `Expired` |
| `Xmin` | `uint` (shadow) | No | **Concurrency token** — ver abajo |

### Concurrency token `xmin` (FR-014)

`xmin` es la columna de sistema de PostgreSQL que guarda el id de la transacción que escribió la fila por última vez. Se incrementa solo en cada `UPDATE`, así que sirve como token de concurrencia optimista **sin agregar columna propia**.

Configuración en `StockmaDbContext.OnModelCreating`:

```csharp
modelBuilder.Entity<Batch>().UseXminAsConcurrencyToken();
// equivalente explícito:
// modelBuilder.Entity<Batch>().Property<uint>("Xmin")
//     .HasColumnName("xmin").IsRowVersion().IsConcurrencyToken();
```

Comportamiento ante conflicto:

- EF Core agrega `WHERE xmin = @original` al `UPDATE`; si no afecta filas, lanza `DbUpdateConcurrencyException`.
- `TransactionBehaviour` (pipeline MediatR) **reintenta una vez** (DEBERÍA, FR-014); si el conflicto persiste, responde `409` con `errorCode: CONCURRENCY_CONFLICT`.
- Elegido sobre ETag string y `Timestamp varbinary` por ser nativo de PostgreSQL.

**Invariantes**

- `CurrentQuantity` NO DEBE ser negativa. Un ajuste que llevaría el stock por debajo de cero se rechaza **por completo** (rollback vía `IUnitOfWork`, sin ajuste parcial) con `422` y `errorCode: BATCH_NEGATIVE_STOCK` (FR-013).
- El `delta` del ajuste DEBE ser distinto de cero.
- `ProductId` DEBE referenciar un `Product` existente **del mismo tenant**.
- La semaforización NO DEBE persistirse: se computa en cada lectura (NFR-010, FR-015).

**Semaforización — `BatchStatusCalculator` (domain service)**

VO `SemaphoreColor` = `Green` | `Yellow` | `Red` | `Expired`. Se computa contra `TenantSettings.Thresholds` (defaults 6/3 meses):

| Color | Condición (con defaults) |
|---|---|
| `Expired` | `ExpirationDate` < hoy |
| `Red` | faltan < 3 meses (`YellowMonths`) |
| `Yellow` | faltan entre 3 y 6 meses |
| `Green` | faltan > 6 meses (`GreenMonths`) |

Con umbrales personalizados (p. ej. verde `>9 meses`), un lote a 7 meses evalúa `Yellow` — escenario "Umbrales personalizados" de FR-015.

`Status` (`Active`/`Depleted`/`Expired`) es un campo **persistido** distinto del color computado. `[PENDIENTE: las fuentes no definen las transiciones de Status — cuándo pasa a Depleted (¿CurrentQuantity == 0?) o a Expired (¿job? ¿al leer?). Sin Hangfire en este slice, la transición automática no tiene ejecutor]`

**Relaciones**

- `N : 1` con `Product` (`ProductId`, FK en base de datos)
- `N : 1` con `Tenant`

**Índices**

| Índice | Tipo | Para qué |
|---|---|---|
| `Id` | PK | — |
| `ProductId` | FK | Integridad referencial con `Product` |
| `(ProductId, ExpirationDate)` | Compuesto | `GetBatchesQuery` y **soporte para el FEFO diferido del slice 2** (NFR-009) |

**Aplicación de `TenantId`**: filtro EF (`HasQueryFilter`) + política RLS `tenant_isolation ON batches`. El `AuditSaveChangesInterceptor` rechaza cualquier modificación de `TenantId`.

---

## Migración `InitialSchema`

Migración **única** que crea, en este orden:

1. Tablas de Identity (usuarios, roles, claims, tokens) con `TenantId` agregado a `ApplicationUser` (`PhoneNumber` y `PhoneNumberConfirmed` vienen de `IdentityUser`)
2. `tenant_settings`
3. `trusted_devices`, `device_otps`
4. `products`, `batches`
5. Índices: unique `(TenantId, Sku)`, unique parcial `(TenantId, Barcode)`, trigram sobre `Name`, compuesto `(ProductId, ExpirationDate)`
6. `ENABLE ROW LEVEL SECURITY` + política `tenant_isolation` en cada tabla tenant
7. Rol **no-propietario** `app_user` con los grants necesarios

Patrón: migraciones tenant-wide sobre una DB compartida con filtrado lógico. Arranque local: `docker compose up postgres` → `dotnet ef database update` desde `backend/src/Infrastructure`.

**Rollback**: greenfield sin datos — drop + migrate.

---

## Resumen de `[PENDIENTE]`

| # | Entidad | Hueco |
|---|---|---|
| 1 | `Tenant` | Campos de negocio (nombre, NIT, estado, moneda por defecto) sin definir |
| 2 | `Tenant` | Alta/onboarding fuera de alcance; seed manual |
| 3 | `Tenant` | ¿RLS o restricción por rol sobre la tabla `tenants`? |
| 4 | `User` | `DeviceFingerprint`: ¿navegación o columna escalar? |
| 4b | `User` | ~~`PhoneNumber` sin cargar~~ — **RESUELTO**: bloqueo con `403 AUTH_PHONE_NOT_ENROLLED`; el alta es acto de admin (T050) y el cambio es self-service con OTP al número actual (T061) |
| 4c | `User` | Formato/validación y unicidad por tenant del `PhoneNumber`; endpoint admin propuesto, no en las fuentes |
| 5 | `User` | ~~Índice único global vs. compuesto per-tenant~~ — **resuelto (T055)**: se usa el índice global por defecto de Identity |
| 6 | `User` | `HasQueryFilter` durante el login — **acotado (T055)**: el login corre sin `TenantContext` y usa `IgnoreQueryFilters()` explícito en una única consulta |
| 7 | `TrustedDevice` | Algoritmo de `Fingerprint`; unicidad de `(TenantId, UserId, DeviceId)` |
| 8 | `DeviceOtp` | ¿`ConsumedAt` o borrado? Política de purga |
| 8b | `DeviceOtp` | Proveedor de SMS no elegido; columna `Channel` propuesta, no en las fuentes |
| 9 | `Product` | Mecanismo de la secuencia `SKU-{n}` per-tenant (colisión bajo concurrencia) |
| 10 | `Product` | Trigram vs. full-text para el autocompletado; validación de `Currency` |
| 11 | `Batch` | `DateOnly` vs. `DateTime` en `ExpirationDate`; entero vs. decimal en `CurrentQuantity` |
| 12 | `Batch` | Transiciones de `Status` (`Active` → `Depleted` / `Expired`) sin ejecutor definido |
