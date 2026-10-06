import { postJson } from '../shared/http'

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

export interface RefreshRequest {
  deviceId: string
}

export interface RefreshResult {
  accessToken: string
  expiresIn: number
}

interface LoginResponseBody {
  accessToken?: string
  expiresIn?: number
  requiresDeviceConfirmation?: boolean
}

export class AuthService {
  async login(request: LoginRequest): Promise<LoginResult> {
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

  async confirmDevice(request: ConfirmDeviceRequest): Promise<ConfirmDeviceResult> {
    return postJson<ConfirmDeviceResult>('/api/auth/confirm-device', request)
  }

  async refresh(request: RefreshRequest): Promise<RefreshResult> {
    return postJson<RefreshResult>('/api/auth/refresh', request)
  }

  async logout(): Promise<void> {
    await postJson<void>('/api/auth/logout')
  }
}
