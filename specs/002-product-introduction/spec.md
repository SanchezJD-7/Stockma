# Spec 002 - Product Introduction Landing

> **Regla de gobernación aplicada**: este documento gobierna _estructura, comportamiento, estado, accesibilidad y restricciones responsivas_. **No contiene copy literal de UI.** El copy versionado vive en los componentes de `frontend/web/src/features/landing/`; el prototipo HTML local es una referencia visual no normativa.

## Objetivo

Crear una pantalla de introducción (landing) en `/` para Stockma, con navegación superior, modo oscuro, responsive y coherente con la identidad visual de la app.

## Contexto

- **Fuente de verdad versionada del copy**: los componentes de `frontend/web/src/features/landing/`. `Stockma_landing.html` se mantiene local e ignorado por Git; sirve como referencia visual, no como requisito de paridad ni fuente normativa.
- Tipografía única: **Inter**.
- Sin voseo (gate `scripts/check-neutral-es.mjs`).
- El copy comercial actual se conserva por decisión de producto como presentación de capacidades objetivo del roadmap; no implica que esas funcionalidades ya estén implementadas en el backend. La implementación de las funcionalidades prometidas queda fuera de esta spec y se abordará en futuras specs.
- Inspiración visual: pagaldia.com, pero con mayor densidad (se pidió corregir el aspecto "vacío").
- Identidad visual actualizada: logos en formato PNG con transparencia (`Stockma_logo.png`, `Stockma_vertical.png`, `Stockma_horizontal.png`, `Stockma_name.png`) en `frontend/web/src/assets/`. Eliminados los SVG de marca antiguos. Favicon actualizado a PNG.

## Alcance

- Landing pública en `/`.
- Navegación superior: logo (enlace a `/`), 4 links de sección, control de tema y menú móvil. Sin CTA de login.
  - Targets de los links, en orden: `#features`, `#how-it-works`, `#pricing`, `#demo`. Las etiquetas visibles se definen en los componentes.
  - Los clics primarios en enlaces internos desplazan la sección al inicio, por debajo del header fijo, sin modificar el hash. En móvil, el menú se superpone al contenido y se cierra sin alterar la posición del destino.
- Aparición progresiva, sutil y de una sola vez para el contenido de cada sección al entrar en el viewport; no debe mover el límite de scroll usado por las anclas.
- Hero: H1 con segundo segmento **rotativo** (3 frases, animación de fundido — ver §Hero), CTA primario (visible, habilitado y **sin acción** en esta spec), CTA secundario hacia `#how-it-works`, y mock visual interactivo de tabla de inventario con pestañas.
- Sección de segmentos de audiencia (`#audience`): **4 tarjetas**, una por segmento objetivo.
- Sección `#how-it-works`: 3 pasos.
- Sección `#features`: 6 tarjetas en grid 3x2, **cada una con icono**; **exactamente una** (reportes) lleva el tag de estado diferido.
- Barra de lista de espera (`#waitlist`): barra **fija** al pie de la pantalla y de ancho completo, con CTA visible, habilitado y **sin acción** en esta spec; se oculta cuando `#demo` está en pantalla (ver §Lista de espera).
- Sección `#comparison`: tabla comparativa manual vs producto, 2 columnas.
- Sección `#pricing`: 4 planes con altura uniforme, con badge de popularidad en el destacado; los 4 botones están visibles, habilitados y **sin acción** en esta spec.
- Sección `#demo`: tarjeta con **CTA único** de lista de espera visible, habilitado y **sin acción**; sin badge ni botón deshabilitado (ver §Agenda tu demo).
- Sección `#faq`: acordeón.
- Footer en tres filas (ver §Diseño). Sin navegación interna.
- Los CTA de lista de espera se muestran activos, pero no abren modal, navegan ni envían datos en esta spec. El formulario y envío de correo se implementarán en la spec 003.
- Modo oscuro con `data-theme`, `localStorage` `stockma-theme`, anti-FOUC y actualización del tema MUI.
- Sin React Router (usar switch por `window.location.pathname` en `App.tsx`: `/login` -> `AuthFlow`, resto -> `LandingPage`). Diferido a T058.

## Fuera de alcance

- Modal, formulario de lista de espera y envío de correo; se implementarán en la spec 003. El prototipo HTML local no forma parte de la implementación de 002.
- Modal/CTA de login en la landing y autenticación desde la landing.
- Chat de demo con redirección a Cal.com.
- Agenda propia / backend de demo.
- Precios cobrables / pasarela (valores son placeholders).
- Nuevas funcionalidades del producto (solo presentación).
- Router SPA completo.

