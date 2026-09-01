# Checklist de Requerimientos: Inventory Foundation

**Feature branch**: `001-inventory-foundation`
**Fuente**: [`../spec.md`](../spec.md) — un ítem por requerimiento, trazable al ID
**Referencias**: [`../plan.md`](../plan.md) · [`../tasks.md`](../tasks.md) · [`../data-model.md`](../data-model.md) · [`../contracts/`](../contracts/)

Marcar un ítem sólo cuando el comportamiento esté implementado **y** cubierto por al menos un test verde.

---

## Requerimientos Funcionales

### tenant-isolation

- [ ] **FR-001 — Resolución de tenant por request**
      El sistema DEBE resolver el `TenantId` en cada request autenticada mediante middleware `TenantContext` a partir del header `X-Tenant-ID`.
      *Tareas*: T009 · *Contratos*: los tres (`X-Tenant-ID` transversal)
      **Verificación**: request con header válido expone el `TenantId` durante toda la request; sin header responde `400` y no ejecuta el pipeline.

- [ ] **FR-002 — Filtro global EF Core**
      El sistema DEBE aplicar `HasQueryFilter` a toda entidad que implemente `ITenantEntity`; toda entidad tenant DEBE implementar `ITenantEntity`.
      *Tareas*: T008, T010, T013 · *Modelo*: `data-model.md` → "Capa 1"
      **Verificación**: tenant A consultando productos/lotes sólo ve los suyos; el test de arquitectura hace fallar el build ante una entidad tenant sin `ITenantEntity`.

- [ ] **FR-003 — Respaldo RLS PostgreSQL**
      El sistema DEBERÍA habilitar Row-Level Security como defensa en profundidad además del filtro EF.
      *Tareas*: T011 · *Modelo*: `data-model.md` → "Capa 2"
      **Verificación**: una query directa con el rol `app_user`, sin filtro de aplicación, retorna sólo filas del tenant de `current_setting('app.tenant')`.

- [ ] **FR-004 — TenantId inmutable**
      El sistema NO DEBE permitir modificar `TenantId` en updates de entidades tenant.
      *Tareas*: T012
      **Verificación**: un update que intenta cambiar `TenantId` es rechazado por `AuditSaveChangesInterceptor` y persiste el `TenantId` original.

### identity-access

- [ ] **FR-005 — Registro de usuario**
      El sistema DEBE registrar usuarios con ASP.NET Core Identity asociando el `TenantId` del header `X-Tenant-ID`.
      *Tareas*: T015, T017, T019, T022 · *Contrato*: `POST /api/auth/register`
      **Verificación**: `201` con `userId` y `ApplicationUser.TenantId` seteado; email duplicado **en el mismo tenant** responde `409`.

- [ ] **FR-006 — Login JWT**
      El sistema DEBE emitir JWT con claims de usuario y tenant tras credenciales válidas.
      *Tareas*: T016, T017, T019, T021 · *Contrato*: `POST /api/auth/login`
      **Verificación**: dispositivo confiable recibe `accessToken` con claims `sub` y `tid`; credenciales inválidas responden `401` sin revelar qué factor falló.

- [ ] **FR-007 — Dispositivos confiables (máx 2, configurable)**
      El sistema DEBE limitar a `MaxTrustedDevices` (def. 2, configurable por tenant) los dispositivos que pueden **saltear el 2FA**, NO DEBE usar ese límite para negar el acceso y NO DEBE revocar dispositivos automáticamente.
      *Tareas*: T020 · *Contrato*: `POST /api/auth/confirm-device`
      **Verificación**:
      1. Con menos de `MaxTrustedDevices` activos, `confirm-device` responde `200` con `deviceTrusted: true` y crea la fila `TrustedDevice` (`TrustedAt` seteado, `RevokedAt = null`).
      2. Con el máximo alcanzado, `confirm-device` responde `200` con `accessToken` y `deviceTrusted: false`: **no** crea `TrustedDevice`, **no** setea `RevokedAt` en ninguna fila existente y **no** devuelve ningún status de error.
      3. Ese dispositivo sin slot vuelve a recibir `requiresDeviceConfirmation` en el login siguiente (OTP en cada ingreso).
      4. Cambiar `TenantSettings.MaxTrustedDevices` altera el umbral sin recompilar; el conteo cuenta sólo `RevokedAt IS NULL`.
      5. `RevokedAt` sólo cambia por la acción manual del admin; ninguna ruta de login o `confirm-device` lo modifica.

