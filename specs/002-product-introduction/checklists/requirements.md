# Checklist de Requerimientos: Product Introduction Landing

**Feature branch**: `002-product-introduction`
**Fuente**: [`../spec.md`](../spec.md) — la spec 002 no usa IDs FR-xxx; cada ítem usa un ID propio trazable a la sección del spec. El prototipo [`../Stockma_landing.html`](../Stockma_landing.html) es la **fuente de verdad del copy**.
**Referencias**: [`../design.md`](../design.md) · tasks (artefacto en Engram, `sdd/002-product-introduction/tasks`; las referencias a tareas son tentativas mientras se regenera) · [`../Stockma_landing.html`](../Stockma_landing.html) (prototipo de referencia vivo)

Marcar un ítem sólo cuando el comportamiento esté implementado **y** cubierto por al menos un test verde o una verificación manual registrada. La marca `[SIN TAREA]` indica que el comportamiento no tiene tarea correspondiente en el artefacto tasks actual.

---

## Ruta y estructura

- [ ] **RUTA-01 — Switch de ruta sin React Router**
      `App.tsx` DEBE decidir por `window.location.pathname`: `/login` renderiza `AuthFlow`; cualquier otra ruta renderiza `LandingPage`.
      *Spec*: §Alcance (último punto) · *Tareas*: 2.1, 2.3 · *Diseño*: "Decisión: Cambio de ruta sin React Router"
      **Verificación**: `/` muestra la landing; `/login` muestra el flujo de autenticación actual; una ruta desconocida (p. ej. `/foo`) muestra la landing. Cubierto por test RTL en `App.test.tsx`.

- [ ] **RUTA-02 — Landing con las 11 secciones en orden**
      La landing DEBE renderizar, en este orden: Header, Hero, Para quién es (`#audience`), Cómo funciona (`#how-it-works`), Funcionalidades (`#features`), Comparativa (`#comparison`), Planes (`#pricing`), Preguntas frecuentes (`#faq`), Agenda tu demo (`#demo`), Barra de lista de espera (`#waitlist`, último hijo de `<main>`), Footer.
      *Spec*: §Estructura de la landing · *Tareas*: 3.3 [SIN TAREA para WaitlistSection y WhoItsFor]
      **Verificación**: cada `id` existe exactamente una vez en el DOM y el orden visual coincide con el spec.

## Header

- [ ] **HEADER-01 — Barra de navegación superior sin CTA de login**
      El header DEBE contener el logo (enlace a `/`), los links de navegación y el control de cambio de tema; NO DEBE contener CTA ni botón de login.
      *Spec*: §Alcance, punto 2 · *Tareas*: 3.4
      **Verificación**: logo, links y toggle presentes; grep en el header: sin botón "Iniciar sesión" ni enlace de autenticación.

- [ ] **HEADER-02 — Links de navegación**
      Los links DEBEN apuntar, en este orden, a: Producto (`#features`), Cómo funciona (`#how-it-works`), Planes (`#pricing`) y Agendar demo (`#demo`).
      *Spec*: §Alcance, punto 2 · *Tareas*: 3.4
      **Verificación**: cada link navega a su ancla por scroll; los textos y targets coinciden con el prototipo.

- [ ] **HEADER-03 — Menú móvil (≤900px)**
      El header DEBE colapsar en un menú desplegable en pantallas de ancho ≤900px, con los mismos links que la navegación de escritorio.
      *Spec*: §Alcance, punto 2 · *Tareas*: 3.4, 4.4
      **Verificación**: a 375px los links de escritorio no están visibles; el botón hamburguesa alterna el panel con `aria-expanded`; al elegir un link el menú se cierra.

- [ ] **HEADER-04 — Tratamiento visual translúcido**
      El header DEBE usar fondo translúcido (`color-mix`), `backdrop-filter` (blur) y borde interior; la barra de lista de espera replica este tratamiento.
      *Spec*: §Lista de espera (Barra) · *Tareas*: 3.4
      **Verificación**: al hacer scroll, el contenido se ve difuminado bajo el header; header y barra `#waitlist` comparten la misma receta visual.

## Hero

