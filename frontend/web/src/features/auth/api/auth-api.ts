export interface LoginRequest {
  email: string
  password: string
  deviceId: string
}

export type LoginResult =
  | { kind: 'authenticated'; accessToken: string; expiresIn: number }
  | { kind: 'requires-confirmation' }

export interface ConfirmDeviceRequest {
  email: string
  deviceId: string
  fingerprint: string
  otp: string
}

export interface ConfirmDeviceResult {
  accessToken: string
  expiresIn: number
  deviceTrusted: boolean
}

export class ApiError extends Error {
  status: number
  errorCode: string | null

  constructor(status: number, errorCode: string | null = null) {
    super(`La solicitud de autenticación falló con status ${status}`)
    this.status = status
    this.errorCode = errorCode
  }
}

interface LoginResponseBody {
  accessToken?: string
  expiresIn?: number
  requiresDeviceConfirmation?: boolean
}

async function readErrorCode(response: Response): Promise<string | null> {
  try {
    const problem = (await response.json()) as { errorCode?: unknown }
    return typeof problem.errorCode === 'string' ? problem.errorCode : null
  } catch {
    return null
  }
}

async function postJson<T>(path: string, body: unknown): Promise<T> {
  const response = await fetch(path, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(body),
  })

  if (!response.ok) {
    throw new ApiError(response.status, await readErrorCode(response))
  }

  return response.json() as Promise<T>
}

export async function login(request: LoginRequest): Promise<LoginResult> {
  const data = await postJson<LoginResponseBody>('/api/auth/login', request)

  if (data.requiresDeviceConfirmation) {
    return { kind: 'requires-confirmation' }
  }

  const { accessToken, expiresIn } = data

  if (typeof accessToken !== 'string' || accessToken === '' || typeof expiresIn !== 'number') {
    throw new Error('El login respondió 200 sin token ni pedido de confirmación')
  }

  return { kind: 'authenticated', accessToken, expiresIn }
}

export async function confirmDevice(request: ConfirmDeviceRequest): Promise<ConfirmDeviceResult> {
  return postJson<ConfirmDeviceResult>('/api/auth/confirm-device', request)
}

export interface RefreshRequest {
  deviceId: string
}

export interface RefreshResult {
  accessToken: string
  expiresIn: number
}

export async function refresh(request: RefreshRequest): Promise<RefreshResult> {
  const response = await fetch('/api/auth/refresh', {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(request),
  })

  if (!response.ok) {
    throw new ApiError(response.status, await readErrorCode(response))
  }

  return response.json() as Promise<RefreshResult>
}

export async function logout(): Promise<void> {
  const response = await fetch('/api/auth/logout', {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
  })

  if (!response.ok) {
    throw new ApiError(response.status, await readErrorCode(response))
  }
}
