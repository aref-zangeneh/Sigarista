import { useCallback, useEffect, useMemo, useState } from 'react'
import { useAdmin } from '../context/AdminContext'
import { hasPermission, PERMISSIONS } from '../utils/adminPermissions'
import { Tip } from '../components/Tip'
import './WarehousesPage.css'

const emptyForm = {
  id: '',
  name: '',
  code: '',
  description: '',
  parentId: '',
  displayOrder: 0,
  isActive: true,
}

function toNumberInput(value) {
  return value === null || value === undefined || value === '' ? 0 : Number(value)
}

export function WarehousesPage() {
  const { session, apiRequest, getReadableError, saving, setSaving, setError, setMessage } = useAdmin()
  const canCreate = hasPermission(session, PERMISSIONS.warehouses.create)
  const canUpdate = hasPermission(session, PERMISSIONS.warehouses.update)
  const canDelete = hasPermission(session, PERMISSIONS.warehouses.delete)
  const canRestock = hasPermission(session, PERMISSIONS.inventory.create)
  const canAdjust = hasPermission(session, PERMISSIONS.inventory.update)

  const [warehouses, setWarehouses] = useState([])
  const [loading, setLoading] = useState(true)
  const [pageError, setPageError] = useState('')
  const [pageMessage, setPageMessage] = useState('')

  const [form, setForm] = useState(emptyForm)
  const [search, setSearch] = useState('')
  const [showInactive, setShowInactive] = useState(true)

  const [stockWarehouseId, setStockWarehouseId] = useState(null)
  const [stockData, setStockData] = useState(null)
  const [stockLoading, setStockLoading] = useState(false)
  const [stockAction, setStockAction] = useState(null)
  const [ingredientOptions, setIngredientOptions] = useState([])
  const [linkForm, setLinkForm] = useState({ ingredientId: '', quantity: 0 })

  // ── data loading ─────────────────────────────────────────
  const loadWarehouses = useCallback(async () => {
    if (!session?.token) return
    setLoading(true)
    setPageError('')
    try {
      const data = await apiRequest('/admin/warehouses', { token: session.token })
      setWarehouses(Array.isArray(data) ? data : [])
    } catch (requestError) {
      setPageError(getReadableError(requestError))
    } finally {
      setLoading(false)
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [session?.token])

  const loadStock = useCallback(async (warehouseId) => {
    if (!session?.token || !warehouseId) return
    setStockLoading(true)
    try {
      const data = await apiRequest(`/admin/warehouses/${warehouseId}/stock`, { token: session.token })
      setStockData(data)
    } catch (requestError) {
      setPageError(getReadableError(requestError))
      setStockData(null)
    } finally {
      setStockLoading(false)
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [session?.token])

  useEffect(() => {
    loadWarehouses()
  }, [loadWarehouses])

  // Ingredient options for the «stock linking» picker — failure is fine
  // (viewer-only admins cannot restock anyway).
  useEffect(() => {
    if (!stockWarehouseId || !session?.token || !canRestock) return
    let cancelled = false
    apiRequest('/admin/ingredients/options', { token: session.token })
      .then((data) => {
        if (!cancelled) setIngredientOptions(Array.isArray(data) ? data : [])
      })
      .catch(() => {})
    return () => {
      cancelled = true
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [stockWarehouseId, session?.token, canRestock])

  useEffect(() => {
    if (stockWarehouseId) {
      loadStock(stockWarehouseId)
    } else {
      setStockData(null)
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [stockWarehouseId])

  // ── tree helpers ─────────────────────────────────────────
  const roots = useMemo(() => warehouses.filter((w) => !w.parentId), [warehouses])
  const childrenOf = useMemo(() => {
    const map = new Map()
    warehouses.forEach((w) => {
      if (w.parentId) {
        if (!map.has(w.parentId)) map.set(w.parentId, [])
        map.get(w.parentId).push(w)
      }
    })
    return map
  }, [warehouses])

  const visibleWarehouses = useMemo(() => {
    const query = search.trim().toLowerCase()
    const matches = (w) =>
      [w.name, w.code, w.description, w.parentName].filter(Boolean).join(' ').toLowerCase().includes(query)
    if (!query) {
      return warehouses
    }
    return warehouses.filter(matches)
  }, [warehouses, search])

  // Parent candidates for the form: all warehouses except the edited one and
  // its انبارک descendants.
  const parentCandidates = useMemo(() => {
    if (!form.id) return warehouses
    const descendants = new Set()
    const walk = (parentId) => {
      childrenOf.get(parentId)?.forEach((child) => {
        descendants.add(child.id)
        walk(child.id)
      })
    }
    walk(form.id)
    return warehouses.filter((w) => w.id !== form.id && !descendants.has(w.id))
  }, [warehouses, form.id, childrenOf])

  // ── form actions ─────────────────────────────────────────
  function startCreate(parentId = '') {
    setForm({ ...emptyForm, parentId })
  }

  function startEdit(warehouse) {
    setForm({
      id: warehouse.id,
      name: warehouse.name,
      code: warehouse.code ?? '',
      description: warehouse.description ?? '',
      parentId: warehouse.parentId ?? '',
      displayOrder: warehouse.displayOrder ?? 0,
      isActive: warehouse.isActive,
    })
  }

  async function submitForm(event) {
    event.preventDefault()
    if (!canCreate && !form.id) return
    if (!canUpdate && form.id) return

    setSaving(true)
    setError('')
    setMessage('')
    setPageError('')
    setPageMessage('')
    try {
      const payload = {
        name: form.name,
        code: form.code || null,
        description: form.description || null,
        parentId: form.parentId || null,
        displayOrder: toNumberInput(form.displayOrder),
        isActive: form.isActive,
      }
      if (form.id) {
        await apiRequest(`/admin/warehouses/${form.id}`, { method: 'PUT', body: payload, token: session.token })
        setPageMessage(`${form.parentId ? 'انبارک' : 'انبار'} «${form.name}» ویرایش شد.`)
      } else {
        await apiRequest('/admin/warehouses', { method: 'POST', body: payload, token: session.token })
        setPageMessage(`${form.parentId ? 'انبارک' : 'انبار'} «${form.name}» تعریف شد.`)
      }
      setForm(emptyForm)
      await loadWarehouses()
      if (stockWarehouseId) {
        await loadStock(stockWarehouseId)
      }
    } catch (requestError) {
      setPageError(getReadableError(requestError))
    } finally {
      setSaving(false)
    }
  }

  async function handleDelete(warehouse) {
    if (!canDelete) return
    const label = warehouse.isSubWarehouse ? 'انبارک' : 'انبار'
    if (!window.confirm(`حذف ${label} «${warehouse.name}»؟ انبارهایی که موجودی یا تراکنش دارند حذف نمی‌شوند.`)) {
      return
    }

    setSaving(true)
    setPageError('')
    setPageMessage('')
    try {
      await apiRequest(`/admin/warehouses/${warehouse.id}`, { method: 'DELETE', token: session.token })
      setPageMessage(`${label} «${warehouse.name}» حذف شد.`)
      if (stockWarehouseId === warehouse.id) {
        setStockWarehouseId(null)
      }
      await loadWarehouses()
    } catch (requestError) {
      setPageError(getReadableError(requestError))
    } finally {
      setSaving(false)
    }
  }

  // ── stock actions ────────────────────────────────────────
  function openStockAction(item) {
    setStockAction({
      ingredientId: item.ingredientId,
      ingredientName: item.persianName,
      unit: item.unit,
      quantity: 0,
      current: item.quantity,
      threshold: toNumberInput(item.minAlertQuantity),
    })
  }

  async function submitStockRestock(event) {
    event.preventDefault()
    if (!stockAction || !canRestock) return
    const quantity = Number(stockAction.quantity)
    if (!quantity || quantity <= 0) {
      setPageError('مقدار شارژ باید بزرگ‌تر از صفر باشد.')
      return
    }

    setSaving(true)
    setPageError('')
    setPageMessage('')
    try {
      await apiRequest(`/admin/inventory/${stockAction.ingredientId}/restock`, {
        method: 'POST',
        body: {
          quantity,
          note: `شارژ انبار ${stockData?.warehouseName ?? ''}`.trim(),
          warehouseId: stockWarehouseId,
        },
        token: session.token,
      })
      setPageMessage(`موجودی «${stockAction.ingredientName}» شارژ شد.`)
      setStockAction(null)
      await loadStock(stockWarehouseId)
    } catch (requestError) {
      setPageError(getReadableError(requestError))
    } finally {
      setSaving(false)
    }
  }

  async function submitStockAdjust(event) {
    event.preventDefault()
    if (!stockAction || !canAdjust) return
    const delta = Number(stockAction.quantity)
    if (!delta || delta === 0) {
      setPageError('مقدار تعدیل نمی‌تواند صفر باشد.')
      return
    }

    setSaving(true)
    setPageError('')
    setPageMessage('')
    try {
      await apiRequest(`/admin/inventory/${stockAction.ingredientId}/adjust`, {
        method: 'POST',
        body: {
          delta,
          note: `تعدیل انبار ${stockData?.warehouseName ?? ''}`.trim(),
          warehouseId: stockWarehouseId,
        },
        token: session.token,
      })
      setPageMessage(`موجودی «${stockAction.ingredientName}» تعدیل شد.`)
      setStockAction(null)
      await loadStock(stockWarehouseId)
    } catch (requestError) {
      setPageError(getReadableError(requestError))
    } finally {
      setSaving(false)
    }
  }

  async function submitStockThreshold(event) {
    event.preventDefault()
    if (!stockAction || !canAdjust) return

    setSaving(true)
    setPageError('')
    setPageMessage('')
    try {
      await apiRequest(`/admin/inventory/${stockAction.ingredientId}/alert-threshold`, {
        method: 'POST',
        body: {
          minAlertQuantity: Number(stockAction.threshold) || 0,
          warehouseId: stockWarehouseId,
        },
        token: session.token,
      })
      setPageMessage(`آستانهٔ هشدار «${stockAction.ingredientName}» به‌روزرسانی شد.`)
      setStockAction(null)
      await loadStock(stockWarehouseId)
    } catch (requestError) {
      setPageError(getReadableError(requestError))
    } finally {
      setSaving(false)
    }
  }

  async function linkIngredient(event) {
    event.preventDefault()
    if (!canRestock || !linkForm.ingredientId) return

    setSaving(true)
    setPageError('')
    setPageMessage('')
    try {
      const initialQuantity = Number(linkForm.quantity) || 0
      if (initialQuantity <= 0) {
        throw new Error('برای اتصال ماده، مقدار اولیه باید بزرگ‌تر از صفر باشد.')
      }
      await apiRequest(`/admin/inventory/${linkForm.ingredientId}/restock`, {
        method: 'POST',
        body: {
          quantity: initialQuantity,
          note: `اتصال به انبار ${stockData?.warehouseName ?? ''}`.trim(),
          warehouseId: stockWarehouseId,
        },
        token: session.token,
      })
      setPageMessage('ماده به انبار متصل شد.')
      setLinkForm({ ingredientId: '', quantity: 0 })
      await loadStock(stockWarehouseId)
    } catch (requestError) {
      setPageError(getReadableError(requestError))
    } finally {
      setSaving(false)
    }
  }

  const editing = Boolean(form.id)

  // ── render ───────────────────────────────────────────────
  function renderRow(warehouse, isChild) {
    return (
      <tbody key={warehouse.id}>
        <tr className={isChild ? 'warehouse-row warehouse-row-child' : 'warehouse-row'}>
          <td>
            {isChild ? <span className="warehouse-child-marker" aria-hidden="true">↳</span> : null}
            <strong>{warehouse.name}</strong>
            {!warehouse.isActive ? <span className="warehouse-badge warehouse-badge-off">غیرفعال</span> : null}
            <span className="warehouse-badge">{isChild || warehouse.isSubWarehouse ? 'انبارک' : 'انبار'}</span>
            {warehouse.code ? <div className="table-subtext ltr-text">{warehouse.code}</div> : null}
            {warehouse.description ? <div className="table-subtext">{warehouse.description}</div> : null}
          </td>
          <td className="ltr-text">{warehouse.parentName ?? '—'}</td>
          <td className="ltr-text">{isChild || warehouse.subWarehouseCount > 0 ? toPersian(warehouse.subWarehouseCount ?? 0) : '—'}</td>
          <td className="ltr-text">{toPersian(warehouse.displayOrder ?? 0)}</td>
          <td>
            <div className="table-actions">
              <button type="button" className="link-button" onClick={() => openStockPanel(warehouse.id)}>
                موجودی
              </button>
              <button type="button" className="link-button" onClick={() => startEdit(warehouse)} disabled={!canUpdate}>
                ویرایش
              </button>
              <button type="button" className="link-button link-danger" onClick={() => handleDelete(warehouse)} disabled={!canDelete}>
                حذف
              </button>
            </div>
          </td>
        </tr>
        {search.trim() ? null : (childrenOf.get(warehouse.id) ?? [])
          .filter((child) => showInactive || child.isActive)
          .map((child) => renderRow(child, true))}
      </tbody>
    )
  }

  function openStockPanel(warehouseId) {
    setStockWarehouseId((current) => (current === warehouseId ? null : warehouseId))
    setStockAction(null)
  }

  function toPersian(value) {
    return String(value).replace(/\d/g, (digit) => '۰۱۲۳۴۵۶۷۸۹'[digit])
  }

  return (
    <section className="warehouses-page">
      <header className="panel-head warehouses-head">
        <div>
          <h2>
            انبارها و انبارک‌ها <Tip k="warehouses.list" />
          </h2>
          <p className="muted">تعریف، ویرایش و مدیریت موجودی انبارها. موجودی انبار اصلی (موجودی فعلی) دست‌نخورده می‌ماند.</p>
        </div>
        <button type="button" className="ghost-button" onClick={loadWarehouses} disabled={loading}>
          تازه‌سازی
        </button>
      </header>

      {pageError ? <p className="feedback feedback-error" role="alert">{pageError}</p> : null}
      {pageMessage ? <p className="feedback feedback-success">{pageMessage}</p> : null}

      <div className="warehouses-layout">
        {/* ── form ── */}
        <form className="panel form-panel warehouses-form" onSubmit={submitForm}>
          <div className="panel-head">
            <h3>{editing ? 'ویرایش انبار' : 'تعریف انبار / انبارک جدید'}</h3>
            {editing ? (
              <button type="button" className="ghost-button" onClick={() => startCreate()}>
                انصراف
              </button>
            ) : null}
          </div>

          <div className="field-grid">
            <label>
              نام
              <Tip k="warehouses.name" />
              <input
                type="text"
                value={form.name}
                onChange={(event) => setForm((current) => ({ ...current, name: event.target.value }))}
                required
                maxLength={120}
                placeholder="مثلاً انبار مرکزی"
              />
            </label>

            <label>
              کد (اختیاری)
              <input
                type="text"
                className="ltr-input"
                value={form.code}
                onChange={(event) => setForm((current) => ({ ...current, code: event.target.value }))}
                maxLength={40}
                placeholder="WH-01"
              />
            </label>

            <label>
              نوع <Tip k="warehouses.parent" />
              <select
                value={form.parentId}
                onChange={(event) => setForm((current) => ({ ...current, parentId: event.target.value }))}
              >
                <option value="">انبار اصلی (بدون والد)</option>
                {parentCandidates
                  .filter((w) => !w.isSubWarehouse)
                  .map((candidate) => (
                    <option key={candidate.id} value={candidate.id}>
                      انبارک زیر «{candidate.name}»
                    </option>
                  ))}
              </select>
            </label>

            <label>
              ترتیب نمایش
              <input
                type="number"
                min="0"
                value={form.displayOrder}
                onChange={(event) => setForm((current) => ({ ...current, displayOrder: event.target.value }))}
              />
            </label>

            <label className="field-span">
              توضیحات
              <textarea
                rows={2}
                value={form.description}
                onChange={(event) => setForm((current) => ({ ...current, description: event.target.value }))}
                maxLength={500}
                placeholder="اختیاری"
              />
            </label>

            <label className="checkbox-row">
              <input
                type="checkbox"
                checked={form.isActive}
                onChange={(event) => setForm((current) => ({ ...current, isActive: event.target.checked }))}
              />
              فعال <Tip k="warehouses.active" />
            </label>
          </div>

          <div className="modal-actions">
            <button type="submit" className="primary-button" disabled={saving || (editing ? !canUpdate : !canCreate)}>
              {saving ? '...' : editing ? 'ذخیرهٔ تغییرات' : 'تعریف انبار'}
            </button>
            {editing ? (
              <button type="button" className="ghost-button" onClick={() => setForm(emptyForm)}>
                پاک کردن فرم
              </button>
            ) : null}
          </div>
        </form>

        {/* ── list ── */}
        <section className="panel list-panel warehouses-list">
          <div className="panel-head">
            <h3>
              فهرست انبارها <Tip k="warehouses.tree" />
            </h3>
            <div className="warehouses-toolbar">
              <input
                type="search"
                placeholder="جستجو در انبارها..."
                value={search}
                onChange={(event) => setSearch(event.target.value)}
              />
              <label className="checkbox-row warehouses-filter">
                <input
                  type="checkbox"
                  checked={showInactive}
                  onChange={(event) => setShowInactive(event.target.checked)}
                />
                نمایش غیرفعال‌ها
              </label>
            </div>
          </div>

          {loading ? <p className="feedback">در حال بارگذاری انبارها...</p> : null}

          {!loading && visibleWarehouses.length === 0 ? (
            <p className="feedback">
              {search ? 'انباری مطابق جستجو پیدا نشد.' : 'هنوز انباری تعریف نشده است. از فرم روبه‌رو تعریف کنید.'}
            </p>
          ) : null}

          {!loading && visibleWarehouses.length > 0 ? (
            <table className="warehouses-table">
              <thead>
                <tr>
                  <th>نام</th>
                  <th>والد</th>
                  <th>انبارک‌ها</th>
                  <th>ترتیب</th>
                  <th>عملیات</th>
                </tr>
              </thead>
              {search.trim() ? (
                <tbody>
                  {visibleWarehouses.map((warehouse) => renderRow(warehouse, warehouse.isSubWarehouse))}
                </tbody>
              ) : (
                roots
                  .filter((root) => showInactive || root.isActive)
                  .map((root) => renderRow(root, false))
              )}
            </table>
          ) : null}
        </section>
      </div>

      {/* ── stock panel ── */}
      {stockWarehouseId ? (
        <section className="panel warehouses-stock-panel">
          <div className="panel-head">
            <h3>
              موجودی «{stockData?.warehouseName ?? '...'}» <Tip k="warehouses.stock" />
            </h3>
            <button type="button" className="ghost-button" onClick={() => setStockWarehouseId(null)}>
              بستن
            </button>
          </div>

          {stockLoading ? <p className="feedback">در حال بارگذاری موجودی...</p> : null}

          {stockData && !stockLoading ? (
            <>
              {stockData.items.length === 0 ? (
                <p className="feedback">هنوز ماده‌ای به این انبار متصل نشده است.</p>
              ) : (
                <table className="warehouses-table">
                  <thead>
                    <tr>
                      <th>ماده</th>
                      <th>موجودی</th>
                      <th>آستانه هشدار</th>
                      <th>وضعیت</th>
                      <th>عملیات</th>
                    </tr>
                  </thead>
                  <tbody>
                    {stockData.items.map((item) => (
                      <tr key={item.ingredientId} className={item.isLowStock ? 'warehouse-row-low' : ''}>
                        <td>
                          <strong>{item.persianName}</strong>
                          <div className="table-subtext ltr-text">{item.englishName}</div>
                        </td>
                        <td className="ltr-text">{toPersian(item.quantity)} {item.unit}</td>
                        <td className="ltr-text">{toPersian(item.minAlertQuantity)}</td>
                        <td>
                          {item.isLowStock ? (
                            <span className="warehouse-badge warehouse-badge-low">کمتر از آستانه</span>
                          ) : (
                            <span className="warehouse-badge warehouse-badge-ok">مطلوب</span>
                          )}
                        </td>
                        <td>
                          <div className="table-actions">
                            <button type="button" className="link-button" onClick={() => openStockAction(item)} disabled={!canRestock && !canAdjust}>
                              شارژ / تعدیل
                            </button>
                          </div>
                        </td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              )}

              {canRestock ? (
                <form className="warehouse-link-form" onSubmit={linkIngredient}>
                  <label>
                    اتصال مادهٔ جدید به انبار <Tip k="warehouses.link" />
                    <select
                      value={linkForm.ingredientId}
                      onChange={(event) => setLinkForm((current) => ({ ...current, ingredientId: event.target.value }))}
                    >
                      <option value="">انتخاب ماده...</option>
                      {ingredientOptions.map((option) => (
                        <option key={option.id} value={option.id}>
                          {option.persianName} ({option.unit})
                        </option>
                      ))}
                    </select>
                  </label>
                  <label>
                    مقدار اولیه
                    <input
                      type="number"
                      min="0"
                      step="any"
                      value={linkForm.quantity}
                      onChange={(event) => setLinkForm((current) => ({ ...current, quantity: event.target.value }))}
                    />
                  </label>
                  <button type="submit" className="primary-button" disabled={saving || !linkForm.ingredientId}>
                    {saving ? '...' : 'اتصال و شارژ'}
                  </button>
                </form>
              ) : null}
            </>
          ) : null}
        </section>
      ) : null}

      {/* ── stock action modal ── */}
      {stockAction ? (
        <div className="modal-backdrop" role="presentation" onClick={() => setStockAction(null)}>
          <div
            className="modal-card warehouse-stock-modal"
            role="dialog"
            aria-modal="true"
            aria-label={`مدیریت موجودی ${stockAction.ingredientName}`}
            onClick={(event) => event.stopPropagation()}
          >
            <div className="panel-head">
              <h3>مدیریت موجودی «{stockAction.ingredientName}»</h3>
              <button type="button" className="ghost-button" onClick={() => setStockAction(null)}>
                بستن
              </button>
            </div>
            <p className="muted">
              موجودی فعلی: <strong className="ltr-text">{toPersian(stockAction.current)}</strong> {stockAction.unit}
            </p>

            {canRestock ? (
              <form className="warehouse-action-form" onSubmit={submitStockRestock}>
                <label>
                  شارژ (افزودن به موجودی) <Tip k="warehouses.restock" />
                  <input
                    type="number"
                    min="0"
                    step="any"
                    value={stockAction.quantity}
                    onChange={(event) => setStockAction((current) => ({ ...current, quantity: event.target.value }))}
                  />
                </label>
                <button type="submit" className="primary-button" disabled={saving}>
                  {saving ? '...' : 'ثبت شارژ'}
                </button>
              </form>
            ) : null}

            {canAdjust ? (
              <form className="warehouse-action-form" onSubmit={submitStockAdjust}>
                <label>
                  تعدیل (مثبت یا منفی) <Tip k="warehouses.adjust" />
                  <input
                    type="number"
                    step="any"
                    value={stockAction.quantity}
                    onChange={(event) => setStockAction((current) => ({ ...current, quantity: event.target.value }))}
                  />
                </label>
                <button type="submit" className="ghost-button" disabled={saving}>
                  {saving ? '...' : 'ثبت تعدیل'}
                </button>
              </form>
            ) : null}

            {canAdjust ? (
              <form className="warehouse-action-form" onSubmit={submitStockThreshold}>
                <label>
                  آستانهٔ هشدار <Tip k="warehouses.threshold" />
                  <input
                    type="number"
                    min="0"
                    step="any"
                    value={stockAction.threshold}
                    onChange={(event) => setStockAction((current) => ({ ...current, threshold: event.target.value }))}
                  />
                </label>
                <button type="submit" className="ghost-button" disabled={saving}>
                  {saving ? '...' : 'ثبت آستانه'}
                </button>
              </form>
            ) : null}

            {!canRestock && !canAdjust ? (
              <p className="feedback">برای تغییر موجودی به مجوز «ورود / شارژ» یا «تعدیل / آستانهٔ موجودی» نیاز دارید.</p>
            ) : null}
          </div>
        </div>
      ) : null}
    </section>
  )
}
