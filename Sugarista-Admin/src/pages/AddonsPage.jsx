import { useAdmin } from '../context/AdminContext'
import { PaginatedAdminTable } from '../components/PaginatedAdminTable'

export function AddonsPage() {
  const {
    session,
    tableReloadToken,
    addonForm,
    setAddonForm,
    saving,
    setSaving,
    setMessage,
    setError,
    bumpTableReload,
    addonColumns,
    apiRequest,
    getReadableError,
    splitTags,
    emptyAddonForm,
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
              const path = addonForm.id ? `/admin/addons/${addonForm.id}` : '/admin/addons'
    
              await apiRequest(path, {
                method: addonForm.id ? 'PUT' : 'POST',
                token: session.token,
                body: {
                  persianName: addonForm.persianName,
                  englishName: addonForm.englishName,
                  description: addonForm.description,
                  price: Number(addonForm.price),
                  tags: splitTags(addonForm.tags),
                  displayOrder: Number(addonForm.displayOrder),
                  isActive: addonForm.isActive,
                },
              })
    
              setAddonForm({ ...emptyAddonForm, displayOrder: 0 })
              setMessage('افزودنی با موفقیت ذخیره شد.')
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
              <h3>{addonForm.id ? 'ویرایش افزودنی' : 'افزودنی جدید'}</h3>
              <p>افزودنی‌ها مثل شات اضافه، شربت و ... به آیتم‌های اصلی منو اضافه می‌شوند.</p>
            </div>
            <button
              type="button"
              className="ghost-button"
              onClick={() =>
                setAddonForm({
                  ...emptyAddonForm,
                  displayOrder: 0,
                })
              }
            >
              پاک کردن فرم
            </button>
          </div>
          <div className="field-grid">
            <label>
              <span>نام فارسی</span>
              <input
                value={addonForm.persianName}
                onChange={(event) =>
                  setAddonForm((currentForm) => ({
                    ...currentForm,
                    persianName: event.target.value,
                  }))
                }
              />
            </label>
            <label>
              <span>نام انگلیسی</span>
              <input
                className="ltr-input"
                value={addonForm.englishName}
                onChange={(event) =>
                  setAddonForm((currentForm) => ({
                    ...currentForm,
                    englishName: event.target.value,
                  }))
                }
              />
            </label>
          </div>
          <label>
            <span>توضیحات</span>
            <textarea
              rows="3"
              value={addonForm.description}
              onChange={(event) =>
                setAddonForm((currentForm) => ({
                  ...currentForm,
                  description: event.target.value,
                }))
              }
            />
          </label>
          <div className="field-grid">
            <label>
              <span>قیمت</span>
              <input
                className="ltr-input"
                type="number"
                min="0"
                value={addonForm.price}
                onChange={(event) =>
                  setAddonForm((currentForm) => ({
                    ...currentForm,
                    price: Number(event.target.value),
                  }))
                }
              />
            </label>
            <label>
              <span>ترتیب نمایش</span>
              <input
                className="ltr-input"
                type="number"
                min="0"
                value={addonForm.displayOrder}
                onChange={(event) =>
                  setAddonForm((currentForm) => ({
                    ...currentForm,
                    displayOrder: Number(event.target.value),
                  }))
                }
              />
            </label>
          </div>
          <label>
            <span>برچسب‌ها (با کاما جدا کنید)</span>
            <input
              value={addonForm.tags}
              onChange={(event) =>
                setAddonForm((currentForm) => ({
                  ...currentForm,
                  tags: event.target.value,
                }))
              }
            />
          </label>
          <label className="checkbox-row">
            <input
              type="checkbox"
              checked={addonForm.isActive}
              onChange={(event) =>
                setAddonForm((currentForm) => ({
                  ...currentForm,
                  isActive: event.target.checked,
                }))
              }
            />
            <span>افزودنی فعال باشد</span>
          </label>
          <button type="submit" disabled={saving}>
            {saving ? 'در حال ذخیره...' : 'ذخیره افزودنی'}
          </button>
        </form>
    
        <section className="panel list-panel">
          <div className="panel-head">
            <div>
              <h3>افزودنی‌های موجود</h3>
              <p>بارگذاری صفحه‌بندی شده از سرور</p>
            </div>
          </div>
          <PaginatedAdminTable
            apiPath="/admin/addons"
            token={session.token}
            columns={addonColumns}
            enabled
            reloadToken={tableReloadToken}
            searchPlaceholder="جستجو در افزودنی‌ها..."
          />
        </section>
      </section>
  )
}
