import {
  createContext,
  useCallback,
  useContext,
  useMemo,
  useState,
  type PropsWithChildren,
} from 'react'
import { WaitlistModal } from '../WaitlistModal/WaitlistModal'
import { WaitlistSuccessModal } from '../WaitlistSuccessModal/WaitlistSuccessModal'

export type WaitlistPhase = 'closed' | 'form' | 'success'

interface WaitlistContextValue {
  phase: WaitlistPhase
  open: () => void
  close: () => void
}

const WaitlistContext = createContext<WaitlistContextValue | null>(null)

export function WaitlistModalProvider({ children }: PropsWithChildren) {
  const [phase, setPhase] = useState<WaitlistPhase>('closed')

  const open = useCallback(() => setPhase('form'), [])
  const close = useCallback(() => setPhase('closed'), [])
  const handleSuccess = useCallback(() => {
    setPhase((current) => (current === 'form' ? 'success' : current))
  }, [])

  const value = useMemo(() => ({ phase, open, close }), [phase, open, close])

  return (
    <WaitlistContext.Provider value={value}>
      {children}
      <WaitlistModal
        key={phase}
        open={phase === 'form'}
        onClose={close}
        onSuccess={handleSuccess}
      />
      <WaitlistSuccessModal open={phase === 'success'} onClose={close} />
    </WaitlistContext.Provider>
  )
}

export function useOpenWaitlist(): WaitlistContextValue {
  const context = useContext(WaitlistContext)
  if (!context) {
    throw new Error('useOpenWaitlist debe usarse dentro de WaitlistModalProvider')
  }
  return context
}
