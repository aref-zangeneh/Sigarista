import { useCallback, useEffect, useMemo, useState } from 'react'
import { useSearchParams } from 'react-router-dom'
import { useAdmin } from '../context/AdminContext'
import { hasAnyPermission, PERMISSIONS } from '../utils/adminPermissions'
import { ShamsiDatePicker } from '../components/ShamsiDatePicker'
import { useEscapeKey } from '../hooks/useEscapeKey'
import {
  computeVoucherLinePreview,
  formatVoucherMoney,
  formatVoucherQty,
  getReasonsForVoucherType,
  MAIN_WAREHOUSE_KEY,
  printWarehouseVoucher,
  toFaDigits,
} from '../utils/warehouseVoucherPrint.js'
import './WarehouseVouchersPage.css'

function todayIsoDate() {
  const now = new Date()
  const y = now.getFullYear()
  const m = String(now.getMonth() + 1).padStart(2, '0')
  const d = String(now.getDate()).padStart(2, '0')
  return `${y}-${m}-${d}`
}

function emptyLine() {
  return {
    key: `${Date.now()}-${Math.random().toString(36).slice(2, 8)}`,
    ingredientId: '',
    quantity: '',
    unitPrice: '',
    batchNumber: '',
    expirationDate: '',
    lineNote: '',
  }
}

function createInitialForm(voucherType = 'Receipt', warehouseKey = MAIN_WAREHOUSE_KEY) {
  const reasons = getReasonsForVoucherType(voucherType)
  return {
    voucherType,
    reason: reasons[0]?.value ?? (voucherType === 'Issue' ? 'KitchenConsumption' : 'Purchase'),
    warehouseKey: warehouseKey || MAIN_WAREHOUSE_KEY,
    supplierId: '',
    counterpartyName: '',
    externalReference: '',
    voucherDate: todayIsoDate(),
    note: '',
    lines: [emptyLine()],
  }
}

