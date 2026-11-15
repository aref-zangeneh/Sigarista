import { useEffect, useState } from 'react'
import { apiRequest, getReadableError } from '../utils/adminApi'
import { useAdmin } from '../context/AdminContext'
import {
  SMS_KIND_LABELS,
  campaignStatusLabel,
  formatSmsDateTime,
  formatToman,
  toPersian,
} from '../utils/smsHelpers'
import './sms.css'

function parseManualRecipients(raw) {
  return raw
    .split(/[\n,،;\s]+/)
    .map((entry) => entry.trim())
    .filter(Boolean)
}

export function SmsBulkPage() {
  const { session } = useAdmin()
  const [groups, setGroups] = useState([])
  const [providers, setProviders] = useState([])
  const [templates, setTemplates] = useState([])
  const [selectedGroups, setSelectedGroups] = useState([])
  const [manualNumbers, setManualNumbers] = useState('')
  const [csvRows, setCsvRows] = useState([])
  const [providerId, setProviderId] = useState('')
  const [senderLine, setSenderLine] = useState('')
  const [scheduledAt, setScheduledAt] = useState('')
  const [templateId, setTemplateId] = useState('')
  const [body, setBody] = useState('')
  const [kind, setKind] = useState('Marketing')
  const [title, setTitle] = useState('')
  const [preview, setPreview] = useState(null)
  const [campaigns, setCampaigns] = useState([])
  const [submitting, setSubmitting] = useState(false)
  const [flash, setFlash] = useState('')
  const [error, setError] = useState('')

  const selectedProvider = providers.find((provider) => provider.id === providerId)
  const providerLines = selectedProvider?.senderLines ?? []

  const loadCampaigns = async () => {
    try {
      const data = await apiRequest('/admin/sms/campaigns?page=1&pageSize=10', { token: session.token })
      setCampaigns(data?.items ?? [])
    } catch {
      // non-critical
    }
  }

  useEffect(() => {
    apiRequest('/admin/sms/groups', { token: session.token }).then(setGroups).catch(() => {})
    apiRequest('/admin/sms/providers', { token: session.token }).then(setProviders).catch(() => {})
    apiRequest('/admin/sms/templates', { token: session.token })
      .then((list) => setTemplates((list ?? []).filter((template) => template.isActive)))
      .catch(() => {})
    loadCampaigns()
  }, [session.token])

  const onCsvFile = (file) => {
    if (!file) return
    const reader = new FileReader()
    reader.onload = () => {
      const lines = String(reader.result).split('\n')
      const rows = lines
        .map((line) => line.trim())
        .filter(Boolean)
        .filter((line) => !line.toLowerCase().startsWith('mobile'))
        .map((line) => line.split(',')[0])
      setCsvRows(rows)
    }
    reader.readAsText(file)
  }

  const recipients = [...csvRows, ...parseManualRecipients(manualNumbers)]

  const runPreview = async () => {
    setError('')
    setFlash('')
    try {
      const data = await apiRequest('/admin/sms/send-bulk/preview', {
        method: 'POST',
        token: session.token,
        body: {
          recipients,
          groupIds: selectedGroups,
          providerId: providerId || null,
          templateId: templateId || null,
          body: templateId ? null : body,
          kind,
          senderLine: senderLine || null,
        },
      })
      setPreview(data)
    } catch (requestError) {
      setError(getReadableError(requestError))
    }
  }

  const submitCampaign = async () => {
    setSubmitting(true)
    setError('')
    setFlash('')
    try {
      const data = await apiRequest('/admin/sms/send-bulk', {
        method: 'POST',
        token: session.token,
        body: {
          title: title || `ارسال گروهی ${new Date().toLocaleDateString('fa-IR')}`,
          recipients,
          groupIds: selectedGroups,
          providerId: providerId || null,
          templateId: templateId || null,
          body: templateId ? null : body,
          kind,
          senderLine: senderLine || null,
          scheduledAt: scheduledAt ? new Date(scheduledAt).toISOString() : null,
        },
      })
      setFlash(`ارسال گروهی ثبت شد و در پس‌زمینه پردازش می‌شود (${toPersian(data.totalRecipients)} گیرنده).`)
      setPreview(null)
      await loadCampaigns()
    } catch (requestError) {
      setError(getReadableError(requestError))
    } finally {
      setSubmitting(false)
      setTimeout(() => setFlash(''), 8000)
    }
  }

  const ready = preview && preview.totalMessages > 0

  return (
    <div className="sms-page" dir="rtl">
      <header className="sms-page-header">
        <h1>ارسال پیامک گروهی</h1>
        <p>انتخاب گیرندگان از گروه‌ها، شماره‌های دستی یا فایل CSV؛ ارسال در پس‌زمینه به‌صورت Batch.</p>
      </header>

      {error ? <div className="sms-error">{error}</div> : null}
      {flash ? <div className="sms-flash">{flash}</div> : null}

      <div className="sms-card">
        <h2>۱. انتخاب گیرندگان</h2>
        <div className="sms-form">
          <label>
            گروه‌های مخاطبین (چندتایی)
            <select
              multiple
              size={Math.min(6, Math.max(3, groups.length))}
              value={selectedGroups}
              onChange={(event) => setSelectedGroups([...event.target.selectedOptions].map((option) => option.value))}
            >
              {groups.map((group) => (
                <option key={group.id} value={group.id}>
                  {group.name} ({toPersian(group.memberCount)} عضو)
                </option>
              ))}
            </select>
          </label>
          <label>
            شماره‌های دستی (خط یا جداکننده)
            <textarea
              value={manualNumbers}
              onChange={(event) => setManualNumbers(event.target.value)}
              placeholder={'09121234567\n09351234567، 989…'}
              dir="ltr"
            />
          </label>
          <label>
            فایل CSV (ستون اول = موبایل)
            <input type="file" accept=".csv,text/csv" onChange={(event) => onCsvFile(event.target.files?.[0])} />
          </label>
          {csvRows.length ? (
            <p style={{ fontSize: 12 }}>
              از فایل: {toPersian(csvRows.length)} شماره —{' '}
              <button type="button" className="sms-inline-btn" onClick={() => setCsvRows([])}>حذف فایل</button>
            </p>
          ) : null}
        </div>
      </div>

      <div className="sms-card">
        <h2>۲. محتوای پیامک</h2>
        <div className="sms-form">
          <div className="sms-row">
            <label>
              سرویس‌دهنده
              <select value={providerId} onChange={(event) => { setProviderId(event.target.value); setSenderLine('') }}>
                <option value="">پیش‌فرض سیستم</option>
                {providers.map((provider) => (
                  <option key={provider.id} value={provider.id}>
                    {provider.name}{provider.supportsBulk ? ' (ارسال بومی گروهی)' : ''}
                  </option>
                ))}
              </select>
            </label>
            <label>
              خط ارسال
              <select value={senderLine} onChange={(event) => setSenderLine(event.target.value)} disabled={!providerLines.length}>
                <option value="">خط پیش‌فرض Provider</option>
                {providerLines.map((line) => (
                  <option key={line.id} value={line.number}>
                    {line.number}{line.label ? ` (${line.label})` : ''}{line.isDefaultLine ? ' ⭐' : ''}
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
            <label>
              ارسال زمان‌بندی‌شده (اختیاری)
              <input
                type="datetime-local"
                value={scheduledAt}
                onChange={(event) => setScheduledAt(event.target.value)}
              />
            </label>
          </div>
          <label>
            قالب (اختیاری)
            <select value={templateId} onChange={(event) => { setTemplateId(event.target.value); setPreview(null) }}>
              <option value="">متن آزاد</option>
              {templates.map((template) => (
                <option key={template.id} value={template.id}>{template.name}</option>
              ))}
            </select>
          </label>
          {!templateId ? (
            <label>
              متن پیامک (متغیرها: {'{FirstName}'} {'{LastName}'} {'{Mobile}'} …)
              <textarea value={body} onChange={(event) => setBody(event.target.value)} />
            </label>
          ) : null}
          <div className="sms-actions">
            <button type="button" className="sms-btn sms-btn--secondary" onClick={runPreview}>
              اعتبارسنجی و پیش‌نمایش
            </button>
          </div>
        </div>
      </div>

      {preview ? (
        <div className="sms-card">
          <h2>۳. بررسی نهایی</h2>
          <div className="sms-stats" style={{ marginBottom: 14 }}>
            <div className="sms-stat">
              <div className="sms-stat-value">{toPersian(preview.totalMessages)}</div>
              <div className="sms-stat-label">گیرندهٔ نهایی</div>
            </div>
            <div className="sms-stat">
              <div className="sms-stat-value">{toPersian(preview.totalSegments)}</div>
              <div className="sms-stat-label">کل بخش‌های پیامک</div>
            </div>
            <div className="sms-stat">
              <div className="sms-stat-value">{toPersian(preview.invalidRecipients.length)}</div>
              <div className="sms-stat-label">شمارهٔ نامعتبر</div>
            </div>
            <div className="sms-stat">
              <div className="sms-stat-value">{toPersian(preview.deduplicatedRecipients.length)}</div>
              <div className="sms-stat-label">تکراری حذف‌شده</div>
            </div>
            <div className="sms-stat">
              <div className="sms-stat-value">{toPersian(preview.optedOutRecipients.length)}</div>
              <div className="sms-stat-label">انصراف/بدون رضایت</div>
            </div>
          </div>
          <div className="sms-preview">{preview.renderedBody}</div>
          <p style={{ fontSize: 12, color: 'var(--muted, #6b7280)' }}>
            {preview.characters} کاراکتر — {preview.segments} بخش —
            {preview.providerCapabilities?.supportsBulk
              ? ` ارسال بومی گروهی با Batch ${toPersian(preview.providerCapabilities.bulkBatchSize)}تایی`
              : ' ارسال تکی به‌تفکیک گیرنده'}
            {preview.providerCapabilities?.supportsCredit && preview.providerCapabilities?.credit != null
              ? ` — اعتبار: ${formatToman(preview.providerCapabilities.credit)}`
              : ''}
          </p>
          {preview.invalidRecipients.length ? (
            <p style={{ fontSize: 12 }}>
              نامعتبر: <span dir="ltr">{preview.invalidRecipients.join('، ')}</span>
            </p>
          ) : null}
          {preview.optedOutRecipients.length ? (
            <ul className="sms-mini-list" style={{ marginTop: 8 }}>
              {preview.optedOutRecipients.map((mobile) => (
                <li key={mobile} dir="ltr">{mobile}</li>
              ))}
            </ul>
          ) : null}
          <div className="sms-actions" style={{ marginTop: 14 }}>
            <label style={{ flex: '1 1 260px' }}>
              عنوان ارسال
              <input value={title} onChange={(event) => setTitle(event.target.value)} placeholder="اختیاری" />
            </label>
            <button type="button" className="sms-btn" disabled={!ready || submitting} onClick={submitCampaign}>
              {submitting ? 'در حال ثبت…' : 'تأیید نهایی و شروع ارسال'}
            </button>
          </div>
        </div>
      ) : null}

      <div className="sms-card">
        <h2>ارسال‌های اخیر</h2>
        {campaigns.length ? (
          <table className="sms-table">
            <thead>
              <tr>
                <th>عنوان</th>
                <th>وضعیت</th>
                <th>زمان‌بندی</th>
                <th>گیرندگان</th>
                <th>موفق</th>
                <th>ناموفق</th>
                <th>خط ارسال</th>
              </tr>
            </thead>
            <tbody>
              {campaigns.map((campaign) => (
                <tr key={campaign.id}>
                  <td>{campaign.title}</td>
                  <td>{campaignStatusLabel(campaign)}</td>
                  <td>
                    {campaign.scheduledAt
                      ? formatSmsDateTime(campaign.scheduledAt)
                      : '—'}
                  </td>
                  <td>{toPersian(campaign.totalRecipients)}</td>
                  <td>{toPersian(campaign.sentCount)}</td>
                  <td>{toPersian(campaign.failedCount)}</td>
                  <td dir="ltr">{campaign.senderLine ?? '—'}</td>
                </tr>
              ))}
            </tbody>
          </table>
        ) : (
          <p>هنوز ارسال گروهی ثبت نشده است.</p>
        )}
      </div>
    </div>
  )
}
