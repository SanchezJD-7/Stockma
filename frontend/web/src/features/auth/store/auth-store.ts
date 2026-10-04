import { create } from 'zustand'

export const AUTH_SESSION_STORAGE_KEY = 'stockma.auth.session'

export interface AuthSession {
  accessToken: string
  expiresAt: number
}

interface AuthState {
  session: AuthSession | null
  refreshKey: number
  setSession: (accessToken: string, expiresIn: number) => void
  clearSession: () => void
}

function isAuthSession(value: unknown): value is AuthSession {
  if (typeof value !== 'object' || value === null) {
    return false
  }

  const { accessToken, expiresAt } = value as Record<string, unknown>
  return (
    typeof accessToken === 'string' &&
    accessToken.length > 0 &&
    typeof expiresAt === 'number' &&
    Number.isFinite(expiresAt)
  )
}

function removeStoredSession(): void {
  try {
    localStorage.removeItem(AUTH_SESSION_STORAGE_KEY)
  } catch {
    return
  }
}

export function readStoredSession(): AuthSession | null {
  let raw: string | null

  try {
    raw = localStorage.getItem(AUTH_SESSION_STORAGE_KEY)
  } catch {
    return null
  }

  if (!raw) {
    return null
  }

  try {
    const session: unknown = JSON.parse(raw)

    if (isAuthSession(session) && session.expiresAt > Date.now()) {
      return session
    }
  } catch {
    removeStoredSession()
    return null
  }

  removeStoredSession()
  return null
}

function writeStoredSession(session: AuthSession): void {
  try {
    localStorage.setItem(AUTH_SESSION_STORAGE_KEY, JSON.stringify(session))
  } catch {
    return
  }
}

export const useAuthStore = create<AuthState>((set) => ({
  session: readStoredSession(),
  refreshKey: 0,
  setSession: (accessToken, expiresIn) => {
    const session: AuthSession = { accessToken, expiresAt: Date.now() + expiresIn * 1000 }
    writeStoredSession(session)
    set({ session, refreshKey: Date.now() })
  },
  clearSession: () => {
    removeStoredSession()
    set({ session: null })
  },
}))
