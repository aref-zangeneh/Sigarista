import { useMemo, useState } from 'react'
import { DataTable } from './DataTable.jsx'
import { ImageUploader } from './ImageUploader.jsx'
import { apiRequest, getReadableError } from '../utils/adminApi.js'
import { toPersianNumber } from '../utils/adminFormatters.js'
import { HIGHLIGHT_THEME_OPTIONS } from '../utils/blogAppearance.js'

const QUICK_ICONS = ['☕', '🥐', '✨', '🫘', '🍰', '🧊', '🍵', '💡']

function emptyHighlightForm() {
  return {
    id: null,
    title: '',
    subtitle: '',
    badgeText: 'نکته باریستا',
    iconText: '☕',
    imageUrl: '',
    linkUrl: '/mag',
    linkLabel: 'مطالعه بیشتر',
    themeStyle: 'teal',
    displayOrder: 1,
    isActive: true,
  }
}

export function BlogHighlightsPanel({
  highlights,
  loading,
  token,
  canCreate,
  canUpdate,
  canDelete,
  onReload,
  setError,
  setMessage,
}) {
  const [form, setForm] = useState(emptyHighlightForm)
  const [saving, setSaving] = useState(false)

  const handleSave = async (event) => {
    event.preventDefault()
    if (!token) return
    if (!form.title.trim()) {
      setError('لطفاً «عنوان کارت ویژه» را وارد کنید.')
      return
    }
    if (!form.subtitle.trim()) {
      setError('لطفاً «توضیح کوتاه کارت ویژه» را وارد کنید.')
      return
    }

    setSaving(true)
    setError('')
    setMessage('')
    try {
      const payload = {
        title: form.title.trim(),
        subtitle: form.subtitle.trim(),
        badgeText: form.badgeText.trim(),
        iconText: form.iconText.trim() || '☕',
        imageUrl: form.imageUrl.trim() || null,
        linkUrl: form.linkUrl.trim() || null,
        linkLabel: form.linkLabel.trim() || null,
        themeStyle: form.themeStyle || 'teal',
        displayOrder: Math.max(0, Number(form.displayOrder) || 0),
        isActive: Boolean(form.isActive),
      }

      const isEdit = Boolean(form.id)
      await apiRequest(
        isEdit ? `/admin/blog/highlights/${form.id}` : '/admin/blog/highlights',
        {
          method: isEdit ? 'PUT' : 'POST',
          body: payload,
          token,
        },
      )

      setMessage(
        isEdit
          ? 'کارت ویژه مجله با موفقیت ویرایش شد.'
          : 'کارت ویژه جدید به صفحه اول مجله اضافه شد.',
      )
      setForm(emptyHighlightForm())
      await onReload()
    } catch (err) {
      setError(getReadableError(err))
    } finally {
      setSaving(false)
    }
  }

  const handleDelete = async (row) => {
    if (!token || !canDelete) return
    if (!window.confirm(`آیا از حذف کارت ویژه «${row.title}» مطمئن هستید؟`)) return
    setError('')
    setMessage('')
    try {
      await apiRequest(`/admin/blog/highlights/${row.id}`, {
        method: 'DELETE',
        token,
      })
      if (form.id === row.id) {
        setForm(emptyHighlightForm())
      }
      setMessage('کارت ویژه حذف شد.')
      await onReload()
    } catch (err) {
      setError(getReadableError(err))
    }
  }

  const columns = useMemo(
    () => [
      {
        accessorKey: 'displayOrder',
        header: 'ترتیب',
        cell: ({ getValue }) => toPersianNumber(getValue() ?? 0),
      },
      {
        accessorKey: 'title',
        header: 'کارت ویژه',
        cell: ({ row }) => {
          const item = row.original
          const themeInfo =
            HIGHLIGHT_THEME_OPTIONS.find((t) => t.value === item.themeStyle) ??
            HIGHLIGHT_THEME_OPTIONS[0]
          return (
            <div className="blog-hl-table-cell">
              <span
                className="blog-hl-mini-icon"
                style={{ background: themeInfo.color, color: '#fff' }}
              >
                {item.iconText || '☕'}
              </span>
              <div>
                <strong>{item.title}</strong>
                {item.badgeText ? (
                  <span className="blog-badge blog-badge--featured">{item.badgeText}</span>
                ) : null}
                <div className="blog-field-hint">{item.subtitle}</div>
              </div>
            </div>
          )
        },
      },
      {
        accessorKey: 'themeStyle',
        header: 'رنگ کارت',
        cell: ({ getValue }) => {
          const val = getValue() || 'teal'
          const opt =
            HIGHLIGHT_THEME_OPTIONS.find((t) => t.value === val) ?? HIGHLIGHT_THEME_OPTIONS[0]
          return (
            <span>
              <span className="blog-color-swatch" style={{ background: opt.color }} />
              {opt.label.split(' ')[0]} {opt.label.split(' ')[1]}
            </span>
          )
        },
      },
      {
        accessorKey: 'isActive',
        header: 'وضعیت نمایش',
        cell: ({ getValue }) => (
          <span className={`blog-badge ${getValue() ? 'blog-badge--published' : 'blog-badge--draft'}`}>
            {getValue() ? 'فعال در مجله' : 'مخفی'}
          </span>
        ),
      },
      {
        id: 'actions',
        header: 'عملیات',
        enableSorting: false,
        cell: ({ row }) => {
          const item = row.original
          return (
            <div className="table-actions">
              {canUpdate ? (
                <button
                  type="button"
                  className="link-button"
                  onClick={() =>
                    setForm({
                      id: item.id,
                      title: item.title ?? '',
                      subtitle: item.subtitle ?? '',
                      badgeText: item.badgeText ?? '',
                      iconText: item.iconText ?? '☕',
                      imageUrl: item.imageUrl ?? '',
                      linkUrl: item.linkUrl ?? '',
                      linkLabel: item.linkLabel ?? '',
                      themeStyle: item.themeStyle ?? 'teal',
                      displayOrder: item.displayOrder ?? 0,
                      isActive: Boolean(item.isActive),
                    })
                  }
                >
                  ویرایش
                </button>
              ) : null}
              {canDelete ? (
                <button
                  type="button"
                  className="link-button link-danger"
                  onClick={() => handleDelete(item)}
                >
                  حذف
                </button>
              ) : null}
            </div>
          )
        },
      },
    ],
    [canUpdate, canDelete],
  )

  return (
    <section className="two-column-layout">
      {canCreate || (canUpdate && form.id) ? (
        <form className="panel form-panel" onSubmit={handleSave}>
          <div className="panel-head">
            <div>
              <h3>{form.id ? 'ویرایش کارت ویژه مجله' : 'افزودن کارت ویژه جدید'}</h3>
              <p>
                کارت‌های ویژه در ردیف رنگی بالای صفحه اول مجله (/mag) نمایش داده می‌شوند (مناسب نکات کوتاه باریستا، معرفی آیتم‌های روز و پیشنهادهای ویژه).
              </p>
            </div>
            {form.id ? (
              <button
                type="button"
                className="ghost-button"
                onClick={() => setForm(emptyHighlightForm())}
              >
                انصراف از ویرایش
              </button>
            ) : null}
          </div>

          <label>
            <span>عنوان کارت ویژه *</span>
            <input
              type="text"
              required
              maxLength={160}
              placeholder="مثلاً: راز کرمای طلایی اسپرسو"
              value={form.title}
              onChange={(e) => setForm((prev) => ({ ...prev, title: e.target.value }))}
            />
          </label>

          <label>
            <span>توضیح کوتاه داخل کارت *</span>
            <textarea
              rows={3}
              required
              maxLength={400}
              placeholder="توضیح جذاب ۲ تا ۳ خطی برای بازدیدکنندگان مجله..."
              value={form.subtitle}
              onChange={(e) => setForm((prev) => ({ ...prev, subtitle: e.target.value }))}
            />
          </label>

          <div className="field-grid">
            <label>
              <span>متن برچسب گوشه بالا (اختیاری)</span>
              <input
                type="text"
                maxLength={60}
                placeholder="مثلاً: نکته باریستا"
                value={form.badgeText}
                onChange={(e) => setForm((prev) => ({ ...prev, badgeText: e.target.value }))}
              />
            </label>

            <label>
              <span>تم رنگی کارت در مجله</span>
              <select
                value={form.themeStyle}
                onChange={(e) => setForm((prev) => ({ ...prev, themeStyle: e.target.value }))}
              >
                {HIGHLIGHT_THEME_OPTIONS.map((opt) => (
                  <option key={opt.value} value={opt.value}>
                    {opt.label}
                  </option>
                ))}
              </select>
            </label>
          </div>

          <label>
            <span>نماد یا آیکون کارت</span>
            <div className="blog-icon-picker-row">
              <input
                type="text"
                maxLength={16}
                style={{ maxWidth: '88px', textAlign: 'center', fontSize: '1.2rem' }}
                value={form.iconText}
                onChange={(e) => setForm((prev) => ({ ...prev, iconText: e.target.value }))}
              />
              <div className="blog-quick-icons">
                {QUICK_ICONS.map((ic) => (
                  <button
                    key={ic}
                    type="button"
                    className={`blog-quick-icon-btn ${form.iconText === ic ? 'is-selected' : ''}`}
                    onClick={() => setForm((prev) => ({ ...prev, iconText: ic }))}
                  >
                    {ic}
                  </button>
                ))}
              </div>
            </div>
          </label>

          <ImageUploader
            label="تصویر کوچک کارت (اختیاری — در صورت انتخاب، جایگزین آیکون می‌شود)"
            hint="مربعی ۱:۱ پیشنهاد می‌شود"
            value={form.imageUrl}
            token={token}
            onChange={(url) => setForm((prev) => ({ ...prev, imageUrl: url ?? '' }))}
          />

          <div className="field-grid">
            <label>
              <span>لینک مقصد کلیک (اختیاری)</span>
              <input
                className="ltr-input"
                type="text"
                maxLength={400}
                placeholder="/mag یا /"
                value={form.linkUrl}
                onChange={(e) => setForm((prev) => ({ ...prev, linkUrl: e.target.value }))}
              />
            </label>

            <label>
              <span>متن دکمه لینک</span>
              <input
                type="text"
                maxLength={80}
                placeholder="مثلاً: مطالعه بیشتر"
                value={form.linkLabel}
                onChange={(e) => setForm((prev) => ({ ...prev, linkLabel: e.target.value }))}
              />
            </label>
          </div>

          <div className="field-grid">
            <label>
              <span>ترتیب نمایش (از کوچک به بزرگ)</span>
              <input
                className="ltr-input"
                type="number"
                min={0}
                value={form.displayOrder}
                onChange={(e) => setForm((prev) => ({ ...prev, displayOrder: e.target.value }))}
              />
            </label>

            <label className="checkbox-row" style={{ alignSelf: 'end', paddingBottom: '10px' }}>
              <input
                type="checkbox"
                checked={form.isActive}
                onChange={(e) => setForm((prev) => ({ ...prev, isActive: e.target.checked }))}
              />
              <span>فعال و قابل نمایش در مجله</span>
            </label>
          </div>

          {/* پیش‌نمایش زنده کارت ویژه */}
          <div className={`blog-hl-live-preview is-${form.themeStyle}`}>
            <div className="blog-hl-live-top">
              <span className="blog-hl-live-icon">{form.iconText || '☕'}</span>
              {form.badgeText ? <span className="blog-hl-live-badge">{form.badgeText}</span> : null}
            </div>
            <strong>{form.title || 'عنوان کارت ویژه شما'}</strong>
            <p>{form.subtitle || 'توضیح کوتاه کارت ویژه در اینجا نمایش داده می‌شود...'}</p>
            {form.linkUrl ? (
              <small>{form.linkLabel || 'مشاهده بیشتر'} ←</small>
            ) : null}
          </div>

          <button type="submit" disabled={saving}>
            {saving
              ? 'در حال ذخیره...'
              : form.id
                ? 'ذخیره تغییرات کارت ویژه'
                : 'افزودن کارت ویژه به مجله'}
          </button>
        </form>
      ) : null}

      <section className="panel list-panel">
        <div className="panel-head">
          <div>
            <h3>لیست کارت‌های ویژه بالای مجله</h3>
            <p>
              می‌توانید هر تعداد کارت ویژه بسازید، رنگ و متن آن‌ها را ویرایش کنید یا آن‌ها را حذف نمایید.
            </p>
          </div>
        </div>
        <DataTable
          data={highlights}
          columns={columns}
          loading={loading}
          emptyMessage="هنوز کارت ویژه‌ای ثبت نشده است."
        />
      </section>
    </section>
  )
}
