import { useEffect, useId, useRef, useState } from 'react'
import { createPortal } from 'react-dom'
import { useAdmin } from '../context/AdminContext'
import { tooltipDefault } from '../utils/tooltipRegistry'
import './Tip.css'

/**
 * Admin-defined help bubble with smart fixed viewport positioning.
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
  const [coords, setCoords] = useState({ top: 0, left: 12, width: 250, placement: side })
  const anchorRef = useRef(null)
  const tooltipId = useId()

  const override = tipOverrides?.[tipKey]
  // Unregistered key (not in registry) or explicitly disabled → no bubble.
  if (override === '' || (override === undefined && !tooltipDefault(tipKey))) {
    return null
  }

  const text = override || tooltipDefault(tipKey)

  function computePosition() {
    const rect = anchorRef.current?.getBoundingClientRect()
    if (!rect) return
    const vw = window.innerWidth || 360
    const vh = window.innerHeight || 640
    const margin = 12
    const bubbleWidth = Math.min(268, vw - margin * 2)
    const centerX = rect.left + rect.width / 2
    const left = Math.max(margin, Math.min(vw - bubbleWidth - margin, Math.round(centerX - bubbleWidth / 2)))

    let placement = side
    if (rect.top < 130) {
      placement = 'bottom'
    } else if (rect.bottom > vh - 130) {
      placement = 'top'
    }

    const top = placement === 'bottom' ? Math.round(rect.bottom + 8) : Math.round(rect.top - 8)
    setCoords({ top, left, width: bubbleWidth, placement })
  }

  function show() {
    computePosition()
    setOpen(true)
  }

  useEffect(() => {
    if (!open) return undefined

    function onScrollOrResize() {
      computePosition()
    }
    function onKeyDown(event) {
      if (event.key === 'Escape') setOpen(false)
    }
    function onPointerDown(event) {
      if (anchorRef.current && !anchorRef.current.contains(event.target)) {
        setOpen(false)
      }
    }

    window.addEventListener('scroll', onScrollOrResize, true)
    window.addEventListener('resize', onScrollOrResize)
    window.addEventListener('keydown', onKeyDown)
    window.addEventListener('pointerdown', onPointerDown)
    return () => {
      window.removeEventListener('scroll', onScrollOrResize, true)
      window.removeEventListener('resize', onScrollOrResize)
      window.removeEventListener('keydown', onKeyDown)
      window.removeEventListener('pointerdown', onPointerDown)
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [open])

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
          event.stopPropagation()
          if (open) {
            setOpen(false)
          } else {
            show()
          }
        }}
      >
        ؟
      </button>
      {open && typeof document !== 'undefined'
        ? createPortal(
            <span
              role="tooltip"
              id={tooltipId}
              className={`tip-bubble tip-bubble-portal tip-bubble-${coords.placement}`}
              style={{
                top: `${coords.top}px`,
                left: `${coords.left}px`,
                width: `${coords.width}px`,
              }}
            >
              {text}
            </span>,
            document.body,
          )
        : null}
    </span>
  )
}
