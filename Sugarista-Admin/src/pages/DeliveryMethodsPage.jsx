import { useCallback, useEffect, useState } from 'react'
import { useAdmin } from '../context/AdminContext'
import { formatPrice } from '../utils/posCart'

const emptyMethodForm = {
  id: '',
  persianName: '',
  englishName: '',
  description: '',
  deliveryFee: 0,
  displayOrder: 0,
  isActive: true,
  packagings: [],
}

const emptyPackagingRow = { ingredientId: '', quantityPerOrder: 1 }

export function DeliveryMethodsPage() {
  const { session, apiRequest, getReadableError, saving, setSaving, message, error, setMessage, setError } = useAdmin()

  const [methodForm, setMethodForm] = useState(emptyMethodForm)
  const [packagingRow, setPackagingRow] = useState(emptyPackagingRow)
  const [methods, setMethods] = useState([])
  const [loading, setLoading] = useState(true)
  const [packagingOptions, setPackagingOptions] = useState([])

  const loadMethods = useCallback(async () => {
    if (!session?.token) return
    setLoading(true)
    try {
      const data = await apiRequest('/admin/delivery-methods?includeInactive=true', { token: session.token })
      setMethods(Array.isArray(data) ? data : [])
    } catch (requestError) {
      setError(getReadableError(requestError))
    } finally {
      setLoading(false)
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [session?.token])

  useEffect(() => {
    loadMethods()
  }, [loadMethods])

  useEffect(() => {
    // packaging choices: only active packaging items
    const loadPackagingOptions = async () => {
      if (!session?.token) return
      try {
        const data = await apiRequest('/admin/ingredients/options', { token: session.token })
        setPackagingOptions((Array.isArray(data) ? data : []).filter((option) => option.kind === 'packaging' && option.isActive))
      } catch {
        // options are auxiliary; page stays usable without them
      }
    }
    loadPackagingOptions()
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [session?.token])

  const handleDelete = async (method) => {
    if (!window.confirm(`حذف روش تحویل «${method.persianName}»؟\nسفارش‌های قبلی حفظ می‌شوند اما این روش از صندوق فروش حذف می‌شود.`)) {
      return
    }

    setSaving(true)
    setError('')
    setMessage('')
    try {
      await apiRequest(`/admin/delivery-methods/${method.id}`, { method: 'DELETE', token: session.token })
      setMessage('روش تحویل حذف شد.')
      loadMethods()
    } catch (requestError) {
      setError(getReadableError(requestError))
    } finally {
      setSaving(false)
    }
  }

  const addPackagingRow = () => {
    if (!packagingRow.ingredientId) {
      setError('یک قلم بسته‌بندی انتخاب کنید.')
      return
    }
    if (Number(packagingRow.quantityPerOrder) <= 0) {
      setError('تعداد مصرف در هر سفارش باید بیشتر از صفر باشد.')
      return
    }
    setMethodForm((currentForm) => ({
      ...currentForm,
      packagings: [
        ...currentForm.packagings.filter((line) => line.ingredientId !== packagingRow.ingredientId),
        { ingredientId: packagingRow.ingredientId, quantityPerOrder: Number(packagingRow.quantityPerOrder) },
      ],
    }))
    setPackagingRow(emptyPackagingRow)
    setError('')
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
            const path = methodForm.id ? `/admin/delivery-methods/${methodForm.id}` : '/admin/delivery-methods'
            await apiRequest(methodForm.id ? path : '/admin/delivery-methods', {
              method: methodForm.id ? 'PUT' : 'POST',
              token: session.token,
              body: {
                id: methodForm.id || null,
                persianName: methodForm.persianName,
                englishName: methodForm.englishName || methodForm.persianName,
                deliveryFee: Number(methodForm.deliveryFee) || 0,
                description: methodForm.description,
                displayOrder: Number(methodForm.displayOrder) || 0,
                isActive: methodForm.isActive,
                packagings: methodForm.packagings.map((line) => ({
                  ingredientId: line.ingredientId,
                  quantityPerOrder: Number(line.quantityPerOrder),
                })),
              },
            })

            setMethodForm({ ...emptyMethodForm })
            setMessage(methodForm.id ? 'روش تحویل به‌روزرسانی شد.' : 'روش تحویل ذخیره شد.')
            loadMethods()
          } catch (requestError) {
            setError(getReadableError(requestError))
          } finally {
            setSaving(false)
          }
        }}
      >
        <div className="panel-head">
          <div>
            <h3>{methodForm.id ? 'ویرایش روش تحویل' : 'روش تحویل جدید'}</h3>
            <p>روش‌های تحویل سفارش را با هزینه و ست بسته‌بندی مصرفی مدیریت کنید.</p>
          </div>
          <button type="button" className="ghost-button" onClick={() => setMethodForm({ ...emptyMethodForm })}>
            پاک کردن فرم
          </button>
        </div>

        {error ? <p className="feedback feedback-error">{error}</p> : null}
        {message ? <p className="feedback feedback-success">{message}</p> : null}

        <div className="field-grid">
          <label>
            <span>نام روش تحویل</span>
            <input
              required
              maxLength={150}
              placeholder="مثلاً: بیرون‌بر (بگ و کاپ‌هولدر)"
              value={methodForm.persianName}
              onChange={(event) =>
                setMethodForm((currentForm) => ({ ...currentForm, persianName: event.target.value }))
              }
            />
          </label>
          <label>
            <span>نام انگلیسی</span>
            <input
              className="ltr-input"
              maxLength={150}
              placeholder="Takeaway Set"
              value={methodForm.englishName}
              onChange={(event) =>
                setMethodForm((currentForm) => ({ ...currentForm, englishName: event.target.value }))
              }
            />
          </label>
        </div>

        <div className="field-grid">
          <label>
            <span>هزینه تحویل (تومان)</span>
            <input
              className="ltr-input"
              type="number"
              min="0"
              step="1000"
              value={methodForm.deliveryFee}
              onChange={(event) =>
                setMethodForm((currentForm) => ({ ...currentForm, deliveryFee: event.target.value }))
              }
            />
          </label>
          <label>
            <span>ترتیب نمایش</span>
            <input
              className="ltr-input"
              type="number"
              min="0"
              value={methodForm.displayOrder}
              onChange={(event) =>
                setMethodForm((currentForm) => ({ ...currentForm, displayOrder: event.target.value }))
              }
            />
          </label>
        </div>

        <label>
          <span>توضیحات (اختیاری)</span>
          <input
            value={methodForm.description}
            onChange={(event) =>
              setMethodForm((currentForm) => ({ ...currentForm, description: event.target.value }))
            }
          />
        </label>

        <label className="checkbox-row">
          <input
            type="checkbox"
            checked={methodForm.isActive}
            onChange={(event) =>
              setMethodForm((currentForm) => ({ ...currentForm, isActive: event.target.checked }))
            }
          />
          <span>فعال (در صندوق فروش قابل انتخاب)</span>
        </label>

        <div className="form-section">
          <h4>ست بسته‌بندی مصرفی (کسر خودکار از انبار در هر سفارش)</h4>
          <p className="table-subtext">
            هر سفارش که با این روش تحویل بیاید، به‌اندازه‌ی مقادیر زیر از انبار بسته‌بندی کسر می‌شود — مثلاً سفارش بیرون‌بر: بگ × ۱ و کاپ‌هولدر × ۱؛ سفارش سالن: بدون کسر.
          </p>

          <div className="packaging-row-editor">
            <label>
              <span>قلم بسته‌بندی</span>
              <select
                value={packagingRow.ingredientId}
                onChange={(event) => setPackagingRow((current) => ({ ...current, ingredientId: event.target.value }))}
              >
                <option value="">-- انتخاب قلم --</option>
                {packagingOptions.map((option) => (
                  <option key={option.id} value={option.id}>
                    {option.persianName} ({option.unit})
                  </option>
                ))}
              </select>
            </label>
            <label>
              <span>تعداد در هر سفارش</span>
              <input
                className="ltr-input"
                type="number"
                min="1"
                step="1"
                value={packagingRow.quantityPerOrder}
                onChange={(event) => setPackagingRow((current) => ({ ...current, quantityPerOrder: event.target.value }))}
              />
            </label>
            <button type="button" className="ghost-button" onClick={addPackagingRow}>
              افزودن به ست
            </button>
          </div>

          <ul className="packaging-set-list">
            {methodForm.packagings.map((line) => {
              const option = packagingOptions.find((entry) => entry.id === line.ingredientId)
              return (
                <li key={line.ingredientId}>
                  <span>
                    {option?.persianName ?? 'قلم بسته‌بندی'} — {formatPrice(line.quantityPerOrder)} ×
                  </span>
                  <div className="table-actions">
                    <button
                      type="button"
                      className="link-button link-danger"
                      onClick={() =>
                        setMethodForm((currentForm) => ({
                          ...currentForm,
                          packagings: currentForm.packagings.filter(
                            (candidate) => candidate.ingredientId !== line.ingredientId,
                          ),
                        }))
                      }
                    >
                      حذف
                    </button>
                  </div>
                </li>
              )
            })}
            {methodForm.packagings.length === 0 ? <li className="table-subtext">بدون بسته‌بندی خودکار (مثل سفارش سالن).</li> : null}
          </ul>
        </div>

        <button type="submit" disabled={saving}>
          {saving ? 'در حال ذخیره...' : 'ذخیره روش تحویل'}
        </button>
      </form>

      <section className="panel list-panel">
        <div className="panel-head">
          <div>
            <h3>روش‌های تحویل</h3>
            <p>هزینه‌ها در جمع کل صندوق فروش اعمال و ست بسته‌بندی در لحظه‌ی پرداخت از انبار کسر می‌شود.</p>
          </div>
        </div>

        {loading ? (
          <p className="feedback">در حال بارگذاری روش‌های تحویل...</p>
        ) : (
          <table>
            <thead>
              <tr>
                <th>نام</th>
                <th>هزینه</th>
                <th>ست بسته‌بندی</th>
                <th>وضعیت</th>
                <th>عملیات</th>
              </tr>
            </thead>
            <tbody>
              {methods.map((method) => (
                <tr key={method.id}>
                  <td>
                    <strong>{method.persianName}</strong>
                    <div className="table-subtext ltr-text">{method.englishName}</div>
                    {method.description ? <div className="table-subtext">{method.description}</div> : null}
                  </td>
                  <td>{method.deliveryFee > 0 ? `${formatPrice(method.deliveryFee)} تومان` : 'رایگان'}</td>
                  <td>
                    {method.packagings?.length
                      ? method.packagings
                          .map((line) => `${line.ingredientPersianName} × ${formatPrice(line.quantityPerOrder)}`)
                          .join('، ')
                      : '—'}
                  </td>
                  <td>{method.isActive ? 'فعال' : 'غیرفعال'}</td>
                  <td>
                    <div className="table-actions">
                      <button
                        type="button"
                        className="link-button"
                        onClick={() =>
                          setMethodForm({
                            id: method.id,
                            persianName: method.persianName,
                            englishName: method.englishName ?? '',
                            description: method.description ?? '',
                            deliveryFee: method.deliveryFee ?? 0,
                            displayOrder: method.displayOrder ?? 0,
                            isActive: method.isActive,
                            packagings: (method.packagings ?? []).map((line) => ({
                              ingredientId: line.ingredientId,
                              quantityPerOrder: line.quantityPerOrder,
                            })),
                          })
                        }
                      >
                        ویرایش
                      </button>
                      <button type="button" className="link-button link-danger" onClick={() => handleDelete(method)}>
                        حذف
                      </button>
                    </div>
                  </td>
                </tr>
              ))}
              {methods.length === 0 ? (
                <tr>
                  <td colSpan={5}>هنوز روش تحویلی ثبت نشده است.</td>
                </tr>
              ) : null}
            </tbody>
          </table>
        )}
      </section>
    </section>
  )
}
