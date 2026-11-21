import test from 'node:test'
import assert from 'node:assert/strict'
import {
  DEFAULT_ALERT_SOUND_INTERVAL_SECONDS,
  formatAlertSoundDescription,
  getRepeatAlertSoundDelayMs,
  normalizeAlertSoundInterval,
  normalizeAlertSoundSettings,
} from './orderAlertSound.js'

test('normalizeAlertSoundInterval clamps values while preserving 0 (play once)', () => {
  assert.equal(normalizeAlertSoundInterval(undefined), DEFAULT_ALERT_SOUND_INTERVAL_SECONDS)
  assert.equal(normalizeAlertSoundInterval(0), 0)
  assert.equal(normalizeAlertSoundInterval(1), 3)
  assert.equal(normalizeAlertSoundInterval(15), 15)
  assert.equal(normalizeAlertSoundInterval(999), 300)
  assert.equal(normalizeAlertSoundInterval(-5), DEFAULT_ALERT_SOUND_INTERVAL_SECONDS)
})

test('normalizeAlertSoundSettings reads both short and SiteSettings property names', () => {
  assert.deepEqual(
    normalizeAlertSoundSettings({ orderAlertSoundEnabled: false, orderAlertSoundIntervalSeconds: 30 }),
    { enabled: false, intervalSeconds: 30 },
  )
  assert.deepEqual(
    normalizeAlertSoundSettings({ enabled: true, intervalSeconds: 0 }),
    { enabled: true, intervalSeconds: 0 },
  )
})

test('getRepeatAlertSoundDelayMs returns null when muted or set to 0, else interval in ms', () => {
  assert.equal(getRepeatAlertSoundDelayMs({ enabled: false, intervalSeconds: 10 }), null)
  assert.equal(getRepeatAlertSoundDelayMs({ enabled: true, intervalSeconds: 0 }), null)
  assert.equal(getRepeatAlertSoundDelayMs({ enabled: true, intervalSeconds: 15 }), 15000)
})

test('formatAlertSoundDescription reflects muted, play-once, and custom Persian interval states', () => {
  assert.match(formatAlertSoundDescription({ enabled: false, intervalSeconds: 10 }), /خاموش است/)
  assert.match(formatAlertSoundDescription({ enabled: true, intervalSeconds: 0 }), /فقط یک‌بار/)
  assert.match(formatAlertSoundDescription({ enabled: true, intervalSeconds: 25 }), /هر ۲۵ ثانیه/)
})
