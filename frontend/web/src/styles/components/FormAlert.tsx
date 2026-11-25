import type { ReactNode } from 'react'
import { AlertAccent, AlertSurface } from './FormAlert.styles'

export type AlertSeverity = 'error' | 'warning' | 'info' | 'success'

interface FormAlertProps {
  severity?: AlertSeverity
  onClose?: () => void
  children: ReactNode
}

export function FormAlert({ severity = 'error', onClose, children }: FormAlertProps) {
  return (
    <AlertSurface severity={severity} role='alert' onClose={onClose} variant='filled'>
      {children}
      <AlertAccent aria-hidden='true' />
    </AlertSurface>
  )
}