- [ ] **HERO-01 — Título H1 con itálica**
      El hero DEBE mostrar el H1 "Automatiza tu inventario y *deja las planillas atrás*", con la segunda parte en itálica.
      *Spec*: §Hero · *Tareas*: 3.5
      **Verificación**: el `h1` contiene exactamente "Automatiza tu inventario y" más un elemento en itálica (p. ej. `<em>`) con "deja las planillas atrás".

- [ ] **HERO-02 — Subtítulo**
      El hero DEBE mostrar el subtítulo "Stockma es la plataforma de inventarios ideal para tu negocio: cada lote con su vencimiento, cada producto con su código, y el trabajo repetitivo en manos del sistema, no en las tuyas."
      *Spec*: §Hero · *Tareas*: 3.5
      **Verificación**: el texto del subtítulo coincide literalmente con el spec (copy del prototipo).

- [ ] **HERO-03 — CTA primario sin acción en esta spec**
      El CTA primario "Únete a la lista de espera" DEBE permanecer visible y habilitado, sin acción asociada en este cambio.
      *Spec*: §Hero, §Alcance · *Tareas*: 3.5, 4.6
      **Verificación**: el botón no está deshabilitado; el clic no abre un diálogo ni cambia ruta o hash.

- [ ] **HERO-04 — CTA secundario**
      El CTA secundario "Ver cómo funciona" DEBE ser un enlace a `#how-it-works`.
      *Spec*: §Hero · *Tareas*: 3.5, 4.6
      **Verificación**: el enlace tiene `href="#how-it-works"` y el clic hace scroll a la sección.

- [ ] **HERO-05 — Mock visual de tabla interactivo**
      La columna derecha del hero DEBE mostrar el mock decorativo pero interactivo: leyenda "Modelo de referencia. Así se ve tu inventario: cada producto con sus lotes, cantidades y fecha de vencimiento."; encabezado "Inventario · Tu negocio" con el pill "3 lotes por vencer"; un `tablist` conmutable con las pestañas "Productos" (activa por defecto), "Lotes" y "Por vencer", cada una con 3 filas de ejemplo. En "Por vencer", los chips de días son "21 días" (normal), "9 días" (warn) y "3 días" (danger).
      *Spec*: §Hero (Mock visual de tabla) · *Tareas*: 3.5 [SIN TAREA para la conmutación de pestañas]
      **Verificación**: los tres textos de cabecera coinciden; al hacer clic en cada pestaña cambia el panel y `aria-selected` refleja la activa; los chips de "Por vencer" muestran 21/9/3 días con sus estados cromáticos. Test unitario de la lógica de conmutación (design: estrategia de testing, fila "Unit").

## Para quién es Stockma

- [ ] **QUIEN-01 — Sección de segmentos objetivo**
      La sección `#audience` DEBE presentar tres tarjetas de segmento: "Droguerías", "Minimercados" y "Distribuidoras", con su descripción.
      *Spec*: §Alcance, punto 3 · *Diseño*: componente `WhoItsFor` · *Tareas*: [SIN TAREA]
      **Verificación**: la sección existe con `id="audience"` y renderiza las tres tarjetas con título y texto según el prototipo.

## Cómo funciona

- [ ] **COMO-01 — Título y subtítulo**
      La sección `#how-it-works` DEBE usar el título "De cero a tu inventario cargado" y el subtítulo "Tres pasos y tu equipo ya está trabajando con datos reales."
      *Spec*: §Cómo funciona · *Tareas*: 3.6
      **Verificación**: `h2` y subtítulo coinciden literalmente con el spec.

- [ ] **COMO-02 — Tres pasos con copy exacto**
      La sección DEBE mostrar exactamente 3 pasos: 1) "Recibe tus credenciales" — "Un administrador registra tu negocio en Stockma y te entrega el acceso."; 2) "Inicia sesión y confirma con SMS" — "Correo, contraseña y un código que llega a tu celular. Blindaje extra de seguridad para que unicamente tus colaboradores tengan acceso a tu negocio."; 3) "Carga productos, lotes y fechas" — "Con su nombre y su código de barras, cada producto queda con su lote y su vencimiento."
      *Spec*: §Cómo funciona · *Tareas*: 3.6
      **Verificación**: el contenedor tiene 3 artículos numerados 1-3 con esos títulos y descripciones; no hay cuarto paso.

