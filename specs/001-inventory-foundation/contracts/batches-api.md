# Contrato: Batches API

**Base**: `/api/batches`
**Requerimientos**: FR-012 … FR-015 · NFR-009, NFR-010
**Fuentes**: [`../spec.md`](../spec.md) · [`../plan.md`](../plan.md) (sección 6) · [`../data-model.md`](../data-model.md)

---

## Reglas transversales

| Regla | Detalle |
|---|---|
| Header de tenant | `X-Tenant-ID: {guid}` DEBE estar presente. Ausente o no parseable → `400` (FR-001) |
| Autenticación | `Authorization: Bearer {jwt}` DEBE estar presente. `401` si falta o expiró |
| Acotamiento | Todas las consultas DEBE estar acotadas al `TenantId` — filtro EF + RLS |
| Formato de error | `application/problem+json` (`ProblemDetails`) con extensión `errorCode` |
| Concurrencia | `Batch` usa `xmin` de PostgreSQL como concurrency token; el pipeline reintenta **una vez** y luego responde `409` (FR-014) |
| Semaforización | `semaphoreColor` se **computa en cada lectura**, nunca se persiste (FR-015, NFR-010) |
| FEFO | **DIFERIDO** al slice 2. Este contrato no expone selección FEFO; el índice `(ProductId, ExpirationDate)` queda como soporte (NFR-009) |

### `BatchDto`

```json
{
  "id": "c41a7d92-5b60-4e18-a3f2-9d0c7e14b688",
  "productId": "3b8d1f60-2c14-4a9e-8f77-0a5b3e2d9c41",
  "lotNumber": "L-2026-0417",
  "expirationDate": "2027-04-30",
  "currentQuantity": 120,
  "locationShelf": "A-03",
  "status": "Active",
  "semaphoreColor": "Verde"
}
```

- `status` ∈ `Active` | `Depleted` | `Expired` — **persistido**.
- `semaphoreColor` ∈ `Verde` | `Amarillo` | `Rojo` | `Vencido` — **computado**, no persistido.

---

## Ajuste que dejaría stock negativo

**Un único comportamiento** (decisión cerrada — `spec.md` → Decisiones ya cerradas):

| Aspecto | Valor |
|---|---|
| Status | **`422 Unprocessable Entity`** |
| `errorCode` | `BATCH_NEGATIVE_STOCK` |
| Semántica | Rechazo **total** del ajuste (rollback vía `IUnitOfWork`). El sistema NO DEBE aplicar un ajuste parcial |

> Nota de desambiguación: el `409` de `CONCURRENCY_CONFLICT` (FR-014) es **otro** caso; se distingue por status y por `errorCode`.

---

## `POST /api/batches` — FR-012

Registra un lote vinculado a un `Product` existente del mismo tenant.

**Command**: `RegisterBatchCommand`

### Request

```http
POST /api/batches
X-Tenant-ID: 6f2c1b3a-8e41-4f2d-9c7a-1d5e8b0a3f77
Authorization: Bearer eyJhbGciOiJIUzI1NiIs...
Content-Type: application/json
```

```json
{
  "productId": "3b8d1f60-2c14-4a9e-8f77-0a5b3e2d9c41",
  "lotNumber": "L-2026-0417",
  "expirationDate": "2027-04-30",
  "currentQuantity": 120,
  "locationShelf": "A-03"
}
```

### Respuesta `201 Created`

```http
Location: /api/batches/c41a7d92-5b60-4e18-a3f2-9d0c7e14b688
```

```json
{
  "id": "c41a7d92-5b60-4e18-a3f2-9d0c7e14b688",
  "productId": "3b8d1f60-2c14-4a9e-8f77-0a5b3e2d9c41",
  "lotNumber": "L-2026-0417",
  "expirationDate": "2027-04-30",
  "currentQuantity": 120,
  "locationShelf": "A-03",
  "status": "Active",
  "semaphoreColor": "Verde"
}
```

### Errores

| Status | `errorCode` | Cuándo |
|---|---|---|
| `400` | `TENANT_HEADER_MISSING` / `TENANT_HEADER_INVALID` | Header inválido |
| `400` | `VALIDATION_FAILED` | `lotNumber`, `expirationDate`, `currentQuantity` o `locationShelf` ausentes o inválidos |
| `401` | — | JWT ausente o expirado |
| `400` / `404` | `BATCH_PRODUCT_NOT_FOUND` | `productId` inexistente en el tenant (FR-012). `[PENDIENTE: la spec dice "rechazada con 404/400" sin decidir cuál; el errorCode tampoco está nombrado en las fuentes]` |

