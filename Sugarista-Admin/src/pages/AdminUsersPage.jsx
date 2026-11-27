import { useEffect, useMemo, useState } from 'react'
import { useAdmin } from '../context/AdminContext'
import { apiRequest, getReadableError } from '../utils/adminApi.js'
import { hasPermission, PERMISSIONS } from '../utils/adminPermissions.js'
import { toPersianDigits } from '../utils/persianDigits.js'
import {
  applyCategoryPreset,
  applySectionPreset,
  groupCatalogByCategory,
  toggleSectionPermission,
} from '../utils/permissionMatrixHelpers.js'
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

function CategorizedPermissionMatrix({
  catalog,
  selectedKeys,
  onChange,
  disabled = false,
}) {
  const [search, setSearch] = useState('')
  const [openCategories, setOpenCategories] = useState(() => ({ main: true, menu: true }))

  const categories = useMemo(
    () => groupCatalogByCategory(catalog, selectedKeys, search),
    [catalog, selectedKeys, search],
  )

  const totalCatalogCount = useMemo(
    () => (catalog ?? []).reduce((sum, sec) => sum + (sec.permissions?.length ?? 0), 0),
    [catalog],
  )

  const isSearching = search.trim().length > 0

  const toggleCategoryOpen = (catId) => {
    setOpenCategories((prev) => ({ ...prev, [catId]: !prev[catId] }))
  }

  return (
    <div className="au-matrix">
      <div className="au-matrix-toolbar">
        <div className="au-matrix-search-wrap">
          <input
            type="search"
            className="au-matrix-search"
            placeholder="جستجوی منو یا دسترسی (مثلاً: سفارش‌ها، صندوق، انبار)..."
            value={search}
            onChange={(e) => setSearch(e.target.value)}
          />
          <span className="au-matrix-counter">
            {toPersianDigits((selectedKeys ?? []).length)} از {toPersianDigits(totalCatalogCount)} دسترسی
          </span>
        </div>

        {!disabled ? (
          <div className="au-matrix-global-actions">
            <button
              type="button"
              className="au-chip-btn is-viewer"
              onClick={() => onChange(applyCategoryPreset(selectedKeys, catalog, 'viewer'))}
              title="فقط دسترسی مشاهده (Viewer) برای تمام بخش‌ها"
            >
              فقط مشاهده همه
            </button>
            <button
              type="button"
              className="au-chip-btn"
              onClick={() => onChange(applyCategoryPreset(selectedKeys, catalog, 'full'))}
            >
              انتخاب همه
            </button>
            <button
              type="button"
              className="au-chip-btn is-danger"
              onClick={() => onChange([])}
            >
              پاک کردن همه
            </button>
          </div>
        ) : null}
      </div>

      <div className="au-cat-list">
        {categories.map((cat) => {
          const isOpen = isSearching || Boolean(openCategories[cat.categoryId])
          const hasAnySelected = cat.selectedCount > 0

          return (
            <section
              key={cat.categoryId}
              className={`au-cat-card ${hasAnySelected ? 'has-selected' : ''}`}
            >
              <header className="au-cat-head">
                <button
                  type="button"
                  className="au-cat-toggle"
                  onClick={() => toggleCategoryOpen(cat.categoryId)}
                  aria-expanded={isOpen}
                >
                  <span className={`au-chevron ${isOpen ? 'is-open' : ''}`} aria-hidden="true">
                    ‹
                  </span>
                  <strong className="au-cat-title">{cat.categoryLabel}</strong>
                  <span className={`au-cat-count ${hasAnySelected ? 'is-active' : ''}`}>
                    {toPersianDigits(cat.selectedCount)} / {toPersianDigits(cat.totalCount)}
                  </span>
                </button>

                {!disabled ? (
                  <div className="au-cat-presets">
                    <button
                      type="button"
                      className="au-mini-btn is-viewer"
                      onClick={() =>
                        onChange(applyCategoryPreset(selectedKeys, cat.sections, 'viewer'))
                      }
                    >
                      فقط مشاهده
                    </button>
                    <button
                      type="button"
                      className="au-mini-btn"
                      onClick={() =>
                        onChange(
                          applyCategoryPreset(
                            selectedKeys,
                            cat.sections,
                            cat.selectedCount === cat.totalCount ? 'none' : 'full',
                          ),
                        )
                      }
                    >
                      {cat.selectedCount === cat.totalCount ? 'حذف همه' : 'همه'}
                    </button>
                  </div>
                ) : null}
              </header>

              {isOpen ? (
                <div className="au-sections-list">
                  {cat.sections.map((section) => (
                    <div
                      key={section.sectionId}
                      className={`au-section-row ${section.selectedCount > 0 ? 'has-active' : ''}`}
                    >
                      <div className="au-section-info">
                        <span className="au-section-name">{section.sectionLabel}</span>
                        {!disabled ? (
                          <div className="au-section-quick">
                            {section.permissions.some((p) => p.isView) ? (
                              <button
                                type="button"
                                className={`au-quick-tag ${section.isViewerOnly ? 'is-selected' : ''}`}
                                onClick={() =>
                                  onChange(
                                    applySectionPreset(
                                      selectedKeys,
                                      section,
                                      section.isViewerOnly ? 'none' : 'viewer',
                                    ),
                                  )
                                }
                                title="فقط مشاهده (Viewer)"
                              >
                                Viewer
                              </button>
                            ) : null}
                            <button
                              type="button"
                              className={`au-quick-tag ${section.isFull ? 'is-selected' : ''}`}
                              onClick={() =>
                                onChange(
                                  applySectionPreset(
                                    selectedKeys,
                                    section,
                                    section.isFull ? 'none' : 'full',
                                  ),
                                )
                              }
                            >
                              {section.isFull ? 'کامل ✓' : 'کامل'}
                            </button>
                          </div>
                        ) : null}
                      </div>

                      <div className="au-perm-pills">
                        {section.permissions.map((perm) => (
                          <label
                            key={perm.key}
                            className={`au-perm-pill ${perm.checked ? 'is-checked' : ''} ${
                              perm.isView ? 'is-view' : ''
                            } ${disabled ? 'is-disabled' : ''}`}
                          >
                            <input
                              type="checkbox"
                              checked={perm.checked}
                              disabled={disabled}
                              onChange={(e) =>
                                onChange(
                                  toggleSectionPermission(
                                    selectedKeys,
                                    section,
                                    perm.key,
                                    e.target.checked,
                                  ),
                                )
                              }
                            />
                            <span>{perm.actionLabel}</span>
                          </label>
                        ))}
                      </div>
                    </div>
                  ))}
                </div>
              ) : null}
            </section>
          )
        })}

        {categories.length === 0 ? (
          <p className="au-empty-search">هیچ منو یا دسترسی با عبارت «{search}» پیدا نشد.</p>
        ) : null}
      </div>
    </div>
  )
}

