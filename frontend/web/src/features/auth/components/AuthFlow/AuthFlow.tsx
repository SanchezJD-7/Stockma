import { useMemo, useState } from 'react'
import { MutedText, PageTitle } from '../../../../styles/components/texts'
import { AuthLayout } from '../AuthLayout/AuthLayout'
import { SessionScreen } from './AuthFlow.styles'
import { useAuthStore } from '../../store/auth-store'
import { getOrCreateDeviceId } from '../../utils/device'
import { useSessionRenewer } from '../../hooks/useSessionRenewer'
import { DeviceOtpForm } from '../DeviceOtpForm/DeviceOtpForm'
import { LoginPage } from '../LoginPage/LoginPage'

type Screen =
  { name: 'login'; email?: string } | { name: 'otp'; email: string } | { name: 'session' }

const RESEND_COOLDOWN_MS = 2 * 60_000

export function AuthFlow() {
  const deviceId = useMemo(() => getOrCreateDeviceId(), [])
  const session = useAuthStore((state) => state.session)
  const setSession = useAuthStore((state) => state.setSession)
  const [screen, setScreen] = useState<Screen>(session ? { name: 'session' } : { name: 'login' })
  const [otpSentAt, setOtpSentAt] = useState<number | null>(null)

  useSessionRenewer()

  function handleAuthenticated(accessToken: string, expiresIn: number) {
    setSession(accessToken, expiresIn)
    setScreen({ name: 'session' })
  }

  if (screen.name === 'session' && session) {
    return (
      <SessionScreen>
        <div>
          <PageTitle>Sesión iniciada</PageTitle>
          <MutedText>Expira a las {new Date(session.expiresAt).toLocaleTimeString()}</MutedText>
        </div>
      </SessionScreen>
    )
  }

  return (
    <AuthLayout>
      {screen.name === 'otp' ? (
        <DeviceOtpForm
          email={screen.email}
          deviceId={deviceId}
          onAuthenticated={handleAuthenticated}
          onBack={() => setScreen({ name: 'login', email: screen.email })}
          resendAvailableAt={otpSentAt === null ? undefined : otpSentAt + RESEND_COOLDOWN_MS}
        />
      ) : (
        <LoginPage
          initialEmail={screen.name === 'login' ? screen.email : undefined}
          deviceId={deviceId}
          onAuthenticated={handleAuthenticated}
          onRequiresConfirmation={(email) => {
            setOtpSentAt(Date.now())
            setScreen({ name: 'otp', email })
          }}
        />
      )}
    </AuthLayout>
  )
}
