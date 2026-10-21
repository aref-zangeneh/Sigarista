import { useEffect, useMemo, useState } from 'react'
import { useAdmin } from '../context/AdminContext'
import { apiRequest, getReadableError } from '../utils/adminApi'
import { hasPermission, PERMISSIONS } from '../utils/adminPermissions'
import './AdminUsersPage.css'

const emptyUserForm = {
  id: null,
  username: '',
  displayName: '',
  password: '',
  role: 'Admin',
  isActive: true,
  isSuperAdmin: false,
  permissions: [],
}

function actionLabel(action) {
  const labels = {
    view: 'مشاهده',
    create: 'ایجاد',
    update: 'ویرایش',
    delete: 'حذف',
    managePermissions: 'مدیریت دسترسی',
  }
  return labels[action] ?? action
}

export function AdminUsersPage() {
  const { session, setMessage, setError, saving, setSaving } = useAdmin()
  const [users, setUsers] = useState([])
  const [permissionCatalog, setPermissionCatalog] = useState([])
  const [roles, setRoles] = useState([])
  const [roleEditor, setRoleEditor] = useState(null)
  const [loading, setLoading] = useState(true)
  const [form, setForm] = useState(emptyUserForm)
  const [passwordForm, setPasswordForm] = useState({ userId: null, newPassword: '' })

  const canCreate = hasPermission(session, PERMISSIONS['admin-users'].create)
  const canUpdate = hasPermission(session, PERMISSIONS['admin-users'].update)
  const canDelete = hasPermission(session, PERMISSIONS['admin-users'].delete)
  const canManagePermissions = hasPermission(session, PERMISSIONS['admin-users'].managePermissions)
  const isEditingSelf = form.id && form.id === session?.userId

  const groupedSelectedPermissions = useMemo(() => {
    const selected = new Set(form.permissions)
    return permissionCatalog.map((group) => ({
      ...group,
      permissions: group.permissions.map((permission) => ({
        ...permission,
        checked: selected.has(permission.key),
      })),
    }))
  }, [permissionCatalog, form.permissions])

  const groupedRolePermissions = useMemo(() => {
    if (!roleEditor) return []
    const selected = new Set(roleEditor.permissions)
    return permissionCatalog.map((group) => ({
      ...group,
      permissions: group.permissions.map((permission) => ({
        ...permission,
        checked: selected.has(permission.key),
      })),
    }))
  }, [permissionCatalog, roleEditor])

  async function loadUsers() {
    setLoading(true)
    try {
      const [usersResponse, catalogResponse, rolesResponse] = await Promise.all([
        apiRequest('/admin/users', { token: session.token }),
        apiRequest('/admin/users/permissions/catalog', { token: session.token }),
        canManagePermissions || canUpdate
          ? apiRequest('/admin/users/roles', { token: session.token })
          : Promise.resolve([]),
      ])
      setUsers(usersResponse ?? [])
      setPermissionCatalog(catalogResponse ?? [])
      setRoles(rolesResponse ?? [])
    } catch (requestError) {
      setError(getReadableError(requestError))
    } finally {
      setLoading(false)
    }
  }

  useEffect(() => {
    if (session?.token) {
      loadUsers()
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [session?.token])

  function resetForm() {
    setForm(emptyUserForm)
    setPasswordForm({ userId: null, newPassword: '' })
  }

  async function handleSelectUser(userId) {
    setSaving(true)
    setError('')
    try {
      const detail = await apiRequest(`/admin/users/${userId}`, { token: session.token })
      setForm({
        id: detail.id,
        username: detail.username,
        displayName: detail.displayName,
        password: '',
        role: detail.role,
        isActive: detail.isActive,
        isSuperAdmin: detail.isSuperAdmin,
        permissions: detail.permissions ?? [],
      })
      setPasswordForm({ userId: detail.id, newPassword: '' })
    } catch (requestError) {
      setError(getReadableError(requestError))
    } finally {
      setSaving(false)
    }
  }

  function togglePermission(permissionKey, checked) {
    setForm((current) => ({
      ...current,
      permissions: checked
        ? [...new Set([...current.permissions, permissionKey])]
        : current.permissions.filter((key) => key !== permissionKey),
    }))
  }

  function toggleSectionPermissions(sectionPermissions, checked) {
    const keys = sectionPermissions.map((permission) => permission.key)
    setForm((current) => ({
      ...current,
      permissions: checked
        ? [...new Set([...current.permissions, ...keys])]
        : current.permissions.filter((key) => !keys.includes(key)),
    }))
  }

  async function handleSubmit(event) {
    event.preventDefault()
    setSaving(true)
    setError('')
    setMessage('')

    try {
      if (form.id) {
        await apiRequest(`/admin/users/${form.id}`, {
          method: 'PUT',
          token: session.token,
          body: {
            displayName: form.displayName,
            role: form.role,
            isActive: form.isActive,
            isSuperAdmin: form.isSuperAdmin,
          },
        })

        if (!form.isSuperAdmin && canManagePermissions && !isEditingSelf) {
          await apiRequest(`/admin/users/${form.id}/permissions`, {
            method: 'PUT',
            token: session.token,
            body: { permissions: form.permissions },
          })
        }

        setMessage('کاربر با موفقیت بروزرسانی شد.')
      } else {
        await apiRequest('/admin/users', {
          method: 'POST',
          token: session.token,
          body: {
            username: form.username,
            displayName: form.displayName,
            password: form.password,
            role: form.role,
            isActive: form.isActive,
            isSuperAdmin: form.isSuperAdmin,
            permissions: form.isSuperAdmin ? [] : form.permissions,
          },
        })
        setMessage('کاربر جدید ایجاد شد.')
        resetForm()
      }

      await loadUsers()
    } catch (requestError) {
      setError(getReadableError(requestError))
    } finally {
      setSaving(false)
    }
  }

  async function handleResetPassword(event) {
    event.preventDefault()
    if (!passwordForm.userId || !passwordForm.newPassword) {
      return
    }

    setSaving(true)
    setError('')
    setMessage('')

    try {
      await apiRequest(`/admin/users/${passwordForm.userId}/password`, {
        method: 'PUT',
        token: session.token,
        body: { newPassword: passwordForm.newPassword },
      })
      setPasswordForm((current) => ({ ...current, newPassword: '' }))
      setMessage('رمز عبور با موفقیت تغییر کرد.')
    } catch (requestError) {
      setError(getReadableError(requestError))
    } finally {
      setSaving(false)
    }
  }

  async function handleDeactivate(userId) {
    if (!window.confirm('این کاربر غیرفعال شود؟')) {
      return
    }

    setSaving(true)
    setError('')
    setMessage('')

    try {
      await apiRequest(`/admin/users/${userId}`, {
        method: 'DELETE',
        token: session.token,
      })
      if (form.id === userId) {
        resetForm()
      }
      setMessage('کاربر غیرفعال شد.')
      await loadUsers()
    } catch (requestError) {
      setError(getReadableError(requestError))
    } finally {
      setSaving(false)
    }
  }

  return (
    <section className="panel-stack admin-users-page">
      <section className="two-column-layout">
        <form className="panel form-panel" onSubmit={handleSubmit}>
          <div className="panel-head">
            <div>
              <h3>{form.id ? 'ویرایش کاربر' : 'کاربر جدید'}</h3>
              <p>کاربران، نقش‌ها و دسترسی‌های دقیق هر بخش پنل را مدیریت کنید.</p>
            </div>
            <button type="button" className="ghost-button" onClick={resetForm}>
              {form.id ? 'کاربر جدید' : 'پاک کردن فرم'}
            </button>
          </div>

          {!form.id ? (
            <label>
              <span>نام کاربری</span>
              <input
                className="ltr-input"
                value={form.username}
                onChange={(event) => setForm((current) => ({ ...current, username: event.target.value }))}
                required
                disabled={!canCreate}
              />
            </label>
          ) : (
            <label>
              <span>نام کاربری</span>
              <input className="ltr-input" value={form.username} disabled />
            </label>
          )}

          <label>
            <span>نام نمایشی</span>
            <input
              value={form.displayName}
              onChange={(event) => setForm((current) => ({ ...current, displayName: event.target.value }))}
              required
              disabled={form.id ? !canUpdate : !canCreate}
            />
          </label>

          {!form.id ? (
            <label>
              <span>رمز عبور</span>
              <input
                className="ltr-input"
                type="password"
                value={form.password}
                onChange={(event) => setForm((current) => ({ ...current, password: event.target.value }))}
                required
                minLength={8}
                disabled={!canCreate}
              />
            </label>
          ) : null}

          <label>
            <span>نقش دسترسی</span>
            {roles.length > 0 ? (
              <select
                className="ltr-input"
                value={roles.some((item) => item.name === form.role) ? form.role : ''}
                disabled={form.id ? !canUpdate : !canCreate}
                onChange={(event) => {
                  const selectedName = event.target.value
                  const preset = roles.find((item) => item.name === selectedName)
                  if (!preset) {
                    setForm((current) => ({ ...current, role: selectedName }))
                    return
                  }
                  const bundle = preset.permissions ?? []
                  setForm((current) => ({
                    ...current,
                    role: selectedName,
                    permissions: [...new Set([...current.permissions, ...bundle])],
                  }))
                }}
              >
                <option value="">— بدون نقش / شخصی‌سازی کامل —</option>
                {roles.map((item) => (
                  <option key={item.id} value={item.name}>
                    {item.displayName} ({item.name}){item.isSystem ? ' ★' : ''}
                  </option>
                ))}
              </select>
            ) : (
              <input
                className="ltr-input"
                value={form.role}
                onChange={(event) => setForm((current) => ({ ...current, role: event.target.value }))}
                disabled={form.id ? !canUpdate : !canCreate}
              />
            )}
            <span className="admin-users-role-hint">مجوزهای نقش به‌صورت زنده اعمال می‌شود؛ تیک‌های اضافی شخصی‌ساز این کاربر می‌مانند.</span>
          </label>

          <label className="checkbox-row">
            <input
              type="checkbox"
              checked={form.isActive}
              onChange={(event) => setForm((current) => ({ ...current, isActive: event.target.checked }))}
              disabled={form.id ? !canUpdate || isEditingSelf : !canCreate}
            />
            <span>کاربر فعال باشد</span>
          </label>

          {session?.isSuperAdmin ? (
            <label className="checkbox-row admin-users-super-admin">
              <input
                type="checkbox"
                checked={form.isSuperAdmin}
                onChange={(event) =>
                  setForm((current) => ({
                    ...current,
                    isSuperAdmin: event.target.checked,
                    permissions: event.target.checked ? [] : current.permissions,
                  }))
                }
                disabled={form.id ? !canUpdate : !canCreate}
              />
              <span>مدیر ارشد (دسترسی کامل به همه بخش‌ها)</span>
            </label>
          ) : null}

          {!form.isSuperAdmin && canManagePermissions && (!form.id || !isEditingSelf) ? (
            <section className="admin-users-permissions">
              <div className="panel-head">
                <div>
                  <h4>دسترسی‌های بخش‌ها</h4>
                  <p>برای هر بخش، مجوزهای مشاهده، ایجاد، ویرایش و حذف را جداگانه تعیین کنید.</p>
                </div>
              </div>

              <div className="admin-users-permission-groups">
                {groupedSelectedPermissions.map((group) => {
                  const allChecked = group.permissions.every((permission) => permission.checked)
                  const someChecked = group.permissions.some((permission) => permission.checked)

                  return (
                    <article key={group.sectionId} className="admin-users-permission-group">
                      <header>
                        <label className="checkbox-row">
                          <input
                            type="checkbox"
                            checked={allChecked}
                            ref={(element) => {
                              if (element) {
                                element.indeterminate = !allChecked && someChecked
                              }
                            }}
                            onChange={(event) => toggleSectionPermissions(group.permissions, event.target.checked)}
                          />
                          <strong>{group.sectionLabel}</strong>
                        </label>
                      </header>
                      <div className="admin-users-permission-items">
                        {group.permissions.map((permission) => (
                          <label key={permission.key} className="checkbox-row">
                            <input
                              type="checkbox"
                              checked={permission.checked}
                              onChange={(event) => togglePermission(permission.key, event.target.checked)}
                            />
                            <span>{permission.actionLabel || actionLabel(permission.action)}</span>
                          </label>
                        ))}
                      </div>
                    </article>
                  )
                })}
              </div>
            </section>
          ) : null}

          {(form.id ? canUpdate : canCreate) ? (
            <button type="submit" disabled={saving}>
              {saving ? 'در حال ذخیره...' : form.id ? 'ذخیره تغییرات' : 'ایجاد کاربر'}
            </button>
          ) : null}
        </form>

        <section className="panel list-panel">
          <div className="panel-head">
            <div>
              <h3>کاربران سیستم</h3>
              <p>فقط کاربران فعال می‌توانند وارد پنل شوند.</p>
            </div>
          </div>

          {loading ? <p className="feedback">در حال بارگذاری کاربران...</p> : null}

          {!loading && users.length === 0 ? (
            <p className="feedback">هنوز کاربری ثبت نشده است.</p>
          ) : null}

          <ul className="admin-users-list">
            {users.map((user) => (
              <li key={user.id} className={form.id === user.id ? 'admin-users-list-item admin-users-list-item--active' : 'admin-users-list-item'}>
                <button type="button" className="admin-users-list-button" onClick={() => handleSelectUser(user.id)}>
                  <div>
                    <strong>{user.displayName}</strong>
                    <small className="ltr-text">@{user.username}</small>
                  </div>
                  <div className="admin-users-list-meta">
                    {user.isSuperAdmin ? <span className="admin-users-badge">مدیر ارشد</span> : null}
                    {!user.isActive ? <span className="admin-users-badge admin-users-badge--muted">غیرفعال</span> : null}
                    <small>{user.isSuperAdmin ? 'دسترسی کامل' : `${user.permissionCount} مجوز`}</small>
                  </div>
                </button>
                {canDelete && user.id !== session?.userId && user.isActive ? (
                  <button type="button" className="link-button" onClick={() => handleDeactivate(user.id)}>
                    غیرفعال‌سازی
                  </button>
                ) : null}
              </li>
            ))}
          </ul>
        </section>
      </section>

        {canManagePermissions ? (
          <section className="panel admin-roles-panel">
            <div className="panel-head">
              <div>
                <h3>نقش‌های دسترسی</h3>
                <p>
                  هر نقش بسته‌ای از مجوزهاست. کاربرانِ هر نقش مجوزها را در «ورود بعدی» به‌صورت زنده می‌گیرند؛
                  نقش‌های سیستمی (★) قابل حذف نیستند.
                </p>
              </div>
              {!roleEditor ? (
                <button
                  type="button"
                  className="ghost-button"
                  onClick={() => setRoleEditor({ id: null, name: '', displayName: '', permissions: [] })}
                >
                  + نقش جدید
                </button>
              ) : (
                <button type="button" className="ghost-button" onClick={() => setRoleEditor(null)}>
                  بستن
                </button>
              )}
            </div>

            {roleEditor ? (
              <form
                className="admin-roles-editor"
                onSubmit={async (event) => {
                  event.preventDefault()
                  try {
                    if (roleEditor.id) {
                      await apiRequest(`/admin/users/roles/${roleEditor.id}`, {
                        method: 'PUT',
                        token: session.token,
                        body: { displayName: roleEditor.displayName, permissions: roleEditor.permissions },
                      })
                    } else {
                      await apiRequest('/admin/users/roles', {
                        method: 'POST',
                        token: session.token,
                        body: {
                          name: roleEditor.name.trim(),
                          displayName: roleEditor.displayName,
                          permissions: roleEditor.permissions,
                        },
                      })
                    }
                    setRoleEditor(null)
                    await loadUsers()
                    setMessage('نقش ذخیره شد.')
                  } catch (requestError) {
                    setError(getReadableError(requestError))
                  }
                }}
              >
                <div className="field-grid">
                  <label>
                    <span>نام نقش (لاتین) *</span>
                    <input
                      className="ltr-input"
                      value={roleEditor.name}
                      disabled={Boolean(roleEditor.id)}
                      required={!roleEditor.id}
                      onChange={(event) => setRoleEditor((current) => ({ ...current, name: event.target.value }))}
                    />
                  </label>
                  <label>
                    <span>نام نمایشی *</span>
                    <input
                      value={roleEditor.displayName}
                      required
                      onChange={(event) => setRoleEditor((current) => ({ ...current, displayName: event.target.value }))}
                    />
                  </label>
                </div>

                <div className="admin-users-permission-groups">
                  {groupedRolePermissions.map((group) => (
                    <article key={group.sectionId} className="admin-users-permission-group">
                      <header>
                        <label className="checkbox-row">
                          <input
                            type="checkbox"
                            checked={group.permissions.every((permission) => permission.checked)}
                            onChange={(event) => setRoleEditor((current) => {
                              const keys = group.permissions.map((permission) => permission.key)
                              return {
                                ...current,
                                permissions: event.target.checked
                                  ? [...new Set([...current.permissions, ...keys])]
                                  : current.permissions.filter((key) => !keys.includes(key)),
                              }
                            })}
                          />
                          <strong>{group.sectionLabel}</strong>
                        </label>
                      </header>
                      <div>
                        {group.permissions.map((permission) => (
                          <label key={permission.key} className="checkbox-row">
                            <input
                              type="checkbox"
                              checked={permission.checked}
                              onChange={(event) => setRoleEditor((current) => ({
                                ...current,
                                permissions: event.target.checked
                                  ? [...new Set([...current.permissions, permission.key])]
                                  : current.permissions.filter((key) => key !== permission.key),
                              }))}
                            />
                            <span>{permission.actionLabel} — {permission.action}</span>
                          </label>
                        ))}
                      </div>
                    </article>
                  ))}
                </div>

                <div className="modal-actions">
                  <button type="submit" className="ghost-button">ذخیره نقش</button>
                  <button type="button" className="ghost-button" onClick={() => setRoleEditor(null)}>انصراف</button>
                </div>
              </form>
            ) : (
              <table className="admin-table">
                <thead>
                  <tr>
                    <th>نقش</th>
                    <th>نام</th>
                    <th>تعداد مجوزها</th>
                    <th>کاربران</th>
                    <th>عملیات</th>
                  </tr>
                </thead>
                <tbody>
                  {roles.map((role) => (
                    <tr key={role.id}>
                      <td>{role.isSystem ? '★ سیستمی' : 'زیرساخت اختصاصی'}</td>
                      <td>{role.displayName} <span className="ltr-input admin-users-role-name">{role.name}</span></td>
                      <td>{(role.permissions ?? []).length}</td>
                      <td>{role.userCount ?? 0}</td>
                      <td className="table-actions">
                        <button
                          type="button"
                          className="ghost-button"
                          onClick={() => setRoleEditor({
                            id: role.id,
                            name: role.name,
                            displayName: role.displayName,
                            permissions: [...(role.permissions ?? [])],
                          })}
                        >
                          ویرایش
                        </button>
                        {!role.isSystem ? (
                          <button
                            type="button"
                            className="ghost-button danger"
                            onClick={async () => {
                              try {
                                await apiRequest(`/admin/users/roles/${role.id}`, { method: 'DELETE', token: session.token })
                                await loadUsers()
                                setMessage('نقش حذف شد.')
                              } catch (requestError) {
                                setError(getReadableError(requestError))
                              }
                            }}
                          >
                            حذف
                          </button>
                        ) : null}
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            )}
          </section>
        ) : null}

      {form.id && canUpdate ? (
        <section className="panel admin-users-password-panel">
          <div className="panel-head">
            <div>
              <h3>تغییر رمز عبور</h3>
              <p>رمز جدید برای {form.displayName}</p>
            </div>
          </div>
          <form className="admin-users-password-form" onSubmit={handleResetPassword}>
            <label>
              <span>رمز عبور جدید</span>
              <input
                className="ltr-input"
                type="password"
                minLength={8}
                value={passwordForm.newPassword}
                onChange={(event) =>
                  setPasswordForm((current) => ({ ...current, newPassword: event.target.value }))
                }
                required
              />
            </label>
            <button type="submit" disabled={saving || !passwordForm.newPassword}>
              {saving ? 'در حال ذخیره...' : 'تغییر رمز عبور'}
            </button>
          </form>
        </section>
      ) : null}
    </section>
  )
}
