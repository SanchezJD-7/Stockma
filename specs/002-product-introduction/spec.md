# Spec 002 - Product Introduction Landing

## Objetivo
Crear una pantalla de introducción (landing) en `/` para Stockma, con navegación superior, modo oscuro, responsive y coherente con la identidad visual de la app.

## Contexto
- Basado en el prototipo `specs/002-product-introduction/Stockma_landing.html` (fuente de verdad del copy).
- Tipografía única: **Work Sans**.
- Sin voseo (gate `scripts/check-neutral-es.mjs`).
- Solo prometer capacidades reales verificadas en el código base. Nada de alertas por correo/in-app, tiempo real, IA, auditoría, reportes, suscripciones/autoregistro, FEFO como promesa.
- Inspiración visual: pagaldia.com, pero con mayor densidad (se pidió corregir el aspecto "vacío").
- Identidad visual actualizada: logos en formato PNG con transparencia (`Stockma_logo.png`, `Stockma_vertical.png`, `Stockma_horizontal.png`, `Stockma_name.png`) en `frontend/web/src/assets/`. Eliminados los SVG de marca antiguos. Favicon actualizado a PNG.

## Alcance
- Landing pública en `/`.
- Navegación superior con logo, links a secciones (Producto, Cómo funciona, Planes, Agendar demo), menú móvil y cambio de tema. Sin CTA de login.
- Hero con CTA primario "Únete a la lista de espera" (abre la modal), CTA secundario "Ver cómo funciona" y mock visual interactivo de tabla de inventario con pestañas.
- Sección "Para quién es Stockma" (`#para-quien-es`, segmentos objetivo: Droguerías, Minimercados, Distribuidoras).
- Sección "Cómo funciona" (3 pasos).
- Sección "Funcionalidades principales" (6 tarjetas en grid 3x2; la de reportes lleva el tag "Próximamente").
- Barra de lista de espera (`#lista-espera`) pegada al pie de la pantalla, de ancho completo, con CTA que abre la modal.
- Sección "De la hoja y el lápiz a un software que hace ese trabajo por ti" (comparativa).
- Sección "Elige tu plan" (4 planes con altura uniforme; los 4 botones abren la modal de lista de espera).
- Sección "Agenda tu demo" (badge "Próximamente", botón "Agendar demo" deshabilitado y CTA a la lista de espera).
- Sección "Preguntas frecuentes" (acordeón).
- Footer en tres filas: logo + iconos de WhatsApp e Instagram (`space-between`), línea de soporte con el enlace "Escribenos al WhatsApp" y copyright ("© 2026 Stockma" / "Hecho en Colombia", también `space-between`). Sin navegación interna.
- Modal de lista de espera (maqueta, sin backend): nombre, teléfono con prefijo +57 y correo.
- Modo oscuro con `data-theme`, `localStorage` `stockma-theme`, anti-FOUC y actualización del tema MUI.
- Sin React Router (usar switch por `window.location.pathname` en `App.tsx`: `/login` -> `AuthFlow`, resto -> `LandingPage`). Diferido a T058.

## Fuera de alcance
- Modal/CTA de login en la landing y autenticación desde la landing (no existe en el prototipo actual; posible change futuro).
- Chat de demo con redirección a Cal.com (no existe en el prototipo actual; posible change futuro).
- Agenda propia / backend de demo (no existe en el prototipo actual; posible change futuro).
- Precios cobrables / pasarela (valores son placeholders).
- Nuevas funcionalidades del producto (solo presentación).
- Router SPA completo.

## Estructura de la landing
1. Header (logo, nav, tema, menú móvil)
2. Hero (H1, subtítulo, 2 CTAs, mock visual de tabla)
3. Para quién es Stockma (`#para-quien-es`)
4. Cómo funciona (3 pasos, `#como-funciona`)
5. Funcionalidades principales (6 tarjetas, `#producto`)
6. Comparativa manual vs Stockma (`#comparativa`)
7. Planes (`#planes`)
8. Preguntas frecuentes (`#preguntas`)
9. Agenda tu demo (`#agendar`)
10. Barra de lista de espera pegada al pie (`#lista-espera`, último hijo de `<main>`)
11. Footer

