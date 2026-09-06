# Contrato: Products API

**Base**: `/api/products`
**Requerimientos**: FR-009 … FR-011 · NFR-006, NFR-007, NFR-008
**Fuentes**: [`../spec.md`](../spec.md) · [`../plan.md`](../plan.md) (sección 6) · [`../data-model.md`](../data-model.md)

---

## Reglas transversales

| Regla | Detalle |
|---|---|
| Header de tenant | `X-Tenant-ID: {guid}` DEBE estar presente. Ausente o no parseable → `400` (FR-001) |
| Autenticación | `Authorization: Bearer {jwt}` DEBE estar presente. `401` si falta o expiró |
| Acotamiento | Todas las consultas DEBE estar acotadas al `TenantId` (NFR-007) — filtro EF + RLS |
| Formato de error | `application/problem+json` (`ProblemDetails`) con extensión `errorCode` |
| Moneda | `Currency` por defecto `COP`; el producto PUEDE sobrescribir el default del tenant (NFR-008) |

### `ProductDto`

```json
{
  "id": "3b8d1f60-2c14-4a9e-8f77-0a5b3e2d9c41",
  "sku": "SKU-1042",
  "barcode": "7702001234567",
  "name": "Acetaminofén 500mg",
  "category": "Medication",
  "activeIngredient": "Acetaminofén",
  "presentation": "Caja x 20 tabletas",
  "storageConditions": "Lugar seco, < 30 °C",
  "currency": "COP"
}
```

`category` ∈ `Medication` | `Supplement` | `PersonalCare`. `barcode`, `activeIngredient`, `presentation` y `storageConditions` PUEDE ser `null`.

> `[PENDIENTE: las fuentes no definen campos de precio ni de stock agregado en ProductDto. El stock vive en Batch]`

---

## `POST /api/products` — FR-009

Registra un producto. Si no se envía `barcode` ni `sku`, el sistema genera un SKU interno automático `SKU-{n}`, único por tenant.

**Command**: `RegisterProductCommand`

### Request

```http
POST /api/products
X-Tenant-ID: 6f2c1b3a-8e41-4f2d-9c7a-1d5e8b0a3f77
Authorization: Bearer eyJhbGciOiJIUzI1NiIs...
Content-Type: application/json
```

```json
{
  "name": "Acetaminofén 500mg",
  "category": "Medication",
  "activeIngredient": "Acetaminofén",
  "presentation": "Caja x 20 tabletas",
  "storageConditions": "Lugar seco, < 30 °C",
  "currency": "COP"
}
```

Sin `barcode` ni `sku` → el sistema asigna `SKU-{n}`.

### Respuesta `201 Created`

```http
Location: /api/products/3b8d1f60-2c14-4a9e-8f77-0a5b3e2d9c41
```

```json
{
  "id": "3b8d1f60-2c14-4a9e-8f77-0a5b3e2d9c41",
  "sku": "SKU-1042",
  "barcode": null,
  "name": "Acetaminofén 500mg",
  "category": "Medication",
  "activeIngredient": "Acetaminofén",
  "presentation": "Caja x 20 tabletas",
  "storageConditions": "Lugar seco, < 30 °C",
  "currency": "COP"
}
```

### Errores

| Status | `errorCode` | Cuándo |
|---|---|---|
| `400` | `TENANT_HEADER_MISSING` / `TENANT_HEADER_INVALID` | Header `X-Tenant-ID` ausente o inválido |
| `400` | `VALIDATION_FAILED` | `name` o `category` ausentes / inválidos |
| `401` | — | JWT ausente o expirado |
| `409` | `PRODUCT_SKU_DUPLICATE` | El `sku` explícito ya existe en el tenant (FR-009) |
| `409` | `PRODUCT_BARCODE_DUPLICATE` | El `barcode` ya existe en el tenant. `[PENDIENTE: las fuentes definen unique (TenantId, Barcode) pero no nombran el errorCode; propuesto por simetría con PRODUCT_SKU_DUPLICATE]` |

```json
{
  "type": "https://stockma.co/errors/product-sku-duplicate",
  "title": "SKU duplicado",
  "status": 409,
  "errorCode": "PRODUCT_SKU_DUPLICATE",
  "detail": "Ya existe un producto con el SKU 'SKU-1042' en este tenant."
}
```

### Escenarios (Dado/Cuando/Entonces)

**Sin barcode**
- **DADO** `POST /api/products` con nombre/categoría sin barcode
- **CUANDO** se registra
- **ENTONCES** asigna SKU interno automático y retorna `201`

**SKU duplicado**
- **DADO** SKU explícito ya existente en el tenant
- **CUANDO** registrar
- **ENTONCES** responde `409 Conflict`

---

## `PUT /api/products/{id}` — FR-010

Actualiza los campos editables del catálogo. El `sku` es **inmutable**.

**Command**: `UpdateProductCommand`

### Request

```http
PUT /api/products/3b8d1f60-2c14-4a9e-8f77-0a5b3e2d9c41
X-Tenant-ID: 6f2c1b3a-8e41-4f2d-9c7a-1d5e8b0a3f77
Authorization: Bearer eyJhbGciOiJIUzI1NiIs...
Content-Type: application/json
```

```json
{
  "name": "Acetaminofén 500mg x 20",
  "category": "Medication",
  "barcode": "7702001234567",
  "activeIngredient": "Acetaminofén",
  "presentation": "Caja x 20 tabletas",
  "storageConditions": "Lugar seco, < 25 °C",
  "currency": "COP"
}
```

### Respuesta `200 OK`

Retorna el `ProductDto` actualizado, con el `sku` original conservado.

### Errores

