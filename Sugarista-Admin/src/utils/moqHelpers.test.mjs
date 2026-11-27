import test from 'node:test'
import assert from 'node:assert/strict'
import {
  alertSignature,
  detectNewOrEscalatedAlerts,
  filterVisibleToasts,
  getMoqSeverityMeta,
  getMoqSourceMeta,
  parseMoqNumber,
  parseSseChunk,
} from './moqHelpers.js'

test('parseMoqNumber handles Persian/Arabic digits, decimals, and allowZero flag', () => {
  assert.equal(parseMoqNumber('۱۵۰'), 150)
  assert.equal(parseMoqNumber('۱۲,۵۰۰'), 12500)
  assert.equal(parseMoqNumber('2.5'), 2.5)
  assert.equal(parseMoqNumber('۰', { allowZero: false }), null)
  assert.equal(parseMoqNumber('۰', { allowZero: true }), 0)
  assert.equal(parseMoqNumber('-5', { allowZero: true }), null)
  assert.equal(parseMoqNumber('abc'), null)
})

test('detectNewOrEscalatedAlerts detects new alerts, severity escalations, and further drops', () => {
  const prev = new Map([
    ['Ingredient:a', { rank: 1, currentQuantity: 8 }],
    ['Ingredient:b', { rank: 2, currentQuantity: 3 }],
  ])

  const next = [
    // Unchanged -> should NOT trigger
    { alertKey: 'Ingredient:a', severity: 'Warning', currentQuantity: 8 },
    // Escalated from Critical -> OutOfStock -> SHOULD trigger
    { alertKey: 'Ingredient:b', severity: 'OutOfStock', currentQuantity: 0 },
    // Brand new alert -> SHOULD trigger
    { alertKey: 'MenuItem:c', severity: 'Warning', currentQuantity: 4 },
    // Healthy -> ignored
    { alertKey: 'Ingredient:d', severity: 'Healthy', currentQuantity: 50 },
  ]

  const triggered = detectNewOrEscalatedAlerts(prev, next)
  assert.deepEqual(
    triggered.map((x) => x.alertKey),
    ['Ingredient:b', 'MenuItem:c'],
  )
})

test('filterVisibleToasts suppresses dismissed items until their state worsens', () => {
  const item = { alertKey: 'Ingredient:milk', severity: 'Warning', currentQuantity: 5 }
  const dismissed = { 'Ingredient:milk': alertSignature(item) }

  assert.equal(filterVisibleToasts([item], dismissed).length, 0)

  // Quantity drops further -> revives toast automatically!
  const dropped = { ...item, currentQuantity: 3, severity: 'Critical' }
  assert.equal(filterVisibleToasts([dropped], dismissed).length, 1)
})

test('parseSseChunk extracts complete SSE frames, ignores pings, and preserves incomplete remainder', () => {
  const chunk = ': ping\n\nevent: moq_snapshot\nid: 7\ndata: {"version":7,"totalAlertsCount":2}\n\nevent: moq_snapshot\ndata: {"part'
  const { events, remainder } = parseSseChunk(chunk)

  assert.equal(events.length, 1)
  assert.equal(events[0].event, 'moq_snapshot')
  assert.equal(events[0].id, '7')
  assert.deepEqual(JSON.parse(events[0].data), { version: 7, totalAlertsCount: 2 })
  assert.equal(remainder, 'event: moq_snapshot\ndata: {"part')
})

test('source and severity metadata resolve both string and numeric enums', () => {
  assert.equal(getMoqSourceMeta('MenuItem').label, 'آیتم منو')
  assert.equal(getMoqSourceMeta(0).label, 'مواد اولیه')
  assert.equal(getMoqSeverityMeta('OutOfStock').rank, 3)
  assert.equal(getMoqSeverityMeta(1).label, 'کمبود (نزدیک MOQ)')
})
