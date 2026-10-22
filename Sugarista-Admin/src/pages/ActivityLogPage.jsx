import { useCallback, useEffect, useMemo, useState } from 'react'
import { useAdmin } from '../context/AdminContext'
import { apiRequest, getReadableError } from '../utils/adminApi'
import { ShamsiDatePicker } from '../components/ShamsiDatePicker'
import { toPersianNumber } from '../utils/adminFormatters'
import './ActivityLogPage.css'

const ACTIONS = [
  { value: '', label: 'همه' },
  { value: 'login', label: 'ورود' },
  { value: 'logout', label: 'خروج' },
  { value: 'create', label: 'ایجاد' },
  { value: 'update', label: 'ویرایش' },
  { value: 'delete', label: 'حذف' },
  { value: 'status', label: 'تغییر وضعیت' },
  { value: 'upload', label: 'فایل' },
  { value: 'export', label: 'خروجی' },
]

const ACTION_META = {
  login: { label: 'ورود', tone: 'login' },
  logout: { label: 'خروج', tone: 'logout' },
  create: { label: 'ایجاد', tone: 'create' },
  update: { label: 'ویرایش', tone: 'update' },
  delete: { label: 'حذف', tone: 'delete' },
  status: { label: 'تغییر وضعیت', tone: 'status' },
  upload: { label: 'بارگذاری', tone: 'upload' },
  export: { label: 'خروجی', tone: 'export' },
  other: { label: 'عملیات', tone: 'other' },
}

const TEHRAN_TIME = new Intl.DateTimeFormat('fa-IR', {
  timeZone: 'Asia/Tehran',
  hour: '2-digit',
  minute: '2-digit',
  second: '2-digit',
  hour12: false,
})

const TEHRAN_WEEKDAY = new Intl.DateTimeFormat('fa-IR', {
  timeZone: 'Asia/Tehran',
  weekday: 'long',
})

const TEHRAN_DAY = new Intl.DateTimeFormat('fa-IR', {
  timeZone: 'Asia/Tehran',
  day: 'numeric',
  month: 'long',
  year: 'numeric',
})

function fullDayLabel(date) {
  return `${TEHRAN_WEEKDAY.format(date)} ${TEHRAN_DAY.format(date)}`
}

const TEHRAN_ISO_DAY = new Intl.DateTimeFormat('en-CA', {
  timeZone: 'Asia/Tehran',
  year: 'numeric',
  month: '2-digit',
  day: '2-digit',
})

function tehranDayKey(value) {
  return TEHRAN_ISO_DAY.format(new Date(value))
}

function tehranTime(value) {
  return TEHRAN_TIME.format(new Date(value))
}

function dayHeading(key) {
  const today = TEHRAN_ISO_DAY.format(new Date())

  if (key === today) {
    return `امروز — ${fullDayLabel(new Date())}`
  }

  const yesterdayDate = new Date(Date.now() - 24 * 60 * 60 * 1000)
  const yesterday = TEHRAN_ISO_DAY.format(yesterdayDate)
  if (key === yesterday) {
    return `دیروز — ${fullDayLabel(yesterdayDate)}`
  }

  return fullDayLabel(new Date(`${key}T12:00:00Z`))
}

function actionMeta(action) {
  return ACTION_META[action] ?? ACTION_META.other
}

