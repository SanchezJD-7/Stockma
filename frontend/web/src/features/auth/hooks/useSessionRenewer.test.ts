import { renderHook } from '@testing-library/react'
import { useSessionRenewer } from './useSessionRenewer'
import { useAuthStore } from '../store/auth-store'
import { json, mockApi, restoreHttp } from '../../../shared/http.testkit'

function setSession(accessToken: string, expiresIn: number) {
  useAuthStore.getState().setSession(accessToken, expiresIn)
}

function clearSession() {
  useAuthStore.getState().clearSession()
}

describe('useSessionRenewer', () => {
  let requests: ReturnType<typeof mockApi>

  beforeEach(() => {
    clearSession()
    requests = mockApi(json(200, { accessToken: 'renovado', expiresIn: 900 }))
  })

  afterEach(() => {
    restoreHttp()
  })

  it('does nothing when there is no session', () => {
    renderHook(() => useSessionRenewer())

    expect(requests).toHaveLength(0)
  })

  it('does not renew immediately when the session is fresh', () => {
    setSession('token-fresco', 900)

    renderHook(() => useSessionRenewer())

    expect(requests).toHaveLength(0)
  })
})