## Funcionalidades principales

- [ ] **FUNC-01 — Seis tarjetas en grid 3x2 con subtítulo**
      La sección `#features` DEBE mostrar el subtítulo "Tu stock centralizado, con menos trabajo manual y mayor control sobre cada uno de tus productos." y 6 tarjetas en cuadrícula de 3 columnas por 2 filas en escritorio.
      *Spec*: §Funcionalidades principales · *Tareas*: 3.7
      **Verificación**: a ≥1024px la cuadrícula tiene 3 columnas; 6 tarjetas presentes; subtítulo coincide.

- [ ] **FUNC-02 — Títulos exactos de las 6 tarjetas**
      Los títulos DEBEN ser, en orden: "Todo tu stock en un solo lugar", "Los vencimientos se marcan solos", "El producto aparece al pasar el lector", "Todo tu equipo trabaja al mismo tiempo", "Planes según lo que necesites" (cuyo texto menciona "8 días de prueba gratuita") y "Reportes de inventario a tu medida".
      *Spec*: §Funcionalidades principales · *Tareas*: 3.7
      **Verificación**: los 6 `h3` coinciden literalmente con el spec; la tarjeta de planes menciona la prueba gratuita de 8 días.

- [ ] **FUNC-03 — Tag "Próximamente" sólo en reportes**
      Únicamente la tarjeta "Reportes de inventario a tu medida" DEBE llevar el tag "Próximamente"; ninguna otra tarjeta lo lleva.
      *Spec*: §Funcionalidades principales, criterio de aceptación · *Tareas*: 3.7, 4.8
      **Verificación**: grep de la sección: exactamente un tag "Próximamente", dentro de la tarjeta de reportes.

## Comparativa

- [ ] **COMP-01 — Título y subtítulo**
      La sección `#comparison` DEBE usar el título "De la hoja y el lápiz a un software que hace ese trabajo por ti" y el subtítulo "Cansado de los procesos manuales, rutinarios, y agotadores que te quitan tiempo para realizar tus actividades extralaborales? Con Stockma puedes tener un software automatizado que te evita esos dolores de cabeza y hace todos los trabajos manuales por ti".
      *Spec*: §Comparativa · *Tareas*: 3.8
      **Verificación**: `h2` y subtítulo coinciden literalmente con el spec (copy del prototipo).

- [ ] **COMP-02 — Columna "Sin Stockma"**
      La columna izquierda DEBE titular "Sin Stockma: Procesos manuales y pérdidas invisibles", con la intro "Terminas la jornada pero tu trabajo no acaba: te toca reacomodar inventario, adivinar qué lotes están por expirar y asumir pérdidas de productos que se vencieron sin que te dieras cuenta." y 5 bullets en negrita con descripción: Descontrol de registros, Revisiones tediosas, Pérdida de inversión, Rotación deficiente, Cierres de caja propensos al error.
      *Spec*: §Comparativa, columna izquierda · *Tareas*: 3.8
      **Verificación**: título, intro y los 5 bullets presentes con sus descripciones según el prototipo.

- [ ] **COMP-03 — Columna "Con Stockma"**
      La columna derecha DEBE titular "Con Stockma: Cero sorpresas, tienes el control total de tu negocio", con la intro "Automatiza tu operación diaria, elimina los errores manuales y protege cada peso de tu inversión." y 6 bullets en negrita con descripción: Inventario en tiempo real, Semaforización inteligente, Trazabilidad FEFO, Captura rápida con IA, Auditorías a 1 clic, Optimización de tiempo.
      *Spec*: §Comparativa, columna derecha · *Tareas*: 3.8
      **Verificación**: título, intro y los 6 bullets presentes con sus descripciones según el prototipo.

