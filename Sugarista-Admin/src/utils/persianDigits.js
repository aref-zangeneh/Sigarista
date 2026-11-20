const PERSIAN_DIGITS = '۰۱۲۳۴۵۶۷۸۹'
const ARABIC_INDIC_DIGITS = '٠١٢٣٤٥٦٧٨٩'

const TECHNICAL_TOKEN_REGEX =
  /(?:https?:\/\/|\/api\/|www\.)[^\s<>"'`]+|[A-Za-z0-9._%+-]+@[A-Za-z0-9.-]+\.[A-Za-z]{2,}|\b[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}\b|#(?=[0-9a-fA-F]{3,8}\b)(?:[0-9a-fA-F]*[a-fA-F][0-9a-fA-F]*|0{6})\b/g

const SENSITIVE_KEY_SUBSTRINGS = [
  'password',
  'passwd',
  'secret',
  'apikey',
  'api_key',
  'signingkey',
  'signing_key',
  'privatekey',
  'private_key',
  'connectionstring',
  'connection_string',
  'accesstoken',
  'access_token',
  'refreshtoken',
  'refresh_token',
  'idtoken',
  'id_token',
  'bearertoken',
  'bearer_token',
  'extrasettingsjson',
]

/** Converts Persian (۰-۹) and Arabic-Indic (٠-٩) digits to ASCII English (0-9). */
export function toEnglishDigits(value) {
  if (typeof value !== 'string' || !value) {
    return value
  }

  if (!/[۰-۹٠-٩]/.test(value)) {
    return value
  }

  return value.replace(/[۰-۹٠-٩]/g, (digit) => {
    const persianIndex = PERSIAN_DIGITS.indexOf(digit)
    if (persianIndex >= 0) {
      return String(persianIndex)
    }
    return String(ARABIC_INDIC_DIGITS.indexOf(digit))
  })
}

/** Converts ASCII/Arabic-Indic digits to Persian digits (۰-۹). */
export function toPersianDigits(value) {
  if (value === null || value === undefined) {
    return ''
  }

  const text = String(value)
  if (!text || !/[0-9٠-٩]/.test(text)) {
    return text
  }

  return text.replace(/[0-9٠-٩]/g, (digit) => {
    if (digit >= '0' && digit <= '9') {
      return PERSIAN_DIGITS[Number(digit)]
    }
    const arabicIndex = ARABIC_INDIC_DIGITS.indexOf(digit)
    return arabicIndex >= 0 ? PERSIAN_DIGITS[arabicIndex] : digit
  })
}

/**
 * Converts digits to Persian (۰-۹) for visual display while preserving technical tokens
 * such as URLs, emails, UUIDs, and hex color codes verbatim.
 */
export function toPersianDisplayText(text) {
  if (typeof text !== 'string' || !text || !/[0-9٠-٩]/.test(text)) {
    return text
  }

  let result = ''
  let lastIndex = 0
  TECHNICAL_TOKEN_REGEX.lastIndex = 0

  let match
  while ((match = TECHNICAL_TOKEN_REGEX.exec(text)) !== null) {
    if (match.index > lastIndex) {
      result += toPersianDigits(text.slice(lastIndex, match.index))
    }
    result += match[0]
    lastIndex = match.index + match[0].length
  }

  if (lastIndex < text.length) {
    result += toPersianDigits(text.slice(lastIndex))
  }

  return result
}

/**
 * Sanitizes a user-typed numeric string (from Persian, Arabic, or English keyboard)
 * and computes canonical English value, Persian display string, and adjusted caret.
 */
export function sanitizeNumericInput(
  rawValue,
  { allowDecimal = true, allowNegative = false, caret = null } = {},
) {
  const source = String(rawValue ?? '')
  let english = ''
  let seenDecimal = false
  let seenSign = false
  let nextCaret = 0
  const hasCaret = typeof caret === 'number' && !Number.isNaN(caret)

  for (let i = 0; i < source.length; i++) {
    const ch = source[i]
    let mapped = null

    if (ch >= '0' && ch <= '9') {
      mapped = ch
    } else if (ch >= '۰' && ch <= '۹') {
      mapped = String(ch.charCodeAt(0) - 0x06f0)
    } else if (ch >= '٠' && ch <= '٩') {
      mapped = String(ch.charCodeAt(0) - 0x0660)
    } else if ((ch === '.' || ch === '٫') && allowDecimal && !seenDecimal) {
      mapped = '.'
      seenDecimal = true
    } else if ((ch === '-' || ch === '−') && allowNegative && english.length === 0 && !seenSign) {
      mapped = '-'
      seenSign = true
    }

    if (mapped !== null) {
      english += mapped
      if (hasCaret && i < caret) {
        nextCaret += 1
      }
    }
  }

  return {
    english,
    display: toPersianDigits(english),
    caret: hasCaret ? nextCaret : null,
  }
}

export function isSensitiveFieldKey(key) {
  if (typeof key !== 'string' || !key) {
    return false
  }

  const lower = key.trim().toLowerCase()
  if (lower === 'token' || lower === 'hash' || lower === 'signature') {
    return true
  }

  return SENSITIVE_KEY_SUBSTRINGS.some((sub) => lower.includes(sub))
}

function isPlainObject(value) {
  if (!value || typeof value !== 'object') {
    return false
  }
  const proto = Object.getPrototypeOf(value)
  return proto === Object.prototype || proto === null
}

/**
 * Recursively converts Persian/Arabic-Indic digits inside outgoing payloads to ASCII English digits
 * while keeping passwords, API keys, secrets, tokens, binary objects, and extraSettingsJson untouched.
 */
export function normalizePayloadDigits(payload) {
  if (payload === null || payload === undefined) {
    return payload
  }

  if (typeof payload === 'string') {
    return toEnglishDigits(payload)
  }

  if (Array.isArray(payload)) {
    let changed = false
    const next = payload.map((item) => {
      const normalized = normalizePayloadDigits(item)
      if (normalized !== item) {
        changed = true
      }
      return normalized
    })
    return changed ? next : payload
  }

  if (!isPlainObject(payload)) {
    return payload
  }

  let changed = false
  const next = {}
  for (const [key, value] of Object.entries(payload)) {
    if (isSensitiveFieldKey(key)) {
      next[key] = value
      continue
    }

    const normalized = normalizePayloadDigits(value)
    if (normalized !== value) {
      changed = true
    }
    next[key] = normalized
  }

  return changed ? next : payload
}

/**
 * Normalizes Persian/Arabic-Indic digits inside URL query string parameters to ASCII digits.
 */
export function normalizeUrlQueryDigits(path) {
  if (typeof path !== 'string' || !path.includes('?')) {
    return path
  }

  const queryIndex = path.indexOf('?')
  const hashIndex = path.indexOf('#', queryIndex)
  const base = path.slice(0, queryIndex)
  const rawQuery = hashIndex >= 0 ? path.slice(queryIndex + 1, hashIndex) : path.slice(queryIndex + 1)
  const hash = hashIndex >= 0 ? path.slice(hashIndex) : ''

  if (!rawQuery) {
    return path
  }

  const params = new URLSearchParams(rawQuery)
  let changed = false
  const nextParams = new URLSearchParams()

  for (const [key, value] of params.entries()) {
    if (isSensitiveFieldKey(key)) {
      nextParams.append(key, value)
      continue
    }

    const converted = toEnglishDigits(value)
    if (converted !== value) {
      changed = true
    }
    nextParams.append(key, converted)
  }

  return changed ? `${base}?${nextParams.toString()}${hash}` : path
}

const SKIP_TEXT_PARENT_TAGS = new Set([
  'SCRIPT',
  'STYLE',
  'NOSCRIPT',
  'TEMPLATE',
  'INPUT',
  'TEXTAREA',
  'SVG',
  'MATH',
  'CODE',
  'PRE',
  'KBD',
  'SAMP',
])

const EXCLUDED_PLACEHOLDER_TYPES = new Set([
  'password',
  'email',
  'url',
  'date',
  'time',
  'datetime-local',
  'month',
  'week',
  'color',
  'range',
  'checkbox',
  'radio',
  'file',
  'hidden',
])

function shouldSkipElementForDisplay(element) {
  if (!element || element.nodeType !== 1) {
    return true
  }

  if (SKIP_TEXT_PARENT_TAGS.has(element.tagName)) {
    return true
  }

  return Boolean(
    element.closest(
      '[data-latin-digits="true"], [data-persian-digits="off"], .keep-latin-digits, code, pre, kbd, samp',
    ),
  )
}

function normalizeTextNodeForDisplay(textNode) {
  if (!textNode || textNode.nodeType !== 3) {
    return
  }

  const parent = textNode.parentElement
  if (!parent || shouldSkipElementForDisplay(parent)) {
    return
  }

  const raw = textNode.nodeValue
  if (!raw || !/[0-9٠-٩]/.test(raw)) {
    return
  }

  const converted = toPersianDisplayText(raw)
  if (converted !== raw) {
    textNode.nodeValue = converted
  }
}

function normalizePlaceholderForDisplay(element) {
  if (
    !(element instanceof HTMLInputElement || element instanceof HTMLTextAreaElement) ||
    !element.hasAttribute('placeholder')
  ) {
    return
  }

  if (element.hasAttribute('data-jdp') || element.getAttribute('data-latin-digits') === 'true') {
    return
  }

  if (element instanceof HTMLInputElement && EXCLUDED_PLACEHOLDER_TYPES.has(element.type)) {
    return
  }

  const autoComplete = element.getAttribute('autocomplete')
  if (
    autoComplete === 'username' ||
    autoComplete === 'current-password' ||
    autoComplete === 'new-password'
  ) {
    return
  }

  if (element.closest('[data-latin-digits="true"], [data-persian-digits="off"], .keep-latin-digits')) {
    return
  }

  const raw = element.getAttribute('placeholder')
  if (!raw || !/[0-9٠-٩]/.test(raw)) {
    return
  }

  const converted = toPersianDisplayText(raw)
  if (converted !== raw) {
    element.setAttribute('placeholder', converted)
  }
}

function walkSubtreeForDisplay(node) {
  if (!node) {
    return
  }

  if (node.nodeType === 3) {
    normalizeTextNodeForDisplay(node)
    return
  }

  if (node.nodeType !== 1) {
    return
  }

  normalizePlaceholderForDisplay(node)

  if (shouldSkipElementForDisplay(node)) {
    return
  }

  const walker = document.createTreeWalker(node, 1 | 4) // ELEMENT_NODE | TEXT_NODE
  let current = walker.nextNode()
  while (current) {
    if (current.nodeType === 3) {
      normalizeTextNodeForDisplay(current)
    } else if (current.nodeType === 1) {
      normalizePlaceholderForDisplay(current)
    }
    current = walker.nextNode()
  }
}

/**
 * Installs a one-time MutationObserver on `root` so all displayed numbers across
 * rendered React text nodes, selects, placeholders, and dynamic tooltips use Persian digits.
 */
export function installPersianDisplayObserver(
  root = typeof document !== 'undefined' ? document.body : null,
) {
  if (typeof document === 'undefined' || !root || document.__persianDisplayObserverInstalled) {
    return
  }

  document.__persianDisplayObserverInstalled = true
  walkSubtreeForDisplay(root)

  const observer = new MutationObserver((mutations) => {
    for (const mutation of mutations) {
      if (mutation.type === 'characterData') {
        normalizeTextNodeForDisplay(mutation.target)
      } else if (mutation.type === 'attributes') {
        normalizePlaceholderForDisplay(mutation.target)
      } else if (mutation.type === 'childList') {
        for (const added of mutation.addedNodes) {
          walkSubtreeForDisplay(added)
        }
      }
    }
  })

  observer.observe(root, {
    childList: true,
    subtree: true,
    characterData: true,
    attributes: true,
    attributeFilter: ['placeholder'],
  })
}
