import { afterEach, describe, expect, it, vi } from 'vitest'
import { ApiError, confirmDevice, login } from './auth-api'

function jsonResponse(status: number, body: unknown): Response {
  return { ok: status >= 200 && status < 300, status, json: async () => body } as Response
}

afterEach(() => {
  vi.unstubAllGlobals()
})

describe('login', () => {
  it('envía email, password y deviceId, y devuelve el token', async () => {
    const fetchMock = vi
      .fn()
      .mockResolvedValue(jsonResponse(200, { accessToken: 'abc', expiresIn: 3600 }))
    vi.stubGlobal('fetch', fetchMock)

    const result = await login({ email: 'a@a.com', password: 'p', deviceId: 'd'.repeat(16) })

    expect(fetchMock).toHaveBeenCalledWith(
      '/api/auth/login',
      expect.objectContaining({ method: 'POST' }),
    )
    expect(JSON.parse(fetchMock.mock.calls[0][1].body)).toEqual({
      email: 'a@a.com',
      password: 'p',
      deviceId: 'd'.repeat(16),
    })
    expect(result).toEqual({ kind: 'authenticated', accessToken: 'abc', expiresIn: 3600 })
  })

  it('devuelve requires-confirmation cuando el dispositivo es desconocido', async () => {
    vi.stubGlobal(
      'fetch',
      vi.fn().mockResolvedValue(jsonResponse(200, { requiresDeviceConfirmation: true })),
    )

    const result = await login({ email: 'a@a.com', password: 'p', deviceId: 'd'.repeat(16) })

    expect(result).toEqual({ kind: 'requires-confirmation' })
  })

  it('lanza ApiError con el status ante una falla', async () => {
    vi.stubGlobal(
      'fetch',
      vi.fn().mockResolvedValue(jsonResponse(401, { errorCode: 'AUTH_INVALID_CREDENTIALS' })),
    )

    const error = await login({ email: 'a@a.com', password: 'p', deviceId: 'd'.repeat(16) }).catch(
      (e) => e,
    )

    expect(error).toBeInstanceOf(ApiError)
    expect((error as ApiError).status).toBe(401)
  })

  it('lleva en el ApiError el errorCode del problem+json', async () => {
    vi.stubGlobal(
      'fetch',
      vi.fn().mockResolvedValue(jsonResponse(403, { errorCode: 'AUTH_PHONE_NOT_ENROLLED' })),
    )

    const error = await login({ email: 'a@a.com', password: 'p', deviceId: 'd'.repeat(16) }).catch(
      (e) => e,
    )

    expect((error as ApiError).errorCode).toBe('AUTH_PHONE_NOT_ENROLLED')
  })

  it('rechaza un 200 que no trae token ni pide confirmación, en vez de guardar una sesión rota', async () => {
    vi.stubGlobal('fetch', vi.fn().mockResolvedValue(jsonResponse(200, {})))

    await expect(
      login({ email: 'a@a.com', password: 'p', deviceId: 'd'.repeat(16) }),
    ).rejects.toThrow()
  })

  it('deja el errorCode en null si la falla no trae un body JSON', async () => {
    vi.stubGlobal(
      'fetch',
      vi.fn().mockResolvedValue({
        ok: false,
        status: 502,
        json: async () => {
          throw new SyntaxError('Unexpected token <')
        },
      } as unknown as Response),
    )

    const error = await login({ email: 'a@a.com', password: 'p', deviceId: 'd'.repeat(16) }).catch(
      (e) => e,
    )

    expect(error).toBeInstanceOf(ApiError)
    expect((error as ApiError).status).toBe(502)
    expect((error as ApiError).errorCode).toBeNull()
  })
})

describe('confirmDevice', () => {
  it('envía email, deviceId, fingerprint y otp', async () => {
    const fetchMock = vi
      .fn()
      .mockResolvedValue(
        jsonResponse(200, { accessToken: 'abc', expiresIn: 3600, deviceTrusted: true }),
      )
    vi.stubGlobal('fetch', fetchMock)

    const result = await confirmDevice({
      email: 'a@a.com',
      deviceId: 'd'.repeat(16),
      fingerprint: 'fp',
      otp: '123456',
    })

    expect(JSON.parse(fetchMock.mock.calls[0][1].body)).toEqual({
      email: 'a@a.com',
      deviceId: 'd'.repeat(16),
      fingerprint: 'fp',
      otp: '123456',
    })
    expect(result.deviceTrusted).toBe(true)
  })

  it('lanza ApiError 401 cuando el otp es rechazado', async () => {
    vi.stubGlobal(
      'fetch',
      vi.fn().mockResolvedValue(jsonResponse(401, { errorCode: 'AUTH_OTP_REJECTED' })),
    )

    const error = await confirmDevice({
      email: 'a@a.com',
      deviceId: 'd'.repeat(16),
      fingerprint: 'fp',
      otp: '000000',
    }).catch((e) => e)

    expect(error).toBeInstanceOf(ApiError)
    expect((error as ApiError).status).toBe(401)
  })
})
