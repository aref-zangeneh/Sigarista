import { useCallback, useEffect, useMemo, useRef, useState } from 'react'
import { useAdmin } from '../context/AdminContext'
import { apiRequest, getReadableError } from '../utils/adminApi'
import { toPersianNumber } from '../utils/adminFormatters'
import { gregorianIsoToShamsiLabel } from '../utils/shamsiDate'
import { ShamsiDatePicker } from '../components/ShamsiDatePicker'
import { useEscapeKey } from '../hooks/useEscapeKey'
import './AssetsPage.css'

const API_BASE_URL = import.meta.env.VITE_API_BASE_URL ?? '/api'

const CATEGORY_PRESETS = [
  'تجهیزات برقی',
  'تجهیزات آشپزخانه',
  'مبلمان و دکوراسیون',
  'ابزار و ظروف',
  'الکترونیک و نرم‌افزار',
  'نوشیدنی‌ساز',
  'سایر',
]

const LOCATION_PRESETS = ['سالن', 'بار', 'آشپزخانه', 'انبار', 'دفتر', 'فضای بیرونی']

function emptyAssetForm() {
  return {
    id: null,
    persianName: '',
    englishName: '',
    category: CATEGORY_PRESETS[0],
    customCategory: '',
    serialNumber: '',
    location: LOCATION_PRESETS[0],
    purchaseDate: '',
    unitPrice: '',
    quantity: 1,
    supplierName: '',
    note: '',
  }
}

async function uploadAssetPhoto(assetId, file, token) {
  const formData = new FormData()
  formData.append('file', file)
  const response = await fetch(`${API_BASE_URL}/admin/assets/${assetId}/photos`, {
    method: 'POST',
    headers: { ...(token ? { Authorization: `Bearer ${token}` } : {}) },
    body: formData,
  })
  if (!response.ok) {
    let message = `آپلود عکس با خطای ${response.status} مواجه شد.`
    try {
      const payload = await response.json()
      message = payload.detail || payload.title || message
    } catch {}
    throw new Error(message)
  }
  const payload = await response.json()
  return payload
}

