import { describe, expect, it } from 'vitest'
import { ApiError } from '../../../shared/http'
import { authErrorMessage } from './auth-error-message'

describe('authErrorMessage', () => {
  it.each([
    ['AUTH_INVALID_CREDENTIALS', 401, 'Email o contraseña incorrectos.'],
    ['AUTH_OTP_REJECTED', 401, 'Código incorrecto o vencido.'],
    ['AUTH_RATE_LIMITED', 429, 'Demasiados intentos, espera un minuto.'],
    [
      'AUTH_PHONE_NOT_ENROLLED',
      403,
      'Tu usuario no tiene un celular registrado para recibir el código. Pídele a un administrador que lo registre.',
    ],
    ['VALIDATION_FAILED', 400, 'Revisa los datos ingresados e intenta de nuevo.'],
  ])('traduce %s', (errorCode, status, message) => {
    expect(authErrorMessage(new ApiError(status, errorCode))).toBe(message)
  })

  it('decide por el errorCode y no por el status', () => {
    expect(authErrorMessage(new ApiError(401, 'AUTH_OTP_REJECTED'))).not.toBe(
      authErrorMessage(new ApiError(401, 'AUTH_INVALID_CREDENTIALS')),
    )
  })

  it('usa el mensaje genérico ante un errorCode desconocido o ausente, sin mostrar el detail', () => {
    const generic = 'Ocurrió un error. Intenta de nuevo en un momento.'

    expect(authErrorMessage(new ApiError(400, 'ALGO_NUEVO'))).toBe(generic)
    expect(authErrorMessage(new ApiError(502, null))).toBe(generic)
    expect(authErrorMessage(new ApiError(400, 'constructor'))).toBe(generic)
  })

  it('avisa que no hay conexión cuando el fetch ni siquiera llega al servidor', () => {
    expect(authErrorMessage(new TypeError('Failed to fetch'))).toBe(
      'No pudimos conectar con el servidor. Revisa tu conexión e intenta de nuevo.',
    )
  })
})
