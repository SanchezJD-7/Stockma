# Diseño: Landing de Presentación del Producto

> **Regla de gobernación aplicada**: este documento gobierna _decisiones de arquitectura, contratos de datos, estrategia de testing y cambios de archivos_. No contiene copy literal de UI; el copy versionado vive en los componentes de la landing. El HTML del prototipo es local, ignorado y no normativo.

## Enfoque técnico

Implementar una landing pública en `/` como un nuevo módulo de feature de React, integrado mediante un simple switch por `pathname` en `App.tsx` (sin agregar router todavía, según T058 diferido). Reutilizar los tokens de estilo existentes y el bridge de MUI para consistencia de tema. La landing se construirá con CSS Modules/Emotion siguiendo los patrones existentes, con un toggle de tema en cliente y un script anti-FOUC en `index.html`.

## Decisiones de arquitectura

### Decisión: Cambio de ruta sin React Router

**Elección**: Simple chequeo de `window.location.pathname` en `App.tsx` para renderizar `LandingPage` en `/` (y el resto) y `AuthFlow` en `/login`.
**Alternativas consideradas**: Agregar `react-router-dom` ahora.
**Justificación**: Mantiene los cambios mínimos, evita dependencias nuevas; el router queda diferido a T058. Coincide con el comportamiento del prototipo.

### Decisión: Navegación interna alineada con el header

**Elección**: `LandingPage` intercepta los clics primarios en enlaces internos y llama a `scrollIntoView({ block: 'start' })`. El root del documento reserva 73px para el header sticky; los clics con modificadores conservan la navegación nativa y la URL no recibe hash. El menú móvil permanece como overlay absoluto y se cierra al elegir un destino.
**Alternativas consideradas**: dejar el scroll nativo sin offset, mover el menú móvil al flujo del header o usar anclas con hash.
**Justificación**: el header cubre el inicio del contenido si el scroll alinea al borde del viewport. Además, `#demo`, último destino antes del footer, podía quedar desplazado porque el navegador limita el scroll al final del documento. `LandingPage` calcula y agrega temporalmente a esa sección solo el espacio de scroll que falta; lo recalcula en el siguiente clic interno.

### Decisión: Aparición sutil de las secciones

**Elección**: un `IntersectionObserver` revela cada sección una vez con transición de opacidad y `translateY(12px)` de 450ms. Se anima el contenedor interno, no el `<section>`, para conservar la geometría usada por las anclas. Si el usuario reduce el movimiento, las secciones aparecen directamente, la rotación del hero se detiene, el menú y la barra no se deslizan y el scroll es instantáneo. Sin `IntersectionObserver`, el contenido permanece visible.
**Alternativas consideradas**: animar toda la sección (incluido su borde de ancla), animar todas las tarjetas con retrasos escalonados o reproducir la animación continuamente al entrar/salir del viewport.
**Justificación**: mantiene el movimiento breve y discreto, no altera la posición de navegación y evita repetir la animación al recorrer la página.

### Decisión: Landing como módulo de feature

**Elección**: Crear `frontend/web/src/features/landing/` con componentes, estilos, hooks y referencias a assets.
**Alternativas consideradas**: Ponerlo en `pages/` o inline en App.
**Justificación**: Sigue la organización por features usada por `features/auth/`.

### Decisión: Extensión del sistema de temas

**Elección**: Extender los tokens con modo oscuro `[data-theme='dark']`, persistir en la clave de `localStorage` `stockma-theme`, agregar script anti-FOUC en `index.html`, y asegurar que `createMuiBridge()` se recree al cambiar el tema (o que escuche el evento `stockma-theme-change`).
**Alternativas consideradas**: Solo variables CSS sin sincronización con MUI.
**Justificación**: El prototipo muestra que el modo oscuro debe afectar los componentes de MUI; el bridge existente lee los tokens al momento de crearse.

