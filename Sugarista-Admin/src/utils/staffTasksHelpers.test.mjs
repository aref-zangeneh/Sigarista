import test from 'node:test'
import assert from 'node:assert/strict'
import {
  ALL_DAYS_MASK,
  formatDelayBadge,
  formatOverdueLabel,
  formatScheduleLabel,
  toggleWeekdayBit,
} from './staffTasksHelpers.js'

test('toggleWeekdayBit toggles individual Persian weekday bits cleanly', () => {
  let mask = 0
  mask = toggleWeekdayBit(mask, 64) // Saturday
  mask = toggleWeekdayBit(mask, 32) // Friday
  assert.equal(mask, 96)
  mask = toggleWeekdayBit(mask, 64) // remove Saturday
  assert.equal(mask, 32)
})

test('formatScheduleLabel renders Daily, Weekly, and OneTime schedules in Persian', () => {
  assert.equal(
    formatScheduleLabel({ recurrenceType: 'Daily', raiseTime: '21:00', daysOfWeekMask: ALL_DAYS_MASK }),
    'هر روز ساعت ۲۱:۰۰',
  )
  assert.equal(
    formatScheduleLabel({ recurrenceType: 'Weekly', raiseTime: '08:30', daysOfWeekMask: 64 | 1 }),
    'شنبه، یکشنبه — ساعت ۰۸:۳۰',
  )
  const oneTime = formatScheduleLabel({
    recurrenceType: 'OneTime',
    raiseTime: '22:15',
    oneTimeDate: '2026-11-27',
  })
  assert.ok(oneTime.includes('۲۲:۱۵'))
  assert.ok(oneTime.startsWith('یک‌باره'))
})

test('formatOverdueLabel handles 0, minutes, and hours+minutes', () => {
  assert.equal(formatOverdueLabel(0), 'هم‌اکنون وقت انجام است')
  assert.equal(formatOverdueLabel(17), '۱۷ دقیقه از زمان مقرر گذشته')
  assert.equal(formatOverdueLabel(60), '۱ ساعت از زمان مقرر گذشته')
  assert.equal(formatOverdueLabel(135), '۲ ساعت و ۱۵ دقیقه گذشته')
})

test('formatDelayBadge classifies on-time, warn, and late delays', () => {
  assert.equal(formatDelayBadge(0).tone, 'ok')
  assert.equal(formatDelayBadge(5).tone, 'ok')
  assert.equal(formatDelayBadge(15).tone, 'warn')
  assert.equal(formatDelayBadge(75).tone, 'bad')
})
