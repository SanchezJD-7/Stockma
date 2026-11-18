import axios from 'axios'
import { config } from '../config'
import { authConfig } from '../shared/http'

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

class AuthService {
  async login(request: LoginRequest): Promise<LoginResult> {
    const { data } = await axios.post<LoginResponseBody>(
      `${config.API_URL}/api/auth/login`,
      request,
      authConfig(),
    )

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
    const { data } = await axios.post<ConfirmDeviceResult>(
      `${config.API_URL}/api/auth/confirm-device`,
      request,
      authConfig(),
    )

    return data
  }

  async refresh(request: RefreshRequest): Promise<RefreshResult> {
    const { data } = await axios.post<RefreshResult>(
      `${config.API_URL}/api/auth/refresh`,
      request,
      authConfig(),
    )

    return data
  }

  async logout(): Promise<void> {
    await axios.post(`${config.API_URL}/api/auth/logout`, undefined, authConfig())
  }
}

export default AuthService