## Estructura de la landing

1. Header (logo, nav, tema, menú móvil)
2. Hero (H1 con segmento rotativo, subtítulo, 2 CTAs, mock visual de tabla)
3. Para quién es Stockma (`#audience`, 4 tarjetas)
4. Cómo funciona (3 pasos, `#how-it-works`)
5. Funcionalidades principales (6 tarjetas, `#features`)
6. Comparativa manual vs Stockma (`#comparison`)
7. Planes (`#pricing`)
8. Preguntas frecuentes (`#faq`)
9. Agenda tu demo (`#demo`)
10. Barra de lista de espera fija al pie (`#waitlist`, `<aside>` como último hijo de `<main>`)
11. Footer

## Navegación interna y anclas

- Los clics primarios en enlaces `href="#..."` dentro de la landing llaman a `scrollIntoView({ block: 'start' })`; la ruta y el hash permanecen sin cambios. Los clics con modificadores conservan el comportamiento nativo del enlace.
- El viewport reserva **73px** para el header sticky (72px de contenido más el borde) en todos los anchos. Los cuatro destinos del navbar deben quedar con su borde superior a no más de 6px del borde inferior del header.
- En anchos de **900px o menos**, la navegación usa un panel absoluto superpuesto; abrirlo o cerrarlo no cambia la altura del header. Al seguir un enlace, el panel se cierra y se conserva la alineación del destino.
- `#demo` está cerca del final del documento. Si el límite inferior de scroll impide alinearlo, la navegación agrega temporalmente solo el espacio de scroll necesario a esa sección; el espacio se recalcula al siguiente clic interno.
- Respetar `prefers-reduced-motion`: usar desplazamiento instantáneo si el usuario reduce el movimiento.

## Hero

- **H1**: dos segmentos; el segundo es un **texto rotativo** que alterna 3 frases con animación de fundido (fade + desplazamiento vertical, ciclo de 3s, transición de 400ms). Jerarquía tipográfica y strings según los componentes. DEBE respetar `prefers-reduced-motion` (ver CA-16).
- **Subtítulo**: copy institucional definido en los componentes. Validar jerarquía tipográfica y adaptación en viewports móviles.
- **CTA primario**: botón hacia la lista de espera, visible y habilitado, sin acción asociada en esta spec.
- **CTA secundario**: enlace a `#how-it-works`.
- **Mock visual de tabla** (columna derecha, decorativo pero interactivo):
  - Leyenda descriptiva y encabezado con píldora de estado (strings en el componente).
  - `tablist` conmutable con **3 pestañas**, la primera activa por defecto, cada una con **3 filas** de ejemplo.
  - La tercera pestaña (vencimientos) aplica **3 chips de días** con estados cromáticos: normal, warn y danger.
  - Strings, valores y estados exactos en el componente.

## Aparición del contenido

- Cada sección principal se revela una sola vez cuando al menos el 8% entra en el viewport: opacidad de 0 a 1 y desplazamiento vertical máximo de 12px, con transición `ease-out` de 450ms.
- La animación se aplica al contenedor interno, no al elemento `<section>`, para que el movimiento visual no cambie la geometría de los destinos de navegación.
- No se usa retraso escalonado. El contenido permanece visible si `IntersectionObserver` no está disponible.
- Con `prefers-reduced-motion: reduce`, las secciones aparecen sin transición, el texto rotativo del hero permanece estático, el menú/barra no se deslizan y el scroll es instantáneo.

## Cómo funciona

- Título y subtítulo: copy definido en los componentes.
- **3 pasos**, cada uno con título y descripción definidos en el componente.
- Validar numeración 1-3 y **ausencia de un cuarto paso**.

## Funcionalidades principales

- Subtítulo: copy definido en los componentes.
- **6 tarjetas** en grid 3x2 (3 columnas a ≥1024px). Títulos y descripciones según el orden definido en los componentes.
- **Exactamente una** tarjeta —la de reportes— lleva el tag de estado diferido. Ninguna otra lo lleva.
- La tarjeta de planes menciona la duración de la prueba gratuita definida en el copy versionado.

## Comparativa

