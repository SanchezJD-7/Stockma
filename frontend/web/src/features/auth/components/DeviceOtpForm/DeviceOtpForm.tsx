import ArrowBackIcon from '@mui/icons-material/ArrowBack'
import ArrowForwardIcon from '@mui/icons-material/ArrowForward'
import Alert from '@mui/material/Alert'
import TextField from '@mui/material/TextField'
import { useState, type FormEvent } from 'react'
import { AuthService } from '../../../../services/auth-service'
import {
  ActionRow,
  BackButton,
  FormBody,
  FormTitle,
  Hint,
  SubmitButton,
} from '../../styles/auth-form.styles'
import { getFingerprint } from '../../utils/device'
import { useFlashError } from '../../hooks/useFlashError'
import { authErrorMessage } from '../../utils/auth-error-message'

interface DeviceOtpFormProps {
  email: string
  deviceId: string
  onAuthenticated: (accessToken: string, expiresIn: number) => void
  onBack: () => void
}

export function DeviceOtpForm({ email, deviceId, onAuthenticated, onBack }: DeviceOtpFormProps) {
  const authService = new AuthService()
  const [otp, setOtp] = useState('')
  const { error, showError, clearError } = useFlashError()
  const [submitting, setSubmitting] = useState(false)

  async function handleSubmit(event: FormEvent) {
    event.preventDefault()
    clearError()
    setSubmitting(true)

    try {
      const result = await authService.confirmDevice({
        email,
        deviceId,
        fingerprint: getFingerprint(),
        otp,
      })
      onAuthenticated(result.accessToken, result.expiresIn)
    } catch (err) {
      showError(authErrorMessage(err))
    } finally {
      setSubmitting(false)
    }
  }

  return (
    <form onSubmit={handleSubmit}>
      <FormTitle>Confirma el dispositivo</FormTitle>
      <FormBody>
        <Hint>Te enviamos un código por SMS. Ingrésalo para continuar.</Hint>
        <TextField
          label='Código'
          variant='filled'
          value={otp}
          onChange={(event) => setOtp(event.target.value)}
          autoComplete='one-time-code'
          slotProps={{ htmlInput: { maxLength: 6, inputMode: 'numeric' } }}
          fullWidth
        />
        {error && <Alert severity='error'>{error}</Alert>}
        <ActionRow>
          <BackButton type='button' onClick={onBack} disabled={submitting}>
            <ArrowBackIcon fontSize='small' />
            Volver
          </BackButton>
          <SubmitButton type='submit' disabled={submitting}>
            {submitting ? 'Validando…' : 'Continuar'}
            <ArrowForwardIcon fontSize='small' />
          </SubmitButton>
        </ActionRow>
      </FormBody>
    </form>
  )
}
