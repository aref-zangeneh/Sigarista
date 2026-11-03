import { useCallback, useEffect, useState } from 'react'
import { useAdmin } from '../context/AdminContext'
import { formatPrice } from '../utils/posCart'
import { uploadImage } from '../utils/imageUpload'

const emptyBadgeForm = {
  id: '',
  name: '',
  description: '',
  imageUrl: '',
  minPaidOrders: '',
  minTotalSpent: '',
  isEnabled: true,
  displayOrder: 0,
}

function describeConditions(badge) {
  const parts = []
  if (badge.minPaidOrders != null) {
    parts.push(`${badge.minPaidOrders.toString().replace(/\d/g, (d) => '۰۱۲۳۴۵۶۷۸۹'[d])} سفارش پرداخت‌شده`)
  }
  if (badge.minTotalSpent != null) {
    parts.push(`${formatPrice(badge.minTotalSpent)} تومان خرج`)
  }
  return parts.length ? parts.join(' یا ') : 'بدون شرط'
}

export function BadgesPage() {
  const { session, apiRequest, getReadableError, saving, setSaving, message, error, setMessage, setError } = useAdmin()

  const [badgeForm, setBadgeForm] = useState(emptyBadgeForm)
  const [badges, setBadges] = useState([])
  const [loading, setLoading] = useState(true)
  const [uploading, setUploading] = useState(false)
  const [uploadProgress, setUploadProgress] = useState(0)

  const loadBadges = useCallback(async () => {
    if (!session?.token) return
    setLoading(true)
    try {
      const data = await apiRequest('/admin/badges', { token: session.token })
      setBadges(Array.isArray(data) ? data : [])
    } catch (requestError) {
      setError(getReadableError(requestError))
    } finally {
      setLoading(false)
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [session?.token])

  useEffect(() => {
    loadBadges()
  }, [loadBadges])

  const handleImageUpload = async (event) => {
    const file = event.target.files?.[0]
    if (!file) return
    setUploading(true)
    setError('')
    try {
      const result = await uploadImage('/admin/badges/image', file, session.token, {
        onProgress: setUploadProgress,
      })
      setBadgeForm((currentForm) => ({ ...currentForm, imageUrl: result?.url ?? '' }))
      setMessage('تصویر بج آپلود شد.')
    } catch (requestError) {
      setError(getReadableError(requestError))
    } finally {
      setUploading(false)
      setUploadProgress(0)
      event.target.value = ''
    }
  }

  const handleSubmit = async (event) => {
    event.preventDefault()
    if (!badgeForm.name.trim()) return
    if (!badgeForm.minPaidOrders && !badgeForm.minTotalSpent) {
      setError('حداقل یکی از شرط‌های سفارش یا مبلغ را پر کنید.')
      return
    }
    setSaving(true)
    setError('')
    try {
      const payload = {
        id: badgeForm.id ? badgeForm.id : '00000000-0000-0000-0000-000000000000',
        name: badgeForm.name.trim(),
        description: badgeForm.description.trim() || null,
        imageUrl: badgeForm.imageUrl.trim() || null,
        minPaidOrders: badgeForm.minPaidOrders ? Number(badgeForm.minPaidOrders) : null,
        minTotalSpent: badgeForm.minTotalSpent ? Number(badgeForm.minTotalSpent) : null,
        isEnabled: badgeForm.isEnabled,
        displayOrder: Number(badgeForm.displayOrder) || 0,
      }
      await apiRequest(badgeForm.id ? `/admin/badges/${badgeForm.id}` : '/admin/badges', {
        method: badgeForm.id ? 'PUT' : 'POST',
        body: payload,
        token: session.token,
      })
      setBadgeForm({ ...emptyBadgeForm })
      setMessage(badgeForm.id ? 'بج به‌روزرسانی شد.' : 'بج ذخیره شد.')
      loadBadges()
    } catch (requestError) {
      setError(getReadableError(requestError))
    } finally {
      setSaving(false)
    }
  }

  const handleEdit = (badge) => {
    setBadgeForm({
      id: badge.id,
      name: badge.name ?? '',
      description: badge.description ?? '',
      imageUrl: badge.imageUrl ?? '',
      minPaidOrders: badge.minPaidOrders ?? '',
      minTotalSpent: badge.minTotalSpent ?? '',
      isEnabled: badge.isEnabled !== false,
      displayOrder: badge.displayOrder ?? 0,
    })
    setError('')
    setMessage('')
    window.scrollTo({ top: 0, behavior: 'smooth' })
  }

  const handleDelete = async (badge) => {
    if (!window.confirm(`بج «${badge.name}» حذف شود؟ بج‌های کسب‌شدهٔ مشتریان هم حذف می‌شوند.`)) return
    setError('')
    try {
      await apiRequest(`/admin/badges/${badge.id}`, { method: 'DELETE', token: session.token })
      setMessage('بج حذف شد.')
      if (badgeForm.id === badge.id) {
        setBadgeForm({ ...emptyBadgeForm })
      }
      loadBadges()
    } catch (requestError) {
      setError(getReadableError(requestError))
    }
  }

  const handleToggle = async (badge) => {
    setError('')
    try {
      await apiRequest(`/admin/badges/${badge.id}`, {
        method: 'PUT',
        body: {
          id: badge.id,
          name: badge.name,
          description: badge.description ?? null,
          imageUrl: badge.imageUrl ?? null,
          minPaidOrders: badge.minPaidOrders ?? null,
          minTotalSpent: badge.minTotalSpent ?? null,
          isEnabled: !badge.isEnabled,
          displayOrder: badge.displayOrder ?? 0,
        },
        token: session.token,
      })
      loadBadges()
    } catch (requestError) {
      setError(getReadableError(requestError))
    }
  }

  return (
    <section className="two-column-layout">
      <form className="panel form-panel" onSubmit={handleSubmit}>
        <div className="panel-head">
          <div>
            <h3>{badgeForm.id ? 'ویرایش بج' : 'بج جدید'}</h3>
            <p>بج‌های باشگاه مشتریان — به‌محض رسیدن مشتری به شرط، خودکار به پروفایلش داده می‌شود.</p>
          </div>
          <button type="button" className="ghost-button" onClick={() => setBadgeForm({ ...emptyBadgeForm })}>
            پاک کردن فرم
          </button>
        </div>

        {error ? <p className="feedback feedback-error">{error}</p> : null}
        {message ? <p className="feedback feedback-success">{message}</p> : null}

        <div className="field-grid">
          <label>
            <span>نام بج</span>
            <input
              required
              maxLength={100}
              placeholder="مثلاً: مشتری طلایی"
              value={badgeForm.name}
              onChange={(event) => setBadgeForm((current) => ({ ...current, name: event.target.value }))}
            />
          </label>
          <label>
            <span>ترتیب نمایش</span>
            <input
              className="ltr-input"
              type="number"
              min="0"
              value={badgeForm.displayOrder}
              onChange={(event) => setBadgeForm((current) => ({ ...current, displayOrder: event.target.value }))}
            />
          </label>
        </div>

        <label>
          <span>توضیحات (اختیاری)</span>
          <input
            maxLength={500}
            value={badgeForm.description}
            onChange={(event) => setBadgeForm((current) => ({ ...current, description: event.target.value }))}
          />
        </label>

        {/* آپلود تصویر بج */}
        <div className="badge-image-editor">
          <span>تصویر بج (svg / png / jpeg)</span>
          <div className="badge-image-row">
            {badgeForm.imageUrl ? (
              <img className="badge-image-preview" src={badgeForm.imageUrl} alt="پیش‌نمایش بج" />
            ) : (
              <span className="badge-image-placeholder">
                <i className="bi bi-image" />
              </span>
            )}
            <div className="badge-image-actions">
              <input
                type="file"
                accept=".svg,.png,.jpg,.jpeg,image/svg+xml,image/png,image/jpeg"
                onChange={handleImageUpload}
                disabled={uploading}
                className="ltr-text"
              />
              {badgeForm.imageUrl ? (
                <button type="button" className="link-button" onClick={() => setBadgeForm((current) => ({ ...current, imageUrl: '' }))}>
                  حذف تصویر
                </button>
              ) : null}
              {uploading ? <p className="table-subtext">در حال آپلود... {uploadProgress}٪</p> : null}
              <p className="table-subtext">بدون تصویر، آیکون پیش‌فرض نمایش داده می‌شود.</p>
            </div>
          </div>
        </div>

        {/* شرط‌های کسب */}
        <div className="form-section">
          <h4>شرط‌های خودکار کسب (حداقل یکی)</h4>
          <p className="table-subtext">
            مشتری وقتی بج را می‌گیرد که هر دو شرط تعریف‌شده برقرار باشند؛ اگر فقط یکی تعریف شود همان ملاک است.
          </p>
          <div className="field-grid">
            <label>
              <span>حداقل تعداد سفارش پرداخت‌شده</span>
              <input
                className="ltr-input"
                type="number"
                min="0"
                placeholder="مثلاً: ۱۰"
                value={badgeForm.minPaidOrders}
                onChange={(event) => setBadgeForm((current) => ({ ...current, minPaidOrders: event.target.value }))}
              />
            </label>
            <label>
              <span>حداقل مجموع خرید (تومان)</span>
              <input
                className="ltr-input"
                type="number"
                min="0"
                step="10000"
                placeholder="مثلاً: ۱۰۰۰۰۰۰"
                value={badgeForm.minTotalSpent}
                onChange={(event) => setBadgeForm((current) => ({ ...current, minTotalSpent: event.target.value }))}
              />
            </label>
          </div>
          <label className="checkbox-row">
            <input
              type="checkbox"
              checked={badgeForm.isEnabled}
              onChange={(event) => setBadgeForm((current) => ({ ...current, isEnabled: event.target.checked }))}
            />
            <span>فعال (به مشتری‌های واجد شرط داده می‌شود)</span>
          </label>
        </div>

        <button type="submit" disabled={saving || uploading}>
          {saving ? 'در حال ذخیره...' : 'ذخیره بج'}
        </button>
      </form>

      <div className="panel list-panel-wrap">
        <section className="panel list-panel">
          <div className="panel-head">
            <div>
              <h3>بج‌های باشگاه</h3>
              <p>در پروفایل کارت مهر مشتری، بج‌های کسب‌شده برجسته و بقیه کم‌رنگ نمایش داده می‌شوند.</p>
            </div>
          </div>

          {loading ? (
            <p className="feedback">در حال بارگذاری بج‌ها...</p>
          ) : (
            <table>
              <thead>
                <tr>
                  <th>بج</th>
                  <th>شرط خودکار</th>
                  <th>دارنده</th>
                  <th>وضعیت</th>
                  <th>عملیات</th>
                </tr>
              </thead>
              <tbody>
                {badges.map((badge) => (
                  <tr key={badge.id} className={badge.isEnabled ? '' : 'row-muted'}>
                    <td>
                      <div className="badge-cell">
                        {badge.imageUrl ? (
                          <img className="badge-cell-image" src={badge.imageUrl} alt={badge.name} />
                        ) : (
                          <span className="badge-cell-fallback">
                            <i className="bi bi-person-badge" />
                          </span>
                        )}
                        <div>
                          <strong>{badge.name}</strong>
                          {badge.description ? <div className="table-subtext">{badge.description}</div> : null}
                        </div>
                      </div>
                    </td>
                    <td>{describeConditions(badge)}</td>
                    <td>{badge.holders}</td>
                    <td>{badge.isEnabled ? 'فعال' : 'غیرفعال'}</td>
                    <td>
                      <div className="table-actions">
                        <button type="button" className="link-button" onClick={() => handleEdit(badge)}>
                          ویرایش
                        </button>
                        <button type="button" className="link-button" onClick={() => handleToggle(badge)}>
                          {badge.isEnabled ? 'غیرفعال' : 'فعال'}
                        </button>
                        <button type="button" className="link-button link-danger" onClick={() => handleDelete(badge)}>
                          حذف
                        </button>
                      </div>
                    </td>
                  </tr>
                ))}
                {badges.length === 0 ? (
                  <tr>
                    <td colSpan={5}>هنوز بجی ثبت نشده است.</td>
                  </tr>
                ) : null}
              </tbody>
            </table>
          )}
        </section>
      </div>
    </section>
  )
}
