import { useCallback, useEffect, useState } from 'react'
import { useAdmin } from '../context/AdminContext'

const emptySupplierForm = {
  id: '',
  persianName: '',
  phoneNumber: '',
  contactName: '',
  notes: '',
  displayOrder: 0,
  isActive: true,
}

export function SuppliersPage() {
  const { session, apiRequest, getReadableError, saving, setSaving, setMessage, setError } = useAdmin()

  const [supplierForm, setSupplierForm] = useState(emptySupplierForm)
  const [suppliers, setSuppliers] = useState([])
  const [loading, setLoading] = useState(true)
  const [includeInactive, setIncludeInactive] = useState(true)

  const loadSuppliers = useCallback(async () => {
    if (!session?.token) return
    setLoading(true)
    try {
      const data = await apiRequest(`/admin/suppliers?includeInactive=${includeInactive}`, { token: session.token })
      setSuppliers(Array.isArray(data) ? data : [])
    } catch (requestError) {
      setError(getReadableError(requestError))
    } finally {
      setLoading(false)
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [session?.token, includeInactive])

  useEffect(() => {
    loadSuppliers()
  }, [loadSuppliers])

  const handleDelete = async (supplier) => {
    if (!window.confirm(`حذف تامین‌کننده «${supplier.persianName}»؟\nارتباطش با اقلام حذف می‌شود اما خریدهای ثبت‌شده حفظ می‌شوند.`)) {
      return
    }

    setSaving(true)
    setError('')
    setMessage('')
    try {
      await apiRequest(`/admin/suppliers/${supplier.id}`, { method: 'DELETE', token: session.token })
      setMessage('تامین‌کننده حذف شد.')
      loadSuppliers()
    } catch (requestError) {
      setError(getReadableError(requestError))
    } finally {
      setSaving(false)
    }
  }

  return (
    <section className="two-column-layout">
      <form
        className="panel form-panel"
        onSubmit={async (event) => {
          event.preventDefault()
          setSaving(true)
          setError('')
          setMessage('')

          try {
            const path = supplierForm.id ? `/admin/suppliers/${supplierForm.id}` : '/admin/suppliers'
            await apiRequest(path, {
              method: supplierForm.id ? 'PUT' : 'POST',
              token: session.token,
              body: {
                persianName: supplierForm.persianName,
                phoneNumber: supplierForm.phoneNumber,
                contactName: supplierForm.contactName,
                notes: supplierForm.notes,
                displayOrder: Number(supplierForm.displayOrder) || 0,
                isActive: supplierForm.isActive,
              },
            })

            setSupplierForm({ ...emptySupplierForm })
            setMessage(supplierForm.id ? 'تامین‌کننده به‌روزرسانی شد.' : 'تامین‌کننده ذخیره شد.')
            loadSuppliers()
          } catch (requestError) {
            setError(getReadableError(requestError))
          } finally {
            setSaving(false)
          }
        }}
      >
        <div className="panel-head">
          <div>
            <h3>{supplierForm.id ? 'ویرایش تامین‌کننده' : 'تامین‌کننده جدید'}</h3>
            <p>تامین‌کنندگان مواد اولیه و بسته‌بندی را ثبت کنید تا در فرم فاکتور ورود سریع انتخاب شوند.</p>
          </div>
          <button type="button" className="ghost-button" onClick={() => setSupplierForm({ ...emptySupplierForm })}>
            پاک کردن فرم
          </button>
        </div>

        <div className="field-grid">
          <label>
            <span>نام شرکت / تامین‌کننده</span>
            <input
              required
              maxLength={200}
              value={supplierForm.persianName}
              onChange={(event) =>
                setSupplierForm((currentForm) => ({ ...currentForm, persianName: event.target.value }))
              }
            />
          </label>
          <label>
            <span>نام فرد رابط (اختیاری)</span>
            <input
              value={supplierForm.contactName}
              onChange={(event) =>
                setSupplierForm((currentForm) => ({ ...currentForm, contactName: event.target.value }))
              }
            />
          </label>
        </div>

        <div className="field-grid">
          <label>
            <span>شماره تماس (اختیاری)</span>
            <input
              className="ltr-input"
              value={supplierForm.phoneNumber}
              onChange={(event) =>
                setSupplierForm((currentForm) => ({ ...currentForm, phoneNumber: event.target.value }))
              }
            />
          </label>
          <label>
            <span>ترتیب نمایش</span>
            <input
              className="ltr-input"
              type="number"
              min="0"
              value={supplierForm.displayOrder}
              onChange={(event) =>
                setSupplierForm((currentForm) => ({ ...currentForm, displayOrder: event.target.value }))
              }
            />
          </label>
        </div>

        <label>
          <span>یادداشت (اختیاری)</span>
          <input
            value={supplierForm.notes}
            onChange={(event) =>
              setSupplierForm((currentForm) => ({ ...currentForm, notes: event.target.value }))
            }
          />
        </label>

        <label className="checkbox-row">
          <input
            type="checkbox"
            checked={supplierForm.isActive}
            onChange={(event) =>
              setSupplierForm((currentForm) => ({ ...currentForm, isActive: event.target.checked }))
            }
          />
          <span>فعال (در فرم‌های خرید قابل انتخاب)</span>
        </label>

        <button type="submit" disabled={saving}>
          {saving ? 'در حال ذخیره...' : 'ذخیره تامین‌کننده'}
        </button>
      </form>

      <section className="panel list-panel">
        <div className="panel-head">
          <div>
            <h3>تامین‌کنندگان</h3>
            <p>برای افزودن تامین‌کننده به یک ماده، از صفحه‌ی مواد اولیه استفاده کنید.</p>
          </div>
          <label className="checkbox-row purchase-filter-toggle">
            <input
              type="checkbox"
              checked={includeInactive}
              onChange={(event) => setIncludeInactive(event.target.checked)}
            />
            <span>نمایش غیرفعال‌ها</span>
          </label>
        </div>

        {loading ? (
          <p className="feedback">در حال بارگذاری تامین‌کنندگان...</p>
        ) : (
          <table>
            <thead>
              <tr>
                <th>نام</th>
                <th>تلفن</th>
                <th>اقلام متصل</th>
                <th>وضعیت</th>
                <th>عملیات</th>
              </tr>
            </thead>
            <tbody>
              {suppliers.map((supplier) => (
                <tr key={supplier.id}>
                  <td>
                    <strong>{supplier.persianName}</strong>
                    {supplier.contactName ? <div className="table-subtext">ارتباط: {supplier.contactName}</div> : null}
                    {supplier.notes ? <div className="table-subtext">{supplier.notes}</div> : null}
                  </td>
                  <td className="ltr-text">{supplier.phoneNumber || '—'}</td>
                  <td>{new Intl.NumberFormat('fa-IR').format(supplier.ingredientCount ?? 0)}</td>
                  <td>{supplier.isActive ? 'فعال' : 'غیرفعال'}</td>
                  <td>
                    <div className="table-actions">
                      <button type="button" className="link-button" onClick={() => setSupplierForm({
                        id: supplier.id,
                        persianName: supplier.persianName,
                        phoneNumber: supplier.phoneNumber ?? '',
                        contactName: supplier.contactName ?? '',
                        notes: supplier.notes ?? '',
                        displayOrder: supplier.displayOrder ?? 0,
                        isActive: supplier.isActive,
                      })}>
                        ویرایش
                      </button>
                      <button type="button" className="link-button link-danger" onClick={() => handleDelete(supplier)}>
                        حذف
                      </button>
                    </div>
                  </td>
                </tr>
              ))}
              {suppliers.length === 0 ? (
                <tr>
                  <td colSpan={5}>هنوز تامین‌کننده‌ای ثبت نشده است.</td>
                </tr>
              ) : null}
            </tbody>
          </table>
        )}
      </section>
    </section>
  )
}
