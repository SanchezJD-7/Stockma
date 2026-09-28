import { fireEvent, render, screen } from '@testing-library/react'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { DeviceOtpForm } from './DeviceOtpForm'

afterEach(() => {
  vi.unstubAllGlobals()
})

function renderForm() {
  const onAuthenticated = vi.fn()
  const onBack = vi.fn()

  render(
    <DeviceOtpForm
      email='farmacia@ejemplo.co'
      deviceId={'d'.repeat(16)}
      onAuthenticated={onAuthenticated}
      onBack={onBack}
    />,
  )

  return { onAuthenticated, onBack }
}

describe('DeviceOtpForm', () => {
  it('bloquea Volver y Continuar mientras valida el código', async () => {
    vi.stubGlobal('fetch', vi.fn().mockReturnValue(new Promise(() => {})))
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
})
