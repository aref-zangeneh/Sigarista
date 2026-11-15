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
  const [isSmart, setIsSmart] = useState(false)
  const [filterActive, setFilterActive] = useState('true')
  const [filterConsent, setFilterConsent] = useState('')
  const [filterDays, setFilterDays] = useState('')
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

  const resetForm = () => {
    setName('')
    setDescription('')
    setIsSmart(false)
    setFilterActive('true')
    setFilterConsent('')
    setFilterDays('')
    setEditId(null)
  }

  const buildSmartFilter = () => {
    if (!isSmart) return null
    return {
      IsActive: filterActive === '' ? null : filterActive === 'true',
      MarketingConsent: filterConsent === '' ? null : filterConsent === 'true',
      AddedWithinDays: filterDays ? Number(filterDays) : null,
    }
  }

  const save = async () => {
    setBusy(true)
    setError('')
    setFlash('')
    try {
      const payload = { name, description: description || null, smartFilter: buildSmartFilter() }
      if (editId) {
        await apiRequest(`/admin/sms/groups/${editId}`, { method: 'PUT', token: session.token, body: payload })
      } else {
        await apiRequest('/admin/sms/groups', { method: 'POST', token: session.token, body: payload })
      }
      setFlash(editId ? 'گروه ویرایش شد.' : 'گروه ساخته شد.')
      resetForm()
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
    const group = groups.find((entry) => entry.id === groupId)
    if (!group || group.isSmart) return // smart-group membership is filter-based, not editable
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
          <label className="sms-checkbox-row" style={{ flex: 'initial' }}>
            <input type="checkbox" checked={isSmart} onChange={(event) => setIsSmart(event.target.checked)} />
            گروه هوشمند (اعضا به‌صورت خودکار از فیلتر زیر محاسبه می‌شوند)
          </label>
          {isSmart ? (
            <div className="sms-row">
              <label>
                وضعیت مخاطب
                <select value={filterActive} onChange={(event) => setFilterActive(event.target.value)}>
                  <option value="true">فعال</option>
                  <option value="false">غیرفعال</option>
                  <option value="">بدون شرط</option>
                </select>
              </label>
              <label>
                رضایت تبلیغات
                <select value={filterConsent} onChange={(event) => setFilterConsent(event.target.value)}>
                  <option value="">بدون شرط</option>
                  <option value="true">دارای رضایت</option>
                  <option value="false">بدون رضایت</option>
                </select>
              </label>
              <label>
                اضافه‌شده در روز اخیر
                <input
                  type="number"
                  min={1}
                  max={3650}
                  value={filterDays}
                  onChange={(event) => setFilterDays(event.target.value)}
                  placeholder="مثلاً ۳۰ (اختیاری)"
                />
              </label>
            </div>
          ) : null}
          <div className="sms-actions">
            <button type="button" className="sms-btn" disabled={busy || !name.trim()} onClick={save}>
              {editId ? 'ذخیره' : 'ایجاد گروه'}
            </button>
            {editId ? (
              <button type="button" className="sms-btn sms-btn--secondary" onClick={resetForm}>
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
                    <td>
                      {group.name}
                      {group.isSmart ? <span className="sms-badge tone-info" style={{ marginInlineStart: 6 }}>هوشمند</span> : null}
                    </td>
                    <td>
                      {group.isSmart
                        ? `⚙ ${group.smartFilterDescription ?? 'فیلتر هوشمند'}`
                        : group.description ?? '—'}
                    </td>
                    <td>{toPersian(group.memberCount)}</td>
                    <td className="sms-row" style={{ flex: 'initial' }}>
                      <button type="button" className="sms-inline-btn" onClick={() => toggleMembers(group)}>
                        {openGroupId === group.id ? 'بستن اعضا' : 'مشاهدهٔ اعضا'}
                      </button>
                      <button
                        type="button"
                        className="sms-inline-btn"
                        onClick={() => {
                          setEditId(group.id)
                          setName(group.name)
                          setDescription(group.description ?? '')
                          setIsSmart(group.isSmart)
                          setFilterActive(group.smartFilter?.isActive === null || group.smartFilter?.isActive === undefined ? '' : String(group.smartFilter.isActive))
                          setFilterConsent(group.smartFilter?.marketingConsent === null || group.smartFilter?.marketingConsent === undefined ? '' : String(group.smartFilter.marketingConsent))
                          setFilterDays(group.smartFilter?.addedWithinDays ? String(group.smartFilter.addedWithinDays) : '')
                        }}
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
                        {group.isSmart ? (
                          <p style={{ fontSize: 12, margin: 0 }}>
                            اعضای گروه هوشمند از فیلتر محاسبه می‌شوند و حذف دستی ندارند — برای تغییر اعضا فیلتر گروه را ویرایش کنید.
                          </p>
                        ) : null}
                        {members.items.length ? (
                          <ul className="sms-mini-list">
                            {members.items.map((member) => (
                              <li key={member.contactId} style={{ display: 'flex', gap: 6, alignItems: 'center' }}>
                                {[member.firstName, member.lastName].filter(Boolean).join(' ') || '—'}
                                <span dir="ltr">{member.displayMobile}</span>
                                {!group.isSmart ? (
                                  <button
                                    type="button"
                                    className="sms-inline-btn sms-inline-btn--danger"
                                    onClick={() => removeMember(group.id, member.contactId)}
                                    disabled={busy}
                                  >
                                    حذف عضویت
                                  </button>
                                ) : null}
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
