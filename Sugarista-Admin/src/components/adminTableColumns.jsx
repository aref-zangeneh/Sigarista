import { formatMoney } from './DataTable'

function stockStatusBadge(item) {
  if (item.currentQuantity < 0) {
    return <span className="stock-badge stock-badge--negative">منفی</span>
  }
  if (item.isLowStock) {
    return <span className="stock-badge stock-badge--low">کم</span>
  }
  return <span className="stock-badge stock-badge--ok">مناسب</span>
}

function movementTypeLabel(type) {
  if (type === 'Restock') return 'شارژ موجودی'
  if (type === 'ManualAdjustment') return 'تصحیح دستی'
  if (type === 'SaleDeduction') return 'کسر فروش'
  return type
}

export function buildCategoryColumns({ toPersianNumber, editCategory, handleDeleteCategory }) {
  return [
    { accessorKey: 'name', header: 'نام' },
    { accessorKey: 'slug', header: 'اسلاگ', meta: { className: 'ltr-text' } },
    {
      accessorKey: 'itemCount',
      header: 'تعداد آیتم',
      cell: ({ getValue }) => toPersianNumber(getValue()),
    },
    {
      accessorKey: 'isActive',
      header: 'وضعیت',
      cell: ({ getValue }) => (getValue() ? 'فعال' : 'مخفی'),
    },
    {
      id: 'actions',
      header: 'عملیات',
      enableSorting: false,
      cell: ({ row }) => (
        <div className="table-actions">
          <button type="button" className="link-button" onClick={() => editCategory(row.original)}>
            ویرایش
          </button>
          <button
            type="button"
            className="link-button link-danger"
            onClick={() => handleDeleteCategory(row.original.id)}
          >
            حذف
          </button>
        </div>
      ),
    },
  ]
}

export function buildMenuItemColumns({ toPersianNumber, joinTags, editMenuItem, handleDeleteMenuItem }) {
  return [
    {
      id: 'name',
      header: 'نام',
      accessorFn: (row) => `${row.persianName ?? row.name} ${row.englishName}`,
      cell: ({ row }) => (
        <>
          <strong>{row.original.persianName ?? row.original.name}</strong>
          <div className="table-subtext ltr-text">{row.original.englishName}</div>
          <div className="table-subtext">{joinTags(row.original.tags)}</div>
        </>
      ),
    },
    { accessorKey: 'categoryName', header: 'دسته‌بندی' },
    {
      id: 'price',
      header: 'قیمت پایه',
      accessorFn: (row) => row.price,
      cell: ({ row }) => (
        <>
          {formatMoney(row.original.price)}
          {row.original.specialOfferPrice ? (
            <div className="table-subtext">پیشنهاد: {formatMoney(row.original.specialOfferPrice)}</div>
          ) : null}
        </>
      ),
    },
    {
      id: 'status',
      header: 'وضعیت',
      accessorFn: (row) => `${row.isActive} ${row.isOrderable}`,
      cell: ({ row }) => (
        <>
          {row.original.isActive ? 'نمایش داده می‌شود' : 'مخفی'}
          <div className="table-subtext">
            {row.original.isOrderable ? 'قابل سفارش' : 'فعلا غیرقابل سفارش'}
          </div>
        </>
      ),
    },
    {
      id: 'actions',
      header: 'عملیات',
      enableSorting: false,
      cell: ({ row }) => (
        <div className="table-actions">
          <button type="button" className="link-button" onClick={() => editMenuItem(row.original)}>
            ویرایش
          </button>
          <button
            type="button"
            className="link-button link-danger"
            onClick={() => handleDeleteMenuItem(row.original.id)}
          >
            حذف
          </button>
        </div>
      ),
    },
  ]
}