### Escenarios (Dado/Cuando/Entonces)

**Lote válido**
- **DADO** producto existente y datos de lote válidos
- **CUANDO** `POST /api/batches`
- **ENTONCES** retorna `201` con `BatchDto`

**Producto inexistente**
- **DADO** `ProductId` inválido
- **CUANDO** registrar lote
- **ENTONCES** rechazada con `404`/`400`

---

## `POST /api/batches/{id}/adjust` — FR-013, FR-014

Ajusta `currentQuantity` con un delta con signo. El ajuste es **atómico y total**: o se aplica completo, o se rechaza sin cambios.

**Command**: `AdjustBatchStockCommand`

### Request

```http
POST /api/batches/c41a7d92-5b60-4e18-a3f2-9d0c7e14b688/adjust
X-Tenant-ID: 6f2c1b3a-8e41-4f2d-9c7a-1d5e8b0a3f77
Authorization: Bearer eyJhbGciOiJIUzI1NiIs...
Content-Type: application/json
```

```json
{
  "delta": -5
}
```

| Campo | Tipo | Reglas |
|---|---|---|
| `delta` | `int` | DEBE ser ≠ 0. Positivo = entrada, negativo = salida |

> `[PENDIENTE: las fuentes no definen un campo de motivo/razón del ajuste ni trazabilidad de quién lo hizo más allá del AuditSaveChangesInterceptor]`

### Respuesta `200 OK`

```json
{
  "id": "c41a7d92-5b60-4e18-a3f2-9d0c7e14b688",
  "productId": "3b8d1f60-2c14-4a9e-8f77-0a5b3e2d9c41",
  "lotNumber": "L-2026-0417",
  "expirationDate": "2027-04-30",
  "currentQuantity": 115,
  "locationShelf": "A-03",
  "status": "Active",
  "semaphoreColor": "Verde"
}
```

### Errores

| Status | `errorCode` | Cuándo |
|---|---|---|
| `400` | `VALIDATION_FAILED` | `delta` ausente o igual a `0` |
| `400` | `TENANT_HEADER_MISSING` / `TENANT_HEADER_INVALID` | Header inválido |
| `401` | — | JWT ausente o expirado |
| `404` | `BATCH_NOT_FOUND` | El lote no existe en el tenant activo. `[PENDIENTE: errorCode no nombrado en las fuentes]` |
| **`422`** | **`BATCH_NEGATIVE_STOCK`** | El ajuste dejaría `currentQuantity < 0`. Rechazo total, stock sin cambios (FR-013) |
| `409` | `CONCURRENCY_CONFLICT` | Conflicto de `xmin` que persiste tras un reintento (FR-014) |

#### Stock negativo — `422`

```json
{
  "type": "https://stockma.co/errors/batch-negative-stock",
  "title": "Stock insuficiente",
  "status": 422,
  "errorCode": "BATCH_NEGATIVE_STOCK",
  "detail": "El ajuste de -5 dejaría el lote en -2 unidades. El ajuste fue rechazado por completo.",
  "currentQuantity": 3,
  "requestedDelta": -5
}
```

> `[PENDIENTE: los campos currentQuantity y requestedDelta no están en las fuentes; confirmar si el detalle expone el stock actual]`

#### Conflicto de concurrencia — `409`

```json
{
  "type": "https://stockma.co/errors/concurrency-conflict",
  "title": "Conflicto de concurrencia",
  "status": 409,
  "errorCode": "CONCURRENCY_CONFLICT",
  "detail": "El lote fue modificado por otra operación. Reintente."
}
```

Comportamiento: EF Core detecta el `xmin` desactualizado y lanza `DbUpdateConcurrencyException`; `TransactionBehaviour` **reintenta una vez** (DEBERÍA, FR-014); si el conflicto persiste, responde `409`.

### Escenarios (Dado/Cuando/Entonces)

**Aumento de stock**
- **DADO** lote con stock 10
- **CUANDO** ajustar `+5`
- **ENTONCES** `CurrentQuantity = 15`

**Salida excede stock**
- **DADO** lote con stock 3
- **CUANDO** ajustar `-5`
- **ENTONCES** rechazada (`422`, `errorCode: BATCH_NEGATIVE_STOCK`) y stock sin cambios

**Conflicto concurrente**
- **DADO** dos updates simultáneos sobre el mismo lote
- **CUANDO** el segundo llega
- **ENTONCES** reintenta una vez; si persiste, responde `409`

---

## `GET /api/batches?productId=` — FR-015

Lista los lotes del tenant, con el `semaphoreColor` computado en la lectura.

