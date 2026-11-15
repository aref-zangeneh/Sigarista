import { useCallback, useEffect, useState } from 'react'
import { apiRequest, getReadableError } from '../utils/adminApi'
import { useAdmin } from '../context/AdminContext'
import { toPersian, toPersianNumberString } from '../utils/smsHelpers'
import './sms.css'

const EMPTY_FORM = {
  id: '',
  firstName: '',
  lastName: '',
  mobile: '',
  email: '',
  notes: '',
  isActive: true,
  marketingConsent: false,
  groupIds: [],
}

export function SmsContactsPage() {
  const { session } = useAdmin()
  const [rows, setRows] = useState({ items: [], total: 0 })
  const [groups, setGroups] = useState([])
  const [search, setSearch] = useState('')
  const [page, setPage] = useState(1)
  const [pageSize] = useState(20)
  const [form, setForm] = useState(EMPTY_FORM)
  const [selected, setSelected] = useState([])
  const [bulkGroup, setBulkGroup] = useState('')
  const [importReport, setImportReport] = useState(null)
  const [flash, setFlash] = useState('')
  const [error, setError] = useState('')
  const [busy, setBusy] = useState(false)

  const load = useCallback(async () => {
    setError('')
    try {
      const params = new URLSearchParams({ page, pageSize })
      if (search.trim()) params.set('search', search.trim())
      const data = await apiRequest(`/admin/sms/contacts?${params}`, { token: session.token })
      setRows({ items: data?.items ?? [], total: data?.total ?? 0 })
    } catch (requestError) {
      setError(getReadableError(requestError))
    }
  }, [session.token, page, pageSize, search])

  useEffect(() => {
    apiRequest('/admin/sms/groups', { token: session.token }).then(setGroups).catch(() => {})
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
        id: form.id || '00000000-0000-0000-0000-000000000000',
        firstName: form.firstName || null,
        lastName: form.lastName || null,
        mobile: form.mobile,
        email: form.email || null,
        notes: form.notes || null,
        isActive: form.isActive,
        marketingConsent: form.marketingConsent,
        groupIds: form.groupIds,
      }
      if (form.id) {
        await apiRequest(`/admin/sms/contacts/${form.id}`, { method: 'PUT', token: session.token, body: payload })
      } else {
        await apiRequest('/admin/sms/contacts', { method: 'POST', token: session.token, body: payload })
      }
      setFlash(form.id ? 'مخاطب ویرایش شد.' : 'مخاطب اضافه شد.')
      setForm(EMPTY_FORM)
      await load()
    } catch (requestError) {
      setError(getReadableError(requestError))
    } finally {
      setBusy(false)
      setTimeout(() => setFlash(''), 6000)
    }
  }

  const remove = async (contact) => {
    setBusy(true)
    try {
      await apiRequest(`/admin/sms/contacts/${contact.id}`, { method: 'DELETE', token: session.token })
      setFlash('مخاطب حذف شد.')
      await load()
    } catch (requestError) {
      setError(getReadableError(requestError))
    } finally {
      setBusy(false)
      setTimeout(() => setFlash(''), 6000)
    }
  }

  const toggleSelect = (id) => {
    setSelected((current) => (current.includes(id) ? current.filter((entry) => entry !== id) : [...current, id]))
  }

  const bulkAddToGroup = async () => {
    if (!bulkGroup || !selected.length) return
    setBusy(true)
    try {
      const added = await apiRequest('/admin/sms/contacts/add-to-groups', {
        method: 'POST',
        token: session.token,
        body: { contactIds: selected, groupIds: [bulkGroup] },
      })
      setFlash(`${toPersian(added)} عضویت اضافه شد.`)
      setSelected([])
      await load()
    } catch (requestError) {
      setError(getReadableError(requestError))
    } finally {
      setBusy(false)
      setTimeout(() => setFlash(''), 6000)
    }
  }

  const bulkRemove = async () => {
    if (!selected.length) return
    setBusy(true)
    try {
      for (const id of selected) {
        await apiRequest(`/admin/sms/contacts/${id}`, { method: 'DELETE', token: session.token })
      }
      setFlash(`${toPersian(selected.length)} مخاطب حذف شد.`)
      setSelected([])
      await load()
    } catch (requestError) {
      setError(getReadableError(requestError))
    } finally {
      setBusy(false)
      setTimeout(() => setFlash(''), 6000)
    }
  }

  const importCsv = async (file) => {
    if (!file) return
    setBusy(true)
    setImportReport(null)
    try {
      const text = await file.text()
      const report = await apiRequest('/admin/sms/contacts/import', {
        method: 'POST',
        token: session.token,
        body: { csvContent: text, marketingConsentDefault: false },
      })
      setImportReport(report)
      await load()
    } catch (requestError) {
      setError(getReadableError(requestError))
    } finally {
      setBusy(false)
    }
  }

  const exportCsv = async () => {
    try {
      const csv = await apiRequest('/admin/sms/contacts/export', { token: session.token })
      const blob = new Blob([csv], { type: 'text/csv;charset=utf-8' })
      const url = URL.createObjectURL(blob)
      const link = document.createElement('a')
      link.href = url
      link.download = 'sms-contacts.csv'
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
        <h1>دفترچه مخاطبین</h1>
        <p>مخاطبین پیامک با ذخیرهٔ شمارهٔ نرمال‌شده — جلوگیری از ثبت تکراری در سطح دیتابیس.</p>
      </header>

      {error ? <div className="sms-error">{error}</div> : null}
      {flash ? <div className="sms-flash">{flash}</div> : null}

      <div className="sms-card">
        <h2>{form.id ? 'ویرایش مخاطب' : 'مخاطب جدید'}</h2>
        <div className="sms-form">
          <div className="sms-row">
            <label>نام
              <input value={form.firstName} onChange={(event) => setForm({ ...form, firstName: event.target.value })} />
            </label>
            <label>نام خانوادگی
              <input value={form.lastName} onChange={(event) => setForm({ ...form, lastName: event.target.value })} />
            </label>
            <label>موبایل *
              <input dir="ltr" value={form.mobile} onChange={(event) => setForm({ ...form, mobile: event.target.value })} placeholder="09121234567" />
            </label>
          </div>
          <div className="sms-row">
            <label>ایمیل (اختیاری)
              <input dir="ltr" value={form.email} onChange={(event) => setForm({ ...form, email: event.target.value })} />
            </label>
            <label>گروه‌ها (چندتایی)
              <select
                multiple
                size={Math.min(4, Math.max(2, groups.length))}
                value={form.groupIds}
                onChange={(event) => setForm({ ...form, groupIds: [...event.target.selectedOptions].map((option) => option.value) })}
              >
                {groups.map((group) => (
                  <option key={group.id} value={group.id}>{group.name}</option>
                ))}
              </select>
            </label>
          </div>
          <label>توضیحات
            <input value={form.notes} onChange={(event) => setForm({ ...form, notes: event.target.value })} />
          </label>
          <div className="sms-row" style={{ flex: 'initial' }}>
            <label className="sms-checkbox-row">
              <input type="checkbox" checked={form.isActive} onChange={(event) => setForm({ ...form, isActive: event.target.checked })} />
              فعال
            </label>
            <label className="sms-checkbox-row">
              <input type="checkbox" checked={form.marketingConsent} onChange={(event) => setForm({ ...form, marketingConsent: event.target.checked })} />
              رضایت دریافت پیامک تبلیغاتی
            </label>
          </div>
          <div className="sms-actions">
            <button type="button" className="sms-btn" disabled={busy || !form.mobile.trim()} onClick={save}>
              {form.id ? 'ذخیرهٔ تغییرات' : 'افزودن مخاطب'}
            </button>
            {form.id ? (
              <button type="button" className="sms-btn sms-btn--secondary" onClick={() => setForm(EMPTY_FORM)}>
                انصراف
              </button>
            ) : null}
          </div>
        </div>
      </div>

      <div className="sms-card">
        <h2>افزودن گروهی از CSV</h2>
        <div className="sms-row">
          <label style={{ flex: '1 1 260px' }}>
            فایل CSV (mobile,firstName,lastName,email,notes)
            <input type="file" accept=".csv,text/csv" onChange={(event) => importCsv(event.target.files?.[0])} disabled={busy} />
          </label>
          <button type="button" className="sms-btn sms-btn--secondary" onClick={exportCsv}>
            خروجی CSV
          </button>
        </div>
        {importReport ? (
          <p style={{ fontSize: 13 }}>
            نتیجهٔ Import: اضافه‌شده {toPersian(importReport.added)} — به‌روزرسانی {toPersian(importReport.updated)} —
            تکراری {toPersian(importReport.duplicates)} — نامعتبر {toPersian(importReport.invalid)}
          </p>
        ) : null}
      </div>

      <div className="sms-card">
        <div className="sms-row" style={{ marginBottom: 12 }}>
          <label style={{ flex: '1 1 220px' }}>
            جستجو (نام یا شماره)
            <input value={search} onChange={(event) => { setSearch(event.target.value); setPage(1) }} />
          </label>
          {selected.length ? (
            <>
              <label style={{ flex: '1 1 200px' }}>
                افزودن انتخاب‌شده‌ها به گروه
                <select value={bulkGroup} onChange={(event) => setBulkGroup(event.target.value)}>
                  <option value="">انتخاب گروه…</option>
                  {groups.map((group) => (
                    <option key={group.id} value={group.id}>{group.name}</option>
                  ))}
                </select>
              </label>
              <button type="button" className="sms-btn sms-btn--secondary" onClick={bulkAddToGroup} disabled={busy || !bulkGroup}>
                افزودن به گروه
              </button>
              <button type="button" className="sms-btn sms-btn--secondary sms-inline-btn--danger" onClick={bulkRemove} disabled={busy}>
                حذف گروهی
              </button>
            </>
          ) : null}
        </div>

        <table className="sms-table">
          <thead>
            <tr>
              <th></th>
              <th>نام</th>
              <th>موبایل</th>
              <th>فعال</th>
              <th>رضایت تبلیغات</th>
              <th>عملیات</th>
            </tr>
          </thead>
          <tbody>
            {rows.items.map((contact) => (
              <tr key={contact.id}>
                <td>
                  <input type="checkbox" checked={selected.includes(contact.id)} onChange={() => toggleSelect(contact.id)} />
                </td>
                <td>{[contact.firstName, contact.lastName].filter(Boolean).join(' ') || '—'}</td>
                <td dir="ltr">{toPersianNumberString(contact.displayMobile)}</td>
                <td>{contact.isActive ? 'بله' : 'خیر'}</td>
                <td>{contact.marketingConsent ? 'بله' : 'خیر'}</td>
                <td className="sms-row" style={{ flex: 'initial' }}>
                  <button
                    type="button"
                    className="sms-inline-btn"
                    onClick={() => setForm({
                      id: contact.id,
                      firstName: contact.firstName ?? '',
                      lastName: contact.lastName ?? '',
                      mobile: contact.displayMobile,
                      email: contact.email ?? '',
                      notes: contact.notes ?? '',
                      isActive: contact.isActive,
                      marketingConsent: contact.marketingConsent,
                      groupIds: contact.groupIds ?? [],
                    })}
                  >
                    ویرایش
                  </button>
                  <button type="button" className="sms-inline-btn sms-inline-btn--danger" onClick={() => remove(contact)} disabled={busy}>
                    حذف
                  </button>
                </td>
              </tr>
            ))}
            {!rows.items.length ? (
              <tr><td colSpan={6}>مخاطبی یافت نشد.</td></tr>
            ) : null}
          </tbody>
        </table>

        <div className="sms-row" style={{ marginTop: 12, flex: 'initial' }}>
          <button type="button" className="sms-inline-btn" disabled={page <= 1} onClick={() => setPage(page - 1)}>قبلی</button>
          <span style={{ fontSize: 12 }}>صفحه {toPersian(page)} از {toPersian(totalPages)} — کل {toPersian(rows.total)}</span>
          <button type="button" className="sms-inline-btn" disabled={page >= totalPages} onClick={() => setPage(page + 1)}>بعدی</button>
        </div>
      </div>
    </div>
  )
}