export function buildAddonColumns({ joinTags, setAddonForm, emptyAddonForm, bumpTableReload, addonForm, session, apiRequest, setSaving, setError, setMessage, getReadableError }) {
  return [
    {
      id: 'name',
      header: 'نام',
      accessorFn: (row) => `${row.persianName} ${row.englishName}`,
      cell: ({ row }) => (
        <>
          <strong>{row.original.persianName}</strong>
          <div className="table-subtext ltr-text">{row.original.englishName}</div>
        </>
      ),
    },
    {
      accessorKey: 'price',
      header: 'قیمت',
      cell: ({ getValue }) => formatMoney(getValue()),
    },
    {
      accessorKey: 'tags',
      header: 'برچسب‌ها',
      cell: ({ getValue }) => joinTags(getValue()),
      enableSorting: false,
    },
    {
      accessorKey: 'isActive',
      header: 'وضعیت',
      cell: ({ getValue }) => (getValue() ? 'فعال' : 'مخفی'),
    },
    {
      id: 'actions',
      header: 'عملیات',
      enableSorting: false,
      cell: ({ row }) => {
        const addon = row.original
        return (
          <div className="table-actions">
            <button
              type="button"
              className="link-button"
              onClick={() =>
                setAddonForm({
                  id: addon.id,
                  persianName: addon.persianName,
                  englishName: addon.englishName,
                  description: addon.description,
                  price: addon.price,
                  tags: joinTags(addon.tags),
                  displayOrder: addon.displayOrder,
                  isActive: addon.isActive,
                })
              }
            >
              ویرایش
            </button>
            <button
              type="button"
              className="link-button link-danger"
              onClick={async () => {
                if (!window.confirm('از حذف این افزودنی مطمئن هستید؟')) return
                setSaving(true)
                setError('')
                setMessage('')
                try {
                  await apiRequest(`/admin/addons/${addon.id}`, { method: 'DELETE', token: session.token })
                  if (addonForm.id === addon.id) setAddonForm(emptyAddonForm)
                  bumpTableReload()
                  setMessage('افزودنی حذف شد.')
                } catch (requestError) {
                  setError(getReadableError(requestError))
                } finally {
                  setSaving(false)
                }
              }}
            >
              حذف
            </button>
          </div>
        )
      },
    },
  ]
}

export function buildModifierColumns({ toPersianNumber, setModifierForm, emptyModifierForm, bumpTableReload, modifierForm, session, apiRequest, setSaving, setError, setMessage, getReadableError }) {
  return [
    {
      id: 'name',
      header: 'عنوان',
      accessorFn: (row) => `${row.persianName} ${row.englishName}`,
      cell: ({ row }) => (
        <>
          <strong>{row.original.persianName}</strong>
          <div className="table-subtext ltr-text">{row.original.englishName}</div>
        </>
      ),
    },
    {
      accessorKey: 'displayOrder',
      header: 'ترتیب',
      cell: ({ getValue }) => toPersianNumber(getValue()),
    },
    {
      accessorKey: 'isActive',
      header: 'وضعیت',
      cell: ({ getValue }) => (getValue() ? 'فعال' : 'مخفی'),
    },
    {
      id: 'actions',
      header: 'عملیات',
      enableSorting: false,
      cell: ({ row }) => {
        const modifier = row.original
        return (
          <div className="table-actions">
            <button
              type="button"
              className="link-button"
              onClick={() =>
                setModifierForm({
                  id: modifier.id,
                  persianName: modifier.persianName,
                  englishName: modifier.englishName,
                  displayOrder: modifier.displayOrder,
                  isActive: modifier.isActive,
                })
              }
            >
              ویرایش
            </button>
            <button
              type="button"
              className="link-button link-danger"
              onClick={async () => {
                if (!window.confirm('از حذف این گزینه مطمئن هستید؟')) return
                setSaving(true)
                setError('')
                setMessage('')
                try {
                  await apiRequest(`/admin/modifiers/${modifier.id}`, { method: 'DELETE', token: session.token })
                  if (modifierForm.id === modifier.id) setModifierForm(emptyModifierForm)
                  bumpTableReload()
                  setMessage('گزینه تغییر سفارش حذف شد.')
                } catch (requestError) {
                  setError(getReadableError(requestError))
                } finally {
                  setSaving(false)
                }
              }}
            >
              حذف
            </button>
          </div>
        )
      },
    },
  ]
}