| Status | `errorCode` | Cuándo |
|---|---|---|
| `400` | `PRODUCT_SKU_IMMUTABLE` | El body incluye un `sku` distinto al actual (FR-010) |
| `400` | `VALIDATION_FAILED` | Campos inválidos |
| `400` | `TENANT_HEADER_MISSING` / `TENANT_HEADER_INVALID` | Header inválido |
| `401` | — | JWT ausente o expirado |
| `404` | `PRODUCT_NOT_FOUND` | El `id` no existe **en el tenant activo**. `[PENDIENTE: errorCode no nombrado en las fuentes]` |
| `409` | `PRODUCT_BARCODE_DUPLICATE` | El nuevo `barcode` ya pertenece a otro producto del tenant |

```json
{
  "type": "https://stockma.co/errors/product-sku-immutable",
  "title": "SKU inmutable",
  "status": 400,
  "errorCode": "PRODUCT_SKU_IMMUTABLE",
  "detail": "El SKU no puede modificarse después de la creación."
}
```

> Nota de aislamiento: un `id` que existe en otro tenant responde `404`, no `403` — el filtro global lo hace invisible (NFR-007).

### Escenarios (Dado/Cuando/Entonces)

**Update válido**
- **DADO** producto existente
- **CUANDO** `PUT /api/products/{id}`
- **ENTONCES** actualiza campos editables conservando el SKU

**Intento de cambiar SKU**
- **CUANDO** el update incluye SKU nuevo
- **ENTONCES** rechazada con `400`

---

## `GET /api/products?query=&limit=20` — FR-011

Búsqueda por nombre para autocompletado. Implementación: `ILIKE` sobre índice trigram (`pg_trgm`).

**Query**: `SearchProductsQuery`

### Request

```http
GET /api/products?query=aceta&limit=20
X-Tenant-ID: 6f2c1b3a-8e41-4f2d-9c7a-1d5e8b0a3f77
Authorization: Bearer eyJhbGciOiJIUzI1NiIs...
```

| Parámetro | Tipo | Default | Notas |
|---|---|---|---|
| `query` | `string` | — | Fragmento del nombre |
| `limit` | `int` | `20` | `[PENDIENTE: las fuentes no definen un límite máximo ni paginación (offset/cursor)]` |

### Respuesta `200 OK`

```json
[
  {
    "id": "3b8d1f60-2c14-4a9e-8f77-0a5b3e2d9c41",
    "sku": "SKU-1042",
    "barcode": null,
    "name": "Acetaminofén 500mg",
    "category": "Medication",
    "activeIngredient": "Acetaminofén",
    "presentation": "Caja x 20 tabletas",
    "storageConditions": "Lugar seco, < 30 °C",
    "currency": "COP"
  }
]
```

Los resultados DEBE venir ordenados por relevancia y acotados al tenant. La respuesta DEBERÍA entregarse en `<500ms` (NFR-006).

> `[PENDIENTE: "ordenadas por relevancia" no está definido operativamente en las fuentes — ¿similaridad trigram, prefijo, alfabético? Elegir antes de apply]`

Sin coincidencias: `200` con array vacío (no `404`).

### Errores

| Status | `errorCode` | Cuándo |
|---|---|---|
| `400` | `VALIDATION_FAILED` | `query` vacío o `limit` fuera de rango |
| `400` | `TENANT_HEADER_MISSING` / `TENANT_HEADER_INVALID` | Header inválido |
| `401` | — | JWT ausente o expirado |

### Escenario (Dado/Cuando/Entonces)

**Autocompletado por nombre**
- **DADO** producto "Acetaminofén 500mg"
- **CUANDO** `GET /api/products?query=aceta`
- **ENTONCES** retorna coincidencias ordenadas por relevancia

---

## `GET /api/products/by-barcode/{code}` — FR-011

Lookup **exacto** por código de barras, acotado al tenant.

**Query**: `GetProductByBarcodeQuery`

### Request

```http
GET /api/products/by-barcode/7702001234567
X-Tenant-ID: 6f2c1b3a-8e41-4f2d-9c7a-1d5e8b0a3f77
Authorization: Bearer eyJhbGciOiJIUzI1NiIs...
```

### Respuesta `200 OK`

Un único `ProductDto`.

### Errores

| Status | `errorCode` | Cuándo |
|---|---|---|
| `400` | `TENANT_HEADER_MISSING` / `TENANT_HEADER_INVALID` | Header inválido |
| `401` | — | JWT ausente o expirado |
| `404` | `PRODUCT_NOT_FOUND` | No hay match **exacto** de barcode en el tenant (FR-011) |

```json
{
  "type": "https://stockma.co/errors/product-not-found",
  "title": "Producto no encontrado",
  "status": 404,
  "errorCode": "PRODUCT_NOT_FOUND",
  "detail": "No existe un producto con el barcode '7702001234567' en este tenant."
}
```

### Escenario (Dado/Cuando/Entonces)

**Lookup por barcode**
- **CUANDO** `GET /api/products/by-barcode/{code}`
- **ENTONCES** retorna el producto o `404`

---

## Cobertura de requerimientos

| Requerimiento | Endpoint |
|---|---|
| FR-009 | `POST /api/products` |
| FR-010 | `PUT /api/products/{id}` |
| FR-011 | `GET /api/products?query=` + `GET /api/products/by-barcode/{code}` |
| NFR-006 | `GET /api/products?query=` (`<500ms`, índice trigram) |
| NFR-007 | Transversal: filtro EF + RLS en todos los endpoints |
| NFR-008 | Campo `currency` en `POST` / `PUT` |

## Fuera de alcance

- `GET /api/products/{id}` individual — **no está en las fuentes**. `[PENDIENTE: el frontend (ProductForm) probablemente lo necesita; confirmar si se agrega en PR4]`
- `DELETE /api/products/{id}` — no definido en las fuentes.
