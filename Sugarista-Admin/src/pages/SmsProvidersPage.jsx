import { useCallback, useEffect, useState } from 'react'
import { apiRequest, getReadableError } from '../utils/adminApi'
import { useAdmin } from '../context/AdminContext'
import {
  SMS_PROVIDER_TYPES,
  formatSmsDateTime,
  formatToman,
} from '../utils/smsHelpers'
import './sms.css'

const EMPTY_FORM = {
  id: null,
  name: '',
  providerType: 'Kavenegar',
  baseUrl: '',
  apiKey: '',
  username: '',
  password: '',
  timeoutSeconds: 30,
  extraSettingsJson: '',
  deliveryWebhookSecret: '',
  isEnabled: true,
  isDefault: false,
  senderLines: [],
}

const LINE_EMPTY = { number: '', label: '', isDefault: false }

export function SmsProvidersPage() {
  const { session } = useAdmin()
  const [providers, setProviders] = useState([])
  const [form, setForm] = useState(EMPTY_FORM)
  const [flash, setFlash] = useState('')
  const [error, setError] = useState('')
  const [busy, setBusy] = useState(false)

  const load = useCallback(async () => {
    try {
      setProviders(await apiRequest('/admin/sms/providers', { token: session.token }))
    } catch (requestError) {
      setError(getReadableError(requestError))
    }
  }, [session.token])

  useEffect(() => {
    if (session.token) load()
  }, [session.token, load])

  const save = async () => {
    setBusy(true)
    setError('')
    setFlash('')
    try {
      const payload = {
        name: form.name,
        providerType: form.providerType,
        baseUrl: form.baseUrl || null,
        apiKey: form.apiKey || null, // empty on edit = keep stored credential
        username: form.username || null,
        password: form.password || null,
        timeoutSeconds: Number(form.timeoutSeconds) || 30,
        extraSettingsJson: form.extraSettingsJson || null,
        deliveryWebhookSecret: form.deliveryWebhookSecret || null,
        isEnabled: form.isEnabled,
        isDefault: form.isDefault,
        senderLines: form.senderLines.filter((line) => line.number.trim()),
      }
      if (form.id) {
        await apiRequest(`/admin/sms/providers/${form.id}`, { method: 'PUT', token: session.token, body: payload })
        setFlash('سرویس‌دهنده ویرایش شد.')
      } else {
        await apiRequest('/admin/sms/providers', { method: 'POST', token: session.token, body: payload })
        setFlash('سرویس‌دهنده اضافه شد.')
      }
      setForm(EMPTY_FORM)
      await load()
    } catch (requestError) {
      setError(getReadableError(requestError))
    } finally {
      setBusy(false)
      setTimeout(() => setFlash(''), 6000)
    }
  }

  const remove = async (provider) => {
    setBusy(true)
    try {
      await apiRequest(`/admin/sms/providers/${provider.id}`, { method: 'DELETE', token: session.token })
      setFlash('سرویس‌دهنده حذف شد.')
      await load()
    } catch (requestError) {
      setError(getReadableError(requestError))
    } finally {
      setBusy(false)
      setTimeout(() => setFlash(''), 6000)
    }
  }

  const testConnection = async (provider) => {
    setError('')
    setFlash('')
    setBusy(true)
    try {
      const result = await apiRequest(`/admin/sms/providers/${provider.id}/test`, { method: 'POST', token: session.token })
      setFlash(result.success ? `${result.message}${result.credit != null ? ` — اعتبار: ${formatToman(result.credit)}` : ''}` : `خطا: ${result.message}`)
      await load()
    } catch (requestError) {
      setError(getReadableError(requestError))
    } finally {
      setBusy(false)
      setTimeout(() => setFlash(''), 8000)
    }
  }

  const checkCredit = async (provider) => {
    setError('')
    setFlash('')
    setBusy(true)
    try {
      const result = await apiRequest(`/admin/sms/providers/${provider.id}/check-credit`, { method: 'POST', token: session.token })
      setFlash(result.success ? `اعتبار باقی‌مانده: ${formatToman(result.credit)}` : `خطا: ${result.error}`)
    } catch (requestError) {
      setError(getReadableError(requestError))
    } finally {
      setBusy(false)
      setTimeout(() => setFlash(''), 8000)
    }
  }

  return (
    <div className="sms-page" dir="rtl">
      <header className="sms-page-header">
        <h1>مدیریت سرویس‌دهندگان پیامک</h1>
        <p>کلیدهای API رمزنگاری‌شده ذخیره می‌شوند و هرگز در پاسخ API بازگردانده نمی‌شوند — فقط پیش‌نمایش ماسک‌شده.</p>
      </header>

      {error ? <div className="sms-error">{error}</div> : null}
      {flash ? <div className="sms-flash">{flash}</div> : null}

      <div className="sms-card">
        <h2>{form.id ? `ویرایش «${form.name}»` : 'سرویس‌دهندهٔ جدید'}</h2>
        <div className="sms-form">
          <div className="sms-row">
            <label>نام *
              <input value={form.name} onChange={(event) => setForm({ ...form, name: event.target.value })} />
            </label>
            <label>نوع *
              <select
                value={form.providerType}
                disabled={Boolean(form.id)}
                onChange={(event) => setForm({ ...form, providerType: event.target.value })}
              >
                {SMS_PROVIDER_TYPES.map((type) => (
                  <option key={type.value} value={type.value}>{type.label}</option>
                ))}
              </select>
            </label>
          </div>
          <div className="sms-row">
            <label>API Base URL (اختیاری)
              <input dir="ltr" value={form.baseUrl} onChange={(event) => setForm({ ...form, baseUrl: event.target.value })} placeholder="پیش‌فرض خود Provider" />
            </label>
            <label>Timeout (ثانیه)
              <input type="number" min={3} max={120} value={form.timeoutSeconds} onChange={(event) => setForm({ ...form, timeoutSeconds: event.target.value })} />
            </label>
          </div>
          <div className="sms-row">
            <label>
              API Key {form.id ? '(خالی = حفظ مقدار فعلی)' : '*'}
              <input dir="ltr" type="password" value={form.apiKey} onChange={(event) => setForm({ ...form, apiKey: event.target.value })} autoComplete="new-password" />
            </label>
            <label>
              Username (اختیاری){form.id ? ' (خالی = حفظ فعلی)' : ''}
              <input dir="ltr" value={form.username} onChange={(event) => setForm({ ...form, username: event.target.value })} autoComplete="new-password" />
            </label>
            <label>
              Password (اختیاری){form.id ? ' (خالی = حفظ فعلی)' : ''}
              <input dir="ltr" type="password" value={form.password} onChange={(event) => setForm({ ...form, password: event.target.value })} autoComplete="new-password" />
            </label>
          </div>
          <label>تنظیمات اضافهٔ Provider (JSON اختیاری)
            <input dir="ltr" value={form.extraSettingsJson} onChange={(event) => setForm({ ...form, extraSettingsJson: event.target.value })} placeholder='{"key": "value"}' />
          </label>
          <label>
            کلید Webhook گزارش تحویل {form.id ? '(خالی = حفظ مقدار فعلی)' : '(خالی = تولید خودکار)'}
            <div className="sms-row" style={{ flex: 'initial' }}>
              <input dir="ltr" value={form.deliveryWebhookSecret} onChange={(event) => setForm({ ...form, deliveryWebhookSecret: event.target.value })} placeholder="خالی برای تولید خودکار" />
              <button
                type="button"
                className="sms-inline-btn"
                style={{ flex: 'initial' }}
                onClick={() => setForm({ ...form, deliveryWebhookSecret: [...crypto.getRandomValues(new Uint8Array(16))].map((byte) => byte.toString(16).padStart(2, '0')).join('') })}
              >
                تولید کلید
              </button>
            </div>
          </label>
          <label>
            خطوط ارسال (Sender Lines)
            {form.senderLines.map((line, index) => (
              <div className="sms-row" key={index} style={{ flex: 'initial', marginTop: 6 }}>
                <input dir="ltr" placeholder="شماره خط" value={line.number}
                  onChange={(event) => setForm({
                    ...form,
                    senderLines: form.senderLines.map((entry, entryIndex) =>
                      entryIndex === index ? { ...entry, number: event.target.value } : entry),
                  })} />
                <input placeholder="برچسب (اختیاری)" value={line.label}
                  onChange={(event) => setForm({
                    ...form,
                    senderLines: form.senderLines.map((entry, entryIndex) =>
                      entryIndex === index ? { ...entry, label: event.target.value } : entry),
                  })} />
                <label className="sms-checkbox-row" style={{ flex: 'initial' }}>
                  <input type="checkbox" checked={line.isDefault}
                    onChange={(event) => setForm({
                      ...form,
                      senderLines: form.senderLines.map((entry, entryIndex) =>
                        entryIndex === index ? { ...entry, isDefault: event.target.checked } : entry),
                    })} />
                  پیش‌فرض
                </label>
                <button type="button" className="sms-inline-btn"
                  onClick={() => setForm({ ...form, senderLines: form.senderLines.filter((_, entryIndex) => entryIndex !== index) })}>
                  حذف
                </button>
              </div>
            ))}
            <button
              type="button"
              className="sms-inline-btn"
              style={{ marginTop: 6 }}
              onClick={() => setForm({ ...form, senderLines: [...form.senderLines, LINE_EMPTY] })}
            >
              افزودن خط
            </button>
          </label>
          <div className="sms-row" style={{ flex: 'initial' }}>
            <label className="sms-checkbox-row">
              <input type="checkbox" checked={form.isEnabled} onChange={(event) => setForm({ ...form, isEnabled: event.target.checked })} />
              فعال
            </label>
            <label className="sms-checkbox-row">
              <input type="checkbox" checked={form.isDefault} onChange={(event) => setForm({ ...form, isDefault: event.target.checked })} />
              سرویس‌دهندهٔ پیش‌فرض
            </label>
          </div>
          <div className="sms-actions">
            <button type="button" className="sms-btn" disabled={busy || !form.name.trim()} onClick={save}>
              {form.id ? 'ذخیرهٔ تغییرات' : 'افزودن'}
            </button>
            {form.id ? (
              <button type="button" className="sms-btn sms-btn--secondary" onClick={() => setForm(EMPTY_FORM)}>انصراف</button>
            ) : null}
          </div>
        </div>
      </div>

      <div className="sms-card">
        <h2>سرویس‌دهندگان ثبت‌شده</h2>
        {providers.length ? (
          <table className="sms-table">
            <thead>
              <tr>
                <th>نام</th>
                <th>نوع</th>
                <th>کلید API</th>
                <th>خطوط</th>
                <th>وضعیت</th>
                <th>آخرین اتصال موفق</th>
                <th>عملیات</th>
              </tr>
            </thead>
            <tbody>
              {providers.map((provider) => (
                <tr key={provider.id}>
                  <td>{provider.name}{provider.isDefault ? ' ⭐' : ''}</td>
                  <td>{SMS_PROVIDER_TYPES.find((type) => type.value === provider.providerType)?.label ?? provider.providerType}</td>
                  <td>{provider.hasApiKey ? <code dir="ltr">{provider.apiKeyPreview}</code> : '—'}</td>
                  <td>{provider.senderLines.map((line) => line.number).join('، ') || '—'}</td>
                  <td>
                    {provider.isEnabled ? 'فعال' : 'غیرفعال'}
                    {provider.lastConnectionError ? (
                      <><br /><span style={{ color: '#b91c1c', fontSize: 11 }}>{provider.lastConnectionError}</span></>
                    ) : null}
                  </td>
                  <td>{formatSmsDateTime(provider.lastSuccessfulConnectionAt)}</td>
                  <td className="sms-row" style={{ flex: 'initial' }}>
                    {provider.supportsDeliveryReport && provider.deliveryWebhookSecret ? (
                      <button
                        type="button"
                        className="sms-inline-btn"
                        title="کپی URL وب‌هوک برای تنظیم در پنل Provider"
                        onClick={() => {
                          const url = `${window.location.origin}/api/sms/webhook/${provider.id}/${provider.deliveryWebhookSecret}`
                          navigator.clipboard?.writeText(url)
                          alert(`URL وب‌هوک کپی شد:\n${url}`)
                        }}
                      >
                        URL وب‌هوک
                      </button>
                    ) : null}
                    <button
                      type="button"
                      className="sms-inline-btn"
                      onClick={() => setForm({
                        id: provider.id,
                        name: provider.name,
                        providerType: provider.providerType,
                        baseUrl: provider.baseUrl ?? '',
                        apiKey: '',
                        username: '',
                        password: '',
                        timeoutSeconds: provider.timeoutSeconds,
                        extraSettingsJson: provider.extraSettingsJson ?? '',
                        deliveryWebhookSecret: '',
                        isEnabled: provider.isEnabled,
                        isDefault: provider.isDefault,
                        senderLines: provider.senderLines.map((line) => ({ number: line.number, label: line.label ?? '', isDefault: line.isDefaultLine })),
                      })}
                    >
                      ویرایش
                    </button>
                    <button type="button" className="sms-inline-btn" onClick={() => testConnection(provider)} disabled={busy}>تست اتصال</button>
                    {provider.supportsCredit ? (
                      <button type="button" className="sms-inline-btn" onClick={() => checkCredit(provider)} disabled={busy}>اعتبار</button>
                    ) : null}
                    <button type="button" className="sms-inline-btn sms-inline-btn--danger" onClick={() => remove(provider)} disabled={busy}>حذف</button>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        ) : (
          <p>
            هنوز سرویس‌دهنده‌ای ثبت نشده است. بدون Provider دیتابیسی، اعلان‌های سیستمی به تنظیمات قدیمی appsettings برمی‌گردند.
          </p>
        )}
      </div>
    </div>
  )
}