export function buildIngredientColumns({ setIngredientForm, emptyIngredientForm, bumpTableReload, ingredientForm, session, apiRequest, setSaving, setError, setMessage, getReadableError, LOCATION_OPTIONS }) {
  return [
    {
      id: 'name',
      header: 'نام',
      accessorFn: (row) => `${row.persianName} ${row.englishName} ${row.description ?? ''}`,
      cell: ({ row }) => (
        <>
          <strong>{row.original.persianName}</strong>
          <div className="table-subtext ltr-text">{row.original.englishName}</div>
          {row.original.description ? <div className="table-subtext">{row.original.description}</div> : null}
        </>
      ),
    },
    {
      accessorKey: 'unit',
      header: 'واحد',
      meta: { className: 'ltr-text' },
    },
    {
      accessorKey: 'kind',
      header: 'نوع',
      cell: ({ getValue }) => (getValue() === 'packaging' ? 'بسته‌بندی' : 'ماده اولیه'),
    },
    {
      id: 'suppliers',
      header: 'تامین‌کنندگان',
      cell: ({ row }) => {
        const names = row.original.supplierNames ?? []
        if (names.length === 0) return '—'
        const preferredId = row.original.preferredSupplierId
        const namesWithStar = names.filter((_, index) => (row.original.supplierIds ?? [])[index] === preferredId)
        const rest = names.filter((_, index) => (row.original.supplierIds ?? [])[index] !== preferredId)
        return (
          <>
            {namesWithStar.length > 0 ? <span className="preferred-supplier">★ {namesWithStar.join('، ')}</span> : null}
            {rest.length > 0 ? <div className="table-subtext">{rest.join('، ')}</div> : null}
          </>
        )
      },
    },
    {
      accessorKey: 'inventoryLocation',
      header: 'انبار',
      cell: ({ getValue }) => LOCATION_OPTIONS.find((o) => o.value === getValue())?.label ?? getValue(),
    },
    {
      accessorKey: 'isActive',
      header: 'وضعیت',
      cell: ({ getValue }) => (getValue() ? 'فعال' : 'غیرفعال'),
    },
    {
      id: 'actions',
      header: 'عملیات',
      enableSorting: false,
      cell: ({ row }) => {
        const ingredient = row.original
        return (
          <div className="table-actions">
            <button
              type="button"
              className="link-button"
              onClick={() =>
                setIngredientForm({
                id: ingredient.id,
                persianName: ingredient.persianName,
                englishName: ingredient.englishName,
                unit: ingredient.unit,
                description: ingredient.description,
                displayOrder: ingredient.displayOrder,
                isActive: ingredient.isActive,
                inventoryLocation: ingredient.inventoryLocation ?? 'main',
                unitCost: ingredient.unitCost ?? '',
                kind: ingredient.kind ?? 'ingredient',
                supplierIds: ingredient.supplierIds ?? [],
                preferredSupplierId: ingredient.preferredSupplierId ?? '',
              })
              }
            >
              ویرایش
            </button>
            <button
              type="button"
              className="link-button link-danger"
              onClick={async () => {
                if (!window.confirm('از حذف این ماده اولیه مطمئن هستید؟')) return
                setSaving(true)
                setError('')
                setMessage('')
                try {
                  await apiRequest(`/admin/ingredients/${ingredient.id}`, { method: 'DELETE', token: session.token })
                  if (ingredientForm.id === ingredient.id) setIngredientForm(emptyIngredientForm)
                  bumpTableReload()
                  setMessage('ماده اولیه حذف شد.')
                } catch (requestError) {
                  setError(getReadableError(requestError))
                } finally {
                  setSaving(false)
                }
              }}
            >
              حذف
            </button>
          </div>
        )
      },
    },
  ]
}

export function buildRecipeColumns({ setRecipeState, session, apiRequest, setSaving, setError, setMessage, getReadableError, recipeState }) {
  return [
    {
      id: 'ingredient',
      header: 'ماده اولیه',
      accessorFn: (row) => `${row.ingredientPersianName} ${row.ingredientEnglishName}`,
      cell: ({ row }) => (
        <>
          <strong>{row.original.ingredientPersianName}</strong>
          <div className="table-subtext ltr-text">{row.original.ingredientEnglishName}</div>
        </>
      ),
    },
    {
      accessorKey: 'quantity',
      header: 'مقدار',
      meta: { className: 'ltr-text' },
    },
    { accessorKey: 'unit', header: 'واحد', meta: { className: 'ltr-text' } },
    {
      id: 'actions',
      header: 'عملیات',
      enableSorting: false,
      cell: ({ row }) => {
        const line = row.original
        return (
          <div className="table-actions">
            <button
              type="button"
              className="link-button"
              onClick={() =>
                setRecipeState((current) => ({
                  ...current,
                  lineForm: { ingredientId: line.ingredientId, quantity: String(line.quantity) },
                }))
              }
            >
              ویرایش مقدار
            </button>
            <button
              type="button"
              className="link-button link-danger"
              onClick={async () => {
                if (!window.confirm(`از حذف "${line.ingredientPersianName}" از دستور پخت مطمئن هستید؟`)) return
                setSaving(true)
                setError('')
                setMessage('')
                try {
                  await apiRequest(`/admin/recipes/${line.id}`, { method: 'DELETE', token: session.token })
                  setRecipeState((current) => ({
                    ...current,
                    lines: current.lines.filter((x) => x.id !== line.id),
                  }))
                  setMessage('ماده از دستور پخت حذف شد.')
                } catch (requestError) {
                  setError(getReadableError(requestError))
                } finally {
                  setSaving(false)
                }
              }}
            >
              حذف
            </button>
          </div>
        )
      },
    },
  ]
}

