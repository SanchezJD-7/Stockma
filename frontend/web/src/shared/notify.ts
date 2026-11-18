import { create } from 'zustand'

export type NotifySeverity = 'error' | 'warning' | 'info' | 'success'

export interface Toast {
  id: number
  message: string
  severity: NotifySeverity
}

interface NotifyState {
  toasts: Toast[]
  push: (message: string, severity: NotifySeverity) => void
  dismiss: (id: number) => void
}

const AUTO_DISMISS_MS = 6000
let nextId = 0

export const useNotify = create<NotifyState>((set) => ({
  toasts: [],
  push: (message, severity) => {
    const id = ++nextId
    set((state) => ({ toasts: [...state.toasts, { id, message, severity }] }))
    setTimeout(() => useNotify.getState().dismiss(id), AUTO_DISMISS_MS)
  },
  dismiss: (id) => set((state) => ({ toasts: state.toasts.filter((toast) => toast.id !== id) })),
}))

export const notify = {
  error: (message: string) => useNotify.getState().push(message, 'error'),
  warning: (message: string) => useNotify.getState().push(message, 'warning'),
  info: (message: string) => useNotify.getState().push(message, 'info'),
  success: (message: string) => useNotify.getState().push(message, 'success'),
}
