import TextField from '@mui/material/TextField'
import { ThemeProvider } from '@mui/material/styles'
import { render, screen } from '@testing-library/react'
import { afterEach, describe, expect, it } from 'vitest'
import { SubmitButton } from '../../styles/auth-form.styles'
import { createMuiBridge } from '../../../../styles/theme/mui-bridge'
import { AuthLayout } from './AuthLayout'

afterEach(() => {
  document.documentElement.removeAttribute('data-theme')
})

describe('AuthLayout', () => {
  it('muestra el logo de Stockma arriba del contenido', () => {
    render(
      <AuthLayout>
        <p>formulario</p>
      </AuthLayout>,
    )

    const logo = screen.getByRole('img', { name: 'Stockma' })
    const content = screen.getByText('formulario')

    expect(logo.getAttribute('src')).toMatch(/Stockma_logo\.png/)
    expect(logo.compareDocumentPosition(content) & Node.DOCUMENT_POSITION_FOLLOWING).toBeTruthy()
  })

  it('mantiene el botón y los campos con la paleta de la captura aunque el tema global sea oscuro', () => {
    document.documentElement.dataset.theme = 'dark'

    render(
      <ThemeProvider theme={createMuiBridge()}>
        <AuthLayout>
          <TextField label='Email' variant='filled' />
          <SubmitButton>Ingresar</SubmitButton>
        </AuthLayout>
      </ThemeProvider>,
    )

    const field = document.querySelector('.MuiFilledInput-root') as HTMLElement
    const input = document.querySelector('.MuiFilledInput-input') as HTMLElement
    const button = screen.getByRole('button', { name: 'Ingresar' })
    const card = button.closest('section') as HTMLElement

    expect(getComputedStyle(field).backgroundColor).toBe('var(--color-login-field-bg)')
    expect(getComputedStyle(input).color).toBe('var(--color-login-text-primary)')
    expect(getComputedStyle(card).getPropertyValue('--color-primary')).toBe(
      'var(--color-login-primary)',
    )
    expect(
      getComputedStyle(document.documentElement).getPropertyValue('--color-login-field-bg'),
    ).toBe('#e3eef1')
    expect(
      getComputedStyle(document.documentElement).getPropertyValue('--color-login-primary'),
    ).toBe('var(--brand-navy)')
    expect(getComputedStyle(document.documentElement).getPropertyValue('--brand-navy')).toBe(
      '#182f4f',
    )
  })
})
