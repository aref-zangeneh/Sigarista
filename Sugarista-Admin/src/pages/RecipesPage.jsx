import { useAdmin } from '../context/AdminContext'
import { DataTable } from '../components/DataTable'

export function RecipesPage() {
  const {
    session,
    menuItemOptions,
    ingredientOptions,
    recipeState,
    setRecipeState,
    saving,
    setSaving,
    setMessage,
    setError,
    recipeColumns,
    apiRequest,
    getReadableError,
    normalizeRecipeResponse,
    toPersianNumber,
    emptyRecipeLineForm,
  } = useAdmin()

  return (
      <section className="panel-stack">
        <section className="panel">
          <div className="panel-head">
            <div>
              <h3>انتخاب آیتم منو</h3>
              <p>آیتمی را انتخاب کنید تا دستور پخت آن را مشاهده یا ویرایش کنید.</p>
            </div>
          </div>
          <label>
            <span>آیتم منو</span>
            <select
              value={recipeState.selectedMenuItemId}
              onChange={async (event) => {
                const menuItemId = event.target.value
                setRecipeState((current) => ({
                  ...current,
                  selectedMenuItemId: menuItemId,
                  description: '',
                  lines: [],
                  lineForm: emptyRecipeLineForm,
                }))
    
                if (!menuItemId) {
                  return
                }
    
                try {
                  const recipe = normalizeRecipeResponse(
                    await apiRequest(`/admin/recipes/${menuItemId}`, { token: session.token }),
                  )
                  setRecipeState((current) => ({
                    ...current,
                    description: recipe.description,
                    lines: recipe.lines,
                  }))
                } catch (requestError) {
                  setError(getReadableError(requestError))
                }
              }}
            >
              <option value="">-- انتخاب آیتم منو --</option>
              {menuItemOptions.map((item) => (
                <option key={item.id} value={item.id}>
                  {item.persianName} — {item.englishName}
                </option>
              ))}
            </select>
          </label>
        </section>
    
        {recipeState.selectedMenuItemId ? (
          <>
            <section className="panel form-panel">
              <div className="panel-head">
                <div>
                  <h3>توضیحات دستور پخت</h3>
                  <p>شرح کامل آماده‌سازی محصول — مثلاً: «لاته با ۴۵ میلی‌لیتر اسپرسو و ۱۵۰ میلی‌لیتر شیر...»</p>
                </div>
                <button
                  type="button"
                  disabled={saving}
                  onClick={async () => {
                    setSaving(true)
                    setError('')
                    setMessage('')
                    try {
                      await apiRequest(`/admin/recipes/${recipeState.selectedMenuItemId}/description`, {
                        method: 'PUT',
                        token: session.token,
                        body: { description: recipeState.description.trim() || null },
                      })
                      setMessage('توضیحات دستور پخت ذخیره شد.')
                    } catch (requestError) {
                      setError(getReadableError(requestError))
                    } finally {
                      setSaving(false)
                    }
                  }}
                >
                  {saving ? 'در حال ذخیره...' : 'ذخیره توضیحات'}
                </button>
              </div>
              <label>
                <span>شرح دستور پخت</span>
                <textarea
                  rows="5"
                  value={recipeState.description}
                  onChange={(event) =>
                    setRecipeState((current) => ({ ...current, description: event.target.value }))
                  }
                  placeholder="مثال: Ice Latte — 45ml espresso + 150ml milk + ice..."
                />
              </label>
            </section>
    
          <section className="two-column-layout">
            <form
              className="panel form-panel"
              onSubmit={async (event) => {
                event.preventDefault()
                if (!recipeState.lineForm.ingredientId || !recipeState.lineForm.quantity) {
                  return
                }
    
                setSaving(true)
                setError('')
                setMessage('')
    
                try {
                  await apiRequest(`/admin/recipes/${recipeState.selectedMenuItemId}`, {
                    method: 'POST',
                    token: session.token,
                    body: {
                      menuItemId: recipeState.selectedMenuItemId,
                      ingredientId: recipeState.lineForm.ingredientId,
                      quantity: Number(recipeState.lineForm.quantity),
                    },
                  })
    
                  setRecipeState((current) => ({
                    ...current,
                    lineForm: emptyRecipeLineForm,
                  }))
                  setMessage('خط دستور پخت ذخیره شد.')
    
                  const recipe = normalizeRecipeResponse(
                    await apiRequest(`/admin/recipes/${recipeState.selectedMenuItemId}`, {
                      token: session.token,
                    }),
                  )
                  setRecipeState((current) => ({ ...current, lines: recipe.lines }))
                } catch (requestError) {
                  setError(getReadableError(requestError))
                } finally {
                  setSaving(false)
                }
              }}
            >
              <div className="panel-head">
                <div>
                  <h3>افزودن / ویرایش ماده</h3>
                  <p>اگر این ماده قبلاً در دستور پخت بوده، مقدار آن به‌روزرسانی می‌شود.</p>
                </div>
              </div>
              <label>
                <span>ماده اولیه</span>
                <select
                  required
                  value={recipeState.lineForm.ingredientId}
                  onChange={(event) =>
                    setRecipeState((current) => ({
                      ...current,
                      lineForm: { ...current.lineForm, ingredientId: event.target.value },
                    }))
                  }
                >
                  <option value="">-- انتخاب قلم مصرفی --</option>
                  {(ingredientOptions ?? [])
                    .filter((ingredient) => ingredient.isActive)
                    .map((ingredient) => (
                      <option key={ingredient.id} value={ingredient.id}>
                        {ingredient.persianName} ({ingredient.unit})
                      </option>
                    ))}
                </select>
              </label>
              <label>
                <span>مقدار</span>
                <input
                  className="ltr-input"
                  type="number"
                  min="0.001"
                  step="0.001"
                  required
                  value={recipeState.lineForm.quantity}
                  onChange={(event) =>
                    setRecipeState((current) => ({
                      ...current,
                      lineForm: { ...current.lineForm, quantity: event.target.value },
                    }))
                  }
                />
              </label>
              <button type="submit" disabled={saving}>
                {saving ? 'در حال ذخیره...' : 'ذخیره در دستور پخت'}
              </button>
            </form>
    
            <section className="panel list-panel">
              <div className="panel-head">
                <div>
                  <h3>اقلام فرمولاسیون (مواد اولیه و بسته‌بندی)</h3>
                  <p>
                    {recipeState.lines.length === 0
                      ? 'هنوز هیچ ماده‌ای ثبت نشده.'
                      : `${toPersianNumber(recipeState.lines.length)} ماده`}
                  </p>
                </div>
              </div>
              {recipeState.description ? (
                <div className="recipe-description-preview">
                  <strong>توضیحات:</strong>
                  <p>{recipeState.description}</p>
                </div>
              ) : null}
              <DataTable
                data={recipeState.lines}
                columns={recipeColumns}
                searchPlaceholder="جستجو در دستور پخت..."
                emptyMessage="هنوز هیچ ماده‌ای ثبت نشده."
              />
            </section>
          </section>
          </>
        ) : null}
      </section>
  )
}
