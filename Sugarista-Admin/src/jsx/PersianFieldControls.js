import {
  createElement,
  useEffect,
  useLayoutEffect,
  useRef,
  useState,
} from 'react'
import {
  preserveTypedDigitsDisplay,
  sanitizeNumericInput,
  toEnglishDigits,
  toPersianDigits,
  toPersianDisplayText,
} from '../utils/persianDigits.js'

function assignRef(ref, node) {
  if (!ref) {
    return
  }
  if (typeof ref === 'function') {
    ref(node)
    return
  }
  if (typeof ref === 'object') {
    ref.current = node
  }
}

function setNativeValueAndCaret(element, value, caret) {
  if (!element) {
    return
  }

  if (element.value !== value) {
    element.value = value
  }

  if (
    typeof caret === 'number' &&
    document.activeElement === element &&
    typeof element.setSelectionRange === 'function'
  ) {
    const clamped = Math.max(0, Math.min(caret, value.length))
    try {
      element.setSelectionRange(clamped, clamped)
    } catch {
      // Ignore selection errors on elements that disallow selection.
    }
  }
}

function createNormalizedEvent(event, realTarget, englishValue, isNumeric) {
  const numericValue = isNumeric
    ? englishValue === '' || englishValue === '-' || englishValue === '.'
      ? Number.NaN
      : Number(englishValue)
    : Number.NaN

  const makeTargetProxy = (base) =>
    new Proxy(base, {
      get(target, prop) {
        if (prop === 'value') {
          return englishValue
        }
        if (prop === 'valueAsNumber') {
          return numericValue
        }
        const value = Reflect.get(target, prop, target)
        return typeof value === 'function' ? value.bind(target) : value
      },
      set(target, prop, newValue) {
        return Reflect.set(target, prop, newValue, target)
      },
    })

  const targetProxy = makeTargetProxy(realTarget)
  const currentTargetProxy =
    event.currentTarget === realTarget || !event.currentTarget
      ? targetProxy
      : makeTargetProxy(event.currentTarget)

  return new Proxy(event, {
    get(target, prop) {
      if (prop === 'target') {
        return targetProxy
      }
      if (prop === 'currentTarget') {
        return currentTargetProxy
      }
      const value = Reflect.get(target, prop, target)
      return typeof value === 'function' ? value.bind(target) : value
    },
  })
}

function applyNumericValidity(element, englishValue, min, max) {
  if (!element || typeof element.setCustomValidity !== 'function') {
    return
  }

  if (englishValue === '' || englishValue === null || englishValue === undefined) {
    element.setCustomValidity('')
    return
  }

  const parsed = Number(englishValue)
  if (!Number.isFinite(parsed)) {
    element.setCustomValidity('عدد معتبر وارد کنید.')
    return
  }

  if (min !== undefined && min !== '' && parsed < Number(min)) {
    element.setCustomValidity(`مقدار باید بزرگ‌تر یا مساوی ${toPersianDigits(min)} باشد.`)
    return
  }

  if (max !== undefined && max !== '' && parsed > Number(max)) {
    element.setCustomValidity(`مقدار باید کوچک‌تر یا مساوی ${toPersianDigits(max)} باشد.`)
    return
  }

  element.setCustomValidity('')
}

function getNumericConfig(props) {
  const isNumeric = props.type === 'number'
  const rawStep = props.step !== undefined && props.step !== '' ? String(props.step).trim() : ''
  const hasExplicitIntegerStep = rawStep !== '' && rawStep !== 'any' && /^\d+$/.test(rawStep)
  const hasFractionalStep = rawStep === 'any' || (rawStep !== '' && !hasExplicitIntegerStep)
  const allowDecimal = !hasExplicitIntegerStep
  const allowNegative =
    props.min === undefined || props.min === '' || Number(props.min) < 0

  return {
    isNumeric,
    hasFractionalStep,
    allowDecimal,
    allowNegative,
  }
}

function resolveNumericDisplayValue(propValue, isFocused, draft) {
  if (draft !== null) {
    const draftEnglish = draft.english
    const isEmptyProp =
      propValue === '' ||
      propValue === 0 ||
      propValue === null ||
      propValue === undefined ||
      Number.isNaN(propValue)

    if (
      (isFocused && draftEnglish === '' && isEmptyProp) ||
      (isFocused && draftEnglish === '-' && (isEmptyProp || propValue === '-')) ||
      (draftEnglish !== '' &&
        draftEnglish !== '-' &&
        (String(propValue) === draftEnglish ||
          (Number.isFinite(Number(draftEnglish)) &&
            propValue !== '' &&
            propValue !== null &&
            propValue !== undefined &&
            Number(draftEnglish) === Number(propValue))))
    ) {
      return draft.display
    }
  }

  if (propValue === undefined || propValue === null || Number.isNaN(propValue)) {
    return ''
  }

  return toPersianDigits(String(propValue))
}

