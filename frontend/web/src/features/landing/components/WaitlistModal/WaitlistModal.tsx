import CircularProgress from '@mui/material/CircularProgress'
import TextField from '@mui/material/TextField'
import { useRef, useState, type FormEvent } from 'react'
import WaitlistService from '../../../../services/waitlist-service'
import { FormAlert } from '../../../../styles/components/FormAlert'
import { Modal } from '../../../../styles/components/Modal'
import styles from './WaitlistModal.module.css'
import { SubmitButton } from './WaitlistModal.styles'
import { resolveWaitlistError } from './waitlist-errors'

interface WaitlistModalProps {
  open: boolean
  onClose: () => void
  onSuccess: () => void
}

export function WaitlistModal({ open, onClose, onSuccess }: WaitlistModalProps) {
  const [fullName, setFullName] = useState('')
  const [phone, setPhone] = useState('')
  const [email, setEmail] = useState('')
  const [submitting, setSubmitting] = useState(false)
  const [fieldErrors, setFieldErrors] = useState<Record<string, string>>({})
  const [formError, setFormError] = useState<string | null>(null)
  const inFlight = useRef(false)
  const waitlistService = new WaitlistService()

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    if (inFlight.current) return

    inFlight.current = true
    setSubmitting(true)
    setFieldErrors({})
    setFormError(null)

    try {
      await waitlistService.register({ fullName, phone, email })
      onSuccess()
    } catch (error) {
      const resolved = resolveWaitlistError(error)
      setFormError(resolved.formError)
      setFieldErrors(resolved.fieldErrors)
    } finally {
      inFlight.current = false
      setSubmitting(false)
    }
  }

  return (
    <Modal open={open} onClose={onClose} title='Únete a la lista de espera' closeButton>
      <form className={styles.form} onSubmit={handleSubmit} noValidate>
        <TextField
          label='Nombre completo'
          variant='filled'
          fullWidth
          value={fullName}
          onChange={(event) => setFullName(event.target.value)}
          error={Boolean(fieldErrors.fullName)}
          helperText={fieldErrors.fullName || undefined}
          disabled={submitting}
          autoComplete='name'
        />
        <div className={styles.phoneRow}>
          <span className={styles.phonePrefix}>🇨🇴 +57</span>
          <TextField
            className={styles.phoneField}
            label='Teléfono'
            variant='filled'
            type='tel'
            fullWidth
            value={phone}
            onChange={(event) => setPhone(event.target.value)}
            error={Boolean(fieldErrors.phone)}
            helperText={fieldErrors.phone || undefined}
            disabled={submitting}
            autoComplete='tel-national'
          />
        </div>
        <TextField
          label='Correo electrónico'
          variant='filled'
          type='email'
          fullWidth
          value={email}
          onChange={(event) => setEmail(event.target.value)}
          error={Boolean(fieldErrors.email)}
          helperText={fieldErrors.email || undefined}
          disabled={submitting}
          autoComplete='email'
        />
        {formError && <FormAlert>{formError}</FormAlert>}
        <SubmitButton type='submit' disabled={submitting} aria-busy={submitting}>
          {submitting ? (
            <>
              <CircularProgress size={16} color='inherit' aria-label='Enviando' />
              Registrando…
            </>
          ) : (
            'Unirme a la lista de espera'
          )}
        </SubmitButton>
      </form>
    </Modal>
  )
}
