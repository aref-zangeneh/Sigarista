let audioContext = null

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

export function unlockOrderAlertSound() {
  const context = getAudioContext()
  if (!context) {
    return
  }

  if (context.state === 'suspended') {
    void context.resume()
  }
}

export function playOrderAlertSound() {
  try {
    const context = getAudioContext()
    if (!context) {
      return
    }

    if (context.state === 'suspended') {
      void context.resume()
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

    oscillator.start(startAt)
    oscillator.stop(startAt + 0.24)
  } catch {
    // Browsers may block audio until a user gesture unlocks it.
  }
}

export function isUnhandledKitchenOrder(order) {
  const status = order?.fulfillmentStatus
  return !status || status === 'None' || status === '' || status === 'Registered'
}

export function countUnhandledKitchenOrders(alerts) {
  return (alerts?.activeKitchenOrders ?? []).filter(isUnhandledKitchenOrder).length
}
