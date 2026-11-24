import { Modal } from '../../../../styles/components/Modal'
import { AcceptButton, SuccessBody } from './WaitlistSuccessModal.styles'

interface WaitlistSuccessModalProps {
  open: boolean
  onClose: () => void
}

export function WaitlistSuccessModal({ open, onClose }: WaitlistSuccessModalProps) {
  return (
    <Modal open={open} onClose={onClose} title='¡Listo! Ya estás en la lista de espera'>
      <SuccessBody>Te avisaremos por correo muy pronto nuestra fecha de lanzamiento</SuccessBody>
      <AcceptButton type='button' onClick={onClose}>
        Cerrar
      </AcceptButton>
    </Modal>
  )
}
