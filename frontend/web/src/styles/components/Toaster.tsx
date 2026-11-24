import { createPortal } from 'react-dom'
import { useNotify } from '../../shared/notify'
import { FormAlert } from './FormAlert'
import { Toast, Viewport } from './Toaster.styles'

export function Toaster() {
  const toasts = useNotify((state) => state.toasts)
  const dismiss = useNotify((state) => state.dismiss)

  if (toasts.length === 0) return null

  return createPortal(
    <Viewport>
      {toasts.map((toast) => (
        <Toast key={toast.id}>
          <FormAlert severity={toast.severity} onClose={() => dismiss(toast.id)}>
            {toast.message}
          </FormAlert>
        </Toast>
      ))}
    </Viewport>,
    document.body,
  )
}
