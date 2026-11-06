import { useCallback, useEffect, useState } from 'react'
import { useAdmin } from '../context/AdminContext'
import { hasPermission, PERMISSIONS } from '../utils/adminPermissions'

const UNITS = [
  { value: 'Gram', label: 'گرم' },
  { value: 'Milliliter', label: 'میلی‌لیتر' },
  { value: 'Piece', label: 'عدد' },
  { value: 'Shot', label: 'شات' },
  { value: 'Pump', label: 'پمپ' },
]

function emptyOption() {
  return {
    id: null,
    name: '',
    englishName: '',
    unit: 'Gram',
    pricePerUnit: 0,
    minAmount: 0,
    maxAmount: 10,
    step: 1,
    defaultAmount: 0,
    isDefault: false,
    isActive: true,
    displayOrder: 0,
  }
}

function emptyGroup(inputType = 'Slider') {
  const isChoice = inputType === 'Choice'

  return {
    id: null,
    name: '',
    inputType,
    selectionMode: isChoice ? 'Single' : 'Multiple',
    minSelections: 0,
    maxSelections: isChoice ? 1 : 5,
    isRequired: false,
    displayOrder: 0,
    options: [emptyOption()],
  }
}

function emptyProfile() {
  return {
    id: null,
    menuItemId: '',
    name: '',
    isEnabled: true,
    displayOrder: 0,
    groups: [],
  }
}

function normalizeOption(option) {
  return {
    id: option?.id ?? null,
    name: option?.name ?? '',
    englishName: option?.englishName ?? '',
    unit: option?.unit ?? 'Gram',
    pricePerUnit: Number(option?.pricePerUnit ?? 0),
    minAmount: Number(option?.minAmount ?? 0),
    maxAmount: Number(option?.maxAmount ?? 0),
    step: Number(option?.step ?? 1),
    defaultAmount: Number(option?.defaultAmount ?? 0),
    isDefault: Boolean(option?.isDefault),
    isActive: option?.isActive ?? true,
    displayOrder: Number(option?.displayOrder ?? 0),
  }
}

function normalizeGroup(group) {
  const inputType = group?.inputType === 'Choice' ? 'Choice' : 'Slider'

  return {
    id: group?.id ?? null,
    name: group?.name ?? '',
    inputType,
    selectionMode: group?.selectionMode === 'Multiple' ? 'Multiple' : 'Single',
    minSelections: Number(group?.minSelections ?? 0),
    maxSelections: Number(group?.maxSelections ?? 1),
    isRequired: Boolean(group?.isRequired),
    displayOrder: Number(group?.displayOrder ?? 0),
    options: (group?.options ?? []).map(normalizeOption),
  }
}

function normalizeProfile(profile) {
  return {
    id: profile?.id ?? null,
    menuItemId: profile?.menuItemId ?? '',
    menuItemPersianName: profile?.menuItemPersianName ?? '',
    name: profile?.name ?? '',
    isEnabled: profile?.isEnabled ?? true,
    displayOrder: Number(profile?.displayOrder ?? 0),
    groups: (profile?.groups ?? []).map(normalizeGroup),
  }
}

function isAligned(amount, minAmount, step) {
  if (amount === 0) {
    return false
  }

  if (!step || step <= 0) {
    return true
  }

  const remainder = (amount - minAmount) % step
  return Math.abs(remainder) < 0.0001 || Math.abs(Math.abs(remainder) - step) < 0.0001
}

