import { afterEach, describe, expect, it } from 'vitest'
import { AUTH_SESSION_STORAGE_KEY, readStoredSession, useAuthStore } from './auth-store'

afterEach(() => {
  localStorage.clear()
  useAuthStore.setState({ session: null })
})

describe('useAuthStore', () => {
  it('no tiene sesión por defecto', () => {
    expect(useAuthStore.getState().session).toBeNull()
  })

  it('guarda el token y lo persiste en localStorage', () => {
    useAuthStore.getState().setSession('token-123', 3600)

    const session = useAuthStore.getState().session
    expect(session?.accessToken).toBe('token-123')
    expect(session?.expiresAt).toBeGreaterThan(Date.now())

    const stored = JSON.parse(localStorage.getItem(AUTH_SESSION_STORAGE_KEY)!)
    expect(stored.accessToken).toBe('token-123')
  })
})

describe('readStoredSession', () => {
  it('devuelve la sesión guardada mientras no venció', () => {
    const session = { accessToken: 'vigente', expiresAt: Date.now() + 60_000 }
    localStorage.setItem(AUTH_SESSION_STORAGE_KEY, JSON.stringify(session))

    expect(readStoredSession()).toEqual(session)
  })

  it('descarta y borra una sesión vencida', () => {
    const session = { accessToken: 'vencido', expiresAt: Date.now() - 1 }
    localStorage.setItem(AUTH_SESSION_STORAGE_KEY, JSON.stringify(session))

    expect(readStoredSession()).toBeNull()
    expect(localStorage.getItem(AUTH_SESSION_STORAGE_KEY)).toBeNull()
  })

  it.each([
    ['un objeto vacío', '{}'],
    [
      'un token que no es texto',
      JSON.stringify({ accessToken: 1, expiresAt: Date.now() + 60_000 }),
    ],
    ['un token vacío', JSON.stringify({ accessToken: '', expiresAt: Date.now() + 60_000 })],
    ['un vencimiento que no es número', JSON.stringify({ accessToken: 'x', expiresAt: 'mañana' })],
    ['algo que no es JSON', '{roto'],
  ])('descarta y borra %s', (_caso, raw) => {
    localStorage.setItem(AUTH_SESSION_STORAGE_KEY, raw)

    expect(readStoredSession()).toBeNull()
    expect(localStorage.getItem(AUTH_SESSION_STORAGE_KEY)).toBeNull()
  })
})

describe('clearSession', () => {
  it('borra la sesión del estado y de localStorage', () => {
    useAuthStore.getState().setSession('token-123', 3600)

    useAuthStore.getState().clearSession()

    expect(useAuthStore.getState().session).toBeNull()
    expect(localStorage.getItem(AUTH_SESSION_STORAGE_KEY)).toBeNull()
  })
})
