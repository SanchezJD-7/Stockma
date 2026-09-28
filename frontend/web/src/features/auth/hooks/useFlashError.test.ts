import { act, renderHook } from '@testing-library/react'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { FLASH_ERROR_DURATION_MS, useFlashError } from './useFlashError'

beforeEach(() => {
  vi.useFakeTimers()
})

afterEach(() => {
  vi.useRealTimers()
})

describe('useFlashError', () => {
  it('muestra el error durante 5 segundos y después lo borra', () => {
    const { result } = renderHook(() => useFlashError())

    act(() => result.current.showError('Algo falló'))
    expect(FLASH_ERROR_DURATION_MS).toBe(5000)

    act(() => {
      vi.advanceTimersByTime(4999)
    })
    expect(result.current.error).toBe('Algo falló')

    act(() => {
      vi.advanceTimersByTime(1)
    })
    expect(result.current.error).toBeNull()
  })

  it('reinicia los 5 segundos si llega otro error, aunque sea el mismo mensaje', () => {
    const { result } = renderHook(() => useFlashError())

    act(() => result.current.showError('Algo falló'))
    act(() => {
      vi.advanceTimersByTime(3000)
    })
    act(() => result.current.showError('Algo falló'))
    act(() => {
      vi.advanceTimersByTime(4000)
    })

    expect(result.current.error).toBe('Algo falló')
  })

  it('clearError lo borra en el acto', () => {
    const { result } = renderHook(() => useFlashError())

    act(() => result.current.showError('Algo falló'))
    act(() => result.current.clearError())

    expect(result.current.error).toBeNull()
  })
})
