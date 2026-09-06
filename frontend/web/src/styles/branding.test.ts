import { afterEach, describe, expect, it } from 'vitest'
import { applyTenantBranding, contrastTextFor } from './branding'

const root = () => document.documentElement
const readVar = (name: string) => root().style.getPropertyValue(name)

afterEach(() => {
  root().removeAttribute('style')
})

describe('contrastTextFor', () => {
  it('devuelve texto blanco sobre un primary oscuro', () => {
    expect(contrastTextFor('#0d47a1')).toBe('#ffffff')
  })

  it('devuelve texto negro sobre un primary claro', () => {
    expect(contrastTextFor('#ffe082')).toBe('#000000')
  })

  it('acepta hex de 3 dígitos', () => {
    expect(contrastTextFor('#fff')).toBe('#000000')
    expect(contrastTextFor('#000')).toBe('#ffffff')
  })

  it('cae en texto blanco cuando el color es inválido', () => {
    expect(contrastTextFor('rojo')).toBe('#ffffff')
  })
})

describe('applyTenantBranding', () => {
  it('escribe los tres colores del tenant y deriva el contraste', () => {
    applyTenantBranding({
      primary: '#8e24aa',
      primaryActive: '#6a1b9a',
      primaryBg: '#f3e5f5',
    })

    expect(readVar('--color-primary')).toBe('#8e24aa')
    expect(readVar('--color-primary-active')).toBe('#6a1b9a')
    expect(readVar('--color-primary-bg')).toBe('#f3e5f5')
    expect(readVar('--color-primary-contrast')).toBe('#ffffff')
  })

  it('no escribe nada cuando el tenant no configuró branding', () => {
    applyTenantBranding(null)

    expect(readVar('--color-primary')).toBe('')
    expect(readVar('--color-primary-contrast')).toBe('')
  })

  it('limpia branding previo para que vuelvan los valores por defecto de tokens.css', () => {
    applyTenantBranding({ primary: '#8e24aa' })
    expect(readVar('--color-primary')).toBe('#8e24aa')

    applyTenantBranding(null)
    expect(readVar('--color-primary')).toBe('')
    expect(readVar('--color-primary-contrast')).toBe('')
  })

  it('ignora un color inválido en vez de inyectarlo en el DOM', () => {
    applyTenantBranding({
      primary: 'red; background: url(evil)',
      primaryBg: '#f3e5f5',
    })

    expect(readVar('--color-primary')).toBe('')
    expect(readVar('--color-primary-bg')).toBe('#f3e5f5')
  })

  it('sólo escribe los colores presentes y deja el resto en su valor por defecto', () => {
    applyTenantBranding({ primaryBg: '#e7edf6' })

    expect(readVar('--color-primary')).toBe('')
    expect(readVar('--color-primary-active')).toBe('')
    expect(readVar('--color-primary-bg')).toBe('#e7edf6')
    expect(readVar('--color-primary-contrast')).toBe('')
  })
})
