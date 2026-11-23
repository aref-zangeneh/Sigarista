import { toPersianDigits } from './persianDigits.js'

export const DEFAULT_ALERT_SOUND_INTERVAL_SECONDS = 10
export const MIN_REPEAT_INTERVAL_SECONDS = 3
export const MAX_REPEAT_INTERVAL_SECONDS = 300
export const ALERT_SOUND_INTERVAL_PRESETS = [0, 5, 10, 15, 30, 60]

export function normalizeAlertSoundInterval(value) {
  if (value === null || value === undefined || value === '') {
    return DEFAULT_ALERT_SOUND_INTERVAL_SECONDS
  }
  const parsed = Math.round(Number(value))
  if (!Number.isFinite(parsed) || parsed < 0) {
    return DEFAULT_ALERT_SOUND_INTERVAL_SECONDS
  }
  if (parsed === 0) {
    return 0
  }
  return Math.max(MIN_REPEAT_INTERVAL_SECONDS, Math.min(MAX_REPEAT_INTERVAL_SECONDS, parsed))
}

export function normalizeAlertSoundSettings(settings) {
  const enabled = settings?.enabled ?? settings?.orderAlertSoundEnabled ?? true
  const rawInterval = settings?.intervalSeconds ?? settings?.orderAlertSoundIntervalSeconds
  return {
    enabled: enabled !== false,
    intervalSeconds: normalizeAlertSoundInterval(rawInterval),
  }
}

export function getRepeatAlertSoundDelayMs(settings) {
  const normalized = normalizeAlertSoundSettings(settings)
  if (!normalized.enabled || normalized.intervalSeconds === 0) {
    return null
  }
  return normalized.intervalSeconds * 1000
}

export function formatAlertSoundDescription(settings) {
  const normalized = normalizeAlertSoundSettings(settings)
  if (!normalized.enabled) {
    return 'سفارش‌های پرداخت‌شده در صف آشپزخانه یا آماده تحویل — صدای اعلان خاموش است.'
  }
  if (normalized.intervalSeconds === 0) {
    return 'سفارش‌های پرداخت‌شده در صف آشپزخانه یا آماده تحویل — صدا فقط یک‌بار هنگام ورود سفارش جدید پخش می‌شود.'
  }
  return `سفارش‌های پرداخت‌شده در صف آشپزخانه یا آماده تحویل — صدای کوتاه هر ${toPersianDigits(normalized.intervalSeconds)} ثانیه تا زمان «شروع آماده‌سازی» تکرار می‌شود.`
}

let audioContext = null
let alertSoundMuted = false
const activeOscillators = new Set()

function getAudioContext() {
  if (typeof window === 'undefined') {
    return null
  }

  const AudioContextClass = window.AudioContext || window.webkitAudioContext
  if (!AudioContextClass) {
    return null
  }

  if (!audioContext) {
    audioContext = new AudioContextClass()
  }

  return audioContext
}

export function stopOrderAlertSound() {
  for (const oscillator of activeOscillators) {
    try {
      oscillator.stop()
    } catch {
      // ignore already-stopped oscillators
    }
    try {
      oscillator.disconnect()
    } catch {
      // ignore disconnect errors
    }
  }
  activeOscillators.clear()

  if (audioContext && audioContext.state === 'running') {
    void audioContext.suspend().catch(() => {})
  }
}

export function setOrderAlertSoundMuted(muted) {
  alertSoundMuted = Boolean(muted)
  if (alertSoundMuted) {
    stopOrderAlertSound()
  }
}

export function isOrderAlertSoundMuted() {
  return alertSoundMuted
}

export function unlockOrderAlertSound() {
  if (alertSoundMuted) {
    return
  }

  const context = getAudioContext()
  if (!context) {
    return
  }

  if (context.state === 'suspended') {
    void context.resume().catch(() => {})
  }
}

export function playOrderAlertSound({ force = false } = {}) {
  if (!force && alertSoundMuted) {
    return false
  }

  try {
    const context = getAudioContext()
    if (!context) {
      return false
    }

    if (context.state === 'suspended') {
      void context.resume().catch(() => {})
    }

    const startAt = context.currentTime + 0.01
    const oscillator = context.createOscillator()
    const gain = context.createGain()

    oscillator.type = 'sine'
    oscillator.connect(gain)
    gain.connect(context.destination)

    oscillator.frequency.setValueAtTime(880, startAt)
    oscillator.frequency.setValueAtTime(1174.66, startAt + 0.09)

    gain.gain.setValueAtTime(0.0001, startAt)
    gain.gain.exponentialRampToValueAtTime(0.12, startAt + 0.02)
    gain.gain.exponentialRampToValueAtTime(0.0001, startAt + 0.22)

    activeOscillators.add(oscillator)
    oscillator.onended = () => {
      activeOscillators.delete(oscillator)
      try {
        oscillator.disconnect()
        gain.disconnect()
      } catch {
        // ignore cleanup errors
      }
    }

    oscillator.start(startAt)
    oscillator.stop(startAt + 0.24)
    return true
  } catch {
    // Browsers may block audio until a user gesture unlocks it.
    return false
  }
}

export function isUnhandledKitchenOrder(order) {
  const status = order?.fulfillmentStatus
  return !status || status === 'None' || status === '' || status === 'Registered'
}

export function countUnhandledKitchenOrders(alerts) {
  return (alerts?.activeKitchenOrders ?? []).filter(isUnhandledKitchenOrder).length
}
