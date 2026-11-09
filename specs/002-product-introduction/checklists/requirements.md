# Checklist de Requerimientos: Product Introduction Landing

**Feature branch**: `002-product-introduction`
**Fuente**: [`../spec.md`](../spec.md) — la spec 002 no usa IDs FR-xxx; cada ítem usa un ID propio trazable a la sección del spec.
**Fuente de verdad versionada del copy**: los componentes de landing. `Stockma_landing.html` es una referencia visual local, ignorada por Git y no normativa.
**Referencias**: [`../design.md`](../design.md) · [`../tasks.md`](../tasks.md)

> **Regla de gobernación aplicada**: este checklist gobierna _comportamiento, estructura, estado, accesibilidad y restricciones responsivas_. No contiene cadenas de UI literales. El copy se mantiene en los componentes versionados; no se exige paridad de commit con el prototipo local.

Marcar un ítem sólo cuando el comportamiento esté implementado **y** cubierto por al menos un test verde o una verificación manual registrada. La marca `[SIN TAREA]` indica que el comportamiento no tiene tarea correspondiente en el artefacto tasks actual.

> **Estado del cierre**: rediseño visual, navegación responsive, animación de entrada, reduced motion, criterios de producto y pruebas se revisaron y validaron. El copy comercial actual se conserva como roadmap aprobado; la implementación de esas funcionalidades no forma parte de 002. El prototipo HTML permanece local e ignorado por decisión de producto.

---

## Ruta y estructura

- [x] **RUTA-01 — Switch de ruta sin React Router**
      `App.tsx` DEBE decidir por `window.location.pathname`: `/login` renderiza `AuthFlow`; cualquier otra ruta renderiza `LandingPage`.
      _Spec_: §Alcance (último punto) · _Tareas_: 2.1, 2.3 · _Diseño_: "Decisión: Cambio de ruta sin React Router"
      **Verificación**: `/` muestra la landing; `/login` muestra el flujo de autenticación actual; una ruta desconocida (p. ej. `/foo`) muestra la landing. Cubierto por test RTL en `App.test.tsx`.

- [x] **RUTA-02 — Landing con las 11 secciones en orden**
      La landing DEBE renderizar, en este orden: Header, Hero, `#audience`, `#how-it-works`, `#features`, `#comparison`, `#pricing`, `#faq`, `#demo`, `#waitlist` (último hijo de `<main>`), Footer.
      _Spec_: §Estructura de la landing · _Tareas_: 3.3 [SIN TAREA para WaitlistSection y WhoItsFor]
      **Verificación**: cada `id` existe exactamente una vez en el DOM y el orden visual coincide con el spec.

## Header

- [x] **HEADER-01 — Barra de navegación superior sin CTA de login**
      El header DEBE contener el logo (enlace a `/`), los links de navegación y el control de cambio de tema; NO DEBE contener CTA ni botón de login.
      _Spec_: §Alcance, punto 2 · _Tareas_: 3.4
      **Verificación**: logo, links y toggle presentes; grep en el header: sin botón de login ni enlace de autenticación.

- [x] **HEADER-02 — Links de navegación**
      Los 4 links DEBEN apuntar, en este orden, a: `#features`, `#how-it-works`, `#pricing` y `#demo`. Las etiquetas visibles se definen en los componentes.
      _Spec_: §Alcance, punto 2 · _Tareas_: 3.4
      **Verificación**: cada link navega a su ancla por scroll; las etiquetas coinciden con el copy versionado.

- [x] **HEADER-03 — Menú móvil (≤900px)**
      El header DEBE colapsar en un menú desplegable en pantallas de ancho ≤900px, con los mismos targets que la navegación de escritorio.
      _Spec_: §Alcance, punto 2 · _Tareas_: 3.4, 4.4
      **Verificación**: a 375px los links de escritorio no están visibles; el botón hamburguesa alterna el panel con `aria-expanded`; al elegir un link el menú se cierra.

