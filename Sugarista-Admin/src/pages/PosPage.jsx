import { useCallback, useEffect, useMemo, useRef, useState } from 'react'
import { Link } from 'react-router-dom'
import { useAdmin } from '../context/AdminContext'
import { apiRequest, getReadableError } from '../utils/adminApi'
import { formatDateTime, formatDuration, toPersianNumber } from '../utils/adminFormatters'
import { hasPermission, PERMISSIONS, canSubmitPosOrder } from '../utils/adminPermissions'
import { printPosReceipts } from '../utils/posReceipt'
import { Tip } from '../components/Tip'
import { useEscapeKey } from '../hooks/useEscapeKey'
import {
  buildCartLineKey,
  buildUpsertPayload,
  formatPrice,
  computeCartTotals,
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

  useEscapeKey(true, onClose)

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
      <div
        className="pos-modal"
        role="dialog"
        aria-modal="true"
        aria-labelledby="pos-product-modal-title"
        onClick={(event) => event.stopPropagation()}
      >
        <div className="pos-modal-head">
          <div>
            <h3 id="pos-product-modal-title">{item.persianName}</h3>
            {item.description ? <p>{item.description}</p> : null}
          </div>
          <button type="button" className="pos-modal-close" onClick={onClose} aria-label="بستن">
            ✕
          </button>
        </div>

        {item.variants?.length ? (
          <div className="pos-modal-section">
            <label>اندازه</label>
            <div className="pos-modifier-list">
              {item.variants.map((variant) => {
                const variantPrice = resolveItemPrice(item, variant.key)
                return (
                  <button
                    key={variant.key}
                    type="button"
                    className={`pos-modifier-chip ${sizeKey === variant.key ? 'selected' : ''}`}
                    onClick={() => setSizeKey(variant.key)}
                  >
                    <span>{variant.label}</span>
                    <small>{formatPrice(variantPrice)} ت</small>
                  </button>
                )
              })}
            </div>
          </div>
        ) : null}

        {modifiers.length ? (
          <div className="pos-modal-section">
            <label>تغییرات آماده‌سازی</label>
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

        <div className="pos-modal-section">
          <label htmlFor="pos-modal-barista-note">توضیح برای باریستا</label>
          <textarea
            id="pos-modal-barista-note"
            rows={2}
            value={baristaNote}
            onChange={(event) => setBaristaNote(event.target.value)}
            placeholder="مثلاً: شکر کم، سس اضافه، داغ سرو شود..."
          />
        </div>

        <div className="pos-modal-qty-bar">
          <div className="pos-qty-stepper">
            <button
              type="button"
              className="pos-qty-button"
              onClick={() => setQuantity((value) => Math.max(1, value - 1))}
              aria-label="کاهش تعداد"
            >
              −
            </button>
            <span>{toPersianNumber(quantity)}</span>
            <button
              type="button"
              className="pos-qty-button"
              onClick={() => setQuantity((value) => value + 1)}
              aria-label="افزایش تعداد"
            >
              +
            </button>
          </div>
          <strong className="pos-modal-total">{formatPrice(unitPrice * quantity)} تومان</strong>
        </div>

        <div className="pos-modal-actions">
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
                taxPercent: item.taxPercent ?? 0,
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
  const [mobilePane, setMobilePane] = useState('catalog') // 'catalog' | 'order'
  const [productModal, setProductModal] = useState(null)
  const [expandedLineNoteKey, setExpandedLineNoteKey] = useState(null)

  const [orderId, setOrderId] = useState(null)
  const [trackingCode, setTrackingCode] = useState('')
  const [cart, setCart] = useState([])
  const [serviceType, setServiceType] = useState('DineIn')
  const [customerName, setCustomerName] = useState('')
  const [customerPhone, setCustomerPhone] = useState('')
  const [orderNote, setOrderNote] = useState('')
  const [personalCup, setPersonalCup] = useState(false)
  const [useSubscription, setUseSubscription] = useState(false)
  const [salonTableId, setSalonTableId] = useState(null)
  const [deliveryMethodId, setDeliveryMethodId] = useState('')
  const [deliveryOptions, setDeliveryOptions] = useState([])
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

    apiRequest('/admin/delivery-methods/options', { token: session.token })
      .then((data) => setDeliveryOptions(Array.isArray(data) ? data : []))
      .catch(() => setDeliveryOptions([]))
  }, [loadBootstrap, session.token])

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
  const activeTables = (bootstrap?.tables ?? []).filter((table) => table.isActive)
  const heldOrders = bootstrap?.heldOrders ?? []
  const recentOrders = bootstrap?.recentOrders ?? []

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

  const totalItemsCount = useMemo(
    () => cart.reduce((sum, line) => sum + line.quantity, 0),
    [cart],
  )

  const selectedDeliveryMethod = (deliveryOptions ?? []).find(
    (option) => option.id === deliveryMethodId,
  ) ?? null
  const deliveryFee = serviceType === 'TakeAway' ? (selectedDeliveryMethod?.deliveryFee ?? 0) : 0
  const cartTotals = useMemo(() => computeCartTotals(cart, deliveryFee), [cart, deliveryFee])

  const selectedTable = bootstrap?.tables?.find((table) => table.id === salonTableId) ?? null

  function resetCart() {
    setOrderId(null)
    setTrackingCode('')
    setCart([])
    setCustomerName('')
    setCustomerPhone('')
    setOrderNote('')
    setPersonalCup(false)
    setUseSubscription(false)
    setSalonTableId(null)
    setDeliveryMethodId('')
    setSplitBill(EMPTY_SPLIT)
    setExpandedLineNoteKey(null)
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
    if (expandedLineNoteKey === cartKey) {
      setExpandedLineNoteKey(null)
    }
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
      setDeliveryMethodId(order.deliveryMethodId ?? '')
      setSalonTableId(order.salonTableId ?? null)
      setCart(orderToCart(order, items))
      setSplitBill({
        isSplitBill: Boolean(order.isSplitBill),
        splitBillMode: order.splitBillMode ?? 'ByItem',
        guestCount: order.guestCount ?? 2,
        splitShares: order.splitShares ?? [],
      })
      setSidePanel('cart')
      setMobilePane('order')
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
      deliveryMethodId: serviceType === 'TakeAway' ? (deliveryMethodId || null) : null,
      cart,
      action,
      splitBill: splitBill.isSplitBill,
      splitBillMode: splitBill.splitBillMode,
      guestCount: splitBill.guestCount,
      splitShares: splitBill.splitShares,
      personalCup,
      useSubscription,
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
      taxPercent: item.taxPercent ?? 0,
      modifierIds: [],
      modifierLabels: [],
      baristaNote: '',
    })
  }

  if (loading && !bootstrap) {
    return (
      <div className="pos-layout">
        <p className="pos-feedback">در حال بارگذاری صندوق فروش...</p>
      </div>
    )
  }

  const orderableCount = items.filter((item) => item.isOrderable).length

  return (
    <div className="pos-layout">
      <div className={`pos-shell pos-shell--mobile-${mobilePane}`}>
        {/* ── ستون اصلی: کاتالوگ محصولات، جستجو و دسته‌بندی‌ها ─────────── */}
        <section className="pos-main">
          <header className="pos-header">
            <div className="pos-brand">
              <div>
                <h1>صندوق فروش</h1>
                <p>
                  {new Date().toLocaleDateString('fa-IR', {
                    weekday: 'long',
                    day: 'numeric',
                    month: 'long',
                    calendar: 'persian',
                    numberingSystem: 'arabext',
                    timeZone: 'Asia/Tehran',
                  })}
                </p>
              </div>
            </div>

            <div className="pos-search-row">
              <div className="pos-search">
                <span className="pos-search-icon" aria-hidden="true">🔍</span>
                <input
                  ref={searchRef}
                  type="search"
                  placeholder="جستجوی سریع محصول (نام فارسی یا انگلیسی)..."
                  value={search}
                  onChange={(event) => setSearch(event.target.value)}
                />
                {search ? (
                  <button
                    type="button"
                    className="pos-search-clear"
                    onClick={() => setSearch('')}
                    aria-label="پاک کردن جستجو"
                  >
                    ✕
                  </button>
                ) : (
                  <kbd>/</kbd>
                )}
              </div>
              <Tip k="pos.search" />
            </div>

            <div className="pos-header-actions">
              <button
                type="button"
                className="pos-secondary-button pos-btn-sm"
                onClick={loadBootstrap}
                disabled={loading}
              >
                {loading ? '...' : 'تازه‌سازی'}
              </button>
              <Tip k="pos.refresh" />
              <Link to="/dashboard" className="pos-secondary-button pos-btn-sm">
                بازگشت به پنل
              </Link>
            </div>
          </header>

          {/* ── نوار دسته‌بندی‌ها (ارتفاع ثابت، بدون فشردگی) ─────────── */}
          <div className="pos-categories-bar">
            <div className="pos-categories" role="tablist" aria-label="دسته‌بندی محصولات">
              <button
                type="button"
                role="tab"
                aria-selected={activeCategory === 'all'}
                className={`pos-category-card ${activeCategory === 'all' ? 'pos-category-card-active' : ''}`}
                onClick={() => setActiveCategory('all')}
              >
                <strong>همه محصولات</strong>
                <span>{toPersianNumber(orderableCount)}</span>
              </button>
              {categories.map((category) => {
                const count = items.filter((item) => item.category === category.slug && item.isOrderable).length
                return (
                  <button
                    key={category.id}
                    type="button"
                    role="tab"
                    aria-selected={activeCategory === category.slug}
                    className={`pos-category-card ${activeCategory === category.slug ? 'pos-category-card-active' : ''}`}
                    onClick={() => setActiveCategory(category.slug)}
                  >
                    <strong>{category.label}</strong>
                    <span>{toPersianNumber(count)}</span>
                  </button>
                )
              })}
            </div>
            <div className="pos-categories-tips">
              <Tip k="pos.categories" />
              <Tip k="pos.product_add" />
            </div>
          </div>

          {/* ── شبکه محصولات ──────────────────────────────────────────── */}
          {filteredItems.length === 0 ? (
            <div className="pos-empty-catalog">
              <p>محصولی مطابق فیلتر یا جستجوی شما پیدا نشد.</p>
              {(search || activeCategory !== 'all') ? (
                <button
                  type="button"
                  className="pos-secondary-button pos-btn-sm"
                  onClick={() => {
                    setSearch('')
                    setActiveCategory('all')
                  }}
                >
                  نمایش همه محصولات
                </button>
              ) : null}
            </div>
          ) : (
            <div className="pos-products">
              {filteredItems.map((item) => {
                const inCartQty = cartQtyByItem.get(item.id) || 0
                const hasSizesOrModifiers = Boolean(item.variants?.length || modifiers.length)
                const openItem = () =>
                  hasSizesOrModifiers ? setProductModal(item) : quickAddProduct(item)

                return (
                  <article
                    key={item.id}
                    className={`pos-product-card ${inCartQty > 0 ? 'is-in-cart' : ''}`}
                    onClick={openItem}
                  >
                    {inCartQty > 0 ? (
                      <span className="pos-product-qty" title="تعداد در سبد">
                        {toPersianNumber(inCartQty)}
                      </span>
                    ) : null}
                    {item.variants?.length > 1 ? (
                      <span className="pos-product-variant-tag">
                        {toPersianNumber(item.variants.length)} سایز
                      </span>
                    ) : null}
                    <div className="pos-product-media">
                      {item.imageUrl ? (
                        <img src={item.imageUrl} alt={item.persianName} loading="lazy" />
                      ) : (
                        <div className="pos-product-placeholder" aria-hidden="true">☕</div>
                      )}
                    </div>
                    <div className="pos-product-body">
                      <h3>{item.persianName}</h3>
                      <div className="pos-product-foot">
                        <p className="pos-product-price">
                          <strong>{formatPrice(resolveItemPrice(item, resolveDefaultSizeKey(item)))}</strong>
                          <small>تومان</small>
                        </p>
                        <button
                          type="button"
                          className="pos-product-add"
                          onClick={(event) => {
                            event.stopPropagation()
                            openItem()
                          }}
                          aria-label={`افزودن ${item.persianName}`}
                        >
                          +
                        </button>
                      </div>
                    </div>
                  </article>
                )
              })}
            </div>
          )}

          <p className="pos-shortcuts">
            میانبرها: <strong>/</strong> جستجو · <strong>F2</strong> نگه‌داشت · <strong>F4</strong> پرداخت نقد · <strong>F5</strong> پرداخت کارت
          </p>
        </section>

        {/* ── ستون سایدبار: مدیریت سفارش، سبد، میزها و پرداخت ──────────── */}
        <aside className="pos-sidebar">
          {/* نوار بالای سایدبار: عنوان سفارش + تب‌های چهارگانه */}
          <div className="pos-sidebar-top">
            <div className="pos-sidebar-title-row">
              <div className="pos-order-badge-group">
                <h2>{trackingCode ? `سفارش #${trackingCode}` : 'سفارش جدید'}</h2>
                {orderId ? (
                  <button
                    type="button"
                    className="pos-new-order-chip"
                    onClick={resetCart}
                    title="شروع سفارش جدید"
                  >
                    + سفارش جدید
                  </button>
                ) : null}
              </div>
              <span className="pos-sidebar-label">
                پنل‌ها <Tip k="pos.side_panels" />
              </span>
            </div>

            <div className="pos-panel-toggle" role="tablist" aria-label="پنل‌های کنار سبد">
              <button
                type="button"
                role="tab"
                aria-selected={sidePanel === 'cart'}
                className={sidePanel === 'cart' ? 'active' : ''}
                onClick={() => setSidePanel('cart')}
              >
                <span>سبد</span>
                {totalItemsCount > 0 ? (
                  <em className="pos-tab-badge">{toPersianNumber(totalItemsCount)}</em>
                ) : null}
              </button>
              <button
                type="button"
                role="tab"
                aria-selected={sidePanel === 'tables'}
                className={sidePanel === 'tables' ? 'active' : ''}
                onClick={() => setSidePanel('tables')}
              >
                <span>میزها</span>
                {selectedTable ? (
                  <em className="pos-tab-badge pos-tab-badge--gold">{selectedTable.name}</em>
                ) : null}
              </button>
              <button
                type="button"
                role="tab"
                aria-selected={sidePanel === 'held'}
                className={sidePanel === 'held' ? 'active' : ''}
                onClick={() => setSidePanel('held')}
              >
                <span>نگه‌داشته</span>
                {heldOrders.length > 0 ? (
                  <em className="pos-tab-badge">{toPersianNumber(heldOrders.length)}</em>
                ) : null}
              </button>
              <button
                type="button"
                role="tab"
                aria-selected={sidePanel === 'recent'}
                className={sidePanel === 'recent' ? 'active' : ''}
                onClick={() => setSidePanel('recent')}
              >
                <span>اخیر</span>
              </button>
            </div>
          </div>

          {/* مشخصات سفارش (فقط وقتی روی تب «سبد» هستیم نمایش داده می‌شود تا فضای تب‌های دیگر اشغال نشود) */}
          {sidePanel === 'cart' ? (
            <div className="pos-sidebar-head">
              {/* ردیف ۱: نوع سرو + انتخاب سریع میز یا روش ارسال */}
              <div className="pos-service-bar">
                <div className="pos-service-toggle" role="group" aria-label="نوع سرو">
                  <button
                    type="button"
                    className={serviceType === 'DineIn' ? 'active' : ''}
                    aria-pressed={serviceType === 'DineIn'}
                    onClick={() => setServiceType('DineIn')}
                  >
                    🍽️ داخل سالن
                  </button>
                  <button
                    type="button"
                    className={serviceType === 'TakeAway' ? 'active' : ''}
                    aria-pressed={serviceType === 'TakeAway'}
                    onClick={() => setServiceType('TakeAway')}
                  >
                    🛍️ بیرون‌بر
                  </button>
                </div>
                <Tip k="pos.service_toggle" />
              </div>

              {/* ردیف ۲: فیلدهای فشرده مشتری + میز / پیک */}
              <div className="pos-compact-fields">
                <div className="pos-field-row">
                  <input
                    type="text"
                    placeholder="نام مشتری (اختیاری)"
                    value={customerName}
                    onChange={(event) => setCustomerName(event.target.value)}
                  />
                  <Tip k="pos.customer_name" />
                </div>

                {serviceType === 'DineIn' ? (
                  <div className="pos-field-row">
                    <button
                      type="button"
                      className={`pos-table-picker-btn ${selectedTable ? 'has-table' : ''}`}
                      onClick={() => setSidePanel('tables')}
                    >
                      <span>{selectedTable ? `میز: ${selectedTable.name}` : 'انتخاب میز از نقشه...'}</span>
                      {selectedTable ? (
                        <small
                          onClick={(event) => {
                            event.stopPropagation()
                            setSalonTableId(null)
                          }}
                          title="حذف میز"
                        >
                          ✕
                        </small>
                      ) : (
                        <small>▾</small>
                      )}
                    </button>
                    <Tip k="pos.table_input" />
                  </div>
                ) : (
                  <div className="pos-field-row">
                    <input
                      type="tel"
                      dir="ltr"
                      placeholder="شماره موبایل"
                      value={customerPhone}
                      onChange={(event) => setCustomerPhone(event.target.value)}
                    />
                    <Tip k="pos.customer_phone" />
                  </div>
                )}
              </div>

              {serviceType === 'TakeAway' && (deliveryOptions ?? []).length > 0 ? (
                <div className="pos-field-row">
                  <select
                    className="pos-delivery-select"
                    value={deliveryMethodId}
                    onChange={(event) => setDeliveryMethodId(event.target.value)}
                  >
                    <option value="">تحویل حضوری / بدون پیک (بدون هزینه)</option>
                    {deliveryOptions.map((option) => (
                      <option key={option.id} value={option.id}>
                        {option.persianName}
                        {Number(option.deliveryFee) > 0 ? ` (+${formatPrice(option.deliveryFee)} ت)` : ''}
                      </option>
                    ))}
                  </select>
                  <Tip k="pos.delivery_select" />
                </div>
              ) : null}

              <div className="pos-field-row">
                <input
                  type="text"
                  placeholder="یادداشت کلی سفارش (چاپ روی فیش آشپزخانه)..."
                  value={orderNote}
                  onChange={(event) => setOrderNote(event.target.value)}
                />
                <Tip k="pos.order_note" />
              </div>

              {/* ردیف ۳: چیپ‌های انتخابی واضح و مدرن به‌جای چک‌باکس‌های بی‌برچسب */}
              <div className="pos-options-strip" role="group" aria-label="گزینه‌های ویژه سفارش">
                <div className="pos-option-pill-wrap">
                  <button
                    type="button"
                    role="switch"
                    aria-checked={personalCup}
                    className={`pos-option-pill ${personalCup ? 'is-active' : ''}`}
                    onClick={() => setPersonalCup((prev) => !prev)}
                  >
                    <span className="pos-option-check">{personalCup ? '✓' : ''}</span>
                    <span>لیوان شخصی</span>
                  </button>
                  <Tip k="pos.personal_cup" />
                </div>

                <div className="pos-option-pill-wrap">
                  <button
                    type="button"
                    role="switch"
                    aria-checked={useSubscription}
                    className={`pos-option-pill ${useSubscription ? 'is-active' : ''}`}
                    onClick={() => setUseSubscription((prev) => !prev)}
                  >
                    <span className="pos-option-check">{useSubscription ? '✓' : ''}</span>
                    <span>اشتراک ماهانه</span>
                  </button>
                  <Tip k="pos.use_subscription" />
                </div>

                <div className="pos-option-pill-wrap">
                  <button
                    type="button"
                    role="switch"
                    aria-checked={splitBill.isSplitBill}
                    className={`pos-option-pill ${splitBill.isSplitBill ? 'is-active' : ''}`}
                    onClick={() =>
                      setSplitBill((current) => ({ ...current, isSplitBill: !current.isSplitBill }))
                    }
                  >
                    <span className="pos-option-check">{splitBill.isSplitBill ? '✓' : ''}</span>
                    <span>تسویه جداگانه</span>
                  </button>
                  <Tip k="pos.split_bill" />
                </div>
              </div>

              {splitBill.isSplitBill ? (
                <div className="pos-split-config">
                  <label>
                    <span>تعداد نفرات:</span>
                    <select
                      value={splitBill.guestCount}
                      onChange={(event) =>
                        setSplitBill((current) => ({
                          ...current,
                          guestCount: Number(event.target.value) || 2,
                        }))
                      }
                    >
                      {[2, 3, 4, 5, 6, 7, 8].map((count) => (
                        <option key={count} value={count}>
                          {toPersianNumber(count)} نفر
                        </option>
                      ))}
                    </select>
                  </label>
                  <label>
                    <span>روش تقسیم:</span>
                    <select
                      value={splitBill.splitBillMode}
                      onChange={(event) =>
                        setSplitBill((current) => ({
                          ...current,
                          splitBillMode: event.target.value,
                        }))
                      }
                    >
                      <option value="ByItem">بر اساس آیتم</option>
                      <option value="ByPercentage">درصدی / مساوی</option>
                      <option value="ByCustomAmount">مبلغ دلخواه</option>
                    </select>
                  </label>
                </div>
              ) : null}
            </div>
          ) : null}

          {/* ── بخش میانی اسکرول‌پذیر: اقلام سفارش / میزها / نگه‌داشته / اخیر ── */}
          {sidePanel === 'cart' ? (
            <div className="pos-cart-lines">
              <div className="pos-panel-head">
                <span>
                  اقلام سفارش{' '}
                  {totalItemsCount > 0 ? `(${toPersianNumber(totalItemsCount)} مورد)` : ''}
                </span>
                <span className="pos-panel-tips">
                  <Tip k="pos.cart_qty" />
                  <Tip k="pos.cart_note" />
                </span>
              </div>

              {cart.length === 0 ? (
                <div className="pos-empty-cart">
                  <span className="pos-empty-cart-icon" aria-hidden="true">🧾</span>
                  <p>سبد سفارش خالی است</p>
                  <small>روی محصولات سمت راست کلیک کنید تا به سفارش اضافه شوند.</small>
                </div>
              ) : null}

              {cart.map((line) => {
                const showNoteInput =
                  expandedLineNoteKey === line.cartKey || Boolean(line.baristaNote)
                return (
                  <div key={line.cartKey} className="pos-cart-line">
                    <div className="pos-cart-line-top">
                      {line.imageUrl ? (
                        <img src={line.imageUrl} alt="" className="pos-cart-thumb" />
                      ) : (
                        <div className="pos-cart-thumb pos-cart-thumb--placeholder" aria-hidden="true">
                          ☕
                        </div>
                      )}
                      <div className="pos-cart-line-info">
                        <div className="pos-cart-line-title-row">
                          <h4>{line.name}</h4>
                          <strong className="pos-cart-line-total">
                            {formatPrice(line.unitPrice * line.quantity)} <small>ت</small>
                          </strong>
                        </div>
                        <div className="pos-cart-line-meta">
                          {line.sizeLabel ? (
                            <span className="pos-cart-chip">{line.sizeLabel}</span>
                          ) : null}
                          {line.modifierLabels?.map((mod) => (
                            <span key={mod} className="pos-cart-chip pos-cart-chip--mod">
                              {mod}
                            </span>
                          ))}
                          <small className="pos-cart-unit-price">
                            واحد: {formatPrice(line.unitPrice)}
                          </small>
                        </div>
                      </div>
                    </div>

                    <div className="pos-cart-line-bottom">
                      <div className="pos-qty-stepper">
                        <button
                          type="button"
                          className="pos-qty-button"
                          onClick={() => updateLineQuantity(line.cartKey, -1)}
                          aria-label="کاهش تعداد"
                        >
                          −
                        </button>
                        <span>{toPersianNumber(line.quantity)}</span>
                        <button
                          type="button"
                          className="pos-qty-button"
                          onClick={() => updateLineQuantity(line.cartKey, 1)}
                          aria-label="افزایش تعداد"
                        >
                          +
                        </button>
                      </div>

                      <div className="pos-cart-line-tools">
                        <button
                          type="button"
                          className={`pos-note-toggle-btn ${showNoteInput ? 'active' : ''}`}
                          onClick={() =>
                            setExpandedLineNoteKey((current) =>
                              current === line.cartKey ? null : line.cartKey,
                            )
                          }
                        >
                          ✎ توضیح باریستا
                        </button>
                        <button
                          type="button"
                          className="pos-line-remove-btn"
                          onClick={() => removeLine(line.cartKey)}
                          title="حذف آیتم از سبد"
                          aria-label={`حذف ${line.name}`}
                        >
                          حذف
                        </button>
                      </div>
                    </div>

                    {showNoteInput ? (
                      <input
                        type="text"
                        className="pos-line-note-input"
                        placeholder="توضیح باریستا برای این آیتم..."
                        value={line.baristaNote ?? ''}
                        onChange={(event) => updateLineNote(line.cartKey, event.target.value)}
                      />
                    ) : null}
                  </div>
                )
              })}
            </div>
          ) : null}

          {sidePanel === 'tables' ? (
            <div className="pos-cart-lines">
              <div className="pos-panel-head">
                <span>نقشهٔ میزهای سالن ({toPersianNumber(activeTables.length)} میز)</span>
                <Tip k="pos.tables_grid" />
              </div>
              <div className="pos-table-grid">
                {activeTables.map((table) => (
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
                    <div className="pos-table-card-head">
                      <h4>{table.name}</h4>
                      <span className={`pos-table-status-dot ${table.isOccupied ? 'is-busy' : 'is-free'}`} />
                    </div>
                    <p>ظرفیت {toPersianNumber(table.capacity)} نفر</p>
                    <p className="pos-table-state">
                      {table.isOccupied ? `اشغال · ${formatDuration(table.durationMinutes)}` : 'آزاد'}
                    </p>
                    {table.currentBalance > 0 ? (
                      <strong className="pos-table-balance">
                        {formatPrice(table.currentBalance)} تومان
                      </strong>
                    ) : null}
                  </button>
                ))}
              </div>
            </div>
          ) : null}

          {sidePanel === 'held' ? (
            <div className="pos-cart-lines">
              <div className="pos-panel-head">
                <span>سفارش‌های نگه‌داشته ({toPersianNumber(heldOrders.length)})</span>
                <Tip k="pos.held_orders" />
              </div>
              <div className="pos-held-list">
                {heldOrders.length === 0 ? <p>سفارش نگه‌داشته‌شده‌ای وجود ندارد.</p> : null}
                {heldOrders.map((order) => (
                  <div key={order.id} className="pos-held-item">
                    <div>
                      <strong>#{order.trackingCode}</strong>
                      <p>{order.customerName} · {toPersianNumber(order.lineCount)} قلم</p>
                      <small>{formatPrice(order.totalAmount)} تومان</small>
                    </div>
                    <div className="pos-held-actions">
                      <button type="button" className="pos-primary-button pos-btn-sm" onClick={() => loadOrder(order.id)}>
                        ادامه
                      </button>
                      <button type="button" className="pos-secondary-button pos-btn-sm" onClick={() => copyOrder(order.id)}>
                        کپی
                      </button>
                    </div>
                  </div>
                ))}
              </div>
            </div>
          ) : null}

          {sidePanel === 'recent' ? (
            <div className="pos-cart-lines">
              <div className="pos-panel-head">
                <span>سفارش‌های اخیر</span>
                <Tip k="pos.recent_orders" />
              </div>
              <div className="pos-recent-list">
                {recentOrders.length === 0 ? <p>سفارش اخیری ثبت نشده است.</p> : null}
                {recentOrders.map((order) => (
                  <div key={order.id} className="pos-held-item">
                    <div>
                      <strong>#{order.trackingCode}</strong>
                      <p>{order.customerName} · {order.status}</p>
                      <small>{formatDateTime(order.createdAt)}</small>
                    </div>
                    <div className="pos-held-actions">
                      <button type="button" className="pos-primary-button pos-btn-sm" onClick={() => loadOrder(order.id)}>
                        بارگذاری
                      </button>
                      <button type="button" className="pos-secondary-button pos-btn-sm" onClick={() => copyOrder(order.id)}>
                        سفارش مجدد
                      </button>
                    </div>
                  </div>
                ))}
              </div>
            </div>
          ) : null}

          {/* ── فوتر ثابت سایدبار: جمع کل + دکمه‌های پرداخت و نگه‌داشت ── */}
          <div className="pos-cart-footer">
            {error ? <p className="pos-action-feedback pos-feedback-error">{error}</p> : null}
            {message ? <p className="pos-action-feedback pos-feedback-success">{message}</p> : null}
            {!canSubmit ? (
              <p className="pos-action-feedback pos-feedback-error">
                برای ثبت سفارش به مجوز «ثبت / نگه‌داشت» یا «ویرایش سفارش» در صندوق فروش نیاز دارید.
              </p>
            ) : null}

            <div className="pos-totals-box">
              {cartTotals.taxTotal > 0 ? (
                <div className="pos-total-row pos-tax-row">
                  <span>مالیات بر ارزش افزوده</span>
                  <span>{formatPrice(cartTotals.taxTotal)} تومان</span>
                </div>
              ) : null}
              {deliveryFee > 0 ? (
                <div className="pos-total-row pos-delivery-fee-row">
                  <span>هزینه تحویل{selectedDeliveryMethod ? ` (${selectedDeliveryMethod.persianName})` : ''}</span>
                  <span>{formatPrice(deliveryFee)} تومان</span>
                </div>
              ) : null}
              <div className="pos-total-row pos-grand-total-row">
                <span className="pos-grand-total-label">
                  جمع کل قابل پرداخت <Tip k="pos.totals" />
                </span>
                <strong className="pos-grand-total-value">
                  {formatPrice(cartTotals.grandTotal)} <small>تومان</small>
                </strong>
              </div>
            </div>

            <div className="pos-actions">
              <div className="pos-action-cell">
                <button
                  type="button"
                  className="pos-primary-button pos-pay-btn"
                  onClick={() => submitOrder('PayCash')}
                  disabled={submitting || !cart.length}
                >
                  <span>💵 نقد</span>
                  <kbd>F4</kbd>
                </button>
                <Tip k="pos.cash_button" />
              </div>

              <div className="pos-action-cell">
                <button
                  type="button"
                  className="pos-primary-button pos-pay-btn pos-pay-card-btn"
                  onClick={() => submitOrder('PayCard')}
                  disabled={submitting || !cart.length}
                >
                  <span>💳 کارت‌خوان</span>
                  <kbd>F5</kbd>
                </button>
                <Tip k="pos.card_button" />
              </div>

              <div className="pos-action-cell">
                <button
                  type="button"
                  className="pos-secondary-button"
                  onClick={() => submitOrder('Hold')}
                  disabled={submitting || !cart.length}
                >
                  <span>⏸ نگه‌داشت</span>
                  <kbd>F2</kbd>
                </button>
                <Tip k="pos.hold_button" />
              </div>

              <div className="pos-action-cell">
                <button
                  type="button"
                  className="pos-danger-button"
                  onClick={cancelCurrentOrder}
                  disabled={submitting || (!orderId && !cart.length)}
                >
                  {orderId ? 'حذف سفارش' : 'پاک کردن سبد'}
                </button>
                <Tip k="pos.clear_button" />
              </div>
            </div>
          </div>
        </aside>
      </div>

      {/* ── نوار تغییر وضعیت شناور ویژه موبایل و تبلت ─────────────────── */}
      <nav className="pos-mobile-dock" aria-label="تغییر نمای صندوق در موبایل">
        <button
          type="button"
          className={`pos-mobile-dock-btn ${mobilePane === 'catalog' ? 'active' : ''}`}
          onClick={() => setMobilePane('catalog')}
        >
          <span>☕ منوی محصولات</span>
          <small>{toPersianNumber(filteredItems.length)} آیتم</small>
        </button>
        <button
          type="button"
          className={`pos-mobile-dock-btn pos-mobile-dock-btn--cart ${mobilePane === 'order' ? 'active' : ''}`}
          onClick={() => setMobilePane('order')}
        >
          <span>🧾 سبد و تسویه ({toPersianNumber(totalItemsCount)})</span>
          <strong>{formatPrice(cartTotals.grandTotal)} ت</strong>
        </button>
      </nav>

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