**Query**: `GetBatchesQuery`

### Request

```http
GET /api/batches?productId=3b8d1f60-2c14-4a9e-8f77-0a5b3e2d9c41
X-Tenant-ID: 6f2c1b3a-8e41-4f2d-9c7a-1d5e8b0a3f77
Authorization: Bearer eyJhbGciOiJIUzI1NiIs...
```

| Parámetro | Tipo | Obligatorio | Notas |
|---|---|---|---|
| `productId` | `guid` | `[PENDIENTE: las fuentes no dicen si es obligatorio o si sin él lista todos los lotes del tenant]` | Filtra por producto |

> `[PENDIENTE: paginación y orden de la lista no definidos en las fuentes. El índice (ProductId, ExpirationDate) sugiere orden por vencimiento ascendente — confirmar]`

### Respuesta `200 OK`

```json
[
  {
    "id": "c41a7d92-5b60-4e18-a3f2-9d0c7e14b688",
    "productId": "3b8d1f60-2c14-4a9e-8f77-0a5b3e2d9c41",
    "lotNumber": "L-2026-0417",
    "expirationDate": "2027-04-30",
    "currentQuantity": 115,
    "locationShelf": "A-03",
    "status": "Active",
    "semaphoreColor": "Verde"
  },
  {
    "id": "0d5f8a17-91c3-4b26-8e40-72af1c9d3b55",
    "productId": "3b8d1f60-2c14-4a9e-8f77-0a5b3e2d9c41",
    "lotNumber": "L-2025-1130",
    "expirationDate": "2025-11-30",
    "currentQuantity": 8,
    "locationShelf": "A-04",
    "status": "Active",
    "semaphoreColor": "Vencido"
  }
]
```

### Cálculo de `semaphoreColor` (FR-015)

`BatchStatusCalculator` (domain service) evalúa `ExpirationDate` contra `TenantSettings.SemaforoUmbrales`:

| Color | Condición (defaults 6/3 meses) |
|---|---|
| `Vencido` | `expirationDate` < hoy |
| `Rojo` | faltan < `AmarilloMeses` (3 meses) |
| `Amarillo` | faltan entre `AmarilloMeses` y `VerdeMeses` (3–6 meses) |
| `Verde` | faltan > `VerdeMeses` (6 meses) |

Los umbrales son **configurables por tenant**: con verde `>9 meses`, un lote a 7 meses evalúa `Amarillo`.

El color NO DEBE persistirse (NFR-010).

### Errores

| Status | `errorCode` | Cuándo |
|---|---|---|
| `400` | `TENANT_HEADER_MISSING` / `TENANT_HEADER_INVALID` | Header inválido |
| `400` | `VALIDATION_FAILED` | `productId` no es un GUID válido |
| `401` | — | JWT ausente o expirado |

Sin lotes que coincidan: `200` con array vacío.

### Escenarios (Dado/Cuando/Entonces)

**Verde por defecto**
- **DADO** lote a 7 meses de vencer
- **CUANDO** se consulta
- **ENTONCES** `color = Verde`

**Vencido**
- **DADO** lote con `ExpirationDate` pasada
- **CUANDO** se consulta
- **ENTONCES** `color = Vencido`

**Umbrales personalizados**
- **DADO** tenant configura verde `>9 meses`
- **CUANDO** lote a 7 meses
- **ENTONCES** el color se evalúa con los umbrales del tenant (`Amarillo`)

---

## Cobertura de requerimientos

| Requerimiento | Endpoint |
|---|---|
| FR-012 | `POST /api/batches` |
| FR-013 | `POST /api/batches/{id}/adjust` (`422` `BATCH_NEGATIVE_STOCK`) |
| FR-014 | `POST /api/batches/{id}/adjust` (`409` `CONCURRENCY_CONFLICT`) |
| FR-015 | `GET /api/batches?productId=` (`semaphoreColor`) |
| NFR-009 | Índice `(ProductId, ExpirationDate)` — soporte para FEFO diferido |
| NFR-010 | `semaphoreColor` computado en lectura, no persistido |

## Fuera de alcance

- **Selección FEFO** (`FefoBatchSelector`) y agregado `Sale` — slice 2. Este contrato no expone endpoint de selección por vencimiento.
- Alertas proactivas de vencimiento (Hangfire, emails) — slice 3.
- `GET /api/batches/{id}` individual y `DELETE` — no definidos en las fuentes. `[PENDIENTE]`
- Transiciones automáticas de `status` (`Active` → `Depleted` / `Expired`) — sin ejecutor en este slice. `[PENDIENTE: ver data-model.md]`
