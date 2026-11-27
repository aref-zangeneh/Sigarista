const PERSIAN_DIGITS = '۰۱۲۳۴۵۶۷۸۹'
const ARABIC_DIGITS = '٠١٢٣٤٥٦٧٨٩'

export function normalizeMoqDigits(value) {
  return String(value ?? '').replace(/[۰-۹٠-٩]/g, (ch) => {
    const fa = PERSIAN_DIGITS.indexOf(ch)
    if (fa >= 0) return String(fa)
    const ar = ARABIC_DIGITS.indexOf(ch)
    if (ar >= 0) return String(ar)
    return ch
  })
}

export function parseMoqNumber(raw, { allowZero = false } = {}) {
  if (raw === null || raw === undefined) return null
  const clean = normalizeMoqDigits(raw)
    .replace(/[,٬،\s_]/g, '')
    .replace(/٫/g, '.')
    .trim()
  if (!clean || !/^\d+(\.\d+)?$/.test(clean)) return null
  const num = Number(clean)
  if (!Number.isFinite(num)) return null
  if (!allowZero && num <= 0) return null
  if (allowZero && num < 0) return null
  return Math.round(num * 1000) / 1000
}

export const MOQ_SOURCE_META = {
  Ingredient: { label: 'مواد اولیه', tone: 'indigo' },
  MenuItem: { label: 'آیتم منو', tone: 'purple' },
  WarehouseIngredient: { label: 'انبار / انبارک', tone: 'teal' },
  0: { label: 'مواد اولیه', tone: 'indigo' },
  1: { label: 'آیتم منو', tone: 'purple' },
  2: { label: 'انبار / انبارک', tone: 'teal' },
}

export const MOQ_SEVERITY_META = {
  OutOfStock: { label: 'اتمام موجودی', tone: 'danger', rank: 3 },
  Critical: { label: 'بحرانی (زیر ۵۰٪)', tone: 'critical', rank: 2 },
  Warning: { label: 'کمبود (نزدیک MOQ)', tone: 'warning', rank: 1 },
  Healthy: { label: 'موجودی سالم', tone: 'healthy', rank: 0 },
  3: { label: 'اتمام موجودی', tone: 'danger', rank: 3 },
  2: { label: 'بحرانی (زیر ۵۰٪)', tone: 'critical', rank: 2 },
  1: { label: 'کمبود (نزدیک MOQ)', tone: 'warning', rank: 1 },
  0: { label: 'موجودی سالم', tone: 'healthy', rank: 0 },
}

export function getMoqSourceMeta(sourceType) {
  return MOQ_SOURCE_META[sourceType] ?? { label: String(sourceType ?? ''), tone: 'indigo' }
}

export function getMoqSeverityMeta(severity) {
  return MOQ_SEVERITY_META[severity] ?? { label: String(severity ?? ''), tone: 'warning', rank: 1 }
}

export function alertSignature(item) {
  const rank = getMoqSeverityMeta(item.severity).rank
  return `${rank}:${item.currentQuantity}`
}

/**
 * Returns alerts that are either brand-new, escalated in severity,
 * or dropped further in quantity compared to the previous snapshot map.
 */
export function detectNewOrEscalatedAlerts(previousMap, nextItems) {
  const triggered = []
  for (const item of nextItems ?? []) {
    const rank = getMoqSeverityMeta(item.severity).rank
    if (rank <= 0) continue

    const prev = previousMap.get(item.alertKey)
    if (!prev) {
      triggered.push(item)
      continue
    }

    if (rank > prev.rank || item.currentQuantity < prev.currentQuantity) {
      triggered.push(item)
    }
  }
  return triggered
}

/**
 * Filters alerts for the floating Toast stack:
 * a dismissed item stays hidden until its severity worsens or quantity drops further.
 */
export function filterVisibleToasts(items, dismissedSignatures) {
  return (items ?? []).filter((item) => {
    const rank = getMoqSeverityMeta(item.severity).rank
    if (rank <= 0) return false
    const dismissedSig = dismissedSignatures[item.alertKey]
    if (!dismissedSig) return true
    return dismissedSig !== alertSignature(item)
  })
}

/**
 * Pure incremental SSE frame parser for fetch() ReadableStream.
 * Returns { events: Array<{ event, id, data }>, remainder: string }.
 */
export function parseSseChunk(buffer) {
  const normalized = String(buffer ?? '').replace(/\r\n/g, '\n')
  const parts = normalized.split('\n\n')
  const remainder = parts.pop() ?? ''
  const events = []

  for (const rawBlock of parts) {
    const trimmed = rawBlock.trim()
    if (!trimmed) continue

    let event = 'message'
    let id = null
    const dataLines = []

    for (const line of rawBlock.split('\n')) {
      if (!line || line.startsWith(':')) continue
      if (line.startsWith('event:')) {
        event = line.slice(6).trim()
      } else if (line.startsWith('id:')) {
        id = line.slice(3).trim()
      } else if (line.startsWith('data:')) {
        dataLines.push(line.slice(5).trimStart())
      }
    }

    if (dataLines.length > 0) {
      events.push({ event, id, data: dataLines.join('\n') })
    }
  }

  return { events, remainder }
}
