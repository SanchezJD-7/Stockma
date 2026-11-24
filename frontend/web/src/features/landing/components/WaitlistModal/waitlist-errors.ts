import { ApiError } from '../../../../shared/http'

export const ERROR_MESSAGES = {
  validation: 'Revisa los campos marcados e intenta de nuevo.',
  duplicate: 'Ese correo ya está en la lista de espera.',
  internal: 'No pudimos completar el registro; intenta de nuevo.',
  network: 'No hay conexión con el servidor; verifica tu internet.',
} as const

export interface ResolvedWaitlistError {
  formError: string
  fieldErrors: Record<string, string>
}

export function resolveWaitlistError(error: unknown): ResolvedWaitlistError {
  if (!(error instanceof ApiError)) {
    return { formError: ERROR_MESSAGES.network, fieldErrors: {} }
  }

  switch (error.errorCode) {
    case 'VALIDATION_FAILED': {
      const fieldErrors: Record<string, string> = {}
      for (const [field, messages] of Object.entries(error.fieldErrors ?? {})) {
        const [first] = messages
        if (first) fieldErrors[field] = first
      }
      return { formError: ERROR_MESSAGES.validation, fieldErrors }
    }
    case 'WAITLIST_EMAIL_DUPLICATE':
      return { formError: ERROR_MESSAGES.duplicate, fieldErrors: {} }
    case 'INTERNAL_ERROR':
      return { formError: ERROR_MESSAGES.internal, fieldErrors: {} }
    default:
      return error.errorCode === null
        ? { formError: ERROR_MESSAGES.network, fieldErrors: {} }
        : { formError: ERROR_MESSAGES.internal, fieldErrors: {} }
  }
}
