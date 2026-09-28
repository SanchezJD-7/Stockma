import Alert from '@mui/material/Alert'
import Button from '@mui/material/Button'
import TextField from '@mui/material/TextField'
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

    expect(theme.palette.primary.main).toBe('#182f4f')
    expect(theme.palette.primary.dark).toBe('#142843')
    expect(theme.palette.primary.light).toBe('#e8eaed')
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

  it('pinta el fondo de los campos filled con el color de marca y no con el gris de fábrica', () => {
    render(
      <ThemeProvider theme={createMuiBridge()}>
        <TextField label='Email' variant='filled' />
      </ThemeProvider>,
    )

    const field = document.querySelector('.MuiFilledInput-root') as HTMLElement

    expect(getComputedStyle(field).backgroundColor).toBe('rgb(227, 238, 241)')
  })

  it('tapa el celeste del autocompletado del navegador con el fondo de marca', () => {
    render(
      <ThemeProvider theme={createMuiBridge()}>
        <TextField label='Email' variant='filled' />
      </ThemeProvider>,
    )

    const emitted = Array.from(document.querySelectorAll('style'))
      .map((tag) => tag.textContent ?? '')
      .join('')

    expect(emitted).toMatch(/:-webkit-autofill\{[^}]*box-shadow:0 0 0 100px #e3eef1 inset/)
  })

  it('marca el foco de los campos filled con el teal de Stockma', () => {
    render(
      <ThemeProvider theme={createMuiBridge()}>
        <TextField label='Email' variant='filled' />
      </ThemeProvider>,
    )

    const emitted = Array.from(document.querySelectorAll('style'))
      .map((tag) => tag.textContent ?? '')
      .join('')

    expect(emitted).toMatch(/::after\{[^}]*border-bottom-color:#337082/)
    expect(emitted).toMatch(/\.Mui-focused\{[^}]*color:#337082/)
  })

  it('dibuja las etiquetas de los campos con el tamaño de texto de Stockma', () => {
    render(
      <ThemeProvider theme={createMuiBridge()}>
        <TextField label='Email' variant='filled' />
      </ThemeProvider>,
    )

    const label = screen.getByText('Email').closest('label') as HTMLElement

    expect(getComputedStyle(label).fontSize).toBe('14px')
  })

  it('pinta las alertas de error con el rojo de Stockma: texto rojo sobre fondo rojo suave', () => {
    render(
      <ThemeProvider theme={createMuiBridge()}>
        <Alert severity='error'>Algo falló</Alert>
      </ThemeProvider>,
    )

    const alert = getComputedStyle(screen.getByRole('alert'))

    expect(alert.color).toBe('rgb(198, 40, 40)')
    expect(alert.backgroundColor).toBe('rgb(249, 233, 233)')
  })

  it('falla con un mensaje claro si los tokens no están cargados', () => {
    const orphan = document.createElement('div')

    expect(() => createMuiBridge(orphan)).toThrow(/El token --[a-z-]+ no está definido/)
  })
})
