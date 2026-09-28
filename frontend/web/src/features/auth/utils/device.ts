export const DEVICE_ID_STORAGE_KEY = 'stockma.auth.deviceId'

const DEVICE_ID_MIN_LENGTH = 16
const DEVICE_ID_MAX_LENGTH = 128
const FINGERPRINT_MAX_LENGTH = 256
const UNKNOWN_FINGERPRINT = 'desconocido'

function isValidDeviceId(value: string | null): value is string {
  const length = value?.trim().length ?? 0
  return length >= DEVICE_ID_MIN_LENGTH && length <= DEVICE_ID_MAX_LENGTH
}

function generateDeviceId(): string {
  if (typeof crypto.randomUUID === 'function') {
    return crypto.randomUUID()
  }

  const bytes = crypto.getRandomValues(new Uint8Array(16))
  return Array.from(bytes, (byte) => byte.toString(16).padStart(2, '0')).join('')
}

export function getOrCreateDeviceId(): string {
  try {
    const stored = localStorage.getItem(DEVICE_ID_STORAGE_KEY)
    if (isValidDeviceId(stored)) {
      return stored
    }

    const generated = generateDeviceId()
    localStorage.setItem(DEVICE_ID_STORAGE_KEY, generated)
    return generated
  } catch {
    return generateDeviceId()
  }
}

export function getFingerprint(): string {
  const fingerprint = navigator.userAgent.trim().slice(0, FINGERPRINT_MAX_LENGTH)
  return fingerprint || UNKNOWN_FINGERPRINT
}