function ActionIcon({ action }) {
  const common = { viewBox: '0 0 24 24', fill: 'none', stroke: 'currentColor', strokeWidth: 1.8, strokeLinecap: 'round', strokeLinejoin: 'round' }

  switch (action) {
    case 'login':
      return <svg {...common}><path d="M10 17l5-5-5-5" /><path d="M15 12H3" /><path d="M14 4h5a2 2 0 0 1 2 2v12a2 2 0 0 1-2 2h-5" /></svg>
    case 'logout':
      return <svg {...common}><path d="M14 7l-5 5 5 5" /><path d="M9 12h12" /><path d="M10 4H5a2 2 0 0 0-2 2v12a2 2 0 0 0 2 2h5" /></svg>
    case 'create':
      return <svg {...common}><circle cx="12" cy="12" r="9" /><path d="M12 8v8M8 12h8" /></svg>
    case 'update':
      return <svg {...common}><path d="M4 20h4l10-10a2.8 2.8 0 0 0-4-4L4 16z" /><path d="M14 6l4 4" /></svg>
    case 'delete':
      return <svg {...common}><path d="M4 7h16" /><path d="M10 11v6M14 11v6" /><path d="M6 7l1 12a2 2 0 0 0 2 2h6a2 2 0 0 0 2-2l1-12" /><path d="M9 7V4h6v3" /></svg>
    case 'status':
      return <svg {...common}><path d="M4 12a8 8 0 0 1 13.5-5.8" /><path d="M20 12a8 8 0 0 1-13.5 5.8" /><path d="M18 3v4h-4" /><path d="M6 21v-4h4" /></svg>
    case 'upload':
      return <svg {...common}><path d="M12 16V4" /><path d="M8 8l4-4 4 4" /><path d="M4 16v2a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2v-2" /></svg>
    case 'export':
      return <svg {...common}><path d="M12 4v12" /><path d="M8 12l4 4 4-4" /><path d="M4 18v2h16v-2" /></svg>
    default:
      return <svg {...common}><circle cx="12" cy="12" r="3" /><path d="M12 4v2M12 18v2M4 12h2M18 12h2" /></svg>
  }
}

function initialsOf(log) {
  const name = log.actorDisplayName || log.actorUsername || '؟'
  return name.trim().slice(0, 1)
}

function emptyFilters() {
  return {
    search: '',
    sectionId: '',
    action: '',
    username: '',
    fromDate: '',
    toDate: '',
  }
}

