import { fireEvent, render, screen } from '@testing-library/react'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { LoginPage } from './LoginPage'

afterEach(() => {
  vi.unstubAllGlobals()
})

describe('LoginPage', () => {
  it('bloquea el botón mientras espera la respuesta, para no mandar el login dos veces', async () => {
    const fetchMock = vi.fn().mockReturnValue(new Promise(() => {}))
    vi.stubGlobal('fetch', fetchMock)

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
    expect(fetchMock).toHaveBeenCalledTimes(1)
  })
})
