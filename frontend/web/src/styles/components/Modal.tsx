import { useEffect, useId, type MouseEvent as ReactMouseEvent, type ReactNode } from 'react'
import { Backdrop, CloseButton, Dialog, Subtitle, Title } from './Modal.styles'

interface ModalProps {
  open: boolean
  onClose: () => void
  title: ReactNode
  subtitle?: ReactNode
  closeButton?: boolean
  children: ReactNode
}

export function Modal({
  open,
  onClose,
  title,
  subtitle,
  closeButton = false,
  children,
}: ModalProps) {
  const titleId = useId()
  const subtitleId = useId()

  useEffect(() => {
    if (!open) return

    const handleKeyDown = (event: KeyboardEvent) => {
      if (event.key === 'Escape') onClose()
    }

    document.addEventListener('keydown', handleKeyDown)
    return () => document.removeEventListener('keydown', handleKeyDown)
  }, [open, onClose])

  if (!open) return null

  function handleBackdropClick(event: ReactMouseEvent<HTMLDivElement>) {
    if (event.target === event.currentTarget) onClose()
  }

  return (
    <Backdrop onClick={handleBackdropClick}>
      <Dialog
        role='dialog'
        aria-modal='true'
        aria-labelledby={titleId}
        aria-describedby={subtitle ? subtitleId : undefined}
      >
        {closeButton && (
          <CloseButton type='button' aria-label='Cerrar' onClick={onClose}>
            ×
          </CloseButton>
        )}
        <Title id={titleId} $withClose={closeButton}>
          {title}
        </Title>
        {subtitle && <Subtitle id={subtitleId}>{subtitle}</Subtitle>}
        {children}
      </Dialog>
    </Backdrop>
  )
}
