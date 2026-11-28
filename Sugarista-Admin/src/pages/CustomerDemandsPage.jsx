import { useCallback, useEffect, useState } from 'react'
import { useAdmin } from '../context/AdminContext'
import { hasPermission, PERMISSIONS } from '../utils/adminPermissions'
import './MenuSectionMinimal.css'
import './CustomerDemandsPage.css'

const DEMAND_CATEGORIES = [
  { key: 'HotDrink', label: 'نوشیدنی گرم', icon: '☕' },
  { key: 'ColdDrink', label: 'نوشیدنی سرد', icon: '🧊' },
  { key: 'CakeAndDessert', label: 'کیک و دسر', icon: '🍰' },
  { key: 'BakeryAndFood', label: 'نانوایی و غذا', icon: '🥐' },
  { key: 'MilkOrAddon', label: 'شیر گیاهی / افزودنی', icon: '🥛' },
  { key: 'ServiceOrFacility', label: 'امکانات و خدمات کافه', icon: '🛋️' },
  { key: 'Other', label: 'سایر', icon: '💡' },
]

const DEMAND_STATUSES = [
  { key: 'Open', label: 'در حال جمع‌آوری' },
  { key: 'Planned', label: 'در برنامه اضافه شدن' },
  { key: 'AddedToMenu', label: 'به منو اضافه شد' },
  { key: 'Dismissed', label: 'رد شد' },
]

const CHANNELS = [
  { key: 'POS', label: 'صندوق (POS)' },
  { key: 'Salon', label: 'سالن' },
  { key: 'Instagram', label: 'اینستاگرام' },
  { key: 'Online', label: 'سایت / آنلاین' },
  { key: 'Phone', label: 'تلفن / واتساپ' },
]

const RANGE_PRESETS = [
  { key: 'today', label: 'امروز', days: 0 },
  { key: '7d', label: '۷ روز اخیر', days: 7 },
  { key: '30d', label: '۳۰ روز اخیر', days: 30 },
  { key: '90d', label: '۹۰ روز اخیر', days: 90 },
  { key: 'all', label: 'همه زمان‌ها', days: null },
]

function toIsoDateOnly(date) {
  const pad = (n) => String(n).padStart(2, '0')
  return `${date.getFullYear()}-${pad(date.getMonth() + 1)}-${pad(date.getDate())}`
}

function computePresetDates(presetKey) {
  if (presetKey === 'all') return { from: '', to: '' }
  const today = new Date()
  const to = toIsoDateOnly(today)
  if (presetKey === 'today') return { from: to, to }
  const days = RANGE_PRESETS.find((p) => p.key === presetKey)?.days ?? 30
  const start = new Date(today)
  start.setDate(start.getDate() - days)
  return { from: toIsoDateOnly(start), to }
}

