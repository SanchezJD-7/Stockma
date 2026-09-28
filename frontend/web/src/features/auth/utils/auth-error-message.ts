import { ApiError } from '../api/auth-api'

const GENERIC_MESSAGE = 'Ocurrió un error. Intenta de nuevo en un momento.'
const OFFLINE_MESSAGE =
  'No pudimos conectar con el servidor. Revisa tu conexión e intenta de nuevo.'

const MESSAGES: Record<string, string> = {
  AUTH_INVALID_CREDENTIALS: 'Email o contraseña incorrectos.',
  AUTH_OTP_REJECTED: 'Código incorrecto o vencido.',
  AUTH_RATE_LIMITED: 'Demasiados intentos, espera un minuto.',
  AUTH_PHONE_NOT_ENROLLED:
    'Tu usuario no tiene un celular registrado para recibir el código. Pídele a un administrador que lo registre.',
  VALIDATION_FAILED: 'Revisa los datos ingresados e intenta de nuevo.',
}

export function authErrorMessage(error: unknown): string {
  if (error instanceof ApiError) {
    const { errorCode } = error
    return errorCode && Object.hasOwn(MESSAGES, errorCode) ? MESSAGES[errorCode] : GENERIC_MESSAGE
  }

  if (error instanceof TypeError) {
    return OFFLINE_MESSAGE
  }

  return GENERIC_MESSAGE
}