- [ ] **COMP-04 — Copy de "Con Stockma" replicado tal cual (tenencia registrada)**
      El copy de la columna "Con Stockma" DEBE replicarse literalmente del prototipo pese a mencionar IA, FEFO, auditorías y reportes; la tensión con §Contexto está registrada en el spec para revisión con producto y NO debe "suavizarse" en la implementación por cuenta propia.
      *Spec*: §Comparativa (Nota de copy) · *Tareas*: 4.8
      **Verificación**: el texto renderizado es idéntico al del prototipo; si producto decide ajustarlo, el cambio se hace primero en el spec y luego en el código.

## Planes

- [ ] **PLAN-01 — Título y subtítulo**
      La sección `#pricing` DEBE usar el título "Elige tu plan" y el subtítulo "Los primeros 8 días son gratis. Después eliges el plan que te sirva."
      *Spec*: §Planes · *Tareas*: 3.9
      **Verificación**: `h2` y subtítulo coinciden literalmente con el spec.

- [ ] **PLAN-02 — Cuatro planes con copy exacto**
      DEBEN mostrarse los 4 planes: **Básico** — $ 49.000 / mes — "Para dejar el cuaderno sin complicarte." — bullets: productos, lotes y vencimientos / semáforo de vencimientos / 1 usuario — botón "Empezar prueba" (secundario); **Profesional** — $ 89.000 / mes — "Para un equipo que trabaja sobre el mismo inventario." — bullets: todo lo del Básico / hasta 5 usuarios / lector de códigos de barras / reportes para el contador (próximamente) — botón "Empezar prueba" (primario); **Negocio** — $ 149.000 / mes — "Para negocios con más movimiento y más gente." — bullets: todo lo del Profesional / usuarios sin límite / acompañamiento en la carga inicial — botón "Hablar con ventas" (secundario); **A tu medida** — "Bajo cotización" — "Si tu operación pide algo que no está en los planes, lo armamos contigo." — bullets: las funciones que necesites / módulos hechos para tu negocio / acompañamiento dedicado — botón "Cotizar mi plan" (secundario).
      *Spec*: §Planes · *Tareas*: 3.9 · *Diseño*: interfaz `Plan`
      **Verificación**: nombres, precios, descripciones, bullets y labels de botones coinciden con el spec; los precios son placeholders sin pasarela.

- [ ] **PLAN-03 — Altura uniforme**
      Las 4 tarjetas DEBEN tener la misma altura con el botón alineado al pie (`grid-auto-rows: 1fr`, `align-items: stretch`, `margin-top: auto` en el botón).
      *Spec*: §Planes · *Tareas*: 3.9
      **Verificación**: con descripciones y bullets de distinta longitud, las 4 tarjetas miden lo mismo y los botones quedan a la misma altura.

- [ ] **PLAN-04 — Plan "A tu medida" con borde punteado**
      La tarjeta "A tu medida" DEBE usar borde punteado y el estilo diferenciado del prototipo.
      *Spec*: §Planes · *Tareas*: 3.9
      **Verificación**: la cuarta tarjeta tiene borde discontinuo y muestra "Bajo cotización" en lugar de precio numérico.

- [ ] **PLAN-05 — CTA de planes sin acción en esta spec**
      Los 4 botones de los planes DEBEN permanecer visibles y habilitados, sin abrir modal, navegar o enviar datos.
      *Spec*: §Planes, §Fuera de alcance · *Tareas*: 3.9
      **Verificación**: cada botón está habilitado; el clic no abre diálogo ni cambia ruta o hash.

- [ ] **PLAN-06 — Tarjetas de plan seleccionables**
      Las tarjetas de plan DEBEN ser seleccionables: clic, Enter o Espacio marcan la tarjeta con la clase `is-selected` y actualizan `aria-pressed`.
      *Spec*: §Planes (último párrafo) · *Tareas*: [SIN TAREA]
      **Verificación**: test de teclado y puntero: la tarjeta activa queda en `is-selected`, `aria-pressed="true"`, y al seleccionar otra se desmarca la anterior.

## Preguntas frecuentes

- [ ] **FAQ-01 — Acordeón de preguntas**
      La sección `#faq` DEBE mostrar un acordeón con las preguntas del prototipo (fuente de verdad del copy), p. ej. "¿Cuánto cuesta?" con la respuesta "Hay planes según lo que necesites. Los primeros 8 días son gratis."
      *Spec*: §Alcance, punto 9 · *Tareas*: 3.11
      **Verificación**: la sección existe con `id="faq"`; al abrir una pregunta se muestra su respuesta y al cerrarla se oculta; el copy coincide con el prototipo.

