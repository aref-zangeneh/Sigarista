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
          {toPersianNumber(row.original.price)}
          {row.original.specialOfferPrice ? (
            <div className="table-subtext">پیشنهاد: {toPersianNumber(row.original.specialOfferPrice)}</div>
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

export function buildAddonColumns({ toPersianNumber, joinTags, setAddonForm, emptyAddonForm, setAddonItems, addonForm, session, apiRequest, setSaving, setError, setMessage, getReadableError }) {
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
      cell: ({ getValue }) => toPersianNumber(getValue()),
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
                  setAddonItems((current) => current.filter((x) => x.id !== addon.id))
                  if (addonForm.id === addon.id) setAddonForm(emptyAddonForm)
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

export function buildModifierColumns({ toPersianNumber, setModifierForm, emptyModifierForm, setModifiers, modifierForm, session, apiRequest, setSaving, setError, setMessage, getReadableError }) {
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
                  setModifiers((current) => current.filter((x) => x.id !== modifier.id))
                  if (modifierForm.id === modifier.id) setModifierForm(emptyModifierForm)
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

export function buildIngredientColumns({ setIngredientForm, emptyIngredientForm, setIngredients, ingredientForm, session, apiRequest, setSaving, setError, setMessage, getReadableError, LOCATION_OPTIONS }) {
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
    { accessorKey: 'unit', header: 'واحد', meta: { className: 'ltr-text' } },
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
                  setIngredients((current) => current.filter((x) => x.id !== ingredient.id))
                  if (ingredientForm.id === ingredient.id) setIngredientForm(emptyIngredientForm)
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
  if (status === 'PendingPayment') return <span className="stock-badge stock-badge--low">در انتظار پرداخت</span>
  if (status === 'Failed') return <span className="stock-badge stock-badge--negative">ناموفق</span>
  if (status === 'Cancelled') return <span className="stock-badge">لغو شده</span>
  return <span className="stock-badge">{status}</span>
}

export function buildOrderColumns({ toPersianNumber, formatDateTime, setSelectedOrder }) {
  return [
    { accessorKey: 'trackingCode', header: 'کد پیگیری', meta: { className: 'ltr-text' } },
    { accessorKey: 'customerName', header: 'مشتری' },
    { accessorKey: 'customerPhone', header: 'تماس', meta: { className: 'ltr-text' } },
    {
      accessorKey: 'totalAmount',
      header: 'مبلغ',
      cell: ({ getValue }) => `${toPersianNumber(getValue())} تومان`,
    },
    {
      accessorKey: 'status',
      header: 'وضعیت',
      cell: ({ getValue }) => orderStatusBadge(getValue()),
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
        <button type="button" className="link-button" onClick={() => setSelectedOrder(row.original)}>
          جزئیات
        </button>
      ),
    },
  ]
}
