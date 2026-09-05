import { useCallback, useEffect, useMemo, useRef, useState } from 'react'
import { Link } from 'react-router-dom'
import { useAdmin } from '../context/AdminContext'
import { apiRequest, getReadableError } from '../utils/adminApi'
import { formatDateTime, formatDuration, toPersianNumber } from '../utils/adminFormatters'
import { hasPermission, PERMISSIONS, canSubmitPosOrder } from '../utils/adminPermissions'
import { printPosReceipts } from '../utils/posReceipt'
import {
  buildCartLineKey,
  buildUpsertPayload,
  formatPrice,
  orderToCart,
  resolveDefaultSizeKey,
  resolveItemPrice,
} from '../utils/posCart'
import './pos.css'

const EMPTY_SPLIT = { isSplitBill: false, splitBillMode: 'ByItem', guestCount: 2, splitShares: [] }

function ProductModal({ item, modifiers, onClose, onConfirm }) {
  const [sizeKey, setSizeKey] = useState(resolveDefaultSizeKey(item))
  const [selectedModifiers, setSelectedModifiers] = useState([])
  const [baristaNote, setBaristaNote] = useState('')
  const [quantity, setQuantity] = useState(1)

  const unitPrice = resolveItemPrice(item, sizeKey)
  const sizeLabel =
    item.variants?.find((variant) => variant.key === sizeKey)?.label || sizeKey || 'پیش‌فرض'

  function toggleModifier(modifierId) {
    setSelectedModifiers((current) =>
      current.includes(modifierId)
        ? current.filter((entry) => entry !== modifierId)
        : [...current, modifierId],
    )
  }

  return (
    <div className="pos-modal-backdrop" onClick={onClose}>
      <div className="pos-modal" onClick={(event) => event.stopPropagation()}>
        <h3>{item.persianName}</h3>
        <p>{item.description}</p>

        {item.variants?.length ? (
          <div>
            <label>اندازه</label>
            <div className="pos-modifier-list">
              {item.variants.map((variant) => (
                <button
                  key={variant.key}
                  type="button"
                  className={`pos-modifier-chip ${sizeKey === variant.key ? 'selected' : ''}`}
                  onClick={() => setSizeKey(variant.key)}
                >
                  {variant.label}
                </button>
              ))}
            </div>
          </div>
        ) : null}

        {modifiers.length ? (
          <div>
            <label>تغییرات</label>
            <div className="pos-modifier-list">
              {modifiers.map((modifier) => (
                <button
                  key={modifier.id}
                  type="button"
                  className={`pos-modifier-chip ${selectedModifiers.includes(modifier.id) ? 'selected' : ''}`}
                  onClick={() => toggleModifier(modifier.id)}
                >
                  {modifier.persianName}
                </button>
              ))}
            </div>
          </div>
        ) : null}

        <label>
          توضیح برای باریستا
          <textarea
            rows={2}
            value={baristaNote}
            onChange={(event) => setBaristaNote(event.target.value)}
            placeholder="مثلاً: شکر کم، سس اضافه"
          />
        </label>

        <div className="pos-cart-line-actions">
          <button type="button" className="pos-qty-button" onClick={() => setQuantity((value) => Math.max(1, value - 1))}>
            −
          </button>
          <span>{toPersianNumber(quantity)}</span>
          <button type="button" className="pos-qty-button" onClick={() => setQuantity((value) => value + 1)}>
            +
          </button>
          <strong>{formatPrice(unitPrice * quantity)} تومان</strong>
        </div>

        <div className="pos-actions">
          <button type="button" className="pos-secondary-button" onClick={onClose}>
            انصراف
          </button>
          <button
            type="button"
            className="pos-primary-button"
            onClick={() =>
              onConfirm({
                menuItemId: item.id,
                name: item.persianName,
                imageUrl: item.imageUrl,
                sizeKey,
                sizeLabel,
                quantity,
                unitPrice,
                modifierIds: selectedModifiers,
                modifierLabels: modifiers
                  .filter((modifier) => selectedModifiers.includes(modifier.id))
                  .map((modifier) => modifier.persianName),
                baristaNote,
              })
            }
          >
            افزودن به سفارش
          </button>
        </div>
      </div>
    </div>
  )
}