- Título y subtítulo: copy versionado en los componentes.
- **Columna izquierda** (estado sin el producto): título y **5 bullets** en negrita con descripción.
- **Columna derecha** (estado con el producto): título y **5 bullets** en negrita con descripción.
- > **Nota de producto**: el copy promocional representa capacidades objetivo aprobadas para el roadmap, no una declaración de disponibilidad actual. Su implementación queda fuera de esta spec.

## Planes

- Título y subtítulo: copy definido en los componentes (incluye el parámetro de prueba gratuita).
- **4 tarjetas** con misma altura (`grid-auto-rows: 1fr`, `align-items: stretch`, botón con `margin-top: auto`):
  1. Plan base — precio mensual placeholder — descripción y 3 bullets — CTA **secundario**.
  2. Plan destacado — precio mensual placeholder — descripción y 4 bullets — CTA **primario**.
  3. Plan intermedio — precio mensual placeholder — descripción y 3 bullets — CTA **secundario**.
  4. Plan a medida — **sin precio numérico**, borde punteado — descripción y 3 bullets — CTA **secundario**.
- Nombres, precios, descripciones, bullets y labels de botón: en el copy versionado de los componentes.
- La tarjeta destacada lleva un **badge de popularidad** definido en el copy de los componentes; el precio mensual muestra la unidad separada del monto.

Los 4 botones se muestran habilitados y sin acción asociada en esta spec: no abren una modal ni navegan a otra sección. Las tarjetas de plan además son seleccionables (click/Enter/Espacio marca `is-selected` y `aria-pressed`).

## Agenda tu demo

Apartado en la página (`#demo`), tarjeta con fondo acentuado, bordes 10px y sombra.

- **Título y descripción**: copy versionado en el componente.
- **CTA único** (lista de espera): visible y habilitado, sin acción asociada en esta spec.
- El rediseño (2026-11-08) **eliminó** el badge de estado diferido y el botón de agendar deshabilitado del diseño anterior.

## Lista de espera (barra)

La modal, el formulario y el envío de correo quedan fuera del alcance de esta spec; se implementarán en la spec 003.

**Barra (CTA)**

- Barra **fija** al pie (`position: fixed; bottom: 0`, `z-index` por encima del contenido), de ancho completo y con el mismo tratamiento visual del header (fondo translúcido, blur y borde superior). El contenido va dentro de `.wrap` para alinearse con el resto de la página.
- Se renderiza como **`<aside aria-label>`** con `id="waitlist"`, como **último hijo de `<main>`**.
- **Se oculta mientras `#demo` está en pantalla** (IntersectionObserver, `threshold: 0.1`): la sección demo tiene su propio CTA y no se duplica la llamada a la acción; al salir de vista la barra vuelve a aparecer con una animación de entrada desde el borde inferior.
- ≤900px: se apila (texto centrado) y el botón ocupa todo el ancho.
- **Título y texto**: copy versionado en el componente.

## Diseño

- **Tipografía:** Inter (única)
- **Bordes:** tokens globales 10px
- **Botones primarios:** teal de marca `--brand-teal` (`#337082`), hover `--brand-teal-active` (`#2b5f6d`) y texto blanco. Pasa WCAG AA (5.55:1) en claro y en oscuro. No se usa `--accent`, que en modo oscuro da 2.33:1 con texto blanco. Detalle en `design.md`.
- **Modo oscuro:** `[data-theme='dark']`, persistido en `localStorage`, script anti-FOUC
- **Sin chips/banners inclinados** sobre secciones
- **Pantallas chicas (≤600px):** escala tipográfica reducida (h1 30px, h2 de sección 26px, lead 15px, cuerpo de tarjetas 14px) y botones de ancho completo con altura flexible.
- **Densidad y paleta:** separación vertical de secciones `50px` (mayor densidad pedida en §Contexto); títulos de tarjeta en `--color-accent-strong`; neutros en escala slate (`--color-text-primary: #0f172a`, `--color-border: #cbd5e1`, `--color-tint-accent` al 40%). Detalle en `tokens.css`.
- **Sin voseo**
- **Footer:** logo apilado recortado (`Stockma_vertical_trim.png`, 118px), iconos circulares de WhatsApp e Instagram (componentes de `@mui/icons-material`, `currentColor`) con `rel` seguro en nueva pestaña hacia `https://w.app/stockmaapp` e `https://www.instagram.com/stockma_app?dlrf=MXAzNTU1OWl5YzIwZw==` respectivamente; línea de soporte cuyo ancla apunta a `https://w.app/stockmaapp`, y copyright; ambos en disposición `space-between`. **Sin número de teléfono visible.** La sección demo precede al footer; la barra fija de lista de espera es independiente del flujo y se oculta durante la demo.

