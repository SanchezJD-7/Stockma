import { describe, expect, it } from 'vitest'

const components = import.meta.glob<string>(
  ['../features/**/*.tsx', '!../features/**/*.test.tsx'],
  { query: '?raw', import: 'default', eager: true },
)

const forbidden = [
  { name: 'styled', pattern: /@emotion\/styled|\bstyled\s*[.(]/ },
  { name: 'css', pattern: /@emotion\/react|\bcss\s*=\s*\{|\bcss\s*`/ },
  { name: 'sx', pattern: /\bsx\s*=\s*\{/ },
  { name: 'style', pattern: /\bstyle\s*=\s*\{/ },
  { name: 'className', pattern: /\bclassName\s*=/ },
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
    ['className', "<div className='app-landing' />"],
  ])('la regla de %s atrapa: %s', (name, snippet) => {
    expect(violations(snippet)).toContain(name)
  })

  it('encuentra los componentes a revisar', () => {
    expect(Object.keys(components).length).toBeGreaterThan(0)
  })

  it.each(Object.entries(components))('%s no define estilos propios', (_path, source) => {
    expect(violations(source)).toEqual([])
  })
})
