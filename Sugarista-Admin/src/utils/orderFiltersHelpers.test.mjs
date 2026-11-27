import test from 'node:test'
import assert from 'node:assert/strict'
import {
  EMPTY_ORDER_FILTERS,
  buildOrdersApiPath,
  countActiveOrderFilters,
  getOrderDatePresetRange,
} from './orderFiltersHelpers.js'

test('buildOrdersApiPath returns clean /admin/orders when no filters are active', () => {
  assert.equal(buildOrdersApiPath(EMPTY_ORDER_FILTERS), '/admin/orders')
  assert.equal(countActiveOrderFilters(EMPTY_ORDER_FILTERS), 0)
})

test('buildOrdersApiPath serializes Persian numeric amounts and all advanced filters', () => {
  const filters = {
    ...EMPTY_ORDER_FILTERS,
    status: 'Paid',
    channel: 'Pos',
    paymentMethod: 'CardToCard',
    minAmount: '۱۵۰٬۰۰۰',
    maxAmount: '۵۰۰۰۰۰',
    menuItemQuery: 'تیرامیسو',
    tableQuery: 'میز ۴',
    hasDiscount: 'true',
    sortBy: 'amount_desc',
  }

  const path = buildOrdersApiPath(filters)
  assert.ok(path.includes('status=Paid'))
  assert.ok(path.includes('channel=Pos'))
  assert.ok(path.includes('paymentMethod=CardToCard'))
  assert.ok(path.includes('minAmount=150000'))
  assert.ok(path.includes('maxAmount=500000'))
  assert.ok(path.includes('hasDiscount=true'))
  assert.ok(path.includes('sortBy=amount_desc'))
  assert.equal(countActiveOrderFilters(filters), 9)
})

test('getOrderDatePresetRange computes today, yesterday, and 7d ranges cleanly', () => {
  const fixed = new Date(2026, 10, 27, 12, 0, 0) // 2026-11-27
  assert.deepEqual(getOrderDatePresetRange('today', fixed), {
    fromDate: '2026-11-27',
    toDate: '2026-11-27',
  })
  assert.deepEqual(getOrderDatePresetRange('yesterday', fixed), {
    fromDate: '2026-11-26',
    toDate: '2026-11-26',
  })
  assert.deepEqual(getOrderDatePresetRange('7d', fixed), {
    fromDate: '2026-11-21',
    toDate: '2026-11-27',
  })
})