export function buildInventoryColumns({
  setInventoryAction,
  setRestockForm,
  setAdjustForm,
  setAlertForm,
  emptyRestockForm,
  emptyAdjustForm,
  setStockHistory,
  session,
  apiRequest,
  setError,
  getReadableError,
}) {
  return [
    {
      id: 'name',
      header: 'ماده اولیه',
      accessorFn: (row) => `${row.persianName} ${row.englishName}`,
      cell: ({ row }) => (
        <>
          <strong>{row.original.persianName}</strong>
          <div className="table-subtext ltr-text">{row.original.englishName}</div>
        </>
      ),
    },
    {
      id: 'currentQuantity',
      header: 'موجودی فعلی',
      accessorFn: (row) => row.currentQuantity,
      cell: ({ row }) => (
        <span className="ltr-text">
          {row.original.currentQuantity} {row.original.unit}
        </span>
      ),
      meta: { className: 'ltr-text' },
    },
    {
      id: 'minAlertQuantity',
      header: 'حد هشدار',
      accessorFn: (row) => row.minAlertQuantity,
      cell: ({ row }) => (
        <span className="ltr-text">
          {row.original.minAlertQuantity} {row.original.unit}
        </span>
      ),
      meta: { className: 'ltr-text' },
    },
    {
      id: 'status',
      header: 'وضعیت',
      enableSorting: false,
      cell: ({ row }) => stockStatusBadge(row.original),
    },
    {
      id: 'actions',
      header: 'عملیات',
      enableSorting: false,
      cell: ({ row }) => {
        const item = row.original
        return (
          <div className="table-actions">
            <button
              type="button"
              className="link-button"
              onClick={() => {
                setInventoryAction({ ingredientId: item.ingredientId, mode: 'restock' })
                setRestockForm(emptyRestockForm)
              }}
            >
              شارژ
            </button>
            <button
              type="button"
              className="link-button"
              onClick={() => {
                setInventoryAction({ ingredientId: item.ingredientId, mode: 'adjust' })
                setAdjustForm(emptyAdjustForm)
              }}
            >
              تصحیح
            </button>
            <button
              type="button"
              className="link-button"
              onClick={() => {
                setInventoryAction({ ingredientId: item.ingredientId, mode: 'alert' })
                setAlertForm({ minAlertQuantity: String(item.minAlertQuantity) })
              }}
            >
              حد هشدار
            </button>
            <button
              type="button"
              className="link-button"
              onClick={async () => {
                setInventoryAction({ ingredientId: item.ingredientId, mode: 'history' })
                setStockHistory([])
                try {
                  const history = await apiRequest(`/admin/inventory/${item.ingredientId}/history`, {
                    token: session.token,
                  })
                  setStockHistory(history)
                } catch (requestError) {
                  setError(getReadableError(requestError))
                }
              }}
            >
              تاریخچه
            </button>
          </div>
        )
      },
    },
  ]
}

const CONSUMPTION_STATUS_META = {
  out: { label: 'تمام شده', tone: 'danger' },
  critical: { label: 'خیلی کم (≤۳ روز)', tone: 'danger' },
  low: { label: 'زیر حد آلارم', tone: 'danger' },
  watch: { label: 'مراقبت (≤۷ روز)', tone: 'warn' },
  ok: { label: 'سالم', tone: 'ok' },
}