- [x] **HEADER-04 — Tratamiento visual translúcido**
      El header DEBE usar fondo translúcido (`color-mix`), `backdrop-filter` (blur) y borde interior; la barra de lista de espera replica este tratamiento.
      _Spec_: §Lista de espera (Barra) · _Tareas_: 3.4
      **Verificación**: al hacer scroll, el contenido se ve difuminado bajo el header; header y barra `#waitlist` comparten la misma receta visual.

## Hero

- [x] **HERO-01 — H1 con segundo segmento rotativo**
      El hero DEBE renderizar el H1 definido en los componentes, compuesto por dos segmentos; el segundo alterna 3 frases con animación de fundido (ciclo de 3s, transición de 400ms) y DEBE respetar `prefers-reduced-motion`.
      _Spec_: §Hero, CA-16 · _Tareas_: 9.1, 9.17
      **Verificación**: el `h1` contiene un `span` rotativo con las 3 frases; con `prefers-reduced-motion: reduce` la rotación no ocurre.

- [x] **HERO-02 — Subtítulo institucional**
      El componente hero DEBE renderizar el subtítulo corporativo definido en los componentes, validando su jerarquía tipográfica y su correcta adaptación en viewports móviles.
      _Spec_: §Hero · _Tareas_: 3.5
      **Verificación**: el texto del subtítulo coincide con el copy versionado; sin desbordes a 375px.

- [x] **HERO-03 — CTA primario sin acción en esta spec**
      El CTA primario del hero DEBE permanecer visible y habilitado, sin acción asociada en este cambio.
      _Spec_: §Hero, §Alcance · _Tareas_: 3.5, 4.6
      **Verificación**: el botón no está deshabilitado; el clic no abre un diálogo ni cambia ruta o hash.

- [x] **HERO-04 — CTA secundario**
      El CTA secundario del hero DEBE ser un enlace a `#how-it-works`.
      _Spec_: §Hero · _Tareas_: 3.5, 4.6
      **Verificación**: el enlace tiene `href="#how-it-works"` y el clic hace scroll a la sección.

- [x] **HERO-05 — Mock visual de tabla interactivo**
      La columna derecha del hero DEBE mostrar el mock decorativo pero interactivo: leyenda, encabezado con píldora de estado, y un `tablist` conmutable con **3 pestañas** (la primera activa por defecto), cada una con **3 filas** de ejemplo. La tercera pestaña aplica **3 chips de días** con estados cromáticos normal / warn / danger.
      _Spec_: §Hero (Mock visual de tabla) · _Tareas_: 3.5 [SIN TAREA para la conmutación de pestañas]
      **Verificación**: los textos de cabecera coinciden con el copy versionado; al hacer clic en cada pestaña cambia el panel y `aria-selected` refleja la activa; los 3 chips muestran sus estados cromáticos. Test unitario de la lógica de conmutación (design: estrategia de testing, fila "Unit").

## Para quién es Stockma

- [x] **QUIEN-01 — Sección de segmentos objetivo**
      La sección `#audience` DEBE presentar **4 tarjetas** de segmento, cada una con título y descripción definidos en los componentes.
      _Spec_: §Alcance, punto 3 · _Diseño_: componente `WhoItsFor` · _Tareas_: 9.2
      **Verificación**: la sección existe con `id="audience"` y renderiza las 4 tarjetas con título y texto definidos en los componentes.

## Cómo funciona

- [x] **COMO-01 — Título y subtítulo**
      La sección `#how-it-works` DEBE renderizar su título y subtítulo según el copy versionado, validando la jerarquía tipográfica.
      _Spec_: §Cómo funciona · _Tareas_: 3.6
      **Verificación**: `h2` y subtítulo coinciden con el copy versionado.

- [x] **COMO-02 — Tres pasos**
      La sección DEBE mostrar exactamente **3 pasos**, cada uno con título y descripción definidos en los componentes, numerados 1-3. NO DEBE existir un cuarto paso.
      _Spec_: §Cómo funciona · _Tareas_: 3.6
      **Verificación**: el contenedor tiene 3 artículos numerados 1-3 con título y descripción definidos en los componentes; no hay cuarto paso.

