import { act, fireEvent, render, screen, waitFor } from '@testing-library/react'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { AuthFlow } from './AuthFlow'
import { useAuthStore } from '../../store/auth-store'
import { json, mockApi, restoreHttp } from '../../../../shared/http.testkit'

async function fillLoginForm(email = 'farmacia@ejemplo.co', password = 'S3gura#2026') {
  fireEvent.change(screen.getByLabelText(/email/i), { target: { value: email } })
  fireEvent.change(screen.getByLabelText(/contraseña/i), { target: { value: password } })
  fireEvent.click(screen.getByRole('button', { name: 'Ingresar' }))
}

function submitOtp(code: string) {
  fireEvent.change(screen.getByLabelText(/código/i), { target: { value: code } })
  fireEvent.click(screen.getByRole('button', { name: /continuar/i }))
}

beforeEach(() => {
  localStorage.clear()
  useAuthStore.setState({ session: null })
})

afterEach(() => {
  restoreHttp()
  vi.useRealTimers()
})

describe('AuthFlow', () => {
  it('dispositivo desconocido: pide OTP, reutiliza el deviceId y guarda el token al confirmar', async () => {
    const requests = mockApi(
      json(200, { requiresDeviceConfirmation: true }),
      json(200, { accessToken: 'xyz', expiresIn: 1800, deviceTrusted: true }),
    )

    render(<AuthFlow />)
    await fillLoginForm()
    await screen.findByText('Confirma el dispositivo')

    submitOtp('482913')
    await screen.findByText('Sesión iniciada')
    await waitFor(() => expect(requests).toHaveLength(2))

    const loginBody = JSON.parse(String(requests[0].data))
    const confirmBody = JSON.parse(String(requests[1].data))
    expect(confirmBody.deviceId).toBe(loginBody.deviceId)
    expect(loginBody.deviceId.length).toBeGreaterThanOrEqual(16)
    expect(useAuthStore.getState().session?.accessToken).toBe('xyz')
  })

  it('inicia sesión directo y guarda el token cuando el dispositivo es confiable', async () => {
    mockApi(json(200, { accessToken: 'abc', expiresIn: 3600 }))

    render(<AuthFlow />)
    await fillLoginForm()

    await screen.findByText('Sesión iniciada')
    expect(useAuthStore.getState().session?.accessToken).toBe('abc')
  })

  it('muestra un error cuando el OTP es rechazado', async () => {
    mockApi(
      json(200, { requiresDeviceConfirmation: true }),
      json(401, { errorCode: 'AUTH_OTP_REJECTED' }),
    )

    render(<AuthFlow />)
    await fillLoginForm()
    await screen.findByText('Confirma el dispositivo')

    submitOtp('000000')
    await screen.findByText('Código incorrecto o vencido.')
  })

  it('muestra un mensaje genérico cuando las credenciales son inválidas', async () => {
    mockApi(json(401, { errorCode: 'AUTH_INVALID_CREDENTIALS' }))

    render(<AuthFlow />)
    await fillLoginForm()

    await screen.findByText('Email o contraseña incorrectos.')
  })

  it('avisa cuando se superó el límite de intentos', async () => {
    mockApi(json(429, { errorCode: 'AUTH_RATE_LIMITED' }))

    render(<AuthFlow />)
    await fillLoginForm()

    await screen.findByText('Demasiados intentos, espera un minuto.')
  })

  it('vuelve al login cuando la sesión vence con la pestaña abierta', () => {
    vi.useFakeTimers()
    useAuthStore.getState().setSession('abc', 60)

    render(<AuthFlow />)
    expect(screen.getByText('Sesión iniciada')).toBeTruthy()

    act(() => {
      vi.advanceTimersByTime(60_000)
    })

    expect(screen.queryByText('Sesión iniciada')).toBeNull()
    expect(screen.getByText('Iniciar sesión')).toBeTruthy()
    expect(useAuthStore.getState().session).toBeNull()
  })

  it('explica qué hacer cuando el usuario no tiene celular cargado, en vez del error genérico', async () => {
    mockApi(json(403, { errorCode: 'AUTH_PHONE_NOT_ENROLLED' }))

    render(<AuthFlow />)
    await fillLoginForm()

    await screen.findByText(
      'Tu usuario no tiene un celular registrado para recibir el código. Pídele a un administrador que lo registre.',
    )
  })

  it('muestra el error como alerta', async () => {
    mockApi(json(401, { errorCode: 'AUTH_INVALID_CREDENTIALS' }))

    render(<AuthFlow />)
    await fillLoginForm()

    const alert = await screen.findByRole('alert')
    expect(alert.classList.contains('MuiAlert-root')).toBe(true)
    expect(alert.textContent).toContain('Email o contraseña incorrectos.')
  })

  it('al volver del código conserva el email y borra la contraseña', async () => {
    mockApi(json(200, { requiresDeviceConfirmation: true }))

    render(<AuthFlow />)
    await fillLoginForm('farmacia@ejemplo.co', 'S3gura#2026')
    await screen.findByText('Confirma el dispositivo')

    fireEvent.click(screen.getByRole('button', { name: /volver/i }))
    await screen.findByText('Iniciar sesión')

    expect((screen.getByLabelText(/email/i) as HTMLInputElement).value).toBe('farmacia@ejemplo.co')
    expect((screen.getByLabelText(/contraseña/i) as HTMLInputElement).value).toBe('')
  })

  it('pone Volver y Continuar en la misma fila, con flecha a la izquierda y a la derecha', async () => {
    mockApi(json(200, { requiresDeviceConfirmation: true }))

    render(<AuthFlow />)
    await fillLoginForm()
    await screen.findByText('Confirma el dispositivo')

    const back = screen.getByRole('button', { name: /volver/i })
    const next = screen.getByRole('button', { name: /continuar/i })

    expect(back.parentElement).toBe(next.parentElement)
    expect(back.compareDocumentPosition(next) & Node.DOCUMENT_POSITION_FOLLOWING).toBeTruthy()
    expect(back.querySelector('[data-testid="ArrowBackIcon"]')).not.toBeNull()
    expect(next.querySelector('[data-testid="ArrowForwardIcon"]')).not.toBeNull()
  })
})
