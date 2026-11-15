import { useCallback, useEffect, useState } from 'react'
import { apiRequest, getReadableError } from '../utils/adminApi'
import { useAdmin } from '../context/AdminContext'
import {
  SMS_KIND_LABELS,
  SMS_PROVIDER_TYPES,
  SMS_TEMPLATE_PLACEHOLDERS,
  toPersian,
} from '../utils/smsHelpers'
import './sms.css'

const EMPTY_FORM = {
  id: null,
  name: '',
  description: '',
  body: '',
  kind: 'Service',
  isActive: true,
  isDefault: false,
  mappings: [],
}

export function SmsTemplatesPage() {
  const { session } = useAdmin()
  const [templates, setTemplates] = useState([])
  const [form, setForm] = useState(EMPTY_FORM)
  const [preview, setPreview] = useState(null)
  const [flash, setFlash] = useState('')
  const [error, setError] = useState('')
  const [busy, setBusy] = useState(false)

  const load = useCallback(async () => {
    try {
      setTemplates(await apiRequest('/admin/sms/templates', { token: session.token }))
    } catch (requestError) {
      setError(getReadableError(requestError))
    }
  }, [session.token])

  useEffect(() => {
    if (session.token) load()
  }, [session.token, load])

  const runPreview = async () => {
    setError('')
    try {
      setPreview(await apiRequest('/admin/sms/templates/preview', {
        method: 'POST',
        token: session.token,
        body: { body: form.body },
      }))
    } catch (requestError) {
      setError(getReadableError(requestError))
    }
  }

  const save = async () => {
    setBusy(true)
    setError('')
    setFlash('')
    try {
      const payload = {
        name: form.name,
        description: form.description || null,
        body: form.body,
        kind: form.kind,
        isActive: form.isActive,
        isDefault: form.isDefault,
        providerMappings: form.mappings.filter((mapping) => mapping.providerTemplateId.trim()),
      }
      if (form.id) {
        await apiRequest(`/admin/sms/templates/${form.id}`, { method: 'PUT', token: session.token, body: payload })
      } else {
        await apiRequest('/admin/sms/templates', { method: 'POST', token: session.token, body: payload })
      }
      setFlash(form.id ? 'قالب ویرایش شد.' : 'قالب ساخته شد.')
      setForm(EMPTY_FORM)
      setPreview(null)
      await load()
    } catch (requestError) {
      setError(getReadableError(requestError))
    } finally {
      setBusy(false)
      setTimeout(() => setFlash(''), 6000)
    }
  }

  const remove = async (template) => {
    setBusy(true)
    try {
      await apiRequest(`/admin/sms/templates/${template.id}`, { method: 'DELETE', token: session.token })
      setFlash('قالب حذف شد.')
      await load()
    } catch (requestError) {
      setError(getReadableError(requestError))
    } finally {
      setBusy(false)
      setTimeout(() => setFlash(''), 6000)
    }
  }

  const duplicate = async (template) => {
    setBusy(true)
    try {
      await apiRequest(`/admin/sms/templates/${template.id}/duplicate`, { method: 'POST', token: session.token })
      setFlash('کپی قالب ساخته شد.')
      await load()
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
        <h1>قالب‌های پیامکی</h1>
        <p>قالب‌های داخلی سیستم با متغیرهای {'{نام}'} — قابل اتصال به Template ID قالب‌های تأییدشده در پنل Provider.</p>
      </header>

      {error ? <div className="sms-error">{error}</div> : null}
      {flash ? <div className="sms-flash">{flash}</div> : null}

      <div className="sms-card">
        <h2>{form.id ? 'ویرایش قالب' : 'قالب جدید'}</h2>
        <div className="sms-form">
          <div className="sms-row">
            <label>نام قالب *
              <input value={form.name} onChange={(event) => setForm({ ...form, name: event.target.value })} />
            </label>
            <label>نوع پیامک
              <select value={form.kind} onChange={(event) => setForm({ ...form, kind: event.target.value })}>
                {Object.entries(SMS_KIND_LABELS).map(([value, label]) => (
                  <option key={value} value={value}>{label}</option>
                ))}
              </select>
            </label>
          </div>
          <label>توضیحات
            <input value={form.description} onChange={(event) => setForm({ ...form, description: event.target.value })} />
          </label>
          <label>
            متن قالب — متغیرهای مجاز: {SMS_TEMPLATE_PLACEHOLDERS.map((placeholder) => `{${placeholder}}`).join(' ')}
            <textarea value={form.body} onChange={(event) => setForm({ ...form, body: event.target.value })} />
          </label>
          <p style={{ fontSize: 12, margin: 0 }}>
            نمونه: «سلام {'{FirstName}'} عزیز، سفارش {'{OrderNumber}'} شما آماده شد.»
          </p>
          <label>
            اتصال به قالب Providerها (Template ID / Pattern)
            {form.mappings.map((mapping, index) => (
              <div className="sms-row" key={index} style={{ flex: 'initial', marginTop: 6 }}>
                <select
                  value={mapping.providerType}
                  onChange={(event) => setForm({
                    ...form,
                    mappings: form.mappings.map((entry, entryIndex) =>
                      entryIndex === index ? { ...entry, providerType: event.target.value } : entry),
                  })}
                >
                  {SMS_PROVIDER_TYPES.map((type) => (
                    <option key={type.value} value={type.value}>{type.label}</option>
                  ))}
                </select>
                <input
                  dir="ltr"
                  placeholder="Provider Template ID"
                  value={mapping.providerTemplateId}
                  onChange={(event) => setForm({
                    ...form,
                    mappings: form.mappings.map((entry, entryIndex) =>
                      entryIndex === index ? { ...entry, providerTemplateId: event.target.value } : entry),
                  })}
                />
                <button
                  type="button"
                  className="sms-inline-btn"
                  onClick={() => setForm({ ...form, mappings: form.mappings.filter((_, entryIndex) => entryIndex !== index) })}
                >
                  حذف
                </button>
              </div>
            ))}
            <button
              type="button"
              className="sms-inline-btn"
              style={{ marginTop: 6 }}
              onClick={() => setForm({
                ...form,
                mappings: [...form.mappings, { providerType: 'Kavenegar', providerTemplateId: '' }],
              })}
            >
              افزودن اتصال Provider
            </button>
          </label>
          <div className="sms-row" style={{ flex: 'initial' }}>
            <label className="sms-checkbox-row">
              <input type="checkbox" checked={form.isActive} onChange={(event) => setForm({ ...form, isActive: event.target.checked })} />
              فعال
            </label>
            <label className="sms-checkbox-row">
              <input type="checkbox" checked={form.isDefault} onChange={(event) => setForm({ ...form, isDefault: event.target.checked })} />
              پیش‌فرض این نوع
            </label>
          </div>
          <div className="sms-actions">
            <button type="button" className="sms-btn sms-btn--secondary" onClick={runPreview} disabled={!form.body.trim()}>
              پیش‌نمایش با مقادیر نمونه
            </button>
            <button type="button" className="sms-btn" disabled={busy || !form.name.trim() || !form.body.trim()} onClick={save}>
              {form.id ? 'ذخیره' : 'ایجاد قالب'}
            </button>
            {form.id ? (
              <button type="button" className="sms-btn sms-btn--secondary" onClick={() => { setForm(EMPTY_FORM); setPreview(null) }}>
                انصراف
              </button>
            ) : null}
          </div>
        </div>
        {preview ? (
          <div style={{ marginTop: 14 }}>
            <div className="sms-preview">{preview.renderedBody}</div>
            <p style={{ fontSize: 12, color: 'var(--muted, #6b7280)' }}>
              {toPersian(preview.characters)} کاراکتر — {toPersian(preview.segments)} بخش —
              {preview.unicode ? ' یونیکد' : ' GSM-7'} — متغیرها: {preview.placeholders.join('، ') || '—'}
            </p>
          </div>
        ) : null}
      </div>

      <div className="sms-card">
        <h2>قالب‌های ثبت‌شده</h2>
        {templates.length ? (
          <table className="sms-table">
            <thead>
              <tr>
                <th>نام</th>
                <th>نوع</th>
                <th>پیش‌نمایش</th>
                <th>اتصال Provider</th>
                <th>وضعیت</th>
                <th>عملیات</th>
              </tr>
            </thead>
            <tbody>
              {templates.map((template) => (
                <tr key={template.id}>
                  <td>{template.name}{template.isDefault ? ' ⭐' : ''}</td>
                  <td>{SMS_KIND_LABELS[template.kind] ?? template.kind}</td>
                  <td style={{ maxWidth: 260 }}>{template.body.slice(0, 60)}{template.body.length > 60 ? '…' : ''}</td>
                  <td>
                    {template.providerMappings.length
                      ? template.providerMappings.map((mapping) => `${mapping.providerType}: ${mapping.providerTemplateId}`).join('، ')
                      : '—'}
                  </td>
                  <td>{template.isActive ? 'فعال' : 'غیرفعال'}</td>
                  <td className="sms-row" style={{ flex: 'initial' }}>
                    <button
                      type="button"
                      className="sms-inline-btn"
                      onClick={() => setForm({
                        id: template.id,
                        name: template.name,
                        description: template.description ?? '',
                        body: template.body,
                        kind: template.kind,
                        isActive: template.isActive,
                        isDefault: template.isDefault,
                        mappings: template.providerMappings.map((mapping) => ({
                          providerType: mapping.providerType,
                          providerTemplateId: mapping.providerTemplateId,
                        })),
                      })}
                    >
                      ویرایش
                    </button>
                    <button type="button" className="sms-inline-btn" onClick={() => duplicate(template)} disabled={busy}>کپی</button>
                    <button type="button" className="sms-inline-btn sms-inline-btn--danger" onClick={() => remove(template)} disabled={busy}>حذف</button>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        ) : (
          <p>هنوز قالبی ثبت نشده است.</p>
        )}
      </div>
    </div>
  )
}
