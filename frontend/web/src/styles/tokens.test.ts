import { readFileSync, readdirSync } from 'node:fs'
import { dirname, join, relative } from 'node:path'
import { fileURLToPath } from 'node:url'
import { describe, expect, it } from 'vitest'

const srcDir = join(dirname(fileURLToPath(import.meta.url)), '..')

const exempt = new Set([join('styles', 'theme', 'branding.ts')])

function collect(dir: string): string[] {
  const found: string[] = []
  for (const entry of readdirSync(dir, { withFileTypes: true })) {
    const full = join(dir, entry.name)
    if (entry.isDirectory()) found.push(...collect(full))
    else found.push(full)
  }
  return found
}

const styleFiles = collect(srcDir).filter((path) => {
  if (exempt.has(relative(srcDir, path))) return false
  if (path.endsWith('.css')) return !path.endsWith('tokens.css')
  if (path.endsWith('.ts')) return !path.endsWith('.test.ts') && !path.endsWith('.d.ts')
  return false
})

const fontSizeLiteral = /font-size:(?!\s*var\()[^;]+/
const colorLiteral = /#[0-9a-fA-F]{3,8}\b|rgba?\(|hsla?\(/
const namedColor =
  /(?:color|background|border[a-z-]*|outline|fill|stroke|box-shadow)\s*:[^;]*?(?<![-\w])(?:red|white|black|blue|green|gray|grey|orange|yellow|purple|pink)(?![\w-])/

function violations(source: string) {
  const found: string[] = []
  if (fontSizeLiteral.test(source)) found.push('font-size sin var(--fs-*)')
  if (colorLiteral.test(source)) found.push('color literal (hex/rgb)')
  if (namedColor.test(source)) found.push('color con nombre')
  return found
}

describe('los estilos referencian tokens.css', () => {
  it('encuentra los archivos de estilos a revisar', () => {
    expect(styleFiles.length).toBeGreaterThan(10)
  })

  it.each(styleFiles.map((path) => [relative(srcDir, path), path] as const))(
    '%s usa solo tokens',
    (_label, path) => {
      expect(violations(readFileSync(path, 'utf8'))).toEqual([])
    },
  )
})
