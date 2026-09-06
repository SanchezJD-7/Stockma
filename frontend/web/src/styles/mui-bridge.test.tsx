import Button from '@mui/material/Button'
import { ThemeProvider } from '@mui/material/styles'
import { render, screen } from '@testing-library/react'
import { afterEach, describe, expect, it } from 'vitest'
import { applyTenantBranding } from './branding'
import { createMuiBridge } from './mui-bridge'

afterEach(() => {
  document.documentElement.removeAttribute('style')
})

describe('createMuiBridge', () => {
  it('toma los colores por defecto de tokens.css, no literales propios', () => {
    const theme = createMuiBridge()

    expect(theme.palette.primary.main).toBe('#1565c0')
    expect(theme.palette.primary.dark).toBe('#0d47a1')
    expect(theme.palette.primary.light).toBe('#e7edf6')
    expect(theme.palette.primary.contrastText).toBe('#ffffff')
  })

  it('refleja el branding del tenant cuando se reconstruye', () => {
    applyTenantBranding({
      primary: '#8e24aa',
      primaryActive: '#6a1b9a',
      primaryBg: '#f3e5f5',
    })

    const theme = createMuiBridge()

    expect(theme.palette.primary.main).toBe('#8e24aa')
    expect(theme.palette.primary.dark).toBe('#6a1b9a')
    expect(theme.palette.primary.contrastText).toBe('#ffffff')
  })

  it('renderiza un componente de MUI con el color del tenant y no con su azul de fábrica', () => {
    applyTenantBranding({ primary: '#8e24aa' })

    render(
      <ThemeProvider theme={createMuiBridge()}>
        <Button variant='contained'>Guardar</Button>
      </ThemeProvider>,
    )

    const emitted = Array.from(document.querySelectorAll('style'))
      .map((tag) => tag.textContent ?? '')
      .join('')

    expect(screen.getByRole('button', { name: 'Guardar' })).toBeTruthy()
    expect(emitted).toContain('#8e24aa')
    expect(emitted).not.toContain('#1976d2')
  })

  it('falla con un mensaje claro si los tokens no están cargados', () => {
    const orphan = document.createElement('div')

    expect(() => createMuiBridge(orphan)).toThrow(/--color-primary no está definido/)
  })
})
