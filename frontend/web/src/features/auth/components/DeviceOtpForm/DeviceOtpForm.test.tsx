import { act, fireEvent, render, screen } from '@testing-library/react'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { DeviceOtpForm } from './DeviceOtpForm'
import { mockPending, restoreHttp } from '../../../../shared/http.testkit'

afterEach(() => {
  restoreHttp()
  vi.useRealTimers()
})

function renderForm(resendAvailableAt?: number) {
  const onAuthenticated = vi.fn()
  const onBack = vi.fn()

  render(
    <DeviceOtpForm
      email='farmacia@ejemplo.co'
      deviceId={'d'.repeat(16)}
      onAuthenticated={onAuthenticated}
      onBack={onBack}
      resendAvailableAt={resendAvailableAt}
    />,
  )

  return { onAuthenticated, onBack }
}

describe('DeviceOtpForm', () => {
  it('bloquea Volver y Continuar mientras valida el código', async () => {
    mockPending()
    const { onBack } = renderForm()

    fireEvent.change(screen.getByLabelText(/código/i), { target: { value: '123456' } })
    fireEvent.click(screen.getByRole('button', { name: /continuar/i }))

    const back = await screen.findByRole('button', { name: /volver/i })
    expect((back as HTMLButtonElement).disabled).toBe(true)
    expect((screen.getByRole('button', { name: /validando/i }) as HTMLButtonElement).disabled).toBe(
      true,
    )

    fireEvent.click(back)
    expect(onBack).not.toHaveBeenCalled()
  })

  it('cuenta hacia atrás cuánto falta para poder pedir otro código', () => {
    vi.useFakeTimers()
    vi.setSystemTime(new Date('2026-10-13T12:00:00Z'))

    renderForm(Date.now() + 90_000)

    screen.getByText(/en 1:30 podés volver a iniciar sesión para pedir otro/i)

    act(() => {
      vi.advanceTimersByTime(30_000)
    })

    screen.getByText(/en 1:00 podés volver a iniciar sesión para pedir otro/i)
  })

  it('pasado el cooldown avisa que puede volver a iniciar sesión para pedir otro', () => {
    vi.useFakeTimers()
    vi.setSystemTime(new Date('2026-10-13T12:00:00Z'))

    renderForm(Date.now() + 60_000)

    act(() => {
      vi.advanceTimersByTime(61_000)
    })

    screen.getByText(/volvé a iniciar sesión para pedir otro/i)
  })
})