## Funcionalidades principales

- [x] **FUNC-01 — Seis tarjetas en grid 3x2 con icono y subtítulo**
      La sección `#features` DEBE renderizar su subtítulo definido en los componentes y **6 tarjetas** en cuadrícula de 3 columnas por 2 filas en escritorio, **cada una encabezada por un icono**.
      _Spec_: §Funcionalidades principales · _Tareas_: 3.7, 9.3
      **Verificación**: a ≥1024px la cuadrícula tiene 3 columnas; 6 tarjetas presentes, cada una con su `iconWrapper`; subtítulo coincide con el copy versionado.

- [x] **FUNC-02 — Orden y títulos de las 6 tarjetas**
      Los 6 títulos DEBEN coincidir, en orden, con los definidos en los componentes. La quinta tarjeta (planes) DEBE mencionar la duración de la prueba gratuita allí definida.
      _Spec_: §Funcionalidades principales · _Tareas_: 3.7
      **Verificación**: los 6 `h3` coinciden con el copy versionado; la tarjeta de planes menciona la prueba gratuita.

- [x] **FUNC-03 — Tag de estado diferido sólo en reportes**
      Únicamente la tarjeta de reportes DEBE llevar el tag de estado diferido definido en los componentes; ninguna otra tarjeta lo lleva.
      _Spec_: §Funcionalidades principales, criterio de aceptación · _Tareas_: 3.7, 4.8
      **Verificación**: grep sobre la sección: exactamente un tag de estado diferido, dentro de la tarjeta de reportes.

- [x] **FUNC-04 — Copy de producto aprobado para el roadmap**
      Las descripciones actuales comunican capacidades objetivo aprobadas por producto; no constituyen una afirmación de disponibilidad actual. La tarjeta de reportes conserva su tag de estado diferido.
      _Spec_: §Contexto, §Reglas de copy · _Tareas_: 9.15
      **Verificación**: decisión de producto registrada; implementación backend fuera del alcance de 002.

## Comparativa

- [x] **COMP-01 — Título y subtítulo**
      La sección `#comparison` DEBE renderizar su título y subtítulo según el copy versionado.
      _Spec_: §Comparativa · _Tareas_: 3.8
      **Verificación**: `h2` y subtítulo coinciden con el copy versionado.

- [x] **COMP-02 — Columna sin el producto**
      La columna izquierda DEBE titular y listar **5 bullets** en negrita con descripción, según los componentes.
      _Spec_: §Comparativa, columna izquierda · _Tareas_: 3.8, 9.4
      **Verificación**: título y los 5 bullets presentes con sus descripciones según el copy versionado.

- [x] **COMP-03 — Columna con el producto**
      La columna derecha DEBE titular y listar **5 bullets** en negrita con descripción, según los componentes.
      _Spec_: §Comparativa, columna derecha · _Tareas_: 3.8, 9.4
      **Verificación**: título y los 5 bullets presentes con sus descripciones según el copy versionado.

- [x] **COMP-04 — Copy de la columna "con el producto" aprobado como roadmap**
      Se conserva el copy actual de la columna derecha como presentación de capacidades objetivo aprobadas por producto; no se usa como prueba de disponibilidad actual.
      _Spec_: §Comparativa, §Contexto · _Tareas_: 9.4, 9.15
      **Verificación**: decisión de producto registrada; el componente versionado es la fuente de verdad.

## Planes

- [x] **PLAN-01 — Título y subtítulo**
      La sección `#pricing` DEBE renderizar su título y subtítulo según el copy versionado.
      _Spec_: §Planes · _Tareas_: 3.9
      **Verificación**: `h2` y subtítulo coinciden con el copy versionado.

- [x] **PLAN-02 — Cuatro planes**
      DEBEN mostrarse **4 planes** cuyo orden, nombres, precios, descripciones, bullets y labels de botón coinciden con el copy versionado. El CTA de la segunda tarjeta es **primario**; los otros tres, **secundarios**. Los precios son placeholders sin pasarela.
      _Spec_: §Planes · _Tareas_: 3.9 · _Diseño_: interfaz `Plan`
      **Verificación**: nombres, precios, descripciones, bullets y labels coinciden con el copy versionado; el orden de primarios/secundarios es el esperado.

