import { afterEach, describe, expect, it } from 'vitest'
import WaitlistService from './waitlist-service'
import { ApiError } from '../shared/http'
import { json, mockApi, restoreHttp } from '../shared/http.testkit'
import { useAuthStore } from '../features/auth/store/auth-store'

const waitlistService = new WaitlistService()

afterEach(() => {
  restoreHttp()
  useAuthStore.getState().clearSession()
})

describe('WaitlistService.register', () => {
  it('envía fullName, countryCode +57, phone y email a POST /api/waitlist', async () => {
    const requests = mockApi(
      json(201, { id: '3b8d1f60-2c14-4a9e-8f77-0a5b3e2d9c41', email: 'juan@ejemplo.com' }),
    )

    const result = await waitlistService.register({
      fullName: 'Juan Pérez',
      phone: '3001234567',
      email: 'Juan@Ejemplo.com',
    })

    expect(requests).toHaveLength(1)
    expect(requests[0].url).toBe('/api/waitlist')
    expect(requests[0].method).toBe('post')
    expect(JSON.parse(String(requests[0].data))).toEqual({
      fullName: 'Juan Pérez',
      countryCode: '+57',
      phone: '3001234567',
      email: 'Juan@Ejemplo.com',
    })
    expect(result).toEqual({
      id: '3b8d1f60-2c14-4a9e-8f77-0a5b3e2d9c41',
      email: 'juan@ejemplo.com',
    })
  })

  it('propaga el ApiError con status y errorCode del 409 duplicado, sin traducirlo', async () => {
    mockApi(json(409, { errorCode: 'WAITLIST_EMAIL_DUPLICATE' }))

    const error = await waitlistService
      .register({
        fullName: 'Juan Pérez',
        phone: '3001234567',
        email: 'juan@ejemplo.com',
      })
      .catch((e) => e)

    expect(error).toBeInstanceOf(ApiError)
    expect((error as ApiError).status).toBe(409)
    expect((error as ApiError).errorCode).toBe('WAITLIST_EMAIL_DUPLICATE')
  })

  it('propaga VALIDATION_FAILED de un 400 tal cual, sin mensaje amigable propio', async () => {
    mockApi(
      json(400, {
        errorCode: 'VALIDATION_FAILED',
        errors: { email: ['El correo no tiene un formato válido.'] },
      }),
    )

    const error = await waitlistService
      .register({
        fullName: 'J',
        phone: '3001234567',
        email: 'sin-arroba',
      })
      .catch((e) => e)

    expect(error).toBeInstanceOf(ApiError)
    expect((error as ApiError).status).toBe(400)
    expect((error as ApiError).errorCode).toBe('VALIDATION_FAILED')
  })

  it('envía Authorization: Bearer con el token vigente mediante el interceptor compartido', async () => {
    useAuthStore.getState().setSession('token-vigente', 3600)
    const requests = mockApi(json(201, { id: 'x', email: 'a@b.com' }))

    await waitlistService.register({
      fullName: 'Juan Pérez',
      phone: '3001234567',
      email: 'a@b.com',
    })

    expect(requests[0].headers.Authorization).toBe('Bearer token-vigente')
  })

  it('no envía Authorization cuando no hay sesión', async () => {
    const requests = mockApi(json(201, { id: 'x', email: 'a@b.com' }))

    await waitlistService.register({
      fullName: 'Juan Pérez',
      phone: '3001234567',
      email: 'a@b.com',
    })

    expect(requests[0].headers.Authorization).toBeUndefined()
  })
})