## Hero
- **H1:** "Automatiza tu inventario y *deja las planillas atrás*" (la segunda parte va en itálica)
- **Subtítulo:** "Stockma es la plataforma de inventarios ideal para tu negocio: cada lote con su vencimiento, cada producto con su código, y el trabajo repetitivo en manos del sistema, no en las tuyas."
- **CTA primario:** botón "Únete a la lista de espera" -> abre la modal de lista de espera.
- **CTA secundario:** enlace "Ver cómo funciona" -> `#como-funciona`.
- **Mock visual de tabla** (columna derecha, decorativo pero interactivo): leyenda "Modelo de referencia. Así se ve tu inventario: cada producto con sus lotes, cantidades y fecha de vencimiento."; encabezado "Inventario · Tu negocio" + pill "3 lotes por vencer"; pestañas conmutables ("Productos", "Lotes", "Por vencer"), cada una con 3 filas de ejemplo. En "Por vencer" los chips de días son "21 días", "9 días" (warn) y "3 días" (danger).

## Cómo funciona
- **Título:** "De cero a tu inventario cargado"
- **Subtítulo:** "Tres pasos y tu equipo ya está trabajando con datos reales."
- **3 pasos**, copy exacto:
  1. **Recibe tus credenciales** — "Un administrador registra tu negocio en Stockma y te entrega el acceso."
  2. **Inicia sesión y confirma con SMS** — "Correo, contraseña y un código que llega a tu celular. Blindaje extra de seguridad para que unicamente tus colaboradores tengan acceso a tu negocio."
  3. **Carga productos, lotes y fechas** — "Con su nombre y su código de barras, cada producto queda con su lote y su vencimiento."

## Funcionalidades principales
- **Subtítulo:** "Tu stock centralizado, con menos trabajo manual y mayor control sobre cada uno de tus productos."
- 6 tarjetas (3x2), títulos exactos:
  - "Todo tu stock en un solo lugar"
  - "Los vencimientos se marcan solos"
  - "El producto aparece al pasar el lector"
  - "Todo tu equipo trabaja al mismo tiempo"
  - "Planes según lo que necesites" (menciona "8 días de prueba gratuita")
  - "Reportes de inventario a tu medida" con el tag **"Próximamente"** (la única tarjeta con tag; los reportes aún no están disponibles, igual que en la FAQ)

## Comparativa
- **Título:** "De la hoja y el lápiz a un software que hace ese trabajo por ti"
- **Subtítulo:** "Cansado de los procesos manuales, rutinarios, y agotadores que te quitan tiempo para realizar tus actividades extralaborales? Con Stockma puedes tener un software automatizado que te evita esos dolores de cabeza y hace todos los trabajos manuales por ti"
- **Columna izquierda — título:** "Sin Stockma: Procesos manuales y pérdidas invisibles"
  - Intro: "Terminas la jornada pero tu trabajo no acaba: te toca reacomodar inventario, adivinar qué lotes están por expirar y asumir pérdidas de productos que se vencieron sin que te dieras cuenta."
  - Bullets (bold + descripción): **Descontrol de registros** (hojas de Excel desactualizadas y con múltiples versiones), **Revisiones tediosas** (verificación manual al cerrar el día), **Pérdida de inversión** (medicamentos e insumos vencidos por falta de alertas oportunas), **Rotación deficiente** (errores al despachar el lote más antiguo), **Cierres de caja propensos al error** (cuadres manuales que obligan a rehacer el trabajo).
- **Columna derecha — título:** "Con Stockma: Cero sorpresas, tienes el control total de tu negocio"
  - Intro: "Automatiza tu operación diaria, elimina los errores manuales y protege cada peso de tu inversión."
  - Bullets (bold + descripción): **Inventario en tiempo real** (fuente única de verdad para todo el equipo), **Semaforización inteligente** (alertas antes de que cualquier lote se venza), **Trazabilidad FEFO** (identifica el lote más antiguo que debe salir), **Captura rápida con IA** (escanea facturas o códigos y actualiza existencias), **Auditorías a 1 clic** (reportes listos para regulaciones), **Optimización de tiempo** (termina tu jornada a la hora prevista).