## Agenda tu demo

- [ ] **DEMO-01 — Apartado `#demo` con tarjeta acentuada**
      El apartado `#demo` DEBE mostrar una tarjeta con fondo acentuado y bordes de 10px.
      *Spec*: §Agenda tu demo · *Tareas*: 3.10
      **Verificación**: la tarjeta usa el fondo acentuado definido y `border-radius` de 10px.

- [ ] **DEMO-02 — Badge "Próximamente"**
      La tarjeta DEBE mostrar el badge "Próximamente".
      *Spec*: §Agenda tu demo, criterio de aceptación · *Tareas*: 3.10
      **Verificación**: el badge es visible dentro de la tarjeta, antes del título.

- [ ] **DEMO-03 — Título y descripción**
      DEBE mostrar el título "Agenda tu demo" y la descripción "Estamos terminando de construir Stockma y todavía no podemos mostrarte el producto funcionando. Cuando esté listo vas a poder agendar 30 minutos por videollamada: vemos tu inventario real, cargamos un par de productos y revisamos el plan que mejor se ajusta a tu negocio."
      *Spec*: §Agenda tu demo · *Tareas*: 3.10
      **Verificación**: copy exacto del spec (copy del prototipo).

- [ ] **DEMO-04 — Botón "Agendar demo" deshabilitado**
      El botón 1 "Agendar demo" DEBE estar deshabilitado (`disabled`) y sin acción asociada.
      *Spec*: §Agenda tu demo, criterio de aceptación · *Tareas*: 3.10
      **Verificación**: el atributo `disabled` está presente; el clic no abre nada ni navega.

- [ ] **DEMO-05 — CTA secundario sin acción en esta spec**
      El botón 2 "Mientras tanto, únete a la lista de espera" DEBE permanecer visible y habilitado, sin acción asociada.
      *Spec*: §Agenda tu demo, §Fuera de alcance · *Tareas*: 3.10
      **Verificación**: el botón está habilitado; el clic no abre diálogo ni cambia ruta o hash.

## Lista de espera — barra sticky

- [ ] **LISTA-01 — Barra sticky, último hijo de `<main>`**
      La barra `#waitlist` DEBE ser el último hijo de `<main>` con `position: sticky; bottom: 0`, de ancho completo (full-bleed), con el mismo tratamiento visual del header; su contenido va dentro de `.wrap`.
      *Spec*: §Lista de espera (Barra), §Estructura (paso 10) · *Diseño*: "Decisión: CTA de lista de espera como barra inferior sticky" · *Tareas*: [SIN TAREA]
      **Verificación**: en el DOM, `#waitlist` es el último hijo de `<main>`; la barra toca los bordes izquierdo y derecho del viewport; su copy está alineado al mismo eje `.wrap` que el resto de secciones.

- [ ] **LISTA-02 — Visibilidad desde la carga y liberación con el footer**
      La barra DEBE estar visible desde que carga la página y liberarse únicamente cuando el footer entra en pantalla, sin `fixed` ni `IntersectionObserver`.
      *Spec*: §Lista de espera (Barra) · *Diseño*: mismo, con gotcha de `.wrap`
      **Verificación**: al cargar, la barra está pegada al pie; al hacer scroll hasta el final, el footer la empuja y queda en su posición de flujo; no hay `position: fixed` ni observadores en el código.

- [ ] **LISTA-03 — Apilado en móvil (≤900px)**
      A ≤900px la barra DEBE apilarse con el texto centrado y el botón ocupando todo el ancho.
      *Spec*: §Lista de espera (Barra) · *Tareas*: [SIN TAREA]
      **Verificación**: a 375px, texto y botón están en columnas apiladas, el botón mide el ancho del contenedor y no hay scroll horizontal.

      La barra DEBE mostrar el título "Únete a la lista de espera" y el texto "Rellena el formulario y recibe en primicia información de nuestro lanzamiento".
      *Spec*: §Lista de espera (Barra) · *Tareas*: [SIN TAREA]
      **Verificación**: ambos textos coinciden exactamente con el spec.