## Reglas de copy

- El copy comercial comunica la visión de producto aprobada para el roadmap y no sustituye la documentación del estado actual de implementación.
- Descripciones cortas y directas
- Valor de negocio, no obviedades
- Mensaje central: stock centralizado y paso de procesos manuales a automatizados
- **El copy versionado vive en los componentes de landing, no en esta spec** (regla de gobernación)

## Archivos involucrados

- `frontend/web/src/App.tsx` (switch por ruta para integrar landing)
- `frontend/web/src/main.tsx` (ThemeProvider)
- `frontend/web/src/styles/theme/mui-bridge.ts`
- `frontend/web/src/styles/tokens.css`
- `frontend/web/src/styles/components/texts.tsx` (`MutedText`, **global**: quitó `margin: 0` en el rediseño — afecta a toda la app, no sólo a la landing)
- `frontend/web/src/features/landing/landing-texts.ts` (escala de títulos y leads)
- `frontend/web/src/features/landing/landing.css` (offset de anclas y animación de entrada respetando movimiento reducido)
- `frontend/web/src/features/landing/components/LandingPage/LandingPage.tsx` (navegación interna, runway para `#demo` y observación de secciones)
- `frontend/web/src/features/landing/components/Header/` (header sticky y menú móvil superpuesto)
- `frontend/web/src/features/landing/components/Hero/` (texto rotativo y reducción de movimiento)
- `frontend/web/src/features/landing/components/DemoSection/` (runway calculado para alinear el último destino)
- `frontend/web/src/features/landing/components/WaitlistSection/` (barra fija con ID y fallback sin `IntersectionObserver`)
- `frontend/web/src/features/landing/components/LandingPage/LandingPage.test.tsx` (orden, anclas, reveal y CTAs)
- `frontend/web/src/assets/Stockma_horizontal.png`
- `frontend/web/src/assets/Stockma_vertical.png`
- `frontend/web/src/assets/Stockma_vertical_trim.png` (recorte al ras para el footer)
- `frontend/web/src/assets/Stockma_logo.png`
- `frontend/web/src/assets/Stockma_name.png`
- `specs/002-product-introduction/Stockma_landing.html` (prototipo visual local, ignorado por Git y no normativo)
- `scripts/check-neutral-es.mjs` (gate)

## Criterios de aceptación

- [x] Landing accesible en `/` con todas las secciones
- [x] Nav funcional y responsive (incluye menú móvil)
- [x] Modo claro/oscuro funcional
- [x] Hero: CTA primario permanece habilitado sin acción, CTA secundario lleva a `#how-it-works` y las pestañas del mock cambian de panel
- [x] Tarjetas de planes misma altura
- [x] Los 4 botones de planes permanecen habilitados sin acción en esta spec
- [x] Sección `#demo`: tarjeta acentuada con CTA único de lista de espera habilitado sin acción (sin badge ni botón deshabilitado)
- [x] Barra de lista de espera fija al pie, oculta mientras `#demo` está en pantalla, con `id="waitlist"`, de ancho completo y apilada en móvil
- [x] Footer alineado al mismo eje que el resto de la página (24px de padding lateral)
- [x] Sin voseo (pasa gate)
- [x] Copy comercial conservado como roadmap aprobado; la tarjeta de reportes mantiene su tag de estado diferido. La disponibilidad actual se rige por la implementación, no por el copy de landing.
- [x] Bordes coherentes 8-10px
- [x] Footer: logo, iconos de WhatsApp e Instagram con enlace, línea de soporte con enlace a WhatsApp y copyright
- [x] Copy versionado y gobernado por los componentes; el prototipo HTML local permanece ignorado y no normativo, según decisión de producto.
- [x] Hero: segundo segmento rotativo (3 frases, fundido) y contenido estático cuando se activa `prefers-reduced-motion`
- [x] `#audience` con 4 tarjetas de segmento → QUIEN-01
- [x] Todos los enlaces del navbar alinean el inicio de su sección bajo el header en 1440, 900, 600 y 375px; el menú móvil se cierra sin cambiar el destino.
- [x] El contenido entra con una transición sutil de 450ms y 12px, sin transformar las cajas de sección ni animar cuando se reduce el movimiento.
