import assert from 'node:assert/strict'
import test from 'node:test'
import {
  addDaysToIsoDate,
  computeExpiryMeta,
  computeMarginPercent,
  filterQuickStockItems,
} from './quickStockHelpers.js'

test('addDaysToIsoDate shifts calendar days cleanly', () => {
  const base = new Date(2026, 10, 26) // 2026-11-26
  assert.equal(addDaysToIsoDate(0, base), '2026-11-26')
  assert.equal(addDaysToIsoDate(3, base), '2026-11-29')
  assert.equal(addDaysToIsoDate(5, base), '2026-12-01')
})

test('computeExpiryMeta classifies expired, today, tomorrow, soon, fresh, and none', () => {
  const now = new Date(2026, 10, 26, 10, 0, 0)

  assert.equal(computeExpiryMeta(null, now).status, 'none')
  assert.equal(computeExpiryMeta('2026-11-24T12:00:00', now).status, 'expired')
  assert.equal(computeExpiryMeta('2026-11-26T20:00:00', now).status, 'expiring_soon')
  assert.equal(computeExpiryMeta('2026-11-26T20:00:00', now).label, 'انقضا امروز')
  assert.equal(computeExpiryMeta('2026-11-27T12:00:00', now).label, 'انقضا فردا')
  assert.equal(computeExpiryMeta('2026-12-03T12:00:00', now).status, 'fresh')
})

test('computeMarginPercent computes profit margin percentage cleanly', () => {
  assert.equal(computeMarginPercent(200000, 100000), 50)
  assert.equal(computeMarginPercent(150000, 0), null)
  assert.equal(computeMarginPercent(0, 50000), null)
})

test('filterQuickStockItems filters by category, search, expired, expiring, out, and has-lots', () => {
  const items = [
    {
      id: '1',
      persianName: 'تیرامیسو کلاسیک',
      englishName: 'Tiramisu',
      categoryId: 'cat-cake',
      categoryName: 'کیک و دسر',
      hasStockRecord: true,
      currentQuantity: 2,
      minAlertQuantity: 1,
      expiryStatus: 'expiring_soon',
      expiringSoonQuantity: 1,
      expiredQuantity: 0,
      lots: [{ id: 'l1' }, { id: 'l2' }],
    },
    {
      id: '2',
      persianName: 'چیزکیک سن سباستین',
      englishName: 'San Sebastian',
      categoryId: 'cat-cake',
      categoryName: 'کیک و دسر',
      hasStockRecord: true,
      currentQuantity: 0,
      minAlertQuantity: 2,
      expiryStatus: 'expired',
      expiringSoonQuantity: 0,
      expiredQuantity: 1,
      lots: [],
    },
    {
      id: '3',
      persianName: 'اسپرسو دبل',
      englishName: 'Double Espresso',
      categoryId: 'cat-coffee',
      categoryName: 'قهوه گرم',
      hasStockRecord: false,
      currentQuantity: 0,
      minAlertQuantity: 0,
      expiryStatus: 'none',
      expiringSoonQuantity: 0,
      expiredQuantity: 0,
      lots: [],
    },
  ]

  assert.equal(filterQuickStockItems(items, { categoryId: 'cat-cake' }).length, 2)
  assert.equal(filterQuickStockItems(items, { search: 'تیرامیسو' }).length, 1)
  assert.equal(filterQuickStockItems(items, { statusFilter: 'expiring' })[0].id, '1')
  assert.equal(filterQuickStockItems(items, { statusFilter: 'expired' })[0].id, '2')
  assert.equal(filterQuickStockItems(items, { statusFilter: 'out' })[0].id, '2')
  assert.equal(filterQuickStockItems(items, { statusFilter: 'has-lots' })[0].id, '1')
})
