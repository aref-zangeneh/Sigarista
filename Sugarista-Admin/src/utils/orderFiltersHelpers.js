import { toEnglishDigits } from './persianDigits.js'

export const EMPTY_ORDER_FILTERS = {
  status: '',
  fulfillment: '',
  channel: '',
  serviceType: '',
  paymentMethod: '',
  orderKind: '',
  excludedFromAccounting: '',
  fromDate: '',
  toDate: '',
  minAmount: '',
  maxAmount: '',
  hasDiscount: '',
  isSplitBill: '',
  hasNote: '',
  hasManagerNote: '',
  menuItemQuery: '',
  tableQuery: '',
  deliveryMethodId: '',
  sortBy: 'newest',
}

export function toIsoLocalDate(date = new Date()) {
  const y = date.getFullYear()
  const m = String(date.getMonth() + 1).padStart(2, '0')
  const d = String(date.getDate()).padStart(2, '0')
  return `${y}-${m}-${d}`
}

export function getOrderDatePresetRange(preset, now = new Date()) {
  const today = new Date(now.getFullYear(), now.getMonth(), now.getDate())
  if (preset === 'today') {
    const iso = toIsoLocalDate(today)
    return { fromDate: iso, toDate: iso }
  }
  if (preset === 'yesterday') {
    const y = new Date(today)
    y.setDate(y.getDate() - 1)
    const iso = toIsoLocalDate(y)
    return { fromDate: iso, toDate: iso }
  }
  if (preset === '7d') {
    const start = new Date(today)
    start.setDate(start.getDate() - 6)
    return { fromDate: toIsoLocalDate(start), toDate: toIsoLocalDate(today) }
  }
  if (preset === '30d') {
    const start = new Date(today)
    start.setDate(start.getDate() - 29)
    return { fromDate: toIsoLocalDate(start), toDate: toIsoLocalDate(today) }
  }
  return { fromDate: '', toDate: '' }
}

export function countActiveOrderFilters(filters = {}) {
  let count = 0
  for (const [key, value] of Object.entries(filters)) {
    if (key === 'sortBy') {
      if (value && value !== 'newest') count++
      continue
    }
    if (String(value ?? '').trim() !== '') {
      count++
    }
  }
  return count
}

export function buildOrdersApiPath(filters = {}) {
  const params = new URLSearchParams()

  const setIfPresent = (key, val) => {
    const clean = String(val ?? '').trim()
    if (clean) params.set(key, clean)
  }

  setIfPresent('status', filters.status)
  setIfPresent('fulfillment', filters.fulfillment)
  setIfPresent('channel', filters.channel)
  setIfPresent('serviceType', filters.serviceType)
  setIfPresent('paymentMethod', filters.paymentMethod)
  setIfPresent('orderKind', filters.orderKind)
  setIfPresent('excludedFromAccounting', filters.excludedFromAccounting)
  setIfPresent('fromDate', filters.fromDate)
  setIfPresent('toDate', filters.toDate)

  const minAmt = toEnglishDigits(String(filters.minAmount ?? '')).replace(/[^\d.]/g, '')
  const maxAmt = toEnglishDigits(String(filters.maxAmount ?? '')).replace(/[^\d.]/g, '')
  if (minAmt) params.set('minAmount', minAmt)
  if (maxAmt) params.set('maxAmount', maxAmt)

  setIfPresent('hasDiscount', filters.hasDiscount)
  setIfPresent('isSplitBill', filters.isSplitBill)
  setIfPresent('hasNote', filters.hasNote)
  setIfPresent('hasManagerNote', filters.hasManagerNote)
  setIfPresent('menuItemQuery', filters.menuItemQuery)
  setIfPresent('tableQuery', filters.tableQuery)
  setIfPresent('deliveryMethodId', filters.deliveryMethodId)

  if (filters.sortBy && filters.sortBy !== 'newest') {
    params.set('sortBy', filters.sortBy)
  }

  const qs = params.toString()
  return qs ? `/admin/orders?${qs}` : '/admin/orders'
}
