import { useCallback, useEffect, useState } from 'react'
import { useAdmin } from '../context/AdminContext'
import { ImageUploader } from '../components/ImageUploader'
import { formatMoney } from '../components/DataTable'
import { hasPermission, PERMISSIONS } from '../utils/adminPermissions'
import { normalizeImageUrl } from '../utils/imageUrl.js'
import './MenuSectionMinimal.css'
import './IncidentsPage.css'

const INCIDENT_CATEGORIES = [
  { key: 'PowerOutage', label: 'قطع برق', icon: '⚡' },
  { key: 'WaterOrGasOutage', label: 'قطع آب یا گاز', icon: '💧' },
  { key: 'InternetOrPos', label: 'اختلال اینترنت / کارت‌خوان', icon: '📶' },
  { key: 'EquipmentFailure', label: 'خرابی دستگاه و تجهیزات', icon: '⚙️' },
  { key: 'SafetyOrHealth', label: 'ایمنی و بهداشت', icon: '🛡️' },
  { key: 'CustomerOrService', label: 'رخداد مشتری / سرویس‌دهی', icon: '👥' },
  { key: 'SupplyOrInventory', label: 'تأمین / ضایعات کالا', icon: '📦' },
  { key: 'Security', label: 'امنیت و حراست', icon: '🔒' },
  { key: 'Other', label: 'سایر حوادث', icon: '📝' },
]

const INCIDENT_SEVERITIES = [
  { key: 'Low', label: 'جزئی' },
  { key: 'Medium', label: 'متوسط' },
  { key: 'High', label: 'مهم' },
  { key: 'Critical', label: 'بحرانی' },
]

const INCIDENT_STATUSES = [
  { key: 'Open', label: 'باز / در جریان' },
  { key: 'Investigating', label: 'در حال پیگیری' },
  { key: 'Resolved', label: 'برطرف شد' },
  { key: 'Closed', label: 'بسته شد' },
]

const QUICK_TEMPLATES = [
  { label: 'قطع برق', icon: '⚡', category: 'PowerOutage', severity: 'High', title: 'قطع برق کافه' },
  { label: 'قطع آب / گاز', icon: '💧', category: 'WaterOrGasOutage', severity: 'High', title: 'قطع آب یا گاز' },
  { label: 'اختلال اینترنت / کارت‌خوان', icon: '📶', category: 'InternetOrPos', severity: 'Medium', title: 'اختلال اینترنت یا دستگاه کارت‌خوان' },
  { label: 'خرابی دستگاه', icon: '⚙️', category: 'EquipmentFailure', severity: 'High', title: 'خرابی دستگاه در بخش بار / آشپزخانه' },
  { label: 'ایمنی / بهداشت', icon: '🛡️', category: 'SafetyOrHealth', severity: 'Medium', title: 'رخداد ایمنی یا بهداشتی' },
]

function toLocalDateTimeInput(dateValue) {
  const d = dateValue ? new Date(dateValue) : new Date()
  if (Number.isNaN(d.getTime())) return ''
  const pad = (n) => String(n).padStart(2, '0')
  return `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}T${pad(d.getHours())}:${pad(d.getMinutes())}`
}

function formatMinutesReadable(minutes, toPersianNumber) {
  const m = Number(minutes) || 0
  if (m <= 0) return '۰ دقیقه'
  const hours = Math.floor(m / 60)
  const rem = m % 60
  if (hours === 0) return `${toPersianNumber(rem)} دقیقه`
  if (rem === 0) return `${toPersianNumber(hours)} ساعت`
  return `${toPersianNumber(hours)} ساعت و ${toPersianNumber(rem)} دقیقه`
}

function emptyIncidentForm(overrides = {}) {
  return {
    id: '',
    title: '',
    category: 'PowerOutage',
    severity: 'Medium',
    status: 'Open',
    occurredAt: toLocalDateTimeInput(new Date()),
    resolvedAt: '',
    location: '',
    description: '',
    impactSummary: '',
    estimatedLossAmount: '',
    resolutionNotes: '',
    reportedByName: '',
    resolvedByName: '',
    photoUrl: '',
    ...overrides,
  }
}

