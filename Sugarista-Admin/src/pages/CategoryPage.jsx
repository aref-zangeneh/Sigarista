import { useEffect, useRef, useState } from 'react'
import { useAdmin } from '../context/AdminContext'
import { PaginatedAdminTable } from '../components/PaginatedAdminTable'
import { ImageUploader } from '../components/ImageUploader'
import { GeminiDraftButton } from '../components/GeminiDraftButton.jsx'
import { hasPermission, PERMISSIONS } from '../utils/adminPermissions'
import './CategoryPage.css'

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

  const canCreate = hasPermission(session, PERMISSIONS.categories.create)
  const [modalOpen, setModalOpen] = useState(false)
  const prevReloadTokenRef = useRef(tableReloadToken)

  // Open modal automatically when the user clicks "ویرایش" on a table row
  useEffect(() => {
    if (categoryForm?.id) {
      setModalOpen(true)
    }
  }, [categoryForm?.id])

  // Close modal automatically after a successful save reloads the table
  useEffect(() => {
    if (tableReloadToken !== prevReloadTokenRef.current) {
      prevReloadTokenRef.current = tableReloadToken
      setModalOpen(false)
    }
  }, [tableReloadToken])

  // Close modal on Escape key
  useEffect(() => {
    if (!modalOpen) return undefined
    const onKeyDown = (event) => {
      if (event.key === 'Escape' && !saving) {
        setModalOpen(false)
        resetCategoryForm()
      }
    }
    window.addEventListener('keydown', onKeyDown)
    return () => window.removeEventListener('keydown', onKeyDown)
  }, [modalOpen, saving, resetCategoryForm])

  const openCreateModal = () => {
    resetCategoryForm()
    setModalOpen(true)
  }

  const closeModal = () => {
    if (saving) return
    setModalOpen(false)
    resetCategoryForm()
  }

  return (
    <div className="category-page">
      <section className="panel list-panel category-panel">
        <div className="panel-head category-head">
          <div>
            <h3>دسته‌بندی‌ها</h3>
            <p>فیلترها و گروه‌بندی منوی دیجیتال مشتریان</p>
          </div>
          {canCreate ? (
            <button type="button" onClick={openCreateModal}>
              + دسته‌بندی جدید
            </button>
          ) : null}
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

      {modalOpen ? (
        <div
          className="category-modal-backdrop"
          role="dialog"
          aria-modal="true"
          aria-label={categoryForm.id ? 'ویرایش دسته‌بندی' : 'دسته‌بندی جدید'}
          onClick={(event) => {
            if (event.target === event.currentTarget) closeModal()
          }}
        >
          <div className="category-modal">
            <div className="category-modal-head">
              <div>
                <h3>{categoryForm.id ? 'ویرایش دسته‌بندی' : 'دسته‌بندی جدید'}</h3>
                <p>اطلاعات دسته‌بندی را وارد کنید.</p>
              </div>
              <button
                type="button"
                className="category-modal-close"
                aria-label="بستن"
                onClick={closeModal}
              >
                ×
              </button>
            </div>

            <form className="category-modal-form" onSubmit={handleCategorySubmit}>
              <div className="category-modal-grid">
                <label>
                  <span>نام دسته‌بندی</span>
                  <input
                    autoFocus
                    required
                    value={categoryForm.name}
                    onChange={(event) =>
                      setCategoryForm((currentForm) => ({ ...currentForm, name: event.target.value }))
                    }
                  />
                </label>

                <label>
                  <span>اسلاگ انگلیسی</span>
                  <input
                    className="ltr-input"
                    required
                    value={categoryForm.slug}
                    onChange={(event) =>
                      setCategoryForm((currentForm) => ({ ...currentForm, slug: event.target.value }))
                    }
                  />
                </label>
              </div>

              <div className="category-modal-grid">
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

                <label className="checkbox-row" style={{ alignSelf: 'end', paddingBottom: '0.45rem' }}>
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
              </div>

              <div className="form-section">
                <h4>تصویر دسته‌بندی (اختیاری)</h4>
                <ImageUploader
                  label="عکس دسته‌بندی"
                  hint="عکس مربعی و تمیز در منوی مشتری کنار نام دسته‌بندی نمایش داده می‌شود."
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

              <div className="category-modal-actions">
                <GeminiDraftButton
                  section="category"
                  label="پیشنهاد اسلاگ با Gemini"
                  getPayload={() => ({
                    title: categoryForm.name || categoryForm.slug,
                  })}
                  onDraft={(draft) =>
                    setCategoryForm((cur) => ({
                      ...cur,
                      name: cur.name?.trim() ? cur.name : (draft.name ?? cur.name),
                      slug: draft.slug ?? cur.slug,
                    }))
                  }
                />
                <button type="button" className="ghost-button" onClick={closeModal} disabled={saving}>
                  انصراف
                </button>
                <button type="submit" disabled={saving}>
                  {saving ? 'در حال ذخیره...' : categoryForm.id ? 'ذخیره تغییرات' : 'ایجاد دسته‌بندی'}
                </button>
              </div>
            </form>
          </div>
        </div>
      ) : null}
    </div>
  )
}
