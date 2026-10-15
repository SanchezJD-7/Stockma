import { fireEvent, render, screen } from '@testing-library/react'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { LoginPage } from './LoginPage'
import { mockPending, restoreHttp } from '../../../../shared/http.testkit'

afterEach(() => {
  restoreHttp()
})

describe('LoginPage', () => {
  it('bloquea el botón mientras espera la respuesta, para no mandar el login dos veces', async () => {
    const requests = mockPending()

    render(
      <LoginPage
        deviceId={'d'.repeat(16)}
        onAuthenticated={vi.fn()}
        onRequiresConfirmation={vi.fn()}
      />,
    )

    fireEvent.change(screen.getByLabelText(/email/i), { target: { value: 'a@a.com' } })
    fireEvent.change(screen.getByLabelText(/contraseña/i), { target: { value: 'secreta' } })
    fireEvent.click(screen.getByRole('button', { name: 'Ingresar' }))

    const button = await screen.findByRole('button', { name: /ingresando/i })
    expect((button as HTMLButtonElement).disabled).toBe(true)

    fireEvent.click(button)
    expect(requests).toHaveLength(1)
  })

  it('un doble submit en el mismo instante lanza una sola request', () => {
    const requests = mockPending()

    render(
      <LoginPage
        deviceId={'d'.repeat(16)}
        onAuthenticated={vi.fn()}
        onRequiresConfirmation={vi.fn()}
      />,
    )

    fireEvent.change(screen.getByLabelText(/email/i), { target: { value: 'a@a.com' } })
    fireEvent.change(screen.getByLabelText(/contraseña/i), { target: { value: 'secreta' } })

    const form = screen.getByRole('button', { name: 'Ingresar' }).closest('form')
    if (!form) throw new Error('el botón debe vivir dentro del formulario')

    fireEvent.submit(form)
    fireEvent.submit(form)

    expect(requests).toHaveLength(1)
  })
})
