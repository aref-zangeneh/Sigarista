import { useCallback, useEffect, useMemo, useState } from 'react'
import { ShamsiDatePicker } from '../components/ShamsiDatePicker.jsx'
import { useAdmin } from '../context/AdminContext'
import { apiRequest, getReadableError } from '../utils/adminApi'
import { toPersianNumber } from '../utils/adminFormatters'
import { hasPermission, PERMISSIONS } from '../utils/adminPermissions'
import { toEnglishDigits as normalizeAsciiDigits } from '../utils/persianDigits'
import { gregorianIsoToShamsiLabel } from '../utils/shamsiDate'
import {
  addDaysToIsoDate,
  computeExpiryMeta,
  computeMarginPercent,
  filterQuickStockItems,
  isoTimestampToDateInput,
  toIsoDateOnly,
} from '../utils/quickStockHelpers.js'
import './MenuQuickStockPage.css'
import './MenuSectionMinimal.css'

const EXPIRY_PRESETS = [
  { days: 0, label: 'امروز' },
  { days: 1, label: '+۱ روز' },
  { days: 2, label: '+۲ روز' },
  { days: 3, label: '+۳ روز' },
  { days: 7, label: '+۷ روز' },
]

function parseOptionalMoney(raw) {
  const clean = normalizeAsciiDigits(String(raw ?? '')).replace(/[,٬\s]/g, '').trim()
  if (!clean) return null
  const num = Number(clean)
  return Number.isFinite(num) && num >= 0 ? num : null
}

function parseQty(raw) {
  const clean = normalizeAsciiDigits(String(raw ?? '')).replace(/[,٬\s]/g, '').trim()
  if (!clean) return 0
  const num = Number(clean)
  return Number.isFinite(num) && num >= 0 ? num : 0
}

function buildDraftForItem(item) {
  return {
    price: item.price > 0 ? String(Math.round(item.price)) : '',
    specialOfferPrice: item.specialOfferPrice != null ? String(Math.round(item.specialOfferPrice)) : '',
    purchasePrice: item.purchasePrice != null ? String(Math.round(item.purchasePrice)) : '',
    smallPrice: item.smallPrice != null ? String(Math.round(item.smallPrice)) : '',
    smallSpecialOfferPrice:
      item.smallSpecialOfferPrice != null ? String(Math.round(item.smallSpecialOfferPrice)) : '',
    mediumPrice: item.mediumPrice != null ? String(Math.round(item.mediumPrice)) : '',
    mediumSpecialOfferPrice:
      item.mediumSpecialOfferPrice != null ? String(Math.round(item.mediumSpecialOfferPrice)) : '',
    largePrice: item.largePrice != null ? String(Math.round(item.largePrice)) : '',
    largeSpecialOfferPrice:
      item.largeSpecialOfferPrice != null ? String(Math.round(item.largeSpecialOfferPrice)) : '',
    currentQuantity: item.hasStockRecord ? String(item.currentQuantity ?? 0) : '',
    minAlertQuantity: item.hasStockRecord ? String(item.minAlertQuantity ?? 0) : '0',
    isActive: Boolean(item.isActive),
    isOrderable: Boolean(item.isOrderable),
  }
}

function buildLotDraft(lot) {
  return {
    quantity: String(lot.remainingQuantity ?? 0),
    expiresAt: isoTimestampToDateInput(lot.expiresAt),
    receivedAt: isoTimestampToDateInput(lot.receivedAt),
    batchLabel: lot.batchLabel ?? '',
  }
}

function emptyNewLotForm() {
  return {
    quantity: '1',
    expiresAt: addDaysToIsoDate(2),
    receivedAt: toIsoDateOnly(new Date()),
    batchLabel: '',
  }
}

