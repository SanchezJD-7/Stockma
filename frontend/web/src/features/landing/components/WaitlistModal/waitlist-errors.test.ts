import { describe, expect, it } from 'vitest'
import { ApiError } from '../../../../shared/http'
import { ERROR_MESSAGES, resolveWaitlistError } from './waitlist-errors'

describe('resolveWaitlistError', () => {
  it('con VALIDATION_FAILED devuelve el resumen general y la primera razón de cada campo', () => {
    const resolved = resolveWaitlistError(
      new ApiError(400, 'VALIDATION_FAILED', {
        email: ['El correo no tiene un formato válido.', 'Otra razón que no se muestra.'],
        phone: ['El teléfono no es válido.'],
      }),
    )

    expect(resolved.formError).toBe(ERROR_MESSAGES.validation)
    expect(resolved.fieldErrors).toEqual({
      email: 'El correo no tiene un formato válido.',
      phone: 'El teléfono no es válido.',
    })
  })

  it('con VALIDATION_FAILED ignora los campos sin mensajes', () => {
    const resolved = resolveWaitlistError(new ApiError(400, 'VALIDATION_FAILED', { email: [] }))

    expect(resolved.fieldErrors).toEqual({})
    expect(resolved.formError).toBe(ERROR_MESSAGES.validation)
  })

  it('con WAITLIST_EMAIL_DUPLICATE muestra el motivo puntual y sin errores de campo', () => {
    const resolved = resolveWaitlistError(new ApiError(409, 'WAITLIST_EMAIL_DUPLICATE'))

    expect(resolved).toEqual({ formError: ERROR_MESSAGES.duplicate, fieldErrors: {} })
  })

  it('con INTERNAL_ERROR muestra el mensaje genérico', () => {
    const resolved = resolveWaitlistError(new ApiError(500, 'INTERNAL_ERROR'))

    expect(resolved).toEqual({ formError: ERROR_MESSAGES.internal, fieldErrors: {} })
  })

  it('sin errorCode (red caída) se trata como problema de conexión', () => {
    expect(resolveWaitlistError(new ApiError(502)).formError).toBe(ERROR_MESSAGES.network)
  })

  it('un errorCode desconocido cae en el mensaje genérico', () => {
    expect(resolveWaitlistError(new ApiError(500, 'SOMETHING_ELSE')).formError).toBe(
      ERROR_MESSAGES.internal,
    )
  })

  it('un error que no es ApiError se trata como problema de conexión', () => {
    expect(resolveWaitlistError(new Error('boom')).formError).toBe(ERROR_MESSAGES.network)
  })
})
