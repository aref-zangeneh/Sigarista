import { useEffect, useId, useRef } from 'react'
import { gregorianIsoToShamsiLabel, shamsiTextToGregorianIso } from '../utils/shamsiDate'
import { ensureJalaliDatePickerStarted } from '../utils/jalaliDatePickerSetup'
import './ShamsiDatePicker.css'

function readGregorianValue(visibleInput, hiddenInput) {
  if (hiddenInput.value) {
    return hiddenInput.value
  }

  return shamsiTextToGregorianIso(visibleInput.value)
}

export function ShamsiDatePicker({
  value,
  onChange,
  required = false,
  placeholder = 'انتخاب تاریخ از تقویم شمسی',
  minDate,
  maxDate,
}) {
  const visibleRef = useRef(null)
  const gregorianRef = useRef(null)
  const reactId = useId().replace(/:/g, '')
  const gregorianInputId = `jalali-gregorian-${reactId}`

  useEffect(() => {
    ensureJalaliDatePickerStarted()
  }, [])

  useEffect(() => {
    const visibleInput = visibleRef.current
    const hiddenInput = gregorianRef.current
    if (!visibleInput || !hiddenInput) {
      return undefined
    }

    function syncFromPicker() {
      // The picker dispatches change before writing the hidden Gregorian field.
      queueMicrotask(() => {
        onChange(readGregorianValue(visibleInput, hiddenInput))
      })
    }

    visibleInput.addEventListener('jdp:change', syncFromPicker)
    visibleInput.addEventListener('change', syncFromPicker)

    return () => {
      visibleInput.removeEventListener('jdp:change', syncFromPicker)
      visibleInput.removeEventListener('change', syncFromPicker)
    }
  }, [onChange])

  useEffect(() => {
    const visibleInput = visibleRef.current
    const hiddenInput = gregorianRef.current
    if (!visibleInput || !hiddenInput) {
      return
    }

    if (value) {
      hiddenInput.value = value
      visibleInput.value = gregorianIsoToShamsiLabel(value)
      return
    }

    hiddenInput.value = ''
    visibleInput.value = ''
  }, [value])

  return (
    <div className="shamsi-date-field">
      <input
        ref={visibleRef}
        type="text"
        data-jdp
        data-jdp-only-date
        data-jdp-target-value-input={`#${gregorianInputId}`}
        data-jdp-target-value-type="gregorian"
        {...(minDate ? { 'data-jdp-min-date': minDate } : {})}
        {...(maxDate ? { 'data-jdp-max-date': maxDate } : {})}
        className="shamsi-date-input ltr-input"
        placeholder={placeholder}
        required={required}
        autoComplete="off"
        readOnly={false}
      />
      <input type="hidden" id={gregorianInputId} ref={gregorianRef} />
      <small className="table-subtext">روی فیلد کلیک کنید تا تقویم جلالی باز شود</small>
    </div>
  )
}