- [x] **PLAN-03 — Altura uniforme**
      Las 4 tarjetas DEBEN tener la misma altura con el botón alineado al pie (`grid-auto-rows: 1fr`, `align-items: stretch`, `margin-top: auto` en el botón).
      _Spec_: §Planes · _Tareas_: 3.9
      **Verificación**: con descripciones y bullets de distinta longitud, las 4 tarjetas miden lo mismo y los botones quedan a la misma altura.

- [x] **PLAN-04 — Cuarta tarjeta con borde punteado**
      La cuarta tarjeta DEBE usar borde punteado y el estilo diferenciado definido en los componentes, mostrando el indicador de cotización en lugar de precio numérico.
      _Spec_: §Planes · _Tareas_: 3.9
      **Verificación**: la cuarta tarjeta tiene borde discontinuo y no muestra precio numérico.

- [x] **PLAN-05 — CTA de planes sin acción en esta spec**
      Los 4 botones de los planes DEBEN permanecer visibles y habilitados, sin abrir modal, navegar o enviar datos.
      _Spec_: §Planes, §Fuera de alcance · _Tareas_: 3.9
      **Verificación**: cada botón está habilitado; el clic no abre diálogo ni cambia ruta o hash.

- [x] **PLAN-06 — Tarjetas de plan seleccionables**
      Las tarjetas de plan DEBEN ser seleccionables: clic, Enter o Espacio marcan la tarjeta con la clase `is-selected` y actualizan `aria-pressed`.
      _Spec_: §Planes (último párrafo) · _Tareas_: [SIN TAREA]
      **Verificación**: test de teclado y puntero: la tarjeta activa queda en `is-selected`, `aria-pressed="true"`, y al seleccionar otra se desmarca la anterior.

- [x] **PLAN-07 — Badge de popularidad**
      La tarjeta destacada DEBE llevar un badge de popularidad definido en los componentes; ninguna otra tarjeta lo lleva.
      _Spec_: §Planes · _Tareas_: 9.5
      **Verificación**: revisión de código 2026-11-08: el badge existe sólo en la segunda tarjeta (`popular: true`).

## Preguntas frecuentes

- [x] **FAQ-01 — Acordeón de preguntas**
      La sección `#faq` DEBE renderizar su subtítulo y un acordeón con **6 preguntas**, cada una con su respuesta, definidas en los componentes.
      _Spec_: §Alcance, punto 9 · _Tareas_: 3.11
      **Verificación**: la sección existe con `id="faq"`; hay 6 preguntas; al abrir una se muestra su respuesta y al cerrarla se oculta; el copy coincide con los componentes.

## Agenda tu demo

- [x] **DEMO-01 — Apartado `#demo` con tarjeta acentuada**
      El apartado `#demo` DEBE mostrar una tarjeta con fondo acentuado y bordes de 10px.
      _Spec_: §Agenda tu demo · _Tareas_: 3.10
      **Verificación**: la tarjeta usa el fondo acentuado definido y `border-radius` de 10px.

- [x] **DEMO-02 — Sin badge de estado diferido**
      La tarjeta NO DEBE mostrar badge de estado diferido (eliminado en el rediseño 2026-11-08).
      _Spec_: §Agenda tu demo · _Tareas_: 9.6
      **Verificación**: revisión de código: no existe ningún badge dentro de la tarjeta `#demo`.

- [x] **DEMO-03 — Título y descripción**
      La tarjeta DEBE renderizar su título y descripción según el copy versionado.
      _Spec_: §Agenda tu demo · _Tareas_: 3.10
      **Verificación**: copy según los componentes.

- [x] **DEMO-04 — Sin botón deshabilitado**
      La tarjeta NO DEBE mostrar un botón deshabilitado (el botón de agendar se eliminó en el rediseño 2026-11-08); el único CTA es el de lista de espera.
      _Spec_: §Agenda tu demo · _Tareas_: 9.6
      **Verificación**: revisión de código: no hay elementos `disabled` en la tarjeta.

