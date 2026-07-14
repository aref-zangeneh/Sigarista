import { useEffect, useMemo, useState } from 'react'
import { formatMoney } from './DataTable'
import { PaginatedAdminTable } from './PaginatedAdminTable'
import { buildInventoryPurchaseColumns } from './adminTableColumns'
import './InventoryPurchasesPanel.css'

const API_BASE_URL = import.meta.env.VITE_API_BASE_URL ?? '/api'

const emptyPurchaseForm = {
  ingredientId: '',
  quantity: '',
  packageCount: '',
  packageSize: '',
  displayUnit: '',
  useDefaultUnit: true,
  expirationDate: '',
  purchaseUnitPrice: '',
  shippingFee: '',
  taxAmount: '',
  incidentalCost: '',
  description: '',
  documentMode: 'upload',
  documentImageUrl: '',
  entryMode: 'packages',
}

function toPersianNumber(value) {
  return new Intl.NumberFormat('fa-IR').format(value ?? 0)
}

function expirationBadge(status) {
  if (status === 'Expired') return <span className="purchase-badge purchase-badge--expired">منقضی</span>
  if (status === 'ExpiringSoon') return <span className="purchase-badge purchase-badge--soon">نزدیک انقضا</span>
  return <span className="purchase-badge purchase-badge--ok">سالم</span>
}

async function uploadInventoryDocument(file, token) {
  const formData = new FormData()
  formData.append('file', file)

  const response = await fetch(`${API_BASE_URL}/admin/uploads/inventory-documents`, {
    method: 'POST',
    headers: {
      ...(token ? { Authorization: `Bearer ${token}` } : {}),
    },
    body: formData,
  })

  if (!response.ok) {
    let message = `آپلود فایل با خطای ${response.status} مواجه شد.`
    try {
      const payload = await response.json()
      message = payload.detail || payload.title || message
    } catch {
      // Keep fallback message.
    }
    throw new Error(message)
  }

  const payload = await response.json()
  return payload.url
}

