import { render, screen } from '@testing-library/react'
import { describe, expect, it } from 'vitest'
import { FormAlert } from './FormAlert'

describe('FormAlert', () => {
  it('anuncia el aviso con role alert y severidad error por defecto', () => {
    render(<FormAlert>No pudimos completar el registro</FormAlert>)

    const alert = screen.getByRole('alert')
    expect(alert.textContent).toContain('No pudimos completar el registro')
    expect(alert.classList.contains('MuiAlert-colorError')).toBe(true)
    expect(alert.classList.contains('MuiAlert-filled')).toBe(true)
    expect(alert.querySelector('[aria-hidden="true"]')).not.toBeNull()
  })

  it('acepta otra severidad y el texto como children', () => {
    render(<FormAlert severity='success'>Registro completado</FormAlert>)

    const alert = screen.getByRole('alert')
    expect(alert.textContent).toContain('Registro completado')
    expect(alert.classList.contains('MuiAlert-colorSuccess')).toBe(true)
  })
})