- [x] **DEMO-05 — CTA de lista de espera sin acción en esta spec**
      El CTA (lista de espera) DEBE permanecer visible y habilitado, sin acción asociada.
      _Spec_: §Agenda tu demo, §Fuera de alcance · _Tareas_: 3.10
      **Verificación**: el botón está habilitado; el clic no abre diálogo ni cambia ruta o hash.

## Lista de espera — barra fija

- [x] **LISTA-01 — Barra fija, `<aside>` y último hijo de `<main>`**
      La barra `#waitlist` DEBE ser el último hijo de `<main>`, renderizada como `<aside aria-label>` con `id="waitlist"`, con `position: fixed; bottom: 0` y de ancho completo (full-bleed), con el mismo tratamiento visual del header; su contenido va dentro de `.wrap`.
      _Spec_: §Lista de espera (Barra), §Estructura (paso 10) · _Diseño_: "Decisión: CTA de lista de espera como barra inferior fija con ocultamiento por observador" · _Tareas_: 9.7
      **Verificación**: en el DOM, el `<aside>` es el último hijo de `<main>` y tiene `id="waitlist"`; la barra toca los bordes izquierdo y derecho del viewport; su contenido está alineado al eje `.wrap`.

- [x] **LISTA-02 — Ocultamiento sobre `#demo` con IntersectionObserver**
      La barra DEBE estar visible desde que carga la página y ocultarse mientras `#demo` está en pantalla (IntersectionObserver, `threshold: 0.1`), volviendo a aparecer con animación de entrada al salir de vista; el observer DEBE desconectarse al desmontar.
      _Spec_: §Lista de espera (Barra) · _Diseño_: misma decisión, con gotcha de `.wrap` · _Tareas_: 9.7
      **Verificación**: al cargar, la barra está fija al pie; al entrar `#demo` desaparece; al salir, reaparece. Nota de testing: jsdom no implementa `IntersectionObserver` — los tests que renderizan la landing necesitan mock (ver `design.md`, gotchas de testing).

- [x] **LISTA-03 — Apilado en móvil (≤900px)**
      A ≤900px la barra DEBE apilarse con el contenido centrado y el botón ocupando todo el ancho.
      _Spec_: §Lista de espera (Barra) · _Tareas_: [SIN TAREA]
      **Verificación**: a 375px, contenido y botón están en columnas apiladas, el botón mide el ancho del contenedor y no hay scroll horizontal.

- [x] **LISTA-04 — Copy de la barra**
      La barra DEBE renderizar su título y texto según el copy versionado.
      _Spec_: §Lista de espera (Barra) · _Tareas_: [SIN TAREA]
      **Verificación**: ambos textos coinciden con los componentes.

- [x] **LISTA-05 — CTA de la barra sin acción en esta spec**
      El CTA de la barra DEBE permanecer visible y habilitado, sin navegación, modal ni envío de datos.
      _Spec_: §Lista de espera (Barra), §Fuera de alcance
      **Verificación**: el botón no está deshabilitado y al hacer clic no cambia ruta/hash ni abre un diálogo.

## Alcance diferido

La modal, el formulario de datos y el envío de correo pertenecen a la spec 003. El prototipo HTML local no es requisito para implementar 002.

## Footer

- [x] **FOOT-01 — Estructura de tres filas**
      El footer DEBE tener tres filas sin navegación interna.
      _Spec_: §Alcance (Footer) · _Diseño_: componente `Footer` · _Tareas_: 3.12 [ambiguo: la tarea no detalla las 3 filas]
      **Verificación**: el footer contiene exactamente tres bloques apilados y ningún link de navegación de la app.

