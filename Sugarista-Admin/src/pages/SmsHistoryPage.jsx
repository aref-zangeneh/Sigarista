import { useCallback, useEffect, useState } from 'react'
import { apiRequest, getReadableError } from '../utils/adminApi'
import { useAdmin } from '../context/AdminContext'
import { toEnglishDigits } from '../utils/persianDigits'
import {
  SMS_KIND_LABELS,
  SMS_SEND_TYPE_LABELS,
  SMS_STATUS_LABELS,
  formatSmsDateTime,
  toPersian,
  toPersianNumberString,
  toneClass,
} from '../utils/smsHelpers'
import './sms.css'

export function SmsHistoryPage() {
  const { session } = useAdmin()
  const [providers, setProviders] = useState([])
  const [rows, setRows] = useState({ items: [], total: 0 })
  const [filters, setFilters] = useState({ mobile: '', providerId: '', status: '', kind: '' })
  const [page, setPage] = useState(1)
  const [pageSize] = useState(20)
  const [detail, setDetail] = useState(null)
  const [error, setError] = useState('')
  const [flash, setFlash] = useState('')

  const load = useCallback(async () => {
    setError('')
    try {
      const params = new URLSearchParams({ page, pageSize })
      if (filters.mobile.trim()) params.set('mobile', filters.mobile.trim())
      if (filters.providerId) params.set('providerId', filters.providerId)
      if (filters.status) params.set('status', filters.status)
      if (filters.kind) params.set('kind', filters.kind)
      const data = await apiRequest(`/admin/sms/messages?${params}`, { token: session.token })
      setRows({ items: data?.items ?? [], total: data?.total ?? 0 })
    } catch (requestError) {
      setError(getReadableError(requestError))
    }
  }, [session.token, page, pageSize, filters])

  useEffect(() => {
    apiRequest('/admin/sms/providers', { token: session.token }).then(setProviders).catch(() => {})
  }, [session.token])

  useEffect(() => {
    if (session.token) load()
  }, [session.token, load])

  const openDetail = async (message) => {
    try {
      setDetail(await apiRequest(`/admin/sms/messages/${message.id}`, { token: session.token }))
    } catch (requestError) {
      setError(getReadableError(requestError))
    }
  }

  const refreshStatus = async (message) => {
    setError('')
    setFlash('')
    try {
      await apiRequest(`/admin/sms/messages/${message.id}/refresh-status`, { method: 'POST', token: session.token })
      setFlash('وضعیت از Provider استعلام و به‌روزرسانی شد.')
      await load()
      if (detail?.id === message.id) await openDetail(message)
    } catch (requestError) {
      setError(getReadableError(requestError))
    } finally {
      setTimeout(() => setFlash(''), 6000)
    }
  }

  const exportCsv = async () => {
    try {
      const params = new URLSearchParams()
      if (filters.mobile.trim()) params.set('mobile', toEnglishDigits(filters.mobile.trim()))
      if (filters.providerId) params.set('providerId', filters.providerId)
      if (filters.status) params.set('status', filters.status)
      if (filters.kind) params.set('kind', filters.kind)
      const response = await fetch(`/api/admin/sms/messages/export?${params}`, {
        headers: { Authorization: `Bearer ${session.token}` },
      })
      const blob = await response.blob()
      const url = URL.createObjectURL(blob)
      const link = document.createElement('a')
      link.href = url
      link.download = 'sms-messages.csv'
      link.click()
      URL.revokeObjectURL(url)
    } catch (requestError) {
      setError(getReadableError(requestError))
    }
  }

  const totalPages = Math.max(1, Math.ceil(rows.total / pageSize))

  return (
    <div className="sms-page" dir="rtl">
      <header className="sms-page-header">
        <h1>تاریخچه و گزارش ارسال‌ها</h1>
        <p>هر گیرنده یک رکورد مستقل دارد؛ وضعیت ارسال و تحویل از هم تفکیک شده‌اند.</p>
      </header>

      {error ? <div className="sms-error">{error}</div> : null}
      {flash ? <div className="sms-flash">{flash}</div> : null}

      <div className="sms-card">
        <div className="sms-row">
          <label style={{ flex: '1 1 180px' }}>
            شمارهٔ موبایل
            <input dir="ltr" value={filters.mobile} onChange={(event) => { setFilters({ ...filters, mobile: event.target.value }); setPage(1) }} />
          </label>
          <label style={{ flex: '1 1 160px' }}>
            سرویس‌دهنده
            <select value={filters.providerId} onChange={(event) => { setFilters({ ...filters, providerId: event.target.value }); setPage(1) }}>
              <option value="">همه</option>
              {providers.map((provider) => (
                <option key={provider.id} value={provider.id}>{provider.name}</option>
              ))}
            </select>
          </label>
          <label style={{ flex: '1 1 150px' }}>
            وضعیت
            <select value={filters.status} onChange={(event) => { setFilters({ ...filters, status: event.target.value }); setPage(1) }}>
              <option value="">همه</option>
              {Object.entries(SMS_STATUS_LABELS).map(([value, label]) => (
                <option key={value} value={value}>{label}</option>
              ))}
            </select>
          </label>
          <label style={{ flex: '1 1 150px' }}>
            نوع
            <select value={filters.kind} onChange={(event) => { setFilters({ ...filters, kind: event.target.value }); setPage(1) }}>
              <option value="">همه</option>
              {Object.entries(SMS_KIND_LABELS).map(([value, label]) => (
                <option key={value} value={value}>{label}</option>
              ))}
            </select>
          </label>
          <button
            type="button"
            className={`sms-btn ${filters.kind === 'Otp' ? 'sms-btn--primary' : 'sms-btn--secondary'}`}
            style={{ flex: 'initial' }}
            onClick={() => {
              setFilters((prev) => ({ ...prev, kind: prev.kind === 'Otp' ? '' : 'Otp' }))
              setPage(1)
            }}
          >
            {filters.kind === 'Otp' ? 'نمایش همه پیامک‌ها' : 'فقط کدهای تایید (OTP)'}
          </button>
          <button type="button" className="sms-btn sms-btn--secondary" style={{ flex: 'initial' }} onClick={exportCsv}>
            خروجی CSV
          </button>
        </div>
      </div>

      <div className="sms-card">
        <table className="sms-table">
          <thead>
            <tr>
              <th>گیرنده</th>
              <th>وضعیت</th>
              <th>نوع</th>
              <th>متن</th>
              <th>زمان</th>
              <th>عملیات</th>
            </tr>
          </thead>
          <tbody>
            {rows.items.map((message) => (
              <tr key={message.id}>
                <td dir="ltr">{toPersianNumberString(message.displayMobile)}</td>
                <td><span className={toneClass(message.status)}>{SMS_STATUS_LABELS[message.status] ?? message.status}</span></td>
                <td>
                  {SMS_KIND_LABELS[message.kind] ?? message.kind}
                  <br />
                  <span style={{ fontSize: 11, color: 'var(--muted, #6b7280)' }}>
                    {SMS_SEND_TYPE_LABELS[message.sendType] ?? message.sendType}
                  </span>
                </td>
                <td style={{ maxWidth: 260 }}>{message.body.slice(0, 50)}{message.body.length > 50 ? '…' : ''}</td>
                <td>{formatSmsDateTime(message.requestedAt)}</td>
                <td className="sms-row" style={{ flex: 'initial' }}>
                  <button type="button" className="sms-inline-btn" onClick={() => openDetail(message)}>جزئیات</button>
                  <button type="button" className="sms-inline-btn" onClick={() => refreshStatus(message)}>استعلام وضعیت</button>
                </td>
              </tr>
            ))}
            {!rows.items.length ? <tr><td colSpan={6}>پیامکی یافت نشد.</td></tr> : null}
          </tbody>
        </table>
        <div className="sms-row" style={{ marginTop: 12, flex: 'initial' }}>
          <button type="button" className="sms-inline-btn" disabled={page <= 1} onClick={() => setPage(page - 1)}>قبلی</button>
          <span style={{ fontSize: 12 }}>صفحه {toPersian(page)} از {toPersian(Math.max(1, Math.ceil(rows.total / pageSize)))} — کل {toPersian(rows.total)}</span>
          <button type="button" className="sms-inline-btn" disabled={page >= totalPages} onClick={() => setPage(page + 1)}>بعدی</button>
        </div>
      </div>

      {detail ? (
        <div className="sms-card">
          <h2>جزئیات پیامک</h2>
          <table className="sms-table">
            <tbody>
              <tr><th>گیرنده</th><td dir="ltr">{toPersianNumberString(detail.displayMobile)}</td></tr>
              <tr><th>وضعیت</th><td>{SMS_STATUS_LABELS[detail.status] ?? detail.status}</td></tr>
              <tr><th>سرویس‌دهنده</th><td>{detail.providerName ?? '—'}</td></tr>
              <tr><th>شناسهٔ Provider</th><td dir="ltr">{detail.providerMessageId ?? '—'}</td></tr>
              <tr><th>پاسخ Provider</th><td>{detail.providerResponse ?? '—'}</td></tr>
              <tr><th>هزینهٔ گزارش‌شده</th><td>{detail.cost ?? '—'}</td></tr>
              <tr><th>بخش‌ها</th><td>{toPersian(detail.segments)}</td></tr>
              <tr><th>تلاش (failover)</th><td>{toPersian(detail.attemptNumber + 1)}</td></tr>
              <tr><th>زمان درخواست</th><td>{formatSmsDateTime(detail.requestedAt)}</td></tr>
              <tr><th>زمان ارسال</th><td>{formatSmsDateTime(detail.sentAt)}</td></tr>
              <tr><th>آخرین تغییر وضعیت</th><td>{formatSmsDateTime(detail.lastStatusAt)}</td></tr>
              <tr><th>خطا</th><td>{detail.errorMessage ?? '—'}</td></tr>
              <tr><th>کاربر</th><td>{detail.requestedBy ?? '—'}</td></tr>
              <tr><th>متن</th><td style={{ whiteSpace: 'pre-wrap' }}>{detail.body}</td></tr>
            </tbody>
          </table>
          <button type="button" className="sms-btn sms-btn--secondary" style={{ marginTop: 10 }} onClick={() => setDetail(null)}>
            بستن
          </button>
        </div>
      ) : null}
    </div>
  )
}
