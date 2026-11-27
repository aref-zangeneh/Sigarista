import { useCallback, useEffect, useState } from 'react'

export function GeminiSettingsPanel({ session, apiRequest, getReadableError }) {
  const [status, setStatus] = useState(null)
  const [loading, setLoading] = useState(true)
  const [apiKeyInput, setApiKeyInput] = useState('')
  const [model, setModel] = useState('gemini-2.5-flash')
  const [saving, setSaving] = useState(false)
  const [testing, setTesting] = useState(false)
  const [feedback, setFeedback] = useState('')
  const [error, setError] = useState('')

  const loadStatus = useCallback(async () => {
    if (!session?.token) return
    setLoading(true)
    try {
      const data = await apiRequest('/admin/ai-content/settings', { token: session.token })
      setStatus(data)
      if (data?.model) setModel(data.model)
    } catch (err) {
      setError(getReadableError(err))
    } finally {
      setLoading(false)
    }
  }, [session?.token, apiRequest, getReadableError])

  useEffect(() => {
    loadStatus()
  }, [loadStatus])

  const handleSave = async (event) => {
    event.preventDefault()
    setSaving(true)
    setError('')
    setFeedback('')
    try {
      const updated = await apiRequest('/admin/ai-content/settings', {
        method: 'PUT',
        token: session.token,
        body: {
          apiKey: apiKeyInput.trim() || null,
          clearApiKey: false,
          model,
        },
      })
      setStatus(updated)
      setApiKeyInput('')
      setFeedback('تنظیمات Gemini با موفقیت ذخیره شد.')
    } catch (err) {
      setError(getReadableError(err))
    } finally {
      setSaving(false)
    }
  }

  const handleClearKey = async () => {
    setSaving(true)
    setError('')
    setFeedback('')
    try {
      const updated = await apiRequest('/admin/ai-content/settings', {
        method: 'PUT',
        token: session.token,
        body: { clearApiKey: true, model },
      })
      setStatus(updated)
      setApiKeyInput('')
      setFeedback('کلید ذخیره‌شده در تنظیمات سایت پاک شد.')
    } catch (err) {
      setError(getReadableError(err))
    } finally {
      setSaving(false)
    }
  }

  const handleTest = async () => {
    setTesting(true)
    setError('')
    setFeedback('')
    try {
      const res = await apiRequest('/admin/ai-content/test', {
        method: 'POST',
        token: session.token,
      })
      if (res?.success) {
        setFeedback(res.message || 'اتصال با موفقیت برقرار شد.')
      } else {
        setError(res?.message || 'تست اتصال ناموفق بود.')
      }
    } catch (err) {
      setError(getReadableError(err))
    } finally {
      setTesting(false)
    }
  }

  const sourceLabel =
    status?.source === 'site_settings'
      ? `فعال (از تنظیمات سایت — ${status.maskedKey})`
      : status?.source === 'environment'
        ? `فعال (از متغیر محیطی GEMINI_API_KEY — ${status.maskedKey})`
        : 'غیرفعال (کلید API تنظیم نشده)'

  return (
    <section id="settings-gemini" className="panel settings-section" style={{ marginTop: '1rem' }}>
      <div className="panel-head">
        <div className="settings-section-head">
          <span className="settings-section-badge" aria-hidden="true">
            ✨
          </span>
          <div>
            <h2>تولید محتوا با Gemini (فقط پیش‌نویس)</h2>
            <p>
              Gemini هیچ اختیاری روی دیتابیس یا ذخیره خودکار ندارد و صرفاً متن پیش‌نویس را در فرم‌های
              بلاگ، آیتم‌های منو، دسته‌بندی‌ها، ویکی محصولات و دستور پخت تولید می‌کند تا پس از بررسی خودتان ذخیره کنید.
            </p>
          </div>
        </div>
      </div>

      {loading ? (
        <p className="feedback">در حال بررسی وضعیت تنظیمات Gemini...</p>
      ) : (
        <form onSubmit={handleSave} style={{ display: 'flex', flexDirection: 'column', gap: '0.85rem' }}>
          <div
            style={{
              padding: '0.65rem 0.9rem',
              borderRadius: '10px',
              border: '1px solid var(--border)',
              background: 'var(--bg)',
              fontSize: '0.82rem',
              display: 'flex',
              alignItems: 'center',
              justifyContent: 'space-between',
              flexWrap: 'wrap',
              gap: '0.5rem',
            }}
          >
            <span>
              وضعیت فعلی:{' '}
              <strong style={{ color: status?.isConfigured ? '#16a34a' : 'var(--muted)' }}>
                {sourceLabel}
              </strong>
            </span>
            <button
              type="button"
              className="ghost-button"
              disabled={testing || !status?.isConfigured}
              onClick={handleTest}
            >
              {testing ? 'در حال تست...' : 'تست اتصال به Gemini'}
            </button>
          </div>

          <div className="settings-fields-grid">
            <label>
              <span>کلید API جمینای (Gemini API Key)</span>
              <input
                type="password"
                className="ltr-input"
                autoComplete="new-password"
                placeholder={
                  status?.isConfigured
                    ? 'برای تغییر کلید، کلید جدید وارد کنید (خالی = بدون تغییر)'
                    : 'AIzaSy...'
                }
                value={apiKeyInput}
                onChange={(e) => setApiKeyInput(e.target.value)}
              />
              <small className="settings-hint">
                کلید صرفاً روی سرور نگهداری می‌شود و هرگز به مرورگر کاربران برنمی‌گردد. در صورت خالی بودن، متغیر محیطی{' '}
                <code>GEMINI_API_KEY</code> استفاده می‌شود.
              </small>
            </label>

            <label>
              <span>مدل Gemini</span>
              <select className="ltr-input" value={model} onChange={(e) => setModel(e.target.value)}>
                {(status?.availableModels ?? ['gemini-2.5-flash', 'gemini-2.5-pro', 'gemini-2.0-flash']).map(
                  (m) => (
                    <option key={m} value={m}>
                      {m}
                    </option>
                  ),
                )}
              </select>
              <small className="settings-hint">
                پیشنهاد ما <code>gemini-2.5-flash</code> است (سرعت بالا و کیفیت عالی برای فارسی).
              </small>
            </label>
          </div>

          {feedback ? <p className="feedback success">{feedback}</p> : null}
          {error ? <p className="feedback error">{error}</p> : null}

          <div style={{ display: 'flex', gap: '0.6rem', justifyContent: 'flex-end', flexWrap: 'wrap' }}>
            {status?.source === 'site_settings' ? (
              <button
                type="button"
                className="ghost-button"
                disabled={saving}
                onClick={handleClearKey}
              >
                حذف کلید ذخیره‌شده
              </button>
            ) : null}
            <button type="submit" className="primary-button" disabled={saving}>
              {saving ? 'در حال ذخیره...' : 'ذخیره تنظیمات Gemini'}
            </button>
          </div>
        </form>
      )}
    </section>
  )
}
