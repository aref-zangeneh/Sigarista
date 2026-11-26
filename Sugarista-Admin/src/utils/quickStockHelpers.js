import { toPersianDigits as toPersianNumber } from './persianDigits.js'

const MS_PER_DAY = 24 * 60 * 60 * 1000
const PERSIAN_DATE_FORMATTER = new Intl.DateTimeFormat('fa-IR-u-ca-persian', {
  year: 'numeric',
  month: '2-digit',
  day: '2-digit',
  timeZone: 'Asia/Tehran',
})

function formatShamsiDate(date) {
  if (!(date instanceof Date) || Number.isNaN(date.getTime())) return ''
  return PERSIAN_DATE_FORMATTER.format(date)
}

export function toIsoDateOnly(date = new Date()) {
  const d = date instanceof Date ? date : new Date(date)
  if (Number.isNaN(d.getTime())) return ''
  const y = d.getFullYear()
  const m = String(d.getMonth() + 1).padStart(2, '0')
  const day = String(d.getDate()).padStart(2, '0')
  return `${y}-${m}-${day}`
}

export function addDaysToIsoDate(days, baseDate = new Date()) {
  const d = new Date(baseDate.getFullYear(), baseDate.getMonth(), baseDate.getDate())
  d.setDate(d.getDate() + Number(days || 0))
  return toIsoDateOnly(d)
}

export function isoTimestampToDateInput(value) {
  if (!value) return ''
  if (/^\d{4}-\d{2}-\d{2}$/.test(String(value).trim())) {
    return String(value).trim()
  }
  const d = new Date(value)
  if (Number.isNaN(d.getTime())) return ''
  return toIsoDateOnly(d)
}

export function computeExpiryMeta(expiresAt, now = new Date()) {
  if (!expiresAt) {
    return { status: 'none', daysDiff: null, label: 'بدون انقضا', shamsiDate: '' }
  }

  const exp = new Date(expiresAt)
  if (Number.isNaN(exp.getTime())) {
    return { status: 'none', daysDiff: null, label: 'بدون انقضا', shamsiDate: '' }
  }

  const todayStart = new Date(now.getFullYear(), now.getMonth(), now.getDate()).getTime()
  const expDayStart = new Date(exp.getFullYear(), exp.getMonth(), exp.getDate()).getTime()
  const daysDiff = Math.round((expDayStart - todayStart) / MS_PER_DAY)
  const shamsiDate = formatShamsiDate(exp)

  if (daysDiff < 0) {
    const ago = Math.abs(daysDiff)
    return {
      status: 'expired',
      daysDiff,
      label: `منقضی‌شده (${toPersianNumber(ago)} روز پیش)`,
      shamsiDate,
    }
  }

  if (daysDiff === 0) {
    return {
      status: 'expiring_soon',
      daysDiff: 0,
      label: 'انقضا امروز',
      shamsiDate,
    }
  }

  if (daysDiff === 1) {
    return {
      status: 'expiring_soon',
      daysDiff: 1,
      label: 'انقضا فردا',
      shamsiDate,
    }
  }

  if (daysDiff === 2) {
    return {
      status: 'expiring_soon',
      daysDiff: 2,
      label: '۲ روز مانده',
      shamsiDate,
    }
  }

  return {
    status: 'fresh',
    daysDiff,
    label: `${toPersianNumber(daysDiff)} روز مانده`,
    shamsiDate,
  }
}

export function computeMarginPercent(salePrice, purchasePrice) {
  const sale = Number(salePrice)
  const cost = Number(purchasePrice)
  if (!Number.isFinite(sale) || !Number.isFinite(cost) || sale <= 0 || cost <= 0) {
    return null
  }
  return Math.round(((sale - cost) / sale) * 100)
}

export function filterQuickStockItems(items, { search = '', categoryId = '', statusFilter = 'all' } = {}) {
  const list = Array.isArray(items) ? items : []
  const q = String(search || '').trim().toLowerCase()

  return list.filter((item) => {
    if (categoryId && item.categoryId !== categoryId) {
      return false
    }

    if (q) {
      const fa = String(item.persianName || '').toLowerCase()
      const en = String(item.englishName || '').toLowerCase()
      const cat = String(item.categoryName || '').toLowerCase()
      if (!fa.includes(q) && !en.includes(q) && !cat.includes(q)) {
        return false
      }
    }

    if (statusFilter === 'expired') {
      return item.expiryStatus === 'expired' || Number(item.expiredQuantity || 0) > 0
    }
    if (statusFilter === 'expiring') {
      return item.expiryStatus === 'expiring_soon' || Number(item.expiringSoonQuantity || 0) > 0
    }
    if (statusFilter === 'low') {
      return item.hasStockRecord && Number(item.currentQuantity || 0) <= Math.max(0, Number(item.minAlertQuantity || 0))
    }
    if (statusFilter === 'out') {
      return item.hasStockRecord && Number(item.currentQuantity || 0) <= 0
    }
    if (statusFilter === 'has-lots') {
      return Array.isArray(item.lots) && item.lots.length > 0
    }

    return true
  })
}