export function buildInventoryConsumptionColumns({
  toPersianNumber,
  KIND_OPTIONS,
  LOCATION_OPTIONS,
}) {
  return [
    {
      id: 'name',
      header: 'قلم',
      accessorFn: (row) => `${row.persianName} ${row.englishName} ${row.kind}`,
      cell: ({ row }) => (
        <>
          <strong>{row.original.persianName}</strong>
          <div className="table-subtext ltr-text">{row.original.englishName}</div>
          <div className="table-subtext">
            {row.original.kind === 'packaging' ? 'بسته‌بندی' : 'ماده اولیه'} •{' '}
            {LOCATION_OPTIONS.find((o) => o.value === row.original.inventoryLocation)?.label ?? row.original.inventoryLocation}
          </div>
        </>
      ),
    },
    {
      id: 'current',
      header: 'موجودی زنده',
      cell: ({ row }) => (
        <strong>{toPersianNumber(row.original.currentQuantity)} {row.original.unit}</strong>
      ),
    },
    {
      id: 'consumed',
      header: 'مصرف بازه',
      cell: ({ row }) =>
        row.original.consumedQuantity > 0
          ? <>{toPersianNumber(row.original.consumedQuantity)} {row.original.unit}</>
          : '—',
    },
    {
      id: 'dailyAvg',
      header: 'میانگین روزانه',
      cell: ({ row }) =>
        row.original.dailyAvg > 0
          ? <>{toPersianNumber(row.original.dailyAvg)} {row.original.unit}</>
          : '—',
    },
    {
      id: 'daysRemaining',
      header: 'روزهای باقی‌مانده',
      cell: ({ row }) =>
        row.original.daysRemaining != null
          ? <strong>{toPersianNumber(row.original.daysRemaining)} روز</strong>
          : '—',
    },
    {
      id: 'status',
      header: 'وضعیت',
      cell: ({ row }) => {
        const meta = CONSUMPTION_STATUS_META[row.original.status] ?? { label: row.original.status, tone: 'ok' }
        return <span className={`status-badge consumption-badge--${meta.tone}`}>{meta.label}</span>
      },
    },
    {
      id: 'suggested',
      header: 'پیشنهاد سفارش',
      cell: ({ row }) =>
        row.original.suggestedOrderQuantity > 0
          ? <strong>{toPersianNumber(row.original.suggestedOrderQuantity)} {row.original.unit}</strong>
          : '—',
    },
    {
      id: 'purchases',
      header: 'خرید در بازه',
      cell: ({ row }) => toPersianNumber(row.original.purchasesInWindow ?? 0),
    },
  ]
}

export function buildMenuItemPricingColumns({
  toPersianNumber,
  formatMoney,
  markupByItem,
  onMarkupChange,
  onApply,
  bulkPercent,
  formatShamsiDate,
}) {
  function previewPrice(cost, factor, markupPercent) {
    if (!cost || cost <= 0) return null
    const markup = markupPercent === '' || markupPercent == null ? 0 : Number(markupPercent) / 100
    const raw = cost * factor * (1 + markup)
    return Math.ceil(raw / 1000) * 1000
  }

  function markupFor(item) {
    if (item.id in markupByItem) return markupByItem[item.id]
    return item.markupPercent ?? ''
  }

  return [
    {
      id: 'name',
      header: 'قلم منو',
      accessorFn: (row) => `${row.persianName} ${row.englishName}`,
      cell: ({ row }) => (
        <>
          <strong>{row.original.persianName}</strong>
          <div className="table-subtext ltr-text">{row.original.englishName}</div>
          <div className="table-subtext">{row.original.categoryName}</div>
        </>
      ),
    },
    {
      id: 'cost',
      header: 'قیمت تمام‌شده (هر واحد)',
      cell: ({ row }) =>
        row.original.hasRecipe ? (
          <strong>{formatMoney(row.original.costPerServing)}</strong>
        ) : (
          <span className="table-subtext">فرمول تعریف نشده</span>
        ),
    },
    {
      id: 'recipe',
      header: 'سطرهای فرمول',
      cell: ({ row }) => (row.original.hasRecipe ? toPersianNumber(row.original.recipeLineCount) : '—'),
    },
    {
      id: 'current',
      header: 'قیمت فعلی',
      cell: ({ row }) => {
        const parts = []
        if (row.original.smallPrice != null && row.original.smallPrice !== '') {
          parts.push(`ک: ${formatMoney(row.original.smallPrice)}`)
        }
        if (row.original.mediumPrice != null && row.original.mediumPrice !== '') {
          parts.push(`م: ${formatMoney(row.original.mediumPrice)}`)
        }
        if (row.original.largePrice != null && row.original.largePrice !== '') {
          parts.push(`ل: ${formatMoney(row.original.largePrice)}`)
        }
        return parts.length > 0 ? parts.join(' / ') : '—'
      },
    },
    {
      id: 'markup',
      header: 'درصد سود',
      cell: ({ row }) => (
        <input
          className="ltr-input pricing-markup-input"
          type="number"
          min="0"
          step="1"
          placeholder={row.original.markupPercent ?? (bulkPercent ?? '')}
          value={markupFor(row.original)}
          onChange={(event) => onMarkupChange(row.original.id, event.target.value)}
        />
      ),
    },
    {
      id: 'preview',
      header: 'قیمت نهایی (پیش‌نمایش)',
      cell: ({ row }) => {
        const markupPercent = markupFor(row.original)
        const previews = []
        const variants = [
          { label: 'ک', price: row.original.smallPrice, factor: row.original.smallCostShare },
          { label: 'م', price: row.original.mediumPrice, factor: row.original.mediumCostShare },
          { label: 'ل', price: row.original.largePrice, factor: row.original.largeCostShare },
        ]
        for (const variant of variants) {
          if (variant.price == null) continue
          const computed = previewPrice(row.original.costPerServing, variant.factor, markupPercent)
          previews.push(
            <div key={variant.label}>
              {variant.label}: <strong>{formattedMoney(computed, formatMoney)}</strong>
            </div>,
          )
        }
        return row.original.hasRecipe && previews.length > 0 ? previews : '—'
      },
    },
    {
      id: 'actions',
      header: 'عملیات',
      enableSorting: false,
      cell: ({ row }) => (
        <button
          type="button"
          className="link-button"
          disabled={!row.original.hasRecipe}
          onClick={() => onApply({ id: row.original.id, markupPercent: markupFor(row.original) })}
        >
          اعمال
        </button>
      ),
    },
  ]
}