function resolveTextDisplayValue(propValue, draft) {
  if (propValue === undefined || propValue === null) {
    return ''
  }

  const rawProp = String(propValue)
  if (draft !== null && toEnglishDigits(rawProp) === draft.english) {
    return draft.display
  }

  return toPersianDisplayText(rawProp)
}

export function PersianFieldInput(props) {
  const {
    ref: externalRef,
    type,
    value,
    defaultValue,
    onChange,
    onBlur,
    onFocus,
    onKeyDown,
    min,
    max,
    step,
    inputMode,
    dir,
    placeholder,
    'data-latin-digits': _latinAttr,
    ...rest
  } = props

  const localRef = useRef(null)
  const isFocusedRef = useRef(false)
  // Stores { english, display } for the last value typed by the user so their
  // keyboard script (Persian vs English digits) is preserved visually while
  // React state and DB payloads always receive English digits.
  const draftRef = useRef(null)
  const preferredScriptRef = useRef('persian')
  const pendingCaretRef = useRef(null)
  const [, forceBlurSync] = useState(0)

  const { isNumeric, hasFractionalStep, allowDecimal, allowNegative } = getNumericConfig({
    type,
    min,
    step,
  })
  const isControlled = value !== undefined

  const formatForDisplay = (raw) => {
    if (raw === undefined || raw === null) {
      return ''
    }
    return isNumeric ? toPersianDigits(String(raw)) : toPersianDisplayText(String(raw))
  }

  const displayValue = isControlled
    ? isNumeric
      ? resolveNumericDisplayValue(value, isFocusedRef.current, draftRef.current)
      : resolveTextDisplayValue(value, draftRef.current)
    : undefined

  const displayDefaultValue =
    !isControlled && defaultValue !== undefined ? formatForDisplay(defaultValue) : undefined

  const displayPlaceholder =
    typeof placeholder === 'string' ? toPersianDisplayText(placeholder) : placeholder

  useLayoutEffect(() => {
    const el = localRef.current
    if (!el || pendingCaretRef.current === null) {
      return
    }

    const caret = pendingCaretRef.current
    pendingCaretRef.current = null
    if (document.activeElement === el && typeof el.setSelectionRange === 'function') {
      const clamped = Math.max(0, Math.min(caret, el.value.length))
      try {
        el.setSelectionRange(clamped, clamped)
      } catch {
        // Ignore selection errors.
      }
    }
  })

  useEffect(() => {
    if (!isNumeric || !isControlled) {
      return
    }
    applyNumericValidity(
      localRef.current,
      value === undefined || value === null ? '' : toEnglishDigits(String(value)),
      min,
      max,
    )
  }, [isNumeric, isControlled, value, min, max])

  const emitNumericValue = (event, english, nextDisplay, nextCaret, script = null) => {
    const el = localRef.current ?? event.target
    if (script) {
      preferredScriptRef.current = script
    }
    draftRef.current = { english, display: nextDisplay }
    pendingCaretRef.current = nextCaret

    applyNumericValidity(el, english, min, max)
    onChange?.(createNormalizedEvent(event, el, english, true))
    setNativeValueAndCaret(el, nextDisplay, nextCaret)
  }

  const handleFocus = (event) => {
    isFocusedRef.current = true
    onFocus?.(event)
  }

  const handleBlur = (event) => {
    isFocusedRef.current = false
    pendingCaretRef.current = null
    const draft = draftRef.current
    // Clear incomplete numeric drafts ("", "-", trailing ".") on blur while keeping
    // valid typed script drafts so English-typed numbers stay English after blur.
    if (
      isNumeric &&
      draft !== null &&
      (draft.english === '' || draft.english === '-' || draft.english.endsWith('.'))
    ) {
      draftRef.current = null
      forceBlurSync((tick) => tick + 1)
    }
    onBlur?.(event)
  }

  const handleChange = (event) => {
    const el = event.target
    const rawValue = el.value
    const rawCaret = typeof el.selectionStart === 'number' ? el.selectionStart : null

    if (isNumeric) {
      const { english, display, caret, script } = sanitizeNumericInput(rawValue, {
        allowDecimal,
        allowNegative,
        caret: rawCaret,
        preserveScript: true,
      })
      emitNumericValue(event, english, display, caret, script)
      return
    }

    const english = toEnglishDigits(rawValue)
    const display = preserveTypedDigitsDisplay(rawValue)
    draftRef.current = { english, display }
    pendingCaretRef.current = rawCaret
    onChange?.(createNormalizedEvent(event, el, english, false))
    setNativeValueAndCaret(el, display, rawCaret)
  }

  const handleKeyDown = (event) => {
    onKeyDown?.(event)
    if (event.defaultPrevented || !isNumeric) {
      return
    }

    if (event.key !== 'ArrowUp' && event.key !== 'ArrowDown') {
      return
    }

    event.preventDefault()
    const el = event.currentTarget
    const currentEnglish =
      toEnglishDigits(el.value) ||
      (isControlled && value !== '' && value !== null && value !== undefined
        ? toEnglishDigits(String(value))
        : '0')
    const currentNum = Number(currentEnglish)
    const baseNum = Number.isFinite(currentNum) ? currentNum : 0
    const numericStep = step && step !== 'any' && Number(step) > 0 ? Number(step) : 1
    let nextNum = baseNum + (event.key === 'ArrowUp' ? numericStep : -numericStep)

    // Avoid IEEE-754 float tails when stepping by e.g. 0.01 or 0.1.
    const precision = (String(numericStep).split('.')[1] || '').length
    if (precision > 0) {
      nextNum = Number(nextNum.toFixed(precision))
    }

    if (min !== undefined && min !== '') {
      nextNum = Math.max(Number(min), nextNum)
    }
    if (max !== undefined && max !== '') {
      nextNum = Math.min(Number(max), nextNum)
    }

    const english = String(nextNum)
    const display = preferredScriptRef.current === 'latin' ? english : toPersianDigits(english)
    emitNumericValue(event, english, display, display.length, preferredScriptRef.current)
  }

  const resolvedType = isNumeric ? 'text' : (type ?? 'text')
  const resolvedInputMode =
    inputMode ?? (isNumeric ? (hasFractionalStep ? 'decimal' : 'numeric') : undefined)
  const resolvedDir = dir ?? (isNumeric || type === 'tel' ? 'ltr' : undefined)

  return createElement('input', {
    ...rest,
    ref: (node) => {
      localRef.current = node
      assignRef(externalRef, node)
    },
    type: resolvedType,
    inputMode: resolvedInputMode,
    dir: resolvedDir,
    placeholder: displayPlaceholder,
    ...(isNumeric ? { 'data-numeric-input': 'true' } : {}),
    ...(isControlled ? { value: displayValue } : {}),
    ...(displayDefaultValue !== undefined ? { defaultValue: displayDefaultValue } : {}),
    onFocus: handleFocus,
    onBlur: handleBlur,
    onChange: handleChange,
    onKeyDown: handleKeyDown,
  })
}