- [ ] **FR-008 — 2FA por SMS en dispositivo nuevo**
      El sistema DEBE exigir OTP enviado por **SMS** al `ApplicationUser.PhoneNumber` cuando se detecta un dispositivo no conocido. El `PhoneNumber` DEBE ser escribible sólo por un admin del tenant; el usuario NO DEBE poder modificar el suyo.
      *Tareas*: T015, T018, T019, T021, T049, T050 · *Contrato*: `POST /api/auth/login` + `POST /api/auth/confirm-device` + `PUT /api/admin/users/{userId}/phone-number`
      **Verificación**:
      1. Login desde dispositivo desconocido responde `requiresDeviceConfirmation` y el OTP llega por **SMS** al `PhoneNumber` del usuario (no por email).
      2. OTP válido marca el dispositivo confiable y emite JWT; OTP inválido o vencido responde `401` y registra el intento.
      3. `PUT /api/admin/users/{userId}/phone-number` con JWT de admin persiste el número; el mismo intento hecho por el propio usuario no admin responde `403`.
      4. La superficie self-service de Identity sobre `PhoneNumber` (`SetPhoneNumberAsync`, `ChangePhoneNumberAsync`, endpoints del Identity UI/API) está **cerrada**: no existe ruta por la que el usuario cambie su propio número.
      `[PENDIENTE: usuario sin PhoneNumber cargado — definir si el primer login se permite sin 2FA, si el admin debe cargar el número antes de habilitar la cuenta, o si se bloquea]`
      `[PENDIENTE: proveedor de SMS no elegido]`
      `[PENDIENTE: endpoint admin de PhoneNumber propuesto, no está en las fuentes originales]`

### product-catalog

- [ ] **FR-009 — Registro de producto con SKU automático**
      El sistema DEBE generar SKU interno único por tenant cuando el producto no tiene `Barcode`; `Barcode` PUEDE estar ausente.
      *Tareas*: T023, T025, T029 · *Contrato*: `POST /api/products`
      **Verificación**: alta sin barcode asigna `SKU-{n}` y retorna `201`; SKU explícito duplicado responde `409` con `PRODUCT_SKU_DUPLICATE`.
      `[PENDIENTE: mecanismo de la secuencia per-tenant sin definir — ver data-model.md]`

- [ ] **FR-010 — Actualización de producto**
      El sistema DEBE actualizar campos del catálogo; el SKU DEBE ser inmutable tras la creación.
      *Tareas*: T023, T025, T029 · *Contrato*: `PUT /api/products/{id}`
      **Verificación**: update válido conserva el SKU; un body con SKU nuevo responde `400` con `PRODUCT_SKU_IMMUTABLE`.

- [ ] **FR-011 — Búsqueda por nombre y barcode**
      El sistema DEBE soportar búsqueda por nombre (autocompletado) y lookup exacto por `Barcode`, ambos acotados al tenant.
      *Tareas*: T026, T027, T030 · *Contrato*: `GET /api/products?query=` + `GET /api/products/by-barcode/{code}`
      **Verificación**: `query=aceta` retorna "Acetaminofén 500mg" ordenado por relevancia; el lookup por barcode retorna el producto o `404`.
      `[PENDIENTE: criterio operativo de "relevancia" sin definir]`

### batch-inventory

- [ ] **FR-012 — Registro de lote**
      El sistema DEBE registrar lotes vinculados a un `Product` con `LotNumber`, `ExpirationDate`, `CurrentQuantity` y `LocationShelf`.
      *Tareas*: T031, T033, T038 · *Contrato*: `POST /api/batches`
      **Verificación**: lote válido retorna `201` con `BatchDto`; `ProductId` inexistente es rechazado con `404`/`400`.
      `[PENDIENTE: la spec no decide entre 404 y 400]`

- [ ] **FR-013 — Ajuste de stock**
      El sistema DEBE ajustar `CurrentQuantity` con delta con signo y NO DEBE permitir cantidad negativa.
      *Tareas*: T033, T037 · *Contrato*: `POST /api/batches/{id}/adjust`
      **Verificación**: `+5` sobre stock 10 deja 15; `-5` sobre stock 3 es rechazado **por completo** con `422` / `BATCH_NEGATIVE_STOCK` y el stock queda sin cambios (nunca ajuste parcial).

- [ ] **FR-014 — Concurrencia optimista**
      El sistema DEBE usar `xmin` de PostgreSQL como concurrency token y DEBERÍA reintentar conflictos una vez.
      *Tareas*: T031, T032, T037 · *Contrato*: `POST /api/batches/{id}/adjust` (`409` `CONCURRENCY_CONFLICT`)
      **Verificación**: dos updates simultáneos sobre el mismo lote — el segundo reintenta una vez y, si persiste el conflicto, responde `409`.

- [ ] **FR-015 — Semaforización de vencimiento**
      El sistema DEBE clasificar cada lote con umbrales configurables por tenant: Verde `>6 meses`, Amarillo `3–6 meses`, Rojo `<3 meses`, Vencido (fecha pasada).
      *Tareas*: T031, T034, T037 · *Contrato*: `GET /api/batches?productId=`
      **Verificación**: lote a 7 meses → `Verde` con defaults; `ExpirationDate` pasada → `Vencido`; con verde `>9 meses` configurado, un lote a 7 meses → `Amarillo`.

