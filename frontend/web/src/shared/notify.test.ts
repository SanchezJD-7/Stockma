import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { notify, useNotify } from './notify'

beforeEach(() => {
  useNotify.setState({ toasts: [] })
  vi.useFakeTimers()
})

afterEach(() => {
  vi.useRealTimers()
})

describe('notify', () => {
  it('apila un toast por llamada con su severidad', () => {
    notify.error('No hay conexión con el servidor')
    notify.success('Registro completado')

    expect(useNotify.getState().toasts.map((toast) => [toast.severity, toast.message])).toEqual([
      ['error', 'No hay conexión con el servidor'],
      ['success', 'Registro completado'],
    ])
  })

  it('descarta el toast automáticamente a los 6 segundos', () => {
    notify.warning('Atención')

    vi.advanceTimersByTime(5999)
    expect(useNotify.getState().toasts).toHaveLength(1)

    vi.advanceTimersByTime(1)
    expect(useNotify.getState().toasts).toHaveLength(0)
  })

  it('dismiss lo saca de inmediato sin esperar el temporizador', () => {
    notify.error('Fallo')
    const [toast] = useNotify.getState().toasts

    useNotify.getState().dismiss(toast.id)

    expect(useNotify.getState().toasts).toHaveLength(0)
  })

  it('dismiss de un id desconocido no toca los demás', () => {
    notify.error('Fallo')
    notify.info('Dato')

    useNotify.getState().dismiss(9999)

    expect(useNotify.getState().toasts).toHaveLength(2)
  })
})