export function IncidentsPage() {
  const {
    session,
    apiRequest,
    getReadableError,
    setMessage,
    setError,
    toPersianNumber,
    formatDateTime,
  } = useAdmin()

  const canCreate = hasPermission(session, PERMISSIONS.incidents?.create)
  const canUpdate = hasPermission(session, PERMISSIONS.incidents?.update)
  const canDelete = hasPermission(session, PERMISSIONS.incidents?.delete)

  const [loading, setLoading] = useState(true)
  const [saving, setSaving] = useState(false)
  const [board, setBoard] = useState({
    totalCount: 0,
    openCount: 0,
    criticalOpenCount: 0,
    last30DaysCount: 0,
    totalDowntimeMinutes: 0,
    totalEstimatedLoss: 0,
    items: [],
  })

  const [search, setSearch] = useState('')
  const [categoryFilter, setCategoryFilter] = useState('')
  const [severityFilter, setSeverityFilter] = useState('')
  const [statusFilter, setStatusFilter] = useState('')

  const [modalOpen, setModalOpen] = useState(false)
  const [form, setForm] = useState(() => emptyIncidentForm())

  const [resolveTarget, setResolveTarget] = useState(null)
  const [resolveAt, setResolveAt] = useState('')
  const [resolveNotes, setResolveNotes] = useState('')

  const loadIncidents = useCallback(async () => {
    if (!session?.token) return
    try {
      const params = new URLSearchParams()
      if (statusFilter) params.set('status', statusFilter)
      if (categoryFilter) params.set('category', categoryFilter)
      if (severityFilter) params.set('severity', severityFilter)
      if (search.trim()) params.set('search', search.trim())
      const qs = params.toString()

      const data = await apiRequest(`/admin/incidents${qs ? `?${qs}` : ''}`, {
        token: session.token,
      })
      setBoard({
        totalCount: data?.totalCount ?? 0,
        openCount: data?.openCount ?? 0,
        criticalOpenCount: data?.criticalOpenCount ?? 0,
        last30DaysCount: data?.last30DaysCount ?? 0,
        totalDowntimeMinutes: data?.totalDowntimeMinutes ?? 0,
        totalEstimatedLoss: data?.totalEstimatedLoss ?? 0,
        items: Array.isArray(data?.items) ? data.items : [],
      })
    } catch (err) {
      setError(getReadableError(err))
    } finally {
      setLoading(false)
    }
  }, [session?.token, statusFilter, categoryFilter, severityFilter, search, apiRequest, getReadableError, setError])

  useEffect(() => {
    loadIncidents()
  }, [loadIncidents])

  const openCreateModal = (template = null) => {
    setForm(
      emptyIncidentForm(
        template
          ? {
              title: template.title,
              category: template.category,
              severity: template.severity,
            }
          : {},
      ),
    )
    setModalOpen(true)
  }

  const openEditModal = (item) => {
    setForm({
      id: item.id,
      title: item.title ?? '',
      category: item.category ?? 'Other',
      severity: item.severity ?? 'Medium',
      status: item.status ?? 'Open',
      occurredAt: toLocalDateTimeInput(item.occurredAt),
      resolvedAt: item.resolvedAt ? toLocalDateTimeInput(item.resolvedAt) : '',
      location: item.location ?? '',
      description: item.description ?? '',
      impactSummary: item.impactSummary ?? '',
      estimatedLossAmount: item.estimatedLossAmount != null ? String(item.estimatedLossAmount) : '',
      resolutionNotes: item.resolutionNotes ?? '',
      reportedByName: item.reportedByName ?? '',
      resolvedByName: item.resolvedByName ?? '',
      photoUrl: item.photoUrl ?? '',
    })
    setModalOpen(true)
  }

  const handleSubmit = async (event) => {
    event.preventDefault()
    if (!form.title.trim()) {
      setError('عنوان حادثه الزامی است.')
      return
    }

    setSaving(true)
    setError('')
    setMessage('')
    try {
      const payload = {
        title: form.title.trim(),
        category: form.category,
        severity: form.severity,
        status: form.status,
        occurredAt: form.occurredAt ? new Date(form.occurredAt).toISOString() : new Date().toISOString(),
        resolvedAt: form.resolvedAt ? new Date(form.resolvedAt).toISOString() : null,
        location: form.location.trim() || null,
        description: form.description.trim() || null,
        impactSummary: form.impactSummary.trim() || null,
        estimatedLossAmount:
          form.estimatedLossAmount !== '' && Number(form.estimatedLossAmount) >= 0
            ? Number(form.estimatedLossAmount)
            : null,
        resolutionNotes: form.resolutionNotes.trim() || null,
        reportedByName: form.reportedByName.trim() || null,
        resolvedByName: form.resolvedByName.trim() || null,
        photoUrl: form.photoUrl.trim() || null,
      }

      if (form.id) {
        await apiRequest(`/admin/incidents/${form.id}`, {
          method: 'PUT',
          token: session.token,
          body: payload,
        })
        setMessage('گزارش حادثه با موفقیت ویرایش شد.')
      } else {
        await apiRequest('/admin/incidents', {
          method: 'POST',
          token: session.token,
          body: payload,
        })
        setMessage('حادثه با موفقیت ثبت شد.')
      }

      setModalOpen(false)
      await loadIncidents()
    } catch (err) {
      setError(getReadableError(err))
    } finally {
      setSaving(false)
    }
  }

  const openResolveModal = (item) => {
    setResolveTarget(item)
    setResolveAt(toLocalDateTimeInput(new Date()))
    setResolveNotes(item.resolutionNotes ?? '')
  }

  const handleConfirmResolve = async (event) => {
    event.preventDefault()
    if (!resolveTarget) return
    setSaving(true)
    setError('')
    setMessage('')
    try {
      await apiRequest(`/admin/incidents/${resolveTarget.id}/resolve`, {
        method: 'POST',
        token: session.token,
        body: {
          resolvedAt: resolveAt ? new Date(resolveAt).toISOString() : new Date().toISOString(),
          resolutionNotes: resolveNotes.trim() || null,
        },
      })
      setResolveTarget(null)
      setMessage('رفع حادثه با موفقیت ثبت و مدت توقف محاسبه شد.')
      await loadIncidents()
    } catch (err) {
      setError(getReadableError(err))
    } finally {
      setSaving(false)
    }
  }

  const handleDelete = async (item) => {
    if (!window.confirm(`حادثه «${item.title}» حذف شود؟`)) return
    try {
      await apiRequest(`/admin/incidents/${item.id}`, {
        method: 'DELETE',
        token: session.token,
      })
      setMessage('گزارش حادثه حذف شد.')
      await loadIncidents()
    } catch (err) {
      setError(getReadableError(err))
    }
  }

  const getCategoryMeta = (key) =>
    INCIDENT_CATEGORIES.find((c) => c.key === key) || { label: key, icon: '📝' }
  const getSeverityLabel = (key) =>
    INCIDENT_SEVERITIES.find((s) => s.key === key)?.label || key
  const getStatusLabel = (key) =>
    INCIDENT_STATUSES.find((s) => s.key === key)?.label || key

  return (
    <section className="inc-page menu-minimal-scope">
      <div className="panel">
        <div className="inc-head-row">
          <div>
            <h2>ثبت و مدیریت حوادث کافه</h2>
            <p>
              ثبت دقیق رخدادهای عملیاتی مانند قطع برق، اختلال اینترنت/کارت‌خوان، خرابی دستگاه‌ها و پیگیری تا زمان رفع حادثه.
            </p>
          </div>
          <div className="inc-head-actions">
            {canCreate ? (
              <button type="button" className="primary-button" onClick={() => openCreateModal()}>
                + ثبت حادثه جدید
              </button>
            ) : null}
          </div>
        </div>

        <div className="inc-kpi-grid">
          <div className={`inc-kpi-card ${board.openCount > 0 ? 'is-alert' : ''}`}>
            <span className="inc-kpi-label">حوادث باز / در جریان</span>
            <span className="inc-kpi-val">{toPersianNumber(board.openCount)}</span>
            <span className="inc-kpi-sub">
              {board.criticalOpenCount > 0
                ? `${toPersianNumber(board.criticalOpenCount)} مورد بحرانی`
                : 'بدون مورد بحرانی'}
            </span>
          </div>
          <div className="inc-kpi-card">
            <span className="inc-kpi-label">حوادث ۳۰ روز اخیر</span>
            <span className="inc-kpi-val">{toPersianNumber(board.last30DaysCount)}</span>
            <span className="inc-kpi-sub">کل ثبت‌شده: {toPersianNumber(board.totalCount)}</span>
          </div>
          <div className="inc-kpi-card">
            <span className="inc-kpi-label">مجموع مدت اختلال ثبت‌شده</span>
            <span className="inc-kpi-val">
              {formatMinutesReadable(board.totalDowntimeMinutes, toPersianNumber)}
            </span>
            <span className="inc-kpi-sub">محاسبه‌شده از زمان وقوع تا رفع</span>
          </div>
          <div className="inc-kpi-card">
            <span className="inc-kpi-label">برآورد کل خسارت</span>
            <span className="inc-kpi-val">{formatMoney(board.totalEstimatedLoss)}</span>
            <span className="inc-kpi-sub">مجموع برآوردهای مالی ثبت‌شده</span>
          </div>
        </div>

        {canCreate ? (
          <div className="inc-quick-strip">
            <span className="inc-quick-label">ثبت سریع:</span>
            {QUICK_TEMPLATES.map((tpl) => (
              <button
                key={tpl.category}
                type="button"
                className="inc-quick-chip"
                onClick={() => openCreateModal(tpl)}
              >
                <span>{tpl.icon}</span>
                <span>{tpl.label}</span>
              </button>
            ))}
          </div>
        ) : null}
      </div>

      {/* Filters + Timeline */}
      <div className="panel">
        <div className="inc-filters">
          <input
            placeholder="جستجو در عنوان، کد، محل یا شرح حادثه..."
            value={search}
            onChange={(e) => setSearch(e.target.value)}
          />
          <select value={categoryFilter} onChange={(e) => setCategoryFilter(e.target.value)}>
            <option value="">همه دسته‌بندی‌ها</option>
            {INCIDENT_CATEGORIES.map((c) => (
              <option key={c.key} value={c.key}>
                {c.icon} {c.label}
              </option>
            ))}
          </select>
          <select value={severityFilter} onChange={(e) => setSeverityFilter(e.target.value)}>
            <option value="">همه شدت‌ها</option>
            {INCIDENT_SEVERITIES.map((s) => (
              <option key={s.key} value={s.key}>
                {s.label}
              </option>
            ))}
          </select>
          <select value={statusFilter} onChange={(e) => setStatusFilter(e.target.value)}>
            <option value="">همه وضعیت‌ها</option>
            {INCIDENT_STATUSES.map((s) => (
              <option key={s.key} value={s.key}>
                {s.label}
              </option>
            ))}
          </select>
        </div>

        {loading ? (
          <div className="inc-empty">در حال بارگذاری لیست حوادث...</div>
        ) : board.items.length === 0 ? (
          <div className="inc-empty">
            هنوز حادثه‌ای با این فیلتر ثبت نشده است.
          </div>
        ) : (
          <div className="inc-list">
            {board.items.map((item) => {
              const cat = getCategoryMeta(item.category)
              const isOpen = item.status === 'Open' || item.status === 'Investigating'
              const photoSrc = item.photoUrl ? normalizeImageUrl(item.photoUrl) : ''

              return (
                <article key={item.id} className={`inc-card sev-${item.severity}`}>
                  <div className="inc-card-top">
                    <div className="inc-card-title-row">
                      <span className="inc-code" dir="ltr" data-latin-digits="true">
                        {item.incidentCode}
                      </span>
                      <h3 className="inc-title">{item.title}</h3>
                    </div>
                    <div className="inc-badges">
                      <span className="inc-badge cat">
                        {cat.icon} {cat.label}
                      </span>
                      <span className={`inc-badge sev-${item.severity}`}>
                        شدت: {getSeverityLabel(item.severity)}
                      </span>
                      <span className={`inc-badge st-${item.status}`}>
                        {getStatusLabel(item.status)}
                      </span>
                    </div>
                  </div>

                  <div className="inc-meta-strip">
                    <span>
                      🕒 زمان وقوع: <strong>{formatDateTime(item.occurredAt)}</strong>
                    </span>
                    {item.resolvedAt ? (
                      <span>
                        ✅ زمان رفع: <strong>{formatDateTime(item.resolvedAt)}</strong>
                      </span>
                    ) : null}
                    {item.durationMinutes != null ? (
                      <span>
                        ⏱️ مدت اختلال:{' '}
                        <strong>{formatMinutesReadable(item.durationMinutes, toPersianNumber)}</strong>
                      </span>
                    ) : null}
                    {item.location ? (
                      <span>
                        📍 محل: <strong>{item.location}</strong>
                      </span>
                    ) : null}
                    {item.estimatedLossAmount > 0 ? (
                      <span>
                        💸 برآورد خسارت: <strong>{formatMoney(item.estimatedLossAmount)}</strong>
                      </span>
                    ) : null}
                  </div>

                  {item.description || item.impactSummary || item.resolutionNotes || photoSrc ? (
                    <div className="inc-body-grid">
                      <div className="inc-text-blocks">
                        {item.description ? <div>{item.description}</div> : null}
                        {item.impactSummary ? (
                          <div className="inc-note-box">
                            <strong>تاثیر عملیاتی:</strong> {item.impactSummary}
                          </div>
                        ) : null}
                        {item.resolutionNotes ? (
                          <div className="inc-note-box is-resolution">
                            <strong>اقدام انجام‌شده / راه‌حل:</strong> {item.resolutionNotes}
                          </div>
                        ) : null}
                      </div>
                      {photoSrc ? (
                        <a href={photoSrc} target="_blank" rel="noreferrer" title="مشاهده تصویر کامل">
                          <img className="inc-photo-thumb" src={photoSrc} alt={item.title} />
                        </a>
                      ) : null}
                    </div>
                  ) : null}

                  <div className="inc-card-footer">
                    <span style={{ fontSize: '0.74rem', color: 'var(--muted)' }}>
                      ثبت‌کننده: <strong>{item.reportedByName || '—'}</strong>
                      {item.resolvedByName ? ` • برطرف‌کننده: ${item.resolvedByName}` : ''}
                    </span>

                    <div className="inc-footer-actions">
                      {isOpen && canUpdate ? (
                        <button
                          type="button"
                          className="primary-button"
                          onClick={() => openResolveModal(item)}
                        >
                          ✓ ثبت رفع حادثه
                        </button>
                      ) : null}
                      {canUpdate ? (
                        <button
                          type="button"
                          className="ghost-button"
                          onClick={() => openEditModal(item)}
                        >
                          ویرایش
                        </button>
                      ) : null}
                      {canDelete ? (
                        <button
                          type="button"
                          className="ghost-button"
                          style={{ color: '#dc2626' }}
                          onClick={() => handleDelete(item)}
                        >
                          حذف
                        </button>
                      ) : null}
                    </div>
                  </div>
                </article>
              )
            })}
          </div>
        )}
      </div>

      {/* Create / Edit Modal */}
      {modalOpen ? (
        <div className="inc-modal-backdrop" onClick={() => !saving && setModalOpen(false)}>
          <form
            className="inc-modal"
            onSubmit={handleSubmit}
            onClick={(e) => e.stopPropagation()}
            role="dialog"
            aria-modal="true"
          >
            <div className="inc-modal-head">
              <h3>{form.id ? 'ویرایش گزارش حادثه' : 'ثبت حادثه جدید'}</h3>
              <button
                type="button"
                className="ghost-button"
                onClick={() => setModalOpen(false)}
                disabled={saving}
              >
                ×
              </button>
            </div>

            <label>
              <span>عنوان حادثه *</span>
              <input
                required
                placeholder="مثلاً قطع برق سالن و بار به مدت ۴۵ دقیقه"
                value={form.title}
                onChange={(e) => setForm((f) => ({ ...f, title: e.target.value }))}
              />
            </label>

            <div className="inc-form-grid-3">
              <label>
                <span>دسته‌بندی</span>
                <select
                  value={form.category}
                  onChange={(e) => setForm((f) => ({ ...f, category: e.target.value }))}
                >
                  {INCIDENT_CATEGORIES.map((c) => (
                    <option key={c.key} value={c.key}>
                      {c.icon} {c.label}
                    </option>
                  ))}
                </select>
              </label>
              <label>
                <span>شدت حادثه</span>
                <select
                  value={form.severity}
                  onChange={(e) => setForm((f) => ({ ...f, severity: e.target.value }))}
                >
                  {INCIDENT_SEVERITIES.map((s) => (
                    <option key={s.key} value={s.key}>
                      {s.label}
                    </option>
                  ))}
                </select>
              </label>
              <label>
                <span>وضعیت</span>
                <select
                  value={form.status}
                  onChange={(e) => setForm((f) => ({ ...f, status: e.target.value }))}
                >
                  {INCIDENT_STATUSES.map((s) => (
                    <option key={s.key} value={s.key}>
                      {s.label}
                    </option>
                  ))}
                </select>
              </label>
            </div>

            <div className="inc-form-grid-2">
              <label>
                <span>تاریخ و ساعت وقوع *</span>
                <input
                  required
                  type="datetime-local"
                  className="ltr-input"
                  dir="ltr"
                  data-latin-digits="true"
                  value={form.occurredAt}
                  onChange={(e) => setForm((f) => ({ ...f, occurredAt: e.target.value }))}
                />
              </label>
              <label>
                <span>تاریخ و ساعت رفع (اختیاری)</span>
                <input
                  type="datetime-local"
                  className="ltr-input"
                  dir="ltr"
                  data-latin-digits="true"
                  value={form.resolvedAt}
                  onChange={(e) => setForm((f) => ({ ...f, resolvedAt: e.target.value }))}
                />
              </label>
            </div>

            <div className="inc-form-grid-3">
              <label>
                <span>محل وقوع</span>
                <input
                  placeholder="مثلاً بار قهوه، سالن اصلی، انبار"
                  value={form.location}
                  onChange={(e) => setForm((f) => ({ ...f, location: e.target.value }))}
                />
              </label>
              <label>
                <span>گزارش‌دهنده</span>
                <input
                  placeholder="نام ثبت‌کننده (خالی = خودکار)"
                  value={form.reportedByName}
                  onChange={(e) => setForm((f) => ({ ...f, reportedByName: e.target.value }))}
                />
              </label>
              <label>
                <span>برآورد خسارت (تومان — اختیاری)</span>
                <input
                  className="ltr-input"
                  type="number"
                  min="0"
                  placeholder="مثلاً ۵۰۰۰۰۰"
                  value={form.estimatedLossAmount}
                  onChange={(e) => setForm((f) => ({ ...f, estimatedLossAmount: e.target.value }))}
                />
              </label>
            </div>

            <label>
              <span>شرح کامل حادثه</span>
              <textarea
                rows="3"
                placeholder="جزئیات دقیق اتفاقی که افتاد..."
                value={form.description}
                onChange={(e) => setForm((f) => ({ ...f, description: e.target.value }))}
              />
            </label>

            <div className="inc-form-grid-2">
              <label>
                <span>تاثیر روی فروش / سرویس‌دهی</span>
                <textarea
                  rows="2"
                  placeholder="مثلاً توقف سرو نوشیدنی گرم به مدت ۴۵ دقیقه"
                  value={form.impactSummary}
                  onChange={(e) => setForm((f) => ({ ...f, impactSummary: e.target.value }))}
                />
              </label>
              <label>
                <span>اقدام انجام‌شده / راه‌حل</span>
                <textarea
                  rows="2"
                  placeholder="اقدامی که برای رفع مشکل یا پیشگیری انجام شد..."
                  value={form.resolutionNotes}
                  onChange={(e) => setForm((f) => ({ ...f, resolutionNotes: e.target.value }))}
                />
              </label>
            </div>

            <ImageUploader
              label="تصویر مستند (اختیاری)"
              hint="در صورت نیاز می‌توانید عکسی از حادثه، خرابی یا کنتور/تجهیزات پیوست کنید."
              value={form.photoUrl}
              token={session?.token}
              onChange={(url) => setForm((f) => ({ ...f, photoUrl: url ?? '' }))}
            />

            <div className="inc-modal-actions">
              <button
                type="button"
                className="ghost-button"
                onClick={() => setModalOpen(false)}
                disabled={saving}
              >
                انصراف
              </button>
              <button type="submit" className="primary-button" disabled={saving}>
                {saving ? 'در حال ذخیره...' : form.id ? 'ذخیره تغییرات' : 'ثبت حادثه'}
              </button>
            </div>
          </form>
        </div>
      ) : null}

      {/* Quick Resolve Modal */}
      {resolveTarget ? (
        <div className="inc-modal-backdrop" onClick={() => !saving && setResolveTarget(null)}>
          <form
            className="inc-modal inc-modal--sm"
            onSubmit={handleConfirmResolve}
            onClick={(e) => e.stopPropagation()}
            role="dialog"
            aria-modal="true"
          >
            <div className="inc-modal-head">
              <h3>ثبت رفع حادثه</h3>
              <button
                type="button"
                className="ghost-button"
                onClick={() => setResolveTarget(null)}
                disabled={saving}
              >
                ×
              </button>
            </div>

            <p style={{ margin: 0, fontSize: '0.84rem', color: 'var(--text)' }}>
              حادثه: <strong>{resolveTarget.title}</strong>
            </p>

            <label>
              <span>زمان دقیق رفع حادثه</span>
              <input
                required
                type="datetime-local"
                className="ltr-input"
                dir="ltr"
                data-latin-digits="true"
                value={resolveAt}
                onChange={(e) => setResolveAt(e.target.value)}
              />
            </label>

            <label>
              <span>شرح اقدام انجام‌شده / علت رفع</span>
              <textarea
                rows="3"
                placeholder="مثلاً وصل شدن مجدد برق منطقه و بررسی سالم بودن یخچال‌ها"
                value={resolveNotes}
                onChange={(e) => setResolveNotes(e.target.value)}
              />
            </label>

            <div className="inc-modal-actions">
              <button
                type="button"
                className="ghost-button"
                onClick={() => setResolveTarget(null)}
                disabled={saving}
              >
                انصراف
              </button>
              <button type="submit" className="primary-button" disabled={saving}>
                {saving ? 'در حال ثبت...' : '✓ تایید رفع حادثه'}
              </button>
            </div>
          </form>
        </div>
      ) : null}
    </section>
  )
}