export function AdminUsersPage() {
  const { session, setMessage, setError, saving, setSaving } = useAdmin()
  const [activeTab, setActiveTab] = useState('users')
  const [users, setUsers] = useState([])
  const [userSearch, setUserSearch] = useState('')
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
  const isEditingSelf = Boolean(form.id && form.id === session?.userId)

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

  const filteredUsers = useMemo(() => {
    const q = userSearch.trim().toLowerCase()
    if (!q) return users
    return users.filter((u) =>
      [u.displayName, u.username, u.role]
        .filter(Boolean)
        .some((v) => String(v).toLowerCase().includes(q)),
    )
  }, [users, userSearch])

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
      setActiveTab('users')
    } catch (requestError) {
      setError(getReadableError(requestError))
    } finally {
      setSaving(false)
    }
  }

  function applyRoleTemplateToUser(roleName) {
    const matched = roles.find((r) => r.name === roleName)
    if (!matched) return
    setForm((current) => ({
      ...current,
      role: matched.name,
      permissions: [...(matched.permissions ?? [])],
    }))
    setMessage(`دسترسی‌های قالب «${matched.displayName}» بارگذاری شد؛ برای اعمال نهایی ذخیره کنید.`)
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

        setMessage('کاربر و دسترسی‌ها با موفقیت ذخیره شدند.')
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
    if (!passwordForm.userId || !passwordForm.newPassword) return
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
    if (!window.confirm('این کاربر غیرفعال شود؟')) return
    setSaving(true)
    setError('')
    setMessage('')
    try {
      await apiRequest(`/admin/users/${userId}`, { method: 'DELETE', token: session.token })
      if (form.id === userId) resetForm()
      setMessage('کاربر غیرفعال شد.')
      await loadUsers()
    } catch (requestError) {
      setError(getReadableError(requestError))
    } finally {
      setSaving(false)
    }
  }

  const canSaveForm = form.id ? canUpdate || canManagePermissions : canCreate
  const permissionsDisabled = !canManagePermissions || isEditingSelf

  return (
    <div className="au-page" dir="rtl">
      <header className="au-header">
        <div>
          <h2 className="au-title">مدیریت کاربران و سطوح دسترسی</h2>
          <p className="au-subtitle">
            تعریف کاربران پنل و کنترل دقیق دسترسی CRUD برای تک‌تک منوها به‌صورت دسته‌بندی‌شده.
          </p>
        </div>

        <div className="au-header-actions">
          <div className="au-tab-strip" role="tablist">
            <button
              type="button"
              role="tab"
              aria-selected={activeTab === 'users'}
              className={`au-tab-btn ${activeTab === 'users' ? 'is-active' : ''}`}
              onClick={() => setActiveTab('users')}
            >
              کاربران ({toPersianDigits(users.length)})
            </button>
            {canManagePermissions ? (
              <button
                type="button"
                role="tab"
                aria-selected={activeTab === 'roles'}
                className={`au-tab-btn ${activeTab === 'roles' ? 'is-active' : ''}`}
                onClick={() => setActiveTab('roles')}
              >
                قالب‌های نقش ({toPersianDigits(roles.length)})
              </button>
            ) : null}
          </div>

          {canCreate && activeTab === 'users' ? (
            <button type="button" className="au-primary-btn" onClick={resetForm}>
              + کاربر جدید
            </button>
          ) : null}
        </div>
      </header>

      {activeTab === 'users' ? (
        <div className="au-workspace">
          {/* ── ستون لیست کاربران ── */}
          <aside className="au-directory">
            <div className="au-directory-head">
              <input
                type="search"
                className="au-search-input"
                placeholder="جستجوی کاربر..."
                value={userSearch}
                onChange={(e) => setUserSearch(e.target.value)}
              />
            </div>

            <div className="au-user-list">
              {loading && users.length === 0 ? (
                <p className="au-muted-note">در حال بارگذاری…</p>
              ) : null}

              {filteredUsers.map((u) => {
                const selected = form.id === u.id
                return (
                  <div
                    key={u.id}
                    className={`au-user-item ${selected ? 'is-selected' : ''} ${!u.isActive ? 'is-inactive' : ''}`}
                    onClick={() => handleSelectUser(u.id)}
                    role="button"
                    tabIndex={0}
                    onKeyDown={(e) => {
                      if (e.key === 'Enter' || e.key === ' ') {
                        e.preventDefault()
                        handleSelectUser(u.id)
                      }
                    }}
                  >
                    <span className="au-avatar">
                      {(u.displayName || u.username || 'U').trim().charAt(0)}
                    </span>
                    <div className="au-user-meta">
                      <div className="au-user-name-row">
                        <strong>{u.displayName}</strong>
                        <span className={`au-status-dot ${u.isActive ? 'is-on' : 'is-off'}`} />
                      </div>
                      <div className="au-user-sub">
                        <span dir="ltr">@{u.username}</span>
                        <span>·</span>
                        <span>{u.isSuperAdmin ? 'مدیر ارشد' : u.role}</span>
                      </div>
                    </div>
                    <span className={`au-perm-badge ${u.isSuperAdmin ? 'is-super' : ''}`}>
                      {u.isSuperAdmin
                        ? 'کامل'
                        : `${toPersianDigits(u.permissionCount ?? u.permissions?.length ?? 0)}`}
                    </span>
                  </div>
                )
              })}

              {!loading && filteredUsers.length === 0 ? (
                <p className="au-muted-note">کاربری یافت نشد.</p>
              ) : null}
            </div>
          </aside>

          {/* ── ستون ویرایش کاربر و ماتریس دسترسی ── */}
          <div className="au-editor-col">
            <form className="au-card" onSubmit={handleSubmit}>
              <div className="au-card-head">
                <div>
                  <h3>{form.id ? `ویرایش کاربر: ${form.displayName}` : 'ایجاد کاربر جدید'}</h3>
                  <p>
                    {form.id
                      ? 'اطلاعات پایه و دسترسی‌های دقیق هر منو را تنظیم و ذخیره کنید.'
                      : 'اطلاعات کاربر را وارد کنید و دسترسی منوهای مجاز را انتخاب نمایید.'}
                  </p>
                </div>
                <div className="au-card-head-actions">
                  {form.id && canDelete && !isEditingSelf && form.isActive ? (
                    <button
                      type="button"
                      className="au-danger-ghost-btn"
                      disabled={saving}
                      onClick={() => handleDeactivate(form.id)}
                    >
                      غیرفعال‌سازی
                    </button>
                  ) : null}
                  {canSaveForm ? (
                    <button type="submit" className="au-primary-btn" disabled={saving}>
                      {saving ? 'در حال ذخیره…' : form.id ? 'ذخیره تغییرات' : 'ایجاد کاربر'}
                    </button>
                  ) : null}
                </div>
              </div>

              <div className="au-form-grid">
                <label className="au-field">
                  <span>نام کاربری (انگلیسی) *</span>
                  <input
                    dir="ltr"
                    data-latin-digits="true"
                    required
                    disabled={Boolean(form.id)}
                    placeholder="barista_ali"
                    value={form.username}
                    onChange={(e) => setForm((p) => ({ ...p, username: e.target.value }))}
                  />
                </label>

                <label className="au-field">
                  <span>نام نمایشی *</span>
                  <input
                    required
                    placeholder="علی رضایی"
                    value={form.displayName}
                    onChange={(e) => setForm((p) => ({ ...p, displayName: e.target.value }))}
                  />
                </label>

                {!form.id ? (
                  <label className="au-field">
                    <span>رمز عبور (حداقل ۸ کاراکتر) *</span>
                    <input
                      dir="ltr"
                      type="password"
                      minLength={8}
                      required
                      value={form.password}
                      onChange={(e) => setForm((p) => ({ ...p, password: e.target.value }))}
                    />
                  </label>
                ) : null}

                <label className="au-field">
                  <span>نقش / عنوان</span>
                  <div className="au-role-input-row">
                    <input
                      value={form.role}
                      onChange={(e) => setForm((p) => ({ ...p, role: e.target.value }))}
                      placeholder="Cashier / Barista / Viewer"
                    />
                    {roles.length > 0 && canManagePermissions && !form.isSuperAdmin ? (
                      <select
                        className="au-role-preset-select"
                        value=""
                        aria-label="اعمال قالب نقش"
                        onChange={(e) => {
                          if (e.target.value) applyRoleTemplateToUser(e.target.value)
                        }}
                      >
                        <option value="">اعمال قالب نقش…</option>
                        {roles.map((r) => (
                          <option key={r.id} value={r.name}>
                            {r.displayName} ({toPersianDigits((r.permissions ?? []).length)})
                          </option>
                        ))}
                      </select>
                    ) : null}
                  </div>
                </label>
              </div>

              <div className="au-toggle-row">
                <label className="au-switch">
                  <input
                    type="checkbox"
                    checked={form.isActive}
                    disabled={isEditingSelf}
                    onChange={(e) => setForm((p) => ({ ...p, isActive: e.target.checked }))}
                  />
                  <span>حساب کاربری فعال باشد</span>
                </label>

                {session?.isSuperAdmin ? (
                  <label className="au-switch is-super">
                    <input
                      type="checkbox"
                      checked={form.isSuperAdmin}
                      disabled={isEditingSelf}
                      onChange={(e) => setForm((p) => ({ ...p, isSuperAdmin: e.target.checked }))}
                    />
                    <span>مدیر ارشد (دسترسی کامل به کل سیستم)</span>
                  </label>
                ) : null}
              </div>

              <div className="au-divider" />

              {form.isSuperAdmin ? (
                <div className="au-super-banner">
                  ★ این کاربر <strong>مدیر ارشد (Super Admin)</strong> است و به‌صورت خودکار دسترسی کامل به تمام منوها و عملیات سیستم دارد.
                </div>
              ) : (
                <>
                  {isEditingSelf ? (
                    <div className="au-info-banner">
                      برای جلوگیری از قفل شدن حساب خودتان، ویرایش دسترسی‌های حساب فعلی غیرفعال است.
                    </div>
                  ) : null}
                  <CategorizedPermissionMatrix
                    catalog={permissionCatalog}
                    selectedKeys={form.permissions}
                    disabled={permissionsDisabled}
                    onChange={(nextKeys) => setForm((p) => ({ ...p, permissions: nextKeys }))}
                  />
                </>
              )}
            </form>

            {form.id && canUpdate ? (
              <form className="au-card au-password-card" onSubmit={handleResetPassword}>
                <div className="au-password-row">
                  <div>
                    <h4>تغییر رمز عبور ({form.displayName})</h4>
                    <p>حداقل ۸ کاراکتر</p>
                  </div>
                  <div className="au-password-controls">
                    <input
                      dir="ltr"
                      type="password"
                      minLength={8}
                      required
                      placeholder="رمز عبور جدید..."
                      value={passwordForm.newPassword}
                      onChange={(e) =>
                        setPasswordForm((p) => ({ ...p, newPassword: e.target.value }))
                      }
                    />
                    <button
                      type="submit"
                      className="au-secondary-btn"
                      disabled={saving || !passwordForm.newPassword}
                    >
                      تغییر رمز
                    </button>
                  </div>
                </div>
              </form>
            ) : null}
          </div>
        </div>
      ) : null}

      {activeTab === 'roles' && canManagePermissions ? (
        <section className="au-card">
          <div className="au-card-head">
            <div>
              <h3>قالب‌های نقش و دسترسی آماده</h3>
              <p>برای سرعت بیشتر می‌توانید نقش‌های آماده (مثل «فقط بیننده سفارش‌ها» یا «صندوقدار») بسازید.</p>
            </div>
            {!roleEditor ? (
              <button
                type="button"
                className="au-primary-btn"
                onClick={() =>
                  setRoleEditor({ id: null, name: '', displayName: '', permissions: [] })
                }
              >
                + قالب نقش جدید
              </button>
            ) : (
              <button type="button" className="au-secondary-btn" onClick={() => setRoleEditor(null)}>
                بازگشت به لیست نقش‌ها
              </button>
            )}
          </div>

          {roleEditor ? (
            <form
              className="au-role-form"
              onSubmit={async (event) => {
                event.preventDefault()
                setSaving(true)
                setError('')
                try {
                  if (roleEditor.id) {
                    await apiRequest(`/admin/users/roles/${roleEditor.id}`, {
                      method: 'PUT',
                      token: session.token,
                      body: {
                        displayName: roleEditor.displayName,
                        permissions: roleEditor.permissions,
                      },
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
                  setMessage('قالب نقش با موفقیت ذخیره شد.')
                } catch (requestError) {
                  setError(getReadableError(requestError))
                } finally {
                  setSaving(false)
                }
              }}
            >
              <div className="au-form-grid">
                <label className="au-field">
                  <span>نام انگلیسی نقش *</span>
                  <input
                    dir="ltr"
                    data-latin-digits="true"
                    value={roleEditor.name}
                    disabled={Boolean(roleEditor.id)}
                    required={!roleEditor.id}
                    placeholder="OrderViewer"
                    onChange={(e) => setRoleEditor((p) => ({ ...p, name: e.target.value }))}
                  />
                </label>
                <label className="au-field">
                  <span>عنوان فارسی *</span>
                  <input
                    value={roleEditor.displayName}
                    required
                    placeholder="فقط مشاهده سفارش‌ها"
                    onChange={(e) => setRoleEditor((p) => ({ ...p, displayName: e.target.value }))}
                  />
                </label>
              </div>

              <CategorizedPermissionMatrix
                catalog={permissionCatalog}
                selectedKeys={roleEditor.permissions}
                onChange={(nextKeys) => setRoleEditor((p) => ({ ...p, permissions: nextKeys }))}
              />

              <div className="au-role-foot">
                <button type="button" className="au-secondary-btn" onClick={() => setRoleEditor(null)}>
                  انصراف
                </button>
                <button type="submit" className="au-primary-btn" disabled={saving}>
                  {saving ? 'در حال ذخیره…' : 'ذخیره قالب نقش'}
                </button>
              </div>
            </form>
          ) : (
            <div className="au-roles-grid">
              {roles.map((role) => (
                <article key={role.id} className="au-role-card">
                  <div className="au-role-card-top">
                    <div>
                      <strong>{role.displayName}</strong>
                      <span className="au-role-code" dir="ltr">{role.name}</span>
                    </div>
                    <span className="au-perm-badge">
                      {toPersianDigits((role.permissions ?? []).length)} دسترسی
                    </span>
                  </div>
                  <div className="au-role-card-foot">
                    <span className="au-role-meta">
                      {role.isSystem ? '★ سیستمی' : 'سفارشی'} · {toPersianDigits(role.userCount ?? 0)} کاربر
                    </span>
                    <div className="au-role-actions">
                      <button
                        type="button"
                        className="au-mini-btn"
                        onClick={() =>
                          setRoleEditor({
                            id: role.id,
                            name: role.name,
                            displayName: role.displayName,
                            permissions: [...(role.permissions ?? [])],
                          })
                        }
                      >
                        ویرایش
                      </button>
                      {!role.isSystem ? (
                        <button
                          type="button"
                          className="au-mini-btn is-danger"
                          onClick={async () => {
                            if (!window.confirm(`نقش «${role.displayName}» حذف شود؟`)) return
                            try {
                              await apiRequest(`/admin/users/roles/${role.id}`, {
                                method: 'DELETE',
                                token: session.token,
                              })
                              await loadUsers()
                              setMessage('قالب نقش حذف شد.')
                            } catch (requestError) {
                              setError(getReadableError(requestError))
                            }
                          }}
                        >
                          حذف
                        </button>
                      ) : null}
                    </div>
                  </div>
                </article>
              ))}
            </div>
          )}
        </section>
      ) : null}
    </div>
  )
}
