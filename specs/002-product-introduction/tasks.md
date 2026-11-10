# Tasks: Product Introduction Landing

> **Regla de gobernación aplicada**: este artefacto gobierna _qué archivos se tocan y en qué orden_. No contiene copy literal de UI; el copy versionado vive en los componentes. El HTML local es una referencia visual ignorada por Git.

## Review Workload Forecast

| Campo             | Valor                       |
| ----------------- | --------------------------- |
| Líneas estimadas  | ~4.000 (14 comp.+CSS+tests) |
| Riesgo 400 líneas | **High**                    |
| PRs encadenados   | **Yes**                     |
| Entrega           | `ask-on-risk`               |

```
Decision needed before apply: Yes
Chained PRs recommended: Yes
Chain strategy: stacked-to-main
400-line budget risk: High
```

### Suggested Work Units

| Unit | Objetivo              | PR  | Base       |
| ---- | --------------------- | --- | ---------- |
| 1    | Tema/anti-FOUC/assets | PR1 | main/stage |
| 2    | Switch de rutas       | PR2 | rama PR1   |
| 3    | Estructura/Header     | PR3 | rama PR2   |
| 4    | Secciones             | PR4 | rama PR3   |
| 5    | Planes/demo/footer    | PR5 | rama PR4   |
| 6    | Lista de espera       | PR6 | rama PR5   |
| 7    | Tests/gate            | PR7 | rama PR6   |

> Base: `frontend/web/src/`.

## Phase 1: Tema y assets

- [x] 1.1 `styles/tokens.css`: tokens `[data-theme='dark']` + radius 10px.
- [x] 1.2 `frontend/web/index.html`: anti-FOUC `stockma-theme` + links Inter.
- [x] 1.3 `main.tsx`: listener `stockma-theme-change` recrea tema MUI; validar `mui-bridge.ts`.
- [x] 1.4 `assets/`: PNGs `Stockma_*.png`; dimensionar por artwork visible (~49%), no por canvas.

## Phase 2: Rutas

- [x] 2.1 `App.tsx`: pathname: `/login`→`AuthFlow`, resto→`LandingPage` (T058, sin Router).

## Phase 3: Estructura

- [x] 3.1 `features/landing/`: `types.ts` con `interface Plan` (design); `index.ts` exporta `LandingPage`.
- [x] 3.2 `features/landing/components/LandingPage/LandingPage.tsx`: 11 secciones de spec; `WaitlistSection` último hijo de `<main>`.
- [x] 3.3 `features/landing/components/Header/Header.tsx`: anclas de secciones, toggle de tema, menú móvil.

## Phase 4: Secciones

- [x] 4.1 `components/Hero/Hero.tsx`: H1 itálica; CTA primario habilitado sin acción, secundario → `#how-it-works`.
- [x] 4.2 `components/Hero/HeroMockTable.tsx`: `tablist` de 3 pestañas; 3 chips de días con estados normal / warn / danger.
- [x] 4.3 `components/WhoItsFor/WhoItsFor.tsx`: `#audience`: 3 tarjetas de segmento.
- [x] 4.4 `components/HowItWorks/HowItWorks.tsx`: `#how-it-works`: 3 pasos.
- [x] 4.5 `components/Features/Features.tsx`: `#features`: grid 3x2; tag de estado diferido solo en la tarjeta de reportes.
- [x] 4.6 `components/Comparison/Comparison.tsx`: `#comparison`: columnas sin/con el producto, 5 bullets cada una.
- [x] 4.7 `components/Faq/Faq.tsx`: `#faq`: acordeón con 6 preguntas.

## Phase 5: Planes y footer

- [x] 5.1 `components/Pricing/Pricing.tsx`: `#pricing`: 4 planes altura uniforme (`grid-auto-rows:1fr`, botón `margin-top:auto`); selección con `aria-pressed`.
- [x] 5.2 `components/Pricing/Pricing.tsx`: botones activos sin acción; primarios con `--brand-teal`/`--brand-teal-active`, nunca `--accent` (2.33:1).
- [x] 5.3 `components/DemoSection/DemoSection.tsx`: `#demo`: badge de estado diferido, botón principal deshabilitado y CTA de lista de espera activo sin acción.
- [x] 5.4 `components/Footer/Footer.tsx`: 3 filas: `Stockma_vertical_trim.png` (118px), iconos SVG inline WhatsApp/Instagram (`currentColor`), soporte (ancla WhatsApp), copyright; hairline `var(--border)`.

## Phase 6: Barra de lista de espera

- [x] 6.1 `components/WaitlistSection/WaitlistSection.tsx`: `#waitlist` fixed `bottom:0` full-bleed, último hijo de `<main>`; `.wrap` usa `padding-block`, no shorthand `padding`; CTA activo sin acción; ≤900px apilada, botón full-width.
- [x] 6.2 `features/landing/landing.css`: ≤600px: h1 30px, h2 25px, cuerpo 14px; botones ancho completo.

## Phase 7: Tests

