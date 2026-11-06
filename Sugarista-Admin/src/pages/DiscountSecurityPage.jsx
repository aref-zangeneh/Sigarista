import { useCallback, useEffect, useState } from 'react'
import { useAdmin } from '../context/AdminContext'
import { apiRequest } from '../utils/adminApi'
import { formatDateTime, toPersianNumber } from '../utils/adminFormatters'

const KIND_OPTIONS = [
  { value: 'Phone', label: 'شمارهٔ تلفن' },
  { value: 'IpAddress', label: 'آدرس IP' },
]

export function DiscountSecurityPage() {
  const { session, siteSettings, getReadableError } = useAdmin()
  const token = session?.token

  const [entries, setEntries] = useState([])
  const [attempts, setAttempts] = useState([])
  const [kind, setKind] = useState('Phone')
  const [value, setValue] = useState('')
  const [reason, setReason] = useState('')
  const [identifier, setIdentifier] = useState('')
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState('')
  const [message, setMessage] = useState('')

  const loadEntries = useCallback(async () => {
    if (!token) return
    try {
      const data = await apiRequest('/admin/discount-security/blacklist', { token })
      setEntries(Array.isArray(data) ? data : [])
    } catch (requestError) {
      setError(getReadableError(requestError))
    }
  }, [getReadableError, token])

  const loadAttempts = useCallback(async () => {
    if (!token) return
    try {
      const query = identifier.trim() ? `?identifier=${encodeURIComponent(identifier.trim())}` : ''
      const data = await apiRequest(`/admin/discount-security/attempts${query}`, { token })
      setAttempts(Array.isArray(data) ? data : [])
    } catch (requestError) {
      setError(getReadableError(requestError))
    }
  }, [getReadableError, identifier, token])

  useEffect(() => {
    loadEntries()
  }, [loadEntries])

  useEffect(() => {
    loadAttempts()
  }, [loadAttempts])

  const addEntry = async () => {
    if (!token || busy) return
    if (!value.trim()) {
      setError('مقدار ورودی لیست سیاه را وارد کنید.')
      return
    }
    setBusy(true)
    setError('')
    setMessage('')
    try {
      await apiRequest('/admin/discount-security/blacklist', {
        method: 'POST',
        token,
        body: { kind, value: value.trim(), reason: reason.trim() || null },
      })
      setValue('')
      setReason('')
      await loadEntries()
      setMessage('ورودی به لیست سیاه اضافه شد و از همین لحظه اعمال می‌شود.')
    } catch (requestError) {
      setError(getReadableError(requestError))
    } finally {
      setBusy(false)
    }
  }

  const toggleEntry = async (entry) => {
    if (!token) return
    setError('')
    try {
      await apiRequest(`/admin/discount-security/blacklist/${entry.id}/active`, {
        method: 'PATCH',
        token,
        body: { isActive: !entry.isActive },
      })
      await loadEntries()
    } catch (requestError) {
      setError(getReadableError(requestError))
    }
  }

  const deleteEntry = async (entry) => {
    if (!token) return
    setError('')
    try {
      await apiRequest(`/admin/discount-security/blacklist/${entry.id}`, { method: 'DELETE', token })
      await loadEntries()
      setMessage('ورودی از لیست سیاه حذف شد.')
    } catch (requestError) {
      setError(getReadableError(requestError))
    }
  }

  const clearAttempts = async () => {
    if (!token) return
    setError('')
    setMessage('')
    try {
      const result = await apiRequest(
        `/admin/discount-security/attempts${identifier.trim() ? `?identifier=${encodeURIComponent(identifier.trim())}` : ''}`,
        { method: 'DELETE', token },
      )
      await loadAttempts()
      setMessage(`${toPersianNumber(result?.removed ?? 0)} تلاش پاک شد و قفل موقت برداشته می‌شود.`)
    } catch (requestError) {
      setError(getReadableError(requestError))
    }
  }

  return (
    <section className="panel-stack">
      <section className="panel">
        <div className="panel-head">
          <div>
            <h3>امنیت تخفیف و جلوگیری از سوءاستفاده</h3>
            <p>
              حداکثر تلاش ناموفق، مدت قفل موقت و سقف تخفیف روزانه در «تنظیمات سایت» قابل تغییر است و
              بلافاصله اعمال می‌شود. در این صفحه شماره‌ها و IPهای مسدود و تلاش‌های ثبت‌شده را مدیریت کنید.
            </p>
          </div>
        </div>
        <div className="stat-grid">
          <div className="stat-card">
            <span>وضعیت محافظت</span>
            <strong>{siteSettings.discountAbuseProtectionEnabled === false ? 'غیرفعال' : 'فعال'}</strong>
          </div>
          <div className="stat-card">
            <span>حداکثر تلاش ناموفق</span>
            <strong>{toPersianNumber(siteSettings.discountMaxFailedAttempts ?? 5)}</strong>
          </div>
          <div className="stat-card">
            <span>مدت قفل موقت</span>
            <strong>{toPersianNumber(siteSettings.discountLockoutMinutes ?? 15)} دقیقه</strong>
          </div>
          <div className="stat-card">
            <span>سقف تخفیف روزانه هر کاربر</span>
            <strong>
              {(siteSettings.discountDailyCapPerCustomer ?? 0) > 0
                ? `${Number(siteSettings.discountDailyCapPerCustomer).toLocaleString('fa-IR')} تومان`
                : 'بدون محدودیت'}
            </strong>
          </div>
        </div>
        {error ? <p className="feedback feedback-error">{error}</p> : null}
        {message ? <p className="feedback feedback-success">{message}</p> : null}
      </section>

      <section className="panel">
        <div className="panel-head">
          <div>
            <h3>لیست سیاه شماره و IP</h3>
            <p>کاربران لیست سیاه اجازهٔ استفاده از هیچ کد تخفیفی را ندارند.</p>
          </div>
        </div>

        <div className="field-grid">
          <label>
            <span>نوع</span>
            <select value={kind} onChange={(event) => setKind(event.target.value)}>
              {KIND_OPTIONS.map((option) => (
                <option key={option.value} value={option.value}>{option.label}</option>
              ))}
            </select>
          </label>
          <label>
            <span>{kind === 'Phone' ? 'شمارهٔ تلفن' : 'آدرس IP'}</span>
            <input
              className="ltr-input"
              dir="ltr"
              value={value}
              placeholder={kind === 'Phone' ? '09120000000' : '203.0.113.10'}
              onChange={(event) => setValue(event.target.value)}
            />
          </label>
          <label>
            <span>دلیل (اختیاری)</span>
            <input value={reason} onChange={(event) => setReason(event.target.value)} />
          </label>
          <div className="table-actions">
            <button type="button" onClick={addEntry} disabled={busy}>
              {busy ? 'در حال ذخیره...' : 'افزودن به لیست سیاه'}
            </button>
          </div>
        </div>

        <table className="admin-table" style={{ marginTop: '16px' }}>
          <thead>
            <tr>
              <th>نوع</th>
              <th>مقدار</th>
              <th>دلیل</th>
              <th>وضعیت</th>
              <th>تاریخ ثبت</th>
              <th>عملیات</th>
            </tr>
          </thead>
          <tbody>
            {entries.map((entry) => (
              <tr key={entry.id}>
                <td>{KIND_OPTIONS.find((option) => option.value === entry.kind)?.label ?? entry.kind}</td>
                <td className="ltr-input" dir="ltr">{entry.value}</td>
                <td>{entry.reason || '—'}</td>
                <td>{entry.isActive ? 'فعال' : 'غیرفعال'}</td>
                <td>{formatDateTime(entry.createdAt)}</td>
                <td className="table-actions">
                  <button type="button" className="secondary" onClick={() => toggleEntry(entry)}>
                    {entry.isActive ? 'غیرفعال کردن' : 'فعال کردن'}
                  </button>
                  <button type="button" className="danger" onClick={() => deleteEntry(entry)}>
                    حذف
                  </button>
                </td>
              </tr>
            ))}
            {entries.length === 0 ? (
              <tr>
                <td colSpan="6" className="table-subtext">لیست سیاه خالی است.</td>
              </tr>
            ) : null}
          </tbody>
        </table>
      </section>

      <section className="panel">
        <div className="panel-head">
          <div>
            <h3>تلاش‌های استفاده از کد</h3>
            <p>تلاش‌های ناموفق داخل بازهٔ قفل، باعث قفل موقت می‌شوند. با پاک کردن تلاش‌ها قفل برداشته می‌شود.</p>
          </div>
        </div>

        <div className="field-grid">
          <label>
            <span>شماره یا شناسه (خالی = همه)</span>
            <input
              className="ltr-input"
              dir="ltr"
              value={identifier}
              placeholder="09120000000"
              onChange={(event) => setIdentifier(event.target.value)}
            />
          </label>
          <div className="table-actions">
            <button type="button" className="secondary" onClick={loadAttempts}>اعمال فیلتر</button>
            <button type="button" className="danger" onClick={clearAttempts}>پاک کردن تلاش‌ها</button>
          </div>
        </div>

        <table className="admin-table" style={{ marginTop: '16px' }}>
          <thead>
            <tr>
              <th>شناسه</th>
              <th>کد</th>
              <th>نتیجه</th>
              <th>IP</th>
              <th>زمان</th>
            </tr>
          </thead>
          <tbody>
            {attempts.map((attempt) => (
              <tr key={attempt.id}>
                <td className="ltr-input" dir="ltr">{attempt.identifier}</td>
                <td className="ltr-input" dir="ltr">{attempt.code || '—'}</td>
                <td>{attempt.succeeded ? 'موفق' : 'ناموفق'}</td>
                <td className="ltr-input" dir="ltr">{attempt.ipAddress || '—'}</td>
                <td>{formatDateTime(attempt.attemptedAt)}</td>
              </tr>
            ))}
            {attempts.length === 0 ? (
              <tr>
                <td colSpan="5" className="table-subtext">تلاشی ثبت نشده است.</td>
              </tr>
            ) : null}
          </tbody>
        </table>
      </section>
    </section>
  )
}
