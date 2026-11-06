# Tasks: Product Introduction Landing

## Review Workload Forecast

| Campo | Valor |
|-------|-------|
| Líneas estimadas | ~4.000 (14 comp.+CSS+tests) |
| Riesgo 400 líneas | **High** |
| PRs encadenados | **Yes** |
| Entrega | `ask-on-risk` |

```
Decision needed before apply: Yes
Chained PRs recommended: Yes
Chain strategy: stacked-to-main
400-line budget risk: High
```

### Suggested Work Units

|Unit|Objetivo|PR|Base|
|----|--------|--|----|
|1|Tema/anti-FOUC/assets|PR1|main/stage|
|2|Switch de rutas|PR2|rama PR1|
|3|Estructura/Header|PR3|rama PR2|
|4|Secciones|PR4|rama PR3|
|5|Planes/demo/footer|PR5|rama PR4|
|6|Lista de espera|PR6|rama PR5|
|7|Tests/gate|PR7|rama PR6|

> Base: `frontend/web/src/`.

## Phase 1: Tema y assets

- [x] 1.1 `styles/tokens.css`: tokens `[data-theme='dark']` + radius 10px.
- [x] 1.2 `frontend/web/index.html`: anti-FOUC `stockma-theme` + links Work Sans.
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
- [x] 4.2 `components/Hero/HeroMockTable.tsx`: pestañas Productos/Lotes/Por vencer; chips 21/9 warn, 3 danger.
- [x] 4.3 `components/WhoItsFor/WhoItsFor.tsx`: `#audience`: Droguerías, Minimercados, Distribuidoras.
- [x] 4.4 `components/HowItWorks/HowItWorks.tsx`: `#how-it-works`: 3 pasos, copy exacto.
- [x] 4.5 `components/Features/Features.tsx`: `#features`: grid 3x2; tag "Próximamente" solo en reportes.
- [x] 4.6 `components/Comparison/Comparison.tsx`: `#comparison`: columnas Sin/Con Stockma, bullets de spec.
- [x] 4.7 `components/Faq/Faq.tsx`: `#faq`: acordeón, copy del prototipo.

## Phase 5: Planes y footer

- [x] 5.1 `components/Pricing/Pricing.tsx`: `#pricing`: 4 planes altura uniforme (`grid-auto-rows:1fr`, botón `margin-top:auto`); selección con `aria-pressed`.
- [x] 5.2 `components/Pricing/Pricing.tsx`: botones activos sin acción; primarios con `--primary`/`--on-primary`/`--primary-active`, nunca `--accent` (2.33:1).
- [x] 5.3 `components/DemoSection/DemoSection.tsx`: `#demo`: badge "Próximamente", botón "Agendar demo" deshabilitado y CTA de lista de espera activo sin acción.
- [x] 5.4 `components/Footer/Footer.tsx`: 3 filas: `Stockma_vertical_trim.png` (118px), iconos SVG inline WhatsApp/Instagram (`currentColor`), soporte (ancla WhatsApp), copyright; hairline `var(--border)`.

## Phase 6: Barra de lista de espera

- [x] 6.1 `components/WaitlistSection/WaitlistSection.tsx`: `#waitlist` sticky `bottom:0` full-bleed, último hijo de `<main>`; `.wrap` usa `padding-block`, no shorthand `padding`; CTA activo sin acción; ≤900px apilada, botón full-width.
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
- [x] 8.2 `Stockma_landing.html`: paridad claro/oscuro, ≤900px, ≤600px. Verificado en 1440/900/600/375: sin scroll horizontal, h1 56→30px, h2 44→25px, menú móvil y barra apilada; logos con variante oscura.
