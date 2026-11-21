import { useCallback, useEffect, useMemo, useState } from 'react'
import { Link } from 'react-router-dom'

let uidCounter = 0
const nextUid = () => `opt-${Date.now()}-${uidCounter++}`

const emptyGroup = () => ({
  uid: nextUid(),
  id: null,
  sourceTemplateId: null,
  persianName: '',
  englishName: '',
  isRequired: false,
  selectionMode: 'Single',
  minSelections: 0,
  maxSelections: 1,
  displayOrder: 0,
  isActive: true,
  options: [],
})

const emptyOption = (displayOrder) => ({
  uid: nextUid(),
  persianName: '',
  englishName: '',
  priceDelta: 0,
  isDefault: false,
  isActive: true,
  displayOrder,
  ingredients: [],
})

/** Normalise what the server returns into the editable editor document. */
function toEditorGroups(groups) {
  return (groups ?? []).map((group) => ({
    uid: nextUid(),
    id: group.id ?? null,
    sourceTemplateId: group.sourceTemplateId ?? null,
    persianName: group.persianName ?? '',
    englishName: group.englishName ?? '',
    isRequired: group.isRequired,
    selectionMode: group.selectionMode ?? 'Single',
    minSelections: group.minSelections ?? 0,
    maxSelections: group.maxSelections ?? 1,
    displayOrder: group.displayOrder ?? 0,
    isActive: group.isActive ?? true,
    options: (group.options ?? []).map((option) => ({
      uid: nextUid(),
      persianName: option.persianName ?? '',
      englishName: option.englishName ?? '',
      priceDelta: option.priceDelta ?? 0,
      isDefault: option.isDefault ?? false,
      isActive: option.isActive ?? true,
      displayOrder: option.displayOrder ?? 0,
      ingredients: (option.ingredients ?? []).map((row) => ({
        uid: nextUid(),
        ingredientId: row.ingredientId ?? '',
        quantity: row.quantity ?? 0,
      })),
    })),
  }))
}

function templateToEditorGroup(template, displayOrder) {
  return {
    uid: nextUid(),
    id: null,
    sourceTemplateId: template.id,
    persianName: template.persianName ?? '',
    englishName: template.englishName ?? '',
    isRequired: Boolean(template.isRequired),
    selectionMode: template.selectionMode ?? 'Single',
    minSelections: template.minSelections ?? 0,
    maxSelections: template.maxSelections ?? 1,
    displayOrder,
    isActive: template.isActive ?? true,
    options: (template.options ?? []).map((option, idx) => ({
      uid: nextUid(),
      persianName: option.persianName ?? '',
      englishName: option.englishName ?? '',
      priceDelta: option.priceDelta ?? 0,
      isDefault: Boolean(option.isDefault),
      isActive: option.isActive ?? true,
      displayOrder: option.displayOrder ?? idx,
      ingredients: (option.ingredients ?? []).map((row) => ({
        uid: nextUid(),
        ingredientId: row.ingredientId ?? '',
        quantity: row.quantity ?? 0,
      })),
    })),
  }
}

/** Editor document (with uids) → server payload (uids stripped). */
function toServerPayload(groups) {
  return groups.map((group, groupIndex) => ({
    persianName: group.persianName.trim(),
    englishName: group.englishName.trim() || null,
    isRequired: group.isRequired,
    selectionMode: group.selectionMode,
    minSelections: Number(group.minSelections) || 0,
    maxSelections: Number(group.maxSelections) || 1,
    displayOrder: group.displayOrder ?? groupIndex,
    isActive: group.isActive,
    sourceTemplateId: group.sourceTemplateId || null,
    options: group.options.map((option, optionIndex) => ({
      persianName: option.persianName.trim(),
      englishName: option.englishName.trim() || null,
      priceDelta: Number(option.priceDelta) || 0,
      isDefault: option.isDefault,
      isActive: option.isActive,
      displayOrder: option.displayOrder ?? optionIndex,
      ingredients: option.ingredients
        .filter((row) => row.ingredientId)
        .map((row) => ({ ingredientId: row.ingredientId, quantity: Number(row.quantity) || 0 })),
    })),
  }))
}

