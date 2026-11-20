import { useCallback, useEffect, useMemo, useRef, useState } from 'react'
import { Link } from 'react-router-dom'
import { useAdmin } from '../context/AdminContext'
import { apiRequest, getReadableError } from '../utils/adminApi'
import { formatDateTime, formatDuration, toPersianNumber } from '../utils/adminFormatters'
import { hasPermission, PERMISSIONS, canSubmitPosOrder } from '../utils/adminPermissions'
import {
  buildPosReceiptBundle,
  fetchPosReceiptPreviews,
  fetchPrintAgentHealth,
  printPosReceipts,
} from '../utils/posReceipt'
import { Tip } from '../components/Tip'
import { useEscapeKey } from '../hooks/useEscapeKey'
import {
  buildCartLineKey,
  buildUpsertPayload,
  findMissingRequiredOptionGroup,
  formatPrice,
  computeCartTotals,
  orderToCart,
  resolveDefaultSizeKey,
  resolveItemPrice,
} from '../utils/posCart'
import './pos.css'

const EMPTY_SPLIT = { isSplitBill: false, splitBillMode: 'ByItem', guestCount: 2, splitShares: [] }

function ProductModal({ item, initialLine = null, modifiers, onClose, onConfirm }) {
  const [sizeKey, setSizeKey] = useState(initialLine?.sizeKey ?? resolveDefaultSizeKey(item))
  const [selectedModifiers, setSelectedModifiers] = useState(initialLine?.modifierIds ?? [])
  const [baristaNote, setBaristaNote] = useState(initialLine?.baristaNote ?? '')
  const [quantity, setQuantity] = useState(initialLine?.quantity ?? 1)

  const initialOptionIds = useMemo(() => {
    const groups = item.optionGroups || []
    const activeIds = new Set(groups.flatMap((group) => (group.options || []).map((opt) => opt.id)))
    const existing = (initialLine?.optionIds || []).filter((id) => activeIds.has(id))
    const ids = [...existing]

    for (const group of groups) {
      const groupOptionIds = (group.options || []).map((opt) => opt.id)
      const alreadyPicked = ids.some((id) => groupOptionIds.includes(id))
      if (alreadyPicked) {
        continue
      }
      // For new lines (or required groups missing a valid selection), pre-select configured defaults.
      const isRequiredGroup = group.isRequired || group.minSelections > 0
      if (!initialLine || isRequiredGroup) {
        for (const opt of group.options || []) {
          if (opt.isDefault) {
            ids.push(opt.id)
          }
        }
      }
    }
    return ids
  }, [item.optionGroups, initialLine])

  const [selectedOptionIds, setSelectedOptionIds] = useState(initialOptionIds)
  const [optionError, setOptionError] = useState('')

  useEscapeKey(true, onClose)

  const sortedOptionGroups = useMemo(() => {
    const groups = item.optionGroups || []
    return [...groups].sort((a, b) => {
      if (a.isRequired !== b.isRequired) return a.isRequired ? -1 : 1
      return (a.displayOrder ?? 0) - (b.displayOrder ?? 0)
    })
  }, [item.optionGroups])

  function selectSingleOption(group, option) {
    const groupOptionIds = (group.options || []).map((o) => o.id)
    const isRequiredGroup = group.isRequired || group.minSelections > 0
    const isAlreadySelected = selectedOptionIds.includes(option.id)

    // Allow toggling off an optional single-choice option if clicked again.
    if (isAlreadySelected && !isRequiredGroup) {
      setSelectedOptionIds((prev) => prev.filter((id) => id !== option.id))
      setOptionError('')
      return
    }

    setSelectedOptionIds((prev) => [
      ...prev.filter((id) => !groupOptionIds.includes(id)),
      option.id,
    ])
    setOptionError('')
  }

  function toggleMultiOption(group, option) {
    const isSelected = selectedOptionIds.includes(option.id)
    if (isSelected) {
      setSelectedOptionIds((prev) => prev.filter((id) => id !== option.id))
      setOptionError('')
      return
    }
    const groupOptionIds = (group.options || []).map((o) => o.id)
    const count = selectedOptionIds.filter((id) => groupOptionIds.includes(id)).length
    const max = group.maxSelections > 0 ? group.maxSelections : 1
    if (count >= max) {
      setOptionError(`حداکثر ${toPersianNumber(max)} گزینه از «${group.persianName}» قابل انتخاب است.`)
      return
    }
    setSelectedOptionIds((prev) => [...prev, option.id])
    setOptionError('')
  }

  const basePrice = resolveItemPrice(item, sizeKey)
  const optionsTotal = useMemo(() => {
    let total = 0
    for (const group of item.optionGroups || []) {
      for (const opt of group.options || []) {
        if (selectedOptionIds.includes(opt.id)) {
          total += Number(opt.priceDelta) || 0
        }
      }
    }
    return total
  }, [item.optionGroups, selectedOptionIds])

  const unitPrice = basePrice + optionsTotal
  const sizeLabel =
    item.variants?.find((variant) => variant.key === sizeKey)?.label || sizeKey || null

  function validateOptions() {
    const missing = findMissingRequiredOptionGroup(item, selectedOptionIds)
    if (missing) {
      return missing.min <= 1
        ? `انتخاب حداقل یک گزینه از «${missing.group.persianName}» الزامی است.`
        : `حداقل ${toPersianNumber(missing.min)} گزینه از «${missing.group.persianName}» را انتخاب کنید.`
    }
    for (const group of sortedOptionGroups) {
      if (group.maxSelections > 0) {
        const groupOptionIds = (group.options || []).map((o) => o.id)
        const count = selectedOptionIds.filter((id) => groupOptionIds.includes(id)).length
        if (count > group.maxSelections) {
          return `حداکثر ${toPersianNumber(group.maxSelections)} گزینه از «${group.persianName}» قابل انتخاب است.`
        }
      }
    }
    return null
  }

  function toggleModifier(modifierId) {
    setSelectedModifiers((current) =>
      current.includes(modifierId)
        ? current.filter((entry) => entry !== modifierId)
        : [...current, modifierId],
    )
  }

  function handleConfirm() {
    const err = validateOptions()
    if (err) {
      setOptionError(err)
      return
    }

    const selectedOptionObjects = (item.optionGroups || []).flatMap((g) =>
      (g.options || [])
        .filter((o) => selectedOptionIds.includes(o.id))
        .map((o) => ({
          id: o.id,
          groupId: g.id,
          groupName: g.persianName,
          optionName: o.persianName,
          priceDelta: Number(o.priceDelta) || 0,
          priceDeltaLabel: Number(o.priceDelta) > 0 ? formatPrice(o.priceDelta) : null,
        })),
    )

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
      optionIds: selectedOptionIds,
      options: selectedOptionObjects,
      baristaNote,
    })
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

        {sortedOptionGroups.map((group) => {
          const isRequiredGroup = group.isRequired || group.minSelections > 0
          const isSingle = group.selectionMode === 'Single' || group.maxSelections === 1
          return (
            <div key={group.id} className="pos-modal-section">
              <div className="pos-option-group-head">
                <label>{group.persianName}</label>
                <div className="pos-option-group-badges">
                  {!isSingle && group.maxSelections > 1 ? (
                    <span className="pos-group-hint">
                      حداکثر {toPersianNumber(group.maxSelections)} مورد
                    </span>
                  ) : null}
                  {isRequiredGroup ? (
                    <span className="pos-badge-required">الزامی</span>
                  ) : (
                    <span className="pos-badge-optional">دلخواه</span>
                  )}
                </div>
              </div>
              <div className="pos-modifier-list">
                {(group.options || []).map((option) => {
                  const isSelected = selectedOptionIds.includes(option.id)
                  const isLowStock = option.isAvailable === false
                  return (
                    <button
                      key={option.id}
                      type="button"
                      className={`pos-modifier-chip ${isSelected ? 'selected' : ''} ${isLowStock ? 'pos-modifier-chip--low-stock' : ''}`}
                      onClick={() =>
                        isSingle
                          ? selectSingleOption(group, option)
                          : toggleMultiOption(group, option)
                      }
                      title={
                        isLowStock
                          ? 'موجودی ثبت‌شدهٔ انبار برای این گزینه کم است، اما در صندوق قابل انتخاب است'
                          : undefined
                      }
                    >
                      <span>{option.persianName}</span>
                      {Number(option.priceDelta) > 0 ? (
                        <small>+{formatPrice(option.priceDelta)} ت</small>
                      ) : null}
                      {isLowStock ? (
                        <em className="pos-option-stock-warn">کمبود موجودی</em>
                      ) : null}
                    </button>
                  )
                })}
              </div>
            </div>
          )
        })}

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

        {optionError ? <p className="pos-modal-error">{optionError}</p> : null}

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
            onClick={handleConfirm}
          >
            {initialLine ? 'ذخیره تغییرات' : 'افزودن به سفارش'}
          </button>
        </div>
      </div>
    </div>
  )
}

