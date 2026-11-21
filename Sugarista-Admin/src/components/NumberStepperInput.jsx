/**
 * Compact numeric input with built-in up/down stepper arrows that work seamlessly
 * alongside Sugarista's Persian-digit numeric input controls.
 */
export function NumberStepperInput({
  value,
  onChange,
  min = 0,
  max,
  step = 1,
  disabled = false,
  ariaLabel,
}) {
  const numericValue = Number(value)
  const safeCurrent = Number.isFinite(numericValue) ? numericValue : min

  const clamp = (next) => {
    let clamped = next
    if (min !== undefined && min !== null) {
      clamped = Math.max(Number(min), clamped)
    }
    if (max !== undefined && max !== null) {
      clamped = Math.min(Number(max), clamped)
    }
    return clamped
  }

  const handleStep = (delta) => {
    if (disabled) return
    const next = clamp(safeCurrent + delta * step)
    onChange(next)
  }

  const atMin = min !== undefined && min !== null && safeCurrent <= Number(min)
  const atMax = max !== undefined && max !== null && safeCurrent >= Number(max)

  return (
    <div className="number-stepper">
      <input
        className="ltr-input number-stepper-input"
        type="number"
        min={min}
        max={max}
        step={step}
        disabled={disabled}
        aria-label={ariaLabel}
        value={value}
        onChange={(event) => onChange(event.target.value)}
      />
      <div className="number-stepper-buttons">
        <button
          type="button"
          className="number-stepper-btn"
          disabled={disabled || atMax}
          onClick={() => handleStep(1)}
          aria-label="افزایش"
          tabIndex={-1}
        >
          <svg viewBox="0 0 12 12" width="10" height="10" aria-hidden="true">
            <path d="M2.5 7.5 6 4l3.5 3.5" fill="none" stroke="currentColor" strokeWidth="1.8" strokeLinecap="round" strokeLinejoin="round" />
          </svg>
        </button>
        <button
          type="button"
          className="number-stepper-btn"
          disabled={disabled || atMin}
          onClick={() => handleStep(-1)}
          aria-label="کاهش"
          tabIndex={-1}
        >
          <svg viewBox="0 0 12 12" width="10" height="10" aria-hidden="true">
            <path d="M2.5 4.5 6 8l3.5-3.5" fill="none" stroke="currentColor" strokeWidth="1.8" strokeLinecap="round" strokeLinejoin="round" />
          </svg>
        </button>
      </div>
    </div>
  )
}
