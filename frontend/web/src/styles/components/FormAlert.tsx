import Alert from '@mui/material/Alert'
import type { ReactNode } from 'react'

export type AlertSeverity = 'error' | 'warning' | 'info' | 'success'

interface FormAlertProps {
  severity?: AlertSeverity
  onClose?: () => void
  children: ReactNode
}

export function FormAlert({ severity = 'error', onClose, children }: FormAlertProps) {
  return (
    <Alert severity={severity} role='alert' onClose={onClose}>
      {children}
    </Alert>
  )
}
