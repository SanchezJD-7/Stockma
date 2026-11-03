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

- [ ] 1.1 `styles/tokens.css`: tokens `[data-theme='dark']` + radius 10px.
- [ ] 1.2 `frontend/web/index.html`: anti-FOUC `stockma-theme` + links Work Sans.
- [ ] 1.3 `main.tsx`: listener `stockma-theme-change` recrea tema MUI; validar `mui-bridge.ts`.
- [ ] 1.4 `assets/`: PNGs `Stockma_*.png`; dimensionar por artwork visible (~49%), no por canvas.

## Phase 2: Rutas

- [ ] 2.1 `App.tsx`: pathname: `/login`→`AuthFlow`, resto→`LandingPage` (T058, sin Router).

## Phase 3: Estructura

- [ ] 3.1 `features/landing/`: `types.ts` con `interface Plan` (design); `index.ts` exporta `LandingPage`.
- [ ] 3.2 `features/landing/components/LandingPage/LandingPage.tsx`: 11 secciones de spec; `WaitlistSection` último hijo de `<main>`.
- [ ] 3.3 `features/landing/components/Header/Header.tsx`: anclas de secciones, toggle de tema, menú móvil.

## Phase 4: Secciones

- [ ] 4.1 `components/Hero/Hero.tsx`: H1 itálica; primario abre modal, secundario → `#como-funciona`.
- [ ] 4.2 `components/Hero/HeroMockTable.tsx`: pestañas Productos/Lotes/Por vencer; chips 21/9 warn, 3 danger.
- [ ] 4.3 `components/WhoItsFor/WhoItsFor.tsx`: `#para-quien-es`: Droguerías, Minimercados, Distribuidoras.
- [ ] 4.4 `components/HowItWorks/HowItWorks.tsx`: `#como-funciona`: 3 pasos, copy exacto.
- [ ] 4.5 `components/Features/Features.tsx`: `#producto`: grid 3x2; tag "Próximamente" solo en reportes.
- [ ] 4.6 `components/Comparison/Comparison.tsx`: `#comparativa`: columnas Sin/Con Stockma, bullets de spec.
- [ ] 4.7 `components/Faq/Faq.tsx`: `#preguntas`: acordeón, copy del prototipo.

## Phase 5: Planes y footer

- [ ] 5.1 `components/Pricing/Pricing.tsx`: `#planes`: 4 planes altura uniforme (`grid-auto-rows:1fr`, botón `margin-top:auto`); selección con `aria-pressed`.
- [ ] 5.2 `components/Pricing/PricingButton.tsx`: botones `href="#lista-espera"`+`data-open-waitlist` abren modal; primarios con `--primary`/`--on-primary`/`--primary-active`, nunca `--accent` (2.33:1).
- [ ] 5.3 `components/DemoSection/DemoSection.tsx`: `#agendar`: badge "Próximamente", botón deshabilitado, CTA a lista de espera.
- [ ] 5.4 `components/Footer/Footer.tsx`: 3 filas: `Stockma_vertical_trim.png` (118px), iconos SVG inline WhatsApp/Instagram (`currentColor`), soporte (ancla WhatsApp), copyright; hairline `var(--border)`.

## Phase 6: Lista de espera

- [ ] 6.1 `components/WaitlistModal/WaitlistModal.tsx`: nombre, bandera Colombia con `+57` fijo, correo; submit `preventDefault`.
- [ ] 6.2 `components/WaitlistModal/WaitlistModal.tsx`: `is-submitted`: oculta campos/botón, conserva título; "¡Listo, {nombre}!" capitalizado; sin JS "¡Listo!".
- [ ] 6.3 `components/WaitlistModal/SuccessBadge.tsx`: badge animado (ripple + check `stroke-dashoffset`, `--brand-teal`), `prefers-reduced-motion`; cierra con X, fuera, Esc.
- [ ] 6.4 `components/WaitlistSection/WaitlistSection.tsx`: `#lista-espera` sticky `bottom:0` full-bleed, último hijo de `<main>`; `.wrap` usa `padding-block`, no shorthand `padding`; ≤900px apilada, botón full-width.
- [ ] 6.5 `features/landing/landing.css`: ≤600px: h1 30px, h2 25px, cuerpo 14px; botones ancho completo.

## Phase 7: Tests

- [ ] 7.1 `App.test.tsx`: `/` renderiza LandingPage, `/login` AuthFlow (RTL).
- [ ] 7.2 `Header.test.tsx`: toggle: `data-theme`, `localStorage`, evento `stockma-theme-change`.
- [ ] 7.3 `HeroMockTable.test.tsx`: pestaña conmuta panel visible.
- [ ] 7.4 `WaitlistModal.test.tsx`: abre; Esc/X/backdrop cierran; submit muestra `is-submitted`+nombre.
- [ ] 7.5 `LandingPage.test.tsx`: 11 secciones con sus ids.

## Phase 8: Gate

- [ ] 8.1 `node scripts/check-neutral-es.mjs` → ok (sin voseo).
- [ ] 8.2 `Stockma_landing.html`: paridad claro/oscuro, ≤900px, ≤600px.