function validateProfile(profile) {
  if (!profile.menuItemId) {
    return 'آیتم منوی نوشیدنی را انتخاب کنید.'
  }

  if (!profile.name.trim()) {
    return 'نام پروفایل نوشیدنی را وارد کنید.'
  }

  if (!profile.groups.length) {
    return 'حداقل یک گروه برای نوشیدنی سفارشی اضافه کنید.'
  }

  for (const [groupIndex, group] of profile.groups.entries()) {
    if (!group.name.trim()) {
      return `نام گروه ${groupIndex + 1} را وارد کنید.`
    }

    if (!group.options.length) {
      return `گروه «${group.name}» باید حداقل یک گزینه داشته باشد.`
    }

    if (group.inputType === 'Choice') {
      if (Number(group.maxSelections) < 1) {
        return `حداکثر انتخاب گروه «${group.name}» باید حداقل ۱ باشد.`
      }

      if (Number(group.minSelections) < 0) {
        return `حداقل انتخاب گروه «${group.name}» نمی‌تواند منفی باشد.`
      }

      if (Number(group.minSelections) > Number(group.maxSelections)) {
        return `حداقل انتخاب گروه «${group.name}» نمی‌تواند از حداکثر بیشتر باشد.`
      }

      if (
        group.selectionMode === 'Single' &&
        group.options.filter((option) => option.isDefault).length > 1
      ) {
        return `در گروه «${group.name}» فقط یک گزینه می‌تواند پیش‌فرض باشد.`
      }
    }

    for (const option of group.options) {
      if (!option.name.trim()) {
        return `نام یکی از گزینه‌های گروه «${group.name}» را وارد کنید.`
      }

      if (Number(option.pricePerUnit) < 0) {
        return `قیمت هر واحد «${option.name}» نمی‌تواند منفی باشد.`
      }

      if (group.inputType !== 'Slider') {
        continue
      }

      if (Number(option.step) <= 0) {
        return `گام تغییر مقدار «${option.name}» باید بزرگ‌تر از صفر باشد.`
      }

      if (Number(option.maxAmount) < Number(option.minAmount)) {
        return `حداکثر مقدار «${option.name}» نمی‌تواند از حداقل کمتر باشد.`
      }

      const defaultAmount = Number(option.defaultAmount) || 0
      if (
        defaultAmount !== 0 &&
        (defaultAmount < Number(option.minAmount) || defaultAmount > Number(option.maxAmount))
      ) {
        return `مقدار پیش‌فرض «${option.name}» باید بین حداقل و حداکثر مقدار باشد.`
      }

      if (
        defaultAmount !== 0 &&
        !isAligned(defaultAmount, Number(option.minAmount), Number(option.step))
      ) {
        return `مقدار پیش‌فرض «${option.name}» باید مضربی از گام باشد.`
      }
    }
  }

  return null
}

function toPayload(profile) {
  return {
    menuItemId: profile.menuItemId,
    name: profile.name.trim(),
    isEnabled: Boolean(profile.isEnabled),
    displayOrder: Math.max(0, Number(profile.displayOrder) || 0),
    groups: profile.groups.map((group, groupIndex) => {
      const isChoice = group.inputType === 'Choice'
      const maxSelections = isChoice
        ? Math.max(1, Number(group.maxSelections) || 1)
        : Math.max(1, group.options.length)

      return {
        id: group.id,
        name: group.name.trim(),
        inputType: group.inputType,
        selectionMode: isChoice ? group.selectionMode : 'Multiple',
        minSelections: isChoice ? Math.max(0, Number(group.minSelections) || 0) : 0,
        maxSelections,
        isRequired: isChoice ? Boolean(group.isRequired) : false,
        displayOrder: Math.max(0, Number(group.displayOrder) || groupIndex),
        options: group.options.map((option, optionIndex) => {
          const isDefault = isChoice ? Boolean(option.isDefault) : Number(option.defaultAmount) > 0

          return {
            id: option.id,
            name: option.name.trim(),
            englishName: option.englishName?.trim() || null,
            unit: option.unit,
            pricePerUnit: Math.max(0, Number(option.pricePerUnit) || 0),
            minAmount: isChoice ? 0 : Math.max(0, Number(option.minAmount) || 0),
            maxAmount: isChoice ? 1 : Math.max(0, Number(option.maxAmount) || 0),
            step: isChoice ? 1 : Math.max(0, Number(option.step) || 0),
            defaultAmount: isChoice
              ? (isDefault ? 1 : 0)
              : Math.max(0, Number(option.defaultAmount) || 0),
            isDefault,
            isActive: Boolean(option.isActive),
            displayOrder: Math.max(0, Number(option.displayOrder) || optionIndex),
          }
        }),
      }
    }),
  }
}