function PosPrintModal({ bundle, onClose, onPrinted }) {
  const printButtonRef = useRef(null)
  const [printCustomer, setPrintCustomer] = useState(true)
  const [printBarista, setPrintBarista] = useState(true)
  const [agentHealth, setAgentHealth] = useState({
    connected: true,
    printerName: 'GA-E200I',
    printerReady: true,
    paperWidthMm: 80,
    printableWidthMm: 72,
  })
  const [previews, setPreviews] = useState(null)
  const [printing, setPrinting] = useState(false)
  const [printError, setPrintError] = useState('')

  useEscapeKey(!printing, onClose)

  useEffect(() => {
    let active = true
    printButtonRef.current?.focus()

    fetchPrintAgentHealth().then((health) => {
      if (active) setAgentHealth(health)
    })

    fetchPosReceiptPreviews(bundle).then((data) => {
      if (active && data) setPreviews(data)
    })

    return () => {
      active = false
    }
  }, [bundle])

  const selectedCopies = useMemo(() => {
    const list = []
    if (printCustomer) list.push('customer')
    if (printBarista) list.push('barista')
    return list
  }, [printCustomer, printBarista])

  const handlePrint = useCallback(
    async (forceBrowser = false) => {
      if (printing || selectedCopies.length === 0) return
      setPrinting(true)
      setPrintError('')
      try {
        const res = await printPosReceipts(bundle, {
          copies: selectedCopies,
          forceBrowser,
        })
        onPrinted(res)
      } catch (err) {
        setPrintError(getReadableError(err))
        setPrinting(false)
      }
    },
    [bundle, onPrinted, printing, selectedCopies],
  )

  useEffect(() => {
    function onModalKeyDown(event) {
      if (event.key === 'Enter' && !printing && selectedCopies.length > 0) {
        if (event.target.tagName === 'BUTTON' && event.target !== printButtonRef.current) {
          return
        }
        event.preventDefault()
        handlePrint(false)
      }
    }
    window.addEventListener('keydown', onModalKeyDown)
    return () => window.removeEventListener('keydown', onModalKeyDown)
  }, [handlePrint, printing, selectedCopies.length])

  const { customer, barista } = bundle

  return (
    <div className="pos-modal-backdrop" onClick={() => !printing && onClose()}>
      <div
        className="pos-modal pos-print-modal"
        role="dialog"
        aria-modal="true"
        aria-labelledby="pos-print-modal-title"
        onClick={(event) => event.stopPropagation()}
      >
        <div className="pos-print-modal-header">
          <div>
            <h3 id="pos-print-modal-title">چاپ فیش سفارش #{customer.trackingCode}</h3>
            <p>تنظیمات چاپگر حرارتی از پیش تعریف شده است؛ فقط دکمهٔ «چاپ فیش» (یا Enter) را بزنید.</p>
          </div>
          <span
            className={`pos-print-status-badge ${agentHealth.connected && agentHealth.printerReady ? 'ready' : 'fallback'}`}
          >
            {agentHealth.connected && agentHealth.printerReady
              ? `چاپگر ${agentHealth.printerName || 'GA-E200I'} آماده (ESC/POS مستقیم)`
              : agentHealth.connected
                ? `چاپگر ${agentHealth.printerName || 'GA-E200I'} (بررسی صف)`
                : 'چاپ مرورگر'}
          </span>
        </div>

        <div className="pos-print-settings-bar">
          <div className="pos-print-setting-item">
            <span className="pos-print-setting-label">چاپگر مقصد</span>
            <strong>{agentHealth.printerName || 'GA-E200I'} (حرارتی ۸۰ میلی‌متر)</strong>
          </div>
          <div className="pos-print-setting-item">
            <span className="pos-print-setting-label">اندازه و برش کاغذ</span>
            <strong>عرض ۷۲ میلی‌متر (۵۷۶ نقطه) · برش خودکار</strong>
          </div>
          <div className="pos-print-setting-item pos-print-copies-toggle">
            <span className="pos-print-setting-label">نسخه‌های چاپ</span>
            <div className="pos-print-checkboxes">
              <label className="pos-print-check">
                <input
                  type="checkbox"
                  checked={printCustomer}
                  onChange={(e) => setPrintCustomer(e.target.checked)}
                  disabled={printing}
                />
                <span>فیش مشتری</span>
              </label>
              <label className="pos-print-check">
                <input
                  type="checkbox"
                  checked={printBarista}
                  onChange={(e) => setPrintBarista(e.target.checked)}
                  disabled={printing}
                />
                <span>فیش آشپزخانه / باریستا</span>
              </label>
            </div>
          </div>
        </div>

        <div className="pos-print-previews-grid">
          {printCustomer ? (
            <div className="pos-receipt-preview-card">
              <div className="pos-receipt-preview-badge">۱. فیش مشتری</div>
              {previews?.customerPng ? (
                <img
                  src={previews.customerPng}
                  alt="پیش‌نمایش فیش مشتری"
                  className="pos-receipt-preview-img"
                />
              ) : (
                <div className="pos-receipt-paper">
                  <div className="pos-receipt-center pos-receipt-brand">{customer.cafeName}</div>
                  <div className="pos-receipt-center pos-receipt-muted" style={{ fontSize: '0.8rem', marginBottom: 6 }}>صورت‌حساب سفارش</div>
                  {customer.address ? <div className="pos-receipt-center pos-receipt-muted">{customer.address}</div> : null}
                  {customer.contactPhone ? <div className="pos-receipt-center pos-receipt-muted">تلفن تماس: {customer.contactPhone}</div> : null}
                  <div className="pos-receipt-ornament" aria-hidden="true">✦ ☕ ✦</div>
                  <div style={{ border: '1.5px solid #000', borderRadius: 6, padding: '4px 8px', textAlign: 'center', fontWeight: 'bold', margin: '6px 0', fontSize: '0.88rem' }}>
                    شماره سفارش: #{customer.trackingCode}
                  </div>
                  <div className="pos-receipt-row"><span>تاریخ و زمان</span><span>{customer.printedAt}</span></div>
                  <div className="pos-receipt-row"><span>شیوهٔ پذیرایی</span><strong>{customer.service}{customer.table ? ` · میز شماره ${customer.table}` : ''}</strong></div>
                  <div className="pos-receipt-row"><span>مشتری گرامی</span><span>{customer.guestName}</span></div>
                  {customer.note ? <div className="pos-receipt-meta" style={{ marginTop: 4 }}>توضیحات مهمان: {customer.note}</div> : null}
                  <div className="pos-receipt-divider" />
                  {customer.lines.map((line, idx) => (
                    <div key={idx} className="pos-receipt-item" style={{ marginBottom: 6 }}>
                      <div className="pos-receipt-row">
                        <strong>{line.quantityLabel} × {line.name}</strong>
                        <span>{formatPrice(line.lineTotal)} ت</span>
                      </div>
                      {line.descriptor ? <div className="pos-receipt-meta" style={{ paddingRight: 6 }}>{line.descriptor}</div> : null}
                      {line.quantity > 1 ? <div className="pos-receipt-meta" style={{ paddingRight: 6, fontSize: '0.75rem', color: '#666' }}>(نرخ واحد: {formatPrice(line.unitPrice)} تومان)</div> : null}
                    </div>
                  ))}
                  <div className="pos-receipt-divider" />
                  <div className="pos-receipt-row"><span>جمع کل اقلام</span><span>{formatPrice(customer.subtotalAmount)} تومان</span></div>
                  {customer.deliveryFee > 0 ? (
                    <div className="pos-receipt-row"><span>هزینه ارسال / پیک</span><span>{formatPrice(customer.deliveryFee)} تومان</span></div>
                  ) : null}
                  {customer.taxTotal > 0 ? (
                    <div className="pos-receipt-row"><span>مالیات بر ارزش افزوده</span><span>{formatPrice(customer.taxTotal)} تومان</span></div>
                  ) : null}
                  <div className="pos-receipt-row pos-receipt-total" style={{ borderTop: '2px solid #000', borderBottom: '2px solid #000', padding: '6px 0', margin: '6px 0' }}>
                    <span>مبلغ قابل پرداخت</span>
                    <strong>{formatPrice(customer.totalAmount)} تومان</strong>
                  </div>
                  <div className="pos-receipt-row" style={{ marginTop: 6 }}><span>شیوهٔ پرداخت</span><span>{customer.paymentMethod}</span></div>
                  <div className="pos-receipt-row" style={{ fontWeight: 'bold' }}><span>وضعیت پرداخت</span><span>تسویه شد ✓</span></div>
                  <div className="pos-receipt-divider" style={{ borderTop: '1px dotted #000', margin: '8px 0' }} />
                  <div className="pos-receipt-center" style={{ fontWeight: 'bold', fontSize: '0.85rem', margin: '4px 0' }}>
                    {customer.layout?.customer?.footer?.thanksText || customer.layout?.footer?.thanksText || 'نوش جان! از میزبانی شما خرسندیم ✨'}
                  </div>
                  <div className="pos-receipt-center pos-receipt-muted" style={{ fontSize: '0.75rem' }}>« {customer.cafeName} »</div>
                  <div className="pos-receipt-ornament" aria-hidden="true">✦ ☕ ✦</div>
                </div>
              )}
            </div>
          ) : null}

          {printBarista ? (
            <div className="pos-receipt-preview-card">
              <div className="pos-receipt-preview-badge">۲. فیش آشپزخانه / باریستا</div>
              {previews?.baristaPng ? (
                <img
                  src={previews.baristaPng}
                  alt="پیش‌نمایش فیش آشپزخانه"
                  className="pos-receipt-preview-img"
                />
              ) : (
                <div className="pos-receipt-paper">
                  <div className="pos-receipt-center pos-receipt-brand">فیش باریستا</div>
                  <div className="pos-receipt-center pos-receipt-ticket-code">#{barista.trackingCode}</div>
                  <div className="pos-receipt-row"><span>زمان</span><span>{barista.printedAt}</span></div>
                  <div className="pos-receipt-row"><span>نوع</span><strong>{barista.service}{barista.table ? ` · میز ${barista.table}` : ''}</strong></div>
                  <div className="pos-receipt-row"><span>مشتری</span><span>{barista.guestName}</span></div>
                  {barista.note ? <div className="pos-receipt-barista-note">یادداشت سفارش: {barista.note}</div> : null}
                  <div className="pos-receipt-divider" />
                  {barista.lines.map((line, idx) => (
                    <div key={idx} className="pos-receipt-item">
                      <strong>{line.quantityLabel} × {line.name}</strong>
                      {line.sizeLabel ? <div className="pos-receipt-meta">اندازه: {line.sizeLabel}</div> : null}
                      {line.options?.length ? (
                        <div className="pos-receipt-meta">
                          {line.options.map((opt) => `✦ ${typeof opt === 'string' ? opt : opt.optionName}`).join('   ')}
                        </div>
                      ) : null}
                      {line.modifierLabels?.length ? (
                        <div className="pos-receipt-meta">
                          {line.modifierLabels.map((m) => `✦ ${m}`).join('   ')}
                        </div>
                      ) : null}
                      {line.baristaNote ? <div className="pos-receipt-barista-note">! {line.baristaNote}</div> : null}
                    </div>
                  ))}
                </div>
              )}
            </div>
          ) : null}
        </div>

        {printError ? <p className="pos-action-feedback pos-feedback-error">{printError}</p> : null}

        <div className="pos-print-modal-actions">
          <button
            type="button"
            className="pos-secondary-button"
            onClick={onClose}
            disabled={printing}
          >
            بستن (Esc)
          </button>
          <button
            ref={printButtonRef}
            type="button"
            className="pos-primary-button pos-print-confirm-button"
            onClick={() => handlePrint(false)}
            disabled={printing || selectedCopies.length === 0}
          >
            {printing
              ? 'در حال چاپ فیش‌ها…'
              : `چاپ فیش (${printCustomer && printBarista ? 'مشتری + آشپزخانه' : printCustomer ? 'فقط مشتری' : 'فقط آشپزخانه'}) ↵`}
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
  const [printModal, setPrintModal] = useState(null)
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
  const [orderKind, setOrderKind] = useState('Standard') // 'Standard' | 'InternalUse' | 'Complimentary'
  const [complimentaryReason, setComplimentaryReason] = useState('')
  const [excludeFromAccounting, setExcludeFromAccounting] = useState(false)
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
      if (printModal || productModal) {
        return
      }
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
    setOrderKind('Standard')
    setComplimentaryReason('')
    setExcludeFromAccounting(false)
    setSalonTableId(null)
    setDeliveryMethodId('')
    setSplitBill(EMPTY_SPLIT)
    setExpandedLineNoteKey(null)
  }

  function addToCart(line, replaceCartKey = null) {
    const cartKey = buildCartLineKey(line.menuItemId, line.sizeKey, line.modifierIds, line.optionIds)
    setCart((current) => {
      if (replaceCartKey) {
        const withoutOld = current.filter((entry) => entry.cartKey !== replaceCartKey)
        const existing = withoutOld.find((entry) => entry.cartKey === cartKey)
        if (existing) {
          return withoutOld.map((entry) =>
            entry.cartKey === cartKey
              ? {
                  ...entry,
                  quantity: entry.quantity + line.quantity,
                  baristaNote: line.baristaNote || entry.baristaNote,
                }
              : entry,
          )
        }
        return current.map((entry) =>
          entry.cartKey === replaceCartKey ? { ...entry, ...line, cartKey } : entry,
        )
      }

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
    setError('')
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
      setOrderKind(order.orderKind ?? 'Standard')
      setComplimentaryReason(order.complimentaryReason ?? '')
      setExcludeFromAccounting(Boolean(order.isExcludedFromAccounting))
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

    // Ensure every cart line satisfies its item's required option groups before calling the API.
    for (const line of cart) {
      const menuItem = items.find((entry) => entry.id === line.menuItemId)
      const missing = findMissingRequiredOptionGroup(menuItem, line.optionIds)
      if (missing && menuItem) {
        setError(
          `برای «${menuItem.persianName}» انتخاب حداقل ${missing.min <= 1 ? 'یک' : toPersianNumber(missing.min)} گزینه از «${missing.group.persianName}» الزامی است.`,
        )
        setMessage('')
        setProductModal({ item: menuItem, initialLine: line })
        return
      }
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
      orderKind,
      complimentaryReason,
      excludeFromAccounting,
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
        const bundle = buildPosReceiptBundle({
          result,
          cart: cartSnapshot,
          customerName,
          customerPhone,
          orderNote,
          serviceType,
          tableName: selectedTable?.name ?? null,
          deliveryMethodName: selectedDeliveryMethod?.persianName ?? null,
          deliveryFee: action === 'InternalUse' || action === 'Complimentary' ? 0 : deliveryFee,
          taxTotal: action === 'InternalUse' || action === 'Complimentary' ? 0 : cartTotals.taxTotal,
          subtotalAmount: cartTotals.itemsTotal,
          siteSettings,
          orderKind: result.orderKind ?? orderKind,
          complimentaryReason: result.complimentaryReason ?? complimentaryReason,
        })
        setPrintModal({ bundle })
        setMessage(
          action === 'InternalUse'
            ? `سفارش مصرف داخلی ${result.trackingCode} ثبت شد و موجودی کسر گردید.`
            : action === 'Complimentary'
              ? `سفارش مهمان / رایگان ${result.trackingCode} ثبت شد و موجودی کسر گردید.`
              : `پرداخت سفارش ${result.trackingCode} ثبت شد. برای چاپ فیش مشتری و آشپزخانه، دکمهٔ «چاپ فیش» را بزنید.`,
        )
        resetCart()
      }

      await loadBootstrap()
    } catch (submitError) {
      setError(getReadableError(submitError))
    } finally {
      setSubmitting(false)
    }
  }

  async function reprintOrder(id) {
    setSubmitting(true)
    setError('')
    try {
      const order = await apiRequest(`/admin/pos/orders/${id}`, { token: session.token })
      const loadedCart = orderToCart(order, items)
      const tableObj = bootstrap?.tables?.find((t) => t.id === order.salonTableId) ?? null
      const deliveryObj = (deliveryOptions ?? []).find((d) => d.id === order.deliveryMethodId) ?? null
      const bundle = buildPosReceiptBundle({
        result: {
          trackingCode: order.trackingCode,
          totalAmount: order.totalAmount,
          paymentMethod: order.paymentMethod,
          orderKind: order.orderKind,
          complimentaryReason: order.complimentaryReason,
        },
        cart: loadedCart,
        customerName: order.customerName === 'مهمان' ? '' : order.customerName,
        customerPhone: order.customerPhone === 'POS' ? '' : order.customerPhone,
        orderNote: order.note ?? '',
        serviceType: order.serviceType ?? 'TakeAway',
        tableName: tableObj?.name ?? order.salonTableName ?? null,
        deliveryMethodName: deliveryObj?.persianName ?? null,
        deliveryFee: order.deliveryFee ?? deliveryObj?.deliveryFee ?? 0,
        taxTotal: order.taxTotal ?? 0,
        siteSettings,
        createdAt: order.createdAt,
      })
      setPrintModal({ bundle })
    } catch (reprintError) {
      setError(getReadableError(reprintError))
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
    const defaultOptionIds = []
    const defaultOptionObjects = []
    let optionsTotal = 0
    for (const group of item.optionGroups || []) {
      for (const opt of group.options || []) {
        if (opt.isDefault && opt.isAvailable !== false) {
          defaultOptionIds.push(opt.id)
          defaultOptionObjects.push({
            id: opt.id,
            groupId: group.id,
            groupName: group.persianName,
            optionName: opt.persianName,
            priceDelta: Number(opt.priceDelta) || 0,
            priceDeltaLabel: Number(opt.priceDelta) > 0 ? formatPrice(opt.priceDelta) : null,
          })
          optionsTotal += Number(opt.priceDelta) || 0
        }
      }
    }

    addToCart({
      menuItemId: item.id,
      name: item.persianName,
      imageUrl: item.imageUrl,
      sizeKey,
      sizeLabel: item.variants?.find((variant) => variant.key === sizeKey)?.label ?? null,
      quantity: 1,
      unitPrice: resolveItemPrice(item, sizeKey) + optionsTotal,
      taxPercent: item.taxPercent ?? 0,
      modifierIds: [],
      modifierLabels: [],
      optionIds: defaultOptionIds,
      options: defaultOptionObjects,
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
                const hasOptionGroups = Boolean(item.optionGroups?.length)
                const hasSizesOrModifiers = Boolean(
                  item.variants?.length || modifiers.length || hasOptionGroups,
                )
                const openItem = () =>
                  hasSizesOrModifiers
                    ? setProductModal({ item, initialLine: null })
                    : quickAddProduct(item)

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
                    {item.variants?.length > 1 || hasOptionGroups ? (
                      <div className="pos-product-tags">
                        {item.variants?.length > 1 ? (
                          <span className="pos-product-variant-tag">
                            {toPersianNumber(item.variants.length)} سایز
                          </span>
                        ) : null}
                        {hasOptionGroups ? (
                          <span className="pos-product-option-tag">
                            {toPersianNumber(item.optionGroups.length)} گزینه
                          </span>
                        ) : null}
                      </div>
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
                const menuItem = items.find((entry) => entry.id === line.menuItemId)
                const canEditOptions = Boolean(
                  menuItem &&
                    (menuItem.variants?.length ||
                      modifiers.length ||
                      menuItem.optionGroups?.length),
                )
                const missingRequired = findMissingRequiredOptionGroup(menuItem, line.optionIds)
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
                          {line.options?.map((opt, optIdx) => (
                            <span key={opt.id || optIdx} className="pos-cart-chip pos-cart-chip--option">
                              {typeof opt === 'string' ? opt : opt.optionName}
                            </span>
                          ))}
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
                        {canEditOptions ? (
                          <button
                            type="button"
                            className={`pos-note-toggle-btn ${missingRequired ? 'pos-edit-options-btn--missing' : ''}`}
                            onClick={() => setProductModal({ item: menuItem, initialLine: line })}
                          >
                            {missingRequired
                              ? `⚠ انتخاب «${missingRequired.group.persianName}»`
                              : '⚙ گزینه‌ها'}
                          </button>
                        ) : null}
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
                      <p>
                        {order.customerName} ·{' '}
                        {order.orderKind === 'InternalUse'
                          ? 'مصرف داخلی'
                          : order.orderKind === 'Complimentary'
                            ? 'مهمان / رایگان'
                            : order.status}
                        {order.isExcludedFromAccounting ? ' · خارج از حسابداری' : ''}
                      </p>
                      <small>{formatDateTime(order.createdAt)}</small>
                    </div>
                    <div className="pos-held-actions">
                      <button type="button" className="pos-primary-button pos-btn-sm" onClick={() => reprintOrder(order.id)}>
                        چاپ فیش
                      </button>
                      <button type="button" className="pos-secondary-button pos-btn-sm" onClick={() => loadOrder(order.id)}>
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

            <div className="pos-order-kind-panel">
              <div className="pos-order-kind-pills" role="group" aria-label="نوع ثبت سفارش">
                <button
                  type="button"
                  className={`pos-kind-pill ${orderKind === 'Standard' ? 'active' : ''}`}
                  onClick={() => setOrderKind('Standard')}
                >
                  💳 فروش عادی
                </button>
                <button
                  type="button"
                  className={`pos-kind-pill pos-kind-pill--internal ${orderKind === 'InternalUse' ? 'active' : ''}`}
                  onClick={() => setOrderKind('InternalUse')}
                >
                  ☕ مصرف داخلی
                </button>
                <button
                  type="button"
                  className={`pos-kind-pill pos-kind-pill--complimentary ${orderKind === 'Complimentary' ? 'active' : ''}`}
                  onClick={() => setOrderKind('Complimentary')}
                >
                  🎁 مهمان / آشنا
                </button>
              </div>
              {orderKind !== 'Standard' ? (
                <input
                  type="text"
                  className="pos-kind-reason-input"
                  placeholder={
                    orderKind === 'InternalUse'
                      ? 'بابت / علت مصرف داخلی (مثلاً پرسنل شیفت صبح، تست کیفیت...)'
                      : 'بابت / نام مهمان یا آشنا (مثلاً مهمان مدیریت...)'
                  }
                  value={complimentaryReason}
                  onChange={(event) => setComplimentaryReason(event.target.value)}
                />
              ) : null}
              <label className="pos-exclude-accounting-toggle">
                <input
                  type="checkbox"
                  checked={excludeFromAccounting}
                  onChange={(event) => setExcludeFromAccounting(event.target.checked)}
                />
                <span>خارج از حسابداری ثبت شود</span>
              </label>
            </div>

            <div className="pos-totals-box">
              {orderKind === 'Standard' && cartTotals.taxTotal > 0 ? (
                <div className="pos-total-row pos-tax-row">
                  <span>مالیات بر ارزش افزوده</span>
                  <span>{formatPrice(cartTotals.taxTotal)} تومان</span>
                </div>
              ) : null}
              {orderKind === 'Standard' && deliveryFee > 0 ? (
                <div className="pos-total-row pos-delivery-fee-row">
                  <span>هزینه تحویل{selectedDeliveryMethod ? ` (${selectedDeliveryMethod.persianName})` : ''}</span>
                  <span>{formatPrice(deliveryFee)} تومان</span>
                </div>
              ) : null}
              {orderKind !== 'Standard' && cartTotals.itemsTotal > 0 ? (
                <div className="pos-total-row pos-tax-row">
                  <span>ارزش اقلام (کسر از انبار)</span>
                  <span>{formatPrice(cartTotals.itemsTotal)} تومان</span>
                </div>
              ) : null}
              <div className="pos-total-row pos-grand-total-row">
                <span className="pos-grand-total-label">
                  {orderKind === 'InternalUse'
                    ? 'مبلغ دریافتی (مصرف داخلی)'
                    : orderKind === 'Complimentary'
                      ? 'مبلغ دریافتی (مهمان / رایگان)'
                      : 'جمع کل قابل پرداخت'}{' '}
                  <Tip k="pos.totals" />
                </span>
                <strong className="pos-grand-total-value">
                  {orderKind === 'Standard' ? formatPrice(cartTotals.grandTotal) : '۰'} <small>تومان</small>
                </strong>
              </div>
            </div>

            <div className="pos-actions">
              {orderKind === 'Standard' ? (
                <>
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
                </>
              ) : (
                <div className="pos-action-cell pos-action-cell--wide">
                  <button
                    type="button"
                    className={`pos-primary-button pos-pay-btn ${orderKind === 'InternalUse' ? 'pos-internal-submit-btn' : 'pos-complimentary-submit-btn'}`}
                    onClick={() => submitOrder(orderKind)}
                    disabled={submitting || !cart.length}
                  >
                    <span>
                      {orderKind === 'InternalUse'
                        ? '☕ ثبت مصرف داخلی (رایگان)'
                        : '🎁 ثبت سفارش مهمان / آشنا (رایگان)'}
                    </span>
                  </button>
                </div>
              )}

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
          key={productModal.initialLine?.cartKey || productModal.item.id}
          item={productModal.item}
          initialLine={productModal.initialLine}
          modifiers={modifiers}
          onClose={() => setProductModal(null)}
          onConfirm={(line) => addToCart(line, productModal.initialLine?.cartKey ?? null)}
        />
      ) : null}

      {printModal ? (
        <PosPrintModal
          bundle={printModal.bundle}
          onClose={() => setPrintModal(null)}
          onPrinted={(printResult) => {
            const code = printModal.bundle.trackingCode
            const printLabel = printResult.method === 'agent' ? 'چاپگر حرارتی کافه' : 'چاپ مرورگر'
            setPrintModal(null)
            setMessage(`فیش مشتری و آشپزخانه سفارش #${code} (${printLabel}) با موفقیت چاپ شد.`)
          }}
        />
      ) : null}
    </div>
  )
}
