const PERSIAN_DIGITS = '۰۱۲۳۴۵۶۷۸۹'
const ARABIC_DIGITS = '٠١٢٣٤٥٦٧٨٩'

function tehranDateToUtcIso(isoDateStr, endOfDay = false) {
  const clean = String(isoDateStr ?? '').trim()
  if (!/^\d{4}-\d{2}-\d{2}$/.test(clean)) return null
  const suffix = endOfDay ? 'T23:59:59.999+03:30' : 'T00:00:00+03:30'
  const parsed = new Date(`${clean}${suffix}`)
  return Number.isNaN(parsed.getTime()) ? null : parsed.toISOString()
}

export function normalizeDigits(value) {
  return String(value ?? '').replace(/[۰-۹٠-٩]/g, (ch) => {
    const persianIndex = PERSIAN_DIGITS.indexOf(ch)
    if (persianIndex >= 0) return String(persianIndex)
    const arabicIndex = ARABIC_DIGITS.indexOf(ch)
    if (arabicIndex >= 0) return String(arabicIndex)
    return ch
  })
}

export const WALLET_TX_TYPES = {
  TopUp: { label: 'شارژ کیف پول', tone: 'success', sign: '+' },
  Cashback: { label: 'کش‌بک / هدیه', tone: 'info', sign: '+' },
  Purchase: { label: 'پرداخت سفارش', tone: 'danger', sign: '−' },
  Refund: { label: 'برگشت وجه لغو', tone: 'success', sign: '+' },
  Adjustment: { label: 'تعدیل دستی', tone: 'neutral', sign: '±' },
  // Numeric enum fallbacks (in case API serializes as int)
  0: { label: 'شارژ کیف پول', tone: 'success', sign: '+' },
  1: { label: 'کش‌بک / هدیه', tone: 'info', sign: '+' },
  2: { label: 'پرداخت سفارش', tone: 'danger', sign: '−' },
  3: { label: 'برگشت وجه لغو', tone: 'success', sign: '+' },
  4: { label: 'تعدیل دستی', tone: 'neutral', sign: '±' },
}

export const WALLET_TX_TYPE_OPTIONS = [
  { value: '', label: 'همه انواع تراکنش' },
  { value: 'TopUp', label: 'شارژ کیف پول' },
  { value: 'Cashback', label: 'کش‌بک / هدیه' },
  { value: 'Purchase', label: 'پرداخت سفارش' },
  { value: 'Refund', label: 'برگشت وجه لغو' },
  { value: 'Adjustment', label: 'تعدیل دستی' },
]

export const QUICK_TOPUP_PRESETS = [50_000, 100_000, 200_000, 500_000, 1_000_000]

export function getWalletTxMeta(type) {
  return WALLET_TX_TYPES[type] ?? { label: String(type ?? 'نامشخص'), tone: 'neutral', sign: '' }
}

export function parseWalletAmount(rawInput) {
  if (rawInput === null || rawInput === undefined) return null
  const normalized = normalizeDigits(rawInput)
    .replace(/[,٬،\s_]/g, '')
    .trim()
  if (!normalized || !/^\d+(\.\d+)?$/.test(normalized)) {
    return null
  }
  const num = Number(normalized)
  if (!Number.isFinite(num) || num <= 0) {
    return null
  }
  return Math.round(num)
}

export function computeBalancePreview(currentBalance, rawAmount, mode) {
  const base = Math.max(0, Number(currentBalance) || 0)
  const amount = parseWalletAmount(rawAmount)

  if (!amount) {
    return {
      amount: 0,
      signedAmount: 0,
      nextBalance: base,
      isOverdraft: false,
      isValid: false,
    }
  }

  if (mode === 'debit') {
    const nextBalance = base - amount
    const isOverdraft = nextBalance < 0
    return {
      amount,
      signedAmount: -amount,
      nextBalance: Math.max(0, nextBalance),
      isOverdraft,
      isValid: !isOverdraft,
    }
  }

  return {
    amount,
    signedAmount: amount,
    nextBalance: base + amount,
    isOverdraft: false,
    isValid: true,
  }
}

export function buildWalletsQueryString(filters = {}) {
  const params = new URLSearchParams()
  params.set('page', String(filters.page || 1))
  params.set('pageSize', String(filters.pageSize || 25))

  const search = normalizeDigits(filters.search ?? '').trim()
  if (search) params.set('search', search)

  if (filters.balanceFilter && filters.balanceFilter !== 'all') {
    params.set('balanceFilter', filters.balanceFilter)
  }

  const minBalance = parseWalletAmount(filters.minBalance)
  if (minBalance !== null) params.set('minBalance', String(minBalance))

  const maxBalance = parseWalletAmount(filters.maxBalance)
  if (maxBalance !== null) params.set('maxBalance', String(maxBalance))

  if (filters.sortBy) {
    params.set('sortBy', filters.sortBy)
  }

  return params.toString()
}

export function buildWalletTransactionsQueryString(filters = {}) {
  const params = new URLSearchParams()
  params.set('page', String(filters.page || 1))
  params.set('pageSize', String(filters.pageSize || 30))

  if (filters.customerId) params.set('customerId', filters.customerId)
  if (filters.type) params.set('type', filters.type)
  if (filters.direction && filters.direction !== 'all') params.set('direction', filters.direction)

  const search = normalizeDigits(filters.search ?? '').trim()
  if (search) params.set('search', search)

  if (filters.fromDate) {
    const iso = tehranDateToUtcIso(filters.fromDate, false)
    if (iso) params.set('fromDate', iso)
  }

  if (filters.toDate) {
    const iso = tehranDateToUtcIso(filters.toDate, true)
    if (iso) params.set('toDate', iso)
  }

  const minAmount = parseWalletAmount(filters.minAmount)
  if (minAmount !== null) params.set('minAmount', String(minAmount))

  const maxAmount = parseWalletAmount(filters.maxAmount)
  if (maxAmount !== null) params.set('maxAmount', String(maxAmount))

  return params.toString()
}
