# Diseño: Landing de Presentación del Producto

## Enfoque técnico
Implementar una landing pública en `/` como un nuevo módulo de feature de React, integrado mediante un simple switch por `pathname` en `App.tsx` (sin agregar router todavía, según T058 diferido). Reutilizar los tokens de estilo existentes y el bridge de MUI para consistencia de tema. La landing se construirá con CSS Modules/Emotion siguiendo los patrones existentes, con un toggle de tema en cliente y un script anti-FOUC en `index.html`.

## Decisiones de arquitectura

### Decisión: Cambio de ruta sin React Router
**Elección**: Simple chequeo de `window.location.pathname` en `App.tsx` para renderizar `LandingPage` en `/` (y el resto) y `AuthFlow` en `/login`.
**Alternativas consideradas**: Agregar `react-router-dom` ahora.
**Justificación**: Mantiene los cambios mínimos, evita dependencias nuevas; el router queda diferido a T058. Coincide con el comportamiento del prototipo.

### Decisión: Landing como módulo de feature
**Elección**: Crear `frontend/web/src/features/landing/` con componentes, estilos, hooks y referencias a assets.
**Alternativas consideradas**: Ponerlo en `pages/` o inline en App.
**Justificación**: Sigue la organización por features usada por `features/auth/`.

### Decisión: Extensión del sistema de temas
**Elección**: Extender los tokens con modo oscuro `[data-theme='dark']`, persistir en la clave de `localStorage` `stockma-theme`, agregar script anti-FOUC en `index.html`, y asegurar que `createMuiBridge()` se recree al cambiar el tema (o que escuche el evento `stockma-theme-change`).
**Alternativas consideradas**: Solo variables CSS sin sincronización con MUI.
**Justificación**: El prototipo muestra que el modo oscuro debe afectar los componentes de MUI; el bridge existente lee los tokens al momento de crearse.

### Decisión: Los botones primarios usan los tokens de la app al implementar
**Elección**: El prototipo mantiene su teal hardcodeado (`#337082`, hover `#2b5f6d`) por ahora. Al implementar en React, los botones primarios deben estilizarse desde los tokens: `background: var(--primary)`, `color: var(--on-primary)`, hover `var(--primary-active)`.
**Alternativas consideradas**: Swap directo a `var(--accent)` — mismo tono, parece la solución limpia.
**Justificación**: `--accent` pasa a ser `#7fb2c0` en modo oscuro y el texto blanco sobre él mide **2.33:1** (WCAG AA requiere 4.5:1). `--primary` mide 13.48:1 en claro (`#182f4f`) y 4.72:1 en oscuro (`#3d78a8`), mientras que el teal actual mide 5.55:1. El teal se mantiene como acento de marca; el azul marino es el color de acción.

### Decisión: CTA de lista de espera como barra inferior sticky
**Elección**: `#waitlist` es una barra full-bleed con `position: sticky; bottom: 0` usando el mismo tratamiento que el header (fondo translúcido con `color-mix`, `backdrop-filter`, borde en el borde interior). El copy queda dentro de `.wrap` para alinearse con el resto de las secciones, y a ≤900px se apila con un botón de ancho completo. La sección va como **último hijo de `<main>`**: en sticky, la posición de flujo del elemento es su punto de desenganche, así que con el flujo al pie de la página la barra queda visible desde la carga y se libera recién cuando el footer entra en pantalla (sin `fixed` ni `IntersectionObserver`).
**Alternativas consideradas**: Barra con `position: fixed`, o mantener la card original a media página.
**Justificación**: `sticky` da un CTA siempre accesible sin sacar el elemento del flujo, así que no se necesita compensación con `body { padding-bottom }` y la barra se libera naturalmente al hacer scroll más allá de ella. Una barra `fixed` taparía contenido durante toda la sesión.

> **Gotcha para la implementación**: `.wrap` declara `padding: 0 24px`. Cualquier elemento con `class="wrap algo"` debe setear su padding vertical con `padding-block`, nunca con el shorthand `padding` — el shorthand borra silenciosamente el padding horizontal y el bloque queda pegado a los bordes del viewport (este bug exacto desalineaba el footer).

### Decisión: Estilos fuera de los componentes con CSS Modules
**Elección**: Cada sección declara sus reglas en `X.module.css` y consume clases (`className={styles.x}`); la capa de layout repetida (padding de sección, wrap de 1160px, grid de cards) vive en `landing.module.css`, y la tipografía que extiende `PageTitle`/`SectionTitle`/`MutedText` vive en `*.styles.ts` con `styled()` de emotion.
**Alternativas consideradas**: Selectores por `id` y atributos `data-layout` (lo que hacía la primera versión), o mover todas las reglas a emotion.
**Justificación**: `src/styles/no-inline-styles.test.ts` ya exigía que los componentes no declaren estilos, pero prohibía `className`, lo que empujaba a esconder selectores en atributos y dejaba el CSS atado a `#id` con especificidad alta. La regla se ajustó: se permite `className` con un token de módulo y se siguen prohibiendo los literales de clase global, `style`, `sx`, `css` y `styled` dentro del componente. Los overrides sobre componentes de emotion se resuelven con `styled()` o con selector que incluya el tag (`button.cta`), porque una clase sola empata en especificidad y depende del orden de inyección.

