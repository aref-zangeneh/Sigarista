// Global Persian-digit handling for the admin panel.
//
// Keyboards on Persian layouts produce ۰-۹ (U+06F0–U+06F9) and sometimes
// Arabic-Indic ٠-٩ (U+0660–U+0669). Those characters break every Number(...)
// conversion and every numeric API field, so the admin auto-converts them to
// plain English digits the moment they are typed — globally, in a single
// capture-phase listener (see installPersianDigitNormalizer).

const PERSIAN_DIGITS = '۰۱۲۳۴۵۶۷۸۹'
const ARABIC_INDIC_DIGITS = '٠١٢٣٤٥٦٧٨٩'

/** Converts Persian (۰-۹) and Arabic-Indic (٠-٩) digits to English (0-9). */
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

const CONVERTIBLE_INPUT_TYPES = new Set(['text', 'number', 'tel', 'search', 'url'])

/**
 * Installs a one-time, document-level capture listener that rewrites Persian
 * digits inside form fields as the user types. Capture phase matters: it runs
 * before React's delegated onChange handlers at the root, so the React state
 * (and everything saved to the API) already contains English digits.
 *
 * Scope:
 * - inputs of types text/number/tel/search/url — always converted
 * - textareas — only converted when they carry `data-normalize-digits`
 *   (narrative Persian text keeps its own Persian numerals)
 * - any field can opt out with `data-persian-digits="allow"`
 */
export function installPersianDigitNormalizer() {
  if (typeof document === 'undefined' || document.__persianDigitNormalizerInstalled) {
    return
  }

  document.__persianDigitNormalizerInstalled = true

  document.addEventListener(
    'input',
    (event) => {
      const target = event.target
      if (!(target instanceof HTMLInputElement) && !(target instanceof HTMLTextAreaElement)) {
        return
      }

      if (target instanceof HTMLInputElement) {
        if (!CONVERTIBLE_INPUT_TYPES.has(target.type)) {
          return
        }
      } else if (!target.hasAttribute('data-normalize-digits')) {
        return
      }

      if (target.getAttribute('data-persian-digits') === 'allow') {
        return
      }

      const value = target.value
      if (!value || !/[۰-۹٠-٩]/.test(value)) {
        return
      }

      const converted = toEnglishDigits(value)
      if (converted === value) {
        return
      }

      // Rewrite the value through the NATIVE prototype setter instead of
      // `target.value = ...`. React shadows the instance `value` property
      // with its own change-tracker; going around it keeps React's tracker
      // pointing at the pre-conversion value, so the very same input event
      // still looks like a change and React's onChange fires with the
      // converted value. Setting the property directly would update React's
      // tracker and silently swallow the change (the field would snap back).
      const nativeValueSetter = Object.getOwnPropertyDescriptor(
        (target instanceof HTMLInputElement
          ? window.HTMLInputElement
          : window.HTMLTextAreaElement
        ).prototype,
        'value',
      ).set
      nativeValueSetter.call(target, converted)

      // Persian and English digits map 1:1, so the string length never
      // changes and the caret offset stays valid — restore it so typing in
      // the middle of a field does not jump the caret to the end.
      const selectionStart = target.selectionStart
      const selectionEnd = target.selectionEnd
      if (typeof selectionStart === 'number' && selectionStart !== null) {
        try {
          target.setSelectionRange(selectionStart, selectionEnd)
        } catch {
          // Number inputs do not expose selection APIs in some browsers.
        }
      }
    },
    true,
  )
}
