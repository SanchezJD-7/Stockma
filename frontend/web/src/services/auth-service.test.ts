import { afterEach, describe, expect, it } from 'vitest'
import { AuthService } from './auth-service'
import { ApiError } from '../shared/http'
import { json, mockApi, restoreHttp } from '../shared/http.testkit'

const authService = new AuthService()

afterEach(() => {
  restoreHttp()
})

describe('AuthService.login', () => {
  it('envía email, password y deviceId, y devuelve el token', async () => {
    const requests = mockApi(json(200, { accessToken: 'abc', expiresIn: 3600 }))

    const result = await authService.login({
      email: 'a@a.com',
      password: 'p',
      deviceId: 'd'.repeat(16),
    })

    expect(requests).toHaveLength(1)
    expect(requests[0].url).toBe('/api/auth/login')
    expect(requests[0].method).toBe('post')
    expect(JSON.parse(String(requests[0].data))).toEqual({
      email: 'a@a.com',
      password: 'p',
      deviceId: 'd'.repeat(16),
    })
    expect(result).toEqual({ kind: 'authenticated', accessToken: 'abc', expiresIn: 3600 })
  })

  it('devuelve requires-confirmation cuando el dispositivo es desconocido', async () => {
    mockApi(json(200, { requiresDeviceConfirmation: true }))

    const result = await authService.login({
      email: 'a@a.com',
      password: 'p',
      deviceId: 'd'.repeat(16),
    })

    expect(result).toEqual({ kind: 'requires-confirmation' })
  })

  it('lanza ApiError con el status ante una falla', async () => {
    mockApi(json(401, { errorCode: 'AUTH_INVALID_CREDENTIALS' }))

    const error = await authService
      .login({ email: 'a@a.com', password: 'p', deviceId: 'd'.repeat(16) })
      .catch((e) => e)

    expect(error).toBeInstanceOf(ApiError)
    expect((error as ApiError).status).toBe(401)
  })

  it('lleva en el ApiError el errorCode del problem+json', async () => {
    mockApi(json(403, { errorCode: 'AUTH_PHONE_NOT_ENROLLED' }))

    const error = await authService
      .login({ email: 'a@a.com', password: 'p', deviceId: 'd'.repeat(16) })
      .catch((e) => e)

    expect((error as ApiError).errorCode).toBe('AUTH_PHONE_NOT_ENROLLED')
  })

  it('rechaza un 200 que no trae token ni pide confirmación, en vez de guardar una sesión rota', async () => {
    mockApi(json(200, {}))

    await expect(
      authService.login({ email: 'a@a.com', password: 'p', deviceId: 'd'.repeat(16) }),
    ).rejects.toThrow()
  })

  it('deja el errorCode en null si la falla no trae un body JSON', async () => {
    mockApi(json(502, null))

    const error = await authService
      .login({ email: 'a@a.com', password: 'p', deviceId: 'd'.repeat(16) })
      .catch((e) => e)

    expect(error).toBeInstanceOf(ApiError)
    expect((error as ApiError).status).toBe(502)
    expect((error as ApiError).errorCode).toBeNull()
  })
})

describe('AuthService.confirmDevice', () => {
  it('envía email, deviceId, fingerprint y otp', async () => {
    const requests = mockApi(
      json(200, { accessToken: 'abc', expiresIn: 3600, deviceTrusted: true }),
    )

    const result = await authService.confirmDevice({
      email: 'a@a.com',
      deviceId: 'd'.repeat(16),
      fingerprint: 'fp',
      otp: '123456',
    })

    expect(JSON.parse(String(requests[0].data))).toEqual({
      email: 'a@a.com',
      deviceId: 'd'.repeat(16),
      fingerprint: 'fp',
      otp: '123456',
    })
    expect(result.deviceTrusted).toBe(true)
  })

  it('lanza ApiError 401 cuando el otp es rechazado', async () => {
    mockApi(json(401, { errorCode: 'AUTH_OTP_REJECTED' }))

    const error = await authService
      .confirmDevice({
        email: 'a@a.com',
        deviceId: 'd'.repeat(16),
        fingerprint: 'fp',
        otp: '000000',
      })
      .catch((e) => e)

    expect(error).toBeInstanceOf(ApiError)
    expect((error as ApiError).status).toBe(401)
  })
})