## Flujo de datos
```
Usuario -> LandingPage (secciones)
  -> ThemeToggle -> localStorage + data-theme + stockma-theme-change
  -> Nav -> navega a anclas de sección (#features, #how-it-works, #pricing, #demo)
  -> CTAs de lista de espera (hero, planes, demo, barra) -> visibles y habilitados, sin acción en esta spec
```

## Fuera del alcance de esta spec
- La modal de lista de espera y el guardado de datos en backend se implementarán juntos en una futura spec.
- Los CTA actuales permanecen habilitados pero no abren una modal, no navegan y no envían datos.
- `Stockma_landing.html` conserva la modal como referencia visual; no forma parte de este cambio de alcance.

## Cambios de archivos

| Archivo | Acción | Descripción |
|---------|--------|-------------|
| `frontend/web/src/App.tsx` | Modificar | Agregar switch por pathname para renderizar LandingPage o AuthFlow |
| `frontend/web/src/main.tsx` | Modificar | Escuchar cambios de tema para recrear el tema MUI si es necesario |
| `frontend/web/index.html` | Modificar | Agregar script anti-FOUC de tema y links de la fuente Work Sans |
| `frontend/web/src/features/landing/` | Crear | Nuevo módulo de feature (componentes, estilos, hooks, index) |
| `frontend/web/src/features/landing/components/LandingPage/` | Crear | Composición principal de la página |
| `frontend/web/src/features/landing/components/Header/` | Crear | Nav, logo, toggle de tema, menú móvil |
| `frontend/web/src/features/landing/components/Hero/` | Crear | Sección hero: copy, 2 CTAs y mock visual de tabla de inventario con pestañas |
| `frontend/web/src/features/landing/components/HowItWorks/` | Crear | 3 pasos |
| `frontend/web/src/features/landing/components/Features/` | Crear | Grid de 6 cards (una con tag "Próximamente") |
| `frontend/web/src/features/landing/components/Comparison/` | Crear | Manual vs Stockma |
| `frontend/web/src/features/landing/components/Pricing/` | Crear | 4 planes, altura uniforme, CTAs visibles sin acción en esta spec |
| `frontend/web/src/features/landing/components/DemoSection/` | Crear | Sección demo con badge "Próximamente", botón deshabilitado y CTA de lista de espera sin acción |
| `frontend/web/src/features/landing/components/WaitlistSection/` | Crear | Barra CTA inferior sticky (`position: sticky; bottom: 0`), full-bleed, apilada ≤900px |
| `frontend/web/src/features/landing/components/WhoItsFor/` | Crear | Segmentos de "Para quién es Stockma" |
| `frontend/web/src/features/landing/components/Footer/` | Crear | 3 filas: logo + iconos sociales (`space-between`), línea de soporte, copyright |
| `frontend/web/src/features/landing/components/Faq/` | Crear | FAQ en acordeón |
| `frontend/web/src/styles/tokens.css` | Modificar | Agregar tokens de modo oscuro, tokens de radius (10px) |
| `frontend/web/src/assets/` | Modificar | Los logos son PNG con transparencia (`Stockma_*.png`) y **márgenes transparentes grandes** — dimensionarlos por el artwork visible, no por el canvas (`Stockma_vertical.png` es 860x290 pero la marca ocupa ~49% del ancho). `Stockma_vertical_trim.png` es una copia recortada al ras agregada para el footer. El recoloreo con `currentColor` no aplica — elegir el PNG que sirva para cada superficie |

## Interfaces / Contratos
```typescript
// features/landing/types.ts
export interface Plan {
  name: 'Básico' | 'Profesional' | 'Negocio' | 'A tu medida';
  price: string;
  bullets: string[];
  featured?: boolean;
  custom?: boolean;
}
```

## Estrategia de testing
| Capa | Qué probar | Enfoque |
|------|-------------|---------|
| Unit | Lógica del toggle de tema, conmutación de pestañas del mock del hero | Vitest + RTL |
| Componente | Las secciones de la landing renderizan correctamente | Snapshots/assertions básicas con RTL |
| Integración | Switch de rutas, cambio de tema y CTA de lista de espera sin acción | User events con RTL |
| E2E | Check manual por paridad con el prototipo | Comparación visual con Stockma_landing.html |

## Migración / Rollout
No se requiere migración. La feature es aditiva detrás del switch de ruta. Se puede desplegar de forma independiente.

## Preguntas abiertas
- Ninguna
