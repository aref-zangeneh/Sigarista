import { useCallback, useEffect, useMemo, useState } from 'react'
import { ShamsiDatePicker } from '../components/ShamsiDatePicker.jsx'
import { useAdmin } from '../context/AdminContext'
import { apiRequest, getReadableError } from '../utils/adminApi.js'
import { hasPermission, PERMISSIONS } from '../utils/adminPermissions.js'
import { toEnglishDigits, toPersianDigits } from '../utils/persianDigits.js'
import {
  ACTIVITY_ACTION_LABELS,
  ALL_DAYS_MASK,
  PERSIAN_WEEKDAYS,
  PRIORITY_META,
  STAFF_TASK_CATEGORIES,
  TODAY_STATE_META,
  formatDelayBadge,
  formatOverdueLabel,
  formatScheduleLabel,
  formatShamsiDateOnly,
  formatShamsiDateTime,
  toggleWeekdayBit,
} from '../utils/staffTasksHelpers.js'
import './StaffTasksPage.css'

const TIME_PRESETS = ['08:00', '14:00', '18:00', '21:00', '22:30', '23:00']

function emptyTaskForm(defaultAssigneeId = '') {
  return {
    id: null,
    title: '',
    description: '',
    assignedToAdminUserId: defaultAssigneeId,
    category: 'بستن شیفت',
    priority: 'High',
    recurrenceType: 'Daily',
    daysOfWeekMask: ALL_DAYS_MASK,
    oneTimeDate: '',
    raiseTime: '21:00',
    requireNoteOnComplete: false,
    isActive: true,
  }
}

