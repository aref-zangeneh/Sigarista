import { useCallback, useEffect, useMemo, useState } from 'react'
import {
  computeDiscountedBundlePrice,
  evaluateBundleDraft,
  getAvailableSizesForOption,
  resolveComponentRealPrice,
} from '../utils/bundlePricing'
import './MenuItemBundlePanel.css'

let bundleUid = 0
const nextRowUid = () => `bnd-${Date.now()}-${bundleUid++}`

export function MenuItemBundlePanel({
  session,
  apiRequest,
  menuItemOptions,
  getReadableError,
  setMessage,
  setError,
  toPersianNumber,
  formatMoney,
  onBundleSaved,
  focusMenuItemId = '',
}) {
  const [selectedMenuItemId, setSelectedMenuItemId] = useState('')
  const [isBundle, setIsBundle] = useState(false)
  const [bundlePriceInput, setBundlePriceInput] = useState('')
  const [components, setComponents] = useState([])
  const [costByComponentId, setCostByComponentId] = useState(new Map())
  const [pickerSearch, setPickerSearch] = useState('')
  const [loading, setLoading] = useState(false)
  const [saving, setSaving] = useState(false)

  useEffect(() => {
    if (focusMenuItemId) {
      setSelectedMenuItemId(focusMenuItemId)
    }
  }, [focusMenuItemId])

  const optionsById = useMemo(
    () => new Map((menuItemOptions ?? []).map((item) => [item.id, item])),
    [menuItemOptions],
  )

  const sortedTargetOptions = useMemo(() => {
    return [...(menuItemOptions ?? [])].sort((a, b) => {
      if (Boolean(a.isBundle) !== Boolean(b.isBundle)) {
        return a.isBundle ? -1 : 1
      }
      return (a.persianName ?? '').localeCompare(b.persianName ?? '', 'fa')
    })
  }, [menuItemOptions])

  const selectedTarget = optionsById.get(selectedMenuItemId) ?? null

  const loadBundleConfig = useCallback(
    async (menuItemId) => {
      if (!menuItemId || !session?.token) {
        setIsBundle(false)
        setBundlePriceInput('')
        setComponents([])
        return
      }
      setLoading(true)
      setError('')
      try {
        const data = await apiRequest(`/admin/menu-items/${menuItemId}/bundle-items`, {
          token: session.token,
        })
        setIsBundle(Boolean(data?.isBundle))
        setBundlePriceInput(
          Number(data?.bundlePrice) > 0 ? String(Math.round(Number(data.bundlePrice))) : '',
        )
        const nextCosts = new Map()
        const loadedRows = (data?.components ?? []).map((comp) => {
          nextCosts.set(comp.componentMenuItemId, Number(comp.unitCost) || 0)
          return {
            uid: nextRowUid(),
            componentMenuItemId: comp.componentMenuItemId,
            persianName: comp.persianName,
            englishName: comp.englishName,
            imageUrl: comp.imageUrl,
            sizeKey: comp.sizeKey ?? null,
            sizeLabel: comp.sizeLabel ?? null,
            quantity: comp.quantity ?? 1,
            unitRegularPrice: comp.unitRegularPrice ?? 0,
            unitCost: comp.unitCost ?? 0,
            displayOrder: comp.displayOrder ?? 0,
          }
        })
        setCostByComponentId(nextCosts)
        setComponents(loadedRows)
      } catch (err) {
        setError(getReadableError(err))
      } finally {
        setLoading(false)
      }
    },
    [session?.token, apiRequest, getReadableError, setError],
  )

  useEffect(() => {
    loadBundleConfig(selectedMenuItemId)
  }, [selectedMenuItemId, loadBundleConfig])

  const availablePickers = useMemo(() => {
    const q = pickerSearch.trim().toLowerCase()
    return (menuItemOptions ?? [])
      .filter((item) => item.id !== selectedMenuItemId && !item.isBundle)
      .filter((item) => {
        if (!q) return true
        return (
          (item.persianName ?? '').toLowerCase().includes(q) ||
          (item.englishName ?? '').toLowerCase().includes(q)
        )
      })
      .slice(0, 36)
  }, [menuItemOptions, selectedMenuItemId, pickerSearch])

  const evaluation = useMemo(
    () =>
      evaluateBundleDraft({
        components,
        optionsById,
        costByComponentId,
        bundlePrice: Number(bundlePriceInput) || 0,
      }),
    [components, optionsById, costByComponentId, bundlePriceInput],
  )

  const handleAddComponent = (optionItem) => {
    if (!optionItem) return
    const { sizeKey, sizeLabel, unitPrice } = resolveComponentRealPrice(optionItem, null)
    setIsBundle(true)
    setComponents((prev) => {
      const existingIndex = prev.findIndex(
        (row) =>
          row.componentMenuItemId === optionItem.id &&
          (row.sizeKey ?? '') === (sizeKey ?? ''),
      )
      if (existingIndex >= 0) {
        return prev.map((row, idx) =>
          idx === existingIndex
            ? { ...row, quantity: Math.min(50, (Number(row.quantity) || 1) + 1) }
            : row,
        )
      }
      return [
        ...prev,
        {
          uid: nextRowUid(),
          componentMenuItemId: optionItem.id,
          persianName: optionItem.persianName,
          englishName: optionItem.englishName,
          imageUrl: optionItem.imageUrl,
          sizeKey,
          sizeLabel,
          quantity: 1,
          unitRegularPrice: unitPrice,
          unitCost: 0,
          displayOrder: prev.length,
        },
      ]
    })
  }

  const updateComponentRow = (uid, patch) => {
    setComponents((prev) => prev.map((row) => (row.uid === uid ? { ...row, ...patch } : row)))
  }

  const removeComponentRow = (uid) => {
    setComponents((prev) => prev.filter((row) => row.uid !== uid))
  }

  const handleApplyQuickDiscount = (percent) => {
    if (evaluation.regularTotal <= 0) return
    const suggested = computeDiscountedBundlePrice(evaluation.regularTotal, percent)
    setBundlePriceInput(String(suggested))
  }

  const handleSaveBundle = async () => {
    if (!selectedMenuItemId || !session?.token) return
    if (isBundle && components.length === 0) {
      setError('برای ذخیره محصول باندل حداقل یک آیتم زیرمجموعه انتخاب کنید.')
      return
    }
    const numericPrice = Number(bundlePriceInput)
    if (isBundle && (!Number.isFinite(numericPrice) || numericPrice <= 0)) {
      setError('قیمت فروش باندل را به صورت عددی بزرگ‌تر از صفر وارد کنید.')
      return
    }

    setSaving(true)
    setError('')
    setMessage('')
    try {
      const payload = {
        isBundle,
        bundlePrice: isBundle ? numericPrice : null,
        components: isBundle
          ? components.map((row, idx) => ({
              componentMenuItemId: row.componentMenuItemId,
              quantity: Math.max(1, Math.min(50, Math.round(Number(row.quantity) || 1))),
              sizeKey: row.sizeKey || null,
              displayOrder: idx,
            }))
          : [],
      }

      const saved = await apiRequest(`/admin/menu-items/${selectedMenuItemId}/bundle-items`, {
        method: 'PUT',
        body: payload,
        token: session.token,
      })

      setMessage(
        isBundle
          ? `باندل «${saved.persianName}» ذخیره شد (ارزش واقعی ${formatMoney(saved.regularTotal)} → قیمت باندل ${formatMoney(saved.bundlePrice)}).`
          : `حالت باندل از آیتم «${saved.persianName}» برداشته شد.`,
      )
      await loadBundleConfig(selectedMenuItemId)
      if (onBundleSaved) {
        onBundleSaved()
      }
    } catch (err) {
      setError(getReadableError(err))
    } finally {
      setSaving(false)
    }
  }

  return (
    <section className="panel bundle-studio" id="bundle-studio-panel">
      <div className="panel-head">
        <div>
          <h3>استودیو محصولات باندل (پکیج ترکیبی)</h3>
          <p>
            ابتدا آیتم باندل را در فرم بالا ایجاد کنید (مثلاً «باندل صبحانه کافه»)، سپس اینجا آیتم‌های واقعی
            زیرمجموعه آن را انتخاب کنید. قیمت واقعی هر آیتم به‌صورت زنده از منو خوانده می‌شود و سود مشتری
            به‌طور خودکار نمایش داده می‌شود؛ هنگام فروش نیز موجودی انبار و تولید تک‌تک آیتم‌ها خودکار کسر می‌شود.
          </p>
        </div>
      </div>

      <div className="bundle-select-bar">
        <label>
          <span>انتخاب آیتم منو برای تنظیم به‌عنوان باندل</span>
          <select
            value={selectedMenuItemId}
            onChange={(event) => setSelectedMenuItemId(event.target.value)}
          >
            <option value="">— یک آیتم منو انتخاب کنید —</option>
            {sortedTargetOptions.map((item) => (
              <option key={item.id} value={item.id}>
                {item.isBundle ? '★ [باندل] ' : ''}
                {item.persianName}
                {item.englishName ? ` (${item.englishName})` : ''}
              </option>
            ))}
          </select>
        </label>
        {selectedMenuItemId ? (
          <button
            type="button"
            className="primary-button"
            disabled={saving || loading}
            onClick={handleSaveBundle}
          >
            {saving ? 'در حال ذخیره باندل...' : 'ذخیره تنظیمات باندل'}
          </button>
        ) : null}
      </div>

      {!selectedMenuItemId ? (
        <p className="feedback" style={{ marginTop: '0.85rem' }}>
          یک آیتم را از لیست بالا انتخاب کنید تا ترکیب اقلام، قیمت واقعی، سود مشتری و پیش‌نمایش کارت آن را مدیریت کنید.
        </p>
      ) : loading ? (
        <p className="feedback" style={{ marginTop: '0.85rem' }}>
          در حال بارگذاری ترکیب باندل...
        </p>
      ) : (
        <div className="bundle-studio-grid" style={{ marginTop: '1rem' }}>
          {/* ستون راست: انتخاب اقلام باندل */}
          <div className="bundle-studio-col">
            <div className="bundle-toggle-card">
              <div>
                <strong>فعال‌سازی حالت محصول باندل</strong>
                <small>
                  با فعال کردن، این آیتم شامل چند محصول منو با قیمت واقعی جداگانه و یک قیمت ویژه باندل می‌شود.
                </small>
              </div>
              <label className="checkbox-row" style={{ margin: 0 }}>
                <input
                  type="checkbox"
                  checked={isBundle}
                  onChange={(event) => setIsBundle(event.target.checked)}
                />
                <span>{isBundle ? 'باندل فعال' : 'آیتم معمولی'}</span>
              </label>
            </div>

            {isBundle ? (
              <>
                <div className="bundle-picker-box">
                  <label>
                    <span>افزودن آیتم به داخل باندل (قیمت واقعی هر آیتم زنده خوانده می‌شود)</span>
                    <input
                      type="search"
                      placeholder="جستجوی آمریکانو، چیزکیک سن‌سباستین و..."
                      value={pickerSearch}
                      onChange={(event) => setPickerSearch(event.target.value)}
                    />
                  </label>
                  <div className="bundle-picker-list">
                    {availablePickers.map((item) => (
                      <button
                        key={item.id}
                        type="button"
                        className="bundle-picker-chip"
                        onClick={() => handleAddComponent(item)}
                      >
                        <span className="bundle-picker-chip-name">+ {item.persianName}</span>
                        <span className="bundle-picker-chip-price">
                          {item.effectivePrice > 0 ? formatMoney(item.effectivePrice) : 'بدون قیمت'}
                        </span>
                      </button>
                    ))}
                  </div>
                </div>

                <div className="bundle-components-list">
                  {evaluation.rows.length === 0 ? (
                    <p className="feedback">
                      هنوز آیتمی به این باندل اضافه نشده است. از کادر بالا آیتم‌های موردنظر (مثلاً آمریکانو و چیزکیک) را اضافه کنید.
                    </p>
                  ) : (
                    evaluation.rows.map((row) => {
                      const opt = optionsById.get(row.componentMenuItemId)
                      const sizes = getAvailableSizesForOption(opt)
                      return (
                        <div key={row.uid} className="bundle-component-row">
                          <div className="bundle-component-meta">
                            <strong>{row.persianName}</strong>
                            <small>
                              قیمت واقعی واحد: {formatMoney(row.unitRegularPrice)} · جمع:{' '}
                              <strong>{formatMoney(row.lineRegularPrice)}</strong>
                              {row.unitCost > 0 ? ` · تمام‌شده: ${formatMoney(row.lineCost)}` : ''}
                            </small>
                          </div>

                          {sizes.length > 0 ? (
                            <select
                              aria-label="اندازه آیتم"
                              value={row.sizeKey ?? ''}
                              onChange={(event) =>
                                updateComponentRow(row.uid, { sizeKey: event.target.value || null })
                              }
                            >
                              {sizes.map((s) => (
                                <option key={s.key} value={s.key}>
                                  {s.label} ({formatMoney(s.price)})
                                </option>
                              ))}
                            </select>
                          ) : (
                            <span className="table-subtext">تک‌سایز</span>
                          )}

                          <div className="bundle-qty-stepper">
                            <button
                              type="button"
                              aria-label="کاهش تعداد"
                              onClick={() =>
                                updateComponentRow(row.uid, {
                                  quantity: Math.max(1, (Number(row.quantity) || 1) - 1),
                                })
                              }
                            >
                              −
                            </button>
                            <span>{toPersianNumber(row.quantity)}</span>
                            <button
                              type="button"
                              aria-label="افزایش تعداد"
                              onClick={() =>
                                updateComponentRow(row.uid, {
                                  quantity: Math.min(50, (Number(row.quantity) || 1) + 1),
                                })
                              }
                            >
                              +
                            </button>
                          </div>

                          <button
                            type="button"
                            className="link-button link-danger"
                            onClick={() => removeComponentRow(row.uid)}
                          >
                            حذف
                          </button>
                        </div>
                      )
                    })
                  )}
                </div>
              </>
            ) : null}
          </div>

          {/* ستون چپ: محاسبه مالی زنده + پیش‌نمایش کارت مشتری */}
          {isBundle ? (
            <div className="bundle-studio-col">
              <div className="bundle-finance-card">
                <h4 style={{ margin: 0 }}>قیمت‌گذاری و سودآوری باندل</h4>

                <label>
                  <span>قیمت فروش باندل به مشتری (تومان)</span>
                  <input
                    className="ltr-input"
                    type="number"
                    min="0"
                    step="1000"
                    placeholder="مثلاً 430000"
                    value={bundlePriceInput}
                    onChange={(event) => setBundlePriceInput(event.target.value)}
                  />
                </label>

                <div className="bundle-quick-discounts">
                  {[10, 14, 15, 20, 25].map((pct) => (
                    <button
                      key={pct}
                      type="button"
                      className="bundle-quick-btn"
                      onClick={() => handleApplyQuickDiscount(pct)}
                    >
                      {toPersianNumber(pct)}٪ تخفیف
                    </button>
                  ))}
                </div>

                <div className="bundle-finance-rows">
                  <div className="bundle-finance-row bundle-finance-row--strike">
                    <span>جمع قیمت واقعی آیتم‌ها (جداگانه):</span>
                    <strong>{formatMoney(evaluation.regularTotal)}</strong>
                  </div>
                  <div className="bundle-finance-row">
                    <span>قیمت فروش باندل:</span>
                    <strong>{formatMoney(evaluation.bundlePrice)}</strong>
                  </div>
                  {evaluation.savingsAmount > 0 ? (
                    <div className="bundle-finance-row bundle-finance-row--savings">
                      <span>سود مشتری در این باندل:</span>
                      <span>
                        {formatMoney(evaluation.savingsAmount)} ({toPersianNumber(evaluation.savingsPercent)}٪ صرفه‌جویی)
                      </span>
                    </div>
                  ) : null}
                  {evaluation.totalCost > 0 ? (
                    <>
                      <div className="bundle-finance-row">
                        <span>بهای تمام‌شده مجموع (COGS):</span>
                        <span>{formatMoney(evaluation.totalCost)}</span>
                      </div>
                      <div className="bundle-finance-row">
                        <span>سود ناخالص کافه:</span>
                        <strong>
                          {formatMoney(evaluation.estimatedProfit)}
                          {evaluation.estimatedMarginPercent != null
                            ? ` (${toPersianNumber(evaluation.estimatedMarginPercent)}٪)`
                            : ''}
                        </strong>
                      </div>
                    </>
                  ) : null}
                </div>

                {evaluation.warnings.length > 0 ? (
                  <div className="bundle-warning-box">
                    {evaluation.warnings.map((warning) => (
                      <div key={warning}>• {warning}</div>
                    ))}
                  </div>
                ) : null}
              </div>

              {/* پیش‌نمایش زنده نحوه نمایش به مشتری */}
              <div className="bundle-preview-card">
                <div className="bundle-preview-head">
                  <span className="bundle-preview-pill">پکیج باندل</span>
                  {evaluation.savingsPercent > 0 ? (
                    <span className="bundle-preview-save-pill">
                      {toPersianNumber(evaluation.savingsPercent)}٪ صرفه‌جویی
                    </span>
                  ) : null}
                </div>
                <strong>{selectedTarget?.persianName ?? 'پیش‌نمایش باندل'}</strong>
                <div className="bundle-preview-items">
                  {evaluation.rows.length === 0 ? (
                    <small className="table-subtext">اقلام باندل اینجا نمایش داده می‌شوند</small>
                  ) : (
                    evaluation.rows.map((row) => (
                      <div key={row.uid} className="bundle-preview-item-row">
                        <span>
                          {toPersianNumber(row.quantity)} × {row.persianName}
                          {row.sizeLabel ? ` (${row.sizeLabel})` : ''}
                        </span>
                        <span>{formatMoney(row.lineRegularPrice)}</span>
                      </div>
                    ))
                  )}
                </div>
                <div className="bundle-preview-footer">
                  <div>
                    {evaluation.savingsAmount > 0 ? (
                      <div className="bundle-preview-old">{formatMoney(evaluation.regularTotal)}</div>
                    ) : null}
                    <div className="bundle-preview-price">{formatMoney(evaluation.bundlePrice)}</div>
                  </div>
                  {evaluation.savingsAmount > 0 ? (
                    <span className="bundle-preview-save-pill">
                      {formatMoney(evaluation.savingsAmount)} سود شما
                    </span>
                  ) : null}
                </div>
              </div>
            </div>
          ) : null}
        </div>
      )}
    </section>
  )
}