export function MenuItemOptionGroupsPanel({
  session,
  apiRequest,
  menuItemOptions,
  ingredientOptions,
  getReadableError,
  setMessage,
  setError,
  setSaving,
  toPersianNumber,
  formatMoney,
  focusMenuItemId = '',
}) {
  const [selectedMenuItemId, setSelectedMenuItemId] = useState('')
  const [groups, setGroups] = useState([])
  const [loading, setLoading] = useState(false)
  const [dirty, setDirty] = useState(false)

  // Reusable templates library cache
  const [templates, setTemplates] = useState([])
  const [templateCategories, setTemplateCategories] = useState([])
  const [templatePickerOpen, setTemplatePickerOpen] = useState(false)
  const [templateSearch, setTemplateSearch] = useState('')
  const [templateCategoryFilter, setTemplateCategoryFilter] = useState('ALL')
  const [selectedTemplateIds, setSelectedTemplateIds] = useState([])

  // Import from another menu item modal
  const [importModalOpen, setImportModalOpen] = useState(false)
  const [importSourceItemId, setImportSourceItemId] = useState('')
  const [importSourceGroups, setImportSourceGroups] = useState([])
  const [importLoading, setImportLoading] = useState(false)
  const [importSelectedGroupIds, setImportSelectedGroupIds] = useState([])

  // Copy a group (or all groups) to other menu items modal
  const [copyModalGroup, setCopyModalGroup] = useState(null) // null = closed; { all: true } or group object
  const [copyTargetIds, setCopyTargetIds] = useState([])
  const [copySearch, setCopySearch] = useState('')
  const [copyReplaceExisting, setCopyReplaceExisting] = useState(false)
  const [copyBusy, setCopyBusy] = useState(false)

  // Save a group as reusable template modal
  const [saveTemplateGroup, setSaveTemplateGroup] = useState(null)
  const [saveTemplateCategory, setSaveTemplateCategory] = useState('')
  const [saveTemplateBusy, setSaveTemplateBusy] = useState(false)

  const loadTemplates = useCallback(async () => {
    if (!session?.token) return
    try {
      const data = await apiRequest('/admin/option-templates', { token: session.token })
      setTemplates(Array.isArray(data?.templates) ? data.templates : [])
      setTemplateCategories(Array.isArray(data?.categories) ? data.categories : [])
    } catch {
      // Templates library is additive — fail silently if unavailable.
    }
  }, [apiRequest, session?.token])

  useEffect(() => {
    loadTemplates()
  }, [loadTemplates])

  useEffect(() => {
    if (focusMenuItemId && !dirty) {
      setSelectedMenuItemId(focusMenuItemId)
    }
  }, [focusMenuItemId, dirty])

  const loadGroups = useCallback(
    async (menuItemId) => {
      if (!menuItemId) {
        setGroups([])
        return
      }

      setLoading(true)
      try {
        const data = await apiRequest(`/admin/menu-items/${menuItemId}/option-groups`, {
          token: session.token,
        })
        setGroups(toEditorGroups(data.groups))
        setDirty(false)
      } catch (requestError) {
        setError(getReadableError(requestError))
      } finally {
        setLoading(false)
      }
    },
    [apiRequest, session.token, getReadableError, setError],
  )

  useEffect(() => {
    if (selectedMenuItemId) {
      loadGroups(selectedMenuItemId)
    } else {
      setGroups([])
      setDirty(false)
    }
  }, [selectedMenuItemId, loadGroups])

  const markDirty = () => setDirty(true)

  const updateGroup = (groupUid, patch) => {
    markDirty()
    setGroups((current) =>
      current.map((group) => (group.uid === groupUid ? { ...group, ...patch } : group)),
    )
  }

  const addGroup = () => {
    markDirty()
    setGroups((current) => [
      ...current,
      { ...emptyGroup(), displayOrder: current.length },
    ])
  }

  const removeGroup = (groupUid) => {
    markDirty()
    setGroups((current) => current.filter((group) => group.uid !== groupUid))
  }

  const moveGroup = (groupUid, direction) => {
    markDirty()
    setGroups((current) => {
      const index = current.findIndex((group) => group.uid === groupUid)
      const target = index + direction
      if (index === -1 || target < 0 || target >= current.length) {
        return current
      }
      const next = [...current]
      const [moved] = next.splice(index, 1)
      next.splice(target, 0, moved)
      return next.map((group, position) => ({ ...group, displayOrder: position }))
    })
  }

  const updateOption = (groupUid, optionUid, patch) => {
    markDirty()
    setGroups((current) =>
      current.map((group) =>
        group.uid === groupUid
          ? {
              ...group,
              options: group.options.map((option) =>
                option.uid === optionUid ? { ...option, ...patch } : option,
              ),
            }
          : group,
      ),
    )
  }

  const addOption = (groupUid) => {
    markDirty()
    setGroups((current) =>
      current.map((group) =>
        group.uid === groupUid
          ? {
              ...group,
              options: [
                ...group.options,
                emptyOption(group.options.length),
              ],
            }
          : group,
      ),
    )
  }

  const removeOption = (groupUid, optionUid) => {
    markDirty()
    setGroups((current) =>
      current.map((group) =>
        group.uid === groupUid
          ? {
              ...group,
              options: group.options
                .filter((option) => option.uid !== optionUid)
                .map((option, index) => ({ ...option, displayOrder: index })),
            }
          : group,
      ),
    )
  }

  const updateIngredientRow = (groupUid, optionUid, rowUid, patch) => {
    markDirty()
    setGroups((current) =>
      current.map((group) =>
        group.uid === groupUid
          ? {
              ...group,
              options: group.options.map((option) =>
                option.uid === optionUid
                  ? {
                      ...option,
                      ingredients: option.ingredients.map((row) =>
                        row.uid === rowUid ? { ...row, ...patch } : row,
                      ),
                    }
                  : option,
              ),
            }
          : group,
      ),
    )
  }

  const addIngredientRow = (groupUid, optionUid) => {
    markDirty()
    setGroups((current) =>
      current.map((group) =>
        group.uid === groupUid
          ? {
              ...group,
              options: group.options.map((option) =>
                option.uid === optionUid
                  ? {
                      ...option,
                      ingredients: [
                        ...option.ingredients,
                        { uid: nextUid(), ingredientId: '', quantity: 0 },
                      ],
                    }
                  : option,
              ),
            }
          : group,
      ),
    )
  }

  const removeIngredientRow = (groupUid, optionUid, rowUid) => {
    markDirty()
    setGroups((current) =>
      current.map((group) =>
        group.uid === groupUid
          ? {
              ...group,
              options: group.options.map((option) =>
                option.uid === optionUid
                  ? {
                      ...option,
                      ingredients: option.ingredients.filter((row) => row.uid !== rowUid),
                    }
                  : option,
              ),
            }
          : group,
      ),
    )
  }

  const validateGroupsClientSide = (targetGroups) => {
    for (const group of targetGroups) {
      if (!group.persianName.trim()) {
        return 'نام گروه آپشن الزامی است.'
      }
      if (group.options.length === 0) {
        return `گروه «${group.persianName}» باید حداقل یک گزینه داشته باشد.`
      }
      if (group.isRequired && group.options.every((option) => !option.isActive)) {
        return `گروه اجباری «${group.persianName}» باید حداقل یک گزینهٔ فعال داشته باشد.`
      }
      for (const option of group.options) {
        if (!option.persianName.trim()) {
          return `نام گزینه در گروه «${group.persianName}» الزامی است.`
        }
        if (Number(option.priceDelta) < 0) {
          return `مبلغ افزودهٔ گزینهٔ «${option.persianName}» نمی‌تواند منفی باشد.`
        }
        for (const row of option.ingredients) {
          if (!row.ingredientId || Number(row.quantity) <= 0) {
            return `مقدار مصرف مواد اولیهٔ گزینهٔ «${option.persianName}» باید کامل و بزرگ‌تر از صفر باشد.`
          }
        }
      }
    }
    return null
  }

  const handleSave = async () => {
    if (!selectedMenuItemId) return

    const validationError = validateGroupsClientSide(groups)
    if (validationError) {
      setError(validationError)
      return false
    }

    setSaving(true)
    try {
      await apiRequest(`/admin/menu-items/${selectedMenuItemId}/option-groups`, {
        method: 'PUT',
        token: session.token,
        body: { menuItemId: selectedMenuItemId, groups: toServerPayload(groups) },
      })
      setMessage('گروه‌های آپشن این آیتم ذخیره شد.')
      await loadGroups(selectedMenuItemId)
      await loadTemplates()
      return true
    } catch (requestError) {
      setError(getReadableError(requestError))
      return false
    } finally {
      setSaving(false)
    }
  }

  // ── Template Picker handlers ──
  const openTemplatePicker = () => {
    loadTemplates()
    setSelectedTemplateIds([])
    setTemplateSearch('')
    setTemplateCategoryFilter('ALL')
    setTemplatePickerOpen(true)
  }

  const toggleTemplateSelection = (templateId) => {
    setSelectedTemplateIds((current) =>
      current.includes(templateId)
        ? current.filter((id) => id !== templateId)
        : [...current, templateId],
    )
  }

  const filteredTemplates = useMemo(() => {
    const q = templateSearch.trim().toLowerCase()
    return templates.filter((template) => {
      if (
        templateCategoryFilter !== 'ALL' &&
        (template.categoryLabel ?? '') !== templateCategoryFilter
      ) {
        return false
      }
      if (!q) return true
      return (
        (template.persianName ?? '').toLowerCase().includes(q) ||
        (template.englishName ?? '').toLowerCase().includes(q) ||
        (template.categoryLabel ?? '').toLowerCase().includes(q)
      )
    })
  }, [templates, templateCategoryFilter, templateSearch])

  const appendSelectedTemplates = () => {
    if (selectedTemplateIds.length === 0) return
    const chosen = templates.filter((t) => selectedTemplateIds.includes(t.id))
    markDirty()
    setGroups((current) => [
      ...current,
      ...chosen.map((tpl, idx) => templateToEditorGroup(tpl, current.length + idx)),
    ])
    setTemplatePickerOpen(false)
    setMessage(
      `${toPersianNumber(chosen.length)} قالب به لیست اضافه شد. در صورت نیاز آن را ویرایش کرده و روی «ذخیره گروه‌های آپشن» کلیک کنید.`,
    )
  }

  // ── Import from another menu item handlers ──
  const openImportModal = () => {
    setImportSourceItemId('')
    setImportSourceGroups([])
    setImportSelectedGroupIds([])
    setImportModalOpen(true)
  }

  const handleImportSourceChange = async (sourceItemId) => {
    setImportSourceItemId(sourceItemId)
    setImportSourceGroups([])
    setImportSelectedGroupIds([])
    if (!sourceItemId) return
    setImportLoading(true)
    try {
      const data = await apiRequest(`/admin/menu-items/${sourceItemId}/option-groups`, {
        token: session.token,
      })
      const fetched = data?.groups ?? []
      setImportSourceGroups(fetched)
      setImportSelectedGroupIds(fetched.map((g) => g.id).filter(Boolean))
    } catch (requestError) {
      setError(getReadableError(requestError))
    } finally {
      setImportLoading(false)
    }
  }

  const confirmImportFromItem = () => {
    const picked = importSourceGroups.filter((g) => importSelectedGroupIds.includes(g.id))
    if (picked.length === 0) return
    markDirty()
    setGroups((current) => [
      ...current,
      ...toEditorGroups(picked).map((g, idx) => ({
        ...g,
        id: null,
        displayOrder: current.length + idx,
      })),
    ])
    setImportModalOpen(false)
    setMessage(
      `${toPersianNumber(picked.length)} گروه آپشن کپی شد. برای ثبت نهایی، «ذخیره گروه‌های آپشن» را بزنید.`,
    )
  }

  // ── Copy group(s) to other menu items handlers ──
  const openCopyToItemsModal = (groupOrAll) => {
    if (dirty || (groupOrAll && !groupOrAll.all && !groupOrAll.id)) {
      setError('ابتدا تغییرات فعلی این آیتم را ذخیره کنید («ذخیره گروه‌های آپشن») و سپس به آیتم‌های دیگر کپی کنید.')
      return
    }
    setCopyModalGroup(groupOrAll)
    setCopyTargetIds([])
    setCopySearch('')
    setCopyReplaceExisting(false)
  }

  const otherMenuItems = useMemo(() => {
    const q = copySearch.trim().toLowerCase()
    return (menuItemOptions ?? []).filter((item) => {
      if (item.id === selectedMenuItemId) return false
      if (!q) return true
      return (
        (item.persianName ?? '').toLowerCase().includes(q) ||
        (item.englishName ?? '').toLowerCase().includes(q)
      )
    })
  }, [menuItemOptions, selectedMenuItemId, copySearch])

  const confirmCopyToItems = async () => {
    if (!selectedMenuItemId || !copyModalGroup || copyTargetIds.length === 0) return
    setCopyBusy(true)
    setError('')
    setMessage('')
    try {
      const payload = {
        sourceMenuItemId: selectedMenuItemId,
        targetMenuItemIds: copyTargetIds,
        groupIds: copyModalGroup.all ? null : [copyModalGroup.id],
        replaceExisting: copyReplaceExisting,
      }
      const result = await apiRequest('/admin/menu-items/option-groups/copy', {
        method: 'POST',
        token: session.token,
        body: payload,
      })
      const parts = [
        `${toPersianNumber(result.copiedGroupCount)} گروه به ${toPersianNumber(result.updatedMenuItemCount)} آیتم کپی شد`,
      ]
      if (result.skippedGroupCount > 0) {
        parts.push(`${toPersianNumber(result.skippedGroupCount)} مورد تکراری رد شد`)
      }
      setMessage(parts.join(' (') + (result.skippedGroupCount > 0 ? ').' : '.'))
      setCopyModalGroup(null)
    } catch (requestError) {
      setError(getReadableError(requestError))
    } finally {
      setCopyBusy(false)
    }
  }

  // ── Save existing group as reusable template ──
  const openSaveTemplateModal = (group) => {
    const err = validateGroupsClientSide([group])
    if (err) {
      setError(err)
      return
    }
    setSaveTemplateGroup(group)
    setSaveTemplateCategory('')
  }

  const confirmSaveAsTemplate = async () => {
    if (!saveTemplateGroup) return
    setSaveTemplateBusy(true)
    setError('')
    setMessage('')
    try {
      const createdId = await apiRequest('/admin/option-templates', {
        method: 'POST',
        token: session.token,
        body: {
          id: null,
          categoryLabel: saveTemplateCategory.trim() || null,
          persianName: saveTemplateGroup.persianName.trim(),
          englishName: saveTemplateGroup.englishName.trim() || null,
          isRequired: saveTemplateGroup.isRequired,
          selectionMode: saveTemplateGroup.selectionMode,
          minSelections: Number(saveTemplateGroup.minSelections) || 0,
          maxSelections: Number(saveTemplateGroup.maxSelections) || 1,
          displayOrder: templates.length,
          isActive: saveTemplateGroup.isActive,
          options: saveTemplateGroup.options.map((option, idx) => ({
            persianName: option.persianName.trim(),
            englishName: option.englishName.trim() || null,
            priceDelta: Number(option.priceDelta) || 0,
            isDefault: option.isDefault,
            isActive: option.isActive,
            displayOrder: idx,
            ingredients: option.ingredients
              .filter((row) => row.ingredientId)
              .map((row) => ({ ingredientId: row.ingredientId, quantity: Number(row.quantity) || 0 })),
          })),
        },
      })

      if (createdId) {
        updateGroup(saveTemplateGroup.uid, { sourceTemplateId: createdId })
      }
      setSaveTemplateGroup(null)
      await loadTemplates()
      setMessage(`گروه «${saveTemplateGroup.persianName}» در کتابخانهٔ قالب‌های آپشن ذخیره شد.`)
    } catch (requestError) {
      setError(getReadableError(requestError))
    } finally {
      setSaveTemplateBusy(false)
    }
  }

  const templateNameById = useMemo(
    () => Object.fromEntries(templates.map((t) => [t.id, t.persianName])),
    [templates],
  )

  const optionHasIngredients = (option) =>
    option.ingredients.some((row) => row.ingredientId)

  return (
    <section className="panel option-groups-panel full-width">
      <div className="panel-head">
        <div>
          <h3>گروه‌های آپشن آیتم منو</h3>
          <p>
            برای هر آیتم می‌توانید از <strong>قالب‌های آماده</strong> انتخاب کنید، از آیتم‌های دیگر کپی
            بگیرید یا گروه اختصاصی بسازید. هر گزینه روی قیمت نهایی اضافه شده و مواد اولیهٔ آن از انبار کسر می‌شود.
          </p>
        </div>
        <Link className="ghost-button tiny" to="/option-templates" style={{ textDecoration: 'none' }}>
          مدیریت قالب‌های آماده ({toPersianNumber(templates.length)})
        </Link>
      </div>

      <div className="option-groups-toolbar">
        <label>
          <span>آیتم منو</span>
          <select
            value={selectedMenuItemId}
            onChange={(event) => setSelectedMenuItemId(event.target.value)}
          >
            <option value="">-- انتخاب آیتم منو --</option>
            {menuItemOptions.map((item) => (
              <option key={item.id} value={item.id}>
                {item.persianName} — {item.englishName}
              </option>
            ))}
          </select>
        </label>
        <div className="option-groups-actions">
          <button
            type="button"
            className="ghost-button"
            disabled={!selectedMenuItemId}
            onClick={openTemplatePicker}
          >
            + افزودن از قالب آماده
          </button>
          <button
            type="button"
            className="ghost-button"
            disabled={!selectedMenuItemId}
            onClick={openImportModal}
          >
            کپی از آیتم دیگر
          </button>
          {groups.length > 0 ? (
            <button
              type="button"
              className="ghost-button"
              disabled={!selectedMenuItemId || loading}
              onClick={() => openCopyToItemsModal({ all: true })}
            >
              کپی همه به آیتم‌های دیگر
            </button>
          ) : null}
          <button
            type="button"
            className="ghost-button"
            disabled={!selectedMenuItemId}
            onClick={addGroup}
          >
            + افزودن گروه اختصاصی
          </button>
          <button
            type="button"
            className="primary-button"
            disabled={!selectedMenuItemId || loading || !dirty}
            onClick={handleSave}
          >
            ذخیره گروه‌های آپشن
          </button>
        </div>
      </div>

      {selectedMenuItemId && loading && (
        <p className="feedback">در حال دریافت گروه‌های آپشن...</p>
      )}

      {selectedMenuItemId && !loading && (
        <div className="option-groups-list">
          {groups.length === 0 && (
            <p className="feedback">
              برای این آیتم هنوز گروهی تعریف نشده؛ می‌توانید با «افزودن از قالب آماده»، «کپی از آیتم دیگر»
              یا «افزودن گروه اختصاصی» شروع کنید.
            </p>
          )}

          {groups.map((group, groupIndex) => (
            <article
              className={`option-group-card ${group.isRequired ? 'is-required' : ''}`}
              key={group.uid}
            >
              <header className="option-group-head">
                <div className="option-group-index">
                  <span>{toPersianNumber(groupIndex + 1)}</span>
                </div>
                <div className="option-group-title">
                  <input
                    className="option-group-name"
                    placeholder="نام فارسی گروه (مثلاً نوع شیر یا سیروپ‌ها)"
                    value={group.persianName}
                    onChange={(event) =>
                      updateGroup(group.uid, { persianName: event.target.value })
                    }
                  />
                  <input
                    className="ltr-input"
                    placeholder="English name (e.g. Milk Type)"
                    value={group.englishName}
                    onChange={(event) =>
                      updateGroup(group.uid, { englishName: event.target.value })
                    }
                  />
                  {group.sourceTemplateId ? (
                    <div className="option-group-meta-row">
                      <span className="option-group-template-badge">
                        قالب آماده{templateNameById[group.sourceTemplateId] ? `: ${templateNameById[group.sourceTemplateId]}` : ''}
                      </span>
                    </div>
                  ) : null}
                </div>
                <div className="option-group-controls">
                  <button
                    type="button"
                    className="ghost-button tiny"
                    onClick={() => openCopyToItemsModal(group)}
                    title="کپی این گروه به یک یا چند آیتم دیگر منو"
                  >
                    کپی به آیتم دیگر
                  </button>
                  {!group.sourceTemplateId ? (
                    <button
                      type="button"
                      className="ghost-button tiny"
                      onClick={() => openSaveTemplateModal(group)}
                      title="ذخیره این گروه در کتابخانه قالب‌ها برای استفاده مجدد"
                    >
                      ذخیره به عنوان قالب
                    </button>
                  ) : null}
                  <button
                    type="button"
                    className="ghost-button tiny"
                    onClick={() => moveGroup(group.uid, -1)}
                    disabled={groupIndex === 0}
                    aria-label="انتقال به بالا"
                  >
                    ↑
                  </button>
                  <button
                    type="button"
                    className="ghost-button tiny"
                    onClick={() => moveGroup(group.uid, 1)}
                    disabled={groupIndex === groups.length - 1}
                    aria-label="انتقال به پایین"
                  >
                    ↓
                  </button>
                  <button
                    type="button"
                    className="ghost-button tiny danger"
                    onClick={() => removeGroup(group.uid)}
                    aria-label="حذف گروه"
                  >
                    حذف گروه
                  </button>
                </div>
              </header>

              <div className="option-group-settings">
                <label className="checkbox-row">
                  <input
                    type="checkbox"
                    checked={group.isRequired}
                    onChange={(event) =>
                      updateGroup(group.uid, { isRequired: event.target.checked })
                    }
                  />
                  <span>
                    اجباری <em>(مشتری باید از این گروه انتخاب کند)</em>
                  </span>
                </label>
                <label className="checkbox-row">
                  <input
                    type="checkbox"
                    checked={group.isActive}
                    onChange={(event) => updateGroup(group.uid, { isActive: event.target.checked })}
                  />
                  <span>فعال</span>
                </label>
                <label>
                  <span>حالت انتخاب</span>
                  <select
                    value={group.selectionMode}
                    onChange={(event) =>
                      updateGroup(group.uid, { selectionMode: event.target.value })
                    }
                  >
                    <option value="Single">تک‌انتخابی (رادیویی)</option>
                    <option value="Multi">چندانتخابی</option>
                  </select>
                </label>
                <label>
                  <span>حداقل انتخاب</span>
                  <input
                    className="ltr-input"
                    type="number"
                    min="0"
                    value={group.minSelections}
                    onChange={(event) =>
                      updateGroup(group.uid, { minSelections: event.target.value })
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
                    onChange={(event) =>
                      updateGroup(group.uid, { maxSelections: event.target.value })
                    }
                  />
                </label>
              </div>

              <div className="option-rows">
                {group.options.map((option) => (
                  <div className="option-row" key={option.uid}>
                    <div className="option-row-main">
                      <input
                        placeholder="نام گزینه (مثلاً ۱۰۰٪ عربیکا)"
                        value={option.persianName}
                        onChange={(event) =>
                          updateOption(group.uid, option.uid, {
                            persianName: event.target.value,
                          })
                        }
                      />
                      <input
                        className="ltr-input"
                        placeholder="English"
                        value={option.englishName}
                        onChange={(event) =>
                          updateOption(group.uid, option.uid, {
                            englishName: event.target.value,
                          })
                        }
                      />
                      <label className="option-price-input">
                        <span>+ مبلغ (تومان)</span>
                        <input
                          className="ltr-input"
                          type="number"
                          min="0"
                          value={option.priceDelta}
                          onChange={(event) =>
                            updateOption(group.uid, option.uid, {
                              priceDelta: event.target.value,
                            })
                          }
                        />
                      </label>
                      <label className="checkbox-row">
                        <input
                          type="checkbox"
                          checked={option.isDefault}
                          onChange={(event) =>
                            updateOption(group.uid, option.uid, {
                              isDefault: event.target.checked,
                            })
                          }
                        />
                        <span>پیش‌فرض</span>
                      </label>
                      <label className="checkbox-row">
                        <input
                          type="checkbox"
                          checked={option.isActive}
                          onChange={(event) =>
                            updateOption(group.uid, option.uid, {
                              isActive: event.target.checked,
                            })
                          }
                        />
                        <span>فعال</span>
                      </label>
                      <button
                        type="button"
                        className="ghost-button tiny danger"
                        onClick={() => removeOption(group.uid, option.uid)}
                      >
                        حذف
                      </button>
                    </div>

                    <div className="option-ingredients">
                      <div className="option-ingredients-head">
                        <button
                          type="button"
                          className="ghost-button tiny"
                          onClick={() => addIngredientRow(group.uid, option.uid)}
                        >
                          + ماده اولیهٔ مصرفی
                        </button>
                        {option.ingredients.length === 0 && (
                          <span className="option-ingredients-hint">
                            {optionHasIngredients(option)
                              ? ''
                              : 'بدون تعریف، هیچ ماده اولیه‌ای از انبار کسر نمی‌شود.'}
                          </span>
                        )}
                      </div>
                      {option.ingredients.map((row) => {
                        const ingredient = ingredientOptions.find(
                          (candidate) => candidate.id === row.ingredientId,
                        )
                        return (
                          <div className="option-ingredient-row" key={row.uid}>
                            <select
                              value={row.ingredientId}
                              onChange={(event) =>
                                updateIngredientRow(group.uid, option.uid, row.uid, {
                                  ingredientId: event.target.value,
                                })
                              }
                            >
                              <option value="">-- ماده اولیه --</option>
                              {ingredientOptions.map((candidate) => (
                                <option key={candidate.id} value={candidate.id}>
                                  {candidate.persianName}
                                  {candidate.unit ? ` (${candidate.unit})` : ''}
                                </option>
                              ))}
                            </select>
                            <input
                              className="ltr-input"
                              type="number"
                              min="0"
                              step="0.001"
                              placeholder="مقدار"
                              value={row.quantity}
                              onChange={(event) =>
                                updateIngredientRow(group.uid, option.uid, row.uid, {
                                  quantity: event.target.value,
                                })
                              }
                            />
                            <span className="option-ingredient-unit">
                              {ingredient?.unit
                                ? `${option.persianName || 'گزینه'} — هر انتخاب: ${ingredient.unit}`
                                : ''}
                            </span>
                            <button
                              type="button"
                              className="ghost-button tiny danger"
                              onClick={() =>
                                removeIngredientRow(group.uid, option.uid, row.uid)
                              }
                              aria-label="حذف سطر"
                            >
                              ✕
                            </button>
                          </div>
                        )
                      })}
                    </div>
                  </div>
                ))}
              </div>

              <button
                type="button"
                className="ghost-button tiny add-option-button"
                onClick={() => addOption(group.uid)}
              >
                + افزودن گزینه
              </button>
            </article>
          ))}
        </div>
      )}

      {!selectedMenuItemId && (
        <p className="feedback">اول یک آیتم منو را از لیست بالا انتخاب کنید.</p>
      )}

      {/* ── Modal 1: Add from Reusable Option Templates Library ── */}
      {templatePickerOpen ? (
        <div
          className="option-picker-backdrop"
          role="dialog"
          aria-modal="true"
          onClick={(event) => {
            if (event.target === event.currentTarget) setTemplatePickerOpen(false)
          }}
        >
          <div className="option-picker-modal">
            <div className="option-picker-head">
              <h4>افزودن از قالب‌های آماده آپشن</h4>
              <button
                type="button"
                className="ghost-button tiny"
                onClick={() => setTemplatePickerOpen(false)}
              >
                بستن
              </button>
            </div>
            <div className="option-picker-body">
              <input
                type="search"
                placeholder="جستجو در قالب‌ها..."
                value={templateSearch}
                onChange={(event) => setTemplateSearch(event.target.value)}
              />
              {templateCategories.length > 0 ? (
                <div className="option-template-pills">
                  <button
                    type="button"
                    className={`option-template-pill ${templateCategoryFilter === 'ALL' ? 'is-active' : ''}`}
                    onClick={() => setTemplateCategoryFilter('ALL')}
                  >
                    همه
                  </button>
                  {templateCategories.map((cat) => (
                    <button
                      key={cat}
                      type="button"
                      className={`option-template-pill ${templateCategoryFilter === cat ? 'is-active' : ''}`}
                      onClick={() => setTemplateCategoryFilter(cat)}
                    >
                      {cat}
                    </button>
                  ))}
                </div>
              ) : null}

              {filteredTemplates.length === 0 ? (
                <p className="feedback">
                  قالبی پیدا نشد. می‌توانید از بخش{' '}
                  <Link to="/option-templates">قالب‌های آپشن</Link> قالب‌های آماده تعریف کنید.
                </p>
              ) : (
                <div className="option-picker-list">
                  {filteredTemplates.map((template) => {
                    const selected = selectedTemplateIds.includes(template.id)
                    return (
                      <label
                        key={template.id}
                        className={`option-picker-item ${selected ? 'is-selected' : ''}`}
                      >
                        <span className="option-picker-item-main">
                          <input
                            type="checkbox"
                            checked={selected}
                            onChange={() => toggleTemplateSelection(template.id)}
                          />
                          <span>
                            <strong>{template.persianName}</strong>
                            {template.categoryLabel ? (
                              <span className="table-subtext"> ({template.categoryLabel})</span>
                            ) : null}
                            <div className="table-subtext">
                              {(template.options ?? [])
                                .slice(0, 4)
                                .map((o) =>
                                  Number(o.priceDelta) > 0
                                    ? `${o.persianName} (+${formatMoney(o.priceDelta)})`
                                    : o.persianName,
                                )
                                .join('، ')}
                              {(template.options?.length ?? 0) > 4 ? ' …' : ''}
                            </div>
                          </span>
                        </span>
                        <span className="option-template-chip">
                          {toPersianNumber(template.options?.length ?? 0)} گزینه
                        </span>
                      </label>
                    )
                  })}
                </div>
              )}
            </div>
            <div className="option-picker-foot">
              <Link to="/option-templates" className="link-button">
                ساخت / مدیریت قالب‌ها
              </Link>
              <div className="option-groups-actions">
                <button
                  type="button"
                  className="ghost-button"
                  onClick={() => setTemplatePickerOpen(false)}
                >
                  انصراف
                </button>
                <button
                  type="button"
                  className="primary-button"
                  disabled={selectedTemplateIds.length === 0}
                  onClick={appendSelectedTemplates}
                >
                  افزودن ({toPersianNumber(selectedTemplateIds.length)})
                </button>
              </div>
            </div>
          </div>
        </div>
      ) : null}

      {/* ── Modal 2: Import Option Groups from Another Menu Item ── */}
      {importModalOpen ? (
        <div
          className="option-picker-backdrop"
          role="dialog"
          aria-modal="true"
          onClick={(event) => {
            if (event.target === event.currentTarget) setImportModalOpen(false)
          }}
        >
          <div className="option-picker-modal">
            <div className="option-picker-head">
              <h4>کپی گروه‌های آپشن از یک آیتم دیگر</h4>
              <button
                type="button"
                className="ghost-button tiny"
                onClick={() => setImportModalOpen(false)}
              >
                بستن
              </button>
            </div>
            <div className="option-picker-body">
              <label>
                <span>آیتم مبدا را انتخاب کنید</span>
                <select
                  value={importSourceItemId}
                  onChange={(event) => handleImportSourceChange(event.target.value)}
                >
                  <option value="">-- انتخاب آیتم مبدا --</option>
                  {menuItemOptions
                    .filter((item) => item.id !== selectedMenuItemId)
                    .map((item) => (
                      <option key={item.id} value={item.id}>
                        {item.persianName} — {item.englishName}
                      </option>
                    ))}
                </select>
              </label>

              {importLoading ? (
                <p className="feedback">در حال دریافت گروه‌های آیتم مبدا...</p>
              ) : importSourceItemId && importSourceGroups.length === 0 ? (
                <p className="feedback">این آیتم هیچ گروه آپشنی ندارد.</p>
              ) : (
                <div className="option-picker-list">
                  {importSourceGroups.map((group) => {
                    const selected = importSelectedGroupIds.includes(group.id)
                    return (
                      <label
                        key={group.id}
                        className={`option-picker-item ${selected ? 'is-selected' : ''}`}
                      >
                        <span className="option-picker-item-main">
                          <input
                            type="checkbox"
                            checked={selected}
                            onChange={() =>
                              setImportSelectedGroupIds((curr) =>
                                curr.includes(group.id)
                                  ? curr.filter((id) => id !== group.id)
                                  : [...curr, group.id],
                              )
                            }
                          />
                          <span>
                            <strong>{group.persianName}</strong>
                            <div className="table-subtext">
                              {(group.options ?? []).map((o) => o.persianName).join('، ')}
                            </div>
                          </span>
                        </span>
                        <span className="option-template-chip">
                          {toPersianNumber(group.options?.length ?? 0)} گزینه
                        </span>
                      </label>
                    )
                  })}
                </div>
              )}
            </div>
            <div className="option-picker-foot">
              <span className="table-subtext">
                {toPersianNumber(importSelectedGroupIds.length)} گروه انتخاب شده
              </span>
              <div className="option-groups-actions">
                <button
                  type="button"
                  className="ghost-button"
                  onClick={() => setImportModalOpen(false)}
                >
                  انصراف
                </button>
                <button
                  type="button"
                  className="primary-button"
                  disabled={importSelectedGroupIds.length === 0}
                  onClick={confirmImportFromItem}
                >
                  افزودن به این آیتم
                </button>
              </div>
            </div>
          </div>
        </div>
      ) : null}

      {/* ── Modal 3: Copy Group(s) to Other Menu Items ── */}
      {copyModalGroup ? (
        <div
          className="option-picker-backdrop"
          role="dialog"
          aria-modal="true"
          onClick={(event) => {
            if (event.target === event.currentTarget) setCopyModalGroup(null)
          }}
        >
          <div className="option-picker-modal">
            <div className="option-picker-head">
              <h4>
                {copyModalGroup.all
                  ? 'کپی همهٔ گروه‌های آپشن به آیتم‌های دیگر'
                  : `کپی گروه «${copyModalGroup.persianName}» به آیتم‌های دیگر`}
              </h4>
              <button
                type="button"
                className="ghost-button tiny"
                onClick={() => setCopyModalGroup(null)}
              >
                بستن
              </button>
            </div>
            <div className="option-picker-body">
              <div style={{ display: 'flex', gap: 8, flexWrap: 'wrap', alignItems: 'center' }}>
                <input
                  type="search"
                  placeholder="جستجو در آیتم‌های مقصد..."
                  value={copySearch}
                  onChange={(event) => setCopySearch(event.target.value)}
                  style={{ flex: 1, minWidth: 180 }}
                />
                <button
                  type="button"
                  className="ghost-button tiny"
                  onClick={() => setCopyTargetIds(otherMenuItems.map((i) => i.id))}
                >
                  انتخاب همه
                </button>
                <button
                  type="button"
                  className="ghost-button tiny"
                  onClick={() => setCopyTargetIds([])}
                >
                  پاک کردن
                </button>
              </div>

              <label className="checkbox-row">
                <input
                  type="checkbox"
                  checked={copyReplaceExisting}
                  onChange={(event) => setCopyReplaceExisting(event.target.checked)}
                />
                <span>جایگزینی کامل گروه‌های قبلی آیتم مقصد (در غیر این صورت فقط به گروه‌های قبلی اضافه می‌شود)</span>
              </label>

              <div className="option-picker-list">
                {otherMenuItems.map((item) => {
                  const selected = copyTargetIds.includes(item.id)
                  return (
                    <label
                      key={item.id}
                      className={`option-picker-item ${selected ? 'is-selected' : ''}`}
                    >
                      <span className="option-picker-item-main">
                        <input
                          type="checkbox"
                          checked={selected}
                          onChange={() =>
                            setCopyTargetIds((curr) =>
                              curr.includes(item.id)
                                ? curr.filter((id) => id !== item.id)
                                : [...curr, item.id],
                            )
                          }
                        />
                        <span>
                          <strong>{item.persianName}</strong>
                          {item.englishName ? (
                            <span className="table-subtext ltr-text"> — {item.englishName}</span>
                          ) : null}
                        </span>
                      </span>
                    </label>
                  )
                })}
              </div>
            </div>
            <div className="option-picker-foot">
              <span className="table-subtext">
                {toPersianNumber(copyTargetIds.length)} آیتم مقصد انتخاب شده
              </span>
              <div className="option-groups-actions">
                <button
                  type="button"
                  className="ghost-button"
                  onClick={() => setCopyModalGroup(null)}
                >
                  انصراف
                </button>
                <button
                  type="button"
                  className="primary-button"
                  disabled={copyBusy || copyTargetIds.length === 0}
                  onClick={confirmCopyToItems}
                >
                  {copyBusy ? 'در حال کپی...' : 'کپی به آیتم‌های انتخاب‌شده'}
                </button>
              </div>
            </div>
          </div>
        </div>
      ) : null}

      {/* ── Modal 4: Save Group as Reusable Template ── */}
      {saveTemplateGroup ? (
        <div
          className="option-picker-backdrop"
          role="dialog"
          aria-modal="true"
          onClick={(event) => {
            if (event.target === event.currentTarget) setSaveTemplateGroup(null)
          }}
        >
          <div className="option-picker-modal" style={{ maxWidth: 460 }}>
            <div className="option-picker-head">
              <h4>ذخیره «{saveTemplateGroup.persianName}» به عنوان قالب آماده</h4>
              <button
                type="button"
                className="ghost-button tiny"
                onClick={() => setSaveTemplateGroup(null)}
              >
                بستن
              </button>
            </div>
            <div className="option-picker-body">
              <label>
                <span>دسته‌بندی قالب (اختیاری)</span>
                <input
                  list="save-template-cats-list"
                  placeholder="مثلاً نوشیدنی‌های بر پایه شیر، سیروپ‌ها..."
                  value={saveTemplateCategory}
                  onChange={(event) => setSaveTemplateCategory(event.target.value)}
                />
                <datalist id="save-template-cats-list">
                  {templateCategories.map((cat) => (
                    <option key={cat} value={cat} />
                  ))}
                </datalist>
              </label>
              <p className="table-subtext">
                تمام {toPersianNumber(saveTemplateGroup.options.length)} گزینه و مواد اولیه مصرفی آن‌ها در
                کتابخانهٔ قالب‌ها ذخیره می‌شوند تا در محصولات دیگر نیز با یک کلیک قابل استفاده باشند.
              </p>
            </div>
            <div className="option-picker-foot">
              <span />
              <div className="option-groups-actions">
                <button
                  type="button"
                  className="ghost-button"
                  onClick={() => setSaveTemplateGroup(null)}
                >
                  انصراف
                </button>
                <button
                  type="button"
                  className="primary-button"
                  disabled={saveTemplateBusy}
                  onClick={confirmSaveAsTemplate}
                >
                  {saveTemplateBusy ? 'در حال ذخیره...' : 'ذخیره در کتابخانه قالب‌ها'}
                </button>
              </div>
            </div>
          </div>
        </div>
      ) : null}
    </section>
  )
}
