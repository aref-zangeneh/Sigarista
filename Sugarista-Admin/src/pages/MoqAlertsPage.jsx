import { useCallback, useEffect, useMemo, useState } from 'react'
import { useAdmin } from '../context/AdminContext'
import { apiRequest } from '../utils/adminApi'
import { formatDateTime, toPersianNumber } from '../utils/adminFormatters'
import { hasPermission, PERMISSIONS } from '../utils/adminPermissions'
import {
  getMoqSeverityMeta,
  getMoqSourceMeta,
  normalizeMoqDigits,
  parseMoqNumber,
} from '../utils/moqHelpers.js'
import './MoqAlertsPage.css'

export function MoqAlertsPage() {
  const { session, setMessage, setError } = useAdmin()
  const canUpdate = hasPermission(session, PERMISSIONS['moq-alerts'].update)

  const [includeHealthy, setIncludeHealthy] = useState(false)
  const [sourceFilter, setSourceFilter] = useState('')
  const [severityFilter, setSeverityFilter] = useState('')
  const [search, setSearch] = useState('')

  const [snapshot, setSnapshot] = useState(null)
  const [loading, setLoading] = useState(false)
  const [liveConnected, setLiveConnected] = useState(false)

  // Modals
  const [thresholdModal, setThresholdModal] = useState(null)
  const [thresholdInput, setThresholdInput] = useState('')
  const [thresholdSaving, setThresholdSaving] = useState(false)

  const [restockModal, setRestockModal] = useState(null)
  const [restockInput, setRestockInput] = useState('')
  const [restockNote, setRestockNote] = useState('')
  const [restockSaving, setRestockSaving] = useState(false)

  const loadSnapshot = useCallback(async (withHealthy = includeHealthy) => {
    if (!session?.token) return
    setLoading(true)
    try {
      const params = new URLSearchParams()
      if (withHealthy) params.set('includeHealthy', 'true')
      const qs = params.toString()
      const data = await apiRequest(`/admin/moq-alerts${qs ? `?${qs}` : ''}`, {
        token: session.token,
      })
      setSnapshot(data)
    } catch (err) {
      setError(err.message || 'خطا در دریافت هشدارهای MOQ.')
    } finally {
      setLoading(false)
    }
  }, [session?.token, includeHealthy, setError])

  useEffect(() => {
    loadSnapshot(includeHealthy)
  }, [includeHealthy, loadSnapshot])

  // Subscribe to the global real-time SSE stream events (zero extra connections!)
  useEffect(() => {
    const onLiveEvent = (event) => {
      const detail = event.detail ?? {}
      setLiveConnected(Boolean(detail.connected))
      if (!detail.snapshot) return

      if (!includeHealthy) {
        setSnapshot(detail.snapshot)
      } else {
        // When viewing all items including healthy ones, reload full list on version bump
        loadSnapshot(true)
      }
    }

    window.addEventListener('sugarista:moq-snapshot', onLiveEvent)
    return () => window.removeEventListener('sugarista:moq-snapshot', onLiveEvent)
  }, [includeHealthy, loadSnapshot])

  const filteredItems = useMemo(() => {
    const list = snapshot?.items ?? []
    const cleanSearch = normalizeMoqDigits(search).trim().toLowerCase()

    return list.filter((item) => {
      if (sourceFilter && String(item.sourceType) !== sourceFilter) return false
      if (severityFilter && String(item.severity) !== severityFilter) return false
      if (cleanSearch) {
        const hay = `${item.name} ${item.contextLabel ?? ''} ${item.unit}`.toLowerCase()
        if (!hay.includes(cleanSearch)) return false
      }
      return true
    })
  }, [snapshot, sourceFilter, severityFilter, search])

  const openThresholdModal = (item) => {
    setThresholdModal(item)
    setThresholdInput(String(item.moqQuantity ?? 0))
  }

  const openRestockModal = (item) => {
    setRestockModal(item)
    const suggested = item.deficitQuantity > 0 ? item.deficitQuantity : Math.max(1, item.moqQuantity || 1)
    setRestockInput(String(suggested))
    setRestockNote('')
  }

  const handleSaveThreshold = async (event) => {
    event.preventDefault()
    if (!thresholdModal || !canUpdate) return
    const val = parseMoqNumber(thresholdInput, { allowZero: true })
    if (val === null) {
      setError('نقطه سفارش (MOQ) باید عددی غیرمنفی و معتبر باشد.')
      return
    }

    setThresholdSaving(true)
    setError('')
    try {
      await apiRequest('/admin/moq-alerts/threshold', {
        method: 'PUT',
        token: session.token,
        body: {
          sourceType: thresholdModal.sourceType,
          itemId: thresholdModal.itemId,
          warehouseId: thresholdModal.warehouseId ?? null,
          moqQuantity: val,
        },
      })
      setMessage(`نقطه سفارش (MOQ) برای «${thresholdModal.name}» روی ${toPersianNumber(val)} ${thresholdModal.unit} ذخیره شد.`)
      setThresholdModal(null)
      await loadSnapshot(includeHealthy)
    } catch (err) {
      setError(err.message || 'خطا در ذخیره نقطه سفارش.')
    } finally {
      setThresholdSaving(false)
    }
  }

  const handleQuickRestock = async (event) => {
    event.preventDefault()
    if (!restockModal || !canUpdate) return
    const qty = parseMoqNumber(restockInput, { allowZero: false })
    if (qty === null) {
      setError('مقدار شارژ باید بزرگ‌تر از صفر باشد.')
      return
    }

    setRestockSaving(true)
    setError('')
    try {
      await apiRequest('/admin/moq-alerts/quick-restock', {
        method: 'POST',
        token: session.token,
        body: {
          sourceType: restockModal.sourceType,
          itemId: restockModal.itemId,
          warehouseId: restockModal.warehouseId ?? null,
          quantity: qty,
          note: restockNote.trim() || null,
        },
      })
      setMessage(`موجودی «${restockModal.name}» به مقدار ${toPersianNumber(qty)} ${restockModal.unit} شارژ شد.`)
      setRestockModal(null)
      await loadSnapshot(includeHealthy)
    } catch (err) {
      setError(err.message || 'خطا در شارژ سریع موجودی.')
    } finally {
      setRestockSaving(false)
    }
  }

  return (
    <div className="moq-page">
      <header className="moq-header">
        <div className="moq-header-title">
          <h1>هشدارهای MOQ و کمبود موجودی</h1>
          <p>پایش زندهٔ نقطه سفارش (Minimum Order Quantity) مواد اولیه، آیتم‌های منو و انبارها بدون نیاز به رفرش</p>
        </div>
        <div className="moq-header-actions">
          <span className={`moq-live-pill ${liveConnected ? '' : 'moq-live-pill--offline'}`}>
            <span className="moq-live-dot" aria-hidden="true" />
            {liveConnected ? 'متصل • بروزرسانی زنده (Real-time)' : 'در حال همگام‌سازی...'}
          </span>
          <button type="button" className="ghost-button" onClick={() => loadSnapshot(includeHealthy)} disabled={loading}>
            بروزرسانی دستی
          </button>
        </div>
      </header>

      {/* KPI Strip */}
      <section className="moq-kpi-grid" aria-label="خلاصه هشدارهای MOQ">
        <article className="moq-kpi-card moq-kpi-card--danger">
          <span className="moq-kpi-label">اتمام موجودی (صفر یا منفی)</span>
          <strong className="moq-kpi-value">{toPersianNumber(snapshot?.outOfStockCount ?? 0)}</strong>
          <span className="moq-kpi-sub">نیازمند تأمین فوری</span>
        </article>

        <article className="moq-kpi-card moq-kpi-card--critical">
          <span className="moq-kpi-label">وضعیت بحرانی (زیر ۵۰٪ MOQ)</span>
          <strong className="moq-kpi-value">{toPersianNumber(snapshot?.criticalCount ?? 0)}</strong>
          <span className="moq-kpi-sub">خطر اتمام در شیفت جاری</span>
        </article>

        <article className="moq-kpi-card moq-kpi-card--warning">
          <span className="moq-kpi-label">رسیده به حد MOQ</span>
          <strong className="moq-kpi-value">{toPersianNumber(snapshot?.warningCount ?? 0)}</strong>
          <span className="moq-kpi-sub">زمان ثبت سفارش خرید / تولید</span>
        </article>

        <article className="moq-kpi-card">
          <span className="moq-kpi-label">اقلام دارای MOQ فعال</span>
          <strong className="moq-kpi-value">{toPersianNumber(snapshot?.configuredMoqItemsCount ?? 0)}</strong>
          <span className="moq-kpi-sub">
            مجموع هشدارهای فعال: {toPersianNumber(snapshot?.totalAlertsCount ?? 0)}
          </span>
        </article>
      </section>

      {/* Main Panel */}
      <section className="moq-panel">
        <div className="moq-mode-tabs">
          <button
            type="button"
            className={`moq-mode-tab ${!includeHealthy ? 'moq-mode-tab--active' : ''}`}
            onClick={() => {
              setIncludeHealthy(false)
              setSeverityFilter('')
            }}
          >
            هشدارهای فعال ({toPersianNumber(snapshot?.totalAlertsCount ?? 0)})
          </button>
          <button
            type="button"
            className={`moq-mode-tab ${includeHealthy ? 'moq-mode-tab--active' : ''}`}
            onClick={() => setIncludeHealthy(true)}
          >
            همه اقلام و تنظیم MOQ
          </button>
        </div>

        <div className="moq-filters">
          <input
            type="text"
            className="moq-search-input"
            placeholder="جستجوی نام کالا، ماده اولیه، دسته‌بندی یا انبار..."
            value={search}
            onChange={(e) => setSearch(e.target.value)}
          />
          <select value={sourceFilter} onChange={(e) => setSourceFilter(e.target.value)}>
            <option value="">همه منابع</option>
            <option value="Ingredient">مواد اولیه (انبار اصلی)</option>
            <option value="MenuItem">آیتم‌های منو</option>
            <option value="WarehouseIngredient">انبارها / انبارک‌ها</option>
          </select>
          <select value={severityFilter} onChange={(e) => setSeverityFilter(e.target.value)}>
            <option value="">همه وضعیت‌ها</option>
            <option value="OutOfStock">اتمام موجودی</option>
            <option value="Critical">بحرانی (زیر ۵۰٪ MOQ)</option>
            <option value="Warning">کمبود (نزدیک MOQ)</option>
            {includeHealthy && <option value="Healthy">موجودی سالم</option>}
          </select>
          {(search || sourceFilter || severityFilter) && (
            <button
              type="button"
              className="ghost-button"
              onClick={() => {
                setSearch('')
                setSourceFilter('')
                setSeverityFilter('')
              }}
            >
              پاک‌کردن فیلترها
            </button>
          )}
        </div>

        <div className="moq-table-wrap">
          <table className="moq-table">
            <thead>
              <tr>
                <th>قلم / منبع</th>
                <th>وضعیت</th>
                <th>موجودی فعلی</th>
                <th>نقطه سفارش (MOQ)</th>
                <th>کسری تا MOQ</th>
                <th>نسبت موجودی</th>
                <th>آخرین تغییر</th>
                {canUpdate && <th style={{ textAlign: 'left' }}>عملیات</th>}
              </tr>
            </thead>
            <tbody>
              {loading && !snapshot ? (
                <tr>
                  <td colSpan={canUpdate ? 8 : 7} className="moq-empty">در حال بارگذاری...</td>
                </tr>
              ) : filteredItems.length === 0 ? (
                <tr>
                  <td colSpan={canUpdate ? 8 : 7} className="moq-empty">
                    {includeHealthy
                      ? 'قلمی با این فیلترها یافت نشد.'
                      : 'عالی! در حال حاضر هیچ کمبود موجودی یا هشدار MOQ فعالی وجود ندارد.'}
                  </td>
                </tr>
              ) : (
                filteredItems.map((item) => {
                  const sev = getMoqSeverityMeta(item.severity)
                  const src = getMoqSourceMeta(item.sourceType)
                  return (
                    <tr key={item.alertKey}>
                      <td>
                        <div className="moq-item-cell">
                          <span className="moq-item-name">{item.name}</span>
                          <span className="moq-item-meta">
                            <span className={`moq-source-chip moq-source-chip--${src.tone}`}>
                              {src.label}
                            </span>
                            {item.contextLabel ? <span>{item.contextLabel}</span> : null}
                          </span>
                        </div>
                      </td>
                      <td>
                        <span className={`moq-sev-badge moq-sev-badge--${sev.tone}`}>
                          {sev.label}
                        </span>
                      </td>
                      <td style={{ fontWeight: 800, whiteSpace: 'nowrap' }}>
                        {toPersianNumber(item.currentQuantity)} {item.unit}
                      </td>
                      <td style={{ whiteSpace: 'nowrap' }}>
                        {item.moqQuantity > 0
                          ? `${toPersianNumber(item.moqQuantity)} ${item.unit}`
                          : 'تنظیم‌نشده'}
                      </td>
                      <td style={{ whiteSpace: 'nowrap', color: item.deficitQuantity > 0 ? '#dc2626' : 'var(--muted, #64748b)', fontWeight: item.deficitQuantity > 0 ? 700 : 400 }}>
                        {item.deficitQuantity > 0
                          ? `${toPersianNumber(item.deficitQuantity)} ${item.unit}`
                          : '—'}
                      </td>
                      <td>
                        <div className="moq-progress-wrap">
                          <div className="moq-progress-track">
                            <div
                              className={`moq-progress-fill moq-progress-fill--${sev.tone}`}
                              style={{ width: `${Math.min(100, Math.max(0, Number(item.fillPercent) || 0))}%` }}
                            />
                          </div>
                          <small style={{ color: 'var(--muted, #64748b)', fontSize: '0.72rem' }}>
                            {toPersianNumber(item.fillPercent)}٪
                          </small>
                        </div>
                      </td>
                      <td style={{ whiteSpace: 'nowrap', fontSize: '0.78rem', color: 'var(--muted, #64748b)' }}>
                        {formatDateTime(item.lastUpdatedAt)}
                      </td>
                      {canUpdate && (
                        <td>
                          <div className="moq-row-actions">
                            <button
                              type="button"
                              className="moq-btn-xs moq-btn-xs--restock"
                              onClick={() => openRestockModal(item)}
                            >
                              + شارژ
                            </button>
                            <button
                              type="button"
                              className="moq-btn-xs"
                              onClick={() => openThresholdModal(item)}
                            >
                              تنظیم MOQ
                            </button>
                          </div>
                        </td>
                      )}
                    </tr>
                  )
                })
              )}
            </tbody>
          </table>
        </div>
      </section>

      {/* Edit MOQ Threshold Modal */}
      {thresholdModal && (
        <div
          className="moq-modal-backdrop"
          role="dialog"
          aria-modal="true"
          onClick={(e) => {
            if (e.target === e.currentTarget && !thresholdSaving) setThresholdModal(null)
          }}
        >
          <div className="moq-modal">
            <div className="moq-modal-head">
              <h2>تنظیم نقطه سفارش (MOQ) — {thresholdModal.name}</h2>
              <button
                type="button"
                className="moq-toast-close"
                aria-label="بستن"
                onClick={() => setThresholdModal(null)}
              >
                ×
              </button>
            </div>
            <form onSubmit={handleSaveThreshold} style={{ display: 'flex', flexDirection: 'column', gap: '0.85rem' }}>
              <div className="moq-field">
                <label>حداقل موجودی هشدار / MOQ ({thresholdModal.unit})</label>
                <input
                  type="text"
                  inputMode="decimal"
                  autoFocus
                  value={thresholdInput}
                  onChange={(e) => setThresholdInput(e.target.value)}
                />
                <small>
                  موجودی فعلی: {toPersianNumber(thresholdModal.currentQuantity)} {thresholdModal.unit}. قرار دادن عدد ۰ هشدار MOQ این قلم را غیرفعال می‌کند.
                </small>
              </div>
              <div style={{ display: 'flex', justifyContent: 'flex-end', gap: '0.5rem' }}>
                <button type="button" className="ghost-button" onClick={() => setThresholdModal(null)}>
                  انصراف
                </button>
                <button type="submit" disabled={thresholdSaving}>
                  {thresholdSaving ? 'در حال ذخیره...' : 'ذخیره MOQ'}
                </button>
              </div>
            </form>
          </div>
        </div>
      )}

      {/* Quick Restock Modal */}
      {restockModal && (
        <div
          className="moq-modal-backdrop"
          role="dialog"
          aria-modal="true"
          onClick={(e) => {
            if (e.target === e.currentTarget && !restockSaving) setRestockModal(null)
          }}
        >
          <div className="moq-modal">
            <div className="moq-modal-head">
              <h2>شارژ سریع موجودی — {restockModal.name}</h2>
              <button
                type="button"
                className="moq-toast-close"
                aria-label="بستن"
                onClick={() => setRestockModal(null)}
              >
                ×
              </button>
            </div>
            <form onSubmit={handleQuickRestock} style={{ display: 'flex', flexDirection: 'column', gap: '0.85rem' }}>
              <div className="moq-field">
                <label>مقدار ورودی جدید ({restockModal.unit})</label>
                <input
                  type="text"
                  inputMode="decimal"
                  autoFocus
                  value={restockInput}
                  onChange={(e) => setRestockInput(e.target.value)}
                />
                <small>
                  موجودی فعلی: {toPersianNumber(restockModal.currentQuantity)} • نقطه سفارش (MOQ): {toPersianNumber(restockModal.moqQuantity)} {restockModal.unit}
                </small>
              </div>
              <div className="moq-field">
                <label>توضیحات (اختیاری)</label>
                <input
                  type="text"
                  maxLength={250}
                  placeholder="مثلاً خرید فوری از تأمین‌کننده"
                  value={restockNote}
                  onChange={(e) => setRestockNote(e.target.value)}
                />
              </div>
              <div style={{ display: 'flex', justifyContent: 'flex-end', gap: '0.5rem' }}>
                <button type="button" className="ghost-button" onClick={() => setRestockModal(null)}>
                  انصراف
                </button>
                <button type="submit" disabled={restockSaving}>
                  {restockSaving ? 'در حال ثبت...' : 'ثبت شارژ موجودی'}
                </button>
              </div>
            </form>
          </div>
        </div>
      )}
    </div>
  )
}
