import { render, screen } from '@testing-library/react'
import { describe, expect, it } from 'vitest'
import { AuthLayout } from './AuthLayout'

describe('AuthLayout', () => {
  it('muestra el logo de Stockma arriba del contenido', () => {
    render(
      <AuthLayout>
        <p>formulario</p>
      </AuthLayout>,
    )

    const logo = screen.getByRole('img', { name: 'Stockma' })
    const content = screen.getByText('formulario')

    expect(logo.getAttribute('src')).toMatch(/stockma-stacked\.svg/)
    expect(logo.compareDocumentPosition(content) & Node.DOCUMENT_POSITION_FOLLOWING).toBeTruthy()
  })
})
