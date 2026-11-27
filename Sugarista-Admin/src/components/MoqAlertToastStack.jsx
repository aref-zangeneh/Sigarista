import { useEffect, useMemo, useRef, useState } from 'react'
import { API_BASE_URL } from '../utils/adminApi'
import { toPersianNumber } from '../utils/adminFormatters'
import {
  alertSignature,
  detectNewOrEscalatedAlerts,
  filterVisibleToasts,
  getMoqSeverityMeta,
  getMoqSourceMeta,
  parseSseChunk,
} from '../utils/moqHelpers.js'
import './MoqAlertToastStack.css'

const MAX_VISIBLE_CARDS = 3

function playSubtleMoqChime(isUrgent) {
  try {
    const AudioCtx = window.AudioContext || window.webkitAudioContext
    if (!AudioCtx) return
    const ctx = new AudioCtx()
    const now = ctx.currentTime
    const notes = isUrgent ? [523.25, 659.25, 783.99] : [493.88, 659.25]
    notes.forEach((freq, idx) => {
      const osc = ctx.createOscillator()
      const gain = ctx.createGain()
      osc.type = 'sine'
      osc.frequency.setValueAtTime(freq, now + idx * 0.11)
      gain.gain.setValueAtTime(0.0001, now + idx * 0.11)
      gain.gain.exponentialRampToValueAtTime(0.055, now + idx * 0.11 + 0.02)
      gain.gain.exponentialRampToValueAtTime(0.0001, now + idx * 0.11 + 0.22)
      osc.connect(gain)
      gain.connect(ctx.destination)
      osc.start(now + idx * 0.11)
      osc.stop(now + idx * 0.11 + 0.24)
    })
    setTimeout(() => ctx.close().catch(() => {}), 700)
  } catch {
    // Autoplay policy or unsupported environment
  }
}