export function PersianFieldTextarea(props) {
  const {
    ref: externalRef,
    value,
    defaultValue,
    onChange,
    placeholder,
    'data-latin-digits': _latinAttr,
    ...rest
  } = props

  const localRef = useRef(null)
  const draftRef = useRef(null)
  const pendingCaretRef = useRef(null)
  const isControlled = value !== undefined

  const displayValue = isControlled
    ? resolveTextDisplayValue(value, draftRef.current)
    : undefined

  const displayDefaultValue =
    !isControlled && defaultValue !== undefined
      ? defaultValue === null
        ? ''
        : toPersianDisplayText(String(defaultValue))
      : undefined

  const displayPlaceholder =
    typeof placeholder === 'string' ? toPersianDisplayText(placeholder) : placeholder

  useLayoutEffect(() => {
    const el = localRef.current
    if (!el || pendingCaretRef.current === null) {
      return
    }

    const caret = pendingCaretRef.current
    pendingCaretRef.current = null
    if (document.activeElement === el && typeof el.setSelectionRange === 'function') {
      const clamped = Math.max(0, Math.min(caret, el.value.length))
      try {
        el.setSelectionRange(clamped, clamped)
      } catch {
        // Ignore selection errors.
      }
    }
  })

  const handleChange = (event) => {
    const el = event.target
    const rawValue = el.value
    const rawCaret = typeof el.selectionStart === 'number' ? el.selectionStart : null

    const english = toEnglishDigits(rawValue)
    const display = preserveTypedDigitsDisplay(rawValue)
    draftRef.current = { english, display }
    pendingCaretRef.current = rawCaret

    onChange?.(createNormalizedEvent(event, el, english, false))
    setNativeValueAndCaret(el, display, rawCaret)
  }

  return createElement('textarea', {
    ...rest,
    ref: (node) => {
      localRef.current = node
      assignRef(externalRef, node)
    },
    placeholder: displayPlaceholder,
    ...(isControlled ? { value: displayValue } : {}),
    ...(displayDefaultValue !== undefined ? { defaultValue: displayDefaultValue } : {}),
    onChange: handleChange,
  })
}