- [x] **FOOT-02 — Fila 1: logo e iconos sociales enlazados**
      La primera fila DEBE mostrar el logo apilado recortado (`Stockma_vertical_trim.png`, 118px) a la izquierda e iconos circulares de WhatsApp e Instagram (componentes de `@mui/icons-material` con `currentColor`) a la derecha, en disposición `space-between`; el icono de WhatsApp DEBE enlazar a `https://w.app/stockmaapp` y el de Instagram a `https://www.instagram.com/stockma_app?dlrf=MXAzNTU1OWl5YzIwZw==`, ambos en nueva pestaña con `rel` seguro.
      _Spec_: §Diseño (Footer), criterio de aceptación · _Tareas_: 3.12, 9.8
      **Verificación**: el logo usa el archivo `_trim` a 118px; ambos iconos son enlaces con las URLs exactas y heredan el color del texto. _(El rediseño reemplazó los SVG inline por iconos de MUI.)_

- [x] **FOOT-03 — Fila 2: soporte con enlace y copyright**
      La segunda fila DEBE mostrar, en disposición `space-between`, la línea de soporte (cuyo ancla apunta a `https://w.app/stockmaapp`) y el copyright. NO DEBE exigirse el número de teléfono visible.
      _Spec_: §Alcance (Footer), §Diseño (Footer), criterio de aceptación · _Tareas_: 3.12
      **Verificación**: el ancla apunta a `w.app/stockmaapp`; ambos textos están presentes; no hay número +57 expuesto.

- [x] **FOOT-04 — Sin navegación interna**
      El footer NO DEBE incluir links a secciones ni a páginas internas de la app.
      _Spec_: §Alcance (Footer) · _Tareas_: 3.12
      **Verificación**: inspección del DOM: los únicos enlaces del footer son WhatsApp e Instagram (más el ancla de soporte a WhatsApp).

- [x] **FOOT-05 — Alineación al eje `.wrap` (gotcha del shorthand)**
      El contenido del footer DEBE alinearse al mismo eje que el resto de la página (24px de padding lateral); cualquier elemento con `class="wrap ..."` DEBE fijar su padding vertical con `padding-block`, nunca con el shorthand `padding`.
      _Spec_: criterio de aceptación · _Diseño_: gotcha de `.wrap` · _Tareas_: 3.12
      **Verificación**: a cualquier ancho, el borde izquierdo del logo del footer alinea con el de las secciones; grep en los estilos del footer: sin shorthand `padding` sobre clases que incluyan `wrap`.

- [x] **FOOT-06 — Costura superior hairline**
      El footer DEBE separarse de su vecina (la barra clara de lista de espera, no la sección demo) con `border-top: 1px solid var(--border)`.
      _Spec_: §Diseño (Footer) · _Tareas_: 3.12
      **Verificación**: el borde superior existe y usa el token `--border`; la sección inmediatamente anterior al footer en el flujo es la barra `#waitlist`.

## Modo oscuro

- [x] **TEMA-01 — Toggle con `data-theme`**
      El control de tema DEBE alternar el atributo `data-theme` entre `light` y `dark` en la raíz, afectando a toda la landing.
      _Spec_: §Diseño (Modo oscuro) · _Tareas_: 1.1, 4.3 · _Diseño_: "Decisión: Extensión del sistema de temas"
      **Verificación**: test RTL de `Header.test.tsx` (alterna `data-theme`, persiste y emite `stockma-theme-change`); al alternar, los colores de secciones, header y barra cambian sin recargar. El override `html[data-theme='dark'] .page` evita que los tokens claros declarados en `.page` pisen el tema oscuro.

- [x] **TEMA-02 — Persistencia en `localStorage`**
      La preferencia DEBE persistirse en la clave `localStorage` `stockma-theme` y restaurarse en la próxima carga.
      _Spec_: §Diseño (Modo oscuro) · _Tareas_: 1.1, 1.2
      **Verificación**: alternar a oscuro, recargar, y verificar `localStorage.getItem('stockma-theme') === 'dark'` y el tema aplicado.

- [x] **TEMA-03 — Anti-FOUC en `index.html`**
      `index.html` DEBE incluir un script que aplique el tema guardado antes del primer render, sin destello del tema opuesto.
      _Spec_: §Diseño (Modo oscuro) · _Tareas_: 1.4
      **Verificación**: con el tema en oscuro y recarga con caché fría, no se ve ningún frame en claro; el script corre antes de cargar el bundle.

