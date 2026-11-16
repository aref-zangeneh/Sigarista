import { useId, useRef, useState } from 'react'
import { useAdmin } from '../context/AdminContext'
import { tooltipDefault } from '../utils/tooltipRegistry'
import './Tip.css'

/**
 * Admin-defined help bubble.
 *
 * Usage (self-closing, next to the element it explains):
 *   <label>پرداخت نقدی <Tip k="pos.cash_button" /></label>
 *
 * The bubble text comes from the tooltip registry; the admin can override the
 * text or disable each tooltip from «راهنماهای صفحه‌ها».
 *   - no DB row        → registry default text
 *   - row with text    → custom text
 *   - row disabled ("") → hidden completely
 */
export function Tip({ k: tipKey, side = 'top' }) {
  const { tipOverrides } = useAdmin()
  const [open, setOpen] = useState(false)
  const [position, setPosition] = useState(side)
  const anchorRef = useRef(null)
  const tooltipId = useId()

  const override = tipOverrides?.[tipKey]
  // Unregistered key (not in registry) or explicitly disabled → no bubble.
  if (override === '' || (override === undefined && !tooltipDefault(tipKey))) {
    return null
  }

  const text = override || tooltipDefault(tipKey)

  function show() {
    const rect = anchorRef.current?.getBoundingClientRect()
    if (rect && rect.top < 120) {
      setPosition('bottom')
    } else if (rect && rect.bottom > window.innerHeight - 140) {
      setPosition('top')
    } else {
      setPosition(side)
    }
    setOpen(true)
  }

  return (
    <span ref={anchorRef} className="tip-anchor" onMouseLeave={() => setOpen(false)}>
      <button
        type="button"
        className={`tip-trigger ${open ? 'is-open' : ''}`}
        aria-label={`راهنما: ${text}`}
        aria-describedby={open ? tooltipId : undefined}
        onMouseEnter={show}
        onFocus={show}
        onBlur={() => setOpen(false)}
        onClick={(event) => {
          event.preventDefault()
          setOpen((current) => !current)
        }}
      >
        ؟
      </button>
      {open ? (
        <span role="tooltip" id={tooltipId} className={`tip-bubble tip-bubble-${position}`}>
          {text}
        </span>
      ) : null}
    </span>
  )
}
