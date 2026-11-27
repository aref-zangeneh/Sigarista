import { useCallback, useEffect, useMemo, useRef, useState } from 'react'
import { useAdmin } from '../context/AdminContext'
import { PaginatedAdminTable } from '../components/PaginatedAdminTable'
import { DataTable, formatMoney } from '../components/DataTable'
import { ImageUploader } from '../components/ImageUploader'
import { MenuItemOptionGroupsPanel } from '../components/MenuItemOptionGroupsPanel'
import { MenuItemBundlePanel } from '../components/MenuItemBundlePanel.jsx'
import { MenuItemPriceHistoryModal } from '../components/MenuItemPriceHistoryModal.jsx'
import { NumberStepperInput } from '../components/NumberStepperInput.jsx'
import { Tip } from '../components/Tip'
import { buildMenuItemPricingColumns } from '../components/adminTableColumns'
import {
  VOLUME_UNITS,
  formatVolumeValue,
  inferDefaultVolumeUnit,
  parseVolumeValue,
} from '../utils/volumeUnit'
import './MenuSectionMinimal.css'
import './MenuItemsPage.css'

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
    formatDateTime,
    ingredientOptions,
    menuItemOptions,
    bumpTableReload,
    loadAdminData,
    priceHistoryMenuItem,
    openMenuItemPriceHistory,
    closeMenuItemPriceHistory,
  } = useAdmin()

  const [pricingItems, setPricingItems] = useState([])
  const [pricingLoading, setPricingLoading] = useState(true)
  const [markupByItem, setMarkupByItem] = useState({})
  const [manualByItem, setManualByItem] = useState({})
  const [bulkMarkup, setBulkMarkup] = useState('30')
  const [volumeUnitOverrides, setVolumeUnitOverrides] = useState({})
  const editorFormRef = useRef(null)
  const prevEditingIdRef = useRef(menuItemForm.id ?? '')

  const scrollToEditor = useCallback(() => {
    editorFormRef.current?.scrollIntoView({ behavior: 'smooth', block: 'start' })
  }, [])

  useEffect(() => {
    const currentId = menuItemForm.id ?? ''
    if (currentId && currentId !== prevEditingIdRef.current) {
      scrollToEditor()
    }
    prevEditingIdRef.current = currentId
  }, [menuItemForm.id, scrollToEditor])

  const handleStartNewItem = () => {
    resetMenuItemForm()
    scrollToEditor()
  }

  const selectedCategory = useMemo(
    () => (categoryOptions ?? []).find((cat) => cat.id === menuItemForm.categoryId),
    [categoryOptions, menuItemForm.categoryId],
  )
  const defaultVolumeUnit = useMemo(
    () =>
      inferDefaultVolumeUnit(
        [
          selectedCategory?.label,
          selectedCategory?.slug,
          menuItemForm.persianName,
          menuItemForm.englishName,
        ]
          .filter(Boolean)
          .join(' '),
      ),
    [selectedCategory, menuItemForm.persianName, menuItemForm.englishName],
  )

  useEffect(() => {
    setVolumeUnitOverrides({})
  }, [menuItemForm.id])

  const getVolumeParts = (fieldName) => {
    const raw = menuItemForm[fieldName] ?? ''
    const fallbackUnit = volumeUnitOverrides[fieldName] ?? defaultVolumeUnit
    return parseVolumeValue(raw, fallbackUnit)
  }

  const updateVolumeAmount = (fieldName, nextAmount) => {
    const { unit } = getVolumeParts(fieldName)
    setMenuItemForm((currentForm) => ({
      ...currentForm,
      [fieldName]: formatVolumeValue(nextAmount, unit),
    }))
  }

  const updateVolumeUnit = (fieldName, nextUnit) => {
    const { amount } = getVolumeParts(fieldName)
    setVolumeUnitOverrides((current) => ({ ...current, [fieldName]: nextUnit }))
    if (amount) {
      setMenuItemForm((currentForm) => ({
        ...currentForm,
        [fieldName]: formatVolumeValue(amount, nextUnit),
      }))
    }
  }

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
    async ({ itemIds, markupPercent, manualPrice }) => {
      setSaving(true)
      setError('')
      setMessage('')
      try {
        const hasManual = manualPrice !== '' && manualPrice != null && Number(manualPrice) > 0
        const computed = markupPercent === '' || markupPercent == null ? 0 : Number(markupPercent)

        if (hasManual && (Number.isNaN(Number(manualPrice)) || Number(manualPrice) <= 0)) {
          setError('قیمت دستی باید عددی بزرگ‌تر از صفر باشد.')
          return
        }

        if (!hasManual && (Number.isNaN(computed) || computed < 0)) {
          setError('درصد سود باید عددی بزرگ‌تر یا مساوی صفر باشد.')
          return
        }

        const updated = await apiRequest('/admin/menu-items/apply-pricing', {
          method: 'POST',
          token: session.token,
          body: {
            markupPercent: hasManual ? null : computed,
            itemIds: itemIds ?? [],
            manualPrice: hasManual ? Number(manualPrice) : null,
          },
        })

        const count = Array.isArray(updated) ? updated.length : 0
        setMessage(
          count > 0
            ? hasManual
              ? `قیمت ${toPersianNumber(count)} آیتم که تک‌به‌تک روی ${toPersianNumber(Number(manualPrice))} تومان تنظیم شد.`
              : `قیمت ${toPersianNumber(count)} آیتم بر اساس درصد سود ${toPersianNumber(computed)}٪ به‌روزرسانی شد.`
            : 'قلمی با فرمول قابل قیمت‌گذاری پیدا نشد. اول فرمول را در صفحه «دستور پخت» ثبت کنید.',
        )
        setMarkupByItem({})
        setManualByItem({})
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

  const addRecipeLine = () => {
    setMenuItemForm((currentForm) => ({
      ...currentForm,
      recipeLines: [
        ...(currentForm.recipeLines ?? []),
        { ingredientId: '', quantity: '', showToCustomer: true },
      ],
    }))
  }

  const updateRecipeLine = (index, patch) => {
    setMenuItemForm((currentForm) => ({
      ...currentForm,
      recipeLines: (currentForm.recipeLines ?? []).map((line, lineIndex) =>
        lineIndex === index ? { ...line, ...patch } : line),
    }))
  }

  const removeRecipeLine = (index) => {
    setMenuItemForm((currentForm) => ({
      ...currentForm,
      recipeLines: (currentForm.recipeLines ?? []).filter((_, lineIndex) => lineIndex !== index),
    }))
  }

  const handleManualChange = useCallback((itemId, value) => {
    setManualByItem((current) => ({ ...current, [itemId]: value }))
  }, [])

  const pricingColumns = useMemo(
    () =>
      buildMenuItemPricingColumns({
        toPersianNumber,
        formatMoney,
        markupByItem,
        onMarkupChange: handleMarkupChange,
        onApplyMarkup: ({ id, markupPercent }) => applyPricing({ itemIds: [id], markupPercent }),
        manualByItem,
        onManualChange: handleManualChange,
        onApplyManual: ({ id, manualPrice }) => applyPricing({ itemIds: [id], manualPrice }),
      }),
    [markupByItem, handleMarkupChange, manualByItem, handleManualChange, applyPricing, toPersianNumber],
  )

  return (
      <section className="mi-page menu-minimal-scope">
        <section className="panel mi-list-panel">
          <div className="panel-head">
            <div>
              <h3>آیتم‌های موجود منو</h3>
              <p>لیست کامل محصولات منو با جستجو، فیلتر و صفحه‌بندی مستقیم از سرور</p>
            </div>
            <div className="mi-head-actions">
              <button
                type="button"
                className="primary-button"
                onClick={handleStartNewItem}
              >
                + افزودن آیتم جدید
              </button>
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

        <form ref={editorFormRef} className="panel mi-editor-panel" onSubmit={handleMenuItemSubmit}>
          <div className="panel-head">
            <div>
              <div className="mi-editor-title-wrap">
                <h3>{menuItemForm.id ? 'ویرایش آیتم منو' : 'افزودن آیتم جدید منو'}</h3>
                <span className={`mi-mode-pill ${menuItemForm.id ? 'is-editing' : ''}`}>
                  {menuItemForm.id ? 'در حال ویرایش' : 'آیتم جدید'}
                </span>
              </div>
              <p>آیتم‌های منو هم در بخش پرفروش‌ها و هم در منوی عمومی نمایش داده می‌شوند.</p>
            </div>
            <div className="mi-head-actions">
              <button type="button" className="ghost-button" onClick={resetMenuItemForm}>
                پاک کردن فرم
              </button>
            </div>
          </div>

          <div className="form-section">
            <div className="mi-section-header">
              <h4>هویت و دسته‌بندی</h4>
              <span>اطلاعات اصلی نمایش در منوی دیجیتال</span>
            </div>
            <div className="mi-grid-3">
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
            <label>
              <span>توضیحات</span>
              <textarea
                rows="3"
                value={menuItemForm.description}
                onChange={(event) =>
                  setMenuItemForm((currentForm) => ({
                    ...currentForm,
                    description: event.target.value,
                  }))
                }
              />
            </label>
          </div>

          <div className="form-section">
            <div className="mi-section-header">
              <h4>تصاویر محصول</h4>
              <span>تصویر کاور مربعی و گالری تصاویر تکمیلی</span>
            </div>
            <div className="mi-grid-2">
              <ImageUploader
                label="تصویر اصلی"
                hint="برای نمایش در منوی مشتری — طبق طراحی، تصویر مربعی بهترین نتیجه را می‌دهد"
                value={menuItemForm.imageUrl ?? ''}
                token={session?.token}
                onChange={(url) =>
                  setMenuItemForm((currentForm) => ({
                    ...currentForm,
                    imageUrl: url ?? '',
                  }))
                }
              />
              <ImageUploader
                label="گالری تصاویر"
                hint="با کشیدن فایل، هر تعداد عکس اضافه کنید؛ ترتیب با فلش‌ها قابل جابه‌جایی است"
                multiple
                allowUrl={false}
                value={(menuItemForm.galleryImageUrls ?? '')
                  .split('\n')
                  .map((line) => line.trim())
                  .filter(Boolean)}
                token={session?.token}
                onChange={(urls) =>
                  setMenuItemForm((currentForm) => ({
                    ...currentForm,
                    galleryImageUrls: (urls ?? []).join('\n'),
                  }))
                }
              />
            </div>
          </div>

          <div className="form-section">
            <div className="mi-section-header">
              <h4>قیمت‌گذاری، موجودی و سایزها</h4>
              <span>قیمت پایه یا قیمت تفکیکی بر اساس سایز</span>
            </div>
            <div className="mi-base-price-grid">
              <label>
                <span>قیمت فروش پایه (آیتم بدون سایز)</span>
                <input
                  className="ltr-input"
                  type="number"
                  min="0"
                  placeholder="مثلاً چیزکیک: ۲۰۰۰۰۰"
                  value={menuItemForm.price ?? ''}
                  onChange={(event) =>
                    setMenuItemForm((currentForm) => ({
                      ...currentForm,
                      price: event.target.value,
                    }))
                  }
                />
                <Tip k="menu-items.base_price" />
              </label>
              <label>
                <span>قیمت خرید / بهای تمام‌شده (تومان — اختیاری)</span>
                <input
                  className="ltr-input"
                  type="number"
                  min="0"
                  placeholder="مثلاً کیک خریداری‌شده: ۱۰۰۰۰۰"
                  value={menuItemForm.purchasePrice ?? ''}
                  onChange={(event) =>
                    setMenuItemForm((currentForm) => ({
                      ...currentForm,
                      purchasePrice: event.target.value,
                    }))
                  }
                />
              </label>
            </div>
            {(() => {
              const saleVal =
                Number(menuItemForm.price) ||
                Number(menuItemForm.mediumPrice) ||
                Number(menuItemForm.smallPrice) ||
                Number(menuItemForm.largePrice) ||
                0
              const buyVal = Number(menuItemForm.purchasePrice)
              const hasBuy = menuItemForm.purchasePrice !== '' && Number.isFinite(buyVal) && buyVal >= 0
              const profit = hasBuy && saleVal > 0 ? saleVal - buyVal : null
              const marginPct = profit != null && saleVal > 0 ? Math.round((profit / saleVal) * 1000) / 10 : null
              const markupPct = profit != null && buyVal > 0 ? Math.round((profit / buyVal) * 1000) / 10 : null

              return (
                <>
                  {hasBuy && saleVal > 0 ? (
                    <div className={`price-profit-banner ${profit <= 0 ? 'is-warning' : ''}`}>
                      <div>
                        <strong>
                          سود خالص هر عدد: {formatMoney(profit)}
                        </strong>
                        <span>
                          حاشیه سود فروش: {toPersianNumber(marginPct)}٪
                          {markupPct != null ? ` • درصد سود روی خرید: ${toPersianNumber(markupPct)}٪` : ''}
                        </span>
                      </div>
                      {menuItemForm.id ? (
                        <button
                          type="button"
                          className="link-button"
                          onClick={() =>
                            openMenuItemPriceHistory({
                              id: menuItemForm.id,
                              persianName: menuItemForm.persianName,
                              englishName: menuItemForm.englishName,
                            })
                          }
                        >
                          مشاهده لاگ و روند قیمت
                        </button>
                      ) : null}
                    </div>
                  ) : menuItemForm.id ? (
                    <div className="option-groups-actions">
                      <button
                        type="button"
                        className="link-button"
                        onClick={() =>
                          openMenuItemPriceHistory({
                            id: menuItemForm.id,
                            persianName: menuItemForm.persianName,
                            englishName: menuItemForm.englishName,
                          })
                        }
                      >
                        مشاهده لاگ و روند تغییرات قیمت
                      </button>
                    </div>
                  ) : null}

                  <label>
                    <span>یادداشت تغییر قیمت (اختیاری — در لاگ تاریخچه قیمت ثبت می‌شود)</span>
                    <input
                      maxLength="500"
                      placeholder="مثلاً افزایش قیمت خرید از قنادی به ۱۲۰ هزار تومان"
                      value={menuItemForm.priceChangeNote ?? ''}
                      onChange={(event) =>
                        setMenuItemForm((currentForm) => ({
                          ...currentForm,
                          priceChangeNote: event.target.value,
                        }))
                      }
                    />
                  </label>
                </>
              )
            })()}

            <div className="inventory-track-box">
              <label className="checkbox-row">
                <input
                  type="checkbox"
                  checked={Boolean(menuItemForm.trackInventory)}
                  onChange={(event) =>
                    setMenuItemForm((currentForm) => ({
                      ...currentForm,
                      trackInventory: event.target.checked,
                      stockQuantity:
                        event.target.checked && currentForm.stockQuantity === ''
                          ? 0
                          : currentForm.stockQuantity,
                      minStockAlert:
                        event.target.checked && currentForm.minStockAlert === ''
                          ? 2
                          : currentForm.minStockAlert,
                    }))
                  }
                />
                <span>
                  <strong>مدیریت تعداد موجودی برای این آیتم فعال باشد</strong>{' '}
                  (مناسب کیک، دسر و کالاهای آماده — با هر فروش خودکار کسر و هنگام اتمام غیرقابل سفارش می‌شود)
                </span>
              </label>

              {menuItemForm.trackInventory ? (
                <div className="mi-grid-2">
                  <label>
                    <span>تعداد موجودی فعلی</span>
                    <NumberStepperInput
                      min={0}
                      value={menuItemForm.stockQuantity ?? 0}
                      ariaLabel="تعداد موجودی فعلی"
                      onChange={(nextValue) =>
                        setMenuItemForm((currentForm) => ({
                          ...currentForm,
                          stockQuantity: nextValue,
                        }))
                      }
                    />
                  </label>
                  <label>
                    <span>حداقل موجودی هشدار (موجودی کم)</span>
                    <NumberStepperInput
                      min={0}
                      value={menuItemForm.minStockAlert ?? 0}
                      ariaLabel="حداقل موجودی هشدار"
                      onChange={(nextValue) =>
                        setMenuItemForm((currentForm) => ({
                          ...currentForm,
                          minStockAlert: nextValue,
                        }))
                      }
                    />
                  </label>
                </div>
              ) : null}
            </div>

            <div className="mi-variant-grid">
              {[
                ['small', 'کوچک'],
                ['medium', 'متوسط'],
                ['large', 'بزرگ'],
              ].map(([variantKey, variantLabel]) => (
                <div key={variantKey} className="variant-card">
                  <div className="variant-card-head">
                    <strong>{menuItemForm[`${variantKey}Name`]?.trim() || variantLabel}</strong>
                    <span>سایز {variantLabel}</span>
                  </div>
                  <label>
                    <span>نام نمایشی سایز (اختیاری)</span>
                    <input
                      placeholder={variantLabel}
                      maxLength="60"
                      value={menuItemForm[`${variantKey}Name`] ?? ''}
                      onChange={(event) =>
                        setMenuItemForm((currentForm) => ({
                          ...currentForm,
                          [`${variantKey}Name`]: event.target.value,
                        }))
                      }
                    />
                  </label>
                  <div className="mi-grid-2">
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
                  </div>
                  <label>
                    <span>حجم / وزن همین سایز</span>
                    <div className="volume-input-group">
                      <input
                        className="ltr-input"
                        type="number"
                        min="0"
                        step="any"
                        placeholder="مثلاً ۳۶۰"
                        value={getVolumeParts(`${variantKey}Volume`).amount}
                        onChange={(event) =>
                          updateVolumeAmount(`${variantKey}Volume`, event.target.value)
                        }
                      />
                      <select
                        aria-label="واحد حجم یا وزن سایز"
                        value={getVolumeParts(`${variantKey}Volume`).unit}
                        onChange={(event) =>
                          updateVolumeUnit(`${variantKey}Volume`, event.target.value)
                        }
                      >
                        {VOLUME_UNITS.map((unitOption) => (
                          <option key={unitOption} value={unitOption}>
                            {unitOption}
                          </option>
                        ))}
                      </select>
                    </div>
                  </label>
                </div>
              ))}
            </div>

            <div className="mi-grid-2">
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

          <div className="mi-split-sections">
            <div className="form-section">
              <div className="mi-section-header">
                <h4>جزئیات محصول</h4>
                <span>مشخصات تغذیه‌ای و آماده‌سازی</span>
              </div>
              <div className="mi-grid-2">
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
              </div>
              <label>
                <span>حجم / وزن عمومی</span>
                <div className="volume-input-group">
                  <input
                    className="ltr-input"
                    type="number"
                    min="0"
                    step="any"
                    placeholder="مثلاً ۳۶۰"
                    value={getVolumeParts('volume').amount}
                    onChange={(event) => updateVolumeAmount('volume', event.target.value)}
                  />
                  <select
                    aria-label="واحد حجم یا وزن عمومی"
                    value={getVolumeParts('volume').unit}
                    onChange={(event) => updateVolumeUnit('volume', event.target.value)}
                  >
                    {VOLUME_UNITS.map((unitOption) => (
                      <option key={unitOption} value={unitOption}>
                        {unitOption}
                      </option>
                    ))}
                  </select>
                </div>
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

            <div className="form-section">
              <div className="mi-section-header">
                <h4>نمایش و وضعیت</h4>
                <span>تنظیمات انتشار در منو و کارت محصول</span>
              </div>
              <label>
                <span>برچسب‌ها (با کاما جدا کنید)</span>
                <input
                  value={menuItemForm.tags}
                  onChange={(event) =>
                    setMenuItemForm((currentForm) => ({ ...currentForm, tags: event.target.value }))
                  }
                />
              </label>
              <div className="mi-grid-2">
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
              <div className="mi-toggles-row">
                <label className="mi-toggle-chip">
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
                <label className="mi-toggle-chip">
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
                <label className="mi-toggle-chip">
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
          </div>

          <div className="form-section">
            <div className="mi-section-header">
              <h4>محتویات (مواد اولیه)</h4>
              <span>مبنای کسر از انبار، نمایش به مشتری و محاسبه بهای تمام‌شده</span>
            </div>
            <p className="form-hint">
              مواد تشکیل‌دهندهٔ آیتم را از میان مواد اولیه انتخاب کنید. مواردی که «نمایش به مشتری» دارند
              در منوی دیجیتال به مشتری نشان داده می‌شوند؛ مقدار مصرف هم مبنای کسر از انبار و قیمت‌گذاری خودکار است.
            </p>
            {(menuItemForm.recipeLines ?? []).length === 0 ? (
              <p className="form-hint">هنوز ماده‌ای انتخاب نشده است.</p>
            ) : (
              <div className="mi-recipe-list">
                {(menuItemForm.recipeLines ?? []).map((line, index) => {
                  const selectedOption = (ingredientOptions ?? []).find(
                    (option) => option.id === line.ingredientId,
                  )
                  const pickableOptions = (ingredientOptions ?? []).filter(
                    (option) =>
                      option.kind === 'ingredient' &&
                      (option.isActive || option.id === line.ingredientId),
                  )

                  return (
                    <div key={index} className="recipe-line-row">
                      {selectedOption || !line.ingredientId ? (
                        <select
                          value={line.ingredientId}
                          onChange={(event) =>
                            updateRecipeLine(index, { ingredientId: event.target.value })
                          }
                        >
                          <option value="">-- انتخاب ماده --</option>
                          {pickableOptions.map((option) => (
                            <option key={option.id} value={option.id}>
                              {option.persianName} ({option.unit})
                            </option>
                          ))}
                        </select>
                      ) : (
                        <span className="recipe-line-static" title="این قلم در صفحه دستور پخت ثبت شده است">
                          {line.ingredientPersianName || 'قلم ثبت‌شدهٔ قبلی'}
                        </span>
                      )}
                      <input
                        className="ltr-input recipe-line-qty"
                        type="number"
                        min="0.001"
                        step="0.001"
                        placeholder="مقدار"
                        title="مقدار مصرف در هر عدد آیتم"
                        value={line.quantity ?? ''}
                        onChange={(event) =>
                          updateRecipeLine(index, { quantity: event.target.value })
                        }
                      />
                      <label className="recipe-line-show">
                        <input
                          type="checkbox"
                          checked={line.showToCustomer !== false}
                          onChange={(event) =>
                            updateRecipeLine(index, { showToCustomer: event.target.checked })
                          }
                        />
                        <span>نمایش به مشتری</span>
                      </label>
                      <button
                        type="button"
                        className="recipe-line-remove"
                        title="حذف این ماده"
                        onClick={() => removeRecipeLine(index)}
                      >
                        −
                      </button>
                    </div>
                  )
                })}
              </div>
            )}
            <div className="mi-recipe-footer">
              <button type="button" className="ghost-button recipe-line-add" onClick={addRecipeLine}>
                + افزودن ماده
              </button>
            </div>
          </div>

          <div className="mi-form-footer">
            <span className="mi-form-footer-hint">
              {menuItemForm.id
                ? 'تغییرات پس از ذخیره بلافاصله در منوی دیجیتال و صندوق اعمال می‌شوند.'
                : 'پس از تکمیل اطلاعات بالا، روی ذخیره بزنید تا آیتم به منو اضافه شود.'}
            </span>
            <div className="mi-form-footer-actions">
              <button type="button" className="ghost-button" onClick={resetMenuItemForm}>
                پاک کردن فرم
              </button>
              <button type="submit" className="primary-button" disabled={saving || !categoryOptions.length}>
                {saving ? 'در حال ذخیره...' : menuItemForm.id ? 'ذخیره تغییرات آیتم' : 'ذخیره آیتم منو'}
              </button>
            </div>
          </div>
        </form>

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

        <MenuItemBundlePanel
          session={session}
          apiRequest={apiRequest}
          menuItemOptions={menuItemOptions}
          getReadableError={getReadableError}
          setMessage={setMessage}
          setError={setError}
          toPersianNumber={toPersianNumber}
          formatMoney={formatMoney}
          focusMenuItemId={menuItemForm.id ?? ''}
          onBundleSaved={() => {
            if (bumpTableReload) bumpTableReload()
            if (loadAdminData) loadAdminData()
            loadPricing()
          }}
        />

        <MenuItemOptionGroupsPanel
          session={session}
          apiRequest={apiRequest}
          menuItemOptions={menuItemOptions}
          ingredientOptions={ingredientOptions}
          getReadableError={getReadableError}
          setMessage={setMessage}
          setError={setError}
          setSaving={setSaving}
          toPersianNumber={toPersianNumber}
          formatMoney={formatMoney}
          focusMenuItemId={menuItemForm.id ?? ''}
        />

        <MenuItemPriceHistoryModal
          menuItem={priceHistoryMenuItem}
          onClose={closeMenuItemPriceHistory}
          session={session}
          apiRequest={apiRequest}
          getReadableError={getReadableError}
          toPersianNumber={toPersianNumber}
          formatDateTime={formatDateTime}
        />
      </section>
  )
}