- > **Nota de copy:** los bullets de la columna "Con Stockma" mencionan IA, auditoría, FEFO y reportes. El prototipo es la fuente de verdad y su copy se replica tal cual, pero entra en tensión con la regla de §Contexto ("solo capacidades reales"); queda marcado para revisión con producto.

## Planes
- **Título:** "Elige tu plan"
- **Subtítulo:** "Los primeros 8 días son gratis. Después eliges el plan que te sirva."
- 4 tarjetas con misma altura (`grid-auto-rows: 1fr`, `align-items: stretch`, botón con `margin-top: auto`):
  - **Básico** — $ 49.000 / mes — "Para dejar el cuaderno sin complicarte." — bullets: productos, lotes y vencimientos / semáforo de vencimientos / 1 usuario — botón "Empezar prueba" (secundario).
  - **Profesional** — $ 89.000 / mes — "Para un equipo que trabaja sobre el mismo inventario." — bullets: todo lo del Básico / hasta 5 usuarios / lector de códigos de barras / reportes para el contador (próximamente) — botón "Empezar prueba" (primario).
  - **Negocio** — $ 149.000 / mes — "Para negocios con más movimiento y más gente." — bullets: todo lo del Profesional / usuarios sin límite / acompañamiento en la carga inicial — botón "Hablar con ventas" (secundario).
  - **A tu medida** — Bajo cotización (borde punteado) — "Si tu operación pide algo que no está en los planes, lo armamos contigo." — bullets: las funciones que necesites / módulos hechos para tu negocio / acompañamiento dedicado — botón "Cotizar mi plan" (secundario).

Los 4 botones tienen `href="#lista-espera"` + `data-open-waitlist` y **abren la modal de lista de espera** (no navegan a `#agendar`). Las tarjetas de plan además son seleccionables (click/Enter/Espacio marca `is-selected` y `aria-pressed`).

## Agenda tu demo
Apartado en la página (`#agendar`), tarjeta con fondo acentuado, bordes 10px.
- **Badge:** "Próximamente"
- **Título:** "Agenda tu demo"
- **Descripción:** "Estamos terminando de construir Stockma y todavía no podemos mostrarte el producto funcionando. Cuando esté listo vas a poder agendar 30 minutos por videollamada: vemos tu inventario real, cargamos un par de productos y revisamos el plan que mejor se ajusta a tu negocio."
- **Botón 1:** "Agendar demo" -> **deshabilitado** (`disabled`, sin acción)
- **Botón 2:** "Mientras tanto, únete a la lista de espera" -> abre la modal de lista de espera

## Lista de espera (maqueta)
Barra `#lista-espera` + modal `#waitlist-modal`.

**Barra (CTA)**
- Pegada al pie de la pantalla (`position: sticky; bottom: 0`), de ancho completo y con el mismo tratamiento visual del header (fondo translúcido, blur y borde superior). El contenido va dentro de `.wrap` para alinearse con el resto de la página.
- Es el **último hijo de `<main>`**: su posición de flujo es el punto de desenganche del sticky, así que la barra está visible desde que carga la página y se libera únicamente cuando aparece el footer.
- ≤900px: se apila (texto centrado) y el botón ocupa todo el ancho.
- **Título:** "Únete a la lista de espera"
- **Texto:** "Rellena el formulario y recibe en primicia información de nuestro lanzamiento"

**Modal**
- **Encabezado:** "Únete a la lista de espera" + "Déjanos tus datos y te notificaremos por correo electrónico la fecha oficial de nuestro lanzamiento"
- **Campos:** nombre completo, teléfono móvil (bandera de Colombia y `+57` fijo dentro del mismo cuadro, sin selector de país por ahora) y correo electrónico.
- **Botón:** "Unirme a la lista de espera" -> sin backend: `preventDefault` + estado `is-submitted` muestra el mensaje de éxito.
- En el estado de éxito (`is-submitted`) se ocultan los campos y el botón, pero el **título del encabezado se mantiene** (solo se oculta el subtítulo). El contenido se apila centrado en este orden: el saludo **"¡Listo, {nombre}!"** personalizado con el primer nombre del formulario (capitalizado por JS; sin JS queda "¡Listo!"), un **badge circular animado** (círculo con `--accent-tint`, dos ondas *ripple* saliendo y el check dibujándose con `stroke-dashoffset` dentro de un *pop*, todo en el teal de los botones `--brand-teal`) y el texto "Estás en la lista de espera. Te avisaremos por WhatsApp y correo cuando nuestro software esté disponible." Todo respeta `prefers-reduced-motion`.
- Cierra con X, fuera o Esc.

