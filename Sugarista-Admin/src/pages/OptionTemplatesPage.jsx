import { useCallback, useEffect, useMemo, useState } from 'react'
import { Link } from 'react-router-dom'
import { useAdmin } from '../context/AdminContext'
import './MenuSectionMinimal.css'
import { formatMoney } from '../components/DataTable'
import { NumberStepperInput } from '../components/NumberStepperInput.jsx'
import { applyOptionGroupRuleChange } from '../utils/optionGroupRules'

let uidCounter = 0
const nextUid = () => `tpl-${Date.now()}-${uidCounter++}`

const emptyOption = (displayOrder = 0) => ({
  uid: nextUid(),
  persianName: '',
  englishName: '',
  priceDelta: 0,
  isDefault: false,
  isActive: true,
  displayOrder,
  ingredients: [],
})

const emptyTemplateForm = () => ({
  id: null,
  categoryLabel: '',
  persianName: '',
  englishName: '',
  isRequired: false,
  selectionMode: 'Single',
  minSelections: 0,
  maxSelections: 1,
  displayOrder: 0,
  isActive: true,
  options: [emptyOption(0)],
})

function templateToForm(template) {
  return {
    id: template.id,
    categoryLabel: template.categoryLabel ?? '',
    persianName: template.persianName ?? '',
    englishName: template.englishName ?? '',
    isRequired: Boolean(template.isRequired),
    selectionMode: template.selectionMode ?? 'Single',
    minSelections: template.minSelections ?? 0,
    maxSelections: template.maxSelections ?? 1,
    displayOrder: template.displayOrder ?? 0,
    isActive: template.isActive ?? true,
    options:
      (template.options ?? []).length > 0
        ? template.options.map((option, index) => ({
            uid: nextUid(),
            persianName: option.persianName ?? '',
            englishName: option.englishName ?? '',
            priceDelta: option.priceDelta ?? 0,
            isDefault: Boolean(option.isDefault),
            isActive: option.isActive ?? true,
            displayOrder: option.displayOrder ?? index,
            ingredients: (option.ingredients ?? []).map((row) => ({
              uid: nextUid(),
              ingredientId: row.ingredientId ?? '',
              quantity: row.quantity ?? 0,
            })),
          }))
        : [emptyOption(0)],
  }
}

