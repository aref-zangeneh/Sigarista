import { useAdmin } from '../context/AdminContext'
import { PaginatedAdminTable } from '../components/PaginatedAdminTable'
import { ImageUploader } from '../components/ImageUploader'

export function CategoryPage() {
  const {
    session,
    tableReloadToken,
    categoryForm,
    setCategoryForm,
    saving,
    handleCategorySubmit,
    resetCategoryForm,
    categoryColumns,
  } = useAdmin()

  return (
      <section className="two-column-layout">
        <form className="panel form-panel" onSubmit={handleCategorySubmit}>
          <div className="panel-head">
            <div>
              <h3>{categoryForm.id ? 'ویرایش دسته‌بندی' : 'دسته‌بندی جدید'}</h3>
              <p>دسته‌بندی‌ها فیلترها و گروه‌بندی منوی عمومی را کنترل می‌کنند.</p>
            </div>
            <button type="button" className="ghost-button" onClick={resetCategoryForm}>
              پاک کردن فرم
            </button>
          </div>
          <label>
            <span>اسلاگ انگلیسی</span>
            <input
              className="ltr-input"
              value={categoryForm.slug}
              onChange={(event) =>
                setCategoryForm((currentForm) => ({ ...currentForm, slug: event.target.value }))
              }
            />
          </label>
          <label>
            <span>نام دسته‌بندی</span>
            <input
              value={categoryForm.name}
              onChange={(event) =>
                setCategoryForm((currentForm) => ({ ...currentForm, name: event.target.value }))
              }
            />
          </label>
          <div className="form-section">
            <h4>تصویر دسته‌بندی</h4>
            <ImageUploader
              label="عکس دسته‌بندی"
              hint="در منوی مشتری، کنار نام دسته‌بندی نمایش داده میشود — عکس مربعی و تمیز بهترین نتیجه را میدهد (اختیاری)"
              value={categoryForm.imageUrl ?? ''}
              token={session?.token}
              onChange={(url) =>
                setCategoryForm((currentForm) => ({
                  ...currentForm,
                  imageUrl: url ?? '',
                }))
              }
            />
          </div>
          <label>
            <span>ترتیب نمایش</span>
            <input
              className="ltr-input"
              type="number"
              value={categoryForm.displayOrder}
              onChange={(event) =>
                setCategoryForm((currentForm) => ({
                  ...currentForm,
                  displayOrder: Number(event.target.value),
                }))
              }
            />
          </label>
          <label className="checkbox-row">
            <input
              type="checkbox"
              checked={categoryForm.isActive}
              onChange={(event) =>
                setCategoryForm((currentForm) => ({
                  ...currentForm,
                  isActive: event.target.checked,
                }))
              }
            />
            <span>دسته‌بندی فعال باشد</span>
          </label>
          <button type="submit" disabled={saving}>
            {saving ? 'در حال ذخیره...' : 'ذخیره دسته‌بندی'}
          </button>
        </form>
    
        <section className="panel list-panel">
          <div className="panel-head">
            <div>
              <h3>دسته‌بندی‌های موجود</h3>
              <p>بارگذاری صفحه‌بندی شده از سرور</p>
            </div>
          </div>
          <PaginatedAdminTable
            apiPath="/admin/categories"
            token={session.token}
            columns={categoryColumns}
            enabled
            reloadToken={tableReloadToken}
            searchPlaceholder="جستجو در دسته‌بندی‌ها..."
          />
        </section>
      </section>
  )
}
