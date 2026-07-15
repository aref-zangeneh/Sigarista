import { useAdmin } from '../context/AdminContext'
import { DataTable } from '../components/DataTable'

export function RecordSalePage() {
  const {
    session,
    menuItemOptions,
    setInventoryStatus,
    saleForm,
    setSaleForm,
    salePreview,
    salePreviewLoading,
    saleResult,
    setSaleResult,
    saving,
    setSaving,
    setMessage,
    setError,
    salePreviewColumns,
    saleResultColumns,
    apiRequest,
    getReadableError,
    toPersianNumber,
  } = useAdmin()

  return (
      <section className="panel-stack">
        <section className="panel">
          <div className="panel-head">
            <div>
              <h3>ثبت فروش</h3>
              <p>اگر محصول از قبل تولید شده باشد، از موجودی یخچال کسر می‌شود؛ در غیر این صورت مواد اولیه مطابق دستور پخت کسر می‌شود.</p>
            </div>
            {saleResult ? (
              <button type="button" className="ghost-button" onClick={() => setSaleResult(null)}>فروش جدید</button>
            ) : null}
          </div>
    
          {!saleResult ? (
            <form
              onSubmit={async (event) => {
                event.preventDefault()
                setSaving(true)
                setError('')
                setMessage('')
                try {
                  const result = await apiRequest('/admin/sales/record', {
                    method: 'POST',
                    token: session.token,
                    body: {
                      menuItemId: saleForm.menuItemId,
                      quantitySold: Number(saleForm.quantitySold),
                    },
                  })
                  setSaleResult(result)
                  setMessage('فروش ثبت شد و موجودی بروزرسانی شد.')
                  const data = await apiRequest('/admin/inventory', { token: session.token })
                  setInventoryStatus(data)
                } catch (requestError) {
                  setError(getReadableError(requestError))
                } finally {
                  setSaving(false)
                }
              }}
            >
              <div className="field-grid">
                <label>
                  <span>آیتم منو</span>
                  <select
                    required
                    value={saleForm.menuItemId}
                    onChange={(event) => setSaleForm((f) => ({ ...f, menuItemId: event.target.value }))}
                  >
                    <option value="">-- انتخاب آیتم --</option>
                    {menuItemOptions.map((item) => (
                      <option key={item.id} value={item.id}>{item.persianName} — {item.englishName}</option>
                    ))}
                  </select>
                </label>
                <label>
                  <span>تعداد فروخته شده</span>
                  <input
                    className="ltr-input"
                    type="number"
                    min="1"
                    required
                    value={saleForm.quantitySold}
                    onChange={(event) => setSaleForm((f) => ({ ...f, quantitySold: event.target.value }))}
                  />
                </label>
              </div>
    
              {salePreviewLoading ? (
                <p className="sale-preview-note">در حال بارگذاری پیش‌نمایش دستور پخت...</p>
              ) : saleForm.menuItemId && salePreview.length === 0 ? (
                <p className="sale-preview-note sale-preview-note--warning">
                  برای این آیتم دستور پختی ثبت نشده. ابتدا در تب «دستور پخت» مواد را تعریف کنید.
                </p>
              ) : salePreview.length > 0 ? (
                <div className="sale-preview">
                  <h4>پیش‌نمایش کسر از موجودی</h4>
                  <DataTable
                    data={salePreview}
                    columns={salePreviewColumns}
                    enableSearch={false}
                    enablePagination={false}
                    compact
                  />
                </div>
              ) : null}
    
              <button type="submit" disabled={saving || salePreview.length === 0}>
                {saving ? 'در حال ثبت...' : 'تأیید و ثبت فروش'}
              </button>
            </form>
          ) : (
            <div className="sale-result">
              <h4>نتیجه ثبت فروش: {saleResult.menuItemName} × {toPersianNumber(saleResult.quantitySold)}</h4>
              {saleResult.deductedFromProducedStock ? (
                <p className="sale-preview-note">
                  از موجودی تولید (یخچال) کسر شد. موجودی باقی‌مانده:{' '}
                  <strong>{toPersianNumber(saleResult.producedStockAfter ?? 0)}</strong> عدد
                </p>
              ) : (
                <DataTable
                  data={saleResult.deductions}
                  columns={saleResultColumns}
                  enableSearch={false}
                  enablePagination={false}
                  compact
                />
              )}
            </div>
          )}
        </section>
      </section>
  )
}