export function OptionTemplatesPage() {
  const {
    session,
    apiRequest,
    getReadableError,
    setMessage,
    setError,
    setSaving,
    saving,
    toPersianNumber,
    ingredientOptions,
    menuItemOptions,
  } = useAdmin()

  const [templates, setTemplates] = useState([])
  const [categories, setCategories] = useState([])
  const [loading, setLoading] = useState(true)
  const [form, setForm] = useState(emptyTemplateForm)
  const [activeCategoryFilter, setActiveCategoryFilter] = useState('ALL')
  const [searchQuery, setSearchQuery] = useState('')

  // Apply-to-menu-items modal state
  const [applyTargetTemplate, setApplyTargetTemplate] = useState(null)
  const [applySelectedIds, setApplySelectedIds] = useState([])
  const [applySearch, setApplySearch] = useState('')
  const [applyUpdateLinked, setApplyUpdateLinked] = useState(true)
  const [applying, setApplying] = useState(false)

  const loadTemplates = useCallback(async () => {
    if (!session?.token) return
    setLoading(true)
    try {
      const data = await apiRequest('/admin/option-templates', { token: session.token })
      setTemplates(Array.isArray(data?.templates) ? data.templates : [])
      setCategories(Array.isArray(data?.categories) ? data.categories : [])
    } catch (requestError) {
      setError(getReadableError(requestError))
    } finally {
      setLoading(false)
    }
  }, [apiRequest, session?.token, getReadableError, setError])

  useEffect(() => {
    loadTemplates()
  }, [loadTemplates])

  const resetForm = () => {
    setForm({ ...emptyTemplateForm(), displayOrder: templates.length })
  }

  const updateOption = (optionUid, patch) => {
    setForm((current) => ({
      ...current,
      options: current.options.map((opt) => (opt.uid === optionUid ? { ...opt, ...patch } : opt)),
    }))
  }

  const addOption = () => {
    setForm((current) => ({
      ...current,
      options: [...current.options, emptyOption(current.options.length)],
    }))
  }

  const removeOption = (optionUid) => {
    setForm((current) => ({
      ...current,
      options: current.options
        .filter((opt) => opt.uid !== optionUid)
        .map((opt, idx) => ({ ...opt, displayOrder: idx })),
    }))
  }

  const addIngredientRow = (optionUid) => {
    setForm((current) => ({
      ...current,
      options: current.options.map((opt) =>
        opt.uid === optionUid
          ? {
              ...opt,
              ingredients: [...opt.ingredients, { uid: nextUid(), ingredientId: '', quantity: 0 }],
            }
          : opt,
      ),
    }))
  }

  const updateIngredientRow = (optionUid, rowUid, patch) => {
    setForm((current) => ({
      ...current,
      options: current.options.map((opt) =>
        opt.uid === optionUid
          ? {
              ...opt,
              ingredients: opt.ingredients.map((row) =>
                row.uid === rowUid ? { ...row, ...patch } : row,
              ),
            }
          : opt,
      ),
    }))
  }

  const removeIngredientRow = (optionUid, rowUid) => {
    setForm((current) => ({
      ...current,
      options: current.options.map((opt) =>
        opt.uid === optionUid
          ? { ...opt, ingredients: opt.ingredients.filter((row) => row.uid !== rowUid) }
          : opt,
      ),
    }))
  }

  const handleSubmit = async (event) => {
    event.preventDefault()
    setError('')
    setMessage('')

    if (!form.persianName.trim()) {
      setError('نام فارسی قالب گروه آپشن الزامی است.')
      return
    }
    if (form.options.length === 0) {
      setError('هر قالب باید حداقل یک گزینه داشته باشد.')
      return
    }
    if (form.isRequired && form.options.every((opt) => !opt.isActive)) {
      setError('قالب اجباری باید حداقل یک گزینهٔ فعال داشته باشد.')
      return
    }

    for (const opt of form.options) {
      if (!opt.persianName.trim()) {
        setError('نام تمام گزینه‌های قالب الزامی است.')
        return
      }
      if (Number(opt.priceDelta) < 0) {
        setError(`مبلغ افزودهٔ گزینهٔ «${opt.persianName}» نمی‌تواند منفی باشد.`)
        return
      }
      for (const row of opt.ingredients) {
        if (!row.ingredientId || Number(row.quantity) <= 0) {
          setError(`مقدار مصرف مواد اولیهٔ گزینهٔ «${opt.persianName}» باید کامل و بزرگ‌تر از صفر باشد.`)
          return
        }
      }
    }

    setSaving(true)
    try {
      const payload = {
        id: form.id,
        categoryLabel: form.categoryLabel.trim() || null,
        persianName: form.persianName.trim(),
        englishName: form.englishName.trim() || null,
        isRequired: form.isRequired,
        selectionMode: form.selectionMode,
        minSelections: Number(form.minSelections) || 0,
        maxSelections: Number(form.maxSelections) || 1,
        displayOrder: Number(form.displayOrder) || 0,
        isActive: form.isActive,
        options: form.options.map((opt, idx) => ({
          persianName: opt.persianName.trim(),
          englishName: opt.englishName.trim() || null,
          priceDelta: Number(opt.priceDelta) || 0,
          isDefault: opt.isDefault,
          isActive: opt.isActive,
          displayOrder: idx,
          ingredients: opt.ingredients
            .filter((row) => row.ingredientId)
            .map((row) => ({ ingredientId: row.ingredientId, quantity: Number(row.quantity) || 0 })),
        })),
      }

      if (form.id) {
        await apiRequest(`/admin/option-templates/${form.id}`, {
          method: 'PUT',
          token: session.token,
          body: payload,
        })
        setMessage('قالب گروه آپشن با موفقیت به‌روزرسانی شد. برای اعمال تغییرات به آیتم‌های متصل، روی «اعمال به آیتم‌ها» کلیک کنید.')
      } else {
        await apiRequest('/admin/option-templates', {
          method: 'POST',
          token: session.token,
          body: payload,
        })
        setMessage('قالب گروه آپشن جدید با موفقیت ذخیره شد.')
      }

      resetForm()
      await loadTemplates()
    } catch (requestError) {
      setError(getReadableError(requestError))
    } finally {
      setSaving(false)
    }
  }

  const handleEdit = (template) => {
    setError('')
    setMessage('')
    setForm(templateToForm(template))
    window.scrollTo({ top: 0, behavior: 'smooth' })
  }

  const handleDuplicate = async (template) => {
    setSaving(true)
    setError('')
    setMessage('')
    try {
      await apiRequest(`/admin/option-templates/${template.id}/duplicate`, {
        method: 'POST',
        token: session.token,
      })
      setMessage(`کپی قالب «${template.persianName}» ایجاد شد.`)
      await loadTemplates()
    } catch (requestError) {
      setError(getReadableError(requestError))
    } finally {
      setSaving(false)
    }
  }

  const handleDelete = async (template) => {
    if (
      !window.confirm(
        `از حذف قالب «${template.persianName}» مطمئن هستید؟ گروه‌هایی که قبلاً به آیتم‌های منو اضافه شده‌اند بدون تغییر باقی می‌مانند.`,
      )
    ) {
      return
    }
    setSaving(true)
    setError('')
    setMessage('')
    try {
      await apiRequest(`/admin/option-templates/${template.id}`, {
        method: 'DELETE',
        token: session.token,
      })
      if (form.id === template.id) {
        resetForm()
      }
      setMessage('قالب گروه آپشن حذف شد.')
      await loadTemplates()
    } catch (requestError) {
      setError(getReadableError(requestError))
    } finally {
      setSaving(false)
    }
  }

  const openApplyModal = (template) => {
    setApplyTargetTemplate(template)
    setApplySelectedIds([])
    setApplySearch('')
    setApplyUpdateLinked(true)
  }

  const toggleApplyItem = (itemId) => {
    setApplySelectedIds((current) =>
      current.includes(itemId) ? current.filter((id) => id !== itemId) : [...current, itemId],
    )
  }

  const filteredMenuItemsForApply = useMemo(() => {
    const q = applySearch.trim().toLowerCase()
    return (menuItemOptions ?? []).filter((item) => {
      if (!q) return true
      return (
        (item.persianName ?? '').toLowerCase().includes(q) ||
        (item.englishName ?? '').toLowerCase().includes(q)
      )
    })
  }, [menuItemOptions, applySearch])

  const handleApplyConfirm = async () => {
    if (!applyTargetTemplate || applySelectedIds.length === 0) return
    setApplying(true)
    setError('')
    setMessage('')
    try {
      const result = await apiRequest(`/admin/option-templates/${applyTargetTemplate.id}/apply`, {
        method: 'POST',
        token: session.token,
        body: {
          templateId: applyTargetTemplate.id,
          menuItemIds: applySelectedIds,
          updateExistingLinked: applyUpdateLinked,
        },
      })
      const parts = []
      if (result.addedCount > 0) parts.push(`${toPersianNumber(result.addedCount)} آیتم جدید متصل شد`)
      if (result.updatedCount > 0) parts.push(`${toPersianNumber(result.updatedCount)} آیتم متصل به‌روزرسانی شد`)
      if (result.skippedCount > 0) parts.push(`${toPersianNumber(result.skippedCount)} آیتم تکراری رد شد`)
      setMessage(`قالب «${applyTargetTemplate.persianName}» اعمال شد: ${parts.join('، ') || 'انجام شد'}.`)
      setApplyTargetTemplate(null)
      await loadTemplates()
    } catch (requestError) {
      setError(getReadableError(requestError))
    } finally {
      setApplying(false)
    }
  }

  const filteredTemplates = useMemo(() => {
    const q = searchQuery.trim().toLowerCase()
    return templates.filter((template) => {
      if (activeCategoryFilter === 'UNCATEGORIZED' && template.categoryLabel) return false
      if (
        activeCategoryFilter !== 'ALL' &&
        activeCategoryFilter !== 'UNCATEGORIZED' &&
        (template.categoryLabel ?? '') !== activeCategoryFilter
      ) {
        return false
      }
      if (!q) return true
      return (
        (template.persianName ?? '').toLowerCase().includes(q) ||
        (template.englishName ?? '').toLowerCase().includes(q) ||
        (template.categoryLabel ?? '').toLowerCase().includes(q) ||
        (template.options ?? []).some((o) => (o.persianName ?? '').toLowerCase().includes(q))
      )
    })
  }, [templates, activeCategoryFilter, searchQuery])

  const groupedTemplates = useMemo(() => {
    const map = new Map()
    for (const template of filteredTemplates) {
      const key = template.categoryLabel?.trim() || 'عمومی (بدون دسته‌بندی)'
      if (!map.has(key)) map.set(key, [])
      map.get(key).push(template)
    }
    return Array.from(map.entries())
  }, [filteredTemplates])

  const totalLinked = useMemo(
    () => templates.reduce((sum, t) => sum + (t.linkedMenuItemCount ?? 0), 0),
    [templates],
  )

  return (
    <section className="page-grid menu-minimal-scope">
      <section className="panel full-width">
        <div className="panel-head">
          <div>
            <h3>کتابخانهٔ قالب‌های گروه آپشن</h3>
            <p>
              آپشن‌های پرتکرار (مثل نوع شیر، سیروپ‌ها، دمای سرو یا شات اضافه) را یک‌بار اینجا تعریف و
              دسته‌بندی کنید و هر زمان خواستید با یک کلیک به هر تعداد آیتم منو اضافه یا همگام‌سازی کنید.
            </p>
          </div>
          <div className="option-groups-actions">
            <Link className="ghost-button" to="/menu-items" style={{ textDecoration: 'none' }}>
              بازگشت به آیتم‌های منو
            </Link>
          </div>
        </div>

        <div className="stat-grid">
          <div className="stat-card">
            <span className="stat-label">تعداد قالب‌ها</span>
            <strong className="stat-value">{toPersianNumber(templates.length)}</strong>
          </div>
          <div className="stat-card">
            <span className="stat-label">دسته‌بندی‌های قالب</span>
            <strong className="stat-value">{toPersianNumber(categories.length)}</strong>
          </div>
          <div className="stat-card">
            <span className="stat-label">اتصال فعال به آیتم‌های منو</span>
            <strong className="stat-value">{toPersianNumber(totalLinked)}</strong>
          </div>
        </div>
      </section>

      <section className="panel full-width">
        <div className="panel-head">
          <div>
            <h3>{form.id ? `ویرایش قالب: ${form.persianName}` : 'ایجاد قالب گروه آپشن جدید'}</h3>
            <p>گزینه‌ها، قیمت افزوده و مواد اولیه مصرفی را مشخص کنید تا در هر محصولی آماده استفاده باشد.</p>
          </div>
          {form.id ? (
            <button type="button" className="ghost-button" onClick={resetForm}>
              انصراف از ویرایش
            </button>
          ) : null}
        </div>

        <form className="admin-form" onSubmit={handleSubmit}>
          <div className="option-group-card is-required">
            <div className="option-group-title" style={{ gridTemplateColumns: 'repeat(auto-fit, minmax(210px, 1fr))' }}>
              <label>
                <span>دسته‌بندی قالب (اختیاری)</span>
                <input
                  list="option-template-categories-datalist"
                  placeholder="مثلاً نوشیدنی‌های بر پایه شیر، سیروپ‌ها..."
                  value={form.categoryLabel}
                  onChange={(event) => setForm((f) => ({ ...f, categoryLabel: event.target.value }))}
                />
                <datalist id="option-template-categories-datalist">
                  {categories.map((cat) => (
                    <option key={cat} value={cat} />
                  ))}
                </datalist>
              </label>
              <label>
                <span>نام فارسی گروه آپشن *</span>
                <input
                  placeholder="مثلاً نوع شیر"
                  value={form.persianName}
                  onChange={(event) => setForm((f) => ({ ...f, persianName: event.target.value }))}
                  required
                />
              </label>
              <label>
                <span>نام انگلیسی (اختیاری)</span>
                <input
                  className="ltr-input"
                  placeholder="Milk Type"
                  value={form.englishName}
                  onChange={(event) => setForm((f) => ({ ...f, englishName: event.target.value }))}
                />
              </label>
            </div>

            <div className="option-group-settings">
              <label className="checkbox-row">
                <input
                  type="checkbox"
                  checked={Boolean(form.isRequired)}
                  onChange={(event) =>
                    setForm((f) => applyOptionGroupRuleChange(f, { isRequired: event.target.checked }))
                  }
                />
                <span>اجباری (مشتری باید انتخاب کند)</span>
              </label>
              <label className="checkbox-row">
                <input
                  type="checkbox"
                  checked={Boolean(form.isActive)}
                  onChange={(event) => setForm((f) => ({ ...f, isActive: event.target.checked }))}
                />
                <span>فعال</span>
              </label>
              <label>
                <span>حالت انتخاب</span>
                <select
                  value={form.selectionMode}
                  onChange={(event) =>
                    setForm((f) => applyOptionGroupRuleChange(f, { selectionMode: event.target.value }))
                  }
                >
                  <option value="Single">تک‌انتخابی (رادیویی)</option>
                  <option value="Multi">چندانتخابی</option>
                </select>
              </label>
              <label>
                <span>حداقل انتخاب</span>
                <NumberStepperInput
                  min={0}
                  value={form.minSelections}
                  ariaLabel="حداقل انتخاب"
                  onChange={(nextValue) =>
                    setForm((f) => applyOptionGroupRuleChange(f, { minSelections: nextValue }))
                  }
                />
              </label>
              <label>
                <span>حداکثر انتخاب</span>
                <NumberStepperInput
                  min={1}
                  value={form.maxSelections}
                  ariaLabel="حداکثر انتخاب"
                  onChange={(nextValue) =>
                    setForm((f) => applyOptionGroupRuleChange(f, { maxSelections: nextValue }))
                  }
                />
              </label>
            </div>

            <div className="option-rows">
              {form.options.map((option) => (
                <div className="option-row" key={option.uid}>
                  <div className="option-row-main">
                    <input
                      placeholder="نام گزینه (مثلاً شیر جو دوسر)"
                      value={option.persianName}
                      onChange={(event) => updateOption(option.uid, { persianName: event.target.value })}
                    />
                    <input
                      className="ltr-input"
                      placeholder="English (Oat Milk)"
                      value={option.englishName}
                      onChange={(event) => updateOption(option.uid, { englishName: event.target.value })}
                    />
                    <label className="option-price-input">
                      <span>+ مبلغ (تومان)</span>
                      <input
                        className="ltr-input"
                        type="number"
                        min="0"
                        value={option.priceDelta}
                        onChange={(event) => updateOption(option.uid, { priceDelta: event.target.value })}
                      />
                    </label>
                    <label className="checkbox-row">
                      <input
                        type="checkbox"
                        checked={option.isDefault}
                        onChange={(event) => updateOption(option.uid, { isDefault: event.target.checked })}
                      />
                      <span>پیش‌فرض</span>
                    </label>
                    <label className="checkbox-row">
                      <input
                        type="checkbox"
                        checked={option.isActive}
                        onChange={(event) => updateOption(option.uid, { isActive: event.target.checked })}
                      />
                      <span>فعال</span>
                    </label>
                    <button
                      type="button"
                      className="ghost-button tiny danger"
                      onClick={() => removeOption(option.uid)}
                    >
                      حذف
                    </button>
                  </div>

                  <div className="option-ingredients">
                    <div className="option-ingredients-head">
                      <button
                        type="button"
                        className="ghost-button tiny"
                        onClick={() => addIngredientRow(option.uid)}
                      >
                        + ماده اولیهٔ مصرفی
                      </button>
                      {option.ingredients.length === 0 ? (
                        <span className="option-ingredients-hint">
                          در صورت تعریف، هنگام سفارش مشتری خودکار از انبار کسر می‌شود.
                        </span>
                      ) : null}
                    </div>
                    {option.ingredients.map((row) => {
                      const ingredient = ingredientOptions.find((c) => c.id === row.ingredientId)
                      return (
                        <div className="option-ingredient-row" key={row.uid}>
                          <select
                            value={row.ingredientId}
                            onChange={(event) =>
                              updateIngredientRow(option.uid, row.uid, { ingredientId: event.target.value })
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
                              updateIngredientRow(option.uid, row.uid, { quantity: event.target.value })
                            }
                          />
                          <span className="option-ingredient-unit">
                            {ingredient?.unit ? `هر انتخاب: ${ingredient.unit}` : ''}
                          </span>
                          <button
                            type="button"
                            className="ghost-button tiny danger"
                            onClick={() => removeIngredientRow(option.uid, row.uid)}
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

            <div className="option-groups-actions">
              <button type="button" className="ghost-button tiny" onClick={addOption}>
                + افزودن گزینه
              </button>
              <button type="submit" disabled={saving}>
                {form.id ? 'ذخیره تغییرات قالب' : 'ثبت قالب در کتابخانه'}
              </button>
            </div>
          </div>
        </form>
      </section>

      <section className="panel full-width">
        <div className="panel-head">
          <div>
            <h3>قالب‌های ذخیره‌شده ({toPersianNumber(filteredTemplates.length)})</h3>
            <p>روی «اعمال به آیتم‌ها» بزنید تا قالب را به‌صورت گروهی به چند محصول اضافه یا به‌روزرسانی کنید.</p>
          </div>
          <input
            type="search"
            placeholder="جستجو در قالب‌ها یا گزینه‌ها..."
            value={searchQuery}
            onChange={(event) => setSearchQuery(event.target.value)}
            style={{ width: 260, minWidth: 220, maxWidth: '100%' }}
          />
        </div>

        <div className="option-template-pills">
          <button
            type="button"
            className={`option-template-pill ${activeCategoryFilter === 'ALL' ? 'is-active' : ''}`}
            onClick={() => setActiveCategoryFilter('ALL')}
          >
            همه ({toPersianNumber(templates.length)})
          </button>
          {categories.map((cat) => (
            <button
              key={cat}
              type="button"
              className={`option-template-pill ${activeCategoryFilter === cat ? 'is-active' : ''}`}
              onClick={() => setActiveCategoryFilter(cat)}
            >
              {cat} ({toPersianNumber(templates.filter((t) => t.categoryLabel === cat).length)})
            </button>
          ))}
          {templates.some((t) => !t.categoryLabel) ? (
            <button
              type="button"
              className={`option-template-pill ${activeCategoryFilter === 'UNCATEGORIZED' ? 'is-active' : ''}`}
              onClick={() => setActiveCategoryFilter('UNCATEGORIZED')}
            >
              عمومی ({toPersianNumber(templates.filter((t) => !t.categoryLabel).length)})
            </button>
          ) : null}
        </div>

        {loading ? (
          <p className="feedback">در حال بارگذاری قالب‌های آپشن...</p>
        ) : groupedTemplates.length === 0 ? (
          <p className="feedback">
            هنوز قالبی در این بخش ثبت نشده است. از فرم بالا اولین قالب (مثلاً «نوع شیر» یا «سیروپ‌ها») را بسازید.
          </p>
        ) : (
          <div className="option-groups-list">
            {groupedTemplates.map(([categoryName, items]) => (
              <div className="option-template-group-section" key={categoryName}>
                <div className="option-template-group-heading">
                  <span>{categoryName}</span>
                  <span className="table-subtext">{toPersianNumber(items.length)} قالب</span>
                </div>
                <div className="option-template-grid">
                  {items.map((template) => (
                    <article className="option-template-card" key={template.id}>
                      <div className="option-template-card-head">
                        <div>
                          <h4>{template.persianName}</h4>
                          {template.englishName ? (
                            <div className="option-template-card-sub ltr-text">{template.englishName}</div>
                          ) : null}
                        </div>
                        <span className="option-group-template-badge">
                          متصل به {toPersianNumber(template.linkedMenuItemCount)} آیتم
                        </span>
                      </div>

                      <div className="option-template-badges">
                        <span className="option-template-chip">
                          {template.isRequired ? 'اجباری' : 'دلخواه'}
                        </span>
                        <span className="option-template-chip">
                          {template.selectionMode === 'Multi' ? 'چندانتخابی' : 'تک‌انتخابی'}
                        </span>
                        {!template.isActive ? (
                          <span className="option-template-chip">غیرفعال</span>
                        ) : null}
                      </div>

                      <div className="option-template-chip-list">
                        {(template.options ?? []).map((opt) => (
                          <span className="option-template-chip" key={opt.id ?? opt.persianName}>
                            {opt.persianName}
                            {Number(opt.priceDelta) > 0 ? (
                              <strong>+{formatMoney(opt.priceDelta)}</strong>
                            ) : null}
                          </span>
                        ))}
                      </div>

                      <div className="option-template-card-foot">
                        <button
                          type="button"
                          className="link-button"
                          onClick={() => openApplyModal(template)}
                        >
                          اعمال به آیتم‌ها
                        </button>
                        <div className="table-actions">
                          <button
                            type="button"
                            className="link-button"
                            onClick={() => handleEdit(template)}
                          >
                            ویرایش
                          </button>
                          <button
                            type="button"
                            className="link-button"
                            onClick={() => handleDuplicate(template)}
                          >
                            کپی
                          </button>
                          <button
                            type="button"
                            className="link-button link-danger"
                            onClick={() => handleDelete(template)}
                          >
                            حذف
                          </button>
                        </div>
                      </div>
                    </article>
                  ))}
                </div>
              </div>
            ))}
          </div>
        )}
      </section>

      {applyTargetTemplate ? (
        <div
          className="option-picker-backdrop"
          role="dialog"
          aria-modal="true"
          onClick={(event) => {
            if (event.target === event.currentTarget) setApplyTargetTemplate(null)
          }}
        >
          <div className="option-picker-modal">
            <div className="option-picker-head">
              <h4>اعمال قالب «{applyTargetTemplate.persianName}» به آیتم‌های منو</h4>
              <button
                type="button"
                className="ghost-button tiny"
                onClick={() => setApplyTargetTemplate(null)}
              >
                بستن
              </button>
            </div>
            <div className="option-picker-body">
              <div style={{ display: 'flex', gap: 8, flexWrap: 'wrap', alignItems: 'center' }}>
                <input
                  type="search"
                  placeholder="جستجو بین آیتم‌های منو..."
                  value={applySearch}
                  onChange={(event) => setApplySearch(event.target.value)}
                  style={{ flex: 1, minWidth: 180 }}
                />
                <button
                  type="button"
                  className="ghost-button tiny"
                  onClick={() =>
                    setApplySelectedIds(filteredMenuItemsForApply.map((item) => item.id))
                  }
                >
                  انتخاب همه
                </button>
                <button
                  type="button"
                  className="ghost-button tiny"
                  onClick={() => setApplySelectedIds([])}
                >
                  پاک کردن
                </button>
                {applyTargetTemplate.linkedMenuItemIds?.length > 0 ? (
                  <button
                    type="button"
                    className="ghost-button tiny"
                    onClick={() => setApplySelectedIds([...applyTargetTemplate.linkedMenuItemIds])}
                  >
                    انتخاب متصل‌ها ({toPersianNumber(applyTargetTemplate.linkedMenuItemIds.length)})
                  </button>
                ) : null}
              </div>

              <label className="checkbox-row">
                <input
                  type="checkbox"
                  checked={applyUpdateLinked}
                  onChange={(event) => setApplyUpdateLinked(event.target.checked)}
                />
                <span>
                  اگر آیتمی از قبل این گروه را دارد، گزینه‌ها و قیمت‌هایش با آخرین نسخه این قالب به‌روزرسانی شود
                </span>
              </label>

              <div className="option-picker-list">
                {filteredMenuItemsForApply.map((item) => {
                  const selected = applySelectedIds.includes(item.id)
                  const alreadyLinked = (applyTargetTemplate.linkedMenuItemIds ?? []).includes(item.id)
                  return (
                    <label
                      key={item.id}
                      className={`option-picker-item ${selected ? 'is-selected' : ''}`}
                    >
                      <span className="option-picker-item-main">
                        <input
                          type="checkbox"
                          checked={selected}
                          onChange={() => toggleApplyItem(item.id)}
                        />
                        <span>
                          <strong>{item.persianName}</strong>
                          {item.englishName ? (
                            <span className="table-subtext ltr-text"> — {item.englishName}</span>
                          ) : null}
                        </span>
                      </span>
                      {alreadyLinked ? (
                        <span className="option-group-template-badge">متصل</span>
                      ) : null}
                    </label>
                  )
                })}
              </div>
            </div>
            <div className="option-picker-foot">
              <span className="table-subtext">
                {toPersianNumber(applySelectedIds.length)} آیتم انتخاب شده
              </span>
              <div className="option-groups-actions">
                <button
                  type="button"
                  className="ghost-button"
                  onClick={() => setApplyTargetTemplate(null)}
                >
                  انصراف
                </button>
                <button
                  type="button"
                  className="primary-button"
                  disabled={applying || applySelectedIds.length === 0}
                  onClick={handleApplyConfirm}
                >
                  {applying ? 'در حال اعمال...' : 'اعمال به آیتم‌های انتخاب‌شده'}
                </button>
              </div>
            </div>
          </div>
        </div>
      ) : null}
    </section>
  )
}
