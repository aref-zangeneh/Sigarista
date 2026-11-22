import { useCallback, useEffect, useMemo, useState } from 'react'

export const MAIN_WAREHOUSE_KEY = '__MAIN__'

let lineCounter = 0
const nextLineUid = () => `trf-line-${Date.now()}-${lineCounter++}`

function toApiWarehouseId(key) {
  return !key || key === MAIN_WAREHOUSE_KEY ? null : key
}

export function WarehouseTransferModal({
  open,
  onClose,
  warehouses,
  initialSourceKey = MAIN_WAREHOUSE_KEY,
  initialDestinationKey = '',
  initialIngredientId = '',
  session,
  apiRequest,
  getReadableError,
  toPersianNumber,
  onTransferred,
}) {
  const activeWarehouses = useMemo(
    () => (warehouses ?? []).filter((w) => w.isActive),
    [warehouses],
  )

  const locationOptions = useMemo(() => {
    const opts = [{ key: MAIN_WAREHOUSE_KEY, label: 'انبار اصلی (موجودی مرکزی)' }]
    const roots = activeWarehouses.filter((w) => !w.parentId)
    const byParent = new Map()
    activeWarehouses.forEach((w) => {
      if (w.parentId) {
        if (!byParent.has(w.parentId)) byParent.set(w.parentId, [])
        byParent.get(w.parentId).push(w)
      }
    })
    roots.forEach((root) => {
      opts.push({ key: root.id, label: root.name })
      ;(byParent.get(root.id) ?? []).forEach((child) => {
        opts.push({ key: child.id, label: `↳ ${child.name} (${root.name})` })
      })
    })
    // Include any active sub-warehouse whose parent is inactive
    const added = new Set(opts.map((o) => o.key))
    activeWarehouses.forEach((w) => {
      if (!added.has(w.id)) {
        opts.push({ key: w.id, label: w.name })
      }
    })
    return opts
  }, [activeWarehouses])

  const [sourceKey, setSourceKey] = useState(MAIN_WAREHOUSE_KEY)
  const [destinationKey, setDestinationKey] = useState('')
  const [sourceStockItems, setSourceStockItems] = useState([])
  const [stockLoading, setStockLoading] = useState(false)
  const [lines, setLines] = useState([{ uid: nextLineUid(), ingredientId: '', quantity: '' }])
  const [note, setNote] = useState('')
  const [submitting, setSubmitting] = useState(false)
  const [formError, setFormError] = useState('')

  // Initialize modal state whenever opened
  useEffect(() => {
    if (!open) return
    const src = initialSourceKey || MAIN_WAREHOUSE_KEY
    let dst = initialDestinationKey || ''
    if (!dst || dst === src) {
      const firstOther = locationOptions.find((o) => o.key !== src)
      dst = firstOther?.key ?? ''
    }
    setSourceKey(src)
    setDestinationKey(dst)
    setLines([{ uid: nextLineUid(), ingredientId: initialIngredientId || '', quantity: '' }])
    setNote('')
    setFormError('')
  }, [open, initialSourceKey, initialDestinationKey, initialIngredientId, locationOptions])

  const loadSourceStock = useCallback(
    async (key) => {
      if (!open || !session?.token || !key) return
      setStockLoading(true)
      setFormError('')
      try {
        const path =
          key === MAIN_WAREHOUSE_KEY
            ? '/admin/warehouses/main-stock'
            : `/admin/warehouses/${key}/stock`
        const data = await apiRequest(path, { token: session.token })
        setSourceStockItems(Array.isArray(data?.items) ? data.items : [])
      } catch (err) {
        setFormError(getReadableError(err))
        setSourceStockItems([])
      } finally {
        setStockLoading(false)
      }
    },
    [open, session?.token, apiRequest, getReadableError],
  )

  useEffect(() => {
    if (open && sourceKey) {
      loadSourceStock(sourceKey)
    }
  }, [open, sourceKey, loadSourceStock])

  if (!open) return null

  const stockByIngredient = new Map(sourceStockItems.map((item) => [item.ingredientId, item]))
  const availableItems = sourceStockItems.filter((item) => Number(item.quantity) > 0)

  const handleSourceChange = (nextSrc) => {
    setSourceKey(nextSrc)
    if (destinationKey === nextSrc) {
      const fallback = locationOptions.find((o) => o.key !== nextSrc)
      setDestinationKey(fallback?.key ?? '')
    }
    setLines([{ uid: nextLineUid(), ingredientId: '', quantity: '' }])
  }

  const handleSwap = () => {
    if (!destinationKey) return
    const prevSrc = sourceKey
    const prevDst = destinationKey
    setSourceKey(prevDst)
    setDestinationKey(prevSrc)
    setLines([{ uid: nextLineUid(), ingredientId: '', quantity: '' }])
  }

  const updateLine = (uid, patch) => {
    setLines((curr) => curr.map((l) => (l.uid === uid ? { ...l, ...patch } : l)))
  }

  const addLine = () => {
    setLines((curr) => [...curr, { uid: nextLineUid(), ingredientId: '', quantity: '' }])
  }

  const removeLine = (uid) => {
    setLines((curr) => (curr.length <= 1 ? curr : curr.filter((l) => l.uid !== uid)))
  }

  // Sum requested quantity per ingredient across rows to guard duplicate rows
  const requestedTotals = new Map()
  lines.forEach((l) => {
    if (l.ingredientId) {
      const qty = Number(l.quantity) || 0
      requestedTotals.set(l.ingredientId, (requestedTotals.get(l.ingredientId) ?? 0) + qty)
    }
  })

  const handleSubmit = async (event) => {
    event.preventDefault()
    setFormError('')

    if (!destinationKey || sourceKey === destinationKey) {
      setFormError('انبار مبدا و مقصد باید متفاوت و مشخص باشند.')
      return
    }

    const validLines = lines.filter((l) => l.ingredientId && Number(l.quantity) > 0)
    if (validLines.length === 0) {
      setFormError('حداقل یک ماده اولیه با مقدار معتبر (بزرگ‌تر از صفر) وارد کنید.')
      return
    }

    for (const [ingId, totalQty] of requestedTotals.entries()) {
      if (totalQty <= 0) continue
      const stockRow = stockByIngredient.get(ingId)
      const avail = Number(stockRow?.quantity ?? 0)
      if (totalQty > avail) {
        setFormError(
          `موجودی «${stockRow?.persianName ?? 'ماده انتخاب‌شده'}» در انبار مبدا کافی نیست (موجود: ${toPersianNumber(avail)} ${stockRow?.unit ?? ''}).`,
        )
        return
      }
    }

    setSubmitting(true)
    try {
      const result = await apiRequest('/admin/warehouses/transfers', {
        method: 'POST',
        token: session.token,
        body: {
          sourceWarehouseId: toApiWarehouseId(sourceKey),
          destinationWarehouseId: toApiWarehouseId(destinationKey),
          lines: validLines.map((l) => ({
            ingredientId: l.ingredientId,
            quantity: Number(l.quantity),
          })),
          note: note.trim() || null,
        },
      })
      await onTransferred?.(result)
      onClose()
    } catch (err) {
      setFormError(getReadableError(err))
    } finally {
      setSubmitting(false)
    }
  }

  const canTransferBetweenTwo = locationOptions.length >= 2

  return (
    <div
      className="option-picker-backdrop"
      role="dialog"
      aria-modal="true"
      aria-label="حواله انتقال بین انبارها"
      onClick={(event) => {
        if (event.target === event.currentTarget) onClose()
      }}
    >
      <div className="option-picker-modal warehouse-transfer-modal">
        <div className="option-picker-head">
          <div>
            <h4>حواله انتقال بین انبارها</h4>
            <div className="table-subtext">
              انتقال یک یا چند ماده اولیه به‌صورت هم‌زمان با ثبت خودکار تراکنش خروج و ورود
            </div>
          </div>
          <button type="button" className="ghost-button tiny" onClick={onClose}>
            بستن
          </button>
        </div>

        <form onSubmit={handleSubmit} className="warehouse-transfer-form">
          <div className="option-picker-body">
            {!canTransferBetweenTwo ? (
              <p className="feedback feedback-error">
                برای انتقال بین انبارها، ابتدا حداقل یک انبار یا انبارک فعال تعریف کنید.
              </p>
            ) : (
              <>
                {formError ? (
                  <p className="feedback feedback-error" role="alert">
                    {formError}
                  </p>
                ) : null}

                <div className="warehouse-transfer-route">
                  <label>
                    <span>انبار مبدا (خروج از)</span>
                    <select
                      value={sourceKey}
                      onChange={(event) => handleSourceChange(event.target.value)}
                    >
                      {locationOptions.map((opt) => (
                        <option key={opt.key} value={opt.key}>
                          {opt.label}
                        </option>
                      ))}
                    </select>
                  </label>

                  <button
                    type="button"
                    className="ghost-button warehouse-swap-btn"
                    onClick={handleSwap}
                    title="جابه‌جایی مبدا و مقصد"
                    aria-label="جابه‌جایی مبدا و مقصد"
                  >
                    ⇄
                  </button>

                  <label>
                    <span>انبار مقصد (ورود به)</span>
                    <select
                      value={destinationKey}
                      onChange={(event) => setDestinationKey(event.target.value)}
                    >
                      {locationOptions
                        .filter((opt) => opt.key !== sourceKey)
                        .map((opt) => (
                          <option key={opt.key} value={opt.key}>
                            {opt.label}
                          </option>
                        ))}
                    </select>
                  </label>
                </div>

                {stockLoading ? (
                  <p className="feedback">در حال دریافت موجودی انبار مبدا...</p>
                ) : availableItems.length === 0 ? (
                  <p className="feedback">
                    هیچ ماده‌ای با موجودی مثبت در انبار مبدا انتخاب‌شده یافت نشد.
                  </p>
                ) : (
                  <div className="warehouse-transfer-lines">
                    <div className="warehouse-transfer-lines-head">
                      <strong>اقلام انتقالی ({toPersianNumber(lines.length)})</strong>
                      <button type="button" className="ghost-button tiny" onClick={addLine}>
                        + افزودن ماده دیگر
                      </button>
                    </div>

                    {lines.map((line) => {
                      const stockRow = line.ingredientId
                        ? stockByIngredient.get(line.ingredientId)
                        : null
                      const available = Number(stockRow?.quantity ?? 0)
                      const qty = Number(line.quantity) || 0
                      const totalForIng = requestedTotals.get(line.ingredientId) ?? 0
                      const exceeds = Boolean(stockRow) && totalForIng > available
                      const remaining = stockRow ? Math.max(0, available - totalForIng) : null

                      return (
                        <div
                          key={line.uid}
                          className={`warehouse-transfer-line ${exceeds ? 'is-exceeded' : ''}`}
                        >
                          <div className="warehouse-transfer-line-fields">
                            <label>
                              <span>ماده اولیه</span>
                              <select
                                value={line.ingredientId}
                                onChange={(event) =>
                                  updateLine(line.uid, {
                                    ingredientId: event.target.value,
                                    quantity: '',
                                  })
                                }
                              >
                                <option value="">-- انتخاب ماده اولیه --</option>
                                {availableItems.map((item) => (
                                  <option key={item.ingredientId} value={item.ingredientId}>
                                    {item.persianName} — موجودی: {toPersianNumber(item.quantity)}{' '}
                                    {item.unit}
                                  </option>
                                ))}
                              </select>
                            </label>

                            <label>
                              <span>مقدار انتقال {stockRow?.unit ? `(${stockRow.unit})` : ''}</span>
                              <div className="warehouse-qty-wrap">
                                <input
                                  className="ltr-input"
                                  type="number"
                                  min="0"
                                  step="0.001"
                                  placeholder="مقدار"
                                  value={line.quantity}
                                  onChange={(event) =>
                                    updateLine(line.uid, { quantity: event.target.value })
                                  }
                                />
                                {stockRow ? (
                                  <button
                                    type="button"
                                    className="ghost-button tiny"
                                    onClick={() => updateLine(line.uid, { quantity: available })}
                                    title="انتقال تمام موجودی این ماده"
                                  >
                                    کل ({toPersianNumber(available)})
                                  </button>
                                ) : null}
                              </div>
                            </label>

                            {lines.length > 1 ? (
                              <button
                                type="button"
                                className="ghost-button tiny danger"
                                onClick={() => removeLine(line.uid)}
                                aria-label="حذف ردیف"
                                style={{ alignSelf: 'end' }}
                              >
                                حذف
                              </button>
                            ) : null}
                          </div>

                          {stockRow ? (
                            <div className="warehouse-transfer-line-meta">
                              {exceeds ? (
                                <span className="warehouse-transfer-warn">
                                  مقدار درخواستی از موجودی مبدا ({toPersianNumber(available)}{' '}
                                  {stockRow.unit}) بیشتر است!
                                </span>
                              ) : (
                                <span className="table-subtext">
                                  موجود در مبدا: <strong>{toPersianNumber(available)}</strong>{' '}
                                  {stockRow.unit}
                                  {qty > 0 ? (
                                    <>
                                      {' '}
                                      ← باقی‌مانده پس از انتقال:{' '}
                                      <strong>{toPersianNumber(remaining)}</strong> {stockRow.unit}
                                    </>
                                  ) : null}
                                </span>
                              )}
                            </div>
                          ) : null}
                        </div>
                      )
                    })}
                  </div>
                )}

                <label>
                  <span>توضیحات حواله (اختیاری)</span>
                  <input
                    type="text"
                    maxLength={500}
                    placeholder="مثلاً شارژ شیفت عصر انبارک بار گرم"
                    value={note}
                    onChange={(event) => setNote(event.target.value)}
                  />
                </label>
              </>
            )}
          </div>

          <div className="option-picker-foot">
            <span className="table-subtext">
              هر انتقال به‌صورت هم‌زمان در تاریخچه حواله‌ها و کاردکس هر دو انبار ثبت می‌شود.
            </span>
            <div className="option-groups-actions">
              <button type="button" className="ghost-button" onClick={onClose}>
                انصراف
              </button>
              <button
                type="submit"
                disabled={submitting || !canTransferBetweenTwo || availableItems.length === 0}
              >
                {submitting ? 'در حال ثبت انتقال...' : 'ثبت حواله انتقال'}
              </button>
            </div>
          </div>
        </form>
      </div>
    </div>
  )
}