export function AssetsPage() {
  const { session, setMessage, setError } = useAdmin()
  const token = session?.token

  const canCreate = has('admin.assets.create')
  const canUpdate = has('admin.assets.update')
  const canDelete = has('admin.assets.delete')

  function has(permissionKey) {
    if (!session) return false
    if (session.isSuperAdmin) return true
    return (session.permissions ?? []).includes(permissionKey)
  }

  const [assets, setAssets] = useState([])
  const [summary, setSummary] = useState(null)
  const [total, setTotal] = useState(0)
  const [page, setPage] = useState(1)
  const [search, setSearch] = useState('')
  const [categoryFilter, setCategoryFilter] = useState('')
  const [activeFilter, setActiveFilter] = useState('active')
  const [loading, setLoading] = useState(true)
  const [form, setForm] = useState(emptyAssetForm)
  const [saving, setSaving] = useState(false)
  const [pendingFiles, setPendingFiles] = useState([])
  const [detail, setDetail] = useState(null)
  const [formOpen, setFormOpen] = useState(false)
  const fileInputRef = useRef(null)

  const load = useCallback(async () => {
    if (!token) return
    setLoading(true)
    try {
      const params = new URLSearchParams({ page, pageSize: 25 })
      if (search.trim()) params.set('search', search.trim())
      if (categoryFilter) params.set('category', categoryFilter)
      if (activeFilter === 'active') params.set('activeOnly', 'true')
      if (activeFilter === 'retired') params.set('activeOnly', 'false')
      const [listResult, summaryResult] = await Promise.all([
        apiRequest(`/admin/assets?${params.toString()}`, { token }),
        apiRequest('/admin/assets/summary', { token }),
      ])
      setAssets(Array.isArray(listResult.items) ? listResult.items : [])
      setTotal(listResult.totalCount ?? 0)
      setSummary(summaryResult)
    } catch (requestError) {
      setError(getReadableError(requestError))
    } finally {
      setLoading(false)
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [activeFilter, categoryFilter, page, search, token])

  useEffect(() => {
    load()
  }, [load])

  const objectUrls = useMemo(
    () => pendingFiles.map((file) => URL.createObjectURL(file)),
    [pendingFiles],
  )
  useEffect(() => () => objectUrls.forEach((url) => URL.revokeObjectURL(url)), [objectUrls])

  const resetFeedback = () => {
    setMessage('')
    setError('')
  }

  const openCreate = () => {
    resetFeedback()
    setForm(emptyAssetForm())
    setPendingFiles([])
    setDetail(null)
    setFormOpen(true)
  }

  const closeForm = () => {
    setFormOpen(false)
  }

  useEscapeKey(formOpen, closeForm)
  useEscapeKey(Boolean(detail) && !formOpen, () => setDetail(null))

  const openEdit = (asset) => {
    resetFeedback()
    setForm({
      id: asset.id,
      persianName: asset.persianName ?? '',
      englishName: asset.englishName ?? '',
      category: CATEGORY_PRESETS.includes(asset.category) ? asset.category : '',
      customCategory: CATEGORY_PRESETS.includes(asset.category) ? '' : asset.category,
      serialNumber: asset.serialNumber ?? '',
      location: asset.location ?? '',
      purchaseDate: asset.purchaseDate ?? '',
      unitPrice: asset.unitPrice ?? '',
      quantity: asset.quantity ?? 1,
      supplierName: asset.supplierName ?? '',
      note: asset.note ?? '',
    })
    setPendingFiles([])
    setFormOpen(true)
  }

  const submit = async (event) => {
    event.preventDefault()
    resetFeedback()
    setSaving(true)
    try {
      const selectedCategory = form.customCategory.trim() || form.category
      const body = {
        persianName: form.persianName.trim(),
        englishName: form.englishName.trim() || null,
        category: selectedCategory,
        serialNumber: form.serialNumber.trim() || null,
        location: form.location.trim(),
        purchaseDate: form.purchaseDate || null,
        unitPrice: Number(form.unitPrice) || 0,
        quantity: Number(form.quantity) || 1,
        supplierName: form.supplierName.trim() || null,
        note: form.note.trim() || null,
      }

      let assetId
      if (form.id) {
        await apiRequest(`/admin/assets/${form.id}`, { method: 'PUT', token, body })
        assetId = form.id
        setMessage('اموال بروزرسانی شد.')
      } else {
        const created = await apiRequest('/admin/assets', { method: 'POST', token, body })
        assetId = created.id
        setMessage('قلم اموال ثبت شد.')
      }

      for (const file of pendingFiles) {
        await uploadAssetPhoto(assetId, file, token)
      }

      setForm(emptyAssetForm())
      setPendingFiles([])
      setFormOpen(false)
      if (fileInputRef.current) fileInputRef.current.value = ''
      await load()
    } catch (requestError) {
      setError(getReadableError(requestError))
    } finally {
      setSaving(false)
    }
  }

  const uploadExistingPhoto = async (asset) => {
    resetFeedback()
    try {
      const input = document.createElement('input')
      input.type = 'file'
      input.accept = 'image/jpeg,image/png,image/webp,image/gif'
      input.onchange = async () => {
        const file = input.files?.[0]
        if (!file) return
        if (file.size > 5 * 1024 * 1024) {
          setError('حجم عکس نباید بیشتر از ۵ مگابایت باشد.')
          return
        }
        try {
          await uploadAssetPhoto(asset.id, file, token)
          setMessage('عکس اضافه شد.')
          setDetail(null)
          await load()
        } catch (uploadError) {
          setError(getReadableError(uploadError))
        }
      }
      input.click()
    } catch (requestError) {
      setError(getReadableError(requestError))
    }
  }

  const setPrimaryPhoto = async (photoId) => {
    resetFeedback()
    try {
      await apiRequest(`/admin/assets/photos/${photoId}/primary`, { method: 'PUT', token })
      setDetail(null)
      await load()
      setMessage('عکس اصلی تغییر یافت.')
    } catch (requestError) {
      setError(getReadableError(requestError))
    }
  }

  const deletePhoto = async (photoId) => {
    resetFeedback()
    try {
      await apiRequest(`/admin/assets/photos/${photoId}`, { method: 'DELETE', token })
      setDetail(null)
      await load()
      setMessage('عکس حذف شد.')
    } catch (requestError) {
      setError(getReadableError(requestError))
    }
  }

  const toggleStatus = async (asset) => {
    resetFeedback()
    try {
      if (asset.isActive) {
        const note = window.prompt('علت خارج از رده شدن (فروش، خرابی، واگذاری...):')
        if (note === null) return
        await apiRequest(`/admin/assets/${asset.id}/status`, {
          method: 'PUT',
          token,
          body: { isActive: false, disposalNote: note || null },
        })
        setMessage('قلم به «خارج از رده» منتقل شد.')
      } else {
        await apiRequest(`/admin/assets/${asset.id}/status`, {
          method: 'PUT',
          token,
          body: { isActive: true, disposalNote: null },
        })
        setMessage('قلم به سرویس برگشت.')
      }
      load()
    } catch (requestError) {
      setError(getReadableError(requestError))
    }
  }

  const remove = async (asset) => {
    resetFeedback()
    if (!window.confirm(`«${asset.persianName}» با تمام عکس‌هایش برای همیشه حذف شود؟`)) return
    try {
      await apiRequest(`/admin/assets/${asset.id}`, { method: 'DELETE', token })
      setMessage('قلم حذف شد.')
      load()
    } catch (requestError) {
      setError(getReadableError(requestError))
    }
  }

  const totalPages = Math.max(1, Math.ceil(total / 25))

  return (
    <div className="admin-assets-page">
      <h1 className="page-title">اموال و تجهیزات کافه</h1>
      <p className="page-subtitle">
        هر قلم اموال دونه‌دونه با تعداد، قیمت واحد و گالری عکس ثبت می‌شود — دفتر اموال دقیق کافه.
      </p>

      {summary ? (
        <div className="stat-grid">
          <div className="stat-card">
            <span>اقلام در سرویس</span>
            <strong>{toPersianNumber(summary.activeItems)}</strong>
          </div>
          <div className="stat-card">
            <span>خارج از رده</span>
            <strong>{toPersianNumber(summary.retiredItems)}</strong>
          </div>
          <div className="stat-card">
            <span>جمع کل واحدها (در سرویس)</span>
            <strong>{toPersianNumber(summary.totalUnits)}</strong>
          </div>
          <div className="stat-card">
            <span>ارزش دفتری فعال</span>
            <strong>{Number(summary.activeValue).toLocaleString('fa-IR')} تومان</strong>
          </div>
          <div className="stat-card">
            <span>ارزش خارج از رده</span>
            <strong>{Number(summary.retiredValue).toLocaleString('fa-IR')} تومان</strong>
          </div>
        </div>
      ) : null}

      <div className="panel">
        <div className="panel-head">
          <div>
            <h3>دفتر اموال</h3>
            <p>جست‌وجو بر اساس نام، شماره سریال یا تأمین‌کننده.</p>
          </div>
          {canCreate ? (
            <button type="button" className="ghost-button" onClick={openCreate}>
              + ثبت قلم جدید
            </button>
          ) : null}
        </div>

        <form
          className="assets-filters"
          onSubmit={(event) => {
            event.preventDefault()
            setPage(1)
            load()
          }}
        >
          <input
            placeholder="جست‌وجو: نام / سریال / تأمین‌کننده..."
            value={search}
            onChange={(event) => setSearch(event.target.value)}
          />
          <select
            value={categoryFilter}
            onChange={(event) => {
              setCategoryFilter(event.target.value)
              setPage(1)
            }}
          >
            <option value="">همهٔ دسته‌ها</option>
            {CATEGORY_PRESETS.map((category) => (
              <option key={category} value={category}>{category}</option>
            ))}
          </select>
          <select
            value={activeFilter}
            onChange={(event) => {
              setActiveFilter(event.target.value)
              setPage(1)
            }}
          >
            <option value="active">در سرویس</option>
            <option value="retired">خارج از رده</option>
            <option value="all">همه</option>
          </select>
          <button type="submit" className="ghost-button">جست‌وجو</button>
        </form>

        {loading ? (
          <p className="feedback">در حال بارگذاری اموال...</p>
        ) : !assets.length ? (
          <p>قلمی مطابق فیلترهای فعلی ثبت نشده است.</p>
        ) : (
          <table className="admin-table">
            <thead>
              <tr>
                <th>عکس</th>
                <th>نام قلم</th>
                <th>دسته</th>
                <th>محل استقرار</th>
                <th>سریال</th>
                <th>تاریخ خرید</th>
                <th>قیمت واحد</th>
                <th>تعداد</th>
                <th>ارزش دفتری</th>
                <th>وضعیت</th>
                <th>عملیات</th>
              </tr>
            </thead>
            <tbody>
              {assets.map((asset) => (
                <tr key={asset.id} className={asset.isActive ? '' : 'is-retired'}>
                  <td>
                    {asset.photos?.length ? (
                      <img
                        className="asset-thumb"
                        src={asset.photos.find((photo) => photo.isPrimary)?.imageUrl ?? asset.photos[0].imageUrl}
                        alt={asset.persianName}
                        onClick={() => setDetail(asset)}
                      />
                    ) : (
                      <span className="asset-thumb asset-thumb-empty" onClick={() => setDetail(asset)}>؟</span>
                    )}
                  </td>
                  <td>
                    <strong>{asset.persianName}</strong>
                    {asset.englishName ? <span className="ltr-text asset-english"> {asset.englishName}</span> : null}
                  </td>
                  <td>{asset.category}</td>
                  <td>{asset.location}</td>
                  <td dir="ltr">{asset.serialNumber ?? '—'}</td>
                  <td>{asset.purchaseDate ? gregorianIsoToShamsiLabel(asset.purchaseDate) : '—'}</td>
                  <td>{Number(asset.unitPrice).toLocaleString('fa-IR')}</td>
                  <td>{toPersianNumber(asset.quantity)}</td>
                  <td><strong>{Number(asset.totalValue).toLocaleString('fa-IR')}</strong></td>
                  <td>
                    {asset.isActive ? (
                      <span className="asset-badge in">در سرویس</span>
                    ) : (
                      <span className="asset-badge out" title={asset.disposalNote ?? ''}>خارج از رده</span>
                    )}
                  </td>
                  <td className="table-actions">
                    {canUpdate ? (
                      <button type="button" className="ghost-button" onClick={() => openEdit(asset)}>
                        ویرایش
                      </button>
                    ) : null}
                    <button type="button" className="ghost-button" onClick={() => setDetail(asset)}>
                      عکس‌ها
                    </button>
                    {canUpdate ? (
                      <button
                        type="button"
                        className="ghost-button"
                        onClick={() => toggleStatus(asset)}
                      >
                        {asset.isActive ? 'خارج از رده' : 'بازگشت'}
                      </button>
                    ) : null}
                    {canDelete ? (
                      <button type="button" className="ghost-button danger" onClick={() => remove(asset)}>
                        حذف
                      </button>
                    ) : null}
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        )}

        {totalPages > 1 ? (
          <div className="admin-pagination">
            <button type="button" disabled={page <= 1} onClick={() => setPage((current) => current - 1)}>
              قبلی
            </button>
            <span>صفحه {toPersianNumber(page)} از {toPersianNumber(totalPages)}</span>
            <button type="button" disabled={page >= totalPages} onClick={() => setPage((current) => current + 1)}>
              بعدی
            </button>
          </div>
        ) : null}
      </div>

      {/* ── create / edit modal ── */}
      {formOpen ? (
        <div className="modal-backdrop" onClick={(event) => { if (event.target === event.currentTarget) closeForm() }}>
          <div
            className="modal assets-form-modal"
            role="dialog"
            aria-modal="true"
            aria-labelledby="asset-form-title"
          >
            <div className="panel-head">
              <div>
                <h3 id="asset-form-title">{form.id ? 'ویرایش قلم اموال' : 'ثبت قلم اموال جدید'}</h3>
                <p>فیلدهای ستاره‌دار الزامی‌اند. تا ۵ عکس می‌توانید ضمیمه کنید.</p>
              </div>
            </div>
            <form onSubmit={submit} className="field-grid">
              <label>
                <span>نام فارسی *</span>
                <input
                  value={form.persianName}
                  onChange={(event) => setForm((current) => ({ ...current, persianName: event.target.value }))}
                  required
                />
              </label>
              <label>
                <span>نام انگلیسی</span>
                <input
                  className="ltr-input"
                  value={form.englishName}
                  onChange={(event) => setForm((current) => ({ ...current, englishName: event.target.value }))}
                />
              </label>
              <label>
                <span>دسته‌بندی *</span>
                <select
                  value={form.category}
                  onChange={(event) => {
                    const value = event.target.value
                    setForm((current) => ({
                      ...current,
                      category: value,
                      customCategory: value === '__custom' ? current.customCategory : '',
                    }))
                  }}
                >
                  {CATEGORY_PRESETS.map((category) => (
                    <option key={category} value={category}>{category}</option>
                  ))}
                  <option value="__custom">دستهٔ دیگر...</option>
                </select>
              </label>
              {form.category === '__custom' ? (
                <label>
                  <span>نام دستهٔ جدید *</span>
                  <input
                    value={form.customCategory}
                    onChange={(event) => setForm((current) => ({ ...current, customCategory: event.target.value }))}
                    required
                  />
                </label>
              ) : null}
              <label>
                <span>محل استقرار *</span>
                <input
                  list="asset-locations"
                  value={form.location}
                  onChange={(event) => setForm((current) => ({ ...current, location: event.target.value }))}
                  required
                />
                <datalist id="asset-locations">
                  {LOCATION_PRESETS.map((location) => (
                    <option key={location} value={location} />
                  ))}
                </datalist>
              </label>
              <label>
                <span>شماره سریال / شناسه</span>
                <input
                  className="ltr-input"
                  value={form.serialNumber}
                  onChange={(event) => setForm((current) => ({ ...current, serialNumber: event.target.value }))}
                />
              </label>
              <label>
                <span>تاریخ خرید</span>
                <ShamsiDatePicker
                  value={form.purchaseDate ?? ''}
                  onChange={(purchaseDate) => setForm((current) => ({ ...current, purchaseDate }))}
                  showHint={false}
                />
              </label>
              <label>
                <span>تأمین‌کننده</span>
                <input
                  value={form.supplierName}
                  onChange={(event) => setForm((current) => ({ ...current, supplierName: event.target.value }))}
                />
              </label>
              <label>
                <span>قیمت واحد (تومان) *</span>
                <input
                  type="number"
                  min="0"
                  value={form.unitPrice}
                  onChange={(event) => setForm((current) => ({ ...current, unitPrice: event.target.value }))}
                  required
                />
              </label>
              <label>
                <span>تعداد *</span>
                <input
                  type="number"
                  min="1"
                  value={form.quantity}
                  onChange={(event) => setForm((current) => ({ ...current, quantity: event.target.value }))}
                  required
                />
              </label>
              <label className="field-full">
                <span>
                  ارزش دفتری: <strong>{(Number(form.unitPrice) * Number(form.quantity)).toLocaleString('fa-IR')} تومان</strong>
                  {' '}(نمایش زنده: قیمت × تعداد)
                </span>
              </label>
              <label className="field-full">
                <span>عکس‌ها (تا ۵ عکس — JPEG/PNG/WebP/GIF، حداکثر ۵MB)</span>
                <input
                  ref={fileInputRef}
                  type="file"
                  accept="image/jpeg,image/png,image/webp,image/gif"
                  multiple
                  onChange={(event) => setPendingFiles([...(event.target.files ?? [])].slice(0, 5))}
                />
              </label>
              {pendingFiles.length ? (
                <div className="field-full asset-photo-previews">
                  {pendingFiles.map((file, index) => (
                    <figure key={objectUrls[index]}>
                      <img src={objectUrls[index]} alt="پیش‌نمایش" />
                      <figcaption>{file.name}</figcaption>
                    </figure>
                  ))}
                </div>
              ) : null}
              <label className="field-full">
                <span>یادداشت</span>
                <textarea
                  rows={2}
                  value={form.note}
                  onChange={(event) => setForm((current) => ({ ...current, note: event.target.value }))}
                />
              </label>
              <div className="modal-actions field-full">
                <button type="submit" className="ghost-button" disabled={saving}>
                  {saving ? 'در حال ذخیره...' : form.id ? 'ذخیره تغییرات' : 'ثبت قلم'}
                </button>
                <button type="button" className="ghost-button" onClick={closeForm}>انصراف</button>
              </div>
            </form>
          </div>
        </div>
      ) : null}

      {/* ── photos gallery modal ── */}
      {detail ? (
        <div className="modal-backdrop" onClick={(event) => { if (event.target === event.currentTarget) setDetail(null) }}>
          <div
            className="modal assets-photos-modal"
            role="dialog"
            aria-modal="true"
            aria-labelledby="asset-photos-title"
          >
            <div className="panel-head">
              <div>
                <h3 id="asset-photos-title">عکس‌های «{detail.persianName}»</h3>
                <p>
                  {detail.category} — {detail.location} — ارزش دفتری {Number(detail.totalValue).toLocaleString('fa-IR')} تومان
                </p>
              </div>
              {canCreate && (detail.photos?.length ?? 0) < 5 ? (
                <button type="button" className="ghost-button" onClick={() => uploadExistingPhoto(detail)}>
                  + افزودن عکس
                </button>
              ) : null}
            </div>
            {!detail.photos?.length ? (
              <p>عکسی ثبت نشده است.</p>
            ) : (
              <div className="assets-photo-gallery">
                {detail.photos.map((photo) => (
                  <figure key={photo.id}>
                    <img src={photo.imageUrl} alt="عکس قلم" />
                    <figcaption>
                      {photo.isPrimary ? <span className="asset-badge in">عکس اصلی</span> : null}
                    </figcaption>
                    <div className="table-actions">
                      {!photo.isPrimary && canUpdate ? (
                        <button type="button" className="ghost-button" onClick={() => setPrimaryPhoto(photo.id)}>
                          اصلی کن
                        </button>
                      ) : null}
                      {canDelete ? (
                        <button type="button" className="ghost-button danger" onClick={() => deletePhoto(photo.id)}>
                          حذف عکس
                        </button>
                      ) : null}
                    </div>
                  </figure>
                ))}
              </div>
            )}
            <div className="modal-actions">
              <button type="button" className="ghost-button" onClick={() => setDetail(null)}>بستن</button>
            </div>
          </div>
        </div>
      ) : null}
    </div>
  )
}