- [x] **TEMA-04 — Sincronización con el tema MUI**
      El puente de MUI DEBE recrearse (o escuchar el evento `stockma-theme-change`) al cambiar el tema, para que los componentes MUI de la app actualicen sus colores.
      _Spec_: §Alcance, §Archivos involucrados (`mui-bridge.ts`) · _Tareas_: 2.2, 2.4
      **Verificación**: test de integración o manual: alternar el tema en la landing actualiza los tokens que consume `createMuiBridge()`; los componentes MUI existentes reflejan el modo activo tras navegar a `/login`.

## Estilo global, responsive y copy

- [x] **EST-01 — Tipografía única Inter**
      La landing DEBE usar Inter como única tipografía, cargada desde `index.html`.
      _Spec_: §Contexto, §Diseño · _Tareas_: 1.4
      **Verificación**: `font-family` efectivo resuelve a Inter en todo el árbol de la landing; los links de la fuente están en `index.html`.

- [x] **EST-02 — Bordes coherentes con tokens (10px)**
      Los componentes DEBEN usar los tokens globales de radio (10px); no DEBEN existir radios sueltos ajenos a los tokens.
      _Spec_: §Diseño (Bordes), criterio de aceptación ("8-10px") · _Tareas_: 1.3 [SIN TAREA para tokens de radio]
      **Verificación**: los radios de tarjetas, botones, inputs y modales resuelven a tokens (rango 8-10px); grep sin valores de radio arbitrarios.

- [x] **EST-03 — Botones primarios de la landing con teal de marca**
      Los botones primarios de la landing DEBEN usar `background: var(--brand-teal)`, `color: var(--color-white)` y hover `var(--brand-teal-active)`; NO DEBEN usar `--color-accent` como fondo. El teal de marca da 5.55:1 con texto blanco tanto en claro como en oscuro.
      _Spec_: §Diseño (Botones primarios) · _Diseño_: "Decisión: Los botones primarios usan el teal de marca (`--brand-teal`)" · _Tareas_: [SIN TAREA]
      **Verificación**: grep en estilos de botones: primarios con `--brand-teal`; `--color-accent` reservado para texto/iconos sobre tints. Contraste ≥4.5:1 en ambos temas.

- [x] **EST-04 — Escala tipográfica en pantallas chicas (≤600px)**
      A ≤600px la landing DEBE reducir la escala tipográfica (h1 30px, h2 de sección 26px, lead 15px, cuerpo de tarjetas/pasos 14px) y los botones principales DEBEN ir a ancho completo con altura flexible.
      _Spec_: §Diseño (Pantallas chicas) · _Tareas_: 4.4, 9.10 [ambiguo: la tarea no menciona la escala]
      **Verificación**: a 375px, el `h1` mide 30px, los `h2` de sección 26px, tarjetas/pasos 14px, lead 15px y los CTAs ocupan el ancho disponible. _(Valores actualizados por el rediseño; revisión de código 2026-11-08.)_

- [x] **EST-05 — Sin scroll horizontal en ningún ancho**
      La landing NO DEBE producir scroll horizontal a ≤900px ni ≤600px.
      _Spec_: responsive implícito en §Diseño · _Tareas_: 4.4
      **Verificación**: `document.documentElement.scrollWidth <= innerWidth` a 900px, 600px y 375px.

- [x] **EST-06 — Gate de español neutro**
      El repositorio DEBE pasar `node scripts/check-neutral-es.mjs` sin violaciones de voseo.
      _Spec_: §Contexto, criterio de aceptación · _Tareas_: 4.7
      **Verificación**: el comando termina con código 0; el hook pre-commit lo ejecuta.

