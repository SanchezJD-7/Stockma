import { describe, expect, it } from 'vitest'

const components = import.meta.glob<string>(
  ['../features/**/*.tsx', '!../features/**/*.test.tsx'],
  { query: '?raw', import: 'default', eager: true },
)

/**
 * Los componentes de una feature no declaran estilos: solo consumen tokens.
 *
 * Permitido: `className={styles.title}` o `className={`${shared.card} ${styles.plan}`}`,
 * donde `styles` viene de un `*.module.css` del feature.
 * Prohibido: cualquier valor de estilo escrito en el componente (`style`, `sx`, `css`,
 * `styled`) y las clases globales literales (`className='app-landing'`), que escapan
 * al scoping del módulo.
 */
const forbidden = [
  { name: 'styled', pattern: /@emotion\/styled|\bstyled\s*[.(]/ },
  { name: 'css', pattern: /@emotion\/react|\bcss\s*=\s*\{|\bcss\s*`/ },
  { name: 'sx', pattern: /\bsx\s*=\s*\{/ },
  { name: 'style', pattern: /\bstyle\s*=\s*\{/ },
  { name: 'clase global', pattern: /\bclassName\s*=\s*(['"`])/ },
]

function violations(source: string) {
  return forbidden.filter(({ pattern }) => pattern.test(source)).map(({ name }) => name)
}

describe('estilos fuera de los componentes', () => {
  it.each([
    ['styled', 'const Box = styled.div`color: red`'],
    ['styled', 'const Box = styled (Button)`x`'],
    ['css', '<div css={{ color: "red" }} />'],
    ['sx', '<Stack sx = {{ mt: 2 }} />'],
    ['style', "<p style={{ textAlign: 'center' }} />"],
    ['clase global', "<div className='app-landing' />"],
    ['clase global', '<div className="app-landing" />'],
  ])('la regla de %s atrapa: %s', (name, snippet) => {
    expect(violations(snippet)).toContain(name)
  })

  it.each([
    ['un token de módulo', '<div className={styles.root} />'],
    ['tokens combinados', '<div className={`${shared.card} ${styles.plan}`} />'],
  ])('permite %s', (_name, snippet) => {
    expect(violations(snippet)).toEqual([])
  })

  it('encuentra los componentes a revisar', () => {
    expect(Object.keys(components).length).toBeGreaterThan(0)
  })

  it.each(Object.entries(components))('%s no define estilos propios', (_path, source) => {
    expect(violations(source)).toEqual([])
  })
})
