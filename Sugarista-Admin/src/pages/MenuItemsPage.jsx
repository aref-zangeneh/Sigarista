import { useCallback, useEffect, useMemo, useState } from 'react'
import { useAdmin } from '../context/AdminContext'
import { PaginatedAdminTable } from '../components/PaginatedAdminTable'
import { DataTable, formatMoney } from '../components/DataTable'
import { buildMenuItemPricingColumns } from '../components/adminTableColumns'

export function MenuItemsPage() {
  const {
    session,
    categoryOptions,
    tableReloadToken,
    menuItemForm,
    setMenuItemForm,
    saving,
    handleMenuItemSubmit,
    resetMenuItemForm,
    menuItemColumns,
    apiRequest,
    getReadableError,
    setMessage,
    setError,
    saving: sharedSaving,
    setSaving,
    toPersianNumber,
  } = useAdmin()

  const [pricingItems, setPricingItems] = useState([])
  const [pricingLoading, setPricingLoading] = useState(true)
  const [markupByItem, setMarkupByItem] = useState({})
  const [bulkMarkup, setBulkMarkup] = useState('30')

  const loadPricing = useCallback(async () => {
    if (!session?.token) return
    try {
      const data = await apiRequest('/admin/menu-items/pricing', { token: session.token })
      setPricingItems(Array.isArray(data) ? data : [])
    } catch (requestError) {
      setError(getReadableError(requestError))
    } finally {
      setPricingLoading(false)
    }
  }, [session?.token, apiRequest])

  useEffect(() => {
    loadPricing()
  }, [loadPricing])

  useEffect(() => {
    loadPricing()
  }, [tableReloadToken])

  const applyPricing = useCallback(
    async ({ itemIds, markupPercent }) => {
      setSaving(true)
      setError('')
      setMessage('')
      try {
        const computed = markupPercent === '' || markupPercent == null ? 0 : Number(markupPercent)
        if (Number.isNaN(computed) || computed < 0) {
          setError('درصد سود باید عددی بزرگ‌تر یا مساوی صفر باشد.')
          return
        }

        const updated = await apiRequest('/admin/menu-items/apply-pricing', {
          method: 'POST',
          token: session.token,
          body: {
            markupPercent: computed,
            itemIds: itemIds ?? [],
          },
        })

        const count = Array.isArray(updated) ? updated.length : 0
        setMessage(
          count > 0
            ? `قیمت ${toPersianNumber(count)} آیتم بر اساس درصد سود ${toPersianNumber(computed)}٪ به‌روزرسانی شد.`
            : 'قلمی با فرمول قابل قیمت‌گذاری پیدا نشد. اول فرمول را در صفحه «دستور پخت» ثبت کنید.',
        )
        setMarkupByItem({})
        await loadPricing()
      } catch (requestError) {
        setError(getReadableError(requestError))
      } finally {
        setSaving(false)
      }
    },
    [session?.token, apiRequest, loadPricing, toPersianNumber, setError, setMessage, setSaving],
  )

  const handleMarkupChange = useCallback((itemId, value) => {
    setMarkupByItem((current) => ({ ...current, [itemId]: value }))
  }, [])

  const pricingColumns = useMemo(
    () =>
      buildMenuItemPricingColumns({
        toPersianNumber,
        formatMoney,
        markupByItem,
        onMarkupChange: handleMarkupChange,
        onApply: ({ id, markupPercent }) => applyPricing({ itemIds: [id], markupPercent }),
        bulkMarkup,
      }),
    [markupByItem, handleMarkupChange, applyPricing, bulkMarkup, toPersianNumber],
  )

  return (
      <section className="two-column-layout">
        <form className="panel form-panel" onSubmit={handleMenuItemSubmit}>
          <div className="panel-head">
            <div>
              <h3>{menuItemForm.id ? 'ویرایش آیتم منو' : 'آیتم جدید منو'}</h3>
              <p>آیتم‌های منو هم در بخش پرفروش‌ها و هم در منوی عمومی نمایش داده می‌شوند.</p>
            </div>
            <button type="button" className="ghost-button" onClick={resetMenuItemForm}>
              پاک کردن فرم
            </button>
          </div>
          <label>
            <span>دسته‌بندی</span>
            <select
              value={menuItemForm.categoryId}
              onChange={(event) =>
                setMenuItemForm((currentForm) => ({
                  ...currentForm,
                  categoryId: event.target.value,
                }))
              }
            >
              <option value="">یک دسته‌بندی انتخاب کنید</option>
              {categoryOptions.map((category) => (
                <option key={category.id} value={category.id}>
                  {category.name}
                </option>
              ))}
            </select>
          </label>
          <div className="form-section">
            <h4>هویت آیتم</h4>
            <div className="field-grid">
              <label>
                <span>نام فارسی</span>
                <input
                  value={menuItemForm.persianName}
                  onChange={(event) =>
                    setMenuItemForm((currentForm) => ({
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
                  value={menuItemForm.englishName}
                  onChange={(event) =>
                    setMenuItemForm((currentForm) => ({
                      ...currentForm,
                      englishName: event.target.value,
                    }))
                  }
                />
              </label>
            </div>
          </div>
          <label>
            <span>توضیحات</span>
            <textarea
              rows="4"
              value={menuItemForm.description}
              onChange={(event) =>
                setMenuItemForm((currentForm) => ({
                  ...currentForm,
                  description: event.target.value,
                }))
              }
            />
          </label>
          <div className="form-section">
            <h4>تصاویر</h4>
            <label>
              <span>آدرس تصویر اصلی</span>
              <input
                className="ltr-input"
                type="url"
                value={menuItemForm.imageUrl}
                onChange={(event) =>
                  setMenuItemForm((currentForm) => ({
                    ...currentForm,
                    imageUrl: event.target.value,
                  }))
                }
              />
            </label>
            <label>
              <span>گالری تصاویر (هر آدرس در یک خط)</span>
              <textarea
                className="ltr-input"
                rows="3"
                value={menuItemForm.galleryImageUrls}
                onChange={(event) =>
                  setMenuItemForm((currentForm) => ({
                    ...currentForm,
                    galleryImageUrls: event.target.value,
                  }))
                }
              />
            </label>
          </div>
    
          <div className="form-section">
            <h4>قیمت‌گذاری و سایزها</h4>
            <div className="variant-grid">
              {[
                ['small', 'کوچک'],
                ['medium', 'متوسط'],
                ['large', 'بزرگ'],
              ].map(([variantKey, variantLabel]) => (
                <div key={variantKey} className="variant-card">
                  <strong>{variantLabel}</strong>
                  <label>
                    <span>قیمت</span>
                    <input
                      className="ltr-input"
                      type="number"
                      min="0"
                      value={menuItemForm[`${variantKey}Price`]}
                      onChange={(event) =>
                        setMenuItemForm((currentForm) => ({
                          ...currentForm,
                          [`${variantKey}Price`]: event.target.value,
                        }))
                      }
                    />
                  </label>
                  <label>
                    <span>قیمت پیشنهادی</span>
                    <input
                      className="ltr-input"
                      type="number"
                      min="0"
                      value={menuItemForm[`${variantKey}SpecialOfferPrice`]}
                      onChange={(event) =>
                        setMenuItemForm((currentForm) => ({
                          ...currentForm,
                          [`${variantKey}SpecialOfferPrice`]: event.target.value,
                        }))
                      }
                    />
                  </label>
                  <label>
                    <span>حجم همین سایز</span>
                    <input
                      value={menuItemForm[`${variantKey}Volume`]}
                      onChange={(event) =>
                        setMenuItemForm((currentForm) => ({
                          ...currentForm,
                          [`${variantKey}Volume`]: event.target.value,
                        }))
                      }
                    />
                  </label>
                </div>
              ))}
            </div>
            <div className="field-grid">
              <label>
                <span>قیمت پیشنهادی عمومی</span>
                <input
                  className="ltr-input"
                  type="number"
                  min="0"
                  value={menuItemForm.specialOfferPrice}
                  onChange={(event) =>
                    setMenuItemForm((currentForm) => ({
                      ...currentForm,
                      specialOfferPrice: event.target.value,
                    }))
                  }
                />
              </label>
              <label>
                <span>درصد مالیات</span>
                <input
                  className="ltr-input"
                  type="number"
                  min="0"
                  step="0.01"
                  value={menuItemForm.taxPercent}
                  onChange={(event) =>
                    setMenuItemForm((currentForm) => ({
                      ...currentForm,
                      taxPercent: event.target.value,
                    }))
                  }
                />
              </label>
            </div>
          </div>
    
          <div className="form-section">
            <h4>جزئیات محصول</h4>
            <div className="field-grid">
              <label>
                <span>زمان آماده‌سازی (دقیقه)</span>
                <input
                  className="ltr-input"
                  type="number"
                  min="0"
                  value={menuItemForm.preparingDurationMinutes}
                  onChange={(event) =>
                    setMenuItemForm((currentForm) => ({
                      ...currentForm,
                      preparingDurationMinutes: event.target.value,
                    }))
                  }
                />
              </label>
              <label>
                <span>کالری</span>
                <input
                  className="ltr-input"
                  type="number"
                  min="0"
                  value={menuItemForm.calories}
                  onChange={(event) =>
                    setMenuItemForm((currentForm) => ({
                      ...currentForm,
                      calories: event.target.value,
                    }))
                  }
                />
              </label>
              <label>
                <span>حجم عمومی</span>
                <input
                  value={menuItemForm.volume}
                  onChange={(event) =>
                    setMenuItemForm((currentForm) => ({
                      ...currentForm,
                      volume: event.target.value,
                    }))
                  }
                />
              </label>
              <label>
                <span>آلرژن‌ها (با کاما یا خط جدید جدا کنید)</span>
                <textarea
                  rows="3"
                  value={menuItemForm.allergens}
                  onChange={(event) =>
                    setMenuItemForm((currentForm) => ({
                      ...currentForm,
                      allergens: event.target.value,
                    }))
                  }
                />
              </label>
            </div>
          </div>
    
          <div className="form-section">
            <h4>نمایش و وضعیت</h4>
            <label>
              <span>برچسب‌ها (با کاما جدا کنید)</span>
              <input
                value={menuItemForm.tags}
                onChange={(event) =>
                  setMenuItemForm((currentForm) => ({ ...currentForm, tags: event.target.value }))
                }
              />
            </label>
            <div className="field-grid">
              <label>
                <span>نشان ویژه</span>
                <input
                  value={menuItemForm.badge}
                  onChange={(event) =>
                    setMenuItemForm((currentForm) => ({ ...currentForm, badge: event.target.value }))
                  }
                />
              </label>
              <label>
                <span>حروف کوتاه</span>
                <input
                  className="ltr-input"
                  value={menuItemForm.shortLabel}
                  onChange={(event) =>
                    setMenuItemForm((currentForm) => ({
                      ...currentForm,
                      shortLabel: event.target.value,
                    }))
                  }
                />
              </label>
              <label>
                <span>ترتیب نمایش</span>
                <input
                  className="ltr-input"
                  type="number"
                  value={menuItemForm.displayOrder}
                  onChange={(event) =>
                    setMenuItemForm((currentForm) => ({
                      ...currentForm,
                      displayOrder: Number(event.target.value),
                    }))
                  }
                />
              </label>
              <label>
                <span>گرادینت پس‌زمینه</span>
                <input
                  className="ltr-input"
                  value={menuItemForm.accent}
                  onChange={(event) =>
                    setMenuItemForm((currentForm) => ({ ...currentForm, accent: event.target.value }))
                  }
                />
              </label>
            </div>
            <div className="field-grid compact-grid">
              <label className="checkbox-row">
                <input
                  type="checkbox"
                  checked={menuItemForm.featured}
                  onChange={(event) =>
                    setMenuItemForm((currentForm) => ({
                      ...currentForm,
                      featured: event.target.checked,
                    }))
                  }
                />
                <span>در بخش ویژه نمایش داده شود</span>
              </label>
              <label className="checkbox-row">
                <input
                  type="checkbox"
                  checked={menuItemForm.isActive}
                  onChange={(event) =>
                    setMenuItemForm((currentForm) => ({
                      ...currentForm,
                      isActive: event.target.checked,
                    }))
                  }
                />
                <span>قابل نمایش باشد</span>
              </label>
              <label className="checkbox-row">
                <input
                  type="checkbox"
                  checked={menuItemForm.isOrderable}
                  onChange={(event) =>
                    setMenuItemForm((currentForm) => ({
                      ...currentForm,
                      isOrderable: event.target.checked,
                    }))
                  }
                />
                <span>قابل سفارش باشد</span>
              </label>
            </div>
          </div>
          <button type="submit" disabled={saving || !categoryOptions.length}>
            {saving ? 'در حال ذخیره...' : 'ذخیره آیتم منو'}
          </button>
        </form>
    
        <section className="panel list-panel">
          <div className="panel-head">
            <div>
              <h3>آیتم‌های موجود منو</h3>
              <p>بارگذاری صفحه‌بندی شده از سرور</p>
            </div>
          </div>
          <PaginatedAdminTable
            apiPath="/admin/menu-items"
            token={session.token}
            columns={menuItemColumns}
            enabled
            reloadToken={tableReloadToken}
            searchPlaceholder="جستجو در آیتم‌های منو..."
          />
        </section>

        <section className="panel pricing-panel">
          <div className="panel-head">
            <div>
              <h3>قیمت‌گذاری خودکار روی قیمت تمام‌شده</h3>
              <p>
                قیمت تمام‌شده از فرمول (مواد اولیه + بسته‌بندی) با آخرین بهای خرید محاسبه می‌شود؛ درصد سود را بدهید تا قیمت
                نهایی انواع سایز خودکار محاسبه و ثبت شود. قیمت نهایی در منوی دیجیتال همان‌جا نمایش داده می‌شود.
              </p>
            </div>
            <div className="pricing-bulk-controls">
              <label>
                <span>درصد سود گروهی (٪)</span>
                <input
                  className="ltr-input"
                  type="number"
                  min="0"
                  step="1"
                  value={bulkMarkup}
                  onChange={(event) => setBulkMarkup(event.target.value)}
                />
              </label>
              <button
                type="button"
                className="primary-button"
                disabled={sharedSaving || pricingLoading}
                onClick={() => applyPricing({ itemIds: null, markupPercent: bulkMarkup })}
              >
                اعمال روی همه
              </button>
            </div>
          </div>
          {pricingLoading ? (
            <p className="feedback">در حال محاسبه قیمت تمام‌شده آیتم‌ها...</p>
          ) : (
            <DataTable
              data={pricingItems}
              columns={pricingColumns}
              searchPlaceholder="جستجو در قیمت‌گذاری..."
            />
          )}
        </section>
      </section>
  )
}
