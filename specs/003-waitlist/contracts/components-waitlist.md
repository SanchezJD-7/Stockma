# Contrato: Componentes React — Waitlist

**Spec**: [`../spec.md`](../spec.md)
**Capabilities**: `waitlist-capture`
**Regla de estilos**: sin estilos inline (gate `src/styles/no-inline-styles.test.ts`); todo en `.module.css` + `.styles.ts`.

---

## Árbol de componentes

```
LandingPage
└── WaitlistModalProvider            (contexto: abierto/cerrado, origen del CTA)
    ├── Hero (CTA)                    ──┐
    ├── DemoSection (CTA)             ──┼── onClick → useOpenWaitlist().open()
    ├── WaitlistSection (CTA)         ──┘
    ├── WaitlistModal                 (registro; visible sólo si open)
    └── WaitlistSuccessModal          (confirmación; fase 'success')
```

Los CTAs de **Pricing** no se conectan (siguen sin acción).

## `WaitlistModalProvider`

Expone el estado de apertura de las dos modales y las acciones. Sin dependencias de red.

| Miembro | Tipo | Semántica |
|---|---|---|
| `open()` | acción | Abre `WaitlistModal` en fase `form` |
| `close()` | acción | Cierra ambas modales y resetea el formulario |
| fase | `'closed' \| 'form' \| 'success'` | Única fuente de verdad; `success` sólo se alcanza desde `form` con `201` |

Sólo **una** de las dos modales se renderiza a la vez. Escape y clic fuera del backdrop invocan `close()` sólo en fase `form` (en `success`, sólo el botón de cierre).

## `WaitlistModal` — modal de registro

**Props**

| Prop | Tipo | Notas |
|---|---|---|
| `open` | `boolean` | `false` → no se renderiza |
| `onClose` | `() => void` | Escape / clic fuera / botón `×` |
| `onSuccess` | `() => void` | Se invoca tras `201`; el provider pasa a fase `success` |

**Estados internos**

| Estado | Tipo | Reglas |
|---|---|---|
| `fullName` | `string` | 2–120 |
| `phone` | `string` | 7–15 dígitos; el prefijo `+57` es decorativo (campo oculto `countryCode="+57"`) |
| `email` | `string` | `type="email"`, `autocomplete="email"` |
| `status` | `'idle' \| 'submitting' \| 'error'` | — |
| `fieldErrors` | `Record<string, string>` | Mapa del backend (`400.errors`) y validación client-side |
| `formError` | `string \| null` | Mensaje **puntual** del fallo (409 / 500 / red) |

**Comportamiento**

- Submit válido → `status = 'submitting'`: botón con **spinner circular**, `disabled` y `aria-busy="true"`; inputs deshabilitados. Doble submit imposible.
- `201` → `onSuccess()` (la modal la cierra el provider, no se auto-cierra con animación propia).
- `400` → `fieldErrors` por campo + resumen accessible (`role="alert"`).
- `409` → mensaje en `formError`: el correo ya está en la lista; el modal **permanece abierto** y conserva los datos.
- `500` / error de red → `formError` genérico; el modal **permanece abierto** y conserva los datos.
- El mensaje DEBE ser visible en pantalla junto al formulario (no sólo en consola ni sólo en toast efímero).

**Accesibilidad**: `role="dialog"`, `aria-modal="true"`, `aria-labelledby` al título.

## `WaitlistSuccessModal` — modal de confirmación

**Props**: `open: boolean`, `onClose: () => void`.

- Sin campos, sin llamadas de red.
- Copy de éxito versionado en el componente (prototipo 2963-2968, referencia no normativa).
- Cierre por botón, Escape o clic fuera.

## `waitlist-service`

```ts
// frontend/web/src/services/waitlist-service.ts
import axios from 'axios'
import { config } from '../config'
import { authConfig } from '../shared/http'

class WaitlistService {
  register(input: {
    fullName: string; phone: string; email: string;
  }): Promise<{ id: string; email: string }>
}

export default WaitlistService
```

- Llama a `axios.post(\`${config.API_URL}/api/waitlist\`, body, authConfig())` directamente (el `+57` lo agrega el servicio; un solo lugar, definido en apply).
- `authConfig()` vive en `src/shared/http.ts` y se evalúa **en el momento de la petición**: aporta `Authorization: Bearer <token>` con la sesión vigente y devuelve `{}` si no la hay. No puede ser un objeto const a nivel de módulo: quedaría congelado con el valor de antes del login.
- La traducción a `ApiError(status, errorCode)` ocurre en el interceptor de respuesta de `src/shared/http.ts`, no en el servicio.
- Propaga `ApiError(status, errorCode)` tal cual — **la traducción a mensaje amigable vive en el componente/constantes de error, no en el servicio**.

### Mapeo `errorCode` → mensaje

| `errorCode` | Mensaje (español neutro, sin voseo) |
|---|---|
| `VALIDATION_FAILED` | Revisá los campos marcados… → redactar en apply con copy neutro sin voseo |
| `WAITLIST_EMAIL_DUPLICATE` | Ese correo ya está en la lista de espera |
| `INTERNAL_ERROR` | No pudimos completar el registro; intentá de nuevo |
| (sin `errorCode`, red) | No hay conexión con el servidor; verificá tu internet |

> El copy final se versiona en el componente con forma imperativa neutra; gate `scripts/check-neutral-es.mjs` prohíbe voseo.

## Mapeo de éxito → modales

| Paso | UI |
|---|---|
| Clic en CTA | Abre `WaitlistModal` (fase `form`) |
| Submit | Spinner en el botón |
| `201` | Cierra `WaitlistModal`, abre `WaitlistSuccessModal` (fase `success`) |
| `4xx/5xx` | `WaitlistModal` abierta con mensaje puntual |

## Tests (contrato de verificabilidad)

| Test | Verifica |
|---|---|
| `waitlist-service.test.ts` | `postJson` con payload correcto; `ApiError.errorCode` propagado (`mockApi` de `http.testkit.ts`) |
| `WaitlistModal.test.tsx` | Apertura con 3 campos; spinner + `disabled` durante envío; `400` marca campos; `409`/`500` muestran mensaje y no cierran; Escape/cierra; `201` dispara `onSuccess` |
| `WaitlistSuccessModal.test.tsx` | Renderiza y cierra |
| `WaitlistModalProvider.test.tsx` | Transición `form → success`; reset al cerrar |
| `LandingPage.test.tsx` (modificado) | Los 3 CTAs abren la modal; Pricing sigue sin acción (reemplaza `queryByRole('dialog') === null` de 002) |
