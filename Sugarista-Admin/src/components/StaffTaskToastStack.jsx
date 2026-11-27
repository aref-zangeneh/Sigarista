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
    const notes = [587.33, 880] // D5 -> A5 crisp pleasant chime
    notes.forEach((freq, i) => {
      const osc = ctx.createOscillator()
      const gain = ctx.createGain()
      osc.type = 'sine'
      osc.frequency.setValueAtTime(freq, now + i * 0.14)
      gain.gain.setValueAtTime(0.0001, now + i * 0.14)
      gain.gain.exponentialRampToValueAtTime(0.08, now + i * 0.14 + 0.02)
      gain.gain.exponentialRampToValueAtTime(0.0001, now + i * 0.14 + 0.28)
      osc.connect(gain)
      gain.connect(ctx.destination)
      osc.start(now + i * 0.14)
      osc.stop(now + i * 0.14 + 0.3)
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
  const [collapsed, setCollapsed] = useState(false)
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
          setCollapsed(false)
          playGentleTaskChime()
        }
      } else {
        initializedRef.current = true
      }
      knownIdsRef.current = nextKeys
    } catch {
      // Silent background poll — never interrupt user workflow on transient network hiccups
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
        [task.id]: 'نوشتن توضیح کوتاه برای ثبت انجام این تسک الزامی است.',
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
      setItems((prev) => prev.filter((item) => !(item.id === task.id && item.occurrenceDate === task.occurrenceDate)))
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

  const visibleItems = collapsed ? items.slice(0, 1) : items

  return (
    <aside
      className="st-toast-stack"
      role="region"
      aria-label="تسک‌های الزامی در انتظار انجام"
      aria-live="assertive"
    >
      {items.length > 1 ? (
        <div className="st-toast-header-bar">
          <span className="st-toast-header-title">
            <span className="st-toast-pulse-dot" aria-hidden="true" />
            {toPersianDigits(items.length)} تسک در انتظار انجام
          </span>
          <div className="st-toast-header-actions">
            {onOpenStaffTasks ? (
              <button type="button" className="st-toast-link-btn" onClick={onOpenStaffTasks}>
                صفحه تسک‌ها
              </button>
            ) : null}
            <button
              type="button"
              className="st-toast-collapse-btn"
              onClick={() => setCollapsed((c) => !c)}
            >
              {collapsed ? `نمایش همه (${toPersianDigits(items.length)})` : 'فشرده'}
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
            className={`st-toast-card is-${pMeta.tone}`}
            role="alert"
          >
            <div className="st-toast-top">
              <div className="st-toast-badges">
                <span className={`st-toast-priority is-${pMeta.tone}`}>{pMeta.label}</span>
                <span className="st-toast-cat">{task.category}</span>
              </div>
              <span className="st-toast-time" title="ساعت مقرر">
                ⏰ {toPersianDigits(task.raiseTime)}
              </span>
            </div>

            <div className="st-toast-title">{task.title}</div>
            {task.description ? <p className="st-toast-desc">{task.description}</p> : null}

            <div className="st-toast-overdue">
              <span className="st-toast-lock-hint">تا انجام نشود بسته نمی‌شود</span>
              <span>•</span>
              <span>{formatOverdueLabel(task.overdueMinutes)}</span>
            </div>

            {isNoteVisible ? (
              <input
                type="text"
                className="st-toast-note-input"
                maxLength={500}
                placeholder={
                  task.requireNoteOnComplete
                    ? 'توضیح انجام تسک (الزامی)...'
                    : 'توضیح اختیاری هنگام انجام...'
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

            {err ? <div className="st-toast-error">{err}</div> : null}

            <div className="st-toast-actions">
              <button
                type="button"
                className="st-toast-done-btn"
                disabled={isBusy}
                onClick={() => handleComplete(task)}
              >
                {isBusy ? 'در حال ثبت…' : 'انجام شد ✓'}
              </button>
              {!isNoteVisible ? (
                <button
                  type="button"
                  className="st-toast-note-toggle"
                  onClick={() => setNoteOpenId(task.id)}
                >
                  + توضیح
                </button>
              ) : null}
            </div>
          </div>
        )
      })}
    </aside>
  )
}
