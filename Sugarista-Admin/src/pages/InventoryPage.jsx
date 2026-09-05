import { useAdmin } from '../context/AdminContext'
import { DataTable } from '../components/DataTable'

export function InventoryPage() {
  const {
    session,
    inventoryStatus,
    setInventoryStatus,
    inventoryAction,
    setInventoryAction,
    restockForm,
    setRestockForm,
    adjustForm,
    setAdjustForm,
    alertForm,
    setAlertForm,
    stockHistory,
    saving,
    setSaving,
    setMessage,
    setError,
    inventoryColumns,
    stockHistoryColumns,
    apiRequest,
    getReadableError,
    toPersianNumber,
  } = useAdmin()

  return (
      <section className="panel-stack">
        {inventoryStatus ? (
          <>
            <div className="inventory-summary">
              <div className="stat-card">
                <span className="stat-value">{toPersianNumber(inventoryStatus.totalIngredients)}</span>
                <span className="stat-label">مواد اولیه</span>
              </div>
              {inventoryStatus.lowStockCount > 0 ? (
                <div className="stat-card stat-card--danger">
                  <span className="stat-value">{toPersianNumber(inventoryStatus.lowStockCount)}</span>
                  <span className="stat-label">موجودی کم</span>
                </div>
              ) : (
                <div className="stat-card stat-card--ok">
                  <span className="stat-value">موجودی سالم</span>
                  <span className="stat-label">همه مواد در سطح مناسب</span>
                </div>
              )}
              <button
                type="button"
                className="ghost-button"
                onClick={async () => {
                  const data = await apiRequest('/admin/inventory', { token: session.token })
                  setInventoryStatus(data)
                }}
              >
                بروزرسانی
              </button>
            </div>
    
            {inventoryStatus.groups.map((group) => (
              <section key={group.location} className="panel">
                <div className="panel-head">
                  <div>
                    <h3>{group.locationLabel}</h3>
                    <p>{toPersianNumber(group.items.length)} ماده اولیه</p>
                  </div>
                </div>
                <DataTable
                  data={group.items}
                  columns={inventoryColumns}
                  searchPlaceholder={`جستجو در ${group.locationLabel}...`}
                />
              </section>
            ))}
          </>
        ) : (
          <section className="panel"><p>در حال بارگذاری...</p></section>
        )}
    
        {inventoryAction.mode === 'restock' ? (
          <div className="modal-backdrop" onClick={() => setInventoryAction({ ingredientId: null, mode: null })}>
            <div className="modal-card" onClick={(e) => e.stopPropagation()}>
              <h3>شارژ موجودی</h3>
              <form
                onSubmit={async (event) => {
                  event.preventDefault()
                  setSaving(true)
                  setError('')
                  try {
                    await apiRequest(`/admin/inventory/${inventoryAction.ingredientId}/restock`, {
                      method: 'POST',
                      token: session.token,
                      body: { ingredientId: inventoryAction.ingredientId, quantity: Number(restockForm.quantity), note: restockForm.note },
                    })
                    setMessage('موجودی شارژ شد.')
                    setInventoryAction({ ingredientId: null, mode: null })
                    const data = await apiRequest('/admin/inventory', { token: session.token })
                    setInventoryStatus(data)
                  } catch (requestError) {
                    setError(getReadableError(requestError))
                  } finally {
                    setSaving(false)
                  }
                }}
              >
                <label>
                  <span>مقدار اضافه شده</span>
                  <input className="ltr-input" type="number" min="0.001" step="0.001" required value={restockForm.quantity}
                    onChange={(e) => setRestockForm((f) => ({ ...f, quantity: e.target.value }))} />
                </label>
                <label>
                  <span>توضیح (اختیاری)</span>
                  <input value={restockForm.note} onChange={(e) => setRestockForm((f) => ({ ...f, note: e.target.value }))} />
                </label>
                <div className="modal-actions">
                  <button type="submit" disabled={saving}>{saving ? 'در حال ذخیره...' : 'ذخیره'}</button>
                  <button type="button" className="ghost-button" onClick={() => setInventoryAction({ ingredientId: null, mode: null })}>لغو</button>
                </div>
              </form>
            </div>
          </div>
        ) : null}
    
        {inventoryAction.mode === 'adjust' ? (
          <div className="modal-backdrop" onClick={() => setInventoryAction({ ingredientId: null, mode: null })}>
            <div className="modal-card" onClick={(e) => e.stopPropagation()}>
              <h3>تصحیح موجودی</h3>
              <p>مقدار مثبت = افزایش، مقدار منفی = کاهش</p>
              <form
                onSubmit={async (event) => {
                  event.preventDefault()
                  setSaving(true)
                  setError('')
                  try {
                    await apiRequest(`/admin/inventory/${inventoryAction.ingredientId}/adjust`, {
                      method: 'POST',
                      token: session.token,
                      body: { ingredientId: inventoryAction.ingredientId, delta: Number(adjustForm.delta), note: adjustForm.note },
                    })
                    setMessage('موجودی تصحیح شد.')
                    setInventoryAction({ ingredientId: null, mode: null })
                    const data = await apiRequest('/admin/inventory', { token: session.token })
                    setInventoryStatus(data)
                  } catch (requestError) {
                    setError(getReadableError(requestError))
                  } finally {
                    setSaving(false)
                  }
                }}
              >
                <label>
                  <span>مقدار تغییر</span>
                  <input className="ltr-input" type="number" step="0.001" required value={adjustForm.delta}
                    onChange={(e) => setAdjustForm((f) => ({ ...f, delta: e.target.value }))} />
                </label>
                <label>
                  <span>دلیل تصحیح</span>
                  <input required value={adjustForm.note} onChange={(e) => setAdjustForm((f) => ({ ...f, note: e.target.value }))} />
                </label>
                <div className="modal-actions">
                  <button type="submit" disabled={saving}>{saving ? 'در حال ذخیره...' : 'ذخیره'}</button>
                  <button type="button" className="ghost-button" onClick={() => setInventoryAction({ ingredientId: null, mode: null })}>لغو</button>
                </div>
              </form>
            </div>
          </div>
        ) : null}
    
        {inventoryAction.mode === 'alert' ? (
          <div className="modal-backdrop" onClick={() => setInventoryAction({ ingredientId: null, mode: null })}>
            <div className="modal-card" onClick={(e) => e.stopPropagation()}>
              <h3>تنظیم حد هشدار موجودی</h3>
              <form
                onSubmit={async (event) => {
                  event.preventDefault()
                  setSaving(true)
                  setError('')
                  try {
                    await apiRequest(`/admin/inventory/${inventoryAction.ingredientId}/alert-threshold`, {
                      method: 'POST',
                      token: session.token,
                      body: { ingredientId: inventoryAction.ingredientId, minAlertQuantity: Number(alertForm.minAlertQuantity) },
                    })
                    setMessage('حد هشدار ذخیره شد.')
                    setInventoryAction({ ingredientId: null, mode: null })
                    const data = await apiRequest('/admin/inventory', { token: session.token })
                    setInventoryStatus(data)
                  } catch (requestError) {
                    setError(getReadableError(requestError))
                  } finally {
                    setSaving(false)
                  }
                }}
              >
                <label>
                  <span>حداقل موجودی قابل قبول</span>
                  <input className="ltr-input" type="number" min="0" step="0.001" required value={alertForm.minAlertQuantity}
                    onChange={(e) => setAlertForm((f) => ({ ...f, minAlertQuantity: e.target.value }))} />
                </label>
                <div className="modal-actions">
                  <button type="submit" disabled={saving}>{saving ? 'در حال ذخیره...' : 'ذخیره'}</button>
                  <button type="button" className="ghost-button" onClick={() => setInventoryAction({ ingredientId: null, mode: null })}>لغو</button>
                </div>
              </form>
            </div>
          </div>
        ) : null}
    
        {inventoryAction.mode === 'history' ? (
          <div className="modal-backdrop" onClick={() => setInventoryAction({ ingredientId: null, mode: null })}>
            <div className="modal-card modal-card--wide" onClick={(e) => e.stopPropagation()}>
              <div className="panel-head">
                <h3>تاریخچه موجودی</h3>
                <button type="button" className="ghost-button" onClick={() => setInventoryAction({ ingredientId: null, mode: null })}>بستن</button>
              </div>
              <DataTable
                data={stockHistory}
                columns={stockHistoryColumns}
                enableSearch={false}
                enablePagination={stockHistory.length > 10}
                pageSize={10}
                compact
                emptyMessage="تاریخچه‌ای ثبت نشده."
              />
            </div>
          </div>
        ) : null}
      </section>
  )
}