- [ ] **LISTA-05 — CTA de la barra sin acción en esta spec**
        El CTA "Quiero entrar" DEBE permanecer visible y habilitado, sin navegación, modal ni envío de datos.
        *Spec*: §Lista de espera (Barra), §Fuera de alcance
        **Verificación**: el botón no está deshabilitado y al hacer clic no cambia ruta/hash ni abre un diálogo.

## Alcance diferido

La modal, el formulario de datos y el guardado en backend pertenecen a una futura spec. El prototipo HTML conserva su implementación como referencia visual; no se exige reproducirla en 002.

## Footer

- [ ] **FOOT-01 — Estructura de tres filas**
      El footer DEBE tener tres filas sin navegación interna.
      *Spec*: §Alcance (Footer) · *Diseño*: componente `Footer` · *Tareas*: 3.12 [ambiguo: la tarea no detalla las 3 filas]
      **Verificación**: el footer contiene exactamente tres bloques apilados y ningún link de navegación de la app.

- [ ] **FOOT-02 — Fila 1: logo e iconos sociales enlazados**
      La primera fila DEBE mostrar el logo apilado recortado (`Stockma_vertical_trim.png`, 118px) a la izquierda e iconos circulares de WhatsApp e Instagram (SVG inline con `currentColor`) a la derecha, en disposición `space-between`; el icono de WhatsApp DEBE enlazar a `https://w.app/stockmaapp` y el de Instagram a `https://www.instagram.com/stockma_app?dlrf=MXAzNTU1OWl5YzIwZw==`, ambos en nueva pestaña con `rel` seguro.
      *Spec*: §Diseño (Footer), criterio de aceptación · *Tareas*: 3.12
      **Verificación**: el logo usa el archivo `_trim` a 118px; ambos iconos son enlaces con las URLs exactas y heredan el color del texto.

- [ ] **FOOT-03 — Fila 2: soporte con enlace y copyright**
      La segunda fila DEBE mostrar, en disposición `space-between`, la línea "Atención, preguntas frecuentes y soporte: Escribenos al WhatsApp" (el ancla "Escribenos al WhatsApp" enlaza a `https://w.app/stockmaapp`) y el copyright "© 2026 Stockma" / "Hecho en Colombia". NO DEBE exigirse el número de teléfono visible.
      *Spec*: §Alcance (Footer), §Diseño (Footer), criterio de aceptación · *Tareas*: 3.12
      **Verificación**: el ancla apunta a `w.app/stockmaapp`; ambos textos presentes; no hay número +57 expuesto.

- [ ] **FOOT-04 — Sin navegación interna**
      El footer NO DEBE incluir links a secciones ni a páginas internas de la app.
      *Spec*: §Alcance (Footer) · *Tareas*: 3.12
      **Verificación**: inspección del DOM: los únicos enlaces del footer son WhatsApp e Instagram (más el ancla de soporte a WhatsApp).

- [ ] **FOOT-05 — Alineación al eje `.wrap` (gotcha del shorthand)**
      El contenido del footer DEBE alinearse al mismo eje que el resto de la página (24px de padding lateral); cualquier elemento con `class="wrap ..."` DEBE fijar su padding vertical con `padding-block`, nunca con el shorthand `padding`.
      *Spec*: criterio de aceptación · *Diseño*: gotcha de `.wrap` · *Tareas*: 3.12
      **Verificación**: a cualquier ancho, el borde izquierdo del logo del footer alinea con el de las secciones; grep en los estilos del footer: sin shorthand `padding` sobre clases que incluyan `wrap`.

- [ ] **FOOT-06 — Costura superior hairline**
      El footer DEBE separarse de su vecina (la barra clara de lista de espera, no la sección demo) con `border-top: 1px solid var(--border)`.
      *Spec*: §Diseño (Footer) · *Tareas*: 3.12
      **Verificación**: el borde superior existe y usa el token `--border`; la sección inmediatamente anterior al footer en el flujo es la barra `#waitlist`.

## Modo oscuro