export function InventoryPurchasesPanel({
  session,
  ingredientOptions,
  unitOptions,
  apiRequest,
  getReadableError,
  tableReloadToken,
  bumpTableReload,
  setMessage,
  setError,
  saving,
  setSaving,
  showExpiringOnly: showExpiringOnlyProp,
  onShowExpiringOnlyChange,
}) {
  const [purchaseForm, setPurchaseForm] = useState(emptyPurchaseForm)
  const [documentFile, setDocumentFile] = useState(null)
  const [documentPreviewUrl, setDocumentPreviewUrl] = useState('')
  const [showExpiringOnlyInternal, setShowExpiringOnlyInternal] = useState(false)
  const showExpiringOnly = showExpiringOnlyProp ?? showExpiringOnlyInternal
  const setShowExpiringOnly = onShowExpiringOnlyChange ?? setShowExpiringOnlyInternal

  const selectedIngredient = useMemo(
    () => ingredientOptions.find((item) => item.id === purchaseForm.ingredientId) ?? null,
    [ingredientOptions, purchaseForm.ingredientId],
  )

  const ingredientUnit = selectedIngredient?.unit ?? ''
  const activeDisplayUnit = purchaseForm.useDefaultUnit ? ingredientUnit : purchaseForm.displayUnit
  const unitLabel = activeDisplayUnit ? ` (${activeDisplayUnit})` : ''

  useEffect(() => {
    if (!documentFile) {
      setDocumentPreviewUrl('')
      return undefined
    }

    const objectUrl = URL.createObjectURL(documentFile)
    setDocumentPreviewUrl(objectUrl)
    return () => URL.revokeObjectURL(objectUrl)
  }, [documentFile])

  const purchaseColumns = useMemo(
    () =>
      buildInventoryPurchaseColumns({
        toPersianNumber,
        formatMoney,
        expirationBadge,
      }),
    [tableReloadToken],
  )

  function handleIngredientChange(ingredientId) {
    const ingredient = ingredientOptions.find((item) => item.id === ingredientId)
    setPurchaseForm((current) => ({
      ...current,
      ingredientId,
      displayUnit: ingredient?.unit ?? '',
      useDefaultUnit: true,
    }))
  }

  function resetForm() {
    setPurchaseForm(emptyPurchaseForm)
    setDocumentFile(null)
    setDocumentPreviewUrl('')
  }

  async function handleSubmit(event) {
    event.preventDefault()
    setSaving(true)
    setError('')
    setMessage('')

    try {
      let documentImageUrl = null

      if (purchaseForm.documentMode === 'url') {
        documentImageUrl = purchaseForm.documentImageUrl.trim() || null
      } else if (documentFile) {
        documentImageUrl = await uploadInventoryDocument(documentFile, session.token)
      }

      await apiRequest('/admin/inventory/purchases', {
        method: 'POST',
        token: session.token,
        body: {
          ingredientId: purchaseForm.ingredientId,
          quantity: purchaseForm.entryMode === 'total' ? Number(purchaseForm.quantity) : null,
          packageCount: purchaseForm.entryMode === 'packages' ? Number(purchaseForm.packageCount) : null,
          packageSize: purchaseForm.entryMode === 'packages' ? Number(purchaseForm.packageSize) : null,
          expirationDate: purchaseForm.expirationDate,
          purchaseUnitPrice: purchaseForm.purchaseUnitPrice ? Number(purchaseForm.purchaseUnitPrice) : null,
          shippingFee: Number(purchaseForm.shippingFee) || 0,
          taxAmount: Number(purchaseForm.taxAmount) || 0,
          incidentalCost: Number(purchaseForm.incidentalCost) || 0,
          description: purchaseForm.description.trim() || null,
          documentImageUrl,
        },
      })

      resetForm()
      setMessage('خرید انبار با موفقیت ثبت شد.')
      bumpTableReload()
    } catch (requestError) {
      setError(getReadableError(requestError))
    } finally {
      setSaving(false)
    }
  }

  const computedTotal =
    purchaseForm.entryMode === 'packages'
      ? (Number(purchaseForm.packageCount) || 0) * (Number(purchaseForm.packageSize) || 0)
      : Number(purchaseForm.quantity) || 0

  const previewSource =
    purchaseForm.documentMode === 'upload'
      ? documentPreviewUrl
      : purchaseForm.documentImageUrl.trim()

  return (
    <section className="panel-stack inventory-purchases">
      <section className="two-column-layout">
        <form className="panel form-panel" onSubmit={handleSubmit}>
          <div className="panel-head">
            <div>
              <h3>ثبت خرید مواد اولیه</h3>
              <p>مقدار، تاریخ انقضا، هزینه‌ها و تصویر سند (اختیاری) را ثبت کنید.</p>
            </div>
            <button type="button" className="ghost-button" onClick={resetForm}>
              پاک کردن فرم
            </button>
          </div>

          <label>
            <span>ماده اولیه</span>
            <select
              required
              value={purchaseForm.ingredientId}
              onChange={(event) => handleIngredientChange(event.target.value)}
            >
              <option value="">-- انتخاب ماده --</option>
              {ingredientOptions.map((item) => (
                <option key={item.id} value={item.id}>
                  {item.persianName} — {item.englishName} ({item.unit})
                </option>
              ))}
            </select>
          </label>

          {selectedIngredient ? (
            <div className="purchase-unit-panel">
              <label className="checkbox-row">
                <input
                  type="checkbox"
                  checked={purchaseForm.useDefaultUnit}
                  onChange={(event) =>
                    setPurchaseForm((current) => ({
                      ...current,
                      useDefaultUnit: event.target.checked,
                      displayUnit: event.target.checked ? ingredientUnit : current.displayUnit || ingredientUnit,
                    }))
                  }
                />
                <span>استفاده از واحد ثبت‌شده ماده ({ingredientUnit || '—'})</span>
              </label>

              {!purchaseForm.useDefaultUnit ? (
                <label>
                  <span>واحد نمایشی (اختیاری)</span>
                  <select
                    className="ltr-input"
                    value={purchaseForm.displayUnit}
                    onChange={(event) => setPurchaseForm((current) => ({ ...current, displayUnit: event.target.value }))}
                  >
                    <option value="">-- انتخاب واحد --</option>
                    {unitOptions.map((unit) => (
                      <option key={unit} value={unit}>
                        {unit}
                      </option>
                    ))}
                  </select>
                </label>
              ) : (
                <p className="purchase-unit-hint">مقادیر را بر اساس واحد <strong className="ltr-text">{ingredientUnit}</strong> وارد کنید.</p>
              )}
            </div>
          ) : null}

          <div className="purchase-mode-toggle">
            <label className="checkbox-row">
              <input
                type="radio"
                name="entryMode"
                checked={purchaseForm.entryMode === 'packages'}
                onChange={() => setPurchaseForm((f) => ({ ...f, entryMode: 'packages' }))}
              />
              <span>بر اساس بسته{unitLabel ? ` (هر بسته به ${activeDisplayUnit || 'واحد ماده'})` : ''}</span>
            </label>
            <label className="checkbox-row">
              <input
                type="radio"
                name="entryMode"
                checked={purchaseForm.entryMode === 'total'}
                onChange={() => setPurchaseForm((f) => ({ ...f, entryMode: 'total' }))}
              />
              <span>مقدار کل مستقیم</span>
            </label>
          </div>

          {purchaseForm.entryMode === 'packages' ? (
            <div className="field-grid">
              <label>
                <span>تعداد بسته</span>
                <input
                  className="ltr-input"
                  type="number"
                  min="1"
                  required
                  value={purchaseForm.packageCount}
                  onChange={(event) => setPurchaseForm((f) => ({ ...f, packageCount: event.target.value }))}
                />
              </label>
              <label>
                <span>اندازه هر بسته{unitLabel}</span>
                <input
                  className="ltr-input"
                  type="number"
                  min="0.001"
                  step="0.001"
                  required
                  value={purchaseForm.packageSize}
                  onChange={(event) => setPurchaseForm((f) => ({ ...f, packageSize: event.target.value }))}
                />
              </label>
            </div>
          ) : (
            <label>
              <span>مقدار کل{unitLabel}</span>
              <input
                className="ltr-input"
                type="number"
                min="0.001"
                step="0.001"
                required
                value={purchaseForm.quantity}
                onChange={(event) => setPurchaseForm((f) => ({ ...f, quantity: event.target.value }))}
              />
            </label>
          )}

          <p className="purchase-computed-total">
            مقدار نهایی: <strong>{toPersianNumber(computedTotal)}</strong>
            {activeDisplayUnit ? <span className="ltr-text"> {activeDisplayUnit}</span> : null}
          </p>

          <label>
            <span>تاریخ انقضا</span>
            <input
              className="ltr-input"
              type="date"
              required
              value={purchaseForm.expirationDate}
              onChange={(event) => setPurchaseForm((f) => ({ ...f, expirationDate: event.target.value }))}
            />
          </label>

          <div className="field-grid">
            <label>
              <span>قیمت واحد (تومان)</span>
              <input
                className="ltr-input"
                type="number"
                min="0"
                step="0.01"
                value={purchaseForm.purchaseUnitPrice}
                onChange={(event) => setPurchaseForm((f) => ({ ...f, purchaseUnitPrice: event.target.value }))}
              />
            </label>
            <label>
              <span>هزینه حمل</span>
              <input
                className="ltr-input"
                type="number"
                min="0"
                step="0.01"
                value={purchaseForm.shippingFee}
                onChange={(event) => setPurchaseForm((f) => ({ ...f, shippingFee: event.target.value }))}
              />
            </label>
            <label>
              <span>مالیات</span>
              <input
                className="ltr-input"
                type="number"
                min="0"
                step="0.01"
                value={purchaseForm.taxAmount}
                onChange={(event) => setPurchaseForm((f) => ({ ...f, taxAmount: event.target.value }))}
              />
            </label>
            <label>
              <span>هزینه‌های متفرقه</span>
              <input
                className="ltr-input"
                type="number"
                min="0"
                step="0.01"
                value={purchaseForm.incidentalCost}
                onChange={(event) => setPurchaseForm((f) => ({ ...f, incidentalCost: event.target.value }))}
              />
            </label>
          </div>

          <label>
            <span>توضیحات</span>
            <textarea
              rows="3"
              value={purchaseForm.description}
              onChange={(event) => setPurchaseForm((f) => ({ ...f, description: event.target.value }))}
            />
          </label>

          <div className="purchase-document-panel">
            <span className="field-label">سند / تصویر (اختیاری)</span>
            <div className="purchase-document-mode">
              <label className="checkbox-row">
                <input
                  type="radio"
                  name="documentMode"
                  checked={purchaseForm.documentMode === 'upload'}
                  onChange={() => setPurchaseForm((current) => ({ ...current, documentMode: 'upload' }))}
                />
                <span>آپلود تصویر</span>
              </label>
              <label className="checkbox-row">
                <input
                  type="radio"
                  name="documentMode"
                  checked={purchaseForm.documentMode === 'url'}
                  onChange={() => setPurchaseForm((current) => ({ ...current, documentMode: 'url' }))}
                />
                <span>آدرس اینترنتی</span>
              </label>
            </div>

            {purchaseForm.documentMode === 'upload' ? (
              <label>
                <span>انتخاب فایل تصویر</span>
                <input
                  type="file"
                  accept="image/jpeg,image/png,image/webp,image/gif"
                  onChange={(event) => setDocumentFile(event.target.files?.[0] ?? null)}
                />
                {documentFile ? <small className="table-subtext ltr-text">{documentFile.name}</small> : null}
              </label>
            ) : (
              <label>
                <span>آدرس تصویر سند</span>
                <input
                  className="ltr-input"
                  type="url"
                  placeholder="https://..."
                  value={purchaseForm.documentImageUrl}
                  onChange={(event) => setPurchaseForm((f) => ({ ...f, documentImageUrl: event.target.value }))}
                />
              </label>
            )}
          </div>

          {previewSource ? (
            <div className="purchase-doc-preview">
              <img src={previewSource} alt="پیش‌نمایش سند" />
            </div>
          ) : null}

          <button type="submit" disabled={saving || !purchaseForm.ingredientId || computedTotal <= 0}>
            {saving ? 'در حال ثبت...' : 'ثبت خرید و افزودن به انبار'}
          </button>
        </form>

        <section className="panel list-panel">
          <div className="panel-head">
            <div>
              <h3>خریدهای ثبت‌شده</h3>
              <p>ردیابی انقضا و موجودی باقی‌مانده از هر خرید</p>
            </div>
            <label className="checkbox-row purchase-filter-toggle">
              <input
                type="checkbox"
                checked={showExpiringOnly}
                onChange={(event) => setShowExpiringOnly(event.target.checked)}
              />
              <span>فقط نزدیک انقضا / منقضی</span>
            </label>
          </div>
          <PaginatedAdminTable
            apiPath={`/admin/inventory/purchases?expiringOnly=${showExpiringOnly}`}
            token={session.token}
            columns={purchaseColumns}
            enabled={Boolean(session.token)}
            reloadToken={tableReloadToken}
            searchPlaceholder="جستجو در خریدهای انبار..."
          />
        </section>
      </section>
    </section>
  )
}