export function CustomerDemandsPage() {
  const {
    session,
    apiRequest,
    getReadableError,
    setMessage,
    setError,
    toPersianNumber,
    formatDateTime,
  } = useAdmin()

  const canCreate = hasPermission(session, PERMISSIONS['customer-demands']?.create)
  const canUpdate = hasPermission(session, PERMISSIONS['customer-demands']?.update)
  const canDelete = hasPermission(session, PERMISSIONS['customer-demands']?.delete)

  const [rangePreset, setRangePreset] = useState('30d')
  const [fromDate, setFromDate] = useState(() => computePresetDates('30d').from)
  const [toDate, setToDate] = useState(() => computePresetDates('30d').to)
  const [categoryFilter, setCategoryFilter] = useState('')
  const [statusFilter, setStatusFilter] = useState('')
  const [search, setSearch] = useState('')

  const [loading, setLoading] = useState(true)
  const [busy, setBusy] = useState(false)
  const [board, setBoard] = useState({
    totalRequests: 0,
    uniqueItemsCount: 0,
    topRequestedItem: '',
    topRequestedCount: 0,
    plannedItemsCount: 0,
    addedToMenuCount: 0,
    rankedSummary: [],
    entries: [],
  })

  // Quick-add form
  const [requestedItem, setRequestedItem] = useState('')
  const [category, setCategory] = useState('ColdDrink')
  const [requestedCount, setRequestedCount] = useState('1')
  const [channel, setChannel] = useState('POS')
  const [notes, setNotes] = useState('')
  const [customerName, setCustomerName] = useState('')
  const [customerPhone, setCustomerPhone] = useState('')

  const loadBoard = useCallback(async () => {
    if (!session?.token) return
    try {
      const params = new URLSearchParams()
      if (fromDate) params.set('fromDate', new Date(`${fromDate}T00:00:00`).toISOString())
      if (toDate) params.set('toDate', new Date(`${toDate}T23:59:59`).toISOString())
      if (categoryFilter) params.set('category', categoryFilter)
      if (statusFilter) params.set('status', statusFilter)
      if (search.trim()) params.set('search', search.trim())
      const qs = params.toString()

      const data = await apiRequest(`/admin/customer-demands${qs ? `?${qs}` : ''}`, {
        token: session.token,
      })

      setBoard({
        totalRequests: data?.totalRequests ?? 0,
        uniqueItemsCount: data?.uniqueItemsCount ?? 0,
        topRequestedItem: data?.topRequestedItem ?? '',
        topRequestedCount: data?.topRequestedCount ?? 0,
        plannedItemsCount: data?.plannedItemsCount ?? 0,
        addedToMenuCount: data?.addedToMenuCount ?? 0,
        rankedSummary: Array.isArray(data?.rankedSummary) ? data.rankedSummary : [],
        entries: Array.isArray(data?.entries) ? data.entries : [],
      })
    } catch (err) {
      setError(getReadableError(err))
    } finally {
      setLoading(false)
    }
  }, [session?.token, fromDate, toDate, categoryFilter, statusFilter, search, apiRequest, getReadableError, setError])

  useEffect(() => {
    loadBoard()
  }, [loadBoard])

  const handleSelectPreset = (key) => {
    setRangePreset(key)
    const { from, to } = computePresetDates(key)
    setFromDate(from)
    setToDate(to)
  }

  const handleAddDemand = async (event) => {
    event.preventDefault()
    if (!requestedItem.trim()) {
      setError('نام آیتم یا نیاز درخواستی مشتری را وارد کنید.')
      return
    }
    setBusy(true)
    setError('')
    setMessage('')
    try {
      await apiRequest('/admin/customer-demands', {
        method: 'POST',
        token: session.token,
        body: {
          requestedItem: requestedItem.trim(),
          category,
          requestedCount: Math.max(1, Number(requestedCount) || 1),
          channel,
          customerName: customerName.trim() || null,
          customerPhone: customerPhone.trim() || null,
          notes: notes.trim() || null,
        },
      })
      setRequestedItem('')
      setRequestedCount('1')
      setNotes('')
      setCustomerName('')
      setCustomerPhone('')
      setMessage('نیاز درخواستی مشتری ثبت شد.')
      await loadBoard()
    } catch (err) {
      setError(getReadableError(err))
    } finally {
      setBusy(false)
    }
  }

  const handleQuickPlusOne = async (rankedItem) => {
    if (!canCreate) return
    setBusy(true)
    setError('')
    try {
      await apiRequest('/admin/customer-demands', {
        method: 'POST',
        token: session.token,
        body: {
          requestedItem: rankedItem.itemName,
          category: rankedItem.category,
          requestedCount: 1,
          channel: 'POS',
        },
      })
      setMessage(`+۱ درخواست جدید برای «${rankedItem.itemName}» ثبت شد.`)
      await loadBoard()
    } catch (err) {
      setError(getReadableError(err))
    } finally {
      setBusy(false)
    }
  }

  const handleGroupStatusChange = async (normalizedKey, nextStatus) => {
    if (!canUpdate) return
    try {
      await apiRequest('/admin/customer-demands/update-group-status', {
        method: 'POST',
        token: session.token,
        body: { normalizedKey, status: nextStatus },
      })
      await loadBoard()
    } catch (err) {
      setError(getReadableError(err))
    }
  }

  const handleDeleteEntry = async (id) => {
    if (!canDelete) return
    try {
      await apiRequest(`/admin/customer-demands/${id}`, {
        method: 'DELETE',
        token: session.token,
      })
      await loadBoard()
    } catch (err) {
      setError(getReadableError(err))
    }
  }

  const getCategoryMeta = (key) =>
    DEMAND_CATEGORIES.find((c) => c.key === key) || { label: key, icon: '💡' }
  const getChannelLabel = (key) =>
    CHANNELS.find((c) => c.key === key)?.label || key

  const maxRankCount = board.rankedSummary[0]?.totalRequests || 1

  return (
    <section className="cd-page menu-minimal-scope">
      <div className="panel">
        <div className="cd-head-row">
          <div>
            <h2>بررسی نیازمندی‌های مشتریان</h2>
            <p>
              محصولات، نوشیدنی‌ها یا امکاناتی که مشتریان درخواست می‌کنند ولی فعلاً در منو ندارید را ثبت کنید و در هر بازه زمانی پرتقاضاترین‌ها را ببینید.
            </p>
          </div>
        </div>

        {/* Date Range Selector */}
        <div className="cd-range-bar">
          <div className="cd-presets">
            {RANGE_PRESETS.map((p) => (
              <button
                key={p.key}
                type="button"
                className={`cd-preset-btn ${rangePreset === p.key ? 'is-active' : ''}`}
                onClick={() => handleSelectPreset(p.key)}
              >
                {p.label}
              </button>
            ))}
          </div>
          <div className="cd-custom-dates">
            <label>
              <span>از تاریخ:</span>
              <input
                type="date"
                className="ltr-input"
                dir="ltr"
                data-latin-digits="true"
                value={fromDate}
                onChange={(e) => {
                  setRangePreset('custom')
                  setFromDate(e.target.value)
                }}
              />
            </label>
            <label>
              <span>تا تاریخ:</span>
              <input
                type="date"
                className="ltr-input"
                dir="ltr"
                data-latin-digits="true"
                value={toDate}
                onChange={(e) => {
                  setRangePreset('custom')
                  setToDate(e.target.value)
                }}
              />
            </label>
          </div>
        </div>

        {/* KPI Cards */}
        <div className="cd-kpi-grid">
          <div className="cd-kpi-card">
            <span className="cd-kpi-label">کل درخواست‌ها در این بازه</span>
            <span className="cd-kpi-val">{toPersianNumber(board.totalRequests)}</span>
            <span className="cd-kpi-sub">مجموع نفرات / تقاضاهای ثبت‌شده</span>
          </div>
          <div className="cd-kpi-card">
            <span className="cd-kpi-label">اقلام متمایز درخواستی</span>
            <span className="cd-kpi-val">{toPersianNumber(board.uniqueItemsCount)}</span>
            <span className="cd-kpi-sub">مواردی که فعلاً نداریم</span>
          </div>
          <div className="cd-kpi-card is-highlight">
            <span className="cd-kpi-label">پرتقاضاترین آیتم این بازه</span>
            <span className="cd-kpi-val">{board.topRequestedItem || '—'}</span>
            <span className="cd-kpi-sub">
              {board.topRequestedCount > 0
                ? `${toPersianNumber(board.topRequestedCount)} درخواست در این بازه`
                : 'هنوز ثبتی نیست'}
            </span>
          </div>
          <div className="cd-kpi-card">
            <span className="cd-kpi-label">اضافه‌شده به منو / در برنامه</span>
            <span className="cd-kpi-val">
              {toPersianNumber(board.addedToMenuCount)} / {toPersianNumber(board.plannedItemsCount)}
            </span>
            <span className="cd-kpi-sub">تبدیل‌شده به محصول واقعی</span>
          </div>
        </div>
      </div>

      {/* Quick Record Form */}
      {canCreate ? (
        <form className="panel cd-add-form" onSubmit={handleAddDemand}>
          <div className="panel-head">
            <div>
              <h3>ثبت سریع درخواست جدید مشتری</h3>
              <p>اگر نام مشابهی قبلاً ثبت شده باشد، در تحلیل بازه زمانی به‌صورت خودکار با هم جمع می‌شوند.</p>
            </div>
          </div>

          <div className="cd-add-grid">
            <label>
              <span>محصول یا نیاز درخواستی *</span>
              <input
                required
                placeholder="مثلاً ماچا لاته، شیر جو دوسر، کیک بدون قند..."
                value={requestedItem}
                onChange={(e) => setRequestedItem(e.target.value)}
              />
            </label>
            <label>
              <span>دسته‌بندی</span>
              <select value={category} onChange={(e) => setCategory(e.target.value)}>
                {DEMAND_CATEGORIES.map((c) => (
                  <option key={c.key} value={c.key}>
                    {c.icon} {c.label}
                  </option>
                ))}
              </select>
            </label>
            <label>
              <span>تعداد نفر</span>
              <input
                className="ltr-input"
                type="number"
                min="1"
                value={requestedCount}
                onChange={(e) => setRequestedCount(e.target.value)}
              />
            </label>
            <label>
              <span>کانال درخواست</span>
              <select value={channel} onChange={(e) => setChannel(e.target.value)}>
                {CHANNELS.map((c) => (
                  <option key={c.key} value={c.key}>
                    {c.label}
                  </option>
                ))}
              </select>
            </label>
            <label>
              <span>توضیح کوتاه (اختیاری)</span>
              <input
                placeholder="مثلاً سرد با شیر بادام..."
                value={notes}
                onChange={(e) => setNotes(e.target.value)}
              />
            </label>
          </div>

          <div className="cd-add-row2">
            <label>
              <span>نام مشتری (اختیاری)</span>
              <input
                placeholder="برای اطلاع‌رسانی پس از اضافه شدن"
                value={customerName}
                onChange={(e) => setCustomerName(e.target.value)}
              />
            </label>
            <label>
              <span>شماره تماس مشتری (اختیاری)</span>
              <input
                className="ltr-input"
                dir="ltr"
                placeholder="0912..."
                value={customerPhone}
                onChange={(e) => setCustomerPhone(e.target.value)}
              />
            </label>
            <button type="submit" className="primary-button" disabled={busy}>
              {busy ? 'در حال ثبت...' : '+ ثبت درخواست'}
            </button>
          </div>
        </form>
      ) : null}

      {/* Ranked Demand Summary */}
      <div className="panel">
        <div className="panel-head">
          <div>
            <h3>رتبه‌بندی پرتقاضاترین نیازها در بازه انتخابی</h3>
            <p>مشاهده دقیق اینکه چند نفر هر مورد را در این بازه زمانی درخواست کرده‌اند.</p>
          </div>
        </div>

        <div className="cd-filters">
          <input
            placeholder="جستجو در نام آیتم، مشتری یا یادداشت..."
            value={search}
            onChange={(e) => setSearch(e.target.value)}
          />
          <select value={categoryFilter} onChange={(e) => setCategoryFilter(e.target.value)}>
            <option value="">همه دسته‌بندی‌ها</option>
            {DEMAND_CATEGORIES.map((c) => (
              <option key={c.key} value={c.key}>
                {c.icon} {c.label}
              </option>
            ))}
          </select>
          <select value={statusFilter} onChange={(e) => setStatusFilter(e.target.value)}>
            <option value="">همه وضعیت‌ها</option>
            {DEMAND_STATUSES.map((s) => (
              <option key={s.key} value={s.key}>
                {s.label}
              </option>
            ))}
          </select>
        </div>

        {loading ? (
          <div className="cd-empty">در حال تحلیل درخواست‌ها...</div>
        ) : board.rankedSummary.length === 0 ? (
          <div className="cd-empty">در این بازه زمانی درخواستی ثبت نشده است.</div>
        ) : (
          <div className="cd-rank-list">
            {board.rankedSummary.map((row, idx) => {
              const cat = getCategoryMeta(row.category)
              const widthPct = Math.max(8, Math.round((row.totalRequests / maxRankCount) * 100))

              return (
                <div key={row.normalizedKey} className="cd-rank-row">
                  <div className="cd-rank-top">
                    <div className="cd-rank-left">
                      <span className={`cd-rank-num ${idx < 3 ? 'is-top' : ''}`}>
                        {toPersianNumber(idx + 1)}
                      </span>
                      <span className="cd-rank-name">{row.itemName}</span>
                      <span className="cd-cat-pill">
                        {cat.icon} {cat.label}
                      </span>
                    </div>

                    <div className="cd-rank-actions">
                      <span className="cd-count-badge">
                        {toPersianNumber(row.totalRequests)} درخواست ({toPersianNumber(row.percentOfTotal)}٪)
                      </span>
                      {canCreate ? (
                        <button
                          type="button"
                          className="ghost-button"
                          disabled={busy}
                          onClick={() => handleQuickPlusOne(row)}
                          title="ثبت فوری یک درخواست جدید دیگر برای همین مورد"
                        >
                          +۱ تقاضا
                        </button>
                      ) : null}
                      {canUpdate ? (
                        <select
                          aria-label={`وضعیت ${row.itemName}`}
                          value={row.status}
                          onChange={(e) => handleGroupStatusChange(row.normalizedKey, e.target.value)}
                        >
                          {DEMAND_STATUSES.map((s) => (
                            <option key={s.key} value={s.key}>
                              {s.label}
                            </option>
                          ))}
                        </select>
                      ) : null}
                    </div>
                  </div>

                  <div className="cd-bar-track">
                    <div className="cd-bar-fill" style={{ width: `${widthPct}%` }} />
                  </div>

                  <div className="cd-rank-meta">
                    <span>
                      تعداد دفعات ثبت: <strong>{toPersianNumber(row.entryCount)}</strong>
                      {row.uniqueCustomerPhones > 0
                        ? ` • ${toPersianNumber(row.uniqueCustomerPhones)} شماره مشتری ثبت‌شده`
                        : ''}
                    </span>
                    <span>آخرین درخواست: {formatDateTime(row.lastRequestedAt)}</span>
                  </div>
                </div>
              )
            })}
          </div>
        )}
      </div>

      {/* Detailed Log Table */}
      {board.entries.length > 0 ? (
        <div className="panel">
          <div className="panel-head">
            <div>
              <h3>ریز درخواست‌های ثبت‌شده در این بازه ({toPersianNumber(board.entries.length)})</h3>
            </div>
          </div>
          <div className="cd-table-wrap">
            <table className="cd-table">
              <thead>
                <tr>
                  <th>تاریخ و ساعت</th>
                  <th>محصول / نیاز درخواستی</th>
                  <th>تعداد</th>
                  <th>کانال</th>
                  <th>مشتری</th>
                  <th>توضیحات</th>
                  <th>ثبت‌کننده</th>
                  {canDelete ? <th>عملیات</th> : null}
                </tr>
              </thead>
              <tbody>
                {board.entries.map((entry) => (
                  <tr key={entry.id}>
                    <td>{formatDateTime(entry.requestedAt)}</td>
                    <td>
                      <strong>{entry.requestedItem}</strong>
                    </td>
                    <td>{toPersianNumber(entry.requestedCount)}</td>
                    <td>{getChannelLabel(entry.channel)}</td>
                    <td>
                      {entry.customerName || entry.customerPhone
                        ? `${entry.customerName || ''}${entry.customerName && entry.customerPhone ? ' — ' : ''}${entry.customerPhone || ''}`
                        : '—'}
                    </td>
                    <td>{entry.notes || '—'}</td>
                    <td>{entry.recordedByName || '—'}</td>
                    {canDelete ? (
                      <td>
                        <button
                          type="button"
                          className="ghost-button"
                          style={{ color: '#dc2626' }}
                          onClick={() => handleDeleteEntry(entry.id)}
                        >
                          حذف
                        </button>
                      </td>
                    ) : null}
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </div>
      ) : null}
    </section>
  )
}