> **Gotcha de tokens**: `landing.module.css` redeclara la paleta del prototipo en `.page` (fondo blanco, borde y textos claros). Una declaración propia gana sobre el valor heredado de `:root`, así que el modo oscuro DEBE repetir esos tokens en `html[data-theme='dark'] .page`; de lo contrario la página queda blanca con tarjetas oscuras y texto oscuro. El bridge de MUI además fija `palette.mode` según `data-theme`.

### Decisión: Los botones primarios usan el teal de marca (`--brand-teal`)

**Elección**: Los CTA primarios de la landing (hero, planes, barra de lista de espera y demo) usan `background: var(--brand-teal)` (`#337082`), `color: #ffffff` y hover `var(--brand-teal-active)` (`#2b5f6d`), igual que el prototipo. En la landing no se usa `--color-primary` (navy).
**Alternativas consideradas**: Estilizarlos con `var(--color-primary)` (`#182f4f` en claro, `#3d78a8` en oscuro) o con `var(--accent)`.
**Justificación**: Blanco sobre `#337082` mide **5.55:1** tanto en claro como en oscuro, así que pasa WCAG AA y el botón se ve idéntico en los dos temas, como en el prototipo. `var(--accent)` en oscuro es `#7fb2c0` y da **2.33:1** (falla AA), por eso queda descartado. El navy daba 13.48:1 en claro pero 2.33:1 en oscuro con el `--primary` oscuro (`#3d78a8`), y además se apartaba del prototipo sin ganar contraste real. El navy sigue siendo el color de acción del resto de la app; la landing conserva el teal de marca.

### Decisión: CTA de lista de espera como barra inferior fija con ocultamiento por observador

**Elección**: `#waitlist` es una barra full-bleed con `position: fixed; bottom: 0` y `z-index` sobre el contenido, usando el mismo tratamiento que el header (fondo translúcido con `color-mix`, `backdrop-filter`, borde en el borde interior). El copy queda dentro de `.wrap`. Se renderiza como `<aside aria-label>` con `id="waitlist"` como **último hijo de `<main>`**. Un `IntersectionObserver` sobre `#demo` (`threshold: 0.1`) oculta la barra mientras esa sección está en pantalla —demo ya tiene su propio CTA, así que no se duplica la llamada a la acción— y la vuelve a montar, con animación de entrada `slideUp`, al salir de vista.
**Alternativas consideradas**: (1) la barra `sticky; bottom: 0` del diseño anterior, que se liberaba con el footer; (2) barra `fixed` siempre visible.
**Justificación**: el comportamiento nuevo (ocultarse sobre `#demo`) exige desacoplar la barra del flujo: `sticky` sólo se liberaba con el footer, no con la sección demo, y al ocultarse dejaría un hueco. El costo conocido de `fixed` —tapar contenido— se acepta porque la barra se oculta justamente cuando hay otro CTA en pantalla.

> **Gotcha para la implementación**: `.wrap` declara `padding: 0 24px`. Cualquier elemento con `class="wrap algo"` debe setear su padding vertical con `padding-block`, nunca con el shorthand `padding` — el shorthand borra silenciosamente el padding horizontal y el bloque queda pegado a los bordes del viewport (este bug exacto desalineaba el footer).

### Decisión: Texto rotativo en el H1 del hero

**Elección**: El segundo segmento del H1 alterna 3 frases con `useState` + `setInterval` (ciclo de 3s) y una transición de fundido de 400ms (clases `in`/`out` con `opacity` y `translateY`) declaradas en `Hero.module.css`.
**Alternativas consideradas**: mantener el `<em>` estático del diseño anterior; efecto máquina de escribir.
**Justificación**: los 3 mensajes atacan al mismo enemigo (los registros manuales), lo que refuerza el mensaje central del spec sin congelar un único string. El `interval` y cualquier timeout pendiente se limpian en el `cleanup` del efecto; la rotación y la transición se omiten con `prefers-reduced-motion`.

### Decisión: Iconos de MUI en Features y Footer