export function WarehouseVouchersPage() {
  const {
    session,
    siteSettings,
    apiRequest,
    getReadableError,
    formatDateTime,
    toPersianNumber,
  } = useAdmin()
  const [searchParams, setSearchParams] = useSearchParams()

  const canCreate = hasAnyPermission(session, [
    PERMISSIONS.warehouses.update,
    PERMISSIONS.inventory.create,
    PERMISSIONS.inventory.update,
  ])
  const canCancel = hasAnyPermission(session, [
    PERMISSIONS.warehouses.update,
    PERMISSIONS.inventory.update,
  ])

  const [warehouses, setWarehouses] = useState([])
  const [ingredients, setIngredients] = useState([])
  const [suppliers, setSuppliers] = useState([])

  // List & filter state
  const [typeFilter, setTypeFilter] = useState('') // '' | 'Receipt' | 'Issue'
  const [statusFilter, setStatusFilter] = useState('') // '' | 'Posted' | 'Cancelled'
  const [warehouseFilter, setWarehouseFilter] = useState('ALL')
  const [search, setSearch] = useState('')
  const [page, setPage] = useState(1)
  const pageSize = 25

  const [listData, setListData] = useState({
    summary: {
      postedReceiptsCount: 0,
      postedReceiptsAmount: 0,
      postedIssuesCount: 0,
      postedIssuesAmount: 0,
      cancelledCount: 0,
    },
    items: [],
    totalCount: 0,
  })
  const [loading, setLoading] = useState(true)
  const [pageError, setPageError] = useState('')
  const [pageMessage, setPageMessage] = useState('')

  // Create voucher modal state
  const [createOpen, setCreateOpen] = useState(false)
  const [form, setForm] = useState(() => createInitialForm('Receipt'))
  const [warehouseStockMap, setWarehouseStockMap] = useState(() => new Map())
  const [stockLoading, setStockLoading] = useState(false)
  const [submitting, setSubmitting] = useState(false)
  const [formError, setFormError] = useState('')

  // Detail / print preview modal state
  const [detailVoucher, setDetailVoucher] = useState(null)
  const [detailLoading, setDetailLoading] = useState(false)

  // Cancel modal state
  const [cancelTarget, setCancelTarget] = useState(null)
  const [cancelReason, setCancelReason] = useState('')
  const [cancelling, setCancelling] = useState(false)
  const [cancelError, setCancelError] = useState('')

  useEscapeKey(Boolean(cancelTarget), () => setCancelTarget(null))
  useEscapeKey(Boolean(detailVoucher && !cancelTarget), () => setDetailVoucher(null))
  useEscapeKey(Boolean(createOpen && !detailVoucher && !cancelTarget), () => setCreateOpen(false))

  // Load reference data (warehouses, ingredients, suppliers)
  useEffect(() => {
    if (!session?.token) return
    let cancelled = false
    Promise.all([
      apiRequest('/admin/warehouses', { token: session.token }).catch(() => []),
      apiRequest('/admin/ingredients/options', { token: session.token }).catch(() => []),
      apiRequest('/admin/suppliers?page=1&pageSize=200', { token: session.token }).catch(() => []),
    ]).then(([whs, ings, sups]) => {
      if (cancelled) return
      setWarehouses(Array.isArray(whs) ? whs : [])
      setIngredients(Array.isArray(ings) ? ings : [])
      const supplierItems = Array.isArray(sups?.items) ? sups.items : Array.isArray(sups) ? sups : []
      setSuppliers(supplierItems)
    })
    return () => {
      cancelled = true
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [session?.token])

  const loadVouchers = useCallback(async () => {
    if (!session?.token) return
    setLoading(true)
    setPageError('')
    try {
      const params = new URLSearchParams({
        page: String(page),
        pageSize: String(pageSize),
      })
      if (typeFilter) params.set('type', typeFilter)
      if (statusFilter) params.set('status', statusFilter)
      if (warehouseFilter === MAIN_WAREHOUSE_KEY) {
        params.set('mainOnly', 'true')
      } else if (warehouseFilter && warehouseFilter !== 'ALL') {
        params.set('warehouseId', warehouseFilter)
      }
      if (search.trim()) params.set('search', search.trim())

      const data = await apiRequest(`/admin/warehouse-vouchers?${params.toString()}`, {
        token: session.token,
      })
      setListData({
        summary: data?.summary ?? {
          postedReceiptsCount: 0,
          postedReceiptsAmount: 0,
          postedIssuesCount: 0,
          postedIssuesAmount: 0,
          cancelledCount: 0,
        },
        items: Array.isArray(data?.items) ? data.items : [],
        totalCount: Number(data?.totalCount ?? 0),
      })
    } catch (requestError) {
      setPageError(getReadableError(requestError))
    } finally {
      setLoading(false)
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [session?.token, page, typeFilter, statusFilter, warehouseFilter, search])

  useEffect(() => {
    loadVouchers()
  }, [loadVouchers])

  // Support deep-link shortcuts like /admin/warehouse-vouchers?new=Receipt or ?new=Issue
  useEffect(() => {
    const newType = searchParams.get('new')
    if (canCreate && (newType === 'Receipt' || newType === 'Issue')) {
      const whParam = searchParams.get('warehouse') || MAIN_WAREHOUSE_KEY
      setForm(createInitialForm(newType, whParam))
      setFormError('')
      setCreateOpen(true)
      setSearchParams({}, { replace: true })
    }
  }, [canCreate, searchParams, setSearchParams])

  // Load live stock for the selected warehouse whenever Create modal is open
  useEffect(() => {
    if (!createOpen || !session?.token) return
    let cancelled = false
    setStockLoading(true)
    const path =
      form.warehouseKey === MAIN_WAREHOUSE_KEY
        ? '/admin/warehouses/main-stock'
        : `/admin/warehouses/${form.warehouseKey}/stock`

    apiRequest(path, { token: session.token })
      .then((data) => {
        if (cancelled) return
        const map = new Map()
        for (const row of data?.items ?? []) {
          map.set(row.ingredientId, row)
        }
        setWarehouseStockMap(map)
      })
      .catch(() => {
        if (!cancelled) setWarehouseStockMap(new Map())
      })
      .finally(() => {
        if (!cancelled) setStockLoading(false)
      })

    return () => {
      cancelled = true
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [createOpen, form.warehouseKey, session?.token])

  const activeWarehouses = useMemo(
    () => warehouses.filter((w) => w.isActive),
    [warehouses],
  )

  const ingredientsById = useMemo(() => {
    const map = new Map()
    ingredients.forEach((ing) => map.set(ing.id, ing))
    return map
  }, [ingredients])

  const reasonOptions = useMemo(
    () => getReasonsForVoucherType(form.voucherType),
    [form.voucherType],
  )

  // Live line previews & grand total
  const linePreviews = useMemo(
    () =>
      form.lines.map((line) => {
        const stockRow = warehouseStockMap.get(line.ingredientId)
        const currentStock = stockRow ? Number(stockRow.quantity) : 0
        const preview = computeVoucherLinePreview(
          form.voucherType,
          currentStock,
          line.quantity,
          line.unitPrice,
        )
        return { ...preview, currentStock }
      }),
    [form.lines, form.voucherType, warehouseStockMap],
  )

  const formGrandTotal = useMemo(
    () => linePreviews.reduce((sum, p) => sum + (p.lineTotal || 0), 0),
    [linePreviews],
  )

  const hasInsufficientLine = useMemo(
    () => linePreviews.some((p) => p.insufficient),
    [linePreviews],
  )

  function openCreateModal(voucherType) {
    const defaultWh =
      warehouseFilter && warehouseFilter !== 'ALL' ? warehouseFilter : MAIN_WAREHOUSE_KEY
    setForm(createInitialForm(voucherType, defaultWh))
    setFormError('')
    setCreateOpen(true)
  }

  function updateLine(key, patch) {
    setForm((current) => ({
      ...current,
      lines: current.lines.map((line) => (line.key === key ? { ...line, ...patch } : line)),
    }))
  }

  function handleIngredientChange(lineKey, ingredientId) {
    const ing = ingredientsById.get(ingredientId)
    const defaultCost = ing?.unitCost > 0 ? String(ing.unitCost) : ''
    updateLine(lineKey, {
      ingredientId,
      unitPrice: defaultCost,
    })
  }

  function addLine() {
    setForm((current) => ({
      ...current,
      lines: [...current.lines, emptyLine()],
    }))
  }

  function removeLine(key) {
    setForm((current) => ({
      ...current,
      lines: current.lines.length > 1 ? current.lines.filter((l) => l.key !== key) : current.lines,
    }))
  }

  async function submitCreate(event) {
    event.preventDefault()
    if (!canCreate) return
    setFormError('')

    const preparedLines = []
    for (let idx = 0; idx < form.lines.length; idx++) {
      const line = form.lines[idx]
      const preview = linePreviews[idx]
      if (!line.ingredientId) {
        setFormError(`در ردیف ${toFaDigits(idx + 1)} ماده اولیه انتخاب نشده است.`)
        return
      }
      if (preview.qty <= 0) {
        setFormError(`در ردیف ${toFaDigits(idx + 1)} مقدار باید بزرگ‌تر از صفر باشد.`)
        return
      }
      if (preview.insufficient) {
        const ingName = ingredientsById.get(line.ingredientId)?.persianName ?? 'کالا'
        setFormError(`موجودی «${ingName}» برای خروج در این انبار کافی نیست.`)
        return
      }
      preparedLines.push({
        ingredientId: line.ingredientId,
        quantity: preview.qty,
        unitPrice: preview.unitPrice,
        batchNumber: line.batchNumber?.trim() || null,
        expirationDate: line.expirationDate || null,
        lineNote: line.lineNote?.trim() || null,
      })
    }

    setSubmitting(true)
    try {
      const created = await apiRequest('/admin/warehouse-vouchers', {
        method: 'POST',
        token: session.token,
        body: {
          voucherType: form.voucherType,
          reason: form.reason,
          warehouseId: form.warehouseKey === MAIN_WAREHOUSE_KEY ? null : form.warehouseKey,
          supplierId: form.supplierId || null,
          counterpartyName: form.counterpartyName?.trim() || null,
          externalReference: form.externalReference?.trim() || null,
          voucherDate: form.voucherDate ? `${form.voucherDate}T12:00:00+03:30` : null,
          note: form.note?.trim() || null,
          lines: preparedLines,
        },
      })

      setCreateOpen(false)
      setPageMessage(
        `${created.voucherTypeLabel} شماره ${created.voucherNumber} با موفقیت ثبت و موجودی انبار به‌روزرسانی شد.`,
      )
      setDetailVoucher(created)
      await loadVouchers()
    } catch (requestError) {
      setFormError(getReadableError(requestError))
    } finally {
      setSubmitting(false)
    }
  }

  async function openVoucherDetail(voucherId) {
    setDetailLoading(true)
    setPageError('')
    try {
      const data = await apiRequest(`/admin/warehouse-vouchers/${voucherId}`, {
        token: session.token,
      })
      setDetailVoucher(data)
    } catch (requestError) {
      setPageError(getReadableError(requestError))
    } finally {
      setDetailLoading(false)
    }
  }

  async function submitCancelVoucher(event) {
    event.preventDefault()
    if (!cancelTarget || !canCancel) return
    if (!cancelReason.trim()) {
      setCancelError('ثبت علت ابطال سند الزامی است.')
      return
    }

    setCancelling(true)
    setCancelError('')
    try {
      const updated = await apiRequest(`/admin/warehouse-vouchers/${cancelTarget.id}/cancel`, {
        method: 'POST',
        token: session.token,
        body: { reason: cancelReason.trim() },
      })
      setCancelTarget(null)
      setCancelReason('')
      setPageMessage(`سند ${updated.voucherNumber} ابطال شد و موجودی اقلام به‌صورت خودکار برگشت خورد.`)
      if (detailVoucher?.id === updated.id) {
        setDetailVoucher(updated)
      }
      await loadVouchers()
    } catch (requestError) {
      setCancelError(getReadableError(requestError))
    } finally {
      setCancelling(false)
    }
  }

  const totalPages = Math.max(1, Math.ceil((listData.totalCount || 0) / pageSize))
  const { summary } = listData

  return (
    <section className="panel-stack">
      <div className="panel">
        <div className="panel-head">
          <div>
            <h2>رسید ورود و سند خروج انبار</h2>
            <p className="muted">
              صدور رسمی رسید ورود کالا (GRN) و سند / حواله خروج از انبار (GIV) با ثبت دقیق کاردکس موجودی قبل و بعد، چاپ رسمی A4 و قابلیت ابطال امن.
            </p>
          </div>
          <div className="vouchers-action-bar">
            {canCreate ? (
              <>
                <button
                  type="button"
                  className="primary-button voucher-btn-receipt"
                  onClick={() => openCreateModal('Receipt')}
                >
                  📥 ثبت رسید ورود به انبار
                </button>
                <button
                  type="button"
                  className="primary-button voucher-btn-issue"
                  onClick={() => openCreateModal('Issue')}
                >
                  📤 ثبت سند خروج از انبار
                </button>
              </>
            ) : null}
            <button type="button" className="ghost-button" onClick={loadVouchers} disabled={loading}>
              بروزرسانی
            </button>
          </div>
        </div>

        {pageError ? <p className="feedback feedback-error">{pageError}</p> : null}
        {pageMessage ? <p className="feedback feedback-success">{pageMessage}</p> : null}

        <div className="vouchers-kpi-grid">
          <div className="voucher-kpi-card voucher-kpi-card--receipt">
            <span className="voucher-kpi-label">رسیدهای ورود قطعی</span>
            <strong className="voucher-kpi-value">{toPersianNumber(summary.postedReceiptsCount)} سند</strong>
            <span className="voucher-kpi-sub">
              جمع ریالی: {formatVoucherMoney(summary.postedReceiptsAmount)} تومان
            </span>
          </div>
          <div className="voucher-kpi-card voucher-kpi-card--issue">
            <span className="voucher-kpi-label">اسناد خروج قطعی</span>
            <strong className="voucher-kpi-value">{toPersianNumber(summary.postedIssuesCount)} سند</strong>
            <span className="voucher-kpi-sub">
              جمع ریالی: {formatVoucherMoney(summary.postedIssuesAmount)} تومان
            </span>
          </div>
          <div className="voucher-kpi-card">
            <span className="voucher-kpi-label">تراز ریالی اسناد (ورود − خروج)</span>
            <strong className="voucher-kpi-value">
              {formatVoucherMoney((summary.postedReceiptsAmount || 0) - (summary.postedIssuesAmount || 0))} ت
            </strong>
            <span className="voucher-kpi-sub">بر اساس اسناد ثبت قطعی</span>
          </div>
          <div className="voucher-kpi-card voucher-kpi-card--muted">
            <span className="voucher-kpi-label">اسناد ابطال‌شده</span>
            <strong className="voucher-kpi-value">{toPersianNumber(summary.cancelledCount)} سند</strong>
            <span className="voucher-kpi-sub">با برگشت کامل موجودی</span>
          </div>
        </div>

        <div className="vouchers-filters">
          <div className="vouchers-type-tabs" role="group" aria-label="فیلتر نوع سند">
            <button
              type="button"
              className={typeFilter === '' ? 'active' : ''}
              onClick={() => {
                setTypeFilter('')
                setPage(1)
              }}
            >
              همه اسناد
            </button>
            <button
              type="button"
              className={typeFilter === 'Receipt' ? 'active' : ''}
              onClick={() => {
                setTypeFilter('Receipt')
                setPage(1)
              }}
            >
              📥 رسید ورود
            </button>
            <button
              type="button"
              className={typeFilter === 'Issue' ? 'active' : ''}
              onClick={() => {
                setTypeFilter('Issue')
                setPage(1)
              }}
            >
              📤 سند خروج
            </button>
          </div>

          <select
            className="vouchers-filter-select"
            value={warehouseFilter}
            onChange={(event) => {
              setWarehouseFilter(event.target.value)
              setPage(1)
            }}
          >
            <option value="ALL">همه انبارها</option>
            <option value={MAIN_WAREHOUSE_KEY}>انبار اصلی (موجودی مرکزی)</option>
            {warehouses.map((w) => (
              <option key={w.id} value={w.id}>
                {w.isSubWarehouse ? `↳ ${w.name}` : w.name}
              </option>
            ))}
          </select>

          <select
            className="vouchers-filter-select"
            value={statusFilter}
            onChange={(event) => {
              setStatusFilter(event.target.value)
              setPage(1)
            }}
          >
            <option value="">همه وضعیت‌ها</option>
            <option value="Posted">ثبت قطعی</option>
            <option value="Cancelled">ابطال‌شده</option>
          </select>

          <input
            type="search"
            className="vouchers-filter-search"
            placeholder="جستجو بر اساس شماره سند، نام کالا، طرف حساب، شماره فاکتور..."
            value={search}
            onChange={(event) => {
              setSearch(event.target.value)
              setPage(1)
            }}
          />
        </div>

        {loading ? (
          <p className="feedback">در حال بارگذاری اسناد انبار...</p>
        ) : listData.items.length === 0 ? (
          <p className="feedback">هنوز رسید ورود یا سند خروجی با این فیلترها ثبت نشده است.</p>
        ) : (
          <>
            <div className="table-scroll">
              <table className="admin-table">
                <thead>
                  <tr>
                    <th>شماره سند</th>
                    <th>نوع</th>
                    <th>انبار</th>
                    <th>بابت / دلیل</th>
                    <th>طرف حساب / تأمین‌کننده</th>
                    <th>اقلام</th>
                    <th>مبلغ کل</th>
                    <th>تاریخ سند</th>
                    <th>وضعیت</th>
                    <th>عملیات</th>
                  </tr>
                </thead>
                <tbody>
                  {listData.items.map((voucher) => {
                    const isReceipt = voucher.voucherType === 'Receipt'
                    const isCancelled = voucher.status === 'Cancelled'
                    return (
                      <tr key={voucher.id} className={isCancelled ? 'voucher-row-cancelled' : ''}>
                        <td>
                          <strong className="ltr-text">{voucher.voucherNumber}</strong>
                          {voucher.externalReference ? (
                            <div className="table-subtext ltr-text">مرجع: {voucher.externalReference}</div>
                          ) : null}
                        </td>
                        <td>
                          <span className={`voucher-badge ${isReceipt ? 'voucher-badge--receipt' : 'voucher-badge--issue'}`}>
                            {isReceipt ? '📥 رسید ورود' : '📤 سند خروج'}
                          </span>
                        </td>
                        <td>{voucher.warehouseName}</td>
                        <td>
                          <strong>{voucher.reasonLabel}</strong>
                          {voucher.createdByName ? (
                            <div className="table-subtext">ثبت: {voucher.createdByName}</div>
                          ) : null}
                        </td>
                        <td>
                          {voucher.supplierName ? <div>{voucher.supplierName}</div> : null}
                          {voucher.counterpartyName ? (
                            <div className="table-subtext">{voucher.counterpartyName}</div>
                          ) : !voucher.supplierName ? (
                            '—'
                          ) : null}
                        </td>
                        <td>{toPersianNumber(voucher.lineCount)} قلم</td>
                        <td>
                          <strong>{formatVoucherMoney(voucher.totalAmount)} تومان</strong>
                        </td>
                        <td>{formatDateTime(voucher.voucherDate)}</td>
                        <td>
                          <span className={`voucher-badge ${isCancelled ? 'voucher-badge--cancelled' : 'voucher-badge--posted'}`}>
                            {isCancelled ? 'ابطال‌شده' : 'ثبت قطعی'}
                          </span>
                        </td>
                        <td>
                          <div className="table-actions">
                            <button
                              type="button"
                              className="link-button"
                              disabled={detailLoading}
                              onClick={() => openVoucherDetail(voucher.id)}
                            >
                              مشاهده و چاپ
                            </button>
                            {canCancel && !isCancelled ? (
                              <button
                                type="button"
                                className="link-button link-danger"
                                onClick={() => {
                                  setCancelTarget(voucher)
                                  setCancelReason('')
                                  setCancelError('')
                                }}
                              >
                                ابطال سند
                              </button>
                            ) : null}
                          </div>
                        </td>
                      </tr>
                    )
                  })}
                </tbody>
              </table>
            </div>

            {totalPages > 1 ? (
              <div className="voucher-pagination">
                <span>
                  نمایش صفحه {toPersianNumber(page)} از {toPersianNumber(totalPages)} ({toPersianNumber(listData.totalCount)} سند)
                </span>
                <div className="table-actions">
                  <button
                    type="button"
                    className="ghost-button"
                    disabled={page <= 1}
                    onClick={() => setPage((p) => Math.max(1, p - 1))}
                  >
                    صفحه قبل
                  </button>
                  <button
                    type="button"
                    className="ghost-button"
                    disabled={page >= totalPages}
                    onClick={() => setPage((p) => Math.min(totalPages, p + 1))}
                  >
                    صفحه بعد
                  </button>
                </div>
              </div>
            ) : null}
          </>
        )}
      </div>

      {/* ── مودال ثبت رسید ورود / سند خروج جدید ─────────────────── */}
      {createOpen ? (
        <div className="modal-backdrop" onClick={() => setCreateOpen(false)}>
          <div
            className="modal-card voucher-modal-card"
            role="dialog"
            aria-modal="true"
            aria-labelledby="create-voucher-title"
            onClick={(event) => event.stopPropagation()}
          >
            <div
              className={`voucher-modal-banner ${
                form.voucherType === 'Receipt'
                  ? 'voucher-modal-banner--receipt'
                  : 'voucher-modal-banner--issue'
              }`}
            >
              <div>
                <h3 id="create-voucher-title">
                  {form.voucherType === 'Receipt'
                    ? '📥 صدور رسید ورود کالا به انبار (GRN)'
                    : '📤 صدور سند / حواله خروج کالا از انبار (GIV)'}
                </h3>
                <p>
                  {form.voucherType === 'Receipt'
                    ? 'مقادیر ثبت‌شده بلافاصله به موجودی انبار انتخابی اضافه و در کاردکس ثبت می‌شوند.'
                    : 'مقادیر ثبت‌شده از موجودی انبار انتخابی کسر می‌شوند؛ خروج بیش از موجودی مجاز نیست.'}
                </p>
              </div>
              <button type="button" className="ghost-button" onClick={() => setCreateOpen(false)}>
                بستن
              </button>
            </div>

            {formError ? <p className="feedback feedback-error">{formError}</p> : null}

            <form onSubmit={submitCreate}>
              <div className="voucher-form-grid">
                <label>
                  <span>انبار هدف</span>
                  <select
                    value={form.warehouseKey}
                    onChange={(event) =>
                      setForm((current) => ({ ...current, warehouseKey: event.target.value }))
                    }
                  >
                    <option value={MAIN_WAREHOUSE_KEY}>انبار اصلی (موجودی مرکزی)</option>
                    {activeWarehouses.map((w) => (
                      <option key={w.id} value={w.id}>
                        {w.isSubWarehouse ? `↳ ${w.name} (${w.parentName ?? ''})` : w.name}
                      </option>
                    ))}
                  </select>
                </label>

                <label>
                  <span>بابت / دلیل سند</span>
                  <select
                    value={form.reason}
                    onChange={(event) =>
                      setForm((current) => ({ ...current, reason: event.target.value }))
                    }
                  >
                    {reasonOptions.map((opt) => (
                      <option key={opt.value} value={opt.value}>
                        {opt.label}
                      </option>
                    ))}
                  </select>
                </label>

                <label>
                  <span>تاریخ سند (شمسی)</span>
                  <ShamsiDatePicker
                    value={form.voucherDate}
                    onChange={(val) => setForm((current) => ({ ...current, voucherDate: val }))}
                    showHint={false}
                  />
                </label>

                <label>
                  <span>تأمین‌کننده (اختیاری)</span>
                  <select
                    value={form.supplierId}
                    onChange={(event) =>
                      setForm((current) => ({ ...current, supplierId: event.target.value }))
                    }
                  >
                    <option value="">— انتخاب نشده —</option>
                    {suppliers.map((sup) => (
                      <option key={sup.id} value={sup.id}>
                        {sup.persianName}
                      </option>
                    ))}
                  </select>
                </label>

                <label>
                  <span>
                    {form.voucherType === 'Receipt'
                      ? 'تحویل‌دهنده (اختیاری)'
                      : 'تحویل‌گیرنده / بخش مقصد (اختیاری)'}
                  </span>
                  <input
                    type="text"
                    placeholder={
                      form.voucherType === 'Receipt'
                        ? 'مثلاً نماینده پخش یا نام پیک'
                        : 'مثلاً سرآشپز، باریستا شیفت صبح، سالن...'
                    }
                    value={form.counterpartyName}
                    onChange={(event) =>
                      setForm((current) => ({ ...current, counterpartyName: event.target.value }))
                    }
                  />
                </label>

                <label>
                  <span>شماره فاکتور / مرجع (اختیاری)</span>
                  <input
                    type="text"
                    className="ltr-input"
                    placeholder="مثلاً INV-1405-012"
                    value={form.externalReference}
                    onChange={(event) =>
                      setForm((current) => ({ ...current, externalReference: event.target.value }))
                    }
                  />
                </label>

                <label className="full-width">
                  <span>توضیحات کلی سند (اختیاری)</span>
                  <input
                    type="text"
                    placeholder="شرح تکمیلی، شرایط تحویل یا علت خروج..."
                    value={form.note}
                    onChange={(event) =>
                      setForm((current) => ({ ...current, note: event.target.value }))
                    }
                  />
                </label>
              </div>

              <div className="voucher-lines-head">
                <div>
                  <h4>اقلام سند ({toPersianNumber(form.lines.length)} قلم)</h4>
                  {stockLoading ? (
                    <small className="muted">در حال دریافت موجودی زندهٔ انبار انتخابی...</small>
                  ) : null}
                </div>
                <button type="button" className="ghost-button" onClick={addLine}>
                  + افزودن قلم جدید
                </button>
              </div>

              <div className="voucher-lines-table-wrap">
                <table className="voucher-lines-table">
                  <thead>
                    <tr>
                      <th style={{ minWidth: 190 }}>ماده اولیه / کالا</th>
                      <th style={{ width: 95 }}>موجودی فعلی</th>
                      <th style={{ width: 105 }}>مقدار</th>
                      <th style={{ width: 100 }}>موجودی بعد</th>
                      <th style={{ width: 120 }}>قیمت واحد (ت)</th>
                      <th style={{ width: 110 }}>جمع ردیف</th>
                      {form.voucherType === 'Receipt' ? (
                        <>
                          <th style={{ width: 100 }}>شماره بچ</th>
                          <th style={{ width: 130 }}>تاریخ انقضا</th>
                        </>
                      ) : null}
                      <th style={{ minWidth: 120 }}>توضیح ردیف</th>
                      <th style={{ width: 48 }}>حذف</th>
                    </tr>
                  </thead>
                  <tbody>
                    {form.lines.map((line, index) => {
                      const ing = ingredientsById.get(line.ingredientId)
                      const preview = linePreviews[index]
                      return (
                        <tr key={line.key}>
                          <td>
                            <select
                              value={line.ingredientId}
                              onChange={(event) => handleIngredientChange(line.key, event.target.value)}
                              required
                            >
                              <option value="">انتخاب ماده اولیه...</option>
                              {ingredients.map((entry) => {
                                const st = warehouseStockMap.get(entry.id)
                                const avail = st ? Number(st.quantity) : 0
                                return (
                                  <option key={entry.id} value={entry.id}>
                                    {entry.persianName} ({entry.unit}) — موجود: {formatVoucherQty(avail)}
                                  </option>
                                )
                              })}
                            </select>
                          </td>
                          <td>
                            {line.ingredientId ? (
                              <span className="voucher-stock-pill">
                                {formatVoucherQty(preview.currentStock)} {ing?.unit ?? ''}
                              </span>
                            ) : (
                              '—'
                            )}
                          </td>
                          <td>
                            <input
                              type="text"
                              inputMode="decimal"
                              className="ltr-input"
                              placeholder="۰"
                              value={line.quantity}
                              onChange={(event) => updateLine(line.key, { quantity: event.target.value })}
                              required
                            />
                          </td>
                          <td>
                            {line.ingredientId && preview.qty > 0 ? (
                              <span
                                className={`voucher-stock-pill ${
                                  preview.insufficient
                                    ? 'voucher-stock-pill--after-err'
                                    : 'voucher-stock-pill--after-ok'
                                }`}
                              >
                                {formatVoucherQty(preview.stockAfter)} {ing?.unit ?? ''}
                              </span>
                            ) : (
                              '—'
                            )}
                          </td>
                          <td>
                            <input
                              type="text"
                              inputMode="decimal"
                              className="ltr-input"
                              placeholder="تومان"
                              value={line.unitPrice}
                              onChange={(event) => updateLine(line.key, { unitPrice: event.target.value })}
                            />
                          </td>
                          <td>
                            <strong>
                              {preview.lineTotal > 0 ? `${formatVoucherMoney(preview.lineTotal)} ت` : '—'}
                            </strong>
                          </td>
                          {form.voucherType === 'Receipt' ? (
                            <>
                              <td>
                                <input
                                  type="text"
                                  className="ltr-input"
                                  placeholder="اختیاری"
                                  value={line.batchNumber}
                                  onChange={(event) =>
                                    updateLine(line.key, { batchNumber: event.target.value })
                                  }
                                />
                              </td>
                              <td>
                                <ShamsiDatePicker
                                  value={line.expirationDate}
                                  onChange={(val) => updateLine(line.key, { expirationDate: val })}
                                  placeholder="انقضا"
                                  showHint={false}
                                />
                              </td>
                            </>
                          ) : null}
                          <td>
                            <input
                              type="text"
                              placeholder="توضیح..."
                              value={line.lineNote}
                              onChange={(event) => updateLine(line.key, { lineNote: event.target.value })}
                            />
                          </td>
                          <td>
                            <button
                              type="button"
                              className="link-button link-danger"
                              disabled={form.lines.length <= 1}
                              onClick={() => removeLine(line.key)}
                            >
                              ×
                            </button>
                          </td>
                        </tr>
                      )
                    })}
                  </tbody>
                </table>
              </div>

              <div className="voucher-summary-footer">
                <div>
                  <span>تعداد اقلام: </span>
                  <strong>{toPersianNumber(form.lines.length)} قلم</strong>
                  {hasInsufficientLine ? (
                    <span style={{ color: '#b91c1c', marginRight: 12, fontWeight: 700 }}>
                      ⚠ یکی از اقلام بیش از موجودی فعلی انبار است.
                    </span>
                  ) : null}
                </div>
                <div>
                  <span>جمع کل ریالی سند: </span>
                  <strong>{formatVoucherMoney(formGrandTotal)} تومان</strong>
                </div>
              </div>

              <div className="modal-actions">
                <button
                  type="submit"
                  className={
                    form.voucherType === 'Receipt'
                      ? 'primary-button voucher-btn-receipt'
                      : 'primary-button voucher-btn-issue'
                  }
                  disabled={submitting || hasInsufficientLine}
                >
                  {submitting
                    ? 'در حال ثبت قطعی سند...'
                    : form.voucherType === 'Receipt'
                      ? 'ثبت قطعی رسید ورود به انبار'
                      : 'ثبت قطعی سند خروج از انبار'}
                </button>
                <button
                  type="button"
                  className="ghost-button"
                  disabled={submitting}
                  onClick={() => setCreateOpen(false)}
                >
                  انصراف
                </button>
              </div>
            </form>
          </div>
        </div>
      ) : null}

      {/* ── مودال مشاهده و چاپ رسمی سند انبار ────────────────────── */}
      {detailVoucher ? (
        <div className="modal-backdrop" onClick={() => setDetailVoucher(null)}>
          <div
            className="modal-card voucher-modal-card"
            role="dialog"
            aria-modal="true"
            aria-labelledby="detail-voucher-title"
            onClick={(event) => event.stopPropagation()}
          >
            <div className="panel-head">
              <div>
                <h3 id="detail-voucher-title">
                  {detailVoucher.voucherTypeLabel} —{' '}
                  <span className="ltr-text">{detailVoucher.voucherNumber}</span>
                </h3>
                <p className="muted">
                  تاریخ سند: {formatDateTime(detailVoucher.voucherDate)} · وضعیت:{' '}
                  <strong>{detailVoucher.statusLabel}</strong>
                </p>
              </div>
              <div className="table-actions">
                <button
                  type="button"
                  className="primary-button"
                  onClick={() => printWarehouseVoucher(detailVoucher, siteSettings)}
                >
                  🖨 چاپ رسمی سند (A4)
                </button>
                {canCancel && detailVoucher.status !== 'Cancelled' ? (
                  <button
                    type="button"
                    className="link-button link-danger"
                    onClick={() => {
                      setCancelTarget(detailVoucher)
                      setCancelReason('')
                      setCancelError('')
                    }}
                  >
                    ابطال سند
                  </button>
                ) : null}
                <button type="button" className="ghost-button" onClick={() => setDetailVoucher(null)}>
                  بستن
                </button>
              </div>
            </div>

            {detailVoucher.status === 'Cancelled' ? (
              <div className="voucher-cancel-alert">
                <strong>این سند ابطال شده و موجودی تمام اقلام آن به‌صورت خودکار برگشت خورده است.</strong>
                <div>
                  علت ابطال: {detailVoucher.cancellationReason || '—'}
                  {detailVoucher.cancelledByName ? ` · توسط: ${detailVoucher.cancelledByName}` : ''}
                  {detailVoucher.cancelledAt ? ` · زمان: ${formatDateTime(detailVoucher.cancelledAt)}` : ''}
                </div>
              </div>
            ) : null}

            <div className="voucher-doc-preview">
              <div className="voucher-doc-meta-grid">
                <div>
                  <span>انبار</span>
                  <strong>{detailVoucher.warehouseName}</strong>
                </div>
                <div>
                  <span>بابت / دلیل</span>
                  <strong>{detailVoucher.reasonLabel}</strong>
                </div>
                <div>
                  <span>تأمین‌کننده</span>
                  <strong>{detailVoucher.supplierName || '—'}</strong>
                </div>
                <div>
                  <span>
                    {detailVoucher.voucherType === 'Receipt' ? 'تحویل‌دهنده' : 'تحویل‌گیرنده / بخش مقصد'}
                  </span>
                  <strong>{detailVoucher.counterpartyName || '—'}</strong>
                </div>
                <div>
                  <span>شماره فاکتور / مرجع</span>
                  <strong className="ltr-text">{detailVoucher.externalReference || '—'}</strong>
                </div>
                <div>
                  <span>ثبت‌کننده سند</span>
                  <strong>{detailVoucher.createdByName || '—'}</strong>
                </div>
              </div>

              <div className="voucher-lines-table-wrap">
                <table className="voucher-lines-table">
                  <thead>
                    <tr>
                      <th>ردیف</th>
                      <th>شرح کالا / ماده اولیه</th>
                      <th>واحد</th>
                      <th>مقدار</th>
                      <th>موجودی قبل</th>
                      <th>موجودی بعد</th>
                      <th>قیمت واحد</th>
                      <th>مبلغ کل</th>
                      <th>بچ / انقضا</th>
                    </tr>
                  </thead>
                  <tbody>
                    {(detailVoucher.lines ?? []).map((line, idx) => (
                      <tr key={line.id}>
                        <td>{toPersianNumber(idx + 1)}</td>
                        <td>
                          <strong>{line.ingredientName}</strong>
                          {line.ingredientEnglishName ? (
                            <div className="table-subtext ltr-text">{line.ingredientEnglishName}</div>
                          ) : null}
                          {line.lineNote ? <div className="table-subtext">{line.lineNote}</div> : null}
                        </td>
                        <td>{line.unit}</td>
                        <td>
                          <strong style={{ color: detailVoucher.voucherType === 'Receipt' ? '#047857' : '#b45309' }}>
                            {detailVoucher.voucherType === 'Receipt' ? '+' : '−'}
                            {formatVoucherQty(line.quantity)}
                          </strong>
                        </td>
                        <td>{formatVoucherQty(line.stockBefore)}</td>
                        <td>
                          <strong>{formatVoucherQty(line.stockAfter)}</strong>
                        </td>
                        <td>{line.unitPrice != null ? `${formatVoucherMoney(line.unitPrice)} ت` : '—'}</td>
                        <td>
                          <strong>{line.lineTotal > 0 ? `${formatVoucherMoney(line.lineTotal)} ت` : '—'}</strong>
                        </td>
                        <td>
                          {line.batchNumber ? <div>بچ: {line.batchNumber}</div> : null}
                          {line.expirationDate ? <div>انقضا: {line.expirationDate}</div> : null}
                          {!line.batchNumber && !line.expirationDate ? '—' : null}
                        </td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>

              <div className="voucher-summary-footer">
                <span>تعداد اقلام: <strong>{toPersianNumber(detailVoucher.lines?.length ?? 0)} قلم</strong></span>
                <span>جمع کل سند: <strong>{formatVoucherMoney(detailVoucher.totalAmount)} تومان</strong></span>
              </div>

              {detailVoucher.note ? (
                <p className="table-subtext">
                  <strong>توضیحات:</strong> {detailVoucher.note}
                </p>
              ) : null}
            </div>
          </div>
        </div>
      ) : null}

      {/* ── مودال ابطال امن سند انبار ────────────────────────────── */}
      {cancelTarget ? (
        <div className="modal-backdrop" onClick={() => setCancelTarget(null)}>
          <div
            className="modal-card"
            role="dialog"
            aria-modal="true"
            aria-labelledby="cancel-voucher-title"
            onClick={(event) => event.stopPropagation()}
          >
            <h3 id="cancel-voucher-title">
              ابطال سند <span className="ltr-text">{cancelTarget.voucherNumber}</span>
            </h3>
            <p className="muted">
              با ابطال این سند، تاریخچهٔ آن حفظ می‌شود و موجودی تمام اقلام آن به‌صورت خودکار در انبار «{cancelTarget.warehouseName}» برگشت می‌خورد.
            </p>
            {cancelError ? <p className="feedback feedback-error">{cancelError}</p> : null}
            <form onSubmit={submitCancelVoucher}>
              <label>
                <span>علت ابطال سند (الزامی)</span>
                <textarea
                  rows="3"
                  required
                  placeholder="مثلاً ثبت تکراری، خطای تعداد، تغییر انبار هدف..."
                  value={cancelReason}
                  onChange={(event) => setCancelReason(event.target.value)}
                />
              </label>
              <div className="modal-actions">
                <button type="submit" className="link-button link-danger" disabled={cancelling}>
                  {cancelling ? 'در حال ابطال و برگشت موجودی...' : 'تأیید ابطال سند'}
                </button>
                <button
                  type="button"
                  className="ghost-button"
                  disabled={cancelling}
                  onClick={() => setCancelTarget(null)}
                >
                  انصراف
                </button>
              </div>
            </form>
          </div>
        </div>
      ) : null}
    </section>
  )
}