export function MenuQuickStockPage() {
  const { session, setError, setMessage } = useAdmin()
  const [board, setBoard] = useState(null)
  const [loading, setLoading] = useState(true)
  const [search, setSearch] = useState('')
  const [categoryId, setCategoryId] = useState('')
  const [statusFilter, setStatusFilter] = useState('all')

  const [drafts, setDrafts] = useState({})
  const [dirtyRows, setDirtyRows] = useState({})
  const [savingRows, setSavingRows] = useState({})
  const [savedFlash, setSavedFlash] = useState({})

  const [expandedLotsId, setExpandedLotsId] = useState(null)
  const [expandedSizesId, setExpandedSizesId] = useState(null)
  const [newLotForms, setNewLotForms] = useState({})
  const [lotEditDrafts, setLotEditDrafts] = useState({})
  const [lotBusyKey, setLotBusyKey] = useState('')

  const canUpdate = hasPermission(session, PERMISSIONS['menu-items'].update)

  const syncItemIntoBoard = useCallback((updatedItem) => {
    setBoard((prev) => {
      if (!prev) return prev
      const nextItems = prev.items.map((it) => (it.id === updatedItem.id ? updatedItem : it))

      let tracked = 0
      let outCount = 0
      let lowCount = 0
      let expiredLots = 0
      let soonLots = 0
      let activeLots = 0

      for (const it of nextItems) {
        if (it.hasStockRecord) {
          tracked++
          if (it.currentQuantity <= 0) outCount++
          else if (it.minAlertQuantity > 0 && it.currentQuantity <= it.minAlertQuantity) lowCount++
        }
        const lots = Array.isArray(it.lots) ? it.lots : []
        activeLots += lots.length
        for (const l of lots) {
          if (l.status === 'expired') expiredLots++
          else if (l.status === 'expiring_soon') soonLots++
        }
      }

      return {
        ...prev,
        summary: {
          totalItems: nextItems.length,
          trackedItems: tracked,
          outOfStockCount: outCount,
          lowStockCount: lowCount,
          expiredLotsCount: expiredLots,
          expiringSoonLotsCount: soonLots,
          totalActiveLots: activeLots,
        },
        items: nextItems,
      }
    })

    setDrafts((prev) => ({ ...prev, [updatedItem.id]: buildDraftForItem(updatedItem) }))
    setDirtyRows((prev) => ({ ...prev, [updatedItem.id]: false }))
    setLotEditDrafts((prev) => {
      const next = { ...prev }
      for (const lot of updatedItem.lots ?? []) {
        next[lot.id] = buildLotDraft(lot)
      }
      return next
    })
  }, [])

  const loadBoard = useCallback(async () => {
    if (!session?.token) return
    setLoading(true)
    setError('')
    try {
      const data = await apiRequest('/admin/menu-quick-stock', { token: session.token })
      setBoard(data)
      const initialDrafts = {}
      const initialLotDrafts = {}
      for (const item of data?.items ?? []) {
        initialDrafts[item.id] = buildDraftForItem(item)
        for (const lot of item.lots ?? []) {
          initialLotDrafts[lot.id] = buildLotDraft(lot)
        }
      }
      setDrafts(initialDrafts)
      setDirtyRows({})
      setLotEditDrafts(initialLotDrafts)
    } catch (err) {
      setError(getReadableError(err))
    } finally {
      setLoading(false)
    }
  }, [session?.token, setError])

  useEffect(() => {
    loadBoard()
  }, [loadBoard])

  const filteredItems = useMemo(
    () =>
      filterQuickStockItems(board?.items ?? [], {
        search,
        categoryId,
        statusFilter,
      }),
    [board?.items, search, categoryId, statusFilter],
  )

  const updateRowDraft = (itemId, patch) => {
    setDrafts((prev) => ({
      ...prev,
      [itemId]: { ...(prev[itemId] ?? {}), ...patch },
    }))
    setDirtyRows((prev) => ({ ...prev, [itemId]: true }))
  }

  const triggerSavedFlash = (itemId) => {
    setSavedFlash((prev) => ({ ...prev, [itemId]: true }))
    setTimeout(() => {
      setSavedFlash((prev) => ({ ...prev, [itemId]: false }))
    }, 1700)
  }

  const saveRow = async (item, overrideDraft = null) => {
    if (!session?.token || !canUpdate) return
    const d = overrideDraft ?? drafts[item.id]
    if (!d) return

    setSavingRows((prev) => ({ ...prev, [item.id]: true }))
    setError('')
    try {
      const hasQtyInput = String(d.currentQuantity ?? '').trim() !== ''
      const payload = {
        price: parseOptionalMoney(d.price) ?? 0,
        specialOfferPrice: parseOptionalMoney(d.specialOfferPrice),
        updateSpecialOfferPrice: true,
        purchasePrice: parseOptionalMoney(d.purchasePrice),
        updatePurchasePrice: true,
        updateSizePrices: Boolean(item.hasSizes),
        smallPrice: item.hasSizes ? parseOptionalMoney(d.smallPrice) : null,
        smallSpecialOfferPrice: item.hasSizes ? parseOptionalMoney(d.smallSpecialOfferPrice) : null,
        mediumPrice: item.hasSizes ? parseOptionalMoney(d.mediumPrice) : null,
        mediumSpecialOfferPrice: item.hasSizes ? parseOptionalMoney(d.mediumSpecialOfferPrice) : null,
        largePrice: item.hasSizes ? parseOptionalMoney(d.largePrice) : null,
        largeSpecialOfferPrice: item.hasSizes ? parseOptionalMoney(d.largeSpecialOfferPrice) : null,
        isActive: Boolean(d.isActive),
        isOrderable: Boolean(d.isOrderable),
        trackStock: hasQtyInput ? true : null,
        currentQuantity: hasQtyInput ? parseQty(d.currentQuantity) : null,
        minAlertQuantity: hasQtyInput ? parseQty(d.minAlertQuantity) : null,
      }

      const updated = await apiRequest(`/admin/menu-quick-stock/${item.id}/quick-update`, {
        method: 'PATCH',
        body: payload,
        token: session.token,
      })

      syncItemIntoBoard(updated)
      triggerSavedFlash(item.id)
    } catch (err) {
      setError(getReadableError(err))
    } finally {
      setSavingRows((prev) => ({ ...prev, [item.id]: false }))
    }
  }

  const handleQuickStepQty = async (item, delta) => {
    if (!canUpdate) return
    const current = parseQty(drafts[item.id]?.currentQuantity ?? item.currentQuantity)
    const nextQty = Math.max(0, current + delta)
    const nextDraft = {
      ...(drafts[item.id] ?? buildDraftForItem(item)),
      currentQuantity: String(nextQty),
    }
    setDrafts((prev) => ({ ...prev, [item.id]: nextDraft }))
    await saveRow(item, nextDraft)
  }

  const handleToggleActive = async (item) => {
    if (!canUpdate) return
    const nextDraft = {
      ...(drafts[item.id] ?? buildDraftForItem(item)),
      isActive: !item.isActive,
    }
    setDrafts((prev) => ({ ...prev, [item.id]: nextDraft }))
    await saveRow(item, nextDraft)
  }

  const openLotsPanel = (itemId) => {
    setExpandedLotsId((cur) => {
      const next = cur === itemId ? null : itemId
      if (next && !newLotForms[itemId]) {
        setNewLotForms((prev) => ({ ...prev, [itemId]: emptyNewLotForm() }))
      }
      return next
    })
  }

  const handleAddLot = async (item, event) => {
    event.preventDefault()
    if (!session?.token || !canUpdate) return
    const form = newLotForms[item.id] ?? emptyNewLotForm()
    const qty = parseQty(form.quantity)
    if (qty <= 0) {
      setError('تعداد پارت جدید باید بزرگ‌تر از صفر باشد.')
      return
    }

    const busyKey = `add-${item.id}`
    setLotBusyKey(busyKey)
    setError('')
    setMessage('')
    try {
      const payload = {
        quantity: qty,
        receivedAt: form.receivedAt ? `${form.receivedAt}T08:00:00Z` : null,
        expiresAt: form.expiresAt ? `${form.expiresAt}T00:00:00Z` : null,
        batchLabel: form.batchLabel?.trim() || null,
        preserveExistingStock: true,
      }
      const updated = await apiRequest(`/admin/menu-quick-stock/${item.id}/lots`, {
        method: 'POST',
        body: payload,
        token: session.token,
      })
      syncItemIntoBoard(updated)
      setNewLotForms((prev) => ({ ...prev, [item.id]: emptyNewLotForm() }))
      setMessage(`پارت جدید برای «${item.persianName}» ثبت شد و موجودی کل به‌روزرسانی گردید.`)
      triggerSavedFlash(item.id)
    } catch (err) {
      setError(getReadableError(err))
    } finally {
      setLotBusyKey('')
    }
  }

  const handleUpdateLot = async (item, lot) => {
    if (!session?.token || !canUpdate) return
    const draft = lotEditDrafts[lot.id]
    if (!draft) return

    const busyKey = `save-${lot.id}`
    setLotBusyKey(busyKey)
    setError('')
    try {
      const payload = {
        quantity: parseQty(draft.quantity),
        receivedAt: draft.receivedAt ? `${draft.receivedAt}T08:00:00Z` : null,
        expiresAt: draft.expiresAt ? `${draft.expiresAt}T00:00:00Z` : null,
        clearExpiry: !draft.expiresAt,
        batchLabel: draft.batchLabel?.trim() || null,
      }
      const updated = await apiRequest(`/admin/menu-quick-stock/${item.id}/lots/${lot.id}`, {
        method: 'PUT',
        body: payload,
        token: session.token,
      })
      syncItemIntoBoard(updated)
      triggerSavedFlash(item.id)
    } catch (err) {
      setError(getReadableError(err))
    } finally {
      setLotBusyKey('')
    }
  }

  const handleDiscardLot = async (item, lot) => {
    if (!session?.token || !canUpdate) return
    if (!window.confirm(`آیا پارت ${toPersianNumber(lot.remainingQuantity)} عددی «${item.persianName}» به عنوان ضایعات/منقضی ثبت و از موجودی کسر شود؟`)) {
      return
    }

    const busyKey = `discard-${lot.id}`
    setLotBusyKey(busyKey)
    setError('')
    try {
      const updated = await apiRequest(
        `/admin/menu-quick-stock/${item.id}/lots/${lot.id}/discard`,
        {
          method: 'POST',
          body: { reason: lot.status === 'expired' ? 'انقضای تاریخ مصرف' : 'ضایعات یخچال' },
          token: session.token,
        },
      )
      syncItemIntoBoard(updated)
      setMessage(`پارت از موجودی «${item.persianName}» کسر و به عنوان ضایعات ثبت شد.`)
    } catch (err) {
      setError(getReadableError(err))
    } finally {
      setLotBusyKey('')
    }
  }

  const handleDeleteLot = async (item, lot) => {
    if (!session?.token || !canUpdate) return
    if (!window.confirm(`این پارت از «${item.persianName}» حذف شود؟`)) return

    const busyKey = `del-${lot.id}`
    setLotBusyKey(busyKey)
    setError('')
    try {
      const updated = await apiRequest(`/admin/menu-quick-stock/${item.id}/lots/${lot.id}`, {
        method: 'DELETE',
        token: session.token,
      })
      syncItemIntoBoard(updated)
    } catch (err) {
      setError(getReadableError(err))
    } finally {
      setLotBusyKey('')
    }
  }

  const summary = board?.summary

  return (
    <div className="qs-shell menu-minimal-scope">
      {/* ── Minimal Header & Clickable KPI Filter Strip ── */}
      <section className="panel qs-top-panel">
        <div className="qs-head-row">
          <div>
            <h2>موجودی، تاریخ انقضا و قیمت سریع</h2>
            <p>
              کنترل آنی موجودی یخچال (کیک و دسر)، ثبت پارت‌های جداگانه با تاریخ انقضا (اولویت فروش خودکار FEFO) و ویرایش سریع قیمت‌ها بدون باز کردن صفحه هر آیتم.
            </p>
          </div>
          <button
            type="button"
            className="ghost-button qs-refresh-btn"
            disabled={loading}
            onClick={loadBoard}
          >
            {loading ? 'در حال بارگذاری...' : '↻ بروزرسانی'}
          </button>
        </div>

        {summary ? (
          <div className="qs-kpi-strip" role="tablist" aria-label="فیلترهای وضعیت سریع">
            <button
              type="button"
              className={`qs-kpi-pill ${statusFilter === 'all' ? 'is-active' : ''}`}
              onClick={() => setStatusFilter('all')}
            >
              <span>همه آیتم‌ها</span>
              <strong>{toPersianNumber(summary.totalItems)}</strong>
            </button>

            <button
              type="button"
              className={`qs-kpi-pill is-danger ${statusFilter === 'expired' ? 'is-active' : ''}`}
              onClick={() => setStatusFilter('expired')}
            >
              <span>منقضی‌شده</span>
              <strong>{toPersianNumber(summary.expiredLotsCount)}</strong>
            </button>

            <button
              type="button"
              className={`qs-kpi-pill is-warn ${statusFilter === 'expiring' ? 'is-active' : ''}`}
              onClick={() => setStatusFilter('expiring')}
            >
              <span>انقضا نزدیک (۴۸ ساعت)</span>
              <strong>{toPersianNumber(summary.expiringSoonLotsCount)}</strong>
            </button>

            <button
              type="button"
              className={`qs-kpi-pill ${statusFilter === 'out' ? 'is-active' : ''}`}
              onClick={() => setStatusFilter('out')}
            >
              <span>ناموجود</span>
              <strong>{toPersianNumber(summary.outOfStockCount)}</strong>
            </button>

            <button
              type="button"
              className={`qs-kpi-pill is-ok ${statusFilter === 'has-lots' ? 'is-active' : ''}`}
              onClick={() => setStatusFilter('has-lots')}
            >
              <span>پارت‌های فعال یخچال</span>
              <strong>{toPersianNumber(summary.totalActiveLots)}</strong>
            </button>
          </div>
        ) : null}

        {/* ── Search + Category Filter Pills ── */}
        <div className="qs-toolbar">
          <div className="qs-search-wrap">
            <input
              type="search"
              placeholder="جستجوی سریع نام آیتم (مثلاً تیرامیسو، چیزکیک)..."
              value={search}
              onChange={(e) => setSearch(e.target.value)}
            />
          </div>

          <div className="qs-cat-pills" role="navigation" aria-label="فیلتر دسته‌بندی">
            <button
              type="button"
              className={`qs-cat-chip ${categoryId === '' ? 'is-active' : ''}`}
              onClick={() => setCategoryId('')}
            >
              همه دسته‌ها
            </button>
            {(board?.categories ?? [])
              .filter((c) => c.itemCount > 0)
              .map((cat) => (
                <button
                  key={cat.id}
                  type="button"
                  className={`qs-cat-chip ${categoryId === cat.id ? 'is-active' : ''}`}
                  onClick={() => setCategoryId(cat.id)}
                >
                  <span>{cat.name}</span>
                  <small>{toPersianNumber(cat.itemCount)}</small>
                </button>
              ))}
          </div>
        </div>
      </section>

      {/* ── Minimal Inline Quick-Edit Table ── */}
      <section className="panel qs-list-panel">
        <div className="qs-table-head" aria-hidden="true">
          <span>آیتم و وضعیت انقضا</span>
          <span>موجودی و پارت‌ها</span>
          <span>قیمت فروش</span>
          <span>فروش ویژه</span>
          <span>قیمت خرید</span>
          <span>عملیات</span>
        </div>

        {loading && !board ? (
          <div className="qs-empty">در حال دریافت لیست آیتم‌ها و پارت‌های موجودی...</div>
        ) : filteredItems.length === 0 ? (
          <div className="qs-empty">آیتمی مطابق این فیلتر یافت نشد.</div>
        ) : (
          <div className="qs-rows">
            {filteredItems.map((item) => {
              const draft = drafts[item.id] ?? buildDraftForItem(item)
              const isDirty = Boolean(dirtyRows[item.id])
              const isSaving = Boolean(savingRows[item.id])
              const isFlashed = Boolean(savedFlash[item.id])
              const isLotsOpen = expandedLotsId === item.id
              const isSizesOpen = expandedSizesId === item.id
              const expiry = computeExpiryMeta(item.earliestExpiresAt)
              const margin = computeMarginPercent(
                parseOptionalMoney(draft.price),
                parseOptionalMoney(draft.purchasePrice),
              )
              const newLot = newLotForms[item.id] ?? emptyNewLotForm()

              const onRowKeyDown = (ev) => {
                if (ev.key === 'Enter' && canUpdate) {
                  ev.preventDefault()
                  saveRow(item)
                }
              }

              return (
                <div
                  key={item.id}
                  className={`qs-item-block ${!item.isActive ? 'is-inactive' : ''} ${
                    item.expiryStatus === 'expired' ? 'has-expired' : ''
                  }`}
                >
                  <div className="qs-row">
                    {/* Col 1: Item Identity + Expiry Badge */}
                    <div className="qs-cell-item">
                      {item.imageUrl ? (
                        <img className="qs-thumb" src={item.imageUrl} alt={item.persianName} />
                      ) : (
                        <span className="qs-thumb qs-thumb--placeholder" aria-hidden="true">
                          {item.persianName?.[0] || '☕'}
                        </span>
                      )}
                      <div className="qs-item-meta">
                        <div className="qs-item-title-line">
                          <strong>{item.persianName}</strong>
                          <button
                            type="button"
                            className={`qs-active-dot ${item.isActive ? 'is-on' : 'is-off'}`}
                            title={item.isActive ? 'فعال در منو — کلیک برای مخفی کردن' : 'غیرفعال — کلیک برای فعال کردن'}
                            disabled={!canUpdate || isSaving}
                            onClick={() => handleToggleActive(item)}
                          >
                            {item.isActive ? 'فعال' : 'مخفی'}
                          </button>
                        </div>
                        <div className="qs-item-sub">
                          <span className="qs-cat-tag">{item.categoryName}</span>
                          {expiry.status !== 'none' ? (
                            <span className={`qs-exp-badge is-${expiry.status}`}>
                              {expiry.label}
                              {expiry.shamsiDate ? ` · ${expiry.shamsiDate}` : ''}
                            </span>
                          ) : null}
                        </div>
                      </div>
                    </div>

                    {/* Col 2: Stock Stepper + Lots Drawer Trigger */}
                    <div className="qs-cell-stock">
                      <div className="qs-stepper">
                        <button
                          type="button"
                          className="qs-step-btn"
                          aria-label="کاهش موجودی"
                          disabled={!canUpdate || isSaving || parseQty(draft.currentQuantity) <= 0}
                          onClick={() => handleQuickStepQty(item, -1)}
                        >
                          −
                        </button>
                        <input
                          type="text"
                          inputMode="decimal"
                          className="qs-qty-input"
                          placeholder="∞"
                          aria-label={`موجودی ${item.persianName}`}
                          value={draft.currentQuantity}
                          disabled={!canUpdate}
                          onChange={(e) => updateRowDraft(item.id, { currentQuantity: e.target.value })}
                          onKeyDown={onRowKeyDown}
                        />
                        <button
                          type="button"
                          className="qs-step-btn"
                          aria-label="افزایش موجودی"
                          disabled={!canUpdate || isSaving}
                          onClick={() => handleQuickStepQty(item, 1)}
                        >
                          +
                        </button>
                      </div>

                      <button
                        type="button"
                        className={`qs-lots-trigger ${item.lots.length > 0 ? 'has-lots' : ''} ${
                          isLotsOpen ? 'is-open' : ''
                        }`}
                        onClick={() => openLotsPanel(item.id)}
                      >
                        {item.lots.length > 0
                          ? `${toPersianNumber(item.lots.length)} پارت انقضا`
                          : '+ پارت انقضا'}
                        <span aria-hidden="true">{isLotsOpen ? '▴' : '▾'}</span>
                      </button>
                    </div>

                    {/* Col 3: Sale Price */}
                    <div className="qs-cell-money">
                      <span className="qs-mobile-label">قیمت فروش</span>
                      <input
                        type="text"
                        inputMode="numeric"
                        className="qs-money-input"
                        placeholder="۰"
                        aria-label={`قیمت فروش ${item.persianName}`}
                        value={draft.price}
                        disabled={!canUpdate}
                        onChange={(e) => updateRowDraft(item.id, { price: e.target.value })}
                        onKeyDown={onRowKeyDown}
                      />
                    </div>

                    {/* Col 4: Special Offer Price */}
                    <div className="qs-cell-money">
                      <span className="qs-mobile-label">فروش ویژه</span>
                      <input
                        type="text"
                        inputMode="numeric"
                        className="qs-money-input is-special"
                        placeholder="—"
                        aria-label={`قیمت فروش ویژه ${item.persianName}`}
                        value={draft.specialOfferPrice}
                        disabled={!canUpdate}
                        onChange={(e) =>
                          updateRowDraft(item.id, { specialOfferPrice: e.target.value })
                        }
                        onKeyDown={onRowKeyDown}
                      />
                    </div>

                    {/* Col 5: Purchase Price + Margin */}
                    <div className="qs-cell-money">
                      <span className="qs-mobile-label">قیمت خرید</span>
                      <div className="qs-cost-wrap">
                        <input
                          type="text"
                          inputMode="numeric"
                          className="qs-money-input"
                          placeholder="—"
                          aria-label={`قیمت خرید ${item.persianName}`}
                          value={draft.purchasePrice}
                          disabled={!canUpdate}
                          onChange={(e) =>
                            updateRowDraft(item.id, { purchasePrice: e.target.value })
                          }
                          onKeyDown={onRowKeyDown}
                        />
                        {margin !== null ? (
                          <small className={`qs-margin-tag ${margin < 25 ? 'is-low' : ''}`}>
                            سود {toPersianNumber(margin)}٪
                          </small>
                        ) : null}
                      </div>
                    </div>

                    {/* Col 6: Save & Optional Sizes */}
                    <div className="qs-cell-actions">
                      {canUpdate ? (
                        <button
                          type="button"
                          className={`qs-save-btn ${isDirty ? 'is-dirty' : ''} ${
                            isFlashed ? 'is-saved' : ''
                          }`}
                          disabled={isSaving || (!isDirty && !isFlashed)}
                          onClick={() => saveRow(item)}
                        >
                          {isSaving ? '...' : isFlashed ? '✓ ثبت شد' : 'ذخیره'}
                        </button>
                      ) : null}
                      {item.hasSizes ? (
                        <button
                          type="button"
                          className="qs-sub-btn"
                          onClick={() => setExpandedSizesId((cur) => (cur === item.id ? null : item.id))}
                        >
                          سایزها {isSizesOpen ? '▴' : '▾'}
                        </button>
                      ) : null}
                    </div>
                  </div>

                  {/* ── Optional Inline Size Pricing Strip ── */}
                  {isSizesOpen && item.hasSizes ? (
                    <div className="qs-sizes-strip">
                      <div className="qs-size-box">
                        <span>{item.smallName || 'کوچک'}</span>
                        <input
                          type="text"
                          inputMode="numeric"
                          placeholder="قیمت"
                          value={draft.smallPrice}
                          onChange={(e) => updateRowDraft(item.id, { smallPrice: e.target.value })}
                          onKeyDown={onRowKeyDown}
                        />
                        <input
                          type="text"
                          inputMode="numeric"
                          placeholder="ویژه"
                          value={draft.smallSpecialOfferPrice}
                          onChange={(e) =>
                            updateRowDraft(item.id, { smallSpecialOfferPrice: e.target.value })
                          }
                          onKeyDown={onRowKeyDown}
                        />
                      </div>
                      <div className="qs-size-box">
                        <span>{item.mediumName || 'متوسط'}</span>
                        <input
                          type="text"
                          inputMode="numeric"
                          placeholder="قیمت"
                          value={draft.mediumPrice}
                          onChange={(e) => updateRowDraft(item.id, { mediumPrice: e.target.value })}
                          onKeyDown={onRowKeyDown}
                        />
                        <input
                          type="text"
                          inputMode="numeric"
                          placeholder="ویژه"
                          value={draft.mediumSpecialOfferPrice}
                          onChange={(e) =>
                            updateRowDraft(item.id, { mediumSpecialOfferPrice: e.target.value })
                          }
                          onKeyDown={onRowKeyDown}
                        />
                      </div>
                      <div className="qs-size-box">
                        <span>{item.largeName || 'بزرگ'}</span>
                        <input
                          type="text"
                          inputMode="numeric"
                          placeholder="قیمت"
                          value={draft.largePrice}
                          onChange={(e) => updateRowDraft(item.id, { largePrice: e.target.value })}
                          onKeyDown={onRowKeyDown}
                        />
                        <input
                          type="text"
                          inputMode="numeric"
                          placeholder="ویژه"
                          value={draft.largeSpecialOfferPrice}
                          onChange={(e) =>
                            updateRowDraft(item.id, { largeSpecialOfferPrice: e.target.value })
                          }
                          onKeyDown={onRowKeyDown}
                        />
                      </div>
                    </div>
                  ) : null}

                  {/* ── Inline FEFO Fridge Lots & Expiry Drawer ── */}
                  {isLotsOpen ? (
                    <div className="qs-lots-drawer">
                      <div className="qs-lots-header">
                        <div>
                          <strong>پارت‌های موجودی و تاریخ انقضا ({item.persianName})</strong>
                          <small>
                            هنگام ثبت فروش در صندوق یا سایت، سیستم به‌صورت خودکار ابتدا از پارت با نزدیک‌ترین تاریخ انقضا (FEFO) کسر می‌کند.
                          </small>
                        </div>
                        <span className="qs-lots-total">
                          جمع کل: {toPersianNumber(item.currentQuantity)} عدد
                        </span>
                      </div>

                      {/* Quick-Add New Lot Bar */}
                      {canUpdate ? (
                        <form className="qs-add-lot-bar" onSubmit={(ev) => handleAddLot(item, ev)}>
                          <label className="qs-lot-field qs-lot-field--qty">
                            <span>تعداد ورود</span>
                            <input
                              type="text"
                              inputMode="decimal"
                              value={newLot.quantity}
                              onChange={(e) =>
                                setNewLotForms((prev) => ({
                                  ...prev,
                                  [item.id]: { ...newLot, quantity: e.target.value },
                                }))
                              }
                            />
                          </label>

                          <div className="qs-lot-field qs-lot-field--date">
                            <span>تاریخ انقضا (شمسی)</span>
                            <ShamsiDatePicker
                              value={newLot.expiresAt}
                              onChange={(iso) =>
                                setNewLotForms((prev) => ({
                                  ...prev,
                                  [item.id]: { ...newLot, expiresAt: iso },
                                }))
                              }
                              placeholder="تاریخ انقضا"
                              showHint={false}
                            />
                            <div className="qs-expiry-presets">
                              {EXPIRY_PRESETS.map((p) => (
                                <button
                                  key={p.days}
                                  type="button"
                                  className="qs-preset-btn"
                                  onClick={() =>
                                    setNewLotForms((prev) => ({
                                      ...prev,
                                      [item.id]: {
                                        ...newLot,
                                        expiresAt: addDaysToIsoDate(p.days),
                                      },
                                    }))
                                  }
                                >
                                  {p.label}
                                </button>
                              ))}
                              <button
                                type="button"
                                className="qs-preset-btn"
                                onClick={() =>
                                  setNewLotForms((prev) => ({
                                    ...prev,
                                    [item.id]: { ...newLot, expiresAt: '' },
                                  }))
                                }
                              >
                                بدون انقضا
                              </button>
                            </div>
                          </div>

                          <label className="qs-lot-field qs-lot-field--note">
                            <span>برچسب / یادداشت پارت (اختیاری)</span>
                            <input
                              type="text"
                              maxLength={120}
                              placeholder="مثلاً: پخت امروز صبح / یخچال ۱"
                              value={newLot.batchLabel}
                              onChange={(e) =>
                                setNewLotForms((prev) => ({
                                  ...prev,
                                  [item.id]: { ...newLot, batchLabel: e.target.value },
                                }))
                              }
                            />
                          </label>

                          <button
                            type="submit"
                            className="qs-add-lot-btn"
                            disabled={lotBusyKey === `add-${item.id}`}
                          >
                            {lotBusyKey === `add-${item.id}` ? 'در حال ثبت...' : '+ ثبت پارت جدید'}
                          </button>
                        </form>
                      ) : null}

                      {/* Active Lots List (FEFO Ordered) */}
                      {item.lots.length === 0 ? (
                        <div className="qs-lots-empty">
                          هنوز پارتی با تاریخ انقضا برای این آیتم ثبت نشده است. از نوار بالا اولین پارت را اضافه کنید.
                        </div>
                      ) : (
                        <div className="qs-lots-list">
                          {item.lots.map((lot, idx) => {
                            const lDraft = lotEditDrafts[lot.id] ?? buildLotDraft(lot)
                            const lExpiry = computeExpiryMeta(lot.expiresAt)
                            const receivedShamsi = lot.receivedAt
                              ? gregorianIsoToShamsiLabel(isoTimestampToDateInput(lot.receivedAt))
                              : '—'

                            return (
                              <div key={lot.id} className={`qs-lot-row is-${lExpiry.status}`}>
                                <div className="qs-lot-rank">
                                  <span className="qs-rank-num">#{toPersianNumber(idx + 1)}</span>
                                  {idx === 0 ? (
                                    <span className="qs-fefo-pill">اولویت اول فروش</span>
                                  ) : null}
                                  <small>ورود: {receivedShamsi}</small>
                                </div>

                                <div className="qs-lot-edit-qty">
                                  <span>باقیمانده</span>
                                  <input
                                    type="text"
                                    inputMode="decimal"
                                    value={lDraft.quantity}
                                    disabled={!canUpdate}
                                    onChange={(e) =>
                                      setLotEditDrafts((prev) => ({
                                        ...prev,
                                        [lot.id]: { ...lDraft, quantity: e.target.value },
                                      }))
                                    }
                                  />
                                </div>

                                <div className="qs-lot-edit-date">
                                  <span>
                                    انقضا:{' '}
                                    <b className={`qs-exp-inline is-${lExpiry.status}`}>
                                      {lExpiry.label}
                                    </b>
                                  </span>
                                  <ShamsiDatePicker
                                    value={lDraft.expiresAt}
                                    onChange={(iso) =>
                                      setLotEditDrafts((prev) => ({
                                        ...prev,
                                        [lot.id]: { ...lDraft, expiresAt: iso },
                                      }))
                                    }
                                    placeholder="بدون تاریخ انقضا"
                                    showHint={false}
                                  />
                                </div>

                                <div className="qs-lot-edit-label">
                                  <span>توضیح پارت</span>
                                  <input
                                    type="text"
                                    maxLength={120}
                                    placeholder="توضیح کوتاه..."
                                    value={lDraft.batchLabel}
                                    disabled={!canUpdate}
                                    onChange={(e) =>
                                      setLotEditDrafts((prev) => ({
                                        ...prev,
                                        [lot.id]: { ...lDraft, batchLabel: e.target.value },
                                      }))
                                    }
                                  />
                                </div>

                                {canUpdate ? (
                                  <div className="qs-lot-actions">
                                    <button
                                      type="button"
                                      className="qs-lot-save-btn"
                                      disabled={lotBusyKey === `save-${lot.id}`}
                                      onClick={() => handleUpdateLot(item, lot)}
                                    >
                                      {lotBusyKey === `save-${lot.id}` ? '...' : 'ذخیره'}
                                    </button>
                                    <button
                                      type="button"
                                      className="qs-lot-discard-btn"
                                      disabled={lotBusyKey === `discard-${lot.id}`}
                                      onClick={() => handleDiscardLot(item, lot)}
                                    >
                                      ضایعات
                                    </button>
                                    <button
                                      type="button"
                                      className="qs-lot-del-btn"
                                      disabled={lotBusyKey === `del-${lot.id}`}
                                      onClick={() => handleDeleteLot(item, lot)}
                                    >
                                      حذف
                                    </button>
                                  </div>
                                ) : null}
                              </div>
                            )
                          })}
                        </div>
                      )}
                    </div>
                  ) : null}
                </div>
              )
            })}
          </div>
        )}
      </section>
    </div>
  )
}
