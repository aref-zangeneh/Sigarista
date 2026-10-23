import { useCallback, useEffect, useState } from 'react'
import { useAdmin } from '../context/AdminContext'
import { DataTable } from '../components/DataTable'
import { useEscapeKey } from '../hooks/useEscapeKey'

const CONSUMPTION_WINDOWS = [
  { value: 7, label: '۷ روز اخیر' },
  { value: 30, label: '۳۰ روز اخیر' },
  { value: 90, label: '۹۰ روز اخیر' },
]

const emptyRunRateState = { items: [], loading: true, days: 30 }

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
    consumptionColumns,
    apiRequest,
    getReadableError,
    toPersianNumber,
  } = useAdmin()

  const [runRate, setRunRate] = useState(emptyRunRateState)

  const loadConsumption = useCallback(async (days) => {
    if (!session?.token) return
    setRunRate((current) => ({ ...current, loading: true, days }))
    try {
      const items = await apiRequest(`/admin/inventory/consumption?days=${days}`, { token: session.token })
      setRunRate({ items: Array.isArray(items) ? items : [], loading: false, days })
    } catch {
      setRunRate((current) => ({ ...current, loading: false }))
    }
  }, [session?.token, apiRequest])

  useEffect(() => {
    loadConsumption(30)
  }, [loadConsumption])

  useEscapeKey(Boolean(inventoryAction.mode), () =>
    setInventoryAction({ ingredientId: null, mode: null }),
  )

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
    
            {consumptionColumns ? (
              <section className="panel">
                <div className="panel-head">
                  <div>
                    <h3>تحلیل مصرف و Run Rate</h3>
                    <p>مصرف واقعی بر اساس گردش انبار؛ «پیشنهاد سفارش» برای پوشش بازه جایگزینی (بازه + یک هفته) محاسبه می‌شود.</p>
                  </div>
                  <div className="purchase-mode-toggle inventory-runrate-controls">
                    <div className="purchase-mode-toggle">
                      {CONSUMPTION_WINDOWS.map((window) => (
                        <label key={window.value} className="checkbox-row">
                          <input
                            type="radio"
                            name="consumptionWindow"
                            checked={runRate.days === window.value}
                            onChange={() => loadConsumption(window.value)}
                          />
                          <span>{window.label}</span>
                        </label>
                      ))}
                    </div>
                    <button
                      type="button"
                      className="ghost-button"
                      disabled={runRate.loading}
                      onClick={() => loadConsumption(runRate.days)}
                    >
                      {runRate.loading ? 'در حال محاسبه...' : 'محاسبه مجدد'}
                    </button>
                  </div>
                </div>
                {runRate.loading ? (
                  <p className="feedback">در حال محاسبه نرخ مصرف...</p>
                ) : runRate.items.length === 0 ? (
                  <p className="feedback">قلمی برای تحلیل وجود ندارد.</p>
                ) : (
                  <DataTable
                    data={runRate.items}
                    columns={consumptionColumns}
                    searchPlaceholder="جستجو در تحلیل مصرف..."
                  />
                )}
              </section>
            ) : null}

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
            <div
              className="modal-card"
              role="dialog"
              aria-modal="true"
              aria-labelledby="inventory-restock-title"
              onClick={(e) => e.stopPropagation()}
            >
              <h3 id="inventory-restock-title">شارژ موجودی</h3>
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
            <div
              className="modal-card"
              role="dialog"
              aria-modal="true"
              aria-labelledby="inventory-adjust-title"
              onClick={(e) => e.stopPropagation()}
            >
              <h3 id="inventory-adjust-title">تصحیح موجودی</h3>
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
            <div
              className="modal-card"
              role="dialog"
              aria-modal="true"
              aria-labelledby="inventory-alert-title"
              onClick={(e) => e.stopPropagation()}
            >
              <h3 id="inventory-alert-title">تنظیم حد هشدار موجودی</h3>
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
            <div
              className="modal-card modal-card--wide"
              role="dialog"
              aria-modal="true"
              aria-labelledby="inventory-history-title"
              onClick={(e) => e.stopPropagation()}
            >
              <div className="panel-head">
                <h3 id="inventory-history-title">تاریخچه موجودی</h3>
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