---

## Requerimientos No Funcionales

- [ ] **NFR-001 — tenant-isolation · Seguridad**
      El sistema DEBE resistir fuga cross-tenant con tests de integración y arquitectura obligatorios.
      *Tareas*: T013, T014
      **Verificación**: `TenantIsolationTests` (Testcontainers) confirma respuestas vacías cross-tenant; `TenantArchitectureTests` está en el pipeline de CI.

- [ ] **NFR-002 — tenant-isolation · Performance**
      La resolución de tenant DEBERÍA ser O(1) (cacheada en request scope).
      *Tareas*: T009
      **Verificación**: `TenantContext` es scoped/`AsyncLocal`; no hay lookup a base de datos por request para resolver el tenant.

- [ ] **NFR-003 — tenant-isolation · RLS con rol no-propietario**
      RLS DEBERÍA aplicarse con rol no-propietario en la migración inicial.
      *Tareas*: T011
      **Verificación**: la migración `InitialSchema` crea el rol `app_user` no-propietario y la app se conecta con él.

- [ ] **NFR-004 — identity-access · Seguridad**
      Hash PBKDF2 (Identity); el JWT DEBERÍA expirar en ≤60 min; el OTP DEBERÍA expirar en ≤10 min.
      *Tareas*: T016, T018, T049
      **Verificación**: `Jwt:ExpiresMinutes = 60`; `DeviceOtp.ExpiresAt` ≤ `now + 10min` (la ventana **se mantiene** con el canal SMS); el OTP se despacha por `ISmsSender` al `PhoneNumber` y el código no queda en claro en logs; el `PasswordHash` usa el hasher por defecto de Identity.

- [ ] **NFR-005 — identity-access · Rate limiting**
      El login DEBERÍA aplicar rate limiting (p. ej. 5 intentos por IP/minuto).
      *Tareas*: T019
      **Verificación**: el sexto intento en un minuto desde la misma IP responde `429`.

- [ ] **NFR-006 — product-catalog · Performance**
      La búsqueda por nombre DEBERÍA responder en `<500ms` (índice trigram/full-text).
      *Tareas*: T026
      **Verificación**: índice creado sobre `Name`; medición de la query de autocompletado por debajo de 500ms.
      `[PENDIENTE: elegir trigram vs. full-text]`

- [ ] **NFR-007 — product-catalog · Seguridad**
      Las consultas DEBE estar acotadas al `TenantId`.
      *Tareas*: T028 + filtro EF (T010) + RLS (T011)
      **Verificación**: los índices únicos son compuestos con `TenantId`; ningún endpoint de productos consulta sin el filtro global.

- [ ] **NFR-008 — product-catalog · Moneda**
      El `Currency` del producto PUEDE sobrescribir el COP por defecto del tenant.
      *Tareas*: T023
      **Verificación**: `Product.Currency` acepta un valor distinto de `COP` y se persiste.
      `[PENDIENTE: validación de código ISO-4217 no especificada; el "COP por defecto del tenant" no tiene campo definido en Tenant]`

- [ ] **NFR-009 — batch-inventory · Índice para FEFO futuro**
      El índice `(ProductId, ExpirationDate)` DEBERÍA existir para soportar FEFO futuro.
      *Tareas*: T036
      **Verificación**: la migración crea el índice compuesto. El selector FEFO en sí queda **DIFERIDO** al slice 2.

- [ ] **NFR-010 — batch-inventory · Semaforización no persistida**
      La semaforización DEBERÍA computarse al leer (no persistida).
      *Tareas*: T034
      **Verificación**: no existe columna de color en la tabla `batches`; `semaphoreColor` sólo aparece en el DTO de lectura.

---

## Criterios de éxito del slice

- [ ] CI verde: `dotnet build` + `dotnet test` + frontend build/lint
- [ ] Test de integración: tenant A no ve productos/lotes de tenant B
- [ ] Test de arquitectura: toda entidad tenant implementa `ITenantEntity`
- [ ] Login JWT + 2FA por SMS en dispositivo nuevo (sender de consola en dev) `[PENDIENTE: proveedor de SMS no elegido]`
- [ ] Registrar producto y lote vía API con header `X-Tenant-ID`
- [ ] Cobertura ≥ 70% (`.specify/config.yaml` → `verify.coverage_threshold`)

## Bloqueantes antes de `apply`

- [ ] **Guard de carga de revisión** — `Riesgo del presupuesto de 400 líneas: High`, `PRs encadenados recomendados: Yes`, `Decisión requerida antes de apply: Yes`. Con `delivery_strategy = ask-on-risk`, decidir PRs encadenados vs. `size:exception` antes de implementar.
