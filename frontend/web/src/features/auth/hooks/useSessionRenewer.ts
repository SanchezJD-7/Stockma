import { useEffect, useState } from 'react'
import { useAuthStore } from '../store/auth-store'
import { AuthService } from '../../../services/auth-service'
import { getOrCreateDeviceId } from '../utils/device'

const RENEW_THRESHOLD_MS = 2 * 60 * 1000
const IDLE_TIMEOUT_MS = 2 * 60 * 60 * 1000
const CHECK_INTERVAL_MS = 30 * 1000

export function useSessionRenewer() {
  const session = useAuthStore((state) => state.session)
  const refreshKey = useAuthStore((state) => state.refreshKey)
  const setSession = useAuthStore((state) => state.setSession)
  const clearSession = useAuthStore((state) => state.clearSession)
  const [lastActivityAt, setLastActivityAt] = useState(Date.now())

  useEffect(() => {
    const update = () => setLastActivityAt(Date.now())
    window.addEventListener('pointerdown', update)
    window.addEventListener('keydown', update)
    return () => {
      window.removeEventListener('pointerdown', update)
      window.removeEventListener('keydown', update)
    }
  }, [])

  useEffect(() => {
    if (!session) return

    const interval = setInterval(async () => {
      const current = useAuthStore.getState().session
      if (!current) return

      const msUntilExpiry = current.expiresAt - Date.now()
      const hasRecentActivity = Date.now() - lastActivityAt < IDLE_TIMEOUT_MS

      if (msUntilExpiry > RENEW_THRESHOLD_MS || !hasRecentActivity) {
        return
      }

      try {
        const authService = new AuthService()
        await navigator.locks.request('stockma-refresh', async () => {
          const result = await authService.refresh({ deviceId: getOrCreateDeviceId() })
          setSession(result.accessToken, result.expiresIn)
        })
      } catch {
        clearSession()
      }
    }, CHECK_INTERVAL_MS)

    return () => clearInterval(interval)
  }, [session, refreshKey, setSession, clearSession, lastActivityAt])

  useEffect(() => {
    if (!session) return

    const idleTimeout = setTimeout(clearSession, IDLE_TIMEOUT_MS)
    return () => clearTimeout(idleTimeout)
  }, [session, clearSession, lastActivityAt])
}
