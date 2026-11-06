import { useCallback, useEffect, useState } from 'react'
import { useAdmin } from '../context/AdminContext'

const emptyGroupForm = {
  id: '',
  name: '',
  description: '',
  discountPercent: 0,
  approvalMethod: 'Manual',
  emailDomain: '',
  membershipValidityDays: 0,
  displayOrder: 0,
  isEnabled: true,
}

const APPROVAL_LABELS = {
  Manual: 'دستی (توسط ادمین)',
  CorporateEmailDomain: 'دامنهٔ ایمیل سازمانی',
}

export function CustomerGroupsPage() {
  const { session, apiRequest, getReadableError, saving, setSaving, message, error, setMessage, setError } = useAdmin()

  const [groupForm, setGroupForm] = useState(emptyGroupForm)
  const [groups, setGroups] = useState([])
  const [loading, setLoading] = useState(true)

  const loadGroups = useCallback(async () => {
    if (!session?.token) return
    setLoading(true)
    try {
      const data = await apiRequest('/admin/customer-groups', { token: session.token })
      setGroups(Array.isArray(data) ? data : [])
    } catch (requestError) {
      setError(getReadableError(requestError))
    } finally {
      setLoading(false)
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [session?.token])

  useEffect(() => {
    loadGroups()
  }, [loadGroups])

  const startEdit = (group) => {
    setGroupForm({
      id: group.id,
      name: group.name ?? '',
      description: group.description ?? '',
      discountPercent: group.discountPercent ?? 0,
      approvalMethod: group.approvalMethod ?? 'Manual',
      emailDomain: group.emailDomain ?? '',
      membershipValidityDays: group.membershipValidityDays ?? 0,
      displayOrder: group.displayOrder ?? 0,
      isEnabled: group.isEnabled !== false,
    })
    setMessage('')
    setError('')
  }

  const resetForm = () => setGroupForm(emptyGroupForm)

  const handleDelete = async (group) => {
    if (!window.confirm(`حذف گروه «${group.name}»؟\nعضویت مشتریان این گروه نیز حذف می‌شود.`)) {
      return
    }

    setSaving(true)
    setError('')
    setMessage('')
    try {
      await apiRequest(`/admin/customer-groups/${group.id}`, { method: 'DELETE', token: session.token })
      setMessage('گروه مشتری حذف شد.')
      if (groupForm.id === group.id) {
        resetForm()
      }
      loadGroups()
    } catch (requestError) {
      setError(getReadableError(requestError))
    } finally {
      setSaving(false)
    }
  }

  return (
    <section className="two-column-layout">
      <form
        className="panel form-panel"
        onSubmit={async (event) => {
          event.preventDefault()
          setSaving(true)
          setError('')
          setMessage('')

          try {
            await apiRequest('/admin/customer-groups', {
              method: 'POST',
              token: session.token,
              body: {
                id: groupForm.id || '00000000-0000-0000-0000-000000000000',
                name: groupForm.name,
                description: groupForm.description || null,
                discountPercent: Number(groupForm.discountPercent) || 0,
                approvalMethod: groupForm.approvalMethod || 'Manual',
                emailDomain: groupForm.emailDomain || null,
                membershipValidityDays: Number(groupForm.membershipValidityDays) || 0,
                displayOrder: Number(groupForm.displayOrder) || 0,
                isEnabled: groupForm.isEnabled,
              },
            })
            setMessage(groupForm.id ? 'گروه مشتری ویرایش شد.' : 'گروه مشتری ساخته شد.')
            resetForm()
            loadGroups()
          } catch (requestError) {
            setError(getReadableError(requestError))
          } finally {
            setSaving(false)
          }
        }}
      >
        <div className="panel-head">
          <div>
            <h2>{groupForm.id ? 'ویرایش گروه مشتری' : 'گروه مشتری جدید'}</h2>
            <p>هر گروه یک تخفیف دائمی، روش تایید عضویت و مدت اعتبار دارد.</p>
          </div>
        </div>

        <label>
          <span>نام گروه</span>
          <input
            required
            value={groupForm.name}
            onChange={(event) => setGroupForm((current) => ({ ...current, name: event.target.value }))}
            placeholder="مثلاً دانشجویان یا کارمندان شرکت X"
          />
        </label>

        <label>
          <span>توضیح</span>
          <textarea
            rows="2"
            value={groupForm.description}
            onChange={(event) => setGroupForm((current) => ({ ...current, description: event.target.value }))}
          />
        </label>

        <label>
          <span>تخفیف گروه (٪)</span>
          <input
            className="ltr-input"
            dir="ltr"
            type="number"
            min="0"
            max="100"
            step="0.5"
            value={groupForm.discountPercent}
            onChange={(event) => setGroupForm((current) => ({ ...current, discountPercent: event.target.value }))}
          />
          <small className="settings-hint">این درصد به‌صورت دائمی روی سفارش اعضای فعال گروه اعمال می‌شود.</small>
        </label>

        <label>
          <span>روش تایید عضویت</span>
          <select
            value={groupForm.approvalMethod}
            onChange={(event) => setGroupForm((current) => ({ ...current, approvalMethod: event.target.value }))}
          >
            <option value="Manual">دستی (توسط ادمین)</option>
            <option value="CorporateEmailDomain">دامنهٔ ایمیل سازمانی</option>
          </select>
        </label>

        {groupForm.approvalMethod === 'CorporateEmailDomain' ? (
          <label>
            <span>دامنهٔ ایمیل سازمانی</span>
            <input
              className="ltr-input"
              dir="ltr"
              value={groupForm.emailDomain}
              onChange={(event) => setGroupForm((current) => ({ ...current, emailDomain: event.target.value }))}
              placeholder="company.com"
            />
            <small className="settings-hint">مشتریانی که ایمیلشان به این دامنه ختم شود، خودکار تایید می‌شوند.</small>
          </label>
        ) : null}

        <label>
          <span>مدت اعتبار عضویت (روز)</span>
          <input
            className="ltr-input"
            dir="ltr"
            type="number"
            min="0"
            value={groupForm.membershipValidityDays}
            onChange={(event) =>
              setGroupForm((current) => ({ ...current, membershipValidityDays: event.target.value }))
            }
          />
          <small className="settings-hint">مقدار ۰ = بدون انقضا</small>
        </label>

        <label>
          <span>ترتیب نمایش</span>
          <input
            className="ltr-input"
            dir="ltr"
            type="number"
            min="0"
            value={groupForm.displayOrder}
            onChange={(event) => setGroupForm((current) => ({ ...current, displayOrder: event.target.value }))}
          />
        </label>

        <label className="checkbox-row">
          <input
            type="checkbox"
            checked={groupForm.isEnabled}
            onChange={(event) => setGroupForm((current) => ({ ...current, isEnabled: event.target.checked }))}
          />
          <span>گروه فعال است</span>
        </label>

        <div className="form-actions">
          <button type="submit" disabled={saving}>
            {saving ? 'در حال ذخیره...' : groupForm.id ? 'ذخیره تغییرات' : 'ساخت گروه'}
          </button>
          {groupForm.id ? (
            <button type="button" className="ghost-button" onClick={resetForm}>
              انصراف
            </button>
          ) : null}
        </div>

        {error ? <p className="feedback feedback-error">{error}</p> : null}
        {message ? <p className="feedback feedback-success">{message}</p> : null}
      </form>

      <section className="panel">
        <div className="panel-head">
          <div>
            <h2>گروه‌های مشتری</h2>
            <p>تعداد اعضای فعال هر گروه در ستون آخر نمایش داده می‌شود.</p>
          </div>
        </div>

        {loading ? (
          <p>در حال دریافت گروه‌ها...</p>
        ) : groups.length === 0 ? (
          <p>هنوز گروهی ساخته نشده است.</p>
        ) : (
          <table className="admin-table">
            <thead>
              <tr>
                <th>نام</th>
                <th>تخفیف</th>
                <th>روش تایید</th>
                <th>اعتبار (روز)</th>
                <th>اعضای فعال</th>
                <th>وضعیت</th>
                <th />
              </tr>
            </thead>
            <tbody>
              {groups.map((group) => (
                <tr key={group.id}>
                  <td>
                    <strong>{group.name}</strong>
                    {group.description ? <div className="table-subtext">{group.description}</div> : null}
                  </td>
                  <td>{group.discountPercent}٪</td>
                  <td>
                    {APPROVAL_LABELS[group.approvalMethod] ?? '—'}
                    {group.approvalMethod === 'CorporateEmailDomain' && group.emailDomain ? (
                      <div className="table-subtext" dir="ltr">@{group.emailDomain}</div>
                    ) : null}
                  </td>
                  <td>{group.membershipValidityDays > 0 ? group.membershipValidityDays : 'بدون انقضا'}</td>
                  <td>{group.activeMemberCount}</td>
                  <td>{group.isEnabled ? 'فعال' : 'غیرفعال'}</td>
                  <td className="table-actions">
                    <button type="button" className="link-button" onClick={() => startEdit(group)}>
                      ویرایش
                    </button>
                    <button type="button" className="link-button link-danger" onClick={() => handleDelete(group)}>
                      حذف
                    </button>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        )}
      </section>
    </section>
  )
}
