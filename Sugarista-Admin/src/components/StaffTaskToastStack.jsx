import { useCallback, useEffect, useRef, useState } from 'react'
import { apiRequest, getReadableError } from '../utils/adminApi.js'
import { toPersianDigits } from '../utils/persianDigits.js'
import { PRIORITY_META, formatOverdueLabel } from '../utils/staffTasksHelpers.js'
import './StaffTaskToastStack.css'

const POLL_INTERVAL_MS = 20000

function playGentleTaskChime() {
  try {
    const AudioCtx = window.AudioContext || window.webkitAudioContext
    if (!AudioCtx) return
    const ctx = new AudioCtx()
    const now = ctx.currentTime
    const notes = [587.33, 880]
    notes.forEach((freq, i) => {
      const osc = ctx.createOscillator()
      const gain = ctx.createGain()
      osc.type = 'sine'
      osc.frequency.setValueAtTime(freq, now + i * 0.14)
      gain.gain.setValueAtTime(0.0001, now + i * 0.14)
      gain.gain.exponentialRampToValueAtTime(0.07, now + i * 0.14 + 0.02)
      gain.gain.exponentialRampToValueAtTime(0.0001, now + i * 0.14 + 0.26)
      osc.connect(gain)
      gain.connect(ctx.destination)
      osc.start(now + i * 0.14)
      osc.stop(now + i * 0.14 + 0.28)
    })
    setTimeout(() => ctx.close().catch(() => {}), 700)
  } catch {
    // Autoplay policy or unsupported environment — fail silently
  }
}

