import { useAdmin } from '../context/AdminContext'
import { LOCATION_OPTIONS, UNIT_OPTIONS } from '../constants/adminForms'
import { PaginatedAdminTable } from '../components/PaginatedAdminTable'

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
                },
              })
    
              setIngredientForm({ ...emptyIngredientForm, displayOrder: 0 })
              setMessage('ماده اولیه با موفقیت ذخیره شد.')
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
              <h3>{ingredientForm.id ? 'ویرایش ماده اولیه' : 'ماده اولیه جدید'}</h3>
              <p>مواد اولیه در دستور پخت آیتم‌های منو استفاده می‌شوند.</p>
            </div>
            <button
              type="button"
              className="ghost-button"
              onClick={() => setIngredientForm({ ...emptyIngredientForm, displayOrder: 0 })}
            >
              پاک کردن فرم
            </button>
          </div>
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
            <span>فعال (قابل استفاده در دستور پخت)</span>
          </label>
          <button type="submit" disabled={saving}>
            {saving ? 'در حال ذخیره...' : 'ذخیره ماده اولیه'}
          </button>
        </form>
    
        <section className="panel list-panel">
          <div className="panel-head">
            <div>
              <h3>مواد اولیه</h3>
              <p>بارگذاری صفحه‌بندی شده از سرور</p>
            </div>
          </div>
          <PaginatedAdminTable
            apiPath="/admin/ingredients"
            token={session.token}
            columns={ingredientColumns}
            enabled
            reloadToken={tableReloadToken}
            searchPlaceholder="جستجو در مواد اولیه..."
          />
        </section>
      </section>
  )
}