- [ ] **TEMA-01 — Toggle con `data-theme`**
      El control de tema DEBE alternar el atributo `data-theme` entre `light` y `dark` en la raíz, afectando a toda la landing.
      *Spec*: §Diseño (Modo oscuro) · *Tareas*: 1.1, 4.3 · *Diseño*: "Decisión: Extensión del sistema de temas"
      **Verificación**: test unitario de la lógica de `theme-mode.ts`; al alternar, los colores de secciones, header y barra cambian sin recargar.

- [ ] **TEMA-02 — Persistencia en `localStorage`**
      La preferencia DEBE persistirse en la clave `localStorage` `stockma-theme` y restaurarse en la próxima carga.
      *Spec*: §Diseño (Modo oscuro) · *Tareas*: 1.1, 1.2
      **Verificación**: alternar a oscuro, recargar, y verificar `localStorage.getItem('stockma-theme') === 'dark'` y el tema aplicado. La clave coincide con el prototipo.

- [ ] **TEMA-03 — Anti-FOUC en `index.html`**
      `index.html` DEBE incluir un script que aplique el tema guardado antes del primer render, sin destello del tema opuesto.
      *Spec*: §Diseño (Modo oscuro) · *Tareas*: 1.4
      **Verificación**: con el tema en oscuro y recarga con caché fría, no se ve ningún frame en claro; el script corre antes de cargar el bundle.

- [ ] **TEMA-04 — Sincronización con el tema MUI**
      El puente de MUI DEBE recrearse (o escuchar el evento `stockma-theme-change`) al cambiar el tema, para que los componentes MUI de la app actualicen sus colores.
      *Spec*: §Alcance, §Archivos involucrados (`mui-bridge.ts`) · *Tareas*: 2.2, 2.4
      **Verificación**: test de integración o manual: alternar el tema en la landing actualiza los tokens que consume `createMuiBridge()`; los componentes MUI existentes reflejan el modo activo tras navegar a `/login`.

## Estilo global, responsive y copy

- [ ] **EST-01 — Tipografía única Work Sans**
      La landing DEBE usar Work Sans como única tipografía, cargada desde `index.html`.
      *Spec*: §Contexto, §Diseño · *Tareas*: 1.4
      **Verificación**: `font-family` efectivo resuelve a Work Sans en todo el árbol de la landing; los links de la fuente están en `index.html`.

- [ ] **EST-02 — Bordes coherentes con tokens (10px)**
      Los componentes DEBEN usar los tokens globales de radio (10px); no DEBEN existir radios sueltos ajenos a los tokens.
      *Spec*: §Diseño (Bordes), criterio de aceptación ("8-10px") · *Tareas*: 1.3 [SIN TAREA para tokens de radio]
      **Verificación**: los radios de tarjetas, botones, inputs y modales resuelven a tokens (rango 8-10px); grep sin valores de radio arbitrarios.

- [ ] **EST-03 — Botones primarios con tokens de acción (nunca `--accent`)**
      Los botones primarios DEBEN usar `background: var(--primary)`, `color: var(--on-primary)` y hover `var(--primary-active)`; NO DEBEN usar `--accent` como fondo (2.33:1 con texto blanco en modo oscuro, contra los 4.5:1 de WCAG AA).
      *Spec*: §Diseño (Botones primarios) · *Diseño*: "Decisión: Los botones primarios usan los tokens de la app al implementar" · *Tareas*: [SIN TAREA]
      **Verificación**: grep en estilos de botones: sin `--accent` como `background` de primarios; contraste medido ≥4.5:1 en claro y oscuro. El teal de marca se mantiene sólo como acento.

- [ ] **EST-04 — Escala tipográfica en pantallas chicas (≤600px)**
      A ≤600px la landing DEBE reducir la escala tipográfica (h1 30px, h2 de sección 25px, cuerpo 14px) y los botones principales DEBEN ir a ancho completo con altura flexible.
      *Spec*: §Diseño (Pantallas chicas) · *Tareas*: 4.4 [ambiguo: la tarea no menciona la escala]
      **Verificación**: a 375px, el `h1` mide 30px, los `h2` de sección 25px, el cuerpo 14px y los CTAs ocupan el ancho disponible.