function formattedMoney(value, formatMoney) {
  return value == null ? '—' : formatMoney(value)
}

export function buildStockHistoryColumns({ formatDateTime }) {
  return [
    {
      accessorKey: 'occurredAt',
      header: 'تاریخ',
      cell: ({ getValue }) => <span className="ltr-text">{formatDateTime(getValue())}</span>,
      meta: { className: 'ltr-text' },
    },
    {
      accessorKey: 'movementType',
      header: 'نوع',
      cell: ({ getValue }) => movementTypeLabel(getValue()),
    },
    {
      accessorKey: 'quantity',
      header: 'مقدار',
      cell: ({ getValue }) => {
        const value = getValue()
        return (
          <span className={`ltr-text ${value >= 0 ? 'text-green' : 'text-red'}`}>
            {value >= 0 ? '+' : ''}
            {value}
          </span>
        )
      },
      meta: { className: 'ltr-text' },
    },
    {
      id: 'note',
      header: 'توضیح',
      accessorFn: (row) => row.note ?? row.sourceMenuItemName ?? '—',
      enableSorting: false,
    },
  ]
}

export function buildSalePreviewColumns({ toPersianNumber, quantitySold }) {
  return [
    {
      id: 'name',
      header: 'ماده اولیه',
      accessorFn: (row) => `${row.ingredientPersianName} ${row.ingredientEnglishName}`,
      cell: ({ row }) => (
        <>
          <strong>{row.original.ingredientPersianName}</strong>
          <div className="table-subtext ltr-text">{row.original.ingredientEnglishName}</div>
        </>
      ),
    },
    {
      id: 'perItem',
      header: 'مقدار هر واحد',
      cell: ({ row }) => (
        <span className="ltr-text">
          {row.original.perItemQuantity} {row.original.unit}
        </span>
      ),
      meta: { className: 'ltr-text' },
    },
    {
      id: 'total',
      header: `مجموع کسر (${toPersianNumber(quantitySold || 1)} عدد)`,
      cell: ({ row }) => (
        <span className="ltr-text">
          {row.original.totalQuantity} {row.original.unit}
        </span>
      ),
      meta: { className: 'ltr-text' },
    },
  ]
}

export function buildSaleResultColumns({ toPersianNumber }) {
  return [
    { accessorKey: 'ingredientPersianName', header: 'ماده اولیه' },
    {
      id: 'deducted',
      header: 'کسر شده',
      cell: ({ row }) => (
        <span className="ltr-text">
          {row.original.amountDeducted} {row.original.unit}
        </span>
      ),
      meta: { className: 'ltr-text' },
    },
    {
      id: 'after',
      header: 'موجودی بعد از کسر',
      cell: ({ row }) => (
        <span className={`ltr-text ${row.original.stockAfter < 0 ? 'text-red' : ''}`}>
          {row.original.stockAfter} {row.original.unit}
        </span>
      ),
      meta: { className: 'ltr-text' },
    },
    {
      id: 'status',
      header: 'وضعیت',
      enableSorting: false,
      cell: ({ row }) => {
        const d = row.original
        if (d.wentNegative) return <span className="stock-badge stock-badge--negative">موجودی منفی شد</span>
        if (d.stockAfter === 0) return <span className="stock-badge stock-badge--low">تمام شد</span>
        return <span className="stock-badge stock-badge--ok">سالم</span>
      },
    },
  ]
}

