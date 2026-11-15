import { useCallback, useEffect, useState } from 'react'
import { apiRequest, getReadableError } from '../utils/adminApi'
import { useAdmin } from '../context/AdminContext'
import { toPersian } from '../utils/smsHelpers'
import './sms.css'

export function SmsContactGroupsPage() {
  const { session } = useAdmin()
  const [groups, setGroups] = useState([])
  const [name, setName] = useState('')
  const [description, setDescription] = useState('')
  const [editId, setEditId] = useState(null)
  const [openGroupId, setOpenGroupId] = useState(null)
  const [members, setMembers] = useState({ items: [], total: 0 })
  const [flash, setFlash] = useState('')
  const [error, setError] = useState('')
  const [busy, setBusy] = useState(false)

  const load = useCallback(async () => {
    try {
      setGroups(await apiRequest('/admin/sms/groups', { token: session.token }))
    } catch (requestError) {
      setError(getReadableError(requestError))
    }
  }, [session.token])

  useEffect(() => {
    if (session.token) load()
  }, [session.token, load])

  const loadMembers = async (groupId) => {
    try {
      const data = await apiRequest(`/admin/sms/groups/${groupId}/members?page=1&pageSize=50`, { token: session.token })
      setMembers({ items: data?.items ?? [], total: data?.total ?? 0 })
    } catch (requestError) {
      setError(getReadableError(requestError))
    }
  }

  const save = async () => {
    setBusy(true)
    setError('')
    setFlash('')
    try {
      const payload = { name, description: description || null }
      if (editId) {
        await apiRequest(`/admin/sms/groups/${editId}`, { method: 'PUT', token: session.token, body: payload })
      } else {
        await apiRequest('/admin/sms/groups', { method: 'POST', token: session.token, body: payload })
      }
      setFlash(editId ? 'گروه ویرایش شد.' : 'گروه ساخته شد.')
      setName('')
      setDescription('')
      setEditId(null)
      await load()
    } catch (requestError) {
      setError(getReadableError(requestError))
    } finally {
      setBusy(false)
      setTimeout(() => setFlash(''), 6000)
    }
  }

  const remove = async (group) => {
    setBusy(true)
    try {
      await apiRequest(`/admin/sms/groups/${group.id}`, { method: 'DELETE', token: session.token })
      setFlash('گروه حذف شد.')
      if (openGroupId === group.id) setOpenGroupId(null)
      await load()
    } catch (requestError) {
      setError(getReadableError(requestError))
    } finally {
      setBusy(false)
      setTimeout(() => setFlash(''), 6000)
    }
  }

  const toggleMembers = async (group) => {
    if (openGroupId === group.id) {
      setOpenGroupId(null)
      return
    }
    setOpenGroupId(group.id)
    await loadMembers(group.id)
  }

  const removeMember = async (groupId, contactId) => {
    setBusy(true)
    try {
      await apiRequest('/admin/sms/contacts/remove-from-groups', {
        method: 'POST',
        token: session.token,
        body: { contactIds: [contactId], groupIds: [groupId] },
      })
      await loadMembers(groupId)
      await load()
    } catch (requestError) {
      setError(getReadableError(requestError))
    } finally {
      setBusy(false)
    }
  }

  return (
    <div className="sms-page" dir="rtl">
      <header className="sms-page-header">
        <h1>گروه‌های مخاطبین</h1>
        <p>دسته‌بندی مخاطبین برای ارسال گروهی — عضویت یک مخاطب در چند گروه مجاز است و در ارسال، تکراری‌ها حذف می‌شوند.</p>
      </header>

      {error ? <div className="sms-error">{error}</div> : null}
      {flash ? <div className="sms-flash">{flash}</div> : null}

      <div className="sms-card">
        <h2>{editId ? 'ویرایش گروه' : 'گروه جدید'}</h2>
        <div className="sms-form">
          <div className="sms-row">
            <label>نام گروه *
              <input value={name} onChange={(event) => setName(event.target.value)} placeholder="مثلاً: مشتریان VIP" />
            </label>
            <label>توضیحات
              <input value={description} onChange={(event) => setDescription(event.target.value)} />
            </label>
          </div>
          <div className="sms-actions">
            <button type="button" className="sms-btn" disabled={busy || !name.trim()} onClick={save}>
              {editId ? 'ذخیره' : 'ایجاد گروه'}
            </button>
            {editId ? (
              <button type="button" className="sms-btn sms-btn--secondary" onClick={() => { setEditId(null); setName(''); setDescription('') }}>
                انصراف
              </button>
            ) : null}
          </div>
        </div>
      </div>

      <div className="sms-card">
        <h2>گروه‌ها</h2>
        {groups.length ? (
          <table className="sms-table">
            <thead>
              <tr>
                <th>نام</th>
                <th>توضیحات</th>
                <th>اعضا</th>
                <th>عملیات</th>
              </tr>
            </thead>
            <tbody>
              {groups.map((group) => (
                <>
                  <tr key={group.id}>
                    <td>{group.name}</td>
                    <td>{group.description ?? '—'}</td>
                    <td>{toPersian(group.memberCount)}</td>
                    <td className="sms-row" style={{ flex: 'initial' }}>
                      <button type="button" className="sms-inline-btn" onClick={() => toggleMembers(group)}>
                        {openGroupId === group.id ? 'بستن اعضا' : 'مشاهدهٔ اعضا'}
                      </button>
                      <button
                        type="button"
                        className="sms-inline-btn"
                        onClick={() => { setEditId(group.id); setName(group.name); setDescription(group.description ?? '') }}
                      >
                        ویرایش
                      </button>
                      <button type="button" className="sms-inline-btn sms-inline-btn--danger" onClick={() => remove(group)} disabled={busy}>
                        حذف
                      </button>
                    </td>
                  </tr>
                  {openGroupId === group.id ? (
                    <tr key={`${group.id}-members`}>
                      <td colSpan={4}>
                        {members.items.length ? (
                          <ul className="sms-mini-list">
                            {members.items.map((member) => (
                              <li key={member.contactId} style={{ display: 'flex', gap: 6, alignItems: 'center' }}>
                                {[member.firstName, member.lastName].filter(Boolean).join(' ') || '—'}
                                <span dir="ltr">{member.displayMobile}</span>
                                <button
                                  type="button"
                                  className="sms-inline-btn sms-inline-btn--danger"
                                  onClick={() => removeMember(group.id, member.contactId)}
                                  disabled={busy}
                                >
                                  حذف عضویت
                                </button>
                              </li>
                            ))}
                          </ul>
                        ) : (
                          <p style={{ fontSize: 12 }}>عضوی در این گروه نیست.</p>
                        )}
                      </td>
                    </tr>
                  ) : null}
                </>
              ))}
            </tbody>
          </table>
        ) : (
          <p>هنوز گروهی ساخته نشده است.</p>
        )}
      </div>
    </div>
  )
}