export function StaffTasksPage() {
  const { session, setError, setMessage } = useAdmin()
  const token = session?.token

  const canCreate = hasPermission(session, PERMISSIONS['staff-tasks']?.create)
  const canUpdate = hasPermission(session, PERMISSIONS['staff-tasks']?.update)
  const canDelete = hasPermission(session, PERMISSIONS['staff-tasks']?.delete)
  const canViewLogs = hasPermission(session, PERMISSIONS['staff-tasks']?.viewLogs)

  const [activeTab, setActiveTab] = useState('board')
  const [board, setBoard] = useState(null)
  const [loading, setLoading] = useState(true)
  const [assigneeFilter, setAssigneeFilter] = useState('')
  const [statusFilter, setStatusFilter] = useState('')
  const [search, setSearch] = useState('')

  // Form modal state
  const [modalOpen, setModalOpen] = useState(false)
  const [form, setForm] = useState(() => emptyTaskForm())
  const [saving, setSaving] = useState(false)

  // Inline completion state
  const [completingId, setCompletingId] = useState(null)
  const [completeNotes, setCompleteNotes] = useState({})
  const [noteInputOpenId, setNoteInputOpenId] = useState(null)

  // Logs tab state
  const [logsSubTab, setLogsSubTab] = useState('completions')
  const [logs, setLogs] = useState({ completions: [], activities: [] })
  const [logsLoading, setLogsLoading] = useState(false)
  const [logsDays, setLogsDays] = useState(30)
  const [logsUserFilter, setLogsUserFilter] = useState('')

  const loadBoard = useCallback(async () => {
    if (!token) return
    setLoading(true)
    try {
      const params = new URLSearchParams()
      if (assigneeFilter) params.set('assignedToAdminUserId', assigneeFilter)
      const data = await apiRequest(`/admin/staff-tasks${params.toString() ? `?${params}` : ''}`, { token })
      setBoard(data)
    } catch (err) {
      setError(getReadableError(err))
    } finally {
      setLoading(false)
    }
  }, [token, assigneeFilter, setError])

  const loadLogs = useCallback(async () => {
    if (!token || !canViewLogs) return
    setLogsLoading(true)
    try {
      const params = new URLSearchParams({ days: String(logsDays) })
      if (logsUserFilter) params.set('adminUserId', logsUserFilter)
      const data = await apiRequest(`/admin/staff-tasks/logs?${params}`, { token })
      setLogs({
        completions: Array.isArray(data?.completions) ? data.completions : [],
        activities: Array.isArray(data?.activities) ? data.activities : [],
      })
    } catch (err) {
      setError(getReadableError(err))
    } finally {
      setLogsLoading(false)
    }
  }, [token, canViewLogs, logsDays, logsUserFilter, setError])

  useEffect(() => {
    loadBoard()
  }, [loadBoard])

  useEffect(() => {
    const onChanged = () => loadBoard()
    window.addEventListener('sugarista:staff-tasks-changed', onChanged)
    return () => window.removeEventListener('sugarista:staff-tasks-changed', onChanged)
  }, [loadBoard])

  useEffect(() => {
    if (activeTab === 'logs' && canViewLogs) {
      loadLogs()
    }
  }, [activeTab, canViewLogs, loadLogs])

  const assignees = useMemo(() => board?.assignees ?? [], [board])

  const filteredItems = useMemo(() => {
    const items = board?.items ?? []
    const q = search.trim().toLowerCase()
    return items.filter((item) => {
      if (statusFilter && item.todayState !== statusFilter) return false
      if (!q) return true
      return [item.title, item.description, item.category, item.assignedToDisplayName]
        .filter(Boolean)
        .some((v) => String(v).toLowerCase().includes(q))
    })
  }, [board, statusFilter, search])

  const openCreateModal = () => {
    setForm(emptyTaskForm(assignees[0]?.id ?? ''))
    setModalOpen(true)
  }

  const openEditModal = (task) => {
    setForm({
      id: task.id,
      title: task.title || '',
      description: task.description || '',
      assignedToAdminUserId: task.assignedToAdminUserId || '',
      category: task.category || 'عملیات روزانه',
      priority: task.priority || 'Normal',
      recurrenceType: task.recurrenceType || 'Daily',
      daysOfWeekMask: task.daysOfWeekMask ?? ALL_DAYS_MASK,
      oneTimeDate: task.oneTimeDate || '',
      raiseTime: task.raiseTime || '21:00',
      requireNoteOnComplete: Boolean(task.requireNoteOnComplete),
      isActive: Boolean(task.isActive),
    })
    setModalOpen(true)
  }

  const handleSaveTask = async (event) => {
    event.preventDefault()
    if (!token || saving) return

    const cleanTime = toEnglishDigits(form.raiseTime).trim()
    if (!form.title.trim()) {
      setError('لطفاً عنوان تسک را وارد کنید.')
      return
    }
    if (!form.assignedToAdminUserId) {
      setError('لطفاً پرسنل مسئول این تسک را انتخاب کنید.')
      return
    }
    if (!/^\d{1,2}:\d{2}$/.test(cleanTime)) {
      setError('ساعت نمایش تسک باید به فرمت HH:mm باشد (مثلاً 21:00).')
      return
    }

    setSaving(true)
    setError('')
    setMessage('')
    try {
      const payload = {
        title: form.title.trim(),
        description: form.description.trim() || null,
        assignedToAdminUserId: form.assignedToAdminUserId,
        category: form.category.trim() || 'عملیات روزانه',
        priority: form.priority,
        recurrenceType: form.recurrenceType,
        raiseTime: cleanTime.padStart(5, '0'),
        daysOfWeekMask: form.recurrenceType === 'Weekly' ? Number(form.daysOfWeekMask) : ALL_DAYS_MASK,
        oneTimeDate: form.recurrenceType === 'OneTime' ? form.oneTimeDate : null,
        requireNoteOnComplete: Boolean(form.requireNoteOnComplete),
        isActive: Boolean(form.isActive),
      }

      const isEdit = Boolean(form.id)
      await apiRequest(isEdit ? `/admin/staff-tasks/${form.id}` : '/admin/staff-tasks', {
        method: isEdit ? 'PUT' : 'POST',
        token,
        body: payload,
      })

      setModalOpen(false)
      setMessage(isEdit ? 'تسک با موفقیت ویرایش شد.' : 'تسک جدید تعریف و به پرسنل تخصیص داده شد.')
      await loadBoard()
      window.dispatchEvent(new CustomEvent('sugarista:staff-tasks-changed'))
    } catch (err) {
      setError(getReadableError(err))
    } finally {
      setSaving(false)
    }
  }

  const handleComplete = async (task) => {
    const note = (completeNotes[task.id] || '').trim()
    if (task.requireNoteOnComplete && !note) {
      setNoteInputOpenId(task.id)
      setError('برای این تسک نوشتن توضیح کوتاه هنگام انجام الزامی است.')
      return
    }

    setCompletingId(task.id)
    setError('')
    setMessage('')
    try {
      await apiRequest(`/admin/staff-tasks/${task.id}/complete`, {
        method: 'POST',
        token,
        body: {
          note: note || null,
          occurrenceDate: task.occurrenceDate,
        },
      })
      setCompleteNotes((prev) => ({ ...prev, [task.id]: '' }))
      setNoteInputOpenId(null)
      setMessage(`تسک «${task.title}» با موفقیت انجام‌شده ثبت شد.`)
      await loadBoard()
      window.dispatchEvent(new CustomEvent('sugarista:staff-tasks-changed'))
    } catch (err) {
      setError(getReadableError(err))
    } finally {
      setCompletingId(null)
    }
  }

  const handleReopen = async (task) => {
    if (!canUpdate) return
    setError('')
    setMessage('')
    try {
      await apiRequest(`/admin/staff-tasks/${task.id}/reopen?occurrenceDate=${encodeURIComponent(task.occurrenceDate)}`, {
        method: 'POST',
        token,
      })
      setMessage(`تسک «${task.title}» برای امروز بازگشایی شد.`)
      await loadBoard()
      window.dispatchEvent(new CustomEvent('sugarista:staff-tasks-changed'))
    } catch (err) {
      setError(getReadableError(err))
    }
  }

  const handleToggleActive = async (task) => {
    if (!canUpdate) return
    setError('')
    try {
      await apiRequest(`/admin/staff-tasks/${task.id}/toggle`, { method: 'PATCH', token })
      await loadBoard()
      window.dispatchEvent(new CustomEvent('sugarista:staff-tasks-changed'))
    } catch (err) {
      setError(getReadableError(err))
    }
  }

  const handleDelete = async (task) => {
    if (!canDelete) return
    if (!window.confirm(`آیا از حذف تسک «${task.title}» مطمئن هستید؟ (لاگ‌های قبلی حفظ می‌شوند)`)) return
    setError('')
    setMessage('')
    try {
      await apiRequest(`/admin/staff-tasks/${task.id}`, { method: 'DELETE', token })
      setMessage('تسک حذف شد.')
      await loadBoard()
      window.dispatchEvent(new CustomEvent('sugarista:staff-tasks-changed'))
    } catch (err) {
      setError(getReadableError(err))
    }
  }

  const summary = board?.summary

  return (
    <div className="st-page" dir="rtl">
      <header className="st-header">
        <div>
          <h2 className="st-title">تسک‌های پرسنل و چک‌لیست شیفت</h2>
          <p className="st-subtitle">
            {board?.canViewAll
              ? 'تعریف و تخصیص تسک‌های زمان‌بندی‌شده به هر فرد؛ پس از رسیدن ساعت مقرر، اعلان ثابت تا زمان انجام روی پنل همان فرد باقی می‌ماند.'
              : 'لیست تسک‌های محول‌شده به شما. تسک‌هایی که زمانشان فرا رسیده تا زمان ثبت «انجام شد» روی پنل باقی می‌مانند.'}
          </p>
        </div>
        <div className="st-header-actions">
          {board?.tehranTime ? (
            <span className="st-clock-pill" title="ساعت فعلی سرور (تهران)">
              🕒 {toPersianDigits(board.tehranTime)} — {formatShamsiDateOnly(board.tehranDate)}
            </span>
          ) : null}
          {canCreate ? (
            <button type="button" className="st-primary-btn" onClick={openCreateModal}>
              + تعریف تسک جدید
            </button>
          ) : null}
        </div>
      </header>

      {summary ? (
        <section className="st-kpi-grid" aria-label="خلاصه وضعیت امروز">
          <button
            type="button"
            className={`st-kpi-card is-due ${statusFilter === 'Due' ? 'is-selected' : ''}`}
            onClick={() => setStatusFilter((prev) => (prev === 'Due' ? '' : 'Due'))}
          >
            <span className="st-kpi-label">در انتظار انجام (الآن)</span>
            <strong className="st-kpi-value">{toPersianDigits(summary.dueNow)}</strong>
          </button>
          <button
            type="button"
            className={`st-kpi-card is-done ${statusFilter === 'Done' ? 'is-selected' : ''}`}
            onClick={() => setStatusFilter((prev) => (prev === 'Done' ? '' : 'Done'))}
          >
            <span className="st-kpi-label">انجام‌شده امروز</span>
            <strong className="st-kpi-value">{toPersianDigits(summary.doneToday)}</strong>
          </button>
          <button
            type="button"
            className={`st-kpi-card is-upcoming ${statusFilter === 'Upcoming' ? 'is-selected' : ''}`}
            onClick={() => setStatusFilter((prev) => (prev === 'Upcoming' ? '' : 'Upcoming'))}
          >
            <span className="st-kpi-label">در پیش‌رو امروز</span>
            <strong className="st-kpi-value">{toPersianDigits(summary.upcomingToday)}</strong>
          </button>
          <button
            type="button"
            className={`st-kpi-card ${statusFilter === '' ? 'is-selected' : ''}`}
            onClick={() => setStatusFilter('')}
          >
            <span className="st-kpi-label">کل تسک‌های تعریف‌شده</span>
            <strong className="st-kpi-value">{toPersianDigits(summary.totalTasks)}</strong>
          </button>
        </section>
      ) : null}

      <div className="st-tabs-row">
        <div className="st-tabs" role="tablist">
          <button
            type="button"
            role="tab"
            aria-selected={activeTab === 'board'}
            className={`st-tab-btn ${activeTab === 'board' ? 'is-active' : ''}`}
            onClick={() => setActiveTab('board')}
          >
            تابلوی تسک‌ها
          </button>
          {canViewLogs ? (
            <button
              type="button"
              role="tab"
              aria-selected={activeTab === 'logs'}
              className={`st-tab-btn ${activeTab === 'logs' ? 'is-active' : ''}`}
              onClick={() => setActiveTab('logs')}
            >
              لاگ انجام و تاریخچه
            </button>
          ) : null}
        </div>

        {activeTab === 'board' ? (
          <div className="st-filters">
            <input
              type="search"
              className="st-search-input"
              placeholder="جستجو در عنوان، دسته‌بندی یا نام پرسنل..."
              value={search}
              onChange={(e) => setSearch(e.target.value)}
            />
            {board?.canViewAll && assignees.length > 0 ? (
              <select
                className="st-select"
                aria-label="فیلتر بر اساس پرسنل"
                value={assigneeFilter}
                onChange={(e) => setAssigneeFilter(e.target.value)}
              >
                <option value="">همه پرسنل</option>
                {assignees.map((u) => (
                  <option key={u.id} value={u.id}>
                    {u.displayName} ({u.username})
                  </option>
                ))}
              </select>
            ) : null}
          </div>
        ) : null}
      </div>

      {activeTab === 'board' ? (
        <section className="st-list-wrap">
          {loading && !board ? <p className="st-empty">در حال بارگذاری تسک‌ها…</p> : null}
          {!loading && filteredItems.length === 0 ? (
            <div className="st-empty-card">
              <strong>هیچ تسکی با این فیلتر یافت نشد.</strong>
              <p>
                {canCreate
                  ? 'با دکمه «تعریف تسک جدید» می‌توانید چک‌لیست روزانه یا تسک مشخصی را برای هر عضو تیم ثبت کنید.'
                  : 'در حال حاضر تسک فعالی برای شما ثبت نشده است.'}
              </p>
            </div>
          ) : null}

          <div className="st-cards-grid">
            {filteredItems.map((task) => {
              const pMeta = PRIORITY_META[task.priority] ?? PRIORITY_META.Normal
              const sMeta = TODAY_STATE_META[task.todayState] ?? TODAY_STATE_META.Upcoming
              const canCompleteThis =
                task.isActive &&
                task.todayState !== 'Done' &&
                task.todayState !== 'NotScheduledToday' &&
                (task.isAssignedToCurrentUser || canUpdate)
              const showNoteBox = noteInputOpenId === task.id || (canCompleteThis && task.requireNoteOnComplete && task.todayState === 'Due')
              const delayInfo = task.delayMinutes != null ? formatDelayBadge(task.delayMinutes) : null

              return (
                <article
                  key={task.id}
                  className={`st-task-card is-state-${sMeta.tone} is-prio-${pMeta.tone}`}
                >
                  <div className="st-task-head">
                    <div className="st-task-badges">
                      <span className={`st-pill is-state-${sMeta.tone}`}>{sMeta.label}</span>
                      <span className={`st-pill is-prio-${pMeta.tone}`}>{pMeta.label}</span>
                      <span className="st-pill is-cat">{task.category}</span>
                    </div>
                    <span className="st-task-clock">⏰ {toPersianDigits(task.raiseTime)}</span>
                  </div>

                  <h3 className="st-task-title">{task.title}</h3>
                  {task.description ? <p className="st-task-desc">{task.description}</p> : null}

                  <div className="st-task-meta">
                    <span>👤 مسئول: <strong>{task.assignedToDisplayName}</strong></span>
                    <span>📅 {formatScheduleLabel(task)}</span>
                    {task.requireNoteOnComplete ? <span className="st-note-req-tag">توضیح الزامی</span> : null}
                  </div>

                  {task.todayState === 'Due' ? (
                    <div className="st-task-alert-strip">
                      ⚡ {formatOverdueLabel(task.overdueMinutes)} — اعلان ثابت روی پنل {task.assignedToDisplayName} فعال است
                    </div>
                  ) : null}

                  {task.todayState === 'Done' ? (
                    <div className="st-task-done-strip">
                      <div>
                        ✓ انجام توسط <strong>{task.completedByDisplayName}</strong> در{' '}
                        {formatShamsiDateTime(task.completedAtUtc)}
                      </div>
                      {delayInfo ? (
                        <span className={`st-delay-pill is-${delayInfo.tone}`}>{delayInfo.label}</span>
                      ) : null}
                      {task.completionNote ? (
                        <div className="st-done-note">توضیح: {task.completionNote}</div>
                      ) : null}
                    </div>
                  ) : null}

                  {showNoteBox ? (
                    <input
                      type="text"
                      className="st-inline-note"
                      maxLength={500}
                      placeholder={
                        task.requireNoteOnComplete
                          ? 'توضیح انجام کار (الزامی)...'
                          : 'توضیح اختیاری...'
                      }
                      value={completeNotes[task.id] || ''}
                      onChange={(e) =>
                        setCompleteNotes((prev) => ({ ...prev, [task.id]: e.target.value }))
                      }
                    />
                  ) : null}

                  <div className="st-task-footer">
                    <div className="st-task-footer-main">
                      {canCompleteThis ? (
                        <>
                          <button
                            type="button"
                            className="st-done-btn"
                            disabled={completingId === task.id}
                            onClick={() => handleComplete(task)}
                          >
                            {completingId === task.id ? 'در حال ثبت…' : 'انجام شد ✓'}
                          </button>
                          {!showNoteBox ? (
                            <button
                              type="button"
                              className="st-ghost-btn"
                              onClick={() => setNoteInputOpenId(task.id)}
                            >
                              + توضیح
                            </button>
                          ) : null}
                        </>
                      ) : null}

                      {task.todayState === 'Done' && canUpdate ? (
                        <button
                          type="button"
                          className="st-ghost-btn"
                          onClick={() => handleReopen(task)}
                        >
                          بازگشایی مجدد
                        </button>
                      ) : null}
                    </div>

                    {canUpdate || canDelete ? (
                      <div className="st-task-footer-admin">
                        {canUpdate ? (
                          <>
                            <button
                              type="button"
                              className="st-ghost-btn"
                              onClick={() => handleToggleActive(task)}
                            >
                              {task.isActive ? 'توقف' : 'فعال‌سازی'}
                            </button>
                            <button
                              type="button"
                              className="st-ghost-btn"
                              onClick={() => openEditModal(task)}
                            >
                              ویرایش
                            </button>
                          </>
                        ) : null}
                        {canDelete ? (
                          <button
                            type="button"
                            className="st-danger-btn"
                            onClick={() => handleDelete(task)}
                          >
                            حذف
                          </button>
                        ) : null}
                      </div>
                    ) : null}
                  </div>
                </article>
              )
            })}
          </div>
        </section>
      ) : null}

      {activeTab === 'logs' && canViewLogs ? (
        <section className="st-logs-panel">
          <div className="st-logs-toolbar">
            <div className="st-subtabs">
              <button
                type="button"
                className={`st-subtab-btn ${logsSubTab === 'completions' ? 'is-active' : ''}`}
                onClick={() => setLogsSubTab('completions')}
              >
                لاگ انجام تسک‌ها ({toPersianDigits(logs.completions.length)})
              </button>
              <button
                type="button"
                className={`st-subtab-btn ${logsSubTab === 'activities' ? 'is-active' : ''}`}
                onClick={() => setLogsSubTab('activities')}
              >
                لاگ تغییرات مدیریتی ({toPersianDigits(logs.activities.length)})
              </button>
            </div>
            <div className="st-filters">
              <select
                className="st-select"
                value={logsDays}
                onChange={(e) => setLogsDays(Number(e.target.value))}
              >
                <option value={7}>۷ روز اخیر</option>
                <option value={30}>۳۰ روز اخیر</option>
                <option value={90}>۹۰ روز اخیر</option>
              </select>
              {assignees.length > 0 ? (
                <select
                  className="st-select"
                  value={logsUserFilter}
                  onChange={(e) => setLogsUserFilter(e.target.value)}
                >
                  <option value="">همه پرسنل</option>
                  {assignees.map((u) => (
                    <option key={u.id} value={u.id}>{u.displayName}</option>
                  ))}
                </select>
              ) : null}
            </div>
          </div>

          {logsLoading ? <p className="st-empty">در حال بارگذاری لاگ…</p> : null}

          {!logsLoading && logsSubTab === 'completions' ? (
            <div className="st-table-wrap">
              <table className="st-table">
                <thead>
                  <tr>
                    <th>عنوان تسک</th>
                    <th>مسئول / انجام‌دهنده</th>
                    <th>زمان مقرر</th>
                    <th>زمان انجام واقعی</th>
                    <th>وضعیت تاخیر</th>
                    <th>توضیح</th>
                  </tr>
                </thead>
                <tbody>
                  {logs.completions.map((row) => {
                    const delay = formatDelayBadge(row.delayMinutes)
                    return (
                      <tr key={row.id}>
                        <td>
                          <strong>{row.taskTitle}</strong>
                          <div className="st-table-sub">{row.category}</div>
                        </td>
                        <td>
                          {row.completedByDisplayName}
                          {row.assignedToDisplayName !== row.completedByDisplayName ? (
                            <div className="st-table-sub">مسئول: {row.assignedToDisplayName}</div>
                          ) : null}
                        </td>
                        <td>{formatShamsiDateTime(row.scheduledAtUtc)}</td>
                        <td>{formatShamsiDateTime(row.completedAtUtc)}</td>
                        <td>
                          <span className={`st-delay-pill is-${delay.tone}`}>{delay.label}</span>
                        </td>
                        <td>{row.note || '—'}</td>
                      </tr>
                    )
                  })}
                  {logs.completions.length === 0 ? (
                    <tr>
                      <td colSpan={6} className="st-empty">هنوز سابقه انجام تسکی در این بازه ثبت نشده است.</td>
                    </tr>
                  ) : null}
                </tbody>
              </table>
            </div>
          ) : null}

          {!logsLoading && logsSubTab === 'activities' ? (
            <div className="st-table-wrap">
              <table className="st-table">
                <thead>
                  <tr>
                    <th>عملیات</th>
                    <th>عنوان تسک</th>
                    <th>مسئول تسک</th>
                    <th>انجام‌دهنده تغییر</th>
                    <th>جزئیات</th>
                    <th>زمان</th>
                  </tr>
                </thead>
                <tbody>
                  {logs.activities.map((row) => (
                    <tr key={row.id}>
                      <td>
                        <span className="st-pill is-cat">
                          {ACTIVITY_ACTION_LABELS[row.action] ?? row.action}
                        </span>
                      </td>
                      <td><strong>{row.taskTitleSnapshot}</strong></td>
                      <td>{row.assignedToDisplayName}</td>
                      <td>{row.actorDisplayName}</td>
                      <td>{row.details || '—'}</td>
                      <td>{formatShamsiDateTime(row.occurredAtUtc)}</td>
                    </tr>
                  ))}
                  {logs.activities.length === 0 ? (
                    <tr>
                      <td colSpan={6} className="st-empty">لاگی در این بازه ثبت نشده است.</td>
                    </tr>
                  ) : null}
                </tbody>
              </table>
            </div>
          ) : null}
        </section>
      ) : null}

      {/* ── Create / Edit Task Modal ── */}
      {modalOpen ? (
        <div className="st-modal-backdrop" role="dialog" aria-modal="true" aria-label="فرم تسک پرسنل">
          <form className="st-modal" onSubmit={handleSaveTask}>
            <div className="st-modal-head">
              <h3>{form.id ? 'ویرایش تسک پرسنل' : 'تعریف و تخصیص تسک جدید'}</h3>
              <button
                type="button"
                className="st-modal-close"
                onClick={() => setModalOpen(false)}
                aria-label="بستن"
              >
                ×
              </button>
            </div>

            <div className="st-modal-body">
              <label className="st-field">
                <span>عنوان تسک *</span>
                <input
                  type="text"
                  required
                  maxLength={160}
                  placeholder="مثلاً: خاموش کردن دستگاه اسپرسوساز و آسیاب"
                  value={form.title}
                  onChange={(e) => setForm((p) => ({ ...p, title: e.target.value }))}
                />
              </label>

              <div className="st-field-row">
                <label className="st-field">
                  <span>پرسنل مسئول *</span>
                  <select
                    required
                    value={form.assignedToAdminUserId}
                    onChange={(e) => setForm((p) => ({ ...p, assignedToAdminUserId: e.target.value }))}
                  >
                    <option value="">انتخاب پرسنل...</option>
                    {assignees.map((u) => (
                      <option key={u.id} value={u.id}>
                        {u.displayName} ({u.username})
                      </option>
                    ))}
                  </select>
                </label>

                <label className="st-field">
                  <span>اولویت</span>
                  <select
                    value={form.priority}
                    onChange={(e) => setForm((p) => ({ ...p, priority: e.target.value }))}
                  >
                    <option value="Normal">عادی</option>
                    <option value="High">مهم</option>
                    <option value="Critical">حیاتی (هشدار قرمز)</option>
                  </select>
                </label>
              </div>

              <div className="st-field-row">
                <label className="st-field">
                  <span>دسته‌بندی</span>
                  <select
                    value={form.category}
                    onChange={(e) => setForm((p) => ({ ...p, category: e.target.value }))}
                  >
                    {STAFF_TASK_CATEGORIES.map((c) => (
                      <option key={c} value={c}>{c}</option>
                    ))}
                  </select>
                </label>

                <label className="st-field">
                  <span>ساعت شروع اعلان (به وقت تهران) *</span>
                  <input
                    type="time"
                    required
                    value={form.raiseTime}
                    onChange={(e) => setForm((p) => ({ ...p, raiseTime: e.target.value }))}
                  />
                  <div className="st-presets">
                    {TIME_PRESETS.map((t) => (
                      <button
                        key={t}
                        type="button"
                        className={`st-preset-chip ${form.raiseTime === t ? 'is-active' : ''}`}
                        onClick={() => setForm((p) => ({ ...p, raiseTime: t }))}
                      >
                        {toPersianDigits(t)}
                      </button>
                    ))}
                  </div>
                </label>
              </div>

              <div className="st-field">
                <span>تکرار تسک</span>
                <div className="st-rec-options">
                  {[
                    { value: 'Daily', label: 'هر روز (روتین ثابت کافه)' },
                    { value: 'Weekly', label: 'روزهای خاص هفته' },
                    { value: 'OneTime', label: 'یک‌باره در تاریخ مشخص' },
                  ].map((opt) => (
                    <button
                      key={opt.value}
                      type="button"
                      className={`st-rec-btn ${form.recurrenceType === opt.value ? 'is-active' : ''}`}
                      onClick={() => setForm((p) => ({ ...p, recurrenceType: opt.value }))}
                    >
                      {opt.label}
                    </button>
                  ))}
                </div>
              </div>

              {form.recurrenceType === 'Weekly' ? (
                <div className="st-field">
                  <span>انتخاب روزهای هفته</span>
                  <div className="st-weekdays">
                    {PERSIAN_WEEKDAYS.map((day) => {
                      const active = (Number(form.daysOfWeekMask) & day.bit) !== 0
                      return (
                        <button
                          key={day.bit}
                          type="button"
                          className={`st-day-chip ${active ? 'is-active' : ''}`}
                          onClick={() =>
                            setForm((p) => ({
                              ...p,
                              daysOfWeekMask: toggleWeekdayBit(p.daysOfWeekMask, day.bit),
                            }))
                          }
                        >
                          {day.label}
                        </button>
                      )
                    })}
                  </div>
                </div>
              ) : null}

              {form.recurrenceType === 'OneTime' ? (
                <div className="st-field">
                  <span>تاریخ اجرا (شمسی) *</span>
                  <ShamsiDatePicker
                    value={form.oneTimeDate}
                    onChange={(iso) => setForm((p) => ({ ...p, oneTimeDate: iso }))}
                    placeholder="انتخاب تاریخ اجرا..."
                  />
                </div>
              ) : null}

              <label className="st-field">
                <span>توضیحات تکمیلی (اختیاری)</span>
                <textarea
                  rows={2}
                  maxLength={800}
                  placeholder="نکات اجرایی یا چک‌لیست جزئیات برای پرسنل..."
                  value={form.description}
                  onChange={(e) => setForm((p) => ({ ...p, description: e.target.value }))}
                />
              </label>

              <div className="st-checkbox-row">
                <label className="st-checkbox">
                  <input
                    type="checkbox"
                    checked={form.requireNoteOnComplete}
                    onChange={(e) => setForm((p) => ({ ...p, requireNoteOnComplete: e.target.checked }))}
                  />
                  <span>ثبت توضیح کوتاه هنگام زدن «انجام شد» الزامی باشد</span>
                </label>

                <label className="st-checkbox">
                  <input
                    type="checkbox"
                    checked={form.isActive}
                    onChange={(e) => setForm((p) => ({ ...p, isActive: e.target.checked }))}
                  />
                  <span>تسک فعال باشد</span>
                </label>
              </div>
            </div>

            <div className="st-modal-foot">
              <button type="button" className="st-ghost-btn" onClick={() => setModalOpen(false)}>
                انصراف
              </button>
              <button type="submit" className="st-primary-btn" disabled={saving}>
                {saving ? 'در حال ذخیره…' : form.id ? 'ذخیره تغییرات' : 'ثبت و تخصیص تسک'}
              </button>
            </div>
          </form>
        </div>
      ) : null}
    </div>
  )
}
