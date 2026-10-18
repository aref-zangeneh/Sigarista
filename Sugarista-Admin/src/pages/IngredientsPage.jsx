import { useEffect, useState } from 'react'
import { useAdmin } from '../context/AdminContext'
import { KIND_OPTIONS, LOCATION_OPTIONS, UNIT_OPTIONS } from '../constants/adminForms'
import { PaginatedAdminTable } from '../components/PaginatedAdminTable'

const KIND_FILTERS = [
  { value: '', label: 'همه' },
  { value: 'ingredient', label: 'مواد اولیه' },
  { value: 'packaging', label: 'بسته‌بندی' },
]

export function IngredientsPage() {
  const {
    session,
    tableReloadToken,
    ingredientForm,
    setIngredientForm,
    saving,
    setSaving,
    setMessage,
    setError,
    bumpTableReload,
    ingredientColumns,
    apiRequest,
    getReadableError,
    optionalNumber,
    emptyIngredientForm,
  } = useAdmin()

  const [kindFilter, setKindFilter] = useState('')
  const [supplierOptions, setSupplierOptions] = useState([])

  useEffect(() => {
    let cancelled = false
    async function loadSuppliers() {
      if (!session?.token) return
      try {
        const data = await apiRequest('/admin/suppliers', { token: session.token })
        if (!cancelled) {
          setSupplierOptions(Array.isArray(data) ? data : [])
        }
      } catch {
        // supplier list is optional for this page; keep silent if it fails
      }
    }
    loadSuppliers()
    return () => {
      cancelled = true
    }
  }, [session?.token, apiRequest])

  const selectedSupplierIds = Array.isArray(ingredientForm.supplierIds) ? ingredientForm.supplierIds : []
  const selectedKind = ingredientForm.kind ?? 'ingredient'

  function toggleSupplier(supplierId) {
    setIngredientForm((currentForm) => {
      const currentIds = Array.isArray(currentForm.supplierIds) ? currentForm.supplierIds : []
      const has = currentIds.includes(supplierId)
      const nextIds = has ? currentIds.filter((id) => id !== supplierId) : [...currentIds, supplierId]
      let nextPreferred = currentForm.preferredSupplierId ?? ''
      if (!has && !nextPreferred) {
        // first linked supplier becomes preferred by default
        nextPreferred = supplierId
      }
      if (has && nextPreferred === supplierId) {
        nextPreferred = nextIds.includes(nextPreferred) ? nextPreferred : ''
      }
      return { ...currentForm, supplierIds: nextIds, preferredSupplierId: nextPreferred }
    })
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
              const path = ingredientForm.id
                ? `/admin/ingredients/${ingredientForm.id}`
                : '/admin/ingredients'

              await apiRequest(path, {
                method: ingredientForm.id ? 'PUT' : 'POST',
                token: session.token,
                body: {
                  persianName: ingredientForm.persianName,
                  englishName: ingredientForm.englishName,
                  unit: ingredientForm.unit,
                  description: ingredientForm.description,
                  displayOrder: Number(ingredientForm.displayOrder),
                  isActive: ingredientForm.isActive,
                  inventoryLocation: ingredientForm.inventoryLocation,
                  unitCost: optionalNumber(ingredientForm.unitCost) ?? 0,
                  kind: selectedKind,
                  supplierIds: selectedSupplierIds,
                  preferredSupplierId: ingredientForm.preferredSupplierId || null,
                },
              })

              setIngredientForm({ ...emptyIngredientForm, displayOrder: 0 })
              setMessage(selectedKind === 'packaging' ? 'بسته‌بندی با موفقیت ذخیره شد.' : 'ماده اولیه با موفقیت ذخیره شد.')
              bumpTableReload()
            } catch (requestError) {
              setError(getReadableError(requestError))
            } finally {
              setSaving(false)
            }
          }}
        >
          <div className="panel-head">
            <div>
              <h3>{ingredientForm.id ? 'ویرایش قلم مصرفی' : selectedKind === 'packaging' ? 'بسته‌بندی جدید' : 'ماده اولیه جدید'}</h3>
              <p>مواد اولیه در دستور پخت و بسته‌بندی‌ها در خرید انبار و ثبت مصرف استفاده می‌شوند.</p>
            </div>
            <button
              type="button"
              className="ghost-button"
              onClick={() => setIngredientForm({ ...emptyIngredientForm, displayOrder: 0 })}
            >
              پاک کردن فرم
            </button>
          </div>

          <label>
            <span>نوع قلم</span>
            <select
              value={selectedKind}
              onChange={(event) => setIngredientForm((f) => ({ ...f, kind: event.target.value }))}
            >
              {KIND_OPTIONS.map((kind) => (
                <option key={kind.value} value={kind.value}>{kind.label}</option>
              ))}
            </select>
          </label>

          <div className="field-grid">
            <label>
              <span>نام فارسی</span>
              <input
                required
                value={ingredientForm.persianName}
                onChange={(event) =>
                  setIngredientForm((currentForm) => ({ ...currentForm, persianName: event.target.value }))
                }
              />
            </label>
            <label>
              <span>نام انگلیسی</span>
              <input
                className="ltr-input"
                required
                value={ingredientForm.englishName}
                onChange={(event) =>
                  setIngredientForm((currentForm) => ({ ...currentForm, englishName: event.target.value }))
                }
              />
            </label>
          </div>
          <div className="field-grid">
            <label>
              <span>واحد اندازه‌گیری</span>
              <select
                className="ltr-input"
                value={ingredientForm.unit}
                onChange={(event) =>
                  setIngredientForm((currentForm) => ({ ...currentForm, unit: event.target.value }))
                }
              >
                {UNIT_OPTIONS.map((unit) => (
                  <option key={unit} value={unit}>{unit}</option>
                ))}
              </select>
            </label>
            <label>
              <span>محل نگهداری</span>
              <select
                value={ingredientForm.inventoryLocation}
                onChange={(event) =>
                  setIngredientForm((currentForm) => ({ ...currentForm, inventoryLocation: event.target.value }))
                }
              >
                {LOCATION_OPTIONS.map((loc) => (
                  <option key={loc.value} value={loc.value}>{loc.label}</option>
                ))}
              </select>
            </label>
          </div>
          <label>
            <span>توضیح (اختیاری)</span>
            <input
              value={ingredientForm.description}
              onChange={(event) =>
                setIngredientForm((currentForm) => ({ ...currentForm, description: event.target.value }))
              }
            />
          </label>
          <label>
            <span>بهای واحد (تومان — برای محاسبه سود)</span>
            <input
              className="ltr-input"
              type="number"
              min="0"
              step="0.01"
              value={ingredientForm.unitCost}
              onChange={(event) =>
                setIngredientForm((currentForm) => ({ ...currentForm, unitCost: event.target.value }))
              }
            />
          </label>
          <label>
            <span>ترتیب نمایش</span>
            <input
              className="ltr-input"
              type="number"
              min="0"
              value={ingredientForm.displayOrder}
              onChange={(event) =>
                setIngredientForm((currentForm) => ({ ...currentForm, displayOrder: Number(event.target.value) }))
              }
            />
          </label>
          <label className="checkbox-row">
            <input
              type="checkbox"
              checked={ingredientForm.isActive}
              onChange={(event) =>
                setIngredientForm((currentForm) => ({ ...currentForm, isActive: event.target.checked }))
              }
            />
            <span>فعال (قابل استفاده در دستور پخت و خرید)</span>
          </label>

          <fieldset className="supplier-links-panel">
            <legend>تامین‌کنندگان این قلم</legend>
            {supplierOptions.length === 0 ? (
              <p className="purchase-unit-hint">
                هنوز تامین‌کننده‌ای ثبت نشده است. از منوی «تامین‌کنندگان» یکی اضافه کنید.
              </p>
            ) : (
              <>
                <div className="supplier-links-grid">
                  {supplierOptions.map((supplier) => (
                    <label key={supplier.id} className="checkbox-row">
                      <input
                        type="checkbox"
                        checked={selectedSupplierIds.includes(supplier.id)}
                        onChange={() => toggleSupplier(supplier.id)}
                      />
                      <span>{supplier.persianName}</span>
                    </label>
                  ))}
                </div>
                <label>
                  <span>تامین‌کننده اصلی (برای انتخاب سریع در فاکتور)</span>
                  <select
                    value={ingredientForm.preferredSupplierId ?? ''}
                    onChange={(event) =>
                      setIngredientForm((currentForm) => ({ ...currentForm, preferredSupplierId: event.target.value }))
                    }
                  >
                    <option value="">-- بدون انتخاب --</option>
                    {selectedSupplierIds.map((supplierId) => {
                      const supplier = supplierOptions.find((s) => s.id === supplierId)
                      return (
                        <option key={supplierId} value={supplierId}>
                          {supplier?.persianName ?? 'تامین‌کننده'}
                        </option>
                      )
                    })}
                  </select>
                </label>
                <p className="purchase-unit-hint">
                  با ثبت تامین‌کننده‌ها، هنگام ثبت فاکتور ورود برای این قلم، تامین‌کننده‌هایش در بالای لیست انتخاب می‌شوند.
                </p>
              </>
            )}
          </fieldset>

          <button type="submit" disabled={saving}>
            {saving ? 'در حال ذخیره...' : selectedKind === 'packaging' ? 'ذخیره بسته‌بندی' : 'ذخیره ماده اولیه'}
          </button>
        </form>

        <section className="panel list-panel">
          <div className="panel-head">
            <div>
              <h3>اقلام مصرفی</h3>
              <p>بارگذاری صفحه‌بندی شده از سرور</p>
            </div>
            <div className="purchase-mode-toggle">
              {KIND_FILTERS.map((filter) => (
                <label key={filter.value} className="checkbox-row">
                  <input
                    type="radio"
                    name="kindFilter"
                    checked={kindFilter === filter.value}
                    onChange={() => setKindFilter(filter.value)}
                  />
                  <span>{filter.label}</span>
                </label>
              ))}
            </div>
          </div>
          <PaginatedAdminTable
            apiPath={`/admin/ingredients${kindFilter ? `?kind=${kindFilter}` : ''}`}
            token={session.token}
            columns={ingredientColumns}
            enabled
            reloadToken={tableReloadToken}
            searchPlaceholder="جستجو در اقلام مصرفی..."
          />
        </section>
      </section>
  )
}
