import axios from 'axios'

export const api = axios.create()

export class ApiError extends Error {
  status: number
  errorCode: string | null

  constructor(status: number, errorCode: string | null = null) {
    super(`La solicitud de autenticación falló con status ${status}`)
    this.status = status
    this.errorCode = errorCode
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

function toApiError(error: unknown): unknown {
  if (!axios.isAxiosError(error) || !error.response) {
    return error
  }

  return new ApiError(error.response.status, readErrorCode(error.response.data))
}

export async function postJson<T>(path: string, body?: unknown): Promise<T> {
  try {
    const response = await api.post<T>(path, body)
    return response.data
  } catch (error) {
    throw toApiError(error)
  }
}
