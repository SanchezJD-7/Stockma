import { Modal } from '../../../../styles/components/Modal'
import { SubmitButton } from '../WaitlistModal/WaitlistModal.styles'
import { SuccessBody, SuccessIcon } from './WaitlistSuccessModal.styles'

interface WaitlistSuccessModalProps {
  open: boolean
  onClose: () => void
}

export function WaitlistSuccessModal({ open, onClose }: WaitlistSuccessModalProps) {
  return (
    <Modal open={open} onClose={onClose} title='¡Listo! Ya estás en la lista de espera'>
      <SuccessIcon viewBox='0 0 64 64' aria-hidden='true' focusable='false'>
        <circle cx='32' cy='32' r='30' fill='var(--color-tint-green)' />
        <path
          d='M18 32.5 27 41l19-19'
          fill='none'
          stroke='currentColor'
          strokeWidth='4'
          strokeLinecap='round'
          strokeLinejoin='round'
        />
      </SuccessIcon>
      <SuccessBody>Te avisaremos por correo muy pronto nuestra fecha de lanzamiento</SuccessBody>
      <SubmitButton type='button' onClick={onClose}>
        Cerrar
      </SubmitButton>
    </Modal>
  )
}
