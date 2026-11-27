import { useCallback, useEffect, useState } from 'react'
import { apiRequest, getReadableError } from '../utils/adminApi.js'
import {
  SMS_STATUS_LABELS,
  formatSmsDateTime,
  toPersian,
  toPersianNumberString,
  toneClass,
} from '../utils/smsHelpers.js'

export function SmsOtpMonitorCard({ token }) {
  const [diag, setDiag] = useState(null)
  const [loading, setLoading] = useState(false)
  const [testMobile, setTestMobile] = useState('')
  const [sendingTest, setSendingTest] = useState(false)
  const [testResult, setTestResult] = useState(null)
  const [refreshingId, setRefreshingId] = useState(null)
  const [error, setError] = useState('')
  const [flash, setFlash] = useState('')

  const loadDiagnostics = useCallback(async () => {
    if (!token) return
    setLoading(true)
    try {
      const data = await apiRequest('/admin/sms/otp-diagnostics?limit=15', { token })
      setDiag(data)
    } catch (err) {
      setError(getReadableError(err))
    } finally {
      setLoading(false)
    }
  }, [token])

  useEffect(() => {
    loadDiagnostics()
  }, [loadDiagnostics])

  const sendTestOtp = async (event) => {
    event.preventDefault()
    if (!testMobile.trim() || sendingTest) return
    setSendingTest(true)
    setError('')
    setFlash('')
    setTestResult(null)
    try {
      const res = await apiRequest('/admin/sms/otp-test', {
        method: 'POST',
        token,
        body: { mobile: testMobile.trim() },
      })
      setTestResult(res)
      if (res?.success) {
        setFlash(res.summary)
      } else {
        setError(res?.error || res?.summary || 'ارسال آزمایشی ناموفق بود.')
      }
      await loadDiagnostics()
    } catch (err) {
      setError(getReadableError(err))
    } finally {
      setSendingTest(false)
    }
  }

  const refreshDeliveryStatus = async (messageId) => {
    if (!messageId || refreshingId) return
    setRefreshingId(messageId)
    setError('')
    setFlash('')
    try {
      const updated = await apiRequest(`/admin/sms/messages/${messageId}/refresh-status`, {
        method: 'POST',
        token,
      })
      const label = SMS_STATUS_LABELS[updated?.status] ?? updated?.status ?? 'به‌روزرسانی شد'
      setFlash(`نتیجه استعلام از سرویس‌دهنده: ${label}`)
      if (testResult?.messageId === messageId) {
        setTestResult((prev) => (prev ? { ...prev, status: updated?.status ?? prev.status } : prev))
      }
      await loadDiagnostics()
    } catch (err) {
      setError(getReadableError(err))
    } finally {
      setRefreshingId(null)
    }
  }

  const r = diag?.readiness
  const s = diag?.stats
  const logs = diag?.recentLogs ?? []

  return (
    <section className="sms-card" aria-label="پایش و تست ارسال کد تایید OTP">
      <div className="sms-row" style={{ justifyContent: 'space-between', alignItems: 'center', marginBottom: 12 }}>
        <div>
          <h2 style={{ margin: 0 }}>پایش و اطمینان از ارسال کد تایید (OTP)</h2>
          <p style={{ margin: '4px 0 0', fontSize: 12, color: 'var(--muted, #6b7280)' }}>
            بررسی وضعیت سرویس OTP، تست واقعی ارسال به موبایل شما، و استعلام تحویل واقعی به گوشی مشتری.
          </p>
        </div>
        <button
          type="button"
          className="sms-inline-btn"
          disabled={loading}
          onClick={loadDiagnostics}
        >
          {loading ? 'در حال بروزرسانی…' : 'بروزرسانی'}
        </button>
      </div>

      {error ? <div className="sms-error" style={{ marginBottom: 10 }}>{error}</div> : null}
      {flash ? <div className="sms-flash" style={{ marginBottom: 10 }}>{flash}</div> : null}

      {r ? (
        <div
          style={{
            display: 'grid',
            gap: 10,
            padding: '12px 14px',
            borderRadius: 12,
            marginBottom: 14,
            border: `1px solid ${r.isReadyForProduction ? 'rgba(34,197,94,0.35)' : 'rgba(239,68,68,0.4)'}`,
            background: r.isReadyForProduction ? 'rgba(34,197,94,0.06)' : 'rgba(239,68,68,0.06)',
          }}
        >
          <div className="sms-row" style={{ justifyContent: 'space-between', alignItems: 'center', gap: 10 }}>
            <strong>
              {r.isReadyForProduction
                ? '✓ سرویس ارسال OTP آماده و فعال است'
                : '⚠ سرویس ارسال OTP نیاز به بررسی دارد'}
            </strong>
            <span className={r.isReadyForProduction ? 'sms-badge sms-badge--ok' : 'sms-badge sms-badge--bad'}>
              {r.providerName ? `${r.providerName} (${r.providerType})` : 'بدون سرویس‌دهنده'}
            </span>
          </div>

          <div className="sms-row" style={{ fontSize: 12, gap: 14, flexWrap: 'wrap' }}>
            <span>روش ارسال: <strong>{r.deliveryModeLabel}</strong></span>
            <span>قالب OTP: <strong>{r.hasOtpTemplate ? r.otpTemplateName : 'متن پیش‌فرض سیستم'}</strong></span>
            <span>استعلام تحویل واقعی: <strong>{r.supportsStatusRefresh ? 'فعال ✓' : 'فقط وب‌هوک'}</strong></span>
          </div>

          {r.warnings?.length ? (
            <ul style={{ margin: 0, paddingInlineStart: 18, fontSize: 12, color: '#b91c1c' }}>
              {r.warnings.map((w) => (
                <li key={w}>{w}</li>
              ))}
            </ul>
          ) : null}
        </div>
      ) : null}

      {s ? (
        <div className="sms-stats" style={{ marginBottom: 14 }}>
          <div className="sms-stat">
            <div className="sms-stat-value">{toPersian(s.totalAttempts7d)}</div>
            <div className="sms-stat-label">کل OTP (۷ روز اخیر)</div>
          </div>
          <div className="sms-stat">
            <div className="sms-stat-value">{toPersian(s.sentOrAccepted7d)}</div>
            <div className="sms-stat-label">پذیرفته‌شده توسط API</div>
          </div>
          <div className="sms-stat">
            <div className="sms-stat-value">{toPersian(s.delivered7d)}</div>
            <div className="sms-stat-label">تحویل قطعی به گوشی</div>
          </div>
          <div className="sms-stat">
            <div className="sms-stat-value">{toPersian(s.verifiedByCustomer7d)}</div>
            <div className="sms-stat-label">کد وارد و تاییدشده توسط مشتری ✓</div>
          </div>
          <div className="sms-stat">
            <div className="sms-stat-value">{toPersian(s.failedOrRejected7d)}</div>
            <div className="sms-stat-label">ناموفق / ردشده</div>
          </div>
        </div>
      ) : null}

      <form
        onSubmit={sendTestOtp}
        className="sms-row"
        style={{
          alignItems: 'flex-end',
          gap: 10,
          padding: '10px 12px',
          borderRadius: 10,
          border: '1px solid var(--border, #e5e7eb)',
          marginBottom: 14,
        }}
      >
        <label style={{ flex: '1 1 220px', margin: 0 }}>
          تست واقعی ارسال کد OTP (مسیر دقیق مشتری)
          <input
            dir="ltr"
            value={testMobile}
            onChange={(e) => setTestMobile(e.target.value)}
            placeholder="09123456789"
            style={{ marginTop: 4 }}
          />
        </label>
        <button
          type="submit"
          className="sms-btn sms-btn--primary"
          disabled={sendingTest || testMobile.trim().length < 10}
          style={{ flex: 'initial' }}
        >
          {sendingTest ? 'در حال ارسال…' : 'ارسال کد OTP آزمایشی'}
        </button>
        {testResult?.messageId ? (
          <button
            type="button"
            className="sms-btn sms-btn--secondary"
            disabled={refreshingId === testResult.messageId}
            onClick={() => refreshDeliveryStatus(testResult.messageId)}
            style={{ flex: 'initial' }}
          >
            {refreshingId === testResult.messageId ? 'در حال استعلام…' : 'استعلام تحویل واقعی'}
          </button>
        ) : null}
      </form>

      {testResult ? (
        <div
          style={{
            fontSize: 12,
            padding: '10px 12px',
            borderRadius: 10,
            marginBottom: 14,
            border: '1px solid var(--border, #e5e7eb)',
            background: 'var(--surface-soft, #f8fafc)',
          }}
        >
          <div>
            وضعیت API: <span className={toneClass(testResult.status)}>{SMS_STATUS_LABELS[testResult.status] ?? testResult.status}</span>
            {testResult.testCode ? <> — کد ارسالی: <strong dir="ltr">{toPersianNumberString(testResult.testCode)}</strong></> : null}
            {testResult.providerMessageId ? <> — شناسه Provider: <code dir="ltr">{testResult.providerMessageId}</code></> : null}
          </div>
          {testResult.error ? <div style={{ color: '#b91c1c', marginTop: 4 }}>خطا: {testResult.error}</div> : null}
        </div>
      ) : null}

      <h3 style={{ fontSize: 14, margin: '8px 0 8px' }}>آخرین کدهای تایید (OTP) ارسالی</h3>
      <table className="sms-table">
        <thead>
          <tr>
            <th>شماره مشتری</th>
            <th>وضعیت پیامک</th>
            <th>تایید توسط مشتری</th>
            <th>سرویس‌دهنده / شناسه</th>
            <th>زمان</th>
            <th>استعلام تحویل</th>
          </tr>
        </thead>
        <tbody>
          {logs.map((row) => (
            <tr key={row.id}>
              <td dir="ltr">{toPersianNumberString(row.displayMobile)}</td>
              <td>
                <span className={toneClass(row.status)}>{SMS_STATUS_LABELS[row.status] ?? row.status}</span>
                {row.errorMessage ? (
                  <div style={{ fontSize: 11, color: '#b91c1c', marginTop: 3 }}>{row.errorMessage}</div>
                ) : null}
              </td>
              <td>
                {row.verifiedByCustomer ? (
                  <span className="sms-badge sms-badge--ok">تایید و وارد شد ✓</span>
                ) : (
                  <span style={{ fontSize: 12, color: 'var(--muted, #6b7280)' }}>در انتظار / تایید نشده</span>
                )}
              </td>
              <td style={{ fontSize: 12 }}>
                {row.providerName ?? '—'}
                {row.providerMessageId ? (
                  <div dir="ltr" style={{ fontSize: 11, color: 'var(--muted, #6b7280)' }}>#{row.providerMessageId}</div>
                ) : null}
              </td>
              <td style={{ fontSize: 12 }}>{formatSmsDateTime(row.requestedAt)}</td>
              <td>
                {row.canRefreshStatus ? (
                  <button
                    type="button"
                    className="sms-inline-btn"
                    disabled={refreshingId === row.id}
                    onClick={() => refreshDeliveryStatus(row.id)}
                  >
                    {refreshingId === row.id ? 'در حال استعلام…' : 'استعلام تحویل'}
                  </button>
                ) : (
                  <span style={{ fontSize: 11, color: 'var(--muted, #6b7280)' }}>—</span>
                )}
              </td>
            </tr>
          ))}
          {!logs.length ? (
            <tr>
              <td colSpan={6}>هنوز هیچ پیامک OTP در سیستم ثبت نشده است.</td>
            </tr>
          ) : null}
        </tbody>
      </table>
    </section>
  )
}