- [x] **EST-07 — Copy comercial aprobado como roadmap**
      El copy actual representa capacidades objetivo aprobadas por producto y no evidencia disponibilidad actual; la tarjeta de reportes mantiene el tag de estado diferido.
      _Spec_: §Contexto, §Reglas de copy · _Tareas_: 9.15
      **Verificación**: decisión de producto registrada; el estado de implementación se determina por el código, no por el texto de landing.

- [x] **EST-09 — Paleta neutra slate y densidad de secciones**
      Los neutros de `tokens.css` DEBEN usar la escala slate (`--color-text-primary`, `--color-border`, `--color-tint-accent` al 40%) y la separación vertical de secciones DEBE ser de 50px (mayor densidad pedida en §Contexto).
      _Spec_: §Contexto, §Diseño (Densidad y paleta) · _Tareas_: 9.9, 9.10
      **Verificación**: tests `mui-bridge.test.tsx` y `AuthLayout.test.tsx` comparan los valores actualizados de `tokens.css`.

- [x] **EST-08 — Assets de marca en PNG**
      La landing DEBE importar los logos PNG de `frontend/web/src/assets/` (horizontal, vertical, vertical_trim, logo, name) con los imports de Vite, dimensionados por el artwork visible (los PNG tienen márgenes transparentes grandes), con favicon PNG y sin los SVG de marca antiguos.
      _Spec_: §Contexto (identidad visual), §Archivos involucrados · _Diseño_: fila `assets/` de la tabla de cambios · _Tareas_: 1.5
      **Verificación**: los componentes usan los PNG correctos por superficie (trim en footer, horizontal en header) y alternan las variantes `_dark` en modo oscuro; grep sin imports de los SVG antiguos; el tamaño declarado corresponde al artwork, no al canvas.

---

## Criterios de aceptación del spec → ítems de este checklist

- [x] **CA-01 — Landing accesible en `/` con todas las secciones** → RUTA-01, RUTA-02
- [x] **CA-02 — Nav funcional y responsive (incluye menú móvil)** → HEADER-02, HEADER-03
- [x] **CA-03 — Modo claro/oscuro funcional** → TEMA-01, TEMA-02, TEMA-03, TEMA-04
- [x] **CA-04 — Hero: CTA primario sin acción, CTA secundario lleva a `#how-it-works` y las pestañas del mock cambian de panel** → HERO-03, HERO-04, HERO-05
- [x] **CA-05 — Tarjetas de planes misma altura** → PLAN-03
- [x] **CA-06 — Los 4 botones de planes permanecen activos sin acción en esta spec** → PLAN-05
- [x] **CA-07 — Demo: tarjeta acentuada con CTA único de lista de espera sin acción (sin badge ni botón deshabilitado)** → DEMO-01, DEMO-02, DEMO-04, DEMO-05
- [x] **CA-08 — CTA de lista de espera habilitados sin modal, navegación ni envío** → HERO-03, PLAN-05, DEMO-05, LISTA-05
- [x] **CA-09 — Barra de lista de espera fija al pie, oculta sobre `#demo` y apilada en móvil** → LISTA-01, LISTA-02, LISTA-03
- [x] **CA-10 — Footer alineado al mismo eje que el resto de la página (24px de padding lateral)** → FOOT-05
- [x] **CA-11 — Sin voseo (pasa gate)** → EST-06
- [x] **CA-12 — Copy comercial conservado como roadmap aprobado; la tarjeta de reportes mantiene el tag diferido** → FUNC-03, EST-07, COMP-04
- [x] **CA-13 — Bordes coherentes 8-10px** → EST-02
- [x] **CA-14 — Footer: logo, iconos de WhatsApp e Instagram con enlace, línea de soporte con enlace a WhatsApp y copyright** → FOOT-01, FOOT-02, FOOT-03, FOOT-04
- [x] **CA-15 — Copy versionado gobernado por componentes; prototipo local no normativo ni requerido en Git** → FUNC-03, EST-07, COMP-04
- [x] **CA-16 — Hero: segundo segmento rotativo (3 frases, fundido) respetando `prefers-reduced-motion`** → HERO-01
- [x] **CA-17 — `#audience` con 4 tarjetas de segmento** → QUIEN-01
