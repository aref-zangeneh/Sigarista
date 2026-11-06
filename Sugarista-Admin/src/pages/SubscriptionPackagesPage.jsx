import { useCallback, useEffect, useState } from 'react'
import { useAdmin } from '../context/AdminContext'
import { formatPrice } from '../utils/posCart'

const emptyPackageForm = {
  id: '',
  name: '',
  description: '',
  totalUseCount: 10,
  price: 0,
  validityDays: 30,
  dailyUseLimit: 0,
  allowedCategoryIds: [],
  displayOrder: 0,
  isEnabled: true,
}

export function SubscriptionPackagesPage() {
  const { session, apiRequest, getReadableError, saving, setSaving, message, error, setMessage, setError } = useAdmin()

  const [packageForm, setPackageForm] = useState(emptyPackageForm)
  const [packages, setPackages] = useState([])
  const [categoryOptions, setCategoryOptions] = useState([])
  const [loading, setLoading] = useState(true)

  const loadPackages = useCallback(async () => {
    if (!session?.token) return
    setLoading(true)
    try {
      const data = await apiRequest('/admin/subscription-packages', { token: session.token })
      setPackages(Array.isArray(data) ? data : [])
    } catch (requestError) {
      setError(getReadableError(requestError))
    } finally {
      setLoading(false)
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [session?.token])

  useEffect(() => {
    loadPackages()
  }, [loadPackages])

  useEffect(() => {
    const loadCategories = async () => {
      if (!session?.token) return
      try {
        const data = await apiRequest('/admin/categories/options', { token: session.token })
        setCategoryOptions(Array.isArray(data) ? data : [])
      } catch {
        setCategoryOptions([])
      }
    }
    loadCategories()
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [session?.token])

  const parseIds = (raw) =>
    (raw ?? '')
      .split(',')
      .map((value) => value.trim())
      .filter(Boolean)

  const startEdit = (pack) => {
    setPackageForm({
      id: pack.id,
      name: pack.name ?? '',
      description: pack.description ?? '',
      totalUseCount: pack.totalUseCount ?? 10,
      price: pack.price ?? 0,
      validityDays: pack.validityDays ?? 30,
      dailyUseLimit: pack.dailyUseLimit ?? 0,
      allowedCategoryIds: parseIds(pack.allowedCategoryIds),
      displayOrder: pack.displayOrder ?? 0,
      isEnabled: pack.isEnabled !== false,
    })
    setMessage('')
    setError('')
  }

  const resetForm = () => setPackageForm(emptyPackageForm)

  const toggleCategory = (categoryId) => {
    setPackageForm((current) => ({
      ...current,
      allowedCategoryIds: current.allowedCategoryIds.includes(categoryId)
        ? current.allowedCategoryIds.filter((id) => id !== categoryId)
        : [...current.allowedCategoryIds, categoryId],
    }))
  }

  const handleDelete = async (pack) => {
    if (!window.confirm(`حذف بستهٔ «${pack.name}»؟\nبسته‌هایی که فروخته شده‌اند قابل حذف نیستند.`)) {
      return
    }

    setSaving(true)
    setError('')
    setMessage('')
    try {
      await apiRequest(`/admin/subscription-packages/${pack.id}`, { method: 'DELETE', token: session.token })
      setMessage('بستهٔ اشتراک حذف شد.')
      if (packageForm.id === pack.id) {
        resetForm()
      }
      loadPackages()
    } catch (requestError) {
      setError(getReadableError(requestError))
    } finally {
      setSaving(false)
    }
  }

  const categoryNames = (raw) => {
    const ids = parseIds(raw)
    if (ids.length === 0) {
      return 'همهٔ محصولات'
    }
    return ids
      .map((id) => categoryOptions.find((option) => option.id === id)?.name)
      .filter(Boolean)
      .join('، ')
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
            await apiRequest('/admin/subscription-packages', {
              method: 'POST',
              token: session.token,
              body: {
                id: packageForm.id || '00000000-0000-0000-0000-000000000000',
                name: packageForm.name,
                description: packageForm.description || null,
                totalUseCount: Number(packageForm.totalUseCount) || 0,
                price: Number(packageForm.price) || 0,
                validityDays: Number(packageForm.validityDays) || 0,
                dailyUseLimit: Number(packageForm.dailyUseLimit) || 0,
                allowedCategoryIds: packageForm.allowedCategoryIds.join(',') || null,
                displayOrder: Number(packageForm.displayOrder) || 0,
                isEnabled: packageForm.isEnabled,
              },
            })
            setMessage(packageForm.id ? 'بستهٔ اشتراک ویرایش شد.' : 'بستهٔ اشتراک ساخته شد.')
            resetForm()
            loadPackages()
          } catch (requestError) {
            setError(getReadableError(requestError))
          } finally {
            setSaving(false)
          }
        }}
      >
        <div className="panel-head">
          <div>
            <h2>{packageForm.id ? 'ویرایش بستهٔ اشتراک' : 'بستهٔ اشتراک جدید'}</h2>
            <p>مثلاً «۱۰ قهوه در ۳۰ روز» — تعداد مصرف، قیمت، اعتبار و سقف روزانه را تعیین کنید.</p>
          </div>
        </div>

        <label>
          <span>نام بسته</span>
          <input
            required
            value={packageForm.name}
            onChange={(event) => setPackageForm((current) => ({ ...current, name: event.target.value }))}
            placeholder="مثلاً اشتراک ماهانهٔ قهوه"
          />
        </label>

        <label>
          <span>توضیح</span>
          <textarea
            rows="2"
            value={packageForm.description}
            onChange={(event) => setPackageForm((current) => ({ ...current, description: event.target.value }))}
          />
        </label>

        <label>
          <span>تعداد استفاده (کل)</span>
          <input
            className="ltr-input"
            dir="ltr"
            type="number"
            min="1"
            value={packageForm.totalUseCount}
            onChange={(event) => setPackageForm((current) => ({ ...current, totalUseCount: event.target.value }))}
          />
        </label>

        <label>
          <span>قیمت کل (تومان)</span>
          <input
            className="ltr-input"
            dir="ltr"
            type="number"
            min="0"
            step="1000"
            value={packageForm.price}
            onChange={(event) => setPackageForm((current) => ({ ...current, price: event.target.value }))}
          />
        </label>

        <label>
          <span>مدت اعتبار (روز)</span>
          <input
            className="ltr-input"
            dir="ltr"
            type="number"
            min="1"
            value={packageForm.validityDays}
            onChange={(event) => setPackageForm((current) => ({ ...current, validityDays: event.target.value }))}
          />
        </label>

        <label>
          <span>سقف مصرف روزانه</span>
          <input
            className="ltr-input"
            dir="ltr"
            type="number"
            min="0"
            value={packageForm.dailyUseLimit}
            onChange={(event) => setPackageForm((current) => ({ ...current, dailyUseLimit: event.target.value }))}
          />
          <small className="settings-hint">مقدار ۰ = بدون سقف روزانه</small>
        </label>

        <div className="full-width">
          <span className="field-label">محصولات مشمول</span>
          <div className="checkbox-grid">
            {categoryOptions.map((category) => (
              <label key={category.id} className="checkbox-row">
                <input
                  type="checkbox"
                  checked={packageForm.allowedCategoryIds.includes(category.id)}
                  onChange={() => toggleCategory(category.id)}
                />
                <span>{category.name}</span>
              </label>
            ))}
            {categoryOptions.length === 0 ? <small className="settings-hint">در حال دریافت دسته‌ها...</small> : null}
          </div>
          <small className="settings-hint">اگر هیچ دسته‌ای انتخاب نشود، همهٔ محصولات مشمول می‌شوند.</small>
        </div>

        <label>
          <span>ترتیب نمایش</span>
          <input
            className="ltr-input"
            dir="ltr"
            type="number"
            min="0"
            value={packageForm.displayOrder}
            onChange={(event) => setPackageForm((current) => ({ ...current, displayOrder: event.target.value }))}
          />
        </label>

        <label className="checkbox-row">
          <input
            type="checkbox"
            checked={packageForm.isEnabled}
            onChange={(event) => setPackageForm((current) => ({ ...current, isEnabled: event.target.checked }))}
          />
          <span>بسته فعال است</span>
        </label>

        <div className="form-actions">
          <button type="submit" disabled={saving}>
            {saving ? 'در حال ذخیره...' : packageForm.id ? 'ذخیره تغییرات' : 'ساخت بسته'}
          </button>
          {packageForm.id ? (
            <button type="button" className="ghost-button" onClick={resetForm}>
              انصراف
            </button>
          ) : null}
        </div>

        {error ? <p className="feedback feedback-error">{error}</p> : null}
        {message ? <p className="feedback feedback-success">{message}</p> : null}
      </form>

      <section className="panel">
        <div className="panel-head">
          <div>
            <h2>بسته‌های اشتراک</h2>
            <p>اشتراک‌ها در پروندهٔ مشتری فعال می‌شوند و هنگام سفارش (آنلاین و صندوق) قابل استفاده‌اند.</p>
          </div>
        </div>

        {loading ? (
          <p>در حال دریافت بسته‌ها...</p>
        ) : packages.length === 0 ? (
          <p>هنوز بسته‌ای ساخته نشده است.</p>
        ) : (
          <table className="admin-table">
            <thead>
              <tr>
                <th>نام</th>
                <th>استفاده</th>
                <th>قیمت</th>
                <th>اعتبار</th>
                <th>سقف روزانه</th>
                <th>محصولات</th>
                <th>وضعیت</th>
                <th />
              </tr>
            </thead>
            <tbody>
              {packages.map((pack) => (
                <tr key={pack.id}>
                  <td>
                    <strong>{pack.name}</strong>
                    {pack.description ? <div className="table-subtext">{pack.description}</div> : null}
                  </td>
                  <td>{pack.totalUseCount}</td>
                  <td>{formatPrice(pack.price)} تومان</td>
                  <td>{pack.validityDays} روز</td>
                  <td>{pack.dailyUseLimit > 0 ? pack.dailyUseLimit : 'بدون سقف'}</td>
                  <td>{categoryNames(pack.allowedCategoryIds)}</td>
                  <td>{pack.isEnabled ? 'فعال' : 'غیرفعال'}</td>
                  <td className="table-actions">
                    <button type="button" className="link-button" onClick={() => startEdit(pack)}>
                      ویرایش
                    </button>
                    <button type="button" className="link-button link-danger" onClick={() => handleDelete(pack)}>
                      حذف
                    </button>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        )}
      </section>
    </section>
  )
}
