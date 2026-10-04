import { renderHook } from '@testing-library/react'
import { useSessionRenewer } from './useSessionRenewer'
import { useAuthStore } from '../store/auth-store'
import { refresh } from '../api/auth-api'

vi.mock('../api/auth-api', () => ({
  refresh: vi.fn(),
}))

const refreshMock = vi.mocked(refresh)

function setSession(accessToken: string, expiresIn: number) {
  useAuthStore.getState().setSession(accessToken, expiresIn)
}

function clearSession() {
  useAuthStore.getState().clearSession()
}

describe('useSessionRenewer', () => {
  beforeEach(() => {
    clearSession()
    refreshMock.mockReset()
  })

  it('does nothing when there is no session', () => {
    renderHook(() => useSessionRenewer())

    expect(refreshMock).not.toHaveBeenCalled()
  })

  it('does not renew immediately when the session is fresh', () => {
    setSession('token-fresco', 900)

    renderHook(() => useSessionRenewer())

    expect(refreshMock).not.toHaveBeenCalled()
  })
})
