import { useAdmin } from '../context/AdminContext'
import { PaginatedAdminTable } from '../components/PaginatedAdminTable'

export function ModifiersPage() {
  const {
    session,
    tableReloadToken,
    modifierForm,
    setModifierForm,
    saving,
    setSaving,
    setMessage,
    setError,
    bumpTableReload,
    modifierColumns,
    apiRequest,
    getReadableError,
    emptyModifierForm,
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
              const path = modifierForm.id
                ? `/admin/modifiers/${modifierForm.id}`
                : '/admin/modifiers'
    
              await apiRequest(path, {
                method: modifierForm.id ? 'PUT' : 'POST',
                token: session.token,
                body: {
                  persianName: modifierForm.persianName,
                  englishName: modifierForm.englishName,
                  displayOrder: Number(modifierForm.displayOrder),
                  isActive: modifierForm.isActive,
                },
              })
    
              setModifierForm({ ...emptyModifierForm, displayOrder: 0 })
              setMessage('گزینه تغییر سفارش با موفقیت ذخیره شد.')
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
              <h3>{modifierForm.id ? 'ویرایش تغییر سفارش' : 'تغییر سفارش جدید'}</h3>
              <p>این گزینه‌ها برای مشتری در منوی دیجیتال قابل انتخاب هستند.</p>
            </div>
            <button
              type="button"
              className="ghost-button"
              onClick={() => setModifierForm({ ...emptyModifierForm, displayOrder: 0 })}
            >
              پاک کردن فرم
            </button>
          </div>
          <div className="field-grid">
            <label>
              <span>عنوان فارسی</span>
              <input
                value={modifierForm.persianName}
                onChange={(event) =>
                  setModifierForm((currentForm) => ({
                    ...currentForm,
                    persianName: event.target.value,
                  }))
                }
              />
            </label>
            <label>
              <span>عنوان انگلیسی</span>
              <input
                className="ltr-input"
                value={modifierForm.englishName}
                onChange={(event) =>
                  setModifierForm((currentForm) => ({
                    ...currentForm,
                    englishName: event.target.value,
                  }))
                }
              />
            </label>
          </div>
          <label>
            <span>ترتیب نمایش</span>
            <input
              className="ltr-input"
              type="number"
              min="0"
              value={modifierForm.displayOrder}
              onChange={(event) =>
                setModifierForm((currentForm) => ({
                  ...currentForm,
                  displayOrder: Number(event.target.value),
                }))
              }
            />
          </label>
          <label className="checkbox-row">
            <input
              type="checkbox"
              checked={modifierForm.isActive}
              onChange={(event) =>
                setModifierForm((currentForm) => ({
                  ...currentForm,
                  isActive: event.target.checked,
                }))
              }
            />
            <span>برای مشتری نمایش داده شود</span>
          </label>
          <button type="submit" disabled={saving}>
            {saving ? 'در حال ذخیره...' : 'ذخیره تغییر سفارش'}
          </button>
        </form>
    
        <section className="panel list-panel">
          <div className="panel-head">
            <div>
              <h3>گزینه‌های تغییر سفارش</h3>
              <p>بارگذاری صفحه‌بندی شده از سرور</p>
            </div>
          </div>
          <PaginatedAdminTable
            apiPath="/admin/modifiers"
            token={session.token}
            columns={modifierColumns}
            enabled
            reloadToken={tableReloadToken}
            searchPlaceholder="جستجو در تغییرات سفارش..."
          />
        </section>
      </section>
  )
}
