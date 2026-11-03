import { useState } from 'react'
import { useAdmin } from '../context/AdminContext'
import { PaginatedAdminTable } from '../components/PaginatedAdminTable'

const CUSTOMER_GROUP_OPTIONS = [
  { value: 'All', label: 'همه مشتریان' },
  { value: 'New', label: 'مشتریان جدید' },
  { value: 'Returning', label: 'مشتریان بازگشته' },
  { value: 'Bronze', label: 'برنزی' },
  { value: 'Silver', label: 'نقره‌ای' },
  { value: 'Gold', label: 'طلایی' },
  { value: 'Student', label: 'دانشجو' },
  { value: 'Employee', label: 'کارمند' },
  { value: 'Corporate', label: 'شرکت' },
]

export function DiscountsPage() {
  const [status, setStatus] = useState('')

  const {
    session,
    tableReloadToken,
    discountForm,
    setDiscountForm,
    saving,
    setSaving,
    setMessage,
    setError,
    bumpTableReload,
    discountColumns,
    apiRequest,
    getReadableError,
    menuItemOptions,
    categoryOptions,
    emptyDiscountForm,
  } = useAdmin()

  function updateDiscountField(field, value) {
    setDiscountForm((currentForm) => ({
      ...currentForm,
      [field]: value,
    }))
  }

  function addDiscountTier() {
    setDiscountForm((currentForm) => ({
      ...currentForm,
      tiers: [...(currentForm.tiers || []), { thresholdAmount: '', valueKind: 'percent', value: '' }],
    }))
  }

  function removeDiscountTier(index) {
    setDiscountForm((currentForm) => ({
      ...currentForm,
      tiers: (currentForm.tiers || []).filter((_, i) => i !== index),
    }))
  }

  function updateDiscountTier(index, field, value) {
    setDiscountForm((currentForm) => ({
      ...currentForm,
      tiers: (currentForm.tiers || []).map((tier, i) =>
        i === index ? { ...tier, [field]: value } : tier
      ),
    }))
  }

  function toggleDiscountMenuItem(menuItemId) {
    setDiscountForm((currentForm) => {
      const menuItemIds = currentForm.menuItemIds.includes(menuItemId)
        ? currentForm.menuItemIds.filter((id) => id !== menuItemId)
        : [...currentForm.menuItemIds, menuItemId]

      return { ...currentForm, menuItemIds };
    });
  }

  function toggleDiscountCategory(categoryId) {
    setDiscountForm((currentForm) => {
      const categoryIds = currentForm.categoryIds.includes(categoryId)
        ? currentForm.categoryIds.filter((id) => id !== categoryId)
        : [...currentForm.categoryIds, categoryId]

      return { ...currentForm, categoryIds };
    });
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
              const path = discountForm.id ? `/admin/discounts/${discountForm.id}` : '/admin/discounts'

              await apiRequest(path, {
                method: discountForm.id ? 'PUT' : 'POST',
                token: session.token,
                body: {
                  name: discountForm.name,
                  englishName: discountForm.englishName,
                  description: discountForm.description,
                  code: discountForm.code?.trim() || null,
                  type: discountForm.type || 'AmountOff',
                  valueKind: discountForm.valueKind === 'fixed' ? 'FixedAmount' : 'Percentage',
                  value: discountForm.type === 'TieredCart' || discountForm.type === 'BuyXGetY' ? 0 : (Number(discountForm.value) || 0),
                  maxDiscountAmount: discountForm.maxDiscountAmount ? Number(discountForm.maxDiscountAmount) : null,
                  minCartAmount: Number(discountForm.minCartAmount) || 0,
                  startsAt: discountForm.startsAt ? new Date(discountForm.startsAt).toISOString() : null,
                  endsAt: discountForm.endsAt ? new Date(discountForm.endsAt).toISOString() : null,
                  isActive: discountForm.isActive,
                  priority: Number(discountForm.priority) || 0,
                  isStackable: discountForm.isStackable,
                  maxTotalUses: discountForm.maxTotalUses ? Number(discountForm.maxTotalUses) : null,
                  maxUsesPerCustomer: discountForm.maxUsesPerCustomer ? Number(discountForm.maxUsesPerCustomer) : null,
                  appliesTo: discountForm.appliesTo === 'EligibleItems' ? 'EligibleItems' : 'Order',
                  customerGroup: discountForm.customerGroup || 'All',
                  menuItemIds: discountForm.menuItemIds,
                  categoryIds: discountForm.categoryIds,
                  schedules:
                    discountForm.startTime || discountForm.endTime
                      ? [
                          {
                            dayOfWeek: discountForm.scheduleDay ? Number(discountForm.scheduleDay) : null,
                            startTime: discountForm.startTime || null,
                            endTime: discountForm.endTime || null,
                          },
                        ]
                      : [],
                  tiers: (discountForm.tiers || []).map((tier) => ({
                    thresholdAmount: Number(tier.thresholdAmount) || 0,
                    valueKind: tier.valueKind === 'fixed' ? 'FixedAmount' : 'Percentage',
                    value: Number(tier.value) || 0,
                  })),
                  buyQuantity: Number(discountForm.buyQuantity) || 1,
                  getQuantity: Number(discountForm.getQuantity) || 1,
                  freeItemSelection: discountForm.freeItemSelection || 'Cheapest',
                  maxApplications: discountForm.maxApplications ? Number(discountForm.maxApplications) : null,
                  isFlashSale: discountForm.isFlashSale === true,
                },
              })

              setDiscountForm({ ...emptyDiscountForm, isActive: true, isStackable: true })
              setMessage('تخفیف با موفقیت ذخیره شد.')
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
              <h3>{discountForm.id ? 'ویرایش تخفیف' : 'تخفیف جدید'}</h3>
              <p>تخفیف‌ها می‌توانند کد بدهند، خودکار باشند یا مبتنی بر سبد خرید اعمال شوند.</p>
            </div>
            <button
              type="button"
              className="ghost-button"
              onClick={() => setDiscountForm({ ...emptyDiscountForm, isActive: true, isStackable: true })}
            >
              پاک کردن فرم
            </button>
          </div>

          <div className="field-grid">
            <label>
              <span>نام فارسی</span>
              <input
                value={discountForm.name}
                onChange={(event) => updateDiscountField('name', event.target.value)}
              />
            </label>
            <label>
              <span>نام انگلیسی</span>
              <input
                className="ltr-input"
                value={discountForm.englishName}
                onChange={(event) => updateDiscountField('englishName', event.target.value)}
              />
            </label>
          </div>

          <label>
            <span>توضیحات</span>
            <textarea
              rows="3"
              value={discountForm.description}
              onChange={(event) => updateDiscountField('description', event.target.value)}
            />
          </label>

          <div className="field-grid">
            <label>
              <span>نوع تخفیف</span>
              <select
                value={discountForm.type || 'AmountOff'}
                onChange={(event) => updateDiscountField('type', event.target.value)}
              >
                <option value="AmountOff">مبلغی/درصدی روی سبد</option>
                <option value="TieredCart">پلکانی سبد (چند پله)</option>
                <option value="BuyXGetY">خرید X بگیر Y</option>
              </select>
            </label>
          </div>

          <div className="field-grid">
            <label>
              <span>کد تخفیف (خالی = خودکار)</span>
              <input
                className="ltr-input"
                value={discountForm.code}
                onChange={(event) => updateDiscountField('code', event.target.value)}
              />
            </label>
            {discountForm.type !== 'TieredCart' && (
              <label>
                <span>نوع مقدار</span>
                <select
                  value={discountForm.valueKind}
                  onChange={(event) => updateDiscountField('valueKind', event.target.value)}
                >
                  <option value="percent">درصدی</option>
                  <option value="fixed">مبلغی</option>
                </select>
              </label>
            )}
            {discountForm.type !== 'TieredCart' && (
              <>
                <label>
                  <span>مقدار (درصد یا مبلغ)</span>
                  <input
                    className="ltr-input"
                    type="number"
                    min="0"
                    value={discountForm.value}
                    onChange={(event) => updateDiscountField('value', Number(event.target.value))}
                  />
                </label>
                <label>
                  <span>سقف تخفیف (برای درصد)</span>
                  <input
                    className="ltr-input"
                    type="number"
                    min="0"
                    value={discountForm.maxDiscountAmount}
                    onChange={(event) => updateDiscountField('maxDiscountAmount', Number(event.target.value))}
                  />
                </label>
              </>
            )}
          </div>

          {discountForm.type === 'TieredCart' && (
            <div className="tier-editor">
              <div className="panel-head">
                <h4>پله‌های تخفیف</h4>
                <button type="button" className="ghost-button" onClick={addDiscountTier}>
                  افزودن پله
                </button>
              </div>
              {(discountForm.tiers || []).length === 0 && (
                <p className="text-secondary">هنوز پله‌ای تعریف نشده است.</p>
              )}
              {(discountForm.tiers || []).map((tier, index) => (
                <div className="field-grid" key={index}>
                  <label>
                    <span>مبلغ لازم (تومان)</span>
                    <input
                      className="ltr-input"
                      type="number"
                      min="0"
                      value={tier.thresholdAmount}
                      onChange={(event) => updateDiscountTier(index, 'thresholdAmount', event.target.value)}
                    />
                  </label>
                  <label>
                    <span>نوع مقدار</span>
                    <select
                      value={tier.valueKind}
                      onChange={(event) => updateDiscountTier(index, 'valueKind', event.target.value)}
                    >
                      <option value="percent">درصدی</option>
                      <option value="fixed">مبلغی</option>
                    </select>
                  </label>
                  <label>
                    <span>مقدار</span>
                    <input
                      className="ltr-input"
                      type="number"
                      min="0"
                      value={tier.value}
                      onChange={(event) => updateDiscountTier(index, 'value', event.target.value)}
                    />
                  </label>
                  <button
                    type="button"
                    className="link-button link-danger"
                    onClick={() => removeDiscountTier(index)}
                  >
                    حذف
                  </button>
                </div>
              ))}
            </div>
          )}

          {discountForm.type === 'BuyXGetY' && (
            <div className="field-grid">
              <label>
                <span>تعداد خرید (X)</span>
                <input
                  className="ltr-input"
                  type="number"
                  min="1"
                  value={discountForm.buyQuantity ?? 1}
                  onChange={(event) => updateDiscountField('buyQuantity', Number(event.target.value))}
                />
              </label>
              <label>
                <span>تعداد هدیه (Y)</span>
                <input
                  className="ltr-input"
                  type="number"
                  min="1"
                  value={discountForm.getQuantity ?? 1}
                  onChange={(event) => updateDiscountField('getQuantity', Number(event.target.value))}
                />
              </label>
              <label>
                <span>انتخاب آیتم رایگان</span>
                <select
                  value={discountForm.freeItemSelection || 'Cheapest'}
                  onChange={(event) => updateDiscountField('freeItemSelection', event.target.value)}
                >
                  <option value="Cheapest">ارزان‌ترین آیتم مشمول</option>
                  <option value="MostExpensive">گران‌ترین آیتم مشمول</option>
                </select>
              </label>
              <label>
                <span>حداکثر دفعات اعمال (خالی = نامحدود)</span>
                <input
                  className="ltr-input"
                  type="number"
                  min="1"
                  value={discountForm.maxApplications ?? ''}
                  onChange={(event) => updateDiscountField('maxApplications', event.target.value ? Number(event.target.value) : null)}
                />
              </label>
            </div>
          )}

          <div className="field-grid">
            <label>
              <span>ساعت شروع تخفیف (پایان روز)</span>
              <input
                className="ltr-input"
                type="time"
                value={discountForm.startTime || ''}
                onChange={(event) => updateDiscountField('startTime', event.target.value)}
              />
            </label>
            <label>
              <span>ساعت پایان</span>
              <input
                className="ltr-input"
                type="time"
                value={discountForm.endTime || ''}
                onChange={(event) => updateDiscountField('endTime', event.target.value)}
              />
            </label>
            <label>
              <span>روز هفته (خالی = همه روزها)</span>
              <select
                value={discountForm.scheduleDay || ''}
                onChange={(event) => updateDiscountField('scheduleDay', event.target.value)}
              >
                <option value="">همه روزها</option>
                <option value="0">یکشنبه</option>
                <option value="1">دوشنبه</option>
                <option value="2">سه‌شنبه</option>
                <option value="3">چهارشنبه</option>
                <option value="4">پنج‌شنبه</option>
                <option value="5">جمعه</option>
                <option value="6">شنبه</option>
              </select>
            </label>
          </div>

          <div className="field-grid">
            <label>
              <span>حداقل مبلغ سبد (برای اعمال)</span>
              <input
                className="ltr-input"
                type="number"
                min="0"
                value={discountForm.minCartAmount}
                onChange={(event) => updateDiscountField('minCartAmount', Number(event.target.value))}
              />
            </label>
            <label>
              <span>اولویت (عدد بزرگ‌تر = اول)</span>
              <input
                className="ltr-input"
                type="number"
                min="0"
                value={discountForm.priority}
                onChange={(event) => updateDiscountField('priority', Number(event.target.value))}
              />
            </label>
          </div>

          <div className="field-grid">
            <label>
              <span>تاریخ شروع</span>
              <input
                type="date"
                value={discountForm.startsAt}
                onChange={(event) => updateDiscountField('startsAt', event.target.value)}
              />
            </label>
            <label>
              <span>تاریخ پایان</span>
              <input
                type="date"
                value={discountForm.endsAt}
                onChange={(event) => updateDiscountField('endsAt', event.target.value)}
              />
            </label>
          </div>

          <div className="field-grid">
            <label>
              <span>حداکثر استفاده کل</span>
              <input
                className="ltr-input"
                type="number"
                min="0"
                value={discountForm.maxTotalUses}
                onChange={(event) => updateDiscountField('maxTotalUses', event.target.value)}
              />
            </label>
            <label>
              <span>حداکثر استفاده هر کاربر</span>
              <input
                className="ltr-input"
                type="number"
                min="0"
                value={discountForm.maxUsesPerCustomer}
                onChange={(event) => updateDiscountField('maxUsesPerCustomer', event.target.value)}
              />
            </label>
          </div>

          <div className="field-grid">
            <label>
              <span>گروه کاربری</span>
              <select
                value={discountForm.customerGroup}
                onChange={(event) => updateDiscountField('customerGroup', event.target.value)}
              >
                {CUSTOMER_GROUP_OPTIONS.map((option) => (
                  <option key={option.value} value={option.value}>
                    {option.label}
                  </option>
                ))}
              </select>
            </label>
            <label>
              <span>محدودیت اعمال</span>
              <select
                value={discountForm.appliesTo}
                onChange={(event) => updateDiscountField('appliesTo', event.target.value)}
              >
                <option value="Order">کل سبد خرید</option>
                <option value="EligibleItems">فقط موارد مشمول</option>
              </select>
            </label>
          </div>

          <label className="checkbox-row">
            <input
              type="checkbox"
              checked={discountForm.isStackable}
              onChange={(event) => updateDiscountField('isStackable', event.target.checked)}
            />
            <span>قابل ترکیب با سایر تخفیف‌ها</span>
          </label>

          <label className="checkbox-row">
            <input
              type="checkbox"
              checked={discountForm.isFlashSale}
              onChange={(event) => updateDiscountField('isFlashSale', event.target.checked)}
            />
            <span>فروش فوری (بنر شمارش معکوس روی منو)</span>
          </label>

          <div>
            <div>
              <span>مواد مشمول (برای محدودیت به موارد مشمول)</span>
              <div>
                {menuItemOptions.map((item) => (
                  <label key={item.id} className="checkbox-row">
                    <input
                      type="checkbox"
                      checked={discountForm.menuItemIds.includes(item.id)}
                      onChange={() => toggleDiscountMenuItem(item.id)}
                    />
                    <span>
                      {item.persianName} <span className="ltr-text">({item.englishName})</span>
                    </span>
                  </label>
                ))}
              </div>
            </div>
            <div>
              <span>دسته‌بندی‌های مشمول</span>
              <div>
                {categoryOptions.map((category) => (
                  <label key={category.id} className="checkbox-row">
                    <input
                      type="checkbox"
                      checked={discountForm.categoryIds.includes(category.id)}
                      onChange={() => toggleDiscountCategory(category.id)}
                    />
                    <span>{category.name}</span>
                  </label>
                ))}
              </div>
            </div>
          </div>

          <button type="submit" disabled={saving}>
            {saving ? 'در حال ذخیره...' : 'ذخیره تخفیف'}
          </button>
        </form>

        <section className="panel list-panel">
          <div className="panel-head">
            <div>
              <h3>تخفیف‌های موجود</h3>
              <p>بارگذاری صفحه‌بندی شده با فیلتر وضعیت</p>
            </div>
          </div>
          <PaginatedAdminTable
            apiPath="/admin/discounts"
            token={session.token}
            columns={discountColumns}
            enabled
            reloadToken={tableReloadToken}
            searchPlaceholder="جستجو در تخفیف‌ها..."
            status={status}
            onStatusChange={setStatus}
          />
        </section>
      </section>
  )
}
