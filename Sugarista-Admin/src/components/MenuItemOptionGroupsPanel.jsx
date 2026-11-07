import { useCallback, useEffect, useMemo, useState } from 'react'
import { getReadableError } from '../utils/adminApi'

let uidCounter = 0
const nextUid = () => `opt-${Date.now()}-${uidCounter++}`

const emptyGroup = () => ({
  uid: nextUid(),
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
}) {
  const [selectedMenuItemId, setSelectedMenuItemId] = useState('')
  const [groups, setGroups] = useState([])
  const [loading, setLoading] = useState(false)
  const [dirty, setDirty] = useState(false)

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

  const handleSave = async () => {
    if (!selectedMenuItemId) return

    // Client-side sanity checks mirror the server validation rules so admins
    // get instant feedback without a wasted round trip.
    for (const group of groups) {
      if (!group.persianName.trim()) {
        setError('نام گروه آپشن الزامی است.')
        return
      }
      if (group.options.length === 0) {
        setError(`گروه «${group.persianName}» باید حداقل یک گزینه داشته باشد.`)
        return
      }
      if (group.isRequired && group.options.every((option) => !option.isActive)) {
        setError(`گروه اجباری «${group.persianName}» باید حداقل یک گزینهٔ فعال داشته باشد.`)
        return
      }
      for (const option of group.options) {
        if (!option.persianName.trim()) {
          setError(`نام گزینه در گروه «${group.persianName}» الزامی است.`)
          return
        }
        if (Number(option.priceDelta) < 0) {
          setError(`مبلغ افزودهٔ گزینهٔ «${option.persianName}» نمی‌تواند منفی باشد.`)
          return
        }
        for (const row of option.ingredients) {
          if (!row.ingredientId || Number(row.quantity) <= 0) {
            setError(
              `مقدار مصرف مواد اولیهٔ گزینهٔ «${option.persianName}» باید کامل و بزرگ‌تر از صفر باشد.`,
            )
            return
          }
        }
      }
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
    } catch (requestError) {
      setError(getReadableError(requestError))
    } finally {
      setSaving(false)
    }
  }

  const optionHasIngredients = (option) =>
    option.ingredients.some((row) => row.ingredientId)

  const totalOptions = useMemo(
    () => groups.reduce((count, group) => count + group.options.length, 0),
    [groups],
  )

  return (
    <section className="panel option-groups-panel full-width">
      <div className="panel-head">
        <div>
          <h3>گروه‌های آپشن آیتم منو</h3>
          <p>
            برای هر آیتم، گروه‌های اجباری و دلخواه را تعریف کنید. هر گزینه روی قیمت نهایی اضافه شده و
            مواد اولیهٔ تعیین‌شدهٔ آن هنگام سفارش از انبار کسر می‌شود.
          </p>
        </div>
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
            onClick={addGroup}
          >
            + افزودن گروه
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
              برای این آیتم هنوز گروهی تعریف نشده؛ با «افزودن گروه» شروع کنید.
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
                    placeholder="نام گروه (مثلاً درصد دانه قهوه)"
                    value={group.persianName}
                    onChange={(event) =>
                      updateGroup(group.uid, { persianName: event.target.value })
                    }
                  />
                  <input
                    className="ltr-input"
                    placeholder="English name"
                    value={group.englishName}
                    onChange={(event) =>
                      updateGroup(group.uid, { englishName: event.target.value })
                    }
                  />
                </div>
                <div className="option-group-controls">
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
    </section>
  )
}
