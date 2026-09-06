# Contrato: Tenant API

Base: `/api/tenant`. Todas las rutas requieren el header `X-Tenant-ID`; sin él
el `TenantMiddleware` responde `400` antes de llegar al controlador.

## `GET /api/tenant/branding`

Colores de marca del tenant activo. El frontend lo pide al arrancar y le pasa el
resultado a `applyTenantBranding` antes de construir el theme de MUI.

**`200 OK` — tenant con branding configurado**

```json
{
  "primary": "#8e24aa",
  "primaryActive": "#6a1b9a",
  "primaryBg": "#f3e5f5"
}
```

**`200 OK` — tenant sin branding configurado**

```json
null
```

No es `404`. La ausencia de branding es un estado válido, no un recurso que
falta: el frontend le pasa ese `null` directo a `applyTenantBranding`, que ya
significa "usá los valores por defecto de `tokens.css`".

**`400 Bad Request`** — falta el header `X-Tenant-ID`.

### Reglas

- Sólo tres colores son configurables. El color de contraste NO viaja: lo deriva
  el frontend de la luminancia del primary, para que ningún tenant pueda dejarse
  un botón ilegible.
- Los colores del semáforo de vencimiento no son configurables: son semántica
  del producto, no identidad visual.
- Cada color se valida en el dominio contra `#rgb` o `#rrggbb` y se normaliza a
  minúsculas.

## `PUT /api/admin/tenant/branding`

`[PENDIENTE — T053]` Escritura restringida al admin del tenant. Bloqueado hasta
PR 3: hoy la API no tiene autenticación configurada (`Program.cs` llama a
`UseAuthorization()` pero no hay ningún esquema de autenticación registrado), así
que un endpoint "sólo admin" sería en realidad un endpoint abierto.