export function PosPage() {
  const { session, siteSettings } = useAdmin()
  const searchRef = useRef(null)

  const [bootstrap, setBootstrap] = useState(null)
  const [loading, setLoading] = useState(true)
  const [submitting, setSubmitting] = useState(false)
  const [error, setError] = useState('')
  const [message, setMessage] = useState('')

  const [search, setSearch] = useState('')
  const [activeCategory, setActiveCategory] = useState('all')
  const [sidePanel, setSidePanel] = useState('cart')
  const [productModal, setProductModal] = useState(null)

  const [orderId, setOrderId] = useState(null)
  const [trackingCode, setTrackingCode] = useState('')
  const [cart, setCart] = useState([])
  const [serviceType, setServiceType] = useState('DineIn')
  const [customerName, setCustomerName] = useState('')
  const [customerPhone, setCustomerPhone] = useState('')
  const [orderNote, setOrderNote] = useState('')
  const [salonTableId, setSalonTableId] = useState(null)
  const [splitBill, setSplitBill] = useState(EMPTY_SPLIT)

  const canSubmit = canSubmitPosOrder(session, orderId)
  const canDelete = hasPermission(session, PERMISSIONS.pos.delete) || hasPermission(session, PERMISSIONS.orders.delete)

  const loadBootstrap = useCallback(async () => {
    setLoading(true)
    setError('')
    try {
      const payload = await apiRequest('/admin/pos/bootstrap', { token: session.token })
      setBootstrap(payload)
    } catch (loadError) {
      setError(getReadableError(loadError))
    } finally {
      setLoading(false)
    }
  }, [session.token])

  useEffect(() => {
    loadBootstrap()
  }, [loadBootstrap])

  useEffect(() => {
    function onKeyDown(event) {
      if (event.target.matches('input, textarea, select') && event.key !== 'F2' && event.key !== 'F4' && event.key !== 'F5') {
        return
      }

      if (event.key === '/') {
        event.preventDefault()
        searchRef.current?.focus()
      }

      if (event.key === 'F2' && cart.length > 0) {
        event.preventDefault()
        submitOrder('Hold')
      }

      if (event.key === 'F4' && cart.length > 0 && canSubmit) {
        event.preventDefault()
        submitOrder('PayCash')
      }

      if (event.key === 'F5' && cart.length > 0 && canSubmit) {
        event.preventDefault()
        submitOrder('PayCard')
      }
    }

    window.addEventListener('keydown', onKeyDown)
    return () => window.removeEventListener('keydown', onKeyDown)
  })

  const categories = bootstrap?.menu?.categories ?? []
  const items = bootstrap?.menu?.items ?? []
  const modifiers = bootstrap?.menu?.modifiers ?? []

  const filteredItems = useMemo(() => {
    const query = search.trim().toLowerCase()
    return items.filter((item) => {
      if (!item.isOrderable) {
        return false
      }

      if (activeCategory !== 'all' && item.category !== activeCategory) {
        return false
      }

      if (!query) {
        return true
      }

      return [item.persianName, item.englishName, item.name, item.categoryLabel]
        .filter(Boolean)
        .some((value) => value.toLowerCase().includes(query))
    })
  }, [items, search, activeCategory])

  const cartQtyByItem = useMemo(() => {
    const counts = new Map()
    cart.forEach((line) => {
      counts.set(line.menuItemId, (counts.get(line.menuItemId) ?? 0) + line.quantity)
    })
    return counts
  }, [cart])

  const cartTotal = useMemo(
    () => cart.reduce((total, line) => total + line.unitPrice * line.quantity, 0),
    [cart],
  )

  const selectedTable = bootstrap?.tables?.find((table) => table.id === salonTableId) ?? null

  function resetCart() {
    setOrderId(null)
    setTrackingCode('')
    setCart([])
    setCustomerName('')
    setCustomerPhone('')
    setOrderNote('')
    setSalonTableId(null)
    setSplitBill(EMPTY_SPLIT)
  }

  function addToCart(line) {
    const cartKey = buildCartLineKey(line.menuItemId, line.sizeKey, line.modifierIds)
    setCart((current) => {
      const existing = current.find((entry) => entry.cartKey === cartKey)
      if (existing) {
        return current.map((entry) =>
          entry.cartKey === cartKey
            ? {
                ...entry,
                quantity: entry.quantity + line.quantity,
                baristaNote: line.baristaNote || entry.baristaNote,
              }
            : entry,
        )
      }

      return [...current, { ...line, cartKey }]
    })
    setProductModal(null)
  }

  function updateLineQuantity(cartKey, delta) {
    setCart((current) =>
      current
        .map((line) =>
          line.cartKey === cartKey ? { ...line, quantity: line.quantity + delta } : line,
        )
        .filter((line) => line.quantity > 0),
    )
  }

  function updateLineNote(cartKey, baristaNote) {
    setCart((current) =>
      current.map((line) => (line.cartKey === cartKey ? { ...line, baristaNote } : line)),
    )
  }

  function removeLine(cartKey) {
    setCart((current) => current.filter((line) => line.cartKey !== cartKey))
  }

  async function loadOrder(id) {
    setSubmitting(true)
    setError('')
    try {
      const order = await apiRequest(`/admin/pos/orders/${id}`, { token: session.token })
      setOrderId(order.id)
      setTrackingCode(order.trackingCode)
      setCustomerName(order.customerName === 'مهمان' ? '' : order.customerName)
      setCustomerPhone(order.customerPhone === 'POS' ? '' : order.customerPhone)
      setOrderNote(order.note ?? '')
      setServiceType(order.serviceType ?? 'TakeAway')
      setSalonTableId(order.salonTableId ?? null)
      setCart(orderToCart(order, items))
      setSplitBill({
        isSplitBill: order.isSplitBill,
        splitBillMode: order.splitBillMode ?? 'ByItem',
        guestCount: order.guestCount ?? 2,
        splitShares: order.splitShares ?? [],
      })
      setSidePanel('cart')
      setMessage(`سفارش ${order.trackingCode} بارگذاری شد.`)
    } catch (loadError) {
      setError(getReadableError(loadError))
    } finally {
      setSubmitting(false)
    }
  }

  async function submitOrder(action) {
    if (!cart.length) {
      setError('حداقل یک آیتم به سفارش اضافه کنید.')
      setMessage('')
      return
    }

    if (!canSubmit) {
      setError('مجوز ثبت یا ویرایش سفارش در صندوق را ندارید. از مدیر بخواهید مجوزهای صندوق فروش را فعال کند.')
      setMessage('')
      return
    }

    setSubmitting(true)
    setError('')
    setMessage('')

    const payload = buildUpsertPayload({
      orderId,
      customerName,
      customerPhone,
      note: orderNote,
      serviceType,
      salonTableId,
      cart,
      action,
      splitBill: splitBill.isSplitBill,
      splitBillMode: splitBill.splitBillMode,
      guestCount: splitBill.guestCount,
      splitShares: splitBill.splitShares,
    })

    try {
      const result = orderId
        ? await apiRequest(`/admin/pos/orders/${orderId}`, {
            method: 'PUT',
            body: payload,
            token: session.token,
          })
        : await apiRequest('/admin/pos/orders', {
            method: 'POST',
            body: payload,
            token: session.token,
          })

      if (action === 'Hold') {
        setMessage(`سفارش ${result.trackingCode} نگه‌داشته شد.`)
        resetCart()
      } else {
        const cartSnapshot = cart.map((line) => ({ ...line }))
        try {
          const printResult = await printPosReceipts({
            result,
            cart: cartSnapshot,
            customerName,
            customerPhone,
            orderNote,
            serviceType,
            tableName: selectedTable?.name ?? null,
            siteSettings,
          })
          const printLabel = printResult.method === 'agent' ? 'چاپ خودکار' : 'چاپ مرورگر'
          setMessage(`پرداخت سفارش ${result.trackingCode} ثبت شد و رسید مشتری و باریستا (${printLabel}) ارسال شد.`)
        } catch (printError) {
          console.error(printError)
          setMessage(`پرداخت سفارش ${result.trackingCode} ثبت شد، اما چاپ رسید با خطا مواجه شد.`)
          setError(getReadableError(printError))
        }
        resetCart()
      }

      await loadBootstrap()
    } catch (submitError) {
      setError(getReadableError(submitError))
    } finally {
      setSubmitting(false)
    }
  }

  async function cancelCurrentOrder() {
    if (!orderId) {
      resetCart()
      return
    }

    if (!canDelete) {
      setError('مجوز حذف سفارش ندارید.')
      return
    }

    if (!window.confirm('این سفارش حذف شود؟')) {
      return
    }

    setSubmitting(true)
    setError('')
    try {
      await apiRequest(`/admin/pos/orders/${orderId}`, {
        method: 'DELETE',
        token: session.token,
      })
      setMessage('سفارش حذف شد.')
      resetCart()
      await loadBootstrap()
    } catch (cancelError) {
      setError(getReadableError(cancelError))
    } finally {
      setSubmitting(false)
    }
  }

  async function copyOrder(id) {
    setSubmitting(true)
    setError('')
    try {
      const order = await apiRequest(`/admin/pos/orders/${id}/copy`, {
        method: 'POST',
        token: session.token,
      })
      await loadOrder(order.id)
      setMessage(`کپی سفارش ${order.trackingCode} آماده ویرایش است.`)
      await loadBootstrap()
    } catch (copyError) {
      setError(getReadableError(copyError))
    } finally {
      setSubmitting(false)
    }
  }

  function quickAddProduct(item) {
    const sizeKey = resolveDefaultSizeKey(item)
    addToCart({
      menuItemId: item.id,
      name: item.persianName,
      imageUrl: item.imageUrl,
      sizeKey,
      sizeLabel: item.variants?.find((variant) => variant.key === sizeKey)?.label ?? null,
      quantity: 1,
      unitPrice: resolveItemPrice(item, sizeKey),
      modifierIds: [],
      modifierLabels: [],
      baristaNote: '',
    })
  }

  if (loading && !bootstrap) {
    return <div className="pos-layout"><p className="pos-feedback">در حال بارگذاری صندوق...</p></div>
  }

  return (
    <div className="pos-layout">
      <div className="pos-shell">
        <section className="pos-main">
          <header className="pos-header">
            <div className="pos-brand">
              <div>
                <h1>صندوق فروش</h1>
                <p>{new Date().toLocaleDateString('fa-IR', { weekday: 'long', day: 'numeric', month: 'long' })}</p>
              </div>
            </div>
            <div className="pos-header-actions">
              <Link to="/dashboard" className="pos-secondary-button">بازگشت به پنل</Link>
              <button type="button" className="pos-secondary-button" onClick={loadBootstrap} disabled={loading}>
                تازه‌سازی
              </button>
            </div>
          </header>

          <div className="pos-search">
            <input
              ref={searchRef}
              type="search"
              placeholder="جستجوی سریع محصول..."
              value={search}
              onChange={(event) => setSearch(event.target.value)}
            />
            <kbd>/</kbd>
          </div>

          <div className="pos-categories">
            <button
              type="button"
              className={`pos-category-card ${activeCategory === 'all' ? 'pos-category-card-active' : ''}`}
              onClick={() => setActiveCategory('all')}
            >
              همه
              <span>{toPersianNumber(items.filter((item) => item.isOrderable).length)} محصول</span>
            </button>
            {categories.map((category) => {
              const count = items.filter((item) => item.category === category.slug && item.isOrderable).length
              return (
                <button
                  key={category.id}
                  type="button"
                  className={`pos-category-card ${activeCategory === category.slug ? 'pos-category-card-active' : ''}`}
                  onClick={() => setActiveCategory(category.slug)}
                >
                  {category.label}
                  <span>{toPersianNumber(count)} محصول</span>
                </button>
              )
            })}
          </div>

          <div className="pos-products">
            {filteredItems.map((item) => (
              <article key={item.id} className="pos-product-card">
                {cartQtyByItem.get(item.id) ? (
                  <span className="pos-product-qty">{toPersianNumber(cartQtyByItem.get(item.id))}</span>
                ) : null}
                {item.imageUrl ? (
                  <img src={item.imageUrl} alt={item.persianName} loading="lazy" />
                ) : (
                  <div className="pos-product-placeholder">☕</div>
                )}
                <h3>{item.persianName}</h3>
                <p>{formatPrice(resolveItemPrice(item, resolveDefaultSizeKey(item)))} تومان</p>
                <button
                  type="button"
                  className="pos-product-add"
                  onClick={() =>
                    item.variants?.length || modifiers.length
                      ? setProductModal(item)
                      : quickAddProduct(item)
                  }
                  aria-label={`افزودن ${item.persianName}`}
                >
                  +
                </button>
              </article>
            ))}
          </div>

          <p className="pos-shortcuts">
            میانبرها: <strong>/</strong> جستجو · <strong>F2</strong> نگه‌داشت · <strong>F4</strong> پرداخت نقد · <strong>F5</strong> پرداخت کارت
          </p>
        </section>

        <aside className="pos-sidebar">
          <div className="pos-sidebar-head">
            <h2>{trackingCode ? `سفارش #${trackingCode}` : 'سفارش جدید'}</h2>
            <div className="pos-service-toggle">
              <button
                type="button"
                className={serviceType === 'DineIn' ? 'active' : ''}
                onClick={() => setServiceType('DineIn')}
              >
                داخل سالن
              </button>
              <button
                type="button"
                className={serviceType === 'TakeAway' ? 'active' : ''}
                onClick={() => setServiceType('TakeAway')}
              >
                بیرون‌بر
              </button>
            </div>

            <div className="pos-form-grid">
              <input
                type="text"
                placeholder="نام مشتری"
                value={customerName}
                onChange={(event) => setCustomerName(event.target.value)}
              />
              {serviceType === 'DineIn' ? (
                <input
                  type="text"
                  placeholder={selectedTable ? `میز: ${selectedTable.name}` : 'میز را از پایین انتخاب کنید'}
                  value={selectedTable?.name ?? ''}
                  readOnly
                />
              ) : (
                <input
                  type="text"
                  placeholder="شماره تماس (اختیاری)"
                  value={customerPhone}
                  onChange={(event) => setCustomerPhone(event.target.value)}
                />
              )}
              <textarea
                rows={2}
                placeholder="یادداشت سفارش"
                value={orderNote}
                onChange={(event) => setOrderNote(event.target.value)}
              />
              <label>
                <input
                  type="checkbox"
                  checked={splitBill.isSplitBill}
                  onChange={(event) =>
                    setSplitBill((current) => ({ ...current, isSplitBill: event.target.checked }))
                  }
                />{' '}
                تسویه جداگانه
              </label>
            </div>

            <div className="pos-panel-toggle" style={{ marginTop: 12 }}>
              <button type="button" className="pos-secondary-button" onClick={() => setSidePanel('cart')}>سبد</button>
              <button type="button" className="pos-secondary-button" onClick={() => setSidePanel('tables')}>میزها</button>
              <button type="button" className="pos-secondary-button" onClick={() => setSidePanel('held')}>نگه‌داشته</button>
              <button type="button" className="pos-secondary-button" onClick={() => setSidePanel('recent')}>اخیر</button>
            </div>
          </div>

          {sidePanel === 'cart' ? (
            <div className="pos-cart-lines">
              {cart.length === 0 ? <p>سبد خالی است.</p> : null}
              {cart.map((line) => (
                <div key={line.cartKey} className="pos-cart-line">
                  {line.imageUrl ? (
                    <img src={line.imageUrl} alt="" className="pos-cart-thumb" />
                  ) : (
                    <div className="pos-cart-thumb" />
                  )}
                  <div>
                    <h4>{line.name}</h4>
                    <small>
                      {line.sizeLabel ? `${line.sizeLabel} · ` : ''}
                      {formatPrice(line.unitPrice)} × {toPersianNumber(line.quantity)}
                    </small>
                    {line.modifierLabels?.length ? (
                      <small>{line.modifierLabels.join('، ')}</small>
                    ) : null}
                    <input
                      type="text"
                      placeholder="توضیح باریستا"
                      value={line.baristaNote ?? ''}
                      onChange={(event) => updateLineNote(line.cartKey, event.target.value)}
                    />
                    <div className="pos-cart-line-actions">
                      <button type="button" className="pos-qty-button" onClick={() => updateLineQuantity(line.cartKey, -1)}>−</button>
                      <span>{toPersianNumber(line.quantity)}</span>
                      <button type="button" className="pos-qty-button" onClick={() => updateLineQuantity(line.cartKey, 1)}>+</button>
                      <button type="button" className="pos-danger-button" onClick={() => removeLine(line.cartKey)}>حذف</button>
                    </div>
                  </div>
                </div>
              ))}
            </div>
          ) : null}

          {sidePanel === 'tables' ? (
            <div className="pos-cart-lines">
              <div className="pos-table-grid">
                {(bootstrap?.tables ?? []).filter((table) => table.isActive).map((table) => (
                  <button
                    key={table.id}
                    type="button"
                    className={`pos-table-card ${salonTableId === table.id ? 'selected' : ''} ${table.isOccupied ? 'occupied' : ''}`}
                    onClick={() => {
                      setSalonTableId(table.id)
                      setServiceType('DineIn')
                      setSidePanel('cart')
                    }}
                  >
                    <h4>{table.name}</h4>
                    <p>ظرفیت {toPersianNumber(table.capacity)}</p>
                    <p>{table.isOccupied ? `اشغال · ${formatDuration(table.durationMinutes)}` : 'آزاد'}</p>
                    {table.currentBalance > 0 ? (
                      <p>مانده: {formatPrice(table.currentBalance)} تومان</p>
                    ) : null}
                  </button>
                ))}
              </div>
            </div>
          ) : null}

          {sidePanel === 'held' ? (
            <div className="pos-cart-lines">
              <div className="pos-held-list">
                {(bootstrap?.heldOrders ?? []).length === 0 ? <p>سفارش نگه‌داشته‌شده‌ای نیست.</p> : null}
                {(bootstrap?.heldOrders ?? []).map((order) => (
                  <div key={order.id} className="pos-held-item">
                    <div>
                      <strong>#{order.trackingCode}</strong>
                      <p>{order.customerName} · {toPersianNumber(order.lineCount)} قلم</p>
                      <small>{formatPrice(order.totalAmount)} تومان</small>
                    </div>
                    <div className="pos-panel-toggle">
                      <button type="button" className="pos-primary-button" onClick={() => loadOrder(order.id)}>ادامه</button>
                      <button type="button" className="pos-secondary-button" onClick={() => copyOrder(order.id)}>کپی</button>
                    </div>
                  </div>
                ))}
              </div>
            </div>
          ) : null}

          {sidePanel === 'recent' ? (
            <div className="pos-cart-lines">
              <div className="pos-recent-list">
                {(bootstrap?.recentOrders ?? []).map((order) => (
                  <div key={order.id} className="pos-held-item">
                    <div>
                      <strong>#{order.trackingCode}</strong>
                      <p>{order.customerName} · {order.status}</p>
                      <small>{formatDateTime(order.createdAt)}</small>
                    </div>
                    <div className="pos-panel-toggle">
                      <button type="button" className="pos-primary-button" onClick={() => loadOrder(order.id)}>بارگذاری</button>
                      <button type="button" className="pos-secondary-button" onClick={() => copyOrder(order.id)}>سفارش مجدد</button>
                    </div>
                  </div>
                ))}
              </div>
            </div>
          ) : null}

          <div className="pos-cart-footer">
            {error ? <p className="pos-action-feedback pos-feedback-error">{error}</p> : null}
            {message ? <p className="pos-action-feedback pos-feedback-success">{message}</p> : null}
            {!canSubmit ? (
              <p className="pos-action-feedback pos-feedback-error">
                برای ثبت سفارش به مجوز «ثبت / نگه‌داشت» یا «ویرایش سفارش» در صندوق فروش نیاز دارید.
              </p>
            ) : null}
            <div className="pos-total-row">
              <span>جمع کل</span>
              <span>{formatPrice(cartTotal)} تومان</span>
            </div>
            <div className="pos-actions">
              <button
                type="button"
                className="pos-secondary-button"
                onClick={() => submitOrder('Hold')}
                disabled={submitting || !cart.length}
              >
                نگه‌داشت (F2)
              </button>
              <button type="button" className="pos-danger-button" onClick={cancelCurrentOrder} disabled={submitting}>
                {orderId ? 'حذف سفارش' : 'پاک کردن سبد'}
              </button>
              <button
                type="button"
                className="pos-primary-button"
                onClick={() => submitOrder('PayCash')}
                disabled={submitting || !cart.length}
              >
                نقد (F4)
              </button>
              <button
                type="button"
                className="pos-primary-button"
                onClick={() => submitOrder('PayCard')}
                disabled={submitting || !cart.length}
              >
                کارت (F5)
              </button>
            </div>
          </div>
        </aside>
      </div>

      {productModal ? (
        <ProductModal
          item={productModal}
          modifiers={modifiers}
          onClose={() => setProductModal(null)}
          onConfirm={addToCart}
        />
      ) : null}
    </div>
  )
}