**Elección**: Las 6 tarjetas de `#features` y los iconos sociales del footer usan componentes de `@mui/icons-material` (`InventoryRounded`, `EventRounded`, `QrCodeScannerRounded`, `SyncAltRounded`, `CreditCardRounded`, `FileDownloadRounded`, `WhatsApp`, `Instagram`), que heredan el color por `currentColor`.
**Alternativas consideradas**: SVG inline a mano (como en el diseño anterior del footer) o secciones sin iconos.
**Justificación**: un solo set de iconos coherente con la app, sin duplicar paths SVG; los wrappers con fondo tint se estilan con CSS Module.

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

| Archivo                                                         | Acción    | Descripción                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                    |
| --------------------------------------------------------------- | --------- | ---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `frontend/web/src/App.tsx`                                      | Modificar | Agregar switch por pathname para renderizar LandingPage o AuthFlow                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                             |
| `frontend/web/src/main.tsx`                                     | Modificar | Escuchar cambios de tema para recrear el tema MUI si es necesario                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                              |
| `frontend/web/index.html`                                       | Modificar | Agregar script anti-FOUC de tema y links de la fuente Inter                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                    |
| `frontend/web/src/features/landing/`                            | Crear     | Nuevo módulo de feature (componentes, estilos, hooks, index)                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                   |
| `frontend/web/src/features/landing/components/LandingPage/`     | Crear     | Composición principal de la página                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                             |
| `frontend/web/src/features/landing/components/Header/`          | Crear     | Nav, logo, toggle de tema, menú móvil                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                          |
| `frontend/web/src/features/landing/components/Hero/`            | Crear     | Sección hero: H1 con segmento rotativo, subtítulo, 2 CTAs y mock visual de tabla de inventario con pestañas                                                                                                                                                                                                                                                                                                                                                                                                                                                                                    |
| `frontend/web/src/features/landing/components/HowItWorks/`      | Crear     | 3 pasos                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                        |
| `frontend/web/src/features/landing/components/Features/`        | Crear     | Grid de 6 cards con iconos MUI (una con tag de estado diferido)                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                |
| `frontend/web/src/features/landing/components/Comparison/`      | Crear     | Comparativa sin/con el producto (`#comparison`)                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                |
| `frontend/web/src/features/landing/components/Pricing/`         | Crear     | 4 planes, altura uniforme, CTAs visibles sin acción en esta spec                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                               |
| `frontend/web/src/features/landing/components/DemoSection/`     | Crear     | Sección demo con CTA único de lista de espera sin acción (sin badge ni botón deshabilitado; `DemoSection.styles.ts` quedó sin uso en el rediseño)                                                                                                                                                                                                                                                                                                                                                                                                                                              |
| `frontend/web/src/features/landing/components/WaitlistSection/` | Crear     | Barra CTA inferior fija (`position: fixed; bottom: 0`), full-bleed, oculta por IntersectionObserver sobre `#demo`, apilada ≤900px                                                                                                                                                                                                                                                                                                                                                                                                                                                              |
| `frontend/web/src/features/landing/components/WhoItsFor/`       | Crear     | Segmentos de audiencia (`#audience`), 4 tarjetas                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                               |
| `frontend/web/src/features/landing/components/Footer/`          | Crear     | 3 filas: logo + iconos sociales MUI (`space-between`), línea de soporte, copyright                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                             |
| `frontend/web/src/features/landing/components/Faq/`             | Crear     | FAQ en acordeón                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                |
| `frontend/web/src/styles/tokens.css`                            | Modificar | Agregar tokens de modo oscuro, tokens de radius (10px); en el rediseño: neutros slate, `--color-tint-accent` al 40% y tokens de login en oscuro                                                                                                                                                                                                                                                                                                                                                                                                                                                |
| `frontend/web/src/styles/components/texts.tsx`                  | Modificar | `MutedText` sin `margin: 0` (hereda el margen por defecto del `<p>`) — **cambio global**, afecta a toda la app incluido `features/auth`                                                                                                                                                                                                                                                                                                                                                                                                                                                        |
| `frontend/web/src/features/landing/landing-texts.ts`            | Modificar | Escala de títulos (`clamp(32px, 4.5vw, 48px)` a 700) y leads del rediseño                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                      |
| `frontend/web/src/assets/`                                      | Modificar | Los logos son PNG con transparencia (`Stockma_*.png`) y **márgenes transparentes grandes** — dimensionarlos por el artwork visible, no por el canvas (`Stockma_vertical.png` es 860x290 pero la marca ocupa ~49% del ancho). `Stockma_vertical_trim.png` es una copia recortada al ras agregada para el footer. El recoloreo con `currentColor` no aplica — elegir el PNG que sirva para cada superficie. Para oscuro se agregan `Stockma_horizontal_dark.png` y `Stockma_vertical_trim_dark.png` (navy→blanco, teal→`#7fb2c0`) y el componente alterna ambos con `display` según `data-theme` |