export function DrinkBuilderPage() {
  const { session, apiRequest, getReadableError, setMessage, setError } = useAdmin()

  const [settings, setSettings] = useState({ isEnabled: true })
  const [settingsSaving, setSettingsSaving] = useState(false)
  const [profiles, setProfiles] = useState([])
  const [menuItems, setMenuItems] = useState([])
  const [loading, setLoading] = useState(true)
  const [saving, setSaving] = useState(false)
  const [form, setForm] = useState(null)

  const canCreate = hasPermission(session, PERMISSIONS['drink-builder'].create)
  const canUpdate = hasPermission(session, PERMISSIONS['drink-builder'].update)
  const canDelete = hasPermission(session, PERMISSIONS['drink-builder'].delete)

  const reloadProfiles = useCallback(
    async (preferredId = null) => {
      const data = await apiRequest('/admin/drink-builder/profiles', { token: session.token })
      const nextProfiles = Array.isArray(data) ? data : []
      setProfiles(nextProfiles)

      const target =
        nextProfiles.find((profile) => profile.id === preferredId) ??
        nextProfiles.find((profile) => profile.id === form?.id) ??
        nextProfiles[0] ??
        null

      setForm(target ? normalizeProfile(target) : null)
      return nextProfiles
    },
    [apiRequest, session.token, form?.id],
  )

  useEffect(() => {
    let cancelled = false

    async function load() {
      if (!session?.token) {
        return
      }

      setLoading(true)
      setError('')

      try {
        const [settingsData, profilesData, menuItemsData] = await Promise.all([
          apiRequest('/admin/drink-builder/settings', { token: session.token }),
          apiRequest('/admin/drink-builder/profiles', { token: session.token }),
          apiRequest('/admin/menu-items/options', { token: session.token }),
        ])

        if (cancelled) {
          return
        }

        setSettings({ isEnabled: settingsData?.isEnabled ?? true })
        setMenuItems(Array.isArray(menuItemsData) ? menuItemsData : [])

        const nextProfiles = Array.isArray(profilesData) ? profilesData : []
        setProfiles(nextProfiles)
        setForm(nextProfiles.length ? normalizeProfile(nextProfiles[0]) : null)
      } catch (requestError) {
        if (!cancelled) {
          setError(getReadableError(requestError))
        }
      } finally {
        if (!cancelled) {
          setLoading(false)
        }
      }
    }

    load()

    return () => {
      cancelled = true
    }
  }, [apiRequest, getReadableError, session?.token, setError])

  async function toggleSettings(nextEnabled) {
    setSettingsSaving(true)
    setError('')
    setMessage('')

    try {
      const result = await apiRequest('/admin/drink-builder/settings', {
        method: 'PUT',
        token: session.token,
        body: { isEnabled: nextEnabled },
      })
      setSettings({ isEnabled: Boolean(result) })
      setMessage(
        result
          ? 'سازندهٔ نوشیدنی سفارشی برای مشتریان فعال شد.'
          : 'سازندهٔ نوشیدنی سفارشی غیرفعال شد و از منو حذف می‌شود.',
      )
    } catch (requestError) {
      setError(getReadableError(requestError))
    } finally {
      setSettingsSaving(false)
    }
  }

  function selectProfile(profile) {
    setError('')
    setMessage('')
    setForm(normalizeProfile(profile))
  }

  function startNewProfile() {
    setError('')
    setMessage('')
    setForm(emptyProfile())
  }

  function updateForm(patch) {
    setForm((current) => (current ? { ...current, ...patch } : current))
  }

  function updateGroup(groupIndex, patch) {
    setForm((current) =>
      current
        ? {
            ...current,
            groups: current.groups.map((group, index) =>
              index === groupIndex ? { ...group, ...patch } : group,
            ),
          }
        : current,
    )
  }

  function updateOption(groupIndex, optionIndex, patch) {
    setForm((current) =>
      current
        ? {
            ...current,
            groups: current.groups.map((group, index) =>
              index === groupIndex
                ? {
                    ...group,
                    options: group.options.map((option, innerIndex) =>
                      innerIndex === optionIndex ? { ...option, ...patch } : option,
                    ),
                  }
                : group,
            ),
          }
        : current,
    )
  }

  function addGroup(inputType) {
    setForm((current) =>
      current
        ? {
            ...current,
            groups: [
              ...current.groups,
              { ...emptyGroup(inputType), displayOrder: current.groups.length },
            ],
          }
        : current,
    )
  }

  function removeGroup(groupIndex) {
    setForm((current) =>
      current
        ? {
            ...current,
            groups: current.groups.filter((_, index) => index !== groupIndex),
          }
        : current,
    )
  }

  function changeGroupType(groupIndex, inputType) {
    setForm((current) =>
      current
        ? {
            ...current,
            groups: current.groups.map((group, index) => {
              if (index !== groupIndex) {
                return group
              }

              const isChoice = inputType === 'Choice'

              return {
                ...group,
                inputType,
                selectionMode: isChoice ? group.selectionMode : 'Multiple',
                minSelections: isChoice ? group.minSelections : 0,
                maxSelections: isChoice
                  ? Math.max(1, group.maxSelections)
                  : Math.max(1, group.options.length),
                isRequired: isChoice ? group.isRequired : false,
                options: group.options.map((option) =>
                  isChoice
                    ? {
                        ...option,
                        minAmount: 0,
                        maxAmount: 1,
                        step: 1,
                        defaultAmount: option.isDefault ? 1 : 0,
                      }
                    : option,
                ),
              }
            }),
          }
        : current,
    )
  }

  function addOption(groupIndex) {
    setForm((current) =>
      current
        ? {
            ...current,
            groups: current.groups.map((group, index) =>
              index === groupIndex
                ? {
                    ...group,
                    options: [
                      ...group.options,
                      { ...emptyOption(), displayOrder: group.options.length },
                    ],
                  }
                : group,
            ),
          }
        : current,
    )
  }

  function removeOption(groupIndex, optionIndex) {
    setForm((current) =>
      current
        ? {
            ...current,
            groups: current.groups.map((group, index) =>
              index === groupIndex
                ? {
                    ...group,
                    options: group.options.filter((_, innerIndex) => innerIndex !== optionIndex),
                  }
                : group,
            ),
          }
        : current,
    )
  }

  async function handleSave(event) {
    event.preventDefault()

    if (!form) {
      return
    }

    const validationMessage = validateProfile(form)
    if (validationMessage) {
      setError(validationMessage)
      setMessage('')
      return
    }

    setSaving(true)
    setError('')
    setMessage('')

    try {
      const path = form.id
        ? `/admin/drink-builder/profiles/${form.id}`
        : '/admin/drink-builder/profiles'

      const savedId = await apiRequest(path, {
        method: form.id ? 'PUT' : 'POST',
        token: session.token,
        body: toPayload(form),
      })

      setMessage('پروفایل نوشیدنی سفارشی با موفقیت ذخیره شد.')
      await reloadProfiles(savedId)
    } catch (requestError) {
      setError(getReadableError(requestError))
    } finally {
      setSaving(false)
    }
  }

  async function handleDelete() {
    if (!form?.id) {
      return
    }

    if (!window.confirm(`پروفایل «${form.name}» حذف شود؟`)) {
      return
    }

    setSaving(true)
    setError('')
    setMessage('')

    try {
      await apiRequest(`/admin/drink-builder/profiles/${form.id}`, {
        method: 'DELETE',
        token: session.token,
      })
      setMessage('پروفایل نوشیدنی سفارشی حذف شد.')
      await reloadProfiles(null)
    } catch (requestError) {
      setError(getReadableError(requestError))
    } finally {
      setSaving(false)
    }
  }

  return (
    <section className="panel-stack">
      <section className="panel">
        <div className="panel-head">
          <div>
            <h3>سازندهٔ نوشیدنی سفارشی</h3>
            <p>
              مشتری می‌تواند اندازه، شات، شیر، سیروپ و افزودنی‌ها را انتخاب کند. قیمت نهایی
              همیشه در سرور محاسبه و اعتبارسنجی می‌شود.
            </p>
          </div>
          <label className="checkbox-row builder-master-switch">
            <input
              type="checkbox"
              checked={settings.isEnabled}
              disabled={!canUpdate || settingsSaving}
              onChange={(event) => toggleSettings(event.target.checked)}
            />
            <span>{settings.isEnabled ? 'فعال برای مشتریان' : 'غیرفعال (مخفی)'}</span>
          </label>
        </div>
        {!settings.isEnabled ? (
          <p className="builder-hint">
            با غیرفعال کردن این گزینه، دکمهٔ «نوشیدنی سفارشی» از منوی دیجیتال و همهٔ
            محاسبات مرتبط حذف می‌شود. تنظیمات شما حفظ می‌شود و با فعال‌سازی دوباره برمی‌گردد.
          </p>
        ) : null}
      </section>

      <div className="two-column-layout">
        <section className="panel list-panel">
          <div className="panel-head">
            <div>
              <h3>پروفایل‌های نوشیدنی</h3>
              <p>هر آیتم منو می‌تواند یک پروفایل سفارشی‌سازی داشته باشد.</p>
            </div>
            {canCreate ? (
              <button type="button" className="ghost-button" onClick={startNewProfile}>
                پروفایل جدید
              </button>
            ) : null}
          </div>

          {loading ? (
            <p className="table-subtext">در حال بارگذاری...</p>
          ) : profiles.length ? (
            <div className="builder-list">
              {profiles.map((profile) => (
                <button
                  key={profile.id}
                  type="button"
                  className={`builder-list-item ${form?.id === profile.id ? 'is-active' : ''}`}
                  onClick={() => selectProfile(profile)}
                >
                  <span className="builder-list-title">{profile.name}</span>
                  <span className="table-subtext">{profile.menuItemPersianName}</span>
                  <span className="builder-list-meta">
                    <span className={`builder-badge ${profile.isEnabled ? 'is-on' : 'is-off'}`}>
                      {profile.isEnabled ? 'فعال' : 'غیرفعال'}
                    </span>
                    <span className="table-subtext">
                      {profile.groups?.length ?? 0} گروه
                    </span>
                  </span>
                </button>
              ))}
            </div>
          ) : (
            <p className="table-subtext">هنوز پروفایلی ساخته نشده است.</p>
          )}
        </section>

        {form ? (
          <form className="panel form-panel" onSubmit={handleSave}>
            <div className="panel-head">
              <div>
                <h3>{form.id ? `ویرایش «${form.name}»` : 'پروفایل نوشیدنی جدید'}</h3>
                <p>قیمت هر واحد، حداقل و حداکثر مقدار هر جزء را دقیق تعیین کنید.</p>
              </div>
              {form.id && canDelete ? (
                <button
                  type="button"
                  className="ghost-button builder-danger"
                  onClick={handleDelete}
                  disabled={saving}
                >
                  حذف پروفایل
                </button>
              ) : null}
            </div>

            <div className="field-grid">
              <label>
                <span>آیتم منو</span>
                <select
                  value={form.menuItemId}
                  disabled={saving}
                  onChange={(event) => updateForm({ menuItemId: event.target.value })}
                >
                  <option value="">انتخاب کنید...</option>
                  {menuItems.map((item) => (
                    <option key={item.id} value={item.id}>
                      {item.persianName}
                      {item.isActive === false ? ' (غیرفعال)' : ''}
                    </option>
                  ))}
                </select>
              </label>
              <label>
                <span>نام پروفایل</span>
                <input
                  value={form.name}
                  disabled={saving}
                  onChange={(event) => updateForm({ name: event.target.value })}
                  placeholder="مثلاً لاته سفارشی"
                />
              </label>
              <label>
                <span>ترتیب نمایش</span>
                <input
                  className="ltr-input"
                  type="number"
                  min="0"
                  value={form.displayOrder}
                  disabled={saving}
                  onChange={(event) => updateForm({ displayOrder: event.target.value })}
                />
              </label>
              <label className="checkbox-row">
                <input
                  type="checkbox"
                  checked={form.isEnabled}
                  disabled={saving}
                  onChange={(event) => updateForm({ isEnabled: event.target.checked })}
                />
                <span>این نوشیدنی در منو قابل سفارشی‌سازی باشد</span>
              </label>
            </div>

            <div className="panel-head">
              <div>
                <h4>گروه‌ها و گزینه‌ها</h4>
                <p>
                  گروه اسلایدری برای مقادیر پیوسته (مثل گرم شکلات) و گروه انتخابی برای
                  گزینه‌های مشخص (مثل نوع شیر) است.
                </p>
              </div>
              <div className="builder-add-buttons">
                <button
                  type="button"
                  className="ghost-button"
                  disabled={saving}
                  onClick={() => addGroup('Slider')}
                >
                  + گروه اسلایدری
                </button>
                <button
                  type="button"
                  className="ghost-button"
                  disabled={saving}
                  onClick={() => addGroup('Choice')}
                >
                  + گروه انتخابی
                </button>
              </div>
            </div>

            {form.groups.length ? (
              form.groups.map((group, groupIndex) => {
                const isChoice = group.inputType === 'Choice'

                return (
                  <section key={`group-${groupIndex}`} className="builder-group">
                    <div className="builder-group-head">
                      <strong>گروه {groupIndex + 1}</strong>
                      <button
                        type="button"
                        className="link-button link-danger"
                        disabled={saving}
                        onClick={() => removeGroup(groupIndex)}
                      >
                        حذف گروه
                      </button>
                    </div>

                    <div className="field-grid">
                      <label>
                        <span>نام گروه</span>
                        <input
                          value={group.name}
                          disabled={saving}
                          onChange={(event) =>
                            updateGroup(groupIndex, { name: event.target.value })
                          }
                          placeholder="مثلاً شیر یا افزودنی‌ها"
                        />
                      </label>
                      <label>
                        <span>نوع ورودی</span>
                        <select
                          value={group.inputType}
                          disabled={saving}
                          onChange={(event) =>
                            changeGroupType(groupIndex, event.target.value)
                          }
                        >
                          <option value="Slider">اسلایدری (مقدار پیوسته)</option>
                          <option value="Choice">انتخابی (چند گزینه)</option>
                        </select>
                      </label>
                      <label>
                        <span>ترتیب نمایش</span>
                        <input
                          className="ltr-input"
                          type="number"
                          min="0"
                          value={group.displayOrder}
                          disabled={saving}
                          onChange={(event) =>
                            updateGroup(groupIndex, { displayOrder: event.target.value })
                          }
                        />
                      </label>
                    </div>

                    {isChoice ? (
                      <div className="field-grid">
                        <label>
                          <span>حالت انتخاب</span>
                          <select
                            value={group.selectionMode}
                            disabled={saving}
                            onChange={(event) =>
                              updateGroup(groupIndex, {
                                selectionMode: event.target.value,
                                maxSelections:
                                  event.target.value === 'Single'
                                    ? 1
                                    : Math.max(1, group.maxSelections),
                              })
                            }
                          >
                            <option value="Single">تک‌انتخابی</option>
                            <option value="Multiple">چندانتخابی</option>
                          </select>
                        </label>
                        <label>
                          <span>حداقل انتخاب</span>
                          <input
                            className="ltr-input"
                            type="number"
                            min="0"
                            value={group.minSelections}
                            disabled={saving}
                            onChange={(event) =>
                              updateGroup(groupIndex, {
                                minSelections: event.target.value,
                              })
                            }
                          />
                        </label>
                        <label>
                          <span>حداکثر انتخاب</span>
                          <input
                            className="ltr-input"
                            type="number"
                            min="1"
                            value={group.maxSelections}
                            disabled={saving || group.selectionMode === 'Single'}
                            onChange={(event) =>
                              updateGroup(groupIndex, {
                                maxSelections: event.target.value,
                              })
                            }
                          />
                        </label>
                        <label className="checkbox-row">
                          <input
                            type="checkbox"
                            checked={group.isRequired}
                            disabled={saving}
                            onChange={(event) =>
                              updateGroup(groupIndex, {
                                isRequired: event.target.checked,
                                minSelections: event.target.checked
                                  ? Math.max(1, Number(group.minSelections) || 0)
                                  : group.minSelections,
                              })
                            }
                          />
                          <span>انتخاب از این گروه الزامی است</span>
                        </label>
                      </div>
                    ) : (
                      <p className="builder-hint">
                        هر گزینه در این گروه یک اسلایدر مستقل است. مقدار صفر یعنی آن جزء
                        انتخاب نشده و قیمتی اضافه نمی‌شود.
                      </p>
                    )}

                    <div className="builder-options">
                      {group.options.map((option, optionIndex) => (
                        <div key={`option-${optionIndex}`} className="builder-option">
                          <div className="builder-option-head">
                            <strong>گزینه {optionIndex + 1}</strong>
                            <button
                              type="button"
                              className="link-button link-danger"
                              disabled={saving}
                              onClick={() => removeOption(groupIndex, optionIndex)}
                            >
                              حذف
                            </button>
                          </div>

                          <div className="builder-option-grid">
                            <label>
                              <span>نام گزینه</span>
                              <input
                                value={option.name}
                                disabled={saving}
                                onChange={(event) =>
                                  updateOption(groupIndex, optionIndex, {
                                    name: event.target.value,
                                  })
                                }
                                placeholder="مثلاً شکلات تلخ"
                              />
                            </label>
                            <label>
                              <span>نام انگلیسی</span>
                              <input
                                className="ltr-input"
                                value={option.englishName}
                                disabled={saving}
                                onChange={(event) =>
                                  updateOption(groupIndex, optionIndex, {
                                    englishName: event.target.value,
                                  })
                                }
                              />
                            </label>
                            <label>
                              <span>واحد</span>
                              <select
                                value={option.unit}
                                disabled={saving}
                                onChange={(event) =>
                                  updateOption(groupIndex, optionIndex, {
                                    unit: event.target.value,
                                  })
                                }
                              >
                                {UNITS.map((unit) => (
                                  <option key={unit.value} value={unit.value}>
                                    {unit.label}
                                  </option>
                                ))}
                              </select>
                            </label>
                            <label>
                              <span>قیمت هر واحد (تومان)</span>
                              <input
                                className="ltr-input"
                                type="number"
                                min="0"
                                step="0.01"
                                value={option.pricePerUnit}
                                disabled={saving}
                                onChange={(event) =>
                                  updateOption(groupIndex, optionIndex, {
                                    pricePerUnit: event.target.value,
                                  })
                                }
                              />
                            </label>
                          </div>

                          {isChoice ? (
                            <div className="builder-option-grid">
                              <label className="checkbox-row">
                                <input
                                  type="checkbox"
                                  checked={option.isDefault}
                                  disabled={saving}
                                  onChange={(event) =>
                                    updateOption(groupIndex, optionIndex, {
                                      isDefault: event.target.checked,
                                      defaultAmount: event.target.checked ? 1 : 0,
                                    })
                                  }
                                />
                                <span>پیش‌فرض انتخاب شود</span>
                              </label>
                              <label className="checkbox-row">
                                <input
                                  type="checkbox"
                                  checked={option.isActive}
                                  disabled={saving}
                                  onChange={(event) =>
                                    updateOption(groupIndex, optionIndex, {
                                      isActive: event.target.checked,
                                    })
                                  }
                                />
                                <span>موجود است</span>
                              </label>
                              <label>
                                <span>ترتیب نمایش</span>
                                <input
                                  className="ltr-input"
                                  type="number"
                                  min="0"
                                  value={option.displayOrder}
                                  disabled={saving}
                                  onChange={(event) =>
                                    updateOption(groupIndex, optionIndex, {
                                      displayOrder: event.target.value,
                                    })
                                  }
                                />
                              </label>
                            </div>
                          ) : (
                            <div className="builder-option-grid">
                              <label>
                                <span>حداقل مقدار</span>
                                <input
                                  className="ltr-input"
                                  type="number"
                                  min="0"
                                  step="0.001"
                                  value={option.minAmount}
                                  disabled={saving}
                                  onChange={(event) =>
                                    updateOption(groupIndex, optionIndex, {
                                      minAmount: event.target.value,
                                    })
                                  }
                                />
                              </label>
                              <label>
                                <span>حداکثر مقدار</span>
                                <input
                                  className="ltr-input"
                                  type="number"
                                  min="0"
                                  step="0.001"
                                  value={option.maxAmount}
                                  disabled={saving}
                                  onChange={(event) =>
                                    updateOption(groupIndex, optionIndex, {
                                      maxAmount: event.target.value,
                                    })
                                  }
                                />
                              </label>
                              <label>
                                <span>گام تغییر</span>
                                <input
                                  className="ltr-input"
                                  type="number"
                                  min="0.001"
                                  step="0.001"
                                  value={option.step}
                                  disabled={saving}
                                  onChange={(event) =>
                                    updateOption(groupIndex, optionIndex, {
                                      step: event.target.value,
                                    })
                                  }
                                />
                              </label>
                              <label>
                                <span>مقدار پیش‌فرض (۰ = بدون انتخاب)</span>
                                <input
                                  className="ltr-input"
                                  type="number"
                                  min="0"
                                  step="0.001"
                                  value={option.defaultAmount}
                                  disabled={saving}
                                  onChange={(event) =>
                                    updateOption(groupIndex, optionIndex, {
                                      defaultAmount: event.target.value,
                                    })
                                  }
                                />
                              </label>
                              <label>
                                <span>ترتیب نمایش</span>
                                <input
                                  className="ltr-input"
                                  type="number"
                                  min="0"
                                  value={option.displayOrder}
                                  disabled={saving}
                                  onChange={(event) =>
                                    updateOption(groupIndex, optionIndex, {
                                      displayOrder: event.target.value,
                                    })
                                  }
                                />
                              </label>
                              <label className="checkbox-row">
                                <input
                                  type="checkbox"
                                  checked={option.isActive}
                                  disabled={saving}
                                  onChange={(event) =>
                                    updateOption(groupIndex, optionIndex, {
                                      isActive: event.target.checked,
                                    })
                                  }
                                />
                                <span>موجود است</span>
                              </label>
                            </div>
                          )}
                        </div>
                      ))}
                    </div>

                    <button
                      type="button"
                      className="ghost-button"
                      disabled={saving}
                      onClick={() => addOption(groupIndex)}
                    >
                      + افزودن گزینه
                    </button>
                  </section>
                )
              })
            ) : (
              <p className="builder-hint">
                هنوز گروهی اضافه نشده است. با دکمه‌های بالا یک گروه اسلایدری یا انتخابی
                بسازید.
              </p>
            )}

            <button type="submit" disabled={saving || (!canCreate && !form.id) || (!canUpdate && form.id)}>
              {saving ? 'در حال ذخیره...' : 'ذخیره پروفایل نوشیدنی'}
            </button>
          </form>
        ) : (
          <section className="panel">
            <div className="panel-head">
              <div>
                <h3>پروفایل جدید</h3>
                <p>
                  برای شروع، یک آیتم منو انتخاب کنید و گروه‌های سازندهٔ نوشیدنی را تعریف
                  کنید.
                </p>
              </div>
            </div>
            {canCreate ? (
              <button type="button" onClick={startNewProfile}>
                ساخت پروفایل نوشیدنی
              </button>
            ) : (
              <p className="builder-hint">شما اجازهٔ ساخت پروفایل جدید را ندارید.</p>
            )}
          </section>
        )}
      </div>
    </section>
  )
}
