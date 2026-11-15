import { useEffect, useState } from 'react'
import { apiRequest, getReadableError } from '../utils/adminApi'
import { useAdmin } from '../context/AdminContext'
import { SMS_KIND_LABELS } from '../utils/smsHelpers'
import './sms.css'

export function SmsSendPage() {
  const { session } = useAdmin()
  const [providers, setProviders] = useState([])
  const [templates, setTemplates] = useState([])
  const [mobile, setMobile] = useState('')
  const [providerId, setProviderId] = useState('')
  const [templateId, setTemplateId] = useState('')
  const [body, setBody] = useState('')
  const [kind, setKind] = useState('Service')
  const [preview, setPreview] = useState(null)
  const [result, setResult] = useState(null)
  const [sending, setSending] = useState(false)
  const [flash, setFlash] = useState('')
  const [error, setError] = useState('')

  useEffect(() => {
    apiRequest('/admin/sms/providers', { token: session.token })
      .then(setProviders)
      .catch(() => {})
    apiRequest('/admin/sms/templates', { token: session.token })
      .then((list) => setTemplates((list ?? []).filter((template) => template.isActive)))
      .catch(() => {})
  }, [session.token])

  const runPreview = async () => {
    setError('')
    const text = templateId ? templates.find((template) => template.id === templateId)?.body : body
    if (!mobile.trim() && !text) return
    try {
      const data = await apiRequest('/admin/sms/templates/preview', {
        method: 'POST',
        token: session.token,
        body: { body: text ?? '' },
      })
      setPreview(data)
    } catch (requestError) {
      setError(getReadableError(requestError))
    }
  }

  const send = async () => {
    setSending(true)
    setError('')
    setFlash('')
    setResult(null)
    try {
      const data = await apiRequest('/admin/sms/send', {
        method: 'POST',
        token: session.token,
        body: {
          mobile,
          providerId: providerId || null,
          templateId: templateId || null,
          body: templateId ? null : body,
          kind,
        },
      })
      setResult(data)
      if (data.success) {
        setFlash('پیامک با موفقیت ثبت و ارسال شد.')
      }
      // One-shot guard: clear the number so a stray re-click can't double-send.
      setMobile('')
    } catch (requestError) {
      setError(getReadableError(requestError))
    } finally {
      setSending(false)
      setTimeout(() => setFlash(''), 6000)
    }
  }

  const canSend = mobile.trim().length >= 10 && (templateId || body.trim())

  return (
    <div className="sms-page" dir="rtl">
      <header className="sms-page-header">
        <h1>ارسال پیامک تکی</h1>
        <p>ارسال فوری به یک شماره — با پیش‌نمایش متن و شمارش بخش‌ها.</p>
      </header>

      {error ? <div className="sms-error">{error}</div> : null}
      {flash ? <div className="sms-flash">{flash}</div> : null}

      <div className="sms-card">
        <div className="sms-form">
          <label>
            شمارهٔ موبایل گیرنده
            <input
              value={mobile}
              onChange={(event) => setMobile(event.target.value)}
              placeholder="09121234567"
              dir="ltr"
            />
          </label>
          <div className="sms-row">
            <label>
              سرویس‌دهنده
              <select value={providerId} onChange={(event) => setProviderId(event.target.value)}>
                <option value="">پیش‌فرض سیستم</option>
                {providers.map((provider) => (
                  <option key={provider.id} value={provider.id}>
                    {provider.name}{provider.isEnabled ? '' : ' (غیرفعال)'}
                  </option>
                ))}
              </select>
            </label>
            <label>
              نوع پیامک
              <select value={kind} onChange={(event) => setKind(event.target.value)}>
                {Object.entries(SMS_KIND_LABELS).map(([value, label]) => (
                  <option key={value} value={value}>{label}</option>
                ))}
              </select>
            </label>
          </div>
          <label>
            قالب پیامک (اختیاری)
            <select value={templateId} onChange={(event) => { setTemplateId(event.target.value); setPreview(null) }}>
              <option value="">متن آزاد</option>
              {templates.map((template) => (
                <option key={template.id} value={template.id}>{template.name}</option>
              ))}
            </select>
          </label>
          {!templateId ? (
            <label>
              متن پیامک
              <textarea
                value={body}
                onChange={(event) => setBody(event.target.value)}
                placeholder="متن پیامک…"
              />
            </label>
          ) : null}
          <div className="sms-actions">
            <button type="button" className="sms-btn sms-btn--secondary" onClick={runPreview}>
              پیش‌نمایش
            </button>
            <button type="button" className="sms-btn" disabled={!canSend || sending} onClick={send}>
              {sending ? 'در حال ارسال…' : 'ارسال نهایی'}
            </button>
          </div>
        </div>
      </div>

      {preview ? (
        <div className="sms-card">
          <h2>پیش‌نمایش (با مقادیر نمونهٔ متغیرها)</h2>
          <div className="sms-preview">{preview.renderedBody}</div>
          <p style={{ fontSize: 12, color: 'var(--muted, #6b7280)' }}>
            {preview.characters} کاراکتر — {preview.segments} بخش پیامک —{' '}
            {preview.unicode ? 'یونیکد (70/67)' : 'GSM-7 (160/153)'}
            {preview.placeholders?.length ? ` — متغیرها: ${preview.placeholders.join('، ')}` : ''}
          </p>
        </div>
      ) : null}

      {result ? (
        <div className="sms-card">
          <h2>نتیجهٔ ارسال</h2>
          <p>
            وضعیت: <strong>{result.status}</strong> —{' '}
            {result.success ? 'موفق' : `ناموفق: ${result.error ?? 'خطای نامشخص'}`}
          </p>
          {result.messageId ? <p>شناسهٔ داخلی پیامک: <code dir="ltr">{result.messageId}</code></p> : null}
          {result.providerMessageId ? <p>شناسهٔ Provider: <code dir="ltr">{result.providerMessageId}</code></p> : null}
        </div>
      ) : null}
    </div>
  )
}