export function ActivityLogPage() {
  const { session, setMessage, setError } = useAdmin()
  const token = session?.token

  const canPurge =
    session?.isSuperAdmin || (session?.permissions ?? []).includes('admin.activity-logs.delete')

  const [logs, setLogs] = useState([])
  const [total, setTotal] = useState(0)
  const [page, setPage] = useState(1)
  const [pageSize] = useState(30)
  const [summary, setSummary] = useState(null)
  const [filterOptions, setFilterOptions] = useState({ actors: [], sections: [], actions: [] })
  const [filters, setFilters] = useState(emptyFilters)
  const [loading, setLoading] = useState(true)
  const [expanded, setExpanded] = useState(() => new Set())
  const [purgeDays, setPurgeDays] = useState(90)
  const [purging, setPurging] = useState(false)

  const load = useCallback(async () => {
    if (!token) {
      return
    }

    setLoading(true)
    try {
      const params = new URLSearchParams({ page, pageSize })
      if (filters.search.trim()) params.set('search', filters.search.trim())
      if (filters.sectionId) params.set('sectionId', filters.sectionId)
      if (filters.action) params.set('action', filters.action)
      if (filters.username) params.set('username', filters.username)
      if (filters.fromDate) params.set('fromDate', filters.fromDate)
      if (filters.toDate) params.set('toDate', filters.toDate)

      const [listResult, summaryResult, filtersResult] = await Promise.all([
        apiRequest(`/admin/activity-logs?${params.toString()}`, { token }),
        apiRequest('/admin/activity-logs/summary', { token }),
        apiRequest('/admin/activity-logs/filters', { token }),
      ])

      setLogs(Array.isArray(listResult.items) ? listResult.items : [])
      setTotal(listResult.totalCount ?? 0)
      setSummary(summaryResult)
      setFilterOptions(filtersResult)
    } catch (requestError) {
      setError(getReadableError(requestError))
    } finally {
      setLoading(false)
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [filters, page, token])

  useEffect(() => {
    load()
  }, [load])

  const grouped = useMemo(() => {
    const groups = []

    for (const log of logs) {
      const key = tehranDayKey(log.occurredAt)
      const last = groups[groups.length - 1]

      if (last && last.key === key) {
        last.items.push(log)
      } else {
        groups.push({ key, items: [log] })
      }
    }

    return groups
  }, [logs])

  const totalPages = Math.max(1, Math.ceil(total / pageSize))

  const updateFilter = (patch) => {
    setFilters((current) => ({ ...current, ...patch }))
    setPage(1)
  }

  const resetFilters = () => {
    setFilters(emptyFilters())
    setPage(1)
  }

  const toggleExpanded = (id) => {
    setExpanded((current) => {
      const next = new Set(current)
      if (next.has(id)) {
        next.delete(id)
      } else {
        next.add(id)
      }
      return next
    })
  }

  const purge = async () => {
    if (!window.confirm(`همهٔ رویدادهای قدیمی‌تر از ${purgeDays} روز برای همیشه حذف شوند؟`)) {
      return
    }

    setPurging(true)
    try {
      const result = await apiRequest(`/admin/activity-logs/purge?days=${purgeDays}`, {
        method: 'DELETE',
        token,
      })
      setMessage(`${toPersianNumber(result.deleted)} رویداد قدیمی پاک شد.`)
      setPage(1)
      await load()
    } catch (requestError) {
      setError(getReadableError(requestError))
    } finally {
      setPurging(false)
    }
  }

  return (
    <div className="admin-activity-log-page">
      <header className="log-hero">
        <div>
          <h1 className="page-title">لاگ فعالیت‌ها</h1>
          <p className="page-subtitle">
            هر کاری که هر کاربر در پنل انجام می‌دهد، همین‌جا با نام کاربر، بخش، جزئیات و ساعت دقیق ثبت می‌شود.
          </p>
        </div>
        <button type="button" className="ghost-button log-refresh" onClick={load} disabled={loading}>
          {loading ? 'به‌روزرسانی…' : 'به‌روزرسانی'}
        </button>
      </header>

      {summary ? (
        <div className="stat-grid log-stats">
          <div className="stat-card">
            <span>کل رویدادها</span>
            <strong>{toPersianNumber(summary.totalEvents)}</strong>
          </div>
          <div className="stat-card">
            <span>رویدادهای امروز</span>
            <strong>{toPersianNumber(summary.todayEvents)}</strong>
          </div>
          <div className="stat-card">
            <span>ورودهای امروز</span>
            <strong>{toPersianNumber(summary.loginsToday)}</strong>
          </div>
          <div className="stat-card">
            <span>کاربران فعال (۷ روز)</span>
            <strong>{toPersianNumber(summary.activeUsersInWeek)}</strong>
          </div>
          <div className="stat-card">
            <span>اقدام ناموفق امروز</span>
            <strong className={summary.failedToday > 0 ? 'log-danger' : ''}>
              {toPersianNumber(summary.failedToday)}
            </strong>
          </div>
        </div>
      ) : null}

      <section className="panel log-panel">
        <div className="log-filters">
          <input
            className="log-search"
            placeholder="جست‌وجو: نام کاربر، بخش یا متن رویداد…"
            value={filters.search}
            onChange={(event) => updateFilter({ search: event.target.value })}
          />

          <select
            value={filters.username}
            onChange={(event) => updateFilter({ username: event.target.value })}
          >
            <option value="">همهٔ کاربران</option>
            {filterOptions.actors.map((actor) => (
              <option key={actor.username} value={actor.username}>
                {actor.displayName ? `${actor.displayName} (${actor.username})` : actor.username}
              </option>
            ))}
          </select>

          <select
            value={filters.sectionId}
            onChange={(event) => updateFilter({ sectionId: event.target.value })}
          >
            <option value="">همهٔ بخش‌ها</option>
            {filterOptions.sections.map((section) => (
              <option key={section.sectionId} value={section.sectionId}>
                {section.sectionTitle}
              </option>
            ))}
          </select>

          <div className="log-date-range">
            <ShamsiDatePicker
              value={filters.fromDate}
              onChange={(value) => updateFilter({ fromDate: value })}
              placeholder="از تاریخ"
            />
            <span>تا</span>
            <ShamsiDatePicker
              value={filters.toDate}
              onChange={(value) => updateFilter({ toDate: value })}
              placeholder="تا تاریخ"
            />
          </div>

          <button type="button" className="ghost-button" onClick={resetFilters}>
            پاک‌کردن فیلترها
          </button>
        </div>

        <div className="log-action-chips">
          {ACTIONS.map((action) => (
            <button
              key={action.value || 'all'}
              type="button"
              className={`log-chip${filters.action === action.value ? ' is-active' : ''}`}
              onClick={() => updateFilter({ action: action.value })}
            >
              {action.label}
            </button>
          ))}
        </div>

        {loading ? (
          <p className="feedback">در حال خواندن لاگ‌ها…</p>
        ) : !logs.length ? (
          <div className="log-empty">
            <p>هنوز رویدادی با این فیلترها ثبت نشده است.</p>
            <span>به‌محض انجام اولین اقدام در پنل، همین‌جا نمایش داده می‌شود.</span>
          </div>
        ) : (
          <div className="log-timeline">
            {grouped.map((group) => (
              <section key={group.key} className="log-day">
                <header className="log-day-head">
                  <span>{dayHeading(group.key)}</span>
                  <em>{toPersianNumber(group.items.length)} رویداد</em>
                </header>

                <ul>
                  {group.items.map((log) => {
                    const meta = actionMeta(log.action)
                    const isOpen = expanded.has(log.id)

                    return (
                      <li
                        key={log.id}
                        className={`log-item tone-${meta.tone}${log.succeeded ? '' : ' is-failed'}`}
                      >
                        <span className="log-avatar" title={log.actorUsername}>
                          {initialsOf(log)}
                        </span>

                        <span className="log-dot">
                          <ActionIcon action={log.action} />
                        </span>

                        <div className="log-body">
                          <div className="log-line">
                            <p>{log.description}</p>
                            <time dateTime={log.occurredAt} title={new Date(log.occurredAt).toISOString()}>
                              {tehranTime(log.occurredAt)}
                            </time>
                          </div>

                          <div className="log-meta">
                            <span className={`log-action-tag tone-${meta.tone}`}>{meta.label}</span>
                            <span className="log-section">{log.sectionTitle}</span>
                            <span className="log-actor">@{log.actorUsername}</span>
                            {!log.succeeded ? (
                              <span className="log-failed">ناموفق · کد {log.statusCode}</span>
                            ) : null}
                            <button
                              type="button"
                              className="log-more"
                              onClick={() => toggleExpanded(log.id)}
                            >
                              {isOpen ? 'بستن جزئیات' : 'جزئیات'}
                            </button>
                          </div>

                          {log.changedFields?.length ? (
                            <div className="log-fields">
                              {log.changedFields.map((field) => (
                                <span key={field}>{field}</span>
                              ))}
                            </div>
                          ) : null}

                          {isOpen ? (
                            <div className="log-detail">
                              <div>
                                <span>مسیر</span>
                                <code dir="ltr">
                                  {log.httpMethod} {log.path}
                                </code>
                              </div>
                              <div>
                                <span>IP</span>
                                <code dir="ltr">{log.ipAddress ?? '—'}</code>
                              </div>
                              <div>
                                <span>مرورگر</span>
                                <code dir="ltr" className="log-ua">
                                  {log.userAgent ?? '—'}
                                </code>
                              </div>
                              <div>
                                <span>شناسهٔ رویداد</span>
                                <code dir="ltr">{log.id}</code>
                              </div>
                            </div>
                          ) : null}
                        </div>
                      </li>
                    )
                  })}
                </ul>
              </section>
            ))}
          </div>
        )}

        {totalPages > 1 ? (
          <div className="admin-pagination">
            <button type="button" disabled={page <= 1} onClick={() => setPage((current) => current - 1)}>
              قبلی
            </button>
            <span>
              صفحه {toPersianNumber(page)} از {toPersianNumber(totalPages)} — {toPersianNumber(total)} رویداد
            </span>
            <button type="button" disabled={page >= totalPages} onClick={() => setPage((current) => current + 1)}>
              بعدی
            </button>
          </div>
        ) : null}

        {canPurge ? (
          <footer className="log-retention">
            <div>
              <strong>نگهداری لاگ</strong>
              <span>رویدادهای قدیمی‌تر از تعداد روز انتخابی برای همیشه حذف می‌شوند.</span>
            </div>
            <div className="log-retention-actions">
              <input
                type="number"
                min="1"
                max="3650"
                value={purgeDays}
                onChange={(event) => setPurgeDays(Number(event.target.value) || 90)}
              />
              <span>روز</span>
              <button type="button" className="ghost-button danger" onClick={purge} disabled={purging}>
                {purging ? 'در حال پاک‌سازی…' : 'پاک‌سازی'}
              </button>
            </div>
          </footer>
        ) : null}
      </section>
    </div>
  )
}
