import { afterEach, describe, expect, it } from 'vitest'
import { ApiError, postJson } from './http'
import { json, mockApi, restoreHttp } from './http.testkit'
import { useAuthStore } from '../features/auth/store/auth-store'

afterEach(() => {
  restoreHttp()
  useAuthStore.getState().clearSession()
})

describe('interceptor de Authorization', () => {
  it('agrega Bearer + accessToken cuando hay sesión vigente', async () => {
    useAuthStore.getState().setSession('token-vigente', 3600)
    const requests = mockApi(json(201, { id: 'x' }))

    await postJson('/api/waitlist', {})

    expect(requests[0].headers.Authorization).toBe('Bearer token-vigente')
  })

  it('no agrega Authorization cuando no hay sesión', async () => {
    const requests = mockApi(json(201, { id: 'x' }))

    await postJson('/api/waitlist', {})

    expect(requests[0].headers.Authorization).toBeUndefined()
  })

  it('lee el token en el momento de la petición, no al cargar el módulo', async () => {
    const requests = mockApi(json(201, { id: 'x' }), json(201, { id: 'x' }))

    await postJson('/api/waitlist', {})
    expect(requests[0].headers.Authorization).toBeUndefined()

    useAuthStore.getState().setSession('token-posterior', 3600)
    await postJson('/api/waitlist', {})

    expect(requests[1].headers.Authorization).toBe('Bearer token-posterior')
  })
})

describe('postJson — mapa de errores por campo', () => {
  it('expone errors del 400 como fieldErrors en el ApiError', async () => {
    mockApi(
      json(400, {
        errorCode: 'VALIDATION_FAILED',
        errors: { email: ['El correo no tiene un formato válido.'] },
      }),
    )

    const error = await postJson('/api/waitlist', {}).catch((e) => e)

    expect(error).toBeInstanceOf(ApiError)
    expect((error as ApiError).fieldErrors).toEqual({
      email: ['El correo no tiene un formato válido.'],
    })
  })

  it('conserva todos los campos del mapa de validación', async () => {
    mockApi(
      json(400, {
        errorCode: 'VALIDATION_FAILED',
        errors: { fullName: ['Muy corto'], phone: ['Debe tener entre 7 y 15 dígitos.'] },
      }),
    )

    const error = await postJson('/api/waitlist', {}).catch((e) => e)

    expect((error as ApiError).fieldErrors).toEqual({
      fullName: ['Muy corto'],
      phone: ['Debe tener entre 7 y 15 dígitos.'],
    })
  })

  it('deja fieldErrors en null cuando la falla no trae mapa de campos', async () => {
    mockApi(json(409, { errorCode: 'WAITLIST_EMAIL_DUPLICATE' }))

    const error = await postJson('/api/waitlist', {}).catch((e) => e)

    expect(error).toBeInstanceOf(ApiError)
    expect((error as ApiError).fieldErrors).toBeNull()
  })

  it('mantiene status y errorCode intactos al agregar fieldErrors', async () => {
    mockApi(json(400, { errorCode: 'VALIDATION_FAILED', errors: { email: ['Inválido.'] } }))

    const error = await postJson('/api/waitlist', {}).catch((e) => e)

    expect((error as ApiError).status).toBe(400)
    expect((error as ApiError).errorCode).toBe('VALIDATION_FAILED')
  })
})