## Interfaces / Contratos

```typescript
// features/landing/types.ts
// Los valores visibles (nombres de plan, precios, bullets) NO se fijan en este
// contrato: son copy y viven en los componentes de la landing.
export interface Plan {
  /** Etiqueta visible definida por los componentes de la landing. */
  name: string;
  price: string;
  bullets: string[];
  featured?: boolean; // CTA primario
  custom?: boolean; // cuarta tarjeta: borde punteado, sin precio numérico
  popular?: boolean; // badge de popularidad en la tarjeta destacada
}
```

> El orden de los 4 planes, cuál es `featured` y cuál es `custom`, así como el
> contenido de `name`, `price` y `bullets`, se definen en los componentes de la
> landing. Este contrato sólo fija la **forma** del dato.

## Estrategia de testing

| Capa          | Qué probar                                                                                | Enfoque                                                        |
| ------------- | ----------------------------------------------------------------------------------------- | -------------------------------------------------------------- |
| Unit          | Lógica del toggle de tema, conmutación de pestañas del mock del hero                      | Vitest + RTL                                                   |
| Componente    | Las secciones de la landing renderizan correctamente                                      | Snapshots/assertions básicas con RTL                           |
| Integración   | Switch de rutas, cambio de tema y CTA de lista de espera sin acción                       | User events con RTL                                            |
| Accesibilidad | Hero y reveal respetan `prefers-reduced-motion`; anclas no se desplazan durante el reveal | Vitest + RTL y validación responsive en navegador              |
| E2E           | Comprobar los flujos y el contenido de la landing implementada                            | Navegador; los componentes versionados son la fuente de verdad |
| Responsive    | Los cuatro destinos del navbar quedan bajo el header en desktop y menú móvil              | Navegador en 1440/900/600/375px                                |

> **Gotchas de testing (rediseño 2026-11-08)**:
>
> - `IntersectionObserver` **no existe en jsdom**: los tests que verifican el reveal deben inyectar un mock. `WaitlistSection` y `LandingPage` dejan el contenido visible si la API no está disponible.
> - `mui-bridge.test.tsx` y `AuthLayout.test.tsx` comparan **valores literales de tokens**: al cambiar `tokens.css` hay que actualizar esas aserciones (fueron los 2 tests que rompió el rediseño de la paleta).

## Migración / Rollout

No se requiere migración. La feature es aditiva detrás del switch de ruta. Se puede desplegar de forma independiente.

## Decisiones de producto y cierre

- Se conserva el copy comercial actual como descripción de capacidades objetivo del roadmap, por decisión de producto. No debe interpretarse como evidencia de que las funcionalidades ya estén disponibles en el backend; su implementación se planificará en specs futuras.
- El formulario de lista de espera y el envío de correo quedan para la spec 003; en esta spec los CTA permanecen visibles y sin acción.
- `Stockma_landing.html` se mantiene local e ignorado por Git. El copy versionado de los componentes es la fuente de verdad; no se exige paridad de commit con el prototipo.
- Los CTA de planes no destacados usan el estilo secundario, tal como define §Planes.
