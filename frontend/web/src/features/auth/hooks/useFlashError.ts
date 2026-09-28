import { useEffect, useState } from 'react'

export const FLASH_ERROR_DURATION_MS = 5000

interface FlashError {
  id: number
  message: string
}

export function useFlashError() {
  const [flash, setFlash] = useState<FlashError | null>(null)

  useEffect(() => {
    if (!flash) {
      return
    }

    const timer = setTimeout(() => setFlash(null), FLASH_ERROR_DURATION_MS)
    return () => clearTimeout(timer)
  }, [flash])

  function showError(message: string) {
    setFlash((current) => ({ id: (current?.id ?? 0) + 1, message }))
  }

  function clearError() {
    setFlash(null)
  }

  return { error: flash?.message ?? null, showError, clearError }
}