- [x] 7.1 `App.test.tsx`: `/` renderiza LandingPage, `/login` AuthFlow (RTL).
- [x] 7.2 `Header.test.tsx`: toggle: `data-theme`, `localStorage`, evento `stockma-theme-change`.
- [x] 7.3 `HeroMockTable.test.tsx`: pestaña conmuta panel visible.
- [x] 7.4 `LandingPage.test.tsx`: los CTA de lista de espera están habilitados, no navegan y no abren modal.
- [x] 7.5 `LandingPage.test.tsx`: 11 secciones con sus ids en inglés y en orden.
- [x] 7.6 `Pricing.test.tsx`: clic/Enter/Espacio marcan `is-selected` y `aria-pressed` en la tarjeta activa y desmarcan la anterior.

## Phase 8: Gate

- [x] 8.1 `node scripts/check-neutral-es.mjs` → ok (sin voseo).
- [x] 8.2 Revisión visual responsive en claro/oscuro, ≤900px y ≤600px. Verificado en 1440/900/600/375: sin scroll horizontal, h1 56→30px, h2 44→25px, menú móvil y barra apilada; logos con variante oscura.

## Phase 9: Rediseño visual (2026-11-08)

> Supersede parcialmente 4.1 (H1 itálica), 4.3 (3 segmentos), 5.3 (demo con badge), 5.4 (iconos SVG inline) y 6.1 (barra sticky). El prototipo HTML se mantiene local y no normativo por decisión de producto; el copy versionado vive en los componentes.

Aplicado:

- [x] 9.1 `components/Hero/Hero.tsx` + `Hero.module.css`: H1 con segundo segmento rotativo (3 frases, ciclo 3s, fundido 400ms) y mock con nombre de negocio.
- [x] 9.2 `components/WhoItsFor/WhoItsFor.tsx` + `.module.css`: `#audience` con 4 tarjetas (nuevo segmento), grid 4→2 columnas.
- [x] 9.3 `components/Features/Features.tsx` + `.module.css`: icono `@mui/icons-material` por tarjeta; tag de reportes al pie de la tarjeta; prueba gratuita mencionada en copy.
- [x] 9.4 `components/Comparison/Comparison.tsx`: copy nuevo, sin intros, 5 bullets por columna.
- [x] 9.5 `components/Pricing/Pricing.tsx` + `.module.css`: badge de popularidad en el plan destacado; precio con unidad en wrapper propio; hover con elevación.
- [x] 9.6 `components/DemoSection/DemoSection.tsx` + `.module.css`: rediseño a CTA único de lista de espera (sin badge ni botón deshabilitado).
- [x] 9.7 `components/WaitlistSection/WaitlistSection.tsx` + `.module.css`: barra `fixed` con `IntersectionObserver` sobre `#demo`; `<aside aria-label>`; animación `slideUp`.
- [x] 9.8 `components/Footer/Footer.tsx`: iconos MUI WhatsApp/Instagram; corrección de "Escribénos".
- [x] 9.9 `styles/tokens.css`: neutros slate, `--color-tint-accent` 40%, tokens de login en oscuro; `index.html` y `README.md` con Inter.
- [x] 9.10 Densidad y escala: `landing.module.css` (`.spaced` 50px, títulos de tarjeta), `landing-texts.ts` (títulos 48px/700, leads), `texts.tsx` (`MutedText` sin `margin: 0` — impacto global, verificar).

Resuelto:

- [x] 9.11 Mantener el prototipo HTML local/ignorado y no normativo; actualizar la gobernación del copy en spec, diseño y checklist.
- [x] 9.12 Actualizar `LandingPage.test.tsx`: mock de `IntersectionObserver`, aserciones del H1 rotativo e `id="waitlist"` en el `<aside>`; `WaitlistSection` conserva un fallback visible si la API no existe.
- [x] 9.13 Actualizar tests de tokens (`mui-bridge.test.tsx`, `AuthLayout.test.tsx`) a los nuevos valores.
- [x] 9.14 Corregir `tsc`: eliminar el import sin uso de `MutedText` en `Comparison.tsx`.
- [x] 9.15 Conservar el copy promocional actual como descripción de capacidades objetivo del roadmap, por decisión de producto; la implementación de esas funcionalidades queda fuera de 002.
- [x] 9.16 Formatear los archivos de código landing reportados por `format:check`.
- [x] 9.17 `prefers-reduced-motion` en la rotación del hero y en las transiciones de entrada (WCAG 2.2.2).
- [x] 9.18 Eliminar `components/DemoSection/DemoSection.styles.ts`, verificado sin referencias.

## Phase 10: Navegación y aparición sutil (2026-11-09)

- [x] 10.1 `LandingPage.tsx`: alinear los destinos bajo el header sticky; preservar la navegación nativa con modificadores y cerrar el menú móvil sin cambiar el layout.
- [x] 10.2 `#demo`: agregar solo el espacio de scroll que falta cuando el límite inferior del documento impide alinear el inicio de la sección; recalcularlo en el siguiente enlace interno.
- [x] 10.3 `landing.css`: revelar una vez el contenido interno de cada sección al entrar en el viewport (opacidad + 12px, 450ms); no transformar las cajas que reciben el scroll.
- [x] 10.4 Hero, reveal, menú y barra: respetar `prefers-reduced-motion`; el contenido permanece visible antes del callback y cuando `IntersectionObserver` no está disponible.
- [x] 10.5 Validar los cuatro destinos y el cierre del menú en 36 combinaciones de viewport: anchos 1440/1024/901/900/768/600/480/375/320px, alturas entre 640 y 900px; sin overflow horizontal ni offset sobre el header.
