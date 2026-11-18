import axios, { type AxiosRequestConfig } from 'axios'
import { useAuthStore } from '../features/auth/store/auth-store'

export const api = axios

api.interceptors.request.use(
  (config) => {
    const session = useAuthStore.getState().session

    if (session) {
      config.headers.Authorization = `Bearer ${session.accessToken}`
    }

    return config
  },
  undefined,
  { synchronous: true },
)

api.interceptors.response.use(
  (response) => response,
  (error) => Promise.reject(toApiError(error)),
)

export function readAccessToken(): string | null {
  return useAuthStore.getState().session?.accessToken ?? null
}

export function authConfig(): AxiosRequestConfig {
  const token = readAccessToken()
  return token ? { headers: { Authorization: `Bearer ${token}` } } : {}
}

export class ApiError extends Error {
  status: number
  errorCode: string | null
  fieldErrors: Record<string, string[]> | null

  constructor(
    status: number,
    errorCode: string | null = null,
    fieldErrors: Record<string, string[]> | null = null,
  ) {
    super(`La solicitud de autenticación falló con status ${status}`)
    this.status = status
    this.errorCode = errorCode
    this.fieldErrors = fieldErrors
  }
}

function readErrorCode(data: unknown): string | null {
  if (data !== null && typeof data === 'object' && 'errorCode' in data) {
    const { errorCode } = data as { errorCode: unknown }
    if (typeof errorCode === 'string') {
      return errorCode
    }
  }

  return null
}

function readFieldErrors(data: unknown): Record<string, string[]> | null {
  if (data === null || typeof data !== 'object' || !('errors' in data)) {
    return null
  }

  const { errors } = data as { errors: unknown }
  if (errors === null || typeof errors !== 'object' || Array.isArray(errors)) {
    return null
  }

  const fieldErrors: Record<string, string[]> = {}
  for (const [field, messages] of Object.entries(errors)) {
    if (
      Array.isArray(messages) &&
      messages.length > 0 &&
      messages.every((message) => typeof message === 'string')
    ) {
      fieldErrors[field] = messages as string[]
    }
  }

  return Object.keys(fieldErrors).length > 0 ? fieldErrors : null
}

function toApiError(error: unknown): unknown {
  if (!axios.isAxiosError(error) || !error.response) {
    return error
  }

  const { data } = error.response
  return new ApiError(error.response.status, readErrorCode(data), readFieldErrors(data))
}

export type HttpPost = <T>(path: string, body?: unknown) => Promise<T>

export async function postJson<T>(path: string, body?: unknown): Promise<T> {
  try {
    const response = await api.post<T>(path, body)
    return response.data
  } catch (error) {
    throw toApiError(error)
  }
}