function orderStatusBadge(status) {
  if (status === 'Paid') return <span className="stock-badge stock-badge--ok">پرداخت شده</span>
  if (status === 'PartiallyPaid') return <span className="stock-badge stock-badge--low">پرداخت جزئی</span>
  if (status === 'PendingPayment') return <span className="stock-badge stock-badge--low">در انتظار پرداخت</span>
  if (status === 'Failed') return <span className="stock-badge stock-badge--negative">ناموفق</span>
  if (status === 'Cancelled') return <span className="stock-badge">لغو شده</span>
  return <span className="stock-badge">{status}</span>
}

function fulfillmentStatusBadge(status) {
  if (!status || status === 'None' || status === '') {
    return <span className="fulfillment-badge fulfillment-badge--registered">سفارش ثبت شده</span>
  }
  if (status === 'Registered') return <span className="fulfillment-badge fulfillment-badge--registered">سفارش ثبت شده</span>
  if (status === 'Preparing') return <span className="fulfillment-badge fulfillment-badge--preparing">در حال آماده‌سازی</span>
  if (status === 'Ready') return <span className="fulfillment-badge fulfillment-badge--ready">سفارش آماده</span>
  if (status === 'Completed') return <span className="fulfillment-badge fulfillment-badge--completed">تحویل داده شد</span>
  return <span className="fulfillment-badge">{status}</span>
}

export function buildOrderColumns({ toPersianNumber, formatDateTime, openOrderEditor, updateOrderFulfillment, saving }) {
  return [
    { accessorKey: 'trackingCode', header: 'کد پیگیری', meta: { className: 'ltr-text' } },
    { accessorKey: 'customerName', header: 'مشتری' },
    { accessorKey: 'customerPhone', header: 'تماس', meta: { className: 'ltr-text' } },
    {
      id: 'amount',
      header: 'مبلغ',
      accessorFn: (row) => row.totalAmount,
      cell: ({ row }) => (
        <>
          {formatMoney(row.original.totalAmount)} تومان
          {row.original.isSplitBill ? <div className="table-subtext">تسویه جداگانه</div> : null}
          {row.original.amountPaid > 0 && row.original.amountPaid < row.original.totalAmount ? (
            <div className="table-subtext">پرداخت شده: {formatMoney(row.original.amountPaid)}</div>
          ) : null}
        </>
      ),
    },
    {
      accessorKey: 'status',
      header: 'پرداخت',
      cell: ({ getValue }) => orderStatusBadge(getValue()),
    },
    {
      accessorKey: 'fulfillmentStatus',
      header: 'آماده‌سازی',
      cell: ({ getValue }) => fulfillmentStatusBadge(getValue()),
    },
    {
      accessorKey: 'createdAt',
      header: 'تاریخ',
      cell: ({ getValue }) => formatDateTime(getValue()),
    },
    {
      accessorKey: 'lineCount',
      header: 'اقلام',
      cell: ({ getValue }) => toPersianNumber(getValue()),
    },
    {
      id: 'actions',
      header: 'عملیات',
      enableSorting: false,
      cell: ({ row }) => (
        <div className="order-fulfillment-actions">
          {row.original.status === 'Paid' &&
          (row.original.fulfillmentStatus === 'Registered' ||
            row.original.fulfillmentStatus === 'None' ||
            !row.original.fulfillmentStatus) ? (
            <button
              type="button"
              className="link-button"
              disabled={saving}
              onClick={() => updateOrderFulfillment?.(row.original.id, 'Preparing')}
            >
              شروع آماده‌سازی
            </button>
          ) : null}
          {row.original.status === 'Paid' &&
          (row.original.fulfillmentStatus === 'Registered' ||
            row.original.fulfillmentStatus === 'None' ||
            !row.original.fulfillmentStatus ||
            row.original.fulfillmentStatus === 'Preparing') ? (
            <button
              type="button"
              className="link-button"
              disabled={saving}
              onClick={() => updateOrderFulfillment?.(row.original.id, 'Ready')}
            >
              آماده شد
            </button>
          ) : null}
          {row.original.status === 'Paid' && row.original.fulfillmentStatus === 'Ready' ? (
            <button
              type="button"
              className="link-button"
              disabled={saving}
              onClick={() => updateOrderFulfillment?.(row.original.id, 'Completed')}
            >
              تحویل دادم
            </button>
          ) : null}
          <button type="button" className="link-button" onClick={() => openOrderEditor(row.original)}>
            مدیریت
          </button>
        </div>
      ),
    },
  ]
}