export function MoqAlertToastStack({ token, enabled = true, onOpenMoqPage, onSnapshotChange }) {
  const [snapshot, setSnapshot] = useState(null)
  const [dismissed, setDismissed] = useState({})
  const [collapsed, setCollapsed] = useState(false)

  const prevMapRef = useRef(new Map())
  const initializedRef = useRef(false)
  const onSnapshotChangeRef = useRef(onSnapshotChange)
  onSnapshotChangeRef.current = onSnapshotChange

  useEffect(() => {
    if (!token || !enabled) {
      setSnapshot(null)
      prevMapRef.current = new Map()
      initializedRef.current = false
      return undefined
    }

    let cancelled = false
    let abortController = null
    let reconnectTimer = null
    let backoffMs = 2000

    const applySnapshot = (nextSnapshot, connected) => {
      if (cancelled || !nextSnapshot) return

      const items = Array.isArray(nextSnapshot.items) ? nextSnapshot.items : []
      if (initializedRef.current) {
        const newlyTriggered = detectNewOrEscalatedAlerts(prevMapRef.current, items)
        if (newlyTriggered.length > 0) {
          const hasUrgent = newlyTriggered.some(
            (it) => getMoqSeverityMeta(it.severity).rank >= 2,
          )
          playSubtleMoqChime(hasUrgent)
        }
      } else {
        initializedRef.current = true
      }

      const nextMap = new Map()
      for (const item of items) {
        nextMap.set(item.alertKey, {
          rank: getMoqSeverityMeta(item.severity).rank,
          currentQuantity: item.currentQuantity,
        })
      }
      prevMapRef.current = nextMap
      setSnapshot(nextSnapshot)

      onSnapshotChangeRef.current?.(nextSnapshot, connected)
      window.dispatchEvent(
        new CustomEvent('sugarista:moq-snapshot', {
          detail: { snapshot: nextSnapshot, connected },
        }),
      )
    }

    const connect = async () => {
      if (cancelled) return
      abortController = new AbortController()

      try {
        const response = await fetch(`${API_BASE_URL}/admin/moq-alerts/stream`, {
          method: 'GET',
          headers: {
            Accept: 'text/event-stream',
            Authorization: `Bearer ${token}`,
          },
          cache: 'no-store',
          signal: abortController.signal,
        })

        if (!response.ok || !response.body) {
          throw new Error(`SSE status ${response.status}`)
        }

        backoffMs = 2000
        const reader = response.body.getReader()
        const decoder = new TextDecoder('utf-8')
        let buffer = ''

        while (!cancelled) {
          const { value, done } = await reader.read()
          if (done) break
          buffer += decoder.decode(value, { stream: true })
          const { events, remainder } = parseSseChunk(buffer)
          buffer = remainder

          for (const evt of events) {
            if (evt.event === 'moq_snapshot' && evt.data) {
              try {
                const parsed = JSON.parse(evt.data)
                applySnapshot(parsed, true)
              } catch {
                // Ignore malformed frame
              }
            }
          }
        }
      } catch {
        if (cancelled) return
        window.dispatchEvent(
          new CustomEvent('sugarista:moq-snapshot', {
            detail: { snapshot: null, connected: false },
          }),
        )
      }

      if (!cancelled) {
        const delay = backoffMs
        backoffMs = Math.min(25000, backoffMs * 2)
        reconnectTimer = window.setTimeout(connect, delay)
      }
    }

    connect()

    return () => {
      cancelled = true
      if (reconnectTimer) window.clearTimeout(reconnectTimer)
      if (abortController) abortController.abort()
    }
  }, [token, enabled])

  const visibleItems = useMemo(
    () => filterVisibleToasts(snapshot?.items ?? [], dismissed),
    [snapshot, dismissed],
  )

  if (!enabled || visibleItems.length === 0) {
    return null
  }

  const urgentCount = visibleItems.filter((it) => getMoqSeverityMeta(it.severity).rank >= 2).length
  const cardsToShow = collapsed ? [] : visibleItems.slice(0, MAX_VISIBLE_CARDS)
  const hiddenMoreCount = Math.max(0, visibleItems.length - cardsToShow.length)

  const handleDismiss = (item) => {
    setDismissed((prev) => ({
      ...prev,
      [item.alertKey]: alertSignature(item),
    }))
  }

  const handleDismissAll = () => {
    setDismissed((prev) => {
      const next = { ...prev }
      for (const item of visibleItems) {
        next[item.alertKey] = alertSignature(item)
      }
      return next
    })
  }

  return (
    <aside
      className="moq-toast-stack"
      role="region"
      aria-label="هشدارهای زنده کمبود موجودی (MOQ)"
      aria-live="polite"
    >
      <div className="moq-toast-header-pill">
        <div className="moq-toast-header-left">
          <span
            className={`moq-pulse-dot ${urgentCount > 0 ? 'moq-pulse-dot--danger' : ''}`}
            aria-hidden="true"
          />
          <span>
            {toPersianNumber(visibleItems.length)} هشدار کمبود موجودی
          </span>
        </div>
        <div className="moq-toast-header-actions">
          <button
            type="button"
            className="moq-toast-pill-btn"
            onClick={() => setCollapsed((c) => !c)}
          >
            {collapsed ? 'نمایش' : 'جمع‌کردن'}
          </button>
          <button
            type="button"
            className="moq-toast-pill-btn"
            onClick={onOpenMoqPage}
          >
            همه
          </button>
          <button
            type="button"
            className="moq-toast-pill-btn"
            aria-label="بستن همه هشدارها"
            onClick={handleDismissAll}
          >
            ×
          </button>
        </div>
      </div>

      {cardsToShow.map((item) => {
        const sev = getMoqSeverityMeta(item.severity)
        const src = getMoqSourceMeta(item.sourceType)
        return (
          <article
            key={item.alertKey}
            className={`moq-toast-card moq-toast-card--${sev.tone}`}
          >
            <div className="moq-toast-top">
              <div className="moq-toast-title-wrap">
                <span className="moq-toast-title" title={item.name}>
                  {item.name}
                </span>
                <span className="moq-toast-context">
                  {src.label}
                  {item.contextLabel ? ` • ${item.contextLabel}` : ''}
                </span>
              </div>
              <button
                type="button"
                className="moq-toast-close"
                aria-label={`بستن هشدار ${item.name}`}
                onClick={() => handleDismiss(item)}
              >
                ×
              </button>
            </div>

            <div className="moq-toast-metrics">
              <span className={`moq-toast-badge moq-toast-badge--${sev.tone}`}>
                {sev.label}
              </span>
              <span className="moq-toast-qty">
                {toPersianNumber(item.currentQuantity)} / {toPersianNumber(item.moqQuantity)} {item.unit}
              </span>
            </div>

            <div className="moq-toast-bar-track" aria-hidden="true">
              <div
                className={`moq-toast-bar-fill moq-toast-bar-fill--${sev.tone}`}
                style={{ width: `${Math.min(100, Math.max(4, Number(item.fillPercent) || 0))}%` }}
              />
            </div>

            <div className="moq-toast-footer">
              <span>
                {item.deficitQuantity > 0
                  ? `کسری: ${toPersianNumber(item.deficitQuantity)} ${item.unit}`
                  : 'در مرز نقطه سفارش'}
              </span>
              <button
                type="button"
                className="moq-toast-open-btn"
                onClick={onOpenMoqPage}
              >
                شارژ / مدیریت ←
              </button>
            </div>
          </article>
        )
      })}

      {!collapsed && hiddenMoreCount > 0 && (
        <button
          type="button"
          className="moq-toast-header-pill"
          style={{ justifyContent: 'center', cursor: 'pointer' }}
          onClick={onOpenMoqPage}
        >
          + {toPersianNumber(hiddenMoreCount)} قلم دیگر در صفحه هشدارهای MOQ ←
        </button>
      )}
    </aside>
  )
}
