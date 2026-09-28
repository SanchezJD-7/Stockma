import Alert from '@mui/material/Alert'
import TextField from '@mui/material/TextField'
import { useState, type FormEvent } from 'react'
import { login } from '../../api/auth-api'
import { FormBody, FormTitle, SubmitButton } from '../../styles/auth-form.styles'
import { useFlashError } from '../../hooks/useFlashError'
import { authErrorMessage } from '../../utils/auth-error-message'

interface LoginPageProps {
  initialEmail?: string
  deviceId: string
  onAuthenticated: (accessToken: string, expiresIn: number) => void
  onRequiresConfirmation: (email: string) => void
}

export function LoginPage({
  initialEmail = '',
  deviceId,
  onAuthenticated,
  onRequiresConfirmation,
}: LoginPageProps) {
  const [email, setEmail] = useState(initialEmail)
  const [password, setPassword] = useState('')
  const { error, showError, clearError } = useFlashError()
  const [submitting, setSubmitting] = useState(false)

  async function handleSubmit(event: FormEvent) {
    event.preventDefault()
    clearError()
    setSubmitting(true)

    try {
      const result = await login({ email, password, deviceId })

      if (result.kind === 'requires-confirmation') {
        onRequiresConfirmation(email)
        return
      }

      onAuthenticated(result.accessToken, result.expiresIn)
    } catch (err) {
      showError(authErrorMessage(err))
    } finally {
      setSubmitting(false)
    }
  }

  return (
    <form onSubmit={handleSubmit}>
      <FormTitle>Iniciar sesión</FormTitle>
      <FormBody>
        <TextField
          label='Email'
          variant='filled'
          type='email'
          autoComplete='email'
          value={email}
          onChange={(event) => setEmail(event.target.value)}
          fullWidth
        />
        <TextField
          label='Contraseña'
          variant='filled'
          type='password'
          autoComplete='current-password'
          value={password}
          onChange={(event) => setPassword(event.target.value)}
          fullWidth
        />
        {error && <Alert severity='error'>{error}</Alert>}
        <SubmitButton type='submit' disabled={submitting}>
          {submitting ? 'Ingresando…' : 'Ingresar'}
        </SubmitButton>
      </FormBody>
    </form>
  )
}
