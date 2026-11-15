import { useCallback, useEffect, useState } from 'react'
import { apiRequest, getReadableError } from '../utils/adminApi'
import { useAdmin } from '../context/AdminContext'
import { toPersian } from '../utils/smsHelpers'
import './sms.css'

const DEFAULTS = {
  maxRecipientsPerSend: 5000,
  batchSize: 50,
  maxRetries: 2,
  retryDelaySeconds: 15,
  dailyMessageLimit: 2000,
  bulkEnabled: true,
  failoverEnabled: false,
  failoverProviderId: null,
  historyRetentionDays: 90,
}

export function SmsSettingsPage() {
  const { session } = useAdmin()
  const [settings, setSettings] = useState(DEFAULTS)
  const [providers, setProviders] = useState([])
  const [flash, setFlash] = useState('')
  const [error, setError] = useState('')
  const [busy, setBusy] = useState(false)

  const load = useCallback(async () => {
    try {
      const data = await apiRequest('/admin/sms/settings', { token: session.token })
      setSettings({ ...DEFAULTS, ...data })
    } catch (requestError) {
      setError(getReadableError(requestError))
    }
    apiRequest('/admin/sms/providers', { token: session.token }).then(setProviders).catch(() => {})
  }, [session.token])

  useEffect(() => {
    if (session.token) load()
  }, [session.token, load])

  const save = async () => {
    setBusy(true)
    setError('')
    setFlash('')
    try {
      const saved = await apiRequest('/admin/sms/settings', {
        method: 'PUT',
        token: session.token,
        body: {
          ...settings,
          maxRecipientsPerSend: Number(settings.maxRecipientsPerSend),
          batchSize: Number(settings.batchSize),
          maxRetries: Number(settings.maxRetries),
          retryDelaySeconds: Number(settings.retryDelaySeconds),
          dailyMessageLimit: Number(settings.dailyMessageLimit),
          historyRetentionDays: Number(settings.historyRetentionDays),
        },
      })
      setSettings({ ...DEFAULTS, ...saved })
      setFlash('تنظیمات ذخیره شد.')
    } catch (requestError) {
      setError(getReadableError(requestError))
    } finally {
      setBusy(false)
      setTimeout(() => setFlash(''), 6000)
    }
  }

  const prune = async () => {
    setBusy(true)
    setError('')
    setFlash('')
    try {
      const removed = await apiRequest('/admin/sms/history/prune', { method: 'POST', token: session.token })
      setFlash(`پاک‌سازی انجام شد (${toPersian(removed)} ردیف واجد شرایط).`)
    } catch (requestError) {
      setError(getReadableError(requestError))
    } finally {
      setBusy(false)
      setTimeout(() => setFlash(''), 6000)
    }
  }

  return (
    <div className="sms-page" dir="rtl">
      <header className="sms-page-header">
        <h1>تنظیمات پیامک</h1>
        <p>سیاست‌های عمومی ارسال — مستقل از appsettings و قابل تغییر در زمان اجرا.</p>
      </header>

      {error ? <div className="sms-error">{error}</div> : null}
      {flash ? <div className="sms-flash">{flash}</div> : null}

      <div className="sms-card">
        <div className="sms-form">
          <div className="sms-row">
            <label>حداکثر گیرنده در هر ارسال
              <input type="number" min={1} value={settings.maxRecipientsPerSend} onChange={(event) => setSettings({ ...settings, maxRecipientsPerSend: event.target.value })} />
            </label>
            <label>اندازهٔ Batch
              <input type="number" min={1} max={500} value={settings.batchSize} onChange={(event) => setSettings({ ...settings, batchSize: event.target.value })} />
            </label>
          </div>
          <div className="sms-row">
            <label>تعداد Retry (خطاهای موقت)
              <input type="number" min={0} max={5} value={settings.maxRetries} onChange={(event) => setSettings({ ...settings, maxRetries: event.target.value })} />
            </label>
            <label>فاصلهٔ Retry (ثانیه)
              <input type="number" min={1} max={600} value={settings.retryDelaySeconds} onChange={(event) => setSettings({ ...settings, retryDelaySeconds: event.target.value })} />
            </label>
          </div>
          <div className="sms-row">
            <label>سقف ارسال روزانه
              <input type="number" min={1} value={settings.dailyMessageLimit} onChange={(event) => setSettings({ ...settings, dailyMessageLimit: event.target.value })} />
            </label>
            <label>نگهداری تاریخچه (روز)
              <input type="number" min={7} max={730} value={settings.historyRetentionDays} onChange={(event) => setSettings({ ...settings, historyRetentionDays: event.target.value })} />
            </label>
          </div>
          <div className="sms-row" style={{ flex: 'initial' }}>
            <label className="sms-checkbox-row">
              <input type="checkbox" checked={settings.bulkEnabled} onChange={(event) => setSettings({ ...settings, bulkEnabled: event.target.checked })} />
              ارسال گروهی فعال باشد
            </label>
            <label className="sms-checkbox-row">
              <input type="checkbox" checked={settings.failoverEnabled} onChange={(event) => setSettings({ ...settings, failoverEnabled: event.target.checked })} />
              Failover فعال باشد
            </label>
          </div>
          {settings.failoverEnabled ? (
            <label>
              سرویس‌دهندهٔ جایگزین (Failover)
              <select
                value={settings.failoverProviderId ?? ''}
                onChange={(event) => setSettings({ ...settings, failoverProviderId: event.target.value || null })}
              >
                <option value="">انتخاب کنید…</option>
                {providers.map((provider) => (
                  <option key={provider.id} value={provider.id}>{provider.name}</option>
                ))}
              </select>
            </label>
          ) : null}
          <p style={{ fontSize: 12, color: 'var(--muted, #6b7280)', margin: 0 }}>
            نکتهٔ امنیتی: Timeout مبهم هرگز باعث ارسال تکراری نمی‌شود — حتی با Retry و Failover فعال.
          </p>
          <div className="sms-actions">
            <button type="button" className="sms-btn" disabled={busy} onClick={save}>ذخیرهٔ تنظیمات</button>
            <button type="button" className="sms-btn sms-btn--secondary" disabled={busy} onClick={prune}>
              پاک‌سازی تاریخچهٔ قدیمی
            </button>
          </div>
        </div>
      </div>
    </div>
  )
}