export function StaffTaskToastStack({ token, onOpenStaffTasks }) {
  const [items, setItems] = useState([])
  const [notes, setNotes] = useState({})
  const [noteOpenId, setNoteOpenId] = useState(null)
  const [busyId, setBusyId] = useState(null)
  const [toastError, setToastError] = useState({})
  const [collapsed, setCollapsed] = useState(() =>
    typeof window !== 'undefined' && window.matchMedia?.('(max-width: 640px)')?.matches,
  )
  const knownIdsRef = useRef(new Set())
  const initializedRef = useRef(false)

  const fetchDue = useCallback(async () => {
    if (!token) return
    try {
      const data = await apiRequest('/admin/staff-tasks/my-due', { token })
      const nextItems = Array.isArray(data?.items) ? data.items : []
      setItems(nextItems)

      const nextKeys = new Set(nextItems.map((it) => `${it.id}:${it.occurrenceDate}`))
      if (initializedRef.current) {
        let hasBrandNew = false
        for (const key of nextKeys) {
          if (!knownIdsRef.current.has(key)) {
            hasBrandNew = true
            break
          }
        }
        if (hasBrandNew) {
          playGentleTaskChime()
        }
      } else {
        initializedRef.current = true
      }
      knownIdsRef.current = nextKeys
    } catch {
      // Silent background poll
    }
  }, [token])

  useEffect(() => {
    if (!token) {
      setItems([])
      knownIdsRef.current = new Set()
      initializedRef.current = false
      return undefined
    }

    fetchDue()
    const interval = window.setInterval(fetchDue, POLL_INTERVAL_MS)
    const onFocus = () => fetchDue()
    const onChanged = () => fetchDue()

    window.addEventListener('focus', onFocus)
    window.addEventListener('sugarista:staff-tasks-changed', onChanged)
    return () => {
      window.clearInterval(interval)
      window.removeEventListener('focus', onFocus)
      window.removeEventListener('sugarista:staff-tasks-changed', onChanged)
    }
  }, [token, fetchDue])

  const handleComplete = async (task) => {
    const noteText = (notes[task.id] || '').trim()
    if (task.requireNoteOnComplete && !noteText) {
      setNoteOpenId(task.id)
      setToastError((prev) => ({
        ...prev,
        [task.id]: 'توضیح کوتاه الزامی است.',
      }))
      return
    }

    setBusyId(task.id)
    setToastError((prev) => ({ ...prev, [task.id]: '' }))
    try {
      await apiRequest(`/admin/staff-tasks/${task.id}/complete`, {
        method: 'POST',
        token,
        body: {
          note: noteText || null,
          occurrenceDate: task.occurrenceDate,
        },
      })
      setItems((prev) =>
        prev.filter((item) => !(item.id === task.id && item.occurrenceDate === task.occurrenceDate)),
      )
      setNotes((prev) => {
        const next = { ...prev }
        delete next[task.id]
        return next
      })
      window.dispatchEvent(new CustomEvent('sugarista:staff-tasks-changed'))
    } catch (err) {
      setToastError((prev) => ({ ...prev, [task.id]: getReadableError(err) }))
    } finally {
      setBusyId(null)
    }
  }

  if (!items.length) return null

  const visibleItems = collapsed && items.length > 1 ? items.slice(0, 1) : items

  return (
    <aside
      className="st-toast-stack"
      role="region"
      aria-label="تسک‌های الزامی در انتظار انجام"
      aria-live="assertive"
    >
      {items.length > 1 ? (
        <div className="st-toast-bar">
          <span className="st-toast-bar-label">
            <span className="st-toast-dot is-gold" aria-hidden="true" />
            {toPersianDigits(items.length)} تسک باز
          </span>
          <div className="st-toast-bar-btns">
            {onOpenStaffTasks ? (
              <button type="button" className="st-toast-bar-btn" onClick={onOpenStaffTasks}>
                همه
              </button>
            ) : null}
            <button
              type="button"
              className="st-toast-bar-btn"
              onClick={() => setCollapsed((c) => !c)}
            >
              {collapsed ? `+${toPersianDigits(items.length - 1)} بیشتر` : 'فشرده'}
            </button>
          </div>
        </div>
      ) : null}

      {visibleItems.map((task) => {
        const pMeta = PRIORITY_META[task.priority] ?? PRIORITY_META.Normal
        const isNoteVisible = task.requireNoteOnComplete || noteOpenId === task.id
        const isBusy = busyId === task.id
        const err = toastError[task.id]

        return (
          <div
            key={`${task.id}:${task.occurrenceDate}`}
            className={`st-toast-item is-${pMeta.tone}`}
            role="alert"
          >
            <div className="st-toast-row">
              <div className="st-toast-content">
                <div className="st-toast-title-line">
                  <span className={`st-toast-dot is-${pMeta.tone}`} aria-hidden="true" />
                  <span className="st-toast-title">{task.title}</span>
                </div>
                {task.description ? <p className="st-toast-desc">{task.description}</p> : null}
                <div className="st-toast-meta">
                  <span className="st-toast-clock">{toPersianDigits(task.raiseTime)}</span>
                  <span className="st-toast-sep">·</span>
                  <span className="st-toast-elapsed">{formatOverdueLabel(task.overdueMinutes)}</span>
                </div>
              </div>

              <div className="st-toast-controls">
                <button
                  type="button"
                  className="st-toast-check-btn"
                  disabled={isBusy}
                  onClick={() => handleComplete(task)}
                  title="ثبت انجام تسک"
                >
                  {isBusy ? '…' : 'انجام ✓'}
                </button>
                {!task.requireNoteOnComplete ? (
                  <button
                    type="button"
                    className={`st-toast-note-btn ${isNoteVisible ? 'is-open' : ''}`}
                    onClick={() => setNoteOpenId((cur) => (cur === task.id ? null : task.id))}
                    title="افزودن توضیح اختیاری"
                    aria-label="افزودن توضیح"
                  >
                    ✎
                  </button>
                ) : null}
              </div>
            </div>

            {isNoteVisible ? (
              <input
                type="text"
                className="st-toast-note"
                maxLength={500}
                placeholder={
                  task.requireNoteOnComplete
                    ? 'توضیح الزامی + Enter...'
                    : 'توضیح اختیاری + Enter...'
                }
                value={notes[task.id] || ''}
                onChange={(e) => setNotes((prev) => ({ ...prev, [task.id]: e.target.value }))}
                onKeyDown={(e) => {
                  if (e.key === 'Enter') {
                    e.preventDefault()
                    handleComplete(task)
                  }
                }}
              />
            ) : null}

            {err ? <div className="st-toast-err">{err}</div> : null}
          </div>
        )
      })}
    </aside>
  )
}
