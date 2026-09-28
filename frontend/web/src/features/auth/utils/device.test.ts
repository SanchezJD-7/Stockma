import { afterEach, describe, expect, it, vi } from 'vitest'
import { DEVICE_ID_STORAGE_KEY, getFingerprint, getOrCreateDeviceId } from './device'

afterEach(() => {
  localStorage.clear()
  vi.restoreAllMocks()
  vi.unstubAllGlobals()
})

function isValidDeviceId(value: string) {
  const trimmed = value.trim()
  return trimmed.length >= 16 && trimmed.length <= 128
}

describe('getOrCreateDeviceId', () => {
  it('genera un deviceId una sola vez y lo reutiliza', () => {
    const first = getOrCreateDeviceId()
    const second = getOrCreateDeviceId()

    expect(first).toBe(second)
    expect(isValidDeviceId(first)).toBe(true)
  })

  it.each([
    ['demasiado corto', 'abc'],
    ['sólo espacios', '                    '],
    ['demasiado largo', 'x'.repeat(129)],
  ])('reemplaza un deviceId guardado %s por uno válido', (_caso, stored) => {
    localStorage.setItem(DEVICE_ID_STORAGE_KEY, stored)

    const deviceId = getOrCreateDeviceId()

    expect(isValidDeviceId(deviceId)).toBe(true)
    expect(localStorage.getItem(DEVICE_ID_STORAGE_KEY)).toBe(deviceId)
  })

  it('genera un deviceId válido aunque crypto.randomUUID no exista (HTTP fuera de localhost)', () => {
    vi.stubGlobal('crypto', { getRandomValues: crypto.getRandomValues.bind(crypto) })

    const deviceId = getOrCreateDeviceId()

    expect(isValidDeviceId(deviceId)).toBe(true)
  })

  it('devuelve un deviceId válido aunque localStorage falle', () => {
    vi.spyOn(Storage.prototype, 'getItem').mockImplementation(() => {
      throw new Error('storage bloqueado')
    })

    expect(isValidDeviceId(getOrCreateDeviceId())).toBe(true)
  })
})

describe('getFingerprint', () => {
  it('recorta la huella a 256 caracteres', () => {
    vi.spyOn(navigator, 'userAgent', 'get').mockReturnValue('A'.repeat(300))

    expect(getFingerprint()).toBe('A'.repeat(256))
  })

  it('nunca devuelve una huella vacía, que el backend rechazaría', () => {
    vi.spyOn(navigator, 'userAgent', 'get').mockReturnValue('   ')

    expect(getFingerprint().length).toBeGreaterThan(0)
  })
})
