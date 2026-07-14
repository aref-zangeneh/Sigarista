import { useEffect, useMemo, useState } from 'react'
import { DataTable } from './DataTable'
import { PaginatedAdminTable } from './PaginatedAdminTable'
import { buildProductionBatchColumns, buildProductionPreviewColumns } from './adminTableColumns'
import './ProductionPanel.css'

const emptyProductionForm = {
  menuItemId: '',
  quantityProduced: '',
  note: '',
}

function toPersianNumber(value) {
  return new Intl.NumberFormat('fa-IR').format(value ?? 0)
}

export function ProductionPanel({
  session,
  menuItemOptions,
  apiRequest,
  getReadableError,
  tableReloadToken,
  bumpTableReload,
  setMessage,
  setError,
  saving,
  setSaving,
}) {
  const [productionForm, setProductionForm] = useState(emptyProductionForm)
  const [productionResult, setProductionResult] = useState(null)
  const [productionStock, setProductionStock] = useState([])
  const [recipePreview, setRecipePreview] = useState([])
  const [previewLoading, setPreviewLoading] = useState(false)

  const selectedMenuItem = useMemo(
    () => (menuItemOptions ?? []).find((item) => item.id === productionForm.menuItemId) ?? null,
    [menuItemOptions, productionForm.menuItemId],
  )

  const currentStock = useMemo(
    () => productionStock.find((item) => item.menuItemId === productionForm.menuItemId) ?? null,
    [productionStock, productionForm.menuItemId],
  )

  const quantityProduced = Number(productionForm.quantityProduced) || 0

  const previewColumns = useMemo(
    () => buildProductionPreviewColumns({ toPersianNumber, quantityProduced: quantityProduced || 1 }),
    [quantityProduced],
  )

  const batchColumns = useMemo(
    () => buildProductionBatchColumns({ toPersianNumber }),
    [tableReloadToken],
  )

  async function loadProductionStock() {
    const data = await apiRequest('/admin/production/stock', { token: session.token })
    setProductionStock(data ?? [])
  }

  useEffect(() => {
    if (!session?.token) {
      return
    }

    loadProductionStock().catch(() => {
      setProductionStock([])
    })
  }, [session?.token, tableReloadToken])

  useEffect(() => {
    if (!session?.token || !productionForm.menuItemId || productionResult) {
      setRecipePreview([])
      return
    }

    let cancelled = false
    setPreviewLoading(true)

    apiRequest(`/admin/recipes/${productionForm.menuItemId}`, { token: session.token })
      .then((payload) => {
        if (cancelled) {
          return
        }

        const lines = payload?.lines ?? payload ?? []
        const qty = Number(productionForm.quantityProduced) || 1
        setRecipePreview(
          lines.map((line) => ({
            ingredientPersianName: line.ingredientPersianName,
            ingredientEnglishName: line.ingredientEnglishName,
            unit: line.unit,
            perItemQuantity: line.quantity,
            totalQuantity: line.quantity * qty,
          })),
        )
      })
      .catch(() => {
        if (!cancelled) {
          setRecipePreview([])
        }
      })
      .finally(() => {
        if (!cancelled) {
          setPreviewLoading(false)
        }
      })

    return () => {
      cancelled = true
    }
  }, [productionForm.menuItemId, productionForm.quantityProduced, productionResult, session?.token])

  function resetForm() {
    setProductionForm(emptyProductionForm)
    setProductionResult(null)
  }

  async function handleSubmit(event) {
    event.preventDefault()
    setSaving(true)
    setError('')
    setMessage('')

    try {
      const result = await apiRequest('/admin/production/record', {
        method: 'POST',
        token: session.token,
        body: {
          menuItemId: productionForm.menuItemId,
          quantityProduced: Number(productionForm.quantityProduced),
          note: productionForm.note.trim() || null,
        },
      })

      setProductionResult(result)
      setMessage('تولید با موفقیت ثبت شد و مواد اولیه از انبار کسر شد.')
      bumpTableReload()
      await loadProductionStock()
    } catch (requestError) {
      setError(getReadableError(requestError))
    } finally {
      setSaving(false)
    }
  }

  return (
    <section className="panel-stack production-panel">
      <section className="panel">
        <div className="panel-head">
          <div>
            <h3>موجودی محصولات تولیدی (یخچال)</h3>
            <p>محصولاتی که تولید شده‌اند و منتظر فروش هستند.</p>
          </div>
        </div>

        {productionStock.length === 0 ? (
          <p className="production-empty-note">هنوز تولیدی ثبت نشده است.</p>
        ) : (
          <div className="production-stock-grid">
            {productionStock.map((item) => (
              <article
                key={item.menuItemId}
                className={`production-stock-card ${item.isLowStock ? 'production-stock-card--low' : ''}`}
              >
                <strong>{item.menuItemPersianName}</strong>
                <span className="table-subtext ltr-text">{item.menuItemEnglishName}</span>
                <p className="production-stock-qty">{toPersianNumber(item.currentQuantity)} عدد</p>
              </article>
            ))}
          </div>
        )}
      </section>

      <section className="two-column-layout">
        <form className="panel form-panel" onSubmit={handleSubmit}>
          <div className="panel-head">
            <div>
              <h3>ثبت تولید</h3>
              <p>مثلاً لیموناد — مواد اولیه مطابق دستور پخت از انبار کسر و به موجودی یخچال اضافه می‌شود.</p>
            </div>
            <button type="button" className="ghost-button" onClick={resetForm}>
              پاک کردن فرم
            </button>
          </div>

          {!productionResult ? (
            <>
              <label>
                <span>محصول</span>
                <select
                  required
                  value={productionForm.menuItemId}
                  onChange={(event) =>
                    setProductionForm((current) => ({ ...current, menuItemId: event.target.value }))
                  }
                >
                  <option value="">-- انتخاب محصول --</option>
                  {menuItemOptions?.map((item) => (
                    <option key={item.id} value={item.id}>
                      {item.persianName} — {item.englishName}
                    </option>
                  ))}
                </select>
              </label>

              {selectedMenuItem && currentStock ? (
                <p className="production-current-stock">
                  موجودی فعلی در یخچال: <strong>{toPersianNumber(currentStock.currentQuantity)}</strong> عدد
                </p>
              ) : null}

              <div className="field-grid">
                <label>
                  <span>تعداد تولید شده</span>
                  <input
                    className="ltr-input"
                    type="number"
                    min="0.001"
                    step="0.001"
                    required
                    value={productionForm.quantityProduced}
                    onChange={(event) =>
                      setProductionForm((current) => ({ ...current, quantityProduced: event.target.value }))
                    }
                  />
                </label>
              </div>

              <label>
                <span>یادداشت (اختیاری)</span>
                <textarea
                  rows={2}
                  maxLength={1000}
                  value={productionForm.note}
                  onChange={(event) =>
                    setProductionForm((current) => ({ ...current, note: event.target.value }))
                  }
                  placeholder="مثلاً بچ صبح، یخچال اصلی"
                />
              </label>

              {previewLoading ? (
                <p className="production-preview-note">در حال بارگذاری دستور پخت...</p>
              ) : productionForm.menuItemId && recipePreview.length === 0 ? (
                <p className="production-preview-note production-preview-note--warning">
                  برای این محصول دستور پختی ثبت نشده. ابتدا در تب «دستور پخت» مواد را تعریف کنید.
                </p>
              ) : recipePreview.length > 0 ? (
                <div className="production-preview">
                  <h4>مواد اولیه‌ای که از انبار کسر می‌شود</h4>
                  <DataTable
                    data={recipePreview}
                    columns={previewColumns}
                    enableSearch={false}
                    enablePagination={false}
                    compact
                  />
                </div>
              ) : null}

              <button
                type="submit"
                disabled={
                  saving ||
                  !productionForm.menuItemId ||
                  quantityProduced <= 0 ||
                  recipePreview.length === 0
                }
              >
                {saving ? 'در حال ثبت...' : 'ثبت تولید و کسر مواد اولیه'}
              </button>
            </>
          ) : (
            <div className="production-result">
              <h4>
                تولید ثبت شد: {productionResult.menuItemName} × {toPersianNumber(productionResult.quantityProduced)}
              </h4>
              <p className="production-result-stock">
                موجودی یخچال پس از تولید: <strong>{toPersianNumber(productionResult.finishedStockAfter)}</strong> عدد
              </p>
              {productionResult.ingredientDeductions?.length > 0 ? (
                <>
                  <h5>مواد اولیه کسر شده</h5>
                  <DataTable
                    data={productionResult.ingredientDeductions.map((item) => ({
                      ingredientPersianName: item.ingredientPersianName,
                      ingredientEnglishName: '',
                      unit: item.unit,
                      perItemQuantity: item.amountDeducted / productionResult.quantityProduced,
                      totalQuantity: item.amountDeducted,
                      amountDeducted: item.amountDeducted,
                    }))}
                    columns={buildProductionPreviewColumns({
                      toPersianNumber,
                      quantityProduced: productionResult.quantityProduced,
                    }).map((column) =>
                      column.id === 'total' ? { ...column, header: 'مقدار کسر شده' } : column,
                    )}
                    enableSearch={false}
                    enablePagination={false}
                    compact
                  />
                </>
              ) : null}
              <button type="button" className="ghost-button" onClick={resetForm}>
                تولید جدید
              </button>
            </div>
          )}
        </form>

        <section className="panel list-panel">
          <div className="panel-head">
            <div>
              <h3>تاریخچه تولید</h3>
              <p>آخرین بچ‌های تولید ثبت‌شده.</p>
            </div>
          </div>
          <PaginatedAdminTable
            apiPath="/admin/production/batches"
            token={session.token}
            columns={batchColumns}
            enabled
            reloadToken={tableReloadToken}
            searchPlaceholder="جستجو بر اساس نام محصول یا یادداشت..."
            emptyMessage="هنوز تولیدی ثبت نشده است."
          />
        </section>
      </section>
    </section>
  )
}
