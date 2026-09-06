# Stockma — Frontend web

Cliente web de Stockma. React 19 + TypeScript + Vite, con **Material UI**
(Emotion) para componentes complejos y **CSS puro** para el sistema de diseño.
TanStack Query para datos del servidor y Zustand para estado de UI.

> Tailwind **no** se usa en este proyecto.

## Comandos

| Comando                | Qué hace                                                 |
| ---------------------- | -------------------------------------------------------- |
| `npm run dev`          | Servidor de desarrollo (proxy `/api` → `localhost:5265`) |
| `npm run build`        | Build de producción                                      |
| `npm run typecheck`    | Chequeo de tipos sin emitir                              |
| `npm run lint`         | oxlint                                                   |
| `npm run test`         | vitest                                                   |
| `npm run format`       | Aplica formato con Prettier                              |
| `npm run format:check` | Verifica formato con Prettier                            |

El proxy apunta al puerto del perfil `http` de la API
(`backend/src/Stockma.Api/Properties/launchSettings.json`). Si lo cambiás allá,
cambialo acá.

## Sistema de diseño

```
src/styles.css            Hoja raíz: tokens, reset y tipografía del documento
src/styles/tokens.css     Única fuente de verdad: SÓLO colores y tipografía
src/styles/buttons.tsx    Botones reutilizables
src/styles/texts.tsx      Textos reutilizables
src/styles/mui-bridge.ts  Construye el theme de MUI desde los tokens
src/styles/branding.ts    Branding por tenant + contraste derivado
```

Reglas:

- **Ningún color literal fuera de `tokens.css`.** Los componentes leen `var(--...)`.
- **`tokens.css` sólo lleva colores y tipografía.** Espaciados, radios y sombras van
  en el componente que los necesita.
- **Lo único compartido son botones y textos.** El layout no es un primitivo: cada
  componente trae su propio CSS al lado.
- **No hay un theme con valores propios.** `createMuiBridge()` lee de `:root` los
  tokens ya resueltos. Pasarle `var()` a la paleta de MUI no alcanza: sus
  componentes llaman `alpha()` al renderizar y no pueden parsearlo.
- El theme es una **foto** de los tokens. Después de cambiar el branding hay que
  reconstruirlo.
- Tipografía única: **Work Sans**.

### Branding por tenant

El tenant configura tres colores: `--color-primary`, `--color-primary-active` y
`--color-primary-bg`. `--color-primary-contrast` lo deriva la aplicación de la
luminancia del primary, para que ningún tenant se deje un botón ilegible.

Sin branding configurado ganan los valores por defecto de `tokens.css`; los
defaults no se duplican en ningún otro lado.

Los colores del semáforo de vencimiento **no** son configurables: rojo significa
"vencido" en toda la aplicación y eso es semántica del producto, no identidad
visual.

## Tests

`src/test-setup.ts` inyecta `tokens.css` en el DOM de cada archivo de test, así
que cualquier código que lea una custom property funciona igual que en el browser.

La configuración de vitest vive en **`vitest.config.ts`**, no en `vite.config.ts`
— el bloque `test` de este último se ignora.

La organización por features llega en la Fase 6 del slice `inventory-foundation`.