export function buildInventoryPurchaseColumns({
  toPersianNumber,
  formatMoney,
  expirationBadge,
  formatShamsiDate = (value) => value ?? '—',
}) {
  return [
    {
      id: 'ingredient',
      header: 'ماده اولیه',
      accessorFn: (row) => `${row.ingredientPersianName} ${row.ingredientEnglishName}`,
      cell: ({ row }) => (
        <>
          <strong>{row.original.ingredientPersianName}</strong>
          <div className="table-subtext ltr-text">{row.original.ingredientEnglishName}</div>
        </>
      ),
    },
    {
      id: 'quantity',
      header: 'مقدار',
      cell: ({ row }) => (
        <>
          {row.original.packageCount ? (
            <div>{toPersianNumber(row.original.packageCount)} × {toPersianNumber(row.original.packageSize)} {row.original.unit}</div>
          ) : null}
          <strong>{toPersianNumber(row.original.quantity)} {row.original.unit}</strong>
          <div className="table-subtext">باقی‌مانده: {toPersianNumber(row.original.remainingQuantity)}</div>
        </>
      ),
    },
    {
      accessorKey: 'expirationDate',
      header: 'انقضا',
      cell: ({ row }) => (
        <>
          <span className="ltr-text">{formatShamsiDate(row.original.expirationDate)}</span>
          <div>{expirationBadge(row.original.expirationStatus)}</div>
        </>
      ),
    },
    {
      id: 'supplier',
      header: 'تامین‌کننده',
      accessorFn: (row) => row.supplierName ?? '',
      cell: ({ getValue }) => getValue() || '—',
    },
    {
      id: 'costs',
      header: 'هزینه کل',
      cell: ({ row }) => (
        <>
          <strong>{formatMoney(row.original.totalCost)}</strong>
          {row.original.purchaseUnitPrice ? (
            <div className="table-subtext">واحد: {formatMoney(row.original.purchaseUnitPrice)}</div>
          ) : null}
        </>
      ),
    },
    {
      accessorKey: 'description',
      header: 'توضیحات',
      cell: ({ getValue }) => getValue() || '—',
    },
    {
      id: 'document',
      header: 'سند',
      cell: ({ row }) =>
        row.original.documentImageUrl ? (
          <a className="purchase-doc-link ltr-text" href={row.original.documentImageUrl} target="_blank" rel="noreferrer">
            مشاهده
          </a>
        ) : (
          '—'
        ),
    },
  ]
}

export function buildProductionPreviewColumns({ toPersianNumber, quantityProduced }) {
  return [
    {
      id: 'name',
      header: 'ماده اولیه',
      accessorFn: (row) => `${row.ingredientPersianName} ${row.ingredientEnglishName}`,
      cell: ({ row }) => (
        <>
          <strong>{row.original.ingredientPersianName}</strong>
          <div className="table-subtext ltr-text">{row.original.ingredientEnglishName}</div>
        </>
      ),
    },
    {
      id: 'perItem',
      header: 'مقدار هر واحد',
      cell: ({ row }) => (
        <span className="ltr-text">
          {row.original.perItemQuantity} {row.original.unit}
        </span>
      ),
      meta: { className: 'ltr-text' },
    },
    {
      id: 'total',
      header: `مجموع کسر (${toPersianNumber(quantityProduced || 1)} عدد)`,
      cell: ({ row }) => (
        <span className="ltr-text">
          {row.original.totalQuantity ?? row.original.amountDeducted} {row.original.unit}
        </span>
      ),
      meta: { className: 'ltr-text' },
    },
  ]
}

export function buildProductionBatchColumns({ toPersianNumber }) {
  return [
    {
      id: 'menuItem',
      header: 'محصول',
      accessorFn: (row) => `${row.menuItemPersianName} ${row.menuItemEnglishName}`,
      cell: ({ row }) => (
        <>
          <strong>{row.original.menuItemPersianName}</strong>
          <div className="table-subtext ltr-text">{row.original.menuItemEnglishName}</div>
        </>
      ),
    },
    {
      accessorKey: 'quantityProduced',
      header: 'تعداد تولید',
      cell: ({ getValue }) => toPersianNumber(getValue()),
    },
    {
      accessorKey: 'note',
      header: 'یادداشت',
      cell: ({ getValue }) => getValue() || '—',
    },
    {
      accessorKey: 'producedAt',
      header: 'زمان تولید',
      cell: ({ getValue }) =>
        new Intl.DateTimeFormat('fa-IR', { dateStyle: 'medium', timeStyle: 'short' }).format(new Date(getValue())),
    },
  ]
}