- [ ] **EST-05 — Sin scroll horizontal en ningún ancho**
      La landing NO DEBE producir scroll horizontal a ≤900px ni ≤600px.
      *Spec*: responsive implícito en §Diseño · *Tareas*: 4.4
      **Verificación**: `document.documentElement.scrollWidth <= innerWidth` a 900px, 600px y 375px.

- [ ] **EST-06 — Gate de español neutro**
      El repositorio DEBE pasar `node scripts/check-neutral-es.mjs` sin violaciones de voseo.
      *Spec*: §Contexto, criterio de aceptación · *Tareas*: 4.7
      **Verificación**: el comando termina con código 0; el hook pre-commit lo ejecuta.

- [ ] **EST-07 — Copy: capacidades reales y tags "Próximamente"**
      Todo el copy DEBE describir capacidades verificadas en el código base; las capacidades futuras que se mencionen (reportes) DEBEN ir siempre marcadas como "Próximamente". La única excepción consentida es el copy de la columna "Con Stockma" (ver COMP-04).
      *Spec*: §Contexto, §Reglas de copy, criterio de aceptación · *Tareas*: 4.8
      **Verificación**: revisión de copy de todas las secciones; grep de "reportes"/"Próximamente": toda mención de reportes lleva su tag.

- [ ] **EST-08 — Assets de marca en PNG**
      La landing DEBE importar los logos PNG de `frontend/web/src/assets/` (horizontal, vertical, vertical_trim, logo, name) con los imports de Vite, dimensionados por el artwork visible (los PNG tienen márgenes transparentes grandes), con favicon PNG y sin los SVG de marca antiguos.
      *Spec*: §Contexto (identidad visual), §Archivos involucrados · *Diseño*: fila `assets/` de la tabla de cambios · *Tareas*: 1.5 [contradictoria: habla de "theme variants"; el design prohíbe recolorear PNGs]
      **Verificación**: los componentes usan los PNG correctos por superficie (trim en footer, horizontal en header); grep sin imports de los SVG antiguos; el tamaño declarado corresponde al artwork, no al canvas.

---

## Criterios de aceptación del spec → ítems de este checklist

- [ ] **CA-01 — Landing accesible en `/` con todas las secciones** → RUTA-01, RUTA-02
- [ ] **CA-02 — Nav funcional y responsive (incluye menú móvil)** → HEADER-02, HEADER-03
- [ ] **CA-03 — Modo claro/oscuro funcional** → TEMA-01, TEMA-02, TEMA-03, TEMA-04
- [ ] **CA-04 — Hero: CTA primario sin acción, CTA secundario lleva a `#how-it-works` y las pestañas del mock cambian de panel** → HERO-03, HERO-04, HERO-05
- [ ] **CA-05 — Tarjetas de planes misma altura** → PLAN-03
- [ ] **CA-06 — Los 4 botones de planes permanecen activos sin acción en esta spec** → PLAN-05
- [ ] **CA-07 — Demo: badge "Próximamente", botón principal deshabilitado y CTA de lista de espera sin acción** → DEMO-02, DEMO-04, DEMO-05
- [ ] **CA-08 — CTA de lista de espera habilitados sin modal, navegación ni envío** → HERO-03, PLAN-05, DEMO-05, LISTA-05
- [ ] **CA-09 — Barra de lista de espera pegada al pie y de ancho completo; apilada en móvil** → LISTA-01, LISTA-02, LISTA-03
- [ ] **CA-10 — Footer alineado al mismo eje que el resto de la página (24px de padding lateral)** → FOOT-05
- [ ] **CA-11 — Sin voseo (pasa gate)** → EST-06
- [ ] **CA-12 — Sin promesas de funcionalidades no implementadas (los reportes van siempre marcados como "Próximamente")** → FUNC-03, EST-07
- [ ] **CA-13 — Bordes coherentes 8-10px** → EST-02
- [ ] **CA-14 — Footer: logo, iconos de WhatsApp e Instagram con enlace, línea de soporte con enlace a WhatsApp y copyright** → FOOT-01, FOOT-02, FOOT-03, FOOT-04