## Diseño
- **Tipografía:** Work Sans (única)
- **Bordes:** tokens globales 10px
- **Botones primarios:** en el prototipo van con teal hardcodeado (`#337082`); al implementar en React usan tokens (`--primary` / `--on-primary` / `--primary-active`), nunca `--accent`, que en modo oscuro da 2.33:1 con texto blanco. Detalle en `design.md`.
- **Modo oscuro:** `[data-theme='dark']`, persistido en `localStorage`, script anti-FOUC
- **Sin chips/banners inclinados** sobre secciones
- **Pantallas chicas (≤600px):** escala tipográfica reducida (h1 30px, h2 de sección 25px, cuerpo 14px) y botones de ancho completo con altura flexible.
- **Sin voseo**
- **Footer:** logo apilado recortado (`Stockma_vertical_trim.png`, 118px), iconos circulares de WhatsApp e Instagram (SVG inline, `currentColor`) enlazando a `https://w.app/stockmaapp` e `https://www.instagram.com/stockma_app?dlrf=MXAzNTU1OWl5YzIwZw==` respectivamente; línea "Atención, preguntas frecuentes y soporte: Escribenos al WhatsApp" (el ancla enlaza a `https://w.app/stockmaapp`) y copyright en `space-between`. Costura superior con hairline `1px solid var(--border)`: la vecina del footer es la barra clara de lista de espera, no la sección demo navy.

## Reglas de copy
- Solo capacidades reales
- Descripciones cortas y directas
- Valor de negocio, no obviedades
- Mensaje central: stock centralizado y paso de procesos manuales a automatizados

## Archivos involucrados
- `frontend/web/src/App.tsx` (switch por ruta para integrar landing)
- `frontend/web/src/main.tsx` (ThemeProvider)
- `frontend/web/src/styles/theme/mui-bridge.ts`
- `frontend/web/src/styles/tokens.css`
- `frontend/web/src/assets/Stockma_horizontal.png`
- `frontend/web/src/assets/Stockma_vertical.png`
- `frontend/web/src/assets/Stockma_vertical_trim.png` (recorte al ras para el footer)
- `frontend/web/src/assets/Stockma_logo.png`
- `frontend/web/src/assets/Stockma_name.png`
- `specs/002-product-introduction/Stockma_landing.html` (referencia viva)
- `scripts/check-neutral-es.mjs` (gate)

## Criterios de aceptación
- [ ] Landing accesible en `/` con todas las secciones
- [ ] Nav funcional y responsive (incluye menú móvil)
- [ ] Modo claro/oscuro funcional
- [ ] Hero: CTA primario abre la modal de lista de espera, CTA secundario lleva a `#como-funciona` y las pestañas del mock cambian de panel
- [ ] Tarjetas de planes misma altura
- [ ] Los 4 botones de planes abren la modal de lista de espera
- [ ] Sección "Agenda tu demo": badge "Próximamente", botón "Agendar demo" deshabilitado y CTA que abre la lista de espera
- [ ] Modal de lista de espera abre/cierra y muestra el estado de éxito
- [ ] Teléfono de la lista de espera: bandera de Colombia + `+57` en un solo cuadro
- [ ] Barra de lista de espera pegada al pie y de ancho completo; apilada y a todo el ancho en móvil
- [ ] Footer alineado al mismo eje que el resto de la página (24px de padding lateral)
- [ ] Sin voseo (pasa gate)
- [ ] Sin promesas de funcionalidades no implementadas (los reportes van siempre marcados como "Próximamente")
- [ ] Bordes coherentes 8-10px
- [ ] Footer: logo, iconos de WhatsApp e Instagram con enlace, línea de soporte con enlace a WhatsApp y copyright
