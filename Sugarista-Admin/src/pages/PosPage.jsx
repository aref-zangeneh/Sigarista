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
  formatPaymentBreakdown,
  printPosReceipts,
} from '../utils/posReceipt'
import { Tip } from '../components/Tip'
import { BankCardVisual, formatCardNumberGroups } from '../components/BankCardVisual.jsx'
import { useEscapeKey } from '../hooks/useEscapeKey'
import { playOrderAlertSound } from '../utils/orderAlertSound'
import {
  buildCartLineKey,
  buildUpsertPayload,
  computeCartTotals,
  computeCustomerClubPreview,
  computeManualDiscount,
  computeSplitPaymentSummary,
  findMissingRequiredOptionGroup,
  formatPrice,
  orderToCart,
  parseNumericInput,
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
      isBundle: Boolean(item.isBundle && item.bundleItems?.length),
      bundleItems: item.bundleItems ?? [],
      bundleRegularTotal: item.bundleRegularTotal ?? null,
      bundleSavingsAmount: item.bundleSavingsAmount ?? null,
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

        {item.isBundle && item.bundleItems?.length ? (
          <div className="pos-modal-section" style={{ background: 'rgba(170, 49, 113, 0.08)', padding: '0.75rem', borderRadius: '12px' }}>
            <label>محتویات این باندل (قیمت واقعی جداگانه: {formatPrice(item.bundleRegularTotal)} ت)</label>
            <ul style={{ margin: '0.4rem 0 0', paddingInlineStart: '1.1rem', fontSize: '0.86rem' }}>
              {item.bundleItems.map((comp) => (
                <li key={`${comp.menuItemId}-${comp.sizeKey ?? ''}`}>
                  {toPersianNumber(comp.quantity)} × {comp.persianName}
                  {comp.sizeLabel ? ` (${comp.sizeLabel})` : ''} — {formatPrice(comp.linePrice)} تومان
                </li>
              ))}
            </ul>
          </div>
        ) : null}

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
  const mountedRef = useRef(true)
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

  // Always allow Esc to close the print modal even if the OS print dialog didn't fire afterprint
  useEscapeKey(true, onClose)

  useEffect(() => {
    mountedRef.current = true
    return () => {
      mountedRef.current = false
    }
  }, [])

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
        if (mountedRef.current) {
          onPrinted(res)
        }
      } catch (err) {
        if (mountedRef.current) {
          setPrintError(getReadableError(err))
          setPrinting(false)
        }
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
    <div className="pos-modal-backdrop" onClick={onClose}>
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
          <div style={{ display: 'flex', alignItems: 'center', gap: 10 }}>
            <span
              className={`pos-print-status-badge ${agentHealth.connected && agentHealth.printerReady ? 'ready' : 'fallback'}`}
            >
              {agentHealth.connected && agentHealth.printerReady
                ? `چاپگر ${agentHealth.printerName || 'GA-E200I'} آماده (ESC/POS مستقیم)`
                : agentHealth.connected
                  ? `چاپگر ${agentHealth.printerName || 'GA-E200I'} (بررسی صف)`
                  : 'چاپ مرورگر'}
            </span>
            <button
              type="button"
              className="pos-modal-close"
              onClick={onClose}
              aria-label="بستن پنجره چاپ"
              title="بستن (Esc)"
            >
              ×
            </button>
          </div>
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
                  {customer.title ? (
                    <div className="pos-receipt-center pos-receipt-muted" style={{ fontSize: '0.8rem', marginBottom: 6 }}>{customer.title}</div>
                  ) : null}
                  {customer.address ? <div className="pos-receipt-center pos-receipt-muted">{customer.address}</div> : null}
                  {customer.contactPhone ? <div className="pos-receipt-center pos-receipt-muted">تلفن تماس: {customer.contactPhone}</div> : null}
                  {customer.taxId ? <div className="pos-receipt-center pos-receipt-muted">شناسه اقتصادی: {customer.taxId}</div> : null}
                  {customer.showOrnament !== false && customer.ornamentIcon !== 'none' ? (
                    <div className="pos-receipt-ornament" aria-hidden="true">✦ {customer.ornamentIcon || '☕'} ✦</div>
                  ) : (
                    <div className="pos-receipt-divider" />
                  )}
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
                  {customer.discountTotal > 0 ? (
                    <>
                      <div className="pos-receipt-row" style={{ fontWeight: 'bold', color: '#047857' }}>
                        <span>{customer.discountTitle || 'تخفیف'}</span>
                        <span>−{formatPrice(customer.discountTotal)} تومان</span>
                      </div>
                      {customer.manualDiscountReason ? (
                        <div className="pos-receipt-meta" style={{ marginBottom: 4 }}>
                          بابت تخفیف: {customer.manualDiscountReason}
                        </div>
                      ) : null}
                    </>
                  ) : null}
                  <div className="pos-receipt-row pos-receipt-total" style={{ borderTop: '2px solid #000', borderBottom: '2px solid #000', padding: '6px 0', margin: '6px 0' }}>
                    <span>مبلغ قابل پرداخت</span>
                    <strong>{formatPrice(customer.totalAmount)} تومان</strong>
                  </div>
                  <div className="pos-receipt-row" style={{ marginTop: 6 }}><span>شیوهٔ پرداخت</span><span>{customer.paymentMethod}</span></div>
                  <div className="pos-receipt-row" style={{ fontWeight: 'bold' }}><span>وضعیت پرداخت</span><span>تسویه شد ✓</span></div>
                  <div className="pos-receipt-divider" style={{ borderTop: '1px dotted #000', margin: '8px 0' }} />
                  {customer.promoBanner ? (
                    <div className="pos-receipt-center" style={{ border: '1.5px double #000', borderRadius: 6, padding: '4px 6px', fontWeight: 'bold', fontSize: '0.78rem', marginBottom: 6 }}>
                      {customer.promoBanner}
                    </div>
                  ) : null}
                  {customer.showWifi && (customer.wifiSsid || customer.wifiPassword) ? (
                    <div className="pos-receipt-center" style={{ border: '1px solid #000', borderRadius: 6, padding: '4px 6px', fontSize: '0.75rem', marginBottom: 6 }}>
                      <strong>وای‌فای مهمان (Wi-Fi)</strong>
                      {customer.wifiSsid ? <div>شبکه: <span dir="ltr" data-latin-digits="true">{customer.wifiSsid}</span></div> : null}
                      {customer.wifiPassword ? <div>رمز: <span dir="ltr" data-latin-digits="true">{customer.wifiPassword}</span></div> : null}
                    </div>
                  ) : null}
                  {customer.instagramHandle ? (
                    <div className="pos-receipt-center pos-receipt-muted" style={{ fontSize: '0.75rem' }}>
                      اینستاگرام: <span dir="ltr" data-latin-digits="true">{customer.instagramHandle.startsWith('@') ? customer.instagramHandle : `@${customer.instagramHandle}`}</span>
                    </div>
                  ) : null}
                  {customer.websiteUrl ? (
                    <div className="pos-receipt-center pos-receipt-muted" style={{ fontSize: '0.75rem' }}>
                      وب‌سایت: <span dir="ltr" data-latin-digits="true">{customer.websiteUrl}</span>
                    </div>
                  ) : null}
                  {customer.showThanks !== false ? (
                    <div className="pos-receipt-center" style={{ fontWeight: 'bold', fontSize: '0.85rem', margin: '4px 0' }}>
                      {customer.thanksText || customer.layout?.customer?.footer?.thanksText || 'نوش جان! از میزبانی شما خرسندیم ✨'}
                    </div>
                  ) : null}
                  {customer.showCafeNameInFooter !== false ? (
                    <div className="pos-receipt-center" style={{ fontWeight: 'bold', fontSize: '0.78rem' }}>
                      « {customer.footerCafeName || customer.cafeName} »
                    </div>
                  ) : null}
                  {customer.showOrnament !== false && customer.ornamentIcon !== 'none' ? (
                    <div className="pos-receipt-ornament" aria-hidden="true">✦ {customer.ornamentIcon || '☕'} ✦</div>
                  ) : null}
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
                      {line.bundleItems?.length ? (
                        <div className="pos-receipt-meta">
                          {line.bundleItems
                            .map((comp) => `↳ ${toPersianNumber((comp.quantity || 1) * (line.quantity || 1))} × ${comp.persianName}${comp.sizeLabel ? ` (${comp.sizeLabel})` : ''}`)
                            .join(' · ')}
                        </div>
                      ) : null}
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

function PosSplitPaymentModal({ payableTotal, submitting, onClose, onConfirm }) {
  const [cashInput, setCashInput] = useState('')
  const [cardInput, setCardInput] = useState('')
  const [cardToCardInput, setCardToCardInput] = useState('')
  const [cardToCardRef, setCardToCardRef] = useState('')

  useEscapeKey(!submitting, onClose)

  const summary = useMemo(
    () =>
      computeSplitPaymentSummary(payableTotal, {
        Cash: cashInput,
        Card: cardInput,
        CardToCard: cardToCardInput,
        cardToCardReference: cardToCardRef,
      }),
    [payableTotal, cashInput, cardInput, cardToCardInput, cardToCardRef],
  )

  function fillRemainingFor(method) {
    const cashVal = Math.round(parseNumericInput(cashInput))
    const cardVal = Math.round(parseNumericInput(cardInput))
    const c2cVal = Math.round(parseNumericInput(cardToCardInput))
    const target = Math.max(0, Math.round(Number(payableTotal) || 0))

    if (method === 'Cash') {
      const rem = Math.max(0, target - cardVal - c2cVal)
      setCashInput(rem > 0 ? String(rem) : '')
    } else if (method === 'Card') {
      const rem = Math.max(0, target - cashVal - c2cVal)
      setCardInput(rem > 0 ? String(rem) : '')
    } else if (method === 'CardToCard') {
      const rem = Math.max(0, target - cashVal - cardVal)
      setCardToCardInput(rem > 0 ? String(rem) : '')
    }
  }

  function handleSubmit(event) {
    event.preventDefault()
    if (!summary.isExactMatch || submitting) return
    onConfirm(summary.legs)
  }

  return (
    <div className="pos-modal-backdrop" onClick={() => !submitting && onClose()}>
      <div
        className="pos-modal pos-split-pay-modal"
        role="dialog"
        aria-modal="true"
        aria-labelledby="pos-split-pay-title"
        onClick={(event) => event.stopPropagation()}
      >
        <div className="pos-modal-head">
          <div>
            <h3 id="pos-split-pay-title">⚖️ ثبت پرداخت ترکیبی / چندروشی</h3>
            <p>مبلغ دریافتی از هر روش (نقدی، کارت‌خوان، کارت به کارت) را مشخص کنید.</p>
          </div>
          <button
            type="button"
            className="pos-modal-close"
            onClick={onClose}
            disabled={submitting}
            aria-label="بستن"
          >
            ✕
          </button>
        </div>

        <form onSubmit={handleSubmit} className="pos-split-pay-form">
          <div className="pos-split-pay-target">
            <span>مبلغ قابل پرداخت سفارش:</span>
            <strong>{formatPrice(summary.payableTotal)} تومان</strong>
          </div>

          <div className="pos-split-pay-rows">
            <div className="pos-split-pay-row">
              <div className="pos-split-pay-row-head">
                <label htmlFor="pos-split-cash">💵 دریافت نقدی</label>
                <div className="pos-split-pay-quick">
                  <button type="button" onClick={() => fillRemainingFor('Cash')}>
                    الباقی
                  </button>
                  {cashInput ? (
                    <button type="button" className="is-clear" onClick={() => setCashInput('')}>
                      پاک
                    </button>
                  ) : null}
                </div>
              </div>
              <input
                id="pos-split-cash"
                type="text"
                inputMode="numeric"
                dir="ltr"
                className="pos-meta-input ltr-input"
                placeholder="مبلغ نقدی به تومان..."
                value={cashInput}
                onChange={(event) => setCashInput(event.target.value)}
                autoFocus
              />
            </div>

            <div className="pos-split-pay-row">
              <div className="pos-split-pay-row-head">
                <label htmlFor="pos-split-card">💳 دستگاه کارت‌خوان (POS)</label>
                <div className="pos-split-pay-quick">
                  <button type="button" onClick={() => fillRemainingFor('Card')}>
                    الباقی
                  </button>
                  {cardInput ? (
                    <button type="button" className="is-clear" onClick={() => setCardInput('')}>
                      پاک
                    </button>
                  ) : null}
                </div>
              </div>
              <input
                id="pos-split-card"
                type="text"
                inputMode="numeric"
                dir="ltr"
                className="pos-meta-input ltr-input"
                placeholder="مبلغ کارت‌خوان به تومان..."
                value={cardInput}
                onChange={(event) => setCardInput(event.target.value)}
              />
            </div>

            <div className="pos-split-pay-row">
              <div className="pos-split-pay-row-head">
                <label htmlFor="pos-split-c2c">📲 کارت به کارت</label>
                <div className="pos-split-pay-quick">
                  <button type="button" onClick={() => fillRemainingFor('CardToCard')}>
                    الباقی
                  </button>
                  {cardToCardInput ? (
                    <button type="button" className="is-clear" onClick={() => setCardToCardInput('')}>
                      پاک
                    </button>
                  ) : null}
                </div>
              </div>
              <input
                id="pos-split-c2c"
                type="text"
                inputMode="numeric"
                dir="ltr"
                className="pos-meta-input ltr-input"
                placeholder="مبلغ کارت به کارت به تومان..."
                value={cardToCardInput}
                onChange={(event) => setCardToCardInput(event.target.value)}
              />
              {summary.cardToCardAmount > 0 ? (
                <input
                  type="text"
                  className="pos-meta-input pos-split-ref-input"
                  placeholder="شماره پیگیری / ۴ رقم آخر کارت (اختیاری)..."
                  value={cardToCardRef}
                  onChange={(event) => setCardToCardRef(event.target.value)}
                />
              ) : null}
            </div>
          </div>

          <div
            className={`pos-split-pay-status ${
              summary.isExactMatch
                ? 'is-balanced'
                : summary.excessAmount > 0
                  ? 'is-excess'
                  : 'is-remaining'
            }`}
          >
            <div className="pos-split-status-line">
              <span>جمع مبالغ واردشده:</span>
              <strong>{formatPrice(summary.allocatedTotal)} تومان</strong>
            </div>
            {summary.isExactMatch ? (
              <div className="pos-split-status-badge">
                ✓ تراز کامل ({formatPaymentBreakdown('Split', summary.legs)})
              </div>
            ) : summary.remainingAmount > 0 ? (
              <div className="pos-split-status-badge">
                ماندهٔ تسویه‌نشده: <strong>{formatPrice(summary.remainingAmount)} تومان</strong>
              </div>
            ) : (
              <div className="pos-split-status-badge">
                مبلغ واردشده <strong>{formatPrice(summary.excessAmount)} تومان</strong> بیشتر از جمع فاکتور است
              </div>
            )}
          </div>

          <div className="pos-modal-actions">
            <button
              type="button"
              className="pos-secondary-button"
              onClick={onClose}
              disabled={submitting}
            >
              انصراف
            </button>
            <button
              type="submit"
              className="pos-primary-button"
              disabled={!summary.isExactMatch || submitting}
            >
              {submitting ? 'در حال ثبت پرداخت...' : 'ثبت نهایی پرداخت ترکیبی'}
            </button>
          </div>
        </form>
      </div>
    </div>
  )
}

function PosManagerNoteModal({ order, token, onClose, onSaved }) {
  const [noteText, setNoteText] = useState(order?.managerNote ?? '')
  const [savingNote, setSavingNote] = useState(false)
  const [noteError, setNoteError] = useState('')

  useEscapeKey(!savingNote, onClose)

  async function handleSave(event) {
    event.preventDefault()
    setSavingNote(true)
    setNoteError('')
    try {
      const res = await apiRequest(`/admin/pos/orders/${order.id}/manager-note`, {
        method: 'PATCH',
        token,
        body: { managerNote: noteText },
      })
      onSaved(res?.managerNote ?? null)
    } catch (err) {
      setNoteError(getReadableError(err))
      setSavingNote(false)
    }
  }

  return (
    <div className="pos-modal-backdrop" onClick={() => !savingNote && onClose()}>
      <div
        className="pos-modal"
        role="dialog"
        aria-modal="true"
        aria-labelledby="pos-manager-note-modal-title"
        onClick={(event) => event.stopPropagation()}
      >
        <div className="pos-modal-head">
          <div>
            <h3 id="pos-manager-note-modal-title">
              🔒 یادداشت محرمانه مدیریت — سفارش #{order.trackingCode}
            </h3>
            <p>این یادداشت فقط برای مدیریت قابل مشاهده است و روی هیچ فیشی چاپ نمی‌شود.</p>
          </div>
          <button
            type="button"
            className="pos-modal-close"
            onClick={onClose}
            disabled={savingNote}
            aria-label="بستن"
          >
            ✕
          </button>
        </div>

        <form onSubmit={handleSave} className="pos-split-pay-form">
          <textarea
            rows={4}
            className="pos-meta-input"
            placeholder="یادداشت مدیریتی مربوط به این سفارش را بنویسید..."
            value={noteText}
            onChange={(event) => setNoteText(event.target.value)}
            maxLength={1000}
            autoFocus
          />
          {noteError ? <p className="pos-modal-error">{noteError}</p> : null}
          <div className="pos-modal-actions">
            <button
              type="button"
              className="pos-secondary-button"
              onClick={onClose}
              disabled={savingNote}
            >
              انصراف
            </button>
            <button type="submit" className="pos-primary-button" disabled={savingNote}>
              {savingNote ? 'در حال ذخیره...' : 'ذخیره یادداشت مدیریت'}
            </button>
          </div>
        </form>
      </div>
    </div>
  )
}

export function PosPage() {
  const {
    session,
    siteSettings,
    orderAlertSoundSettings,
    orderAlertSoundSaving,
    updateOrderAlertSoundSettings,
  } = useAdmin()
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
  const [splitPaymentModalOpen, setSplitPaymentModalOpen] = useState(false)
  const [managerNoteModalOrder, setManagerNoteModalOrder] = useState(null)
  const [expandedLineNoteKey, setExpandedLineNoteKey] = useState(null)
  const [orderExtrasOpen, setOrderExtrasOpen] = useState(false)

  const [orderId, setOrderId] = useState(null)
  const [trackingCode, setTrackingCode] = useState('')
  const [cart, setCart] = useState([])
  const [serviceType, setServiceType] = useState('DineIn')
  const [customerName, setCustomerName] = useState('')
  const [customerPhone, setCustomerPhone] = useState('')
  const [customerLookup, setCustomerLookup] = useState(null)
  const [customerLookupLoading, setCustomerLookupLoading] = useState(false)
  const autoFilledCustomerNameRef = useRef('')
  const [customerSearchResults, setCustomerSearchResults] = useState([])
  const [customerSearchLoading, setCustomerSearchLoading] = useState(false)
  const [customerSearchOpen, setCustomerSearchOpen] = useState(false)
  const [customerSearchMode, setCustomerSearchMode] = useState('phone') // 'phone' | 'name'
  const skipNextCustomerSearchRef = useRef(false)
  const [adminBankCards, setAdminBankCards] = useState([])
  const [selectedBankCardId, setSelectedBankCardId] = useState('')
  const [cardToCardModalOpen, setCardToCardModalOpen] = useState(false)
  const [copiedCardNumber, setCopiedCardNumber] = useState(false)
  const [discountCode, setDiscountCode] = useState('')
  const [affiliatePreview, setAffiliatePreview] = useState(null)
  const [affiliatePreviewError, setAffiliatePreviewError] = useState('')
  const [affiliatePreviewBusy, setAffiliatePreviewBusy] = useState(false)
  const [redeemLoyaltyPoints, setRedeemLoyaltyPoints] = useState(false)
  const [orderNote, setOrderNote] = useState('')
  const [managerNote, setManagerNote] = useState('')
  const [managerNoteOpen, setManagerNoteOpen] = useState(false)
  const [personalCup, setPersonalCup] = useState(false)
  const [useSubscription, setUseSubscription] = useState(false)
  const [orderKind, setOrderKind] = useState('Standard') // 'Standard' | 'InternalUse' | 'Complimentary'
  const [complimentaryReason, setComplimentaryReason] = useState('')
  const [excludeFromAccounting, setExcludeFromAccounting] = useState(false)
  const [manualDiscountOpen, setManualDiscountOpen] = useState(false)
  const [manualDiscountType, setManualDiscountType] = useState('Amount') // 'Amount' | 'Percent'
  const [manualDiscountValue, setManualDiscountValue] = useState('')
  const [manualDiscountReason, setManualDiscountReason] = useState('')
  const [salonTableId, setSalonTableId] = useState(null)
  const [deliveryMethodId, setDeliveryMethodId] = useState('')
  const [deliveryOptions, setDeliveryOptions] = useState([])
  const [splitBill, setSplitBill] = useState(EMPTY_SPLIT)

  const canSubmit = canSubmitPosOrder(session, orderId)
  const canDelete = hasPermission(session, PERMISSIONS.pos.delete) || hasPermission(session, PERMISSIONS.orders.delete)
  const canApplyManualDiscount = hasPermission(session, PERMISSIONS.pos.applyDiscount)
  const canManageManagerNote = hasPermission(session, PERMISSIONS.pos.managerNote)

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

    apiRequest('/admin/bank-cards?activeOnly=true', { token: session.token })
      .then((data) => {
        const list = Array.isArray(data) ? data : []
        setAdminBankCards(list)
        if (list.length > 0) {
          const defaultCard = list.find((c) => c.isDefault) || list[0]
          setSelectedBankCardId((prev) => prev || defaultCard.id)
        }
      })
      .catch(() => setAdminBankCards([]))
  }, [loadBootstrap, session.token])

  useEffect(() => {
    if (!message) return undefined
    const timer = window.setTimeout(() => setMessage(''), 4500)
    return () => window.clearTimeout(timer)
  }, [message])

  useEffect(() => {
    const rawDigits = String(customerPhone || '')
      .replace(/[۰-۹]/g, (d) => String('۰۱۲۳۴۵۶۷۸۹'.indexOf(d)))
      .replace(/[٠-٩]/g, (d) => String('٠١٢٣٤٥٦٧٨٩'.indexOf(d)))
      .replace(/\D/g, '')

    if (rawDigits.length < 10) {
      setCustomerLookup(null)
      setCustomerLookupLoading(false)
      return undefined
    }

    let active = true
    setCustomerLookupLoading(true)
    const timer = window.setTimeout(async () => {
      try {
        const data = await apiRequest(
          `/admin/pos/customers/lookup?phone=${encodeURIComponent(customerPhone)}`,
          { token: session.token },
        )
        if (!active) return
        setCustomerLookup(data)
        if (data?.persianName) {
          setCustomerName((prev) => {
            const trimmed = prev.trim()
            if (!trimmed || trimmed === autoFilledCustomerNameRef.current) {
              autoFilledCustomerNameRef.current = data.persianName
              return data.persianName
            }
            return prev
          })
        }
      } catch {
        if (active) setCustomerLookup(null)
      } finally {
        if (active) setCustomerLookupLoading(false)
      }
    }, 300)

    return () => {
      active = false
      window.clearTimeout(timer)
    }
  }, [customerPhone, session.token])

  // Fast POS customer autocomplete:
  // - Phone mode: triggers as soon as the 5th digit is typed (e.g. 09123) and updates on every subsequent digit
  // - Name mode: triggers as soon as 2+ characters are typed
  useEffect(() => {
    if (skipNextCustomerSearchRef.current) {
      skipNextCustomerSearchRef.current = false
      return undefined
    }

    const query = customerSearchMode === 'phone' ? customerPhone : customerName
    const trimmed = String(query || '').trim()
    const latinDigits = trimmed
      .replace(/[۰-۹]/g, (d) => String('۰۱۲۳۴۵۶۷۸۹'.indexOf(d)))
      .replace(/[٠-٩]/g, (d) => String('٠١٢٣٤٥٦٧٨٩'.indexOf(d)))
      .replace(/\D/g, '')

    const shouldSearch =
      customerSearchMode === 'phone'
        ? latinDigits.length >= 5
        : trimmed.length >= 2

    if (!shouldSearch) {
      setCustomerSearchResults([])
      setCustomerSearchLoading(false)
      return undefined
    }

    let active = true
    setCustomerSearchLoading(true)
    const timer = window.setTimeout(async () => {
      try {
        const qParam = customerSearchMode === 'phone' ? latinDigits : trimmed
        const data = await apiRequest(
          `/admin/pos/customers/search?q=${encodeURIComponent(qParam)}&mode=${customerSearchMode}&limit=10`,
          { token: session.token },
        )
        if (!active) return
        setCustomerSearchResults(Array.isArray(data) ? data : [])
      } catch {
        if (active) setCustomerSearchResults([])
      } finally {
        if (active) setCustomerSearchLoading(false)
      }
    }, 180)

    return () => {
      active = false
      window.clearTimeout(timer)
    }
  }, [customerPhone, customerName, customerSearchMode, session.token])

  const handleSelectSearchedCustomer = useCallback((customer) => {
    skipNextCustomerSearchRef.current = true
    setCustomerPhone(customer.phoneNumber || '')
    setCustomerName(customer.persianName || '')
    autoFilledCustomerNameRef.current = customer.persianName || ''
    setCustomerSearchOpen(false)
    setCustomerSearchResults([])
  }, [])

  const selectedBankCard = useMemo(
    () => adminBankCards.find((c) => c.id === selectedBankCardId) || adminBankCards[0] || null,
    [adminBankCards, selectedBankCardId],
  )

  const handleOpenCardToCard = useCallback(() => {
    setCopiedCardNumber(false)
    setCardToCardModalOpen(true)
  }, [])

  const handleCopySelectedCardNumber = useCallback(async () => {
    if (!selectedBankCard?.cardNumber) return
    try {
      await navigator.clipboard.writeText(selectedBankCard.cardNumber)
      setCopiedCardNumber(true)
      window.setTimeout(() => setCopiedCardNumber(false), 2000)
    } catch {
      // Fallback ignored; number is selectable
    }
  }, [selectedBankCard])

  useEffect(() => {
    function onKeyDown(event) {
      if (printModal || productModal || splitPaymentModalOpen || cardToCardModalOpen || managerNoteModalOrder) {
        return
      }
      if (
        event.target.matches('input, textarea, select') &&
        event.key !== 'F2' &&
        event.key !== 'F4' &&
        event.key !== 'F5' &&
        event.key !== 'F6'
      ) {
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

      if (event.key === 'F4' && cart.length > 0 && canSubmit && orderKind === 'Standard') {
        event.preventDefault()
        submitOrder('PayCash')
      }

      if (event.key === 'F5' && cart.length > 0 && canSubmit && orderKind === 'Standard') {
        event.preventDefault()
        submitOrder('PayCard')
      }

      if (event.key === 'F6' && cart.length > 0 && canSubmit && orderKind === 'Standard') {
        event.preventDefault()
        handleOpenCardToCard()
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
  const clubPreview = useMemo(
    () =>
      orderKind === 'Standard'
        ? computeCustomerClubPreview({
            itemsTotal: cartTotals.itemsTotal,
            grandTotalBeforeDiscounts: cartTotals.grandTotal,
            customerLookup,
            personalCup,
            siteSettings,
            redeemPoints: redeemLoyaltyPoints
              ? Math.floor(Number(customerLookup?.loyaltyPoints || 0))
              : 0,
            discountCode,
          })
        : { totalClubDiscount: 0, afterClubTotal: cartTotals.grandTotal, breakdown: [], cappedRedeemPoints: 0 },
    [orderKind, cartTotals.itemsTotal, cartTotals.grandTotal, customerLookup, personalCup, siteSettings, redeemLoyaltyPoints, discountCode],
  )
  const manualDiscountPreview = useMemo(
    () =>
      canApplyManualDiscount && orderKind === 'Standard'
        ? computeManualDiscount(clubPreview.afterClubTotal, manualDiscountType, manualDiscountValue)
        : { type: 'Amount', value: 0, amount: 0, finalTotal: clubPreview.afterClubTotal, exceedsLimit: false },
    [canApplyManualDiscount, orderKind, clubPreview.afterClubTotal, manualDiscountType, manualDiscountValue],
  )
  const totalCombinedDiscount = clubPreview.totalClubDiscount + manualDiscountPreview.amount
  const effectivePayableTotal = orderKind === 'Standard' ? manualDiscountPreview.finalTotal : 0

  const selectedTable = bootstrap?.tables?.find((table) => table.id === salonTableId) ?? null

  function resetCart() {
    setOrderId(null)
    setTrackingCode('')
    setCart([])
    setCustomerName('')
    setCustomerPhone('')
    setCustomerLookup(null)
    autoFilledCustomerNameRef.current = ''
    setDiscountCode('')
    setAffiliatePreview(null)
    setAffiliatePreviewError('')
    setRedeemLoyaltyPoints(false)
    setOrderNote('')
    setManagerNote('')
    setManagerNoteOpen(false)
    setPersonalCup(false)
    setUseSubscription(false)
    setOrderKind('Standard')
    setComplimentaryReason('')
    setExcludeFromAccounting(false)
    setManualDiscountOpen(false)
    setManualDiscountType('Amount')
    setManualDiscountValue('')
    setManualDiscountReason('')
    setSalonTableId(null)
    setDeliveryMethodId('')
    setSplitBill(EMPTY_SPLIT)
    setSplitPaymentModalOpen(false)
    setExpandedLineNoteKey(null)
    setOrderExtrasOpen(false)
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
      autoFilledCustomerNameRef.current = order.customerName === 'مهمان' ? '' : order.customerName
      setOrderNote(order.note ?? '')
      if (canManageManagerNote) {
        setManagerNote(order.managerNote ?? '')
        setManagerNoteOpen(Boolean(order.managerNote))
      } else {
        setManagerNote('')
        setManagerNoteOpen(false)
      }
      setServiceType(order.serviceType ?? 'TakeAway')
      setDeliveryMethodId(order.deliveryMethodId ?? '')
      setSalonTableId(order.salonTableId ?? null)
      setOrderKind(order.orderKind ?? 'Standard')
      setComplimentaryReason(order.complimentaryReason ?? '')
      setExcludeFromAccounting(Boolean(order.isExcludedFromAccounting))
      if (canApplyManualDiscount && Number(order.manualDiscountValue) > 0) {
        setManualDiscountType(order.manualDiscountType === 'Percent' ? 'Percent' : 'Amount')
        setManualDiscountValue(String(order.manualDiscountValue))
        setManualDiscountReason(order.manualDiscountReason ?? '')
        setManualDiscountOpen(true)
      } else {
        setManualDiscountType('Amount')
        setManualDiscountValue('')
        setManualDiscountReason('')
        setManualDiscountOpen(false)
      }
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

  async function submitOrder(action, extraOptions = {}) {
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

    if (canApplyManualDiscount && orderKind === 'Standard' && manualDiscountPreview.exceedsLimit) {
      setError(
        manualDiscountType === 'Percent'
          ? 'درصد تخفیف دلخواه نمی‌تواند بیشتر از ۱۰۰ باشد.'
          : 'مبلغ تخفیف دلخواه نمی‌تواند بیشتر از جمع کل سفارش باشد.',
      )
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
      orderKind,
      complimentaryReason,
      excludeFromAccounting,
      manualDiscountType: canApplyManualDiscount ? manualDiscountType : null,
      manualDiscountValue: canApplyManualDiscount ? manualDiscountValue : null,
      manualDiscountReason: canApplyManualDiscount ? manualDiscountReason : '',
      paymentAllocations: extraOptions.paymentAllocations ?? null,
      paymentReference: extraOptions.paymentReference ?? '',
      discountCode,
      redeemPoints: clubPreview.cappedRedeemPoints,
      managerNote: canManageManagerNote ? managerNote : undefined,
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

      setSplitPaymentModalOpen(false)

      if (action === 'Hold') {
        setMessage(`سفارش ${result.trackingCode} نگه‌داشته شد.`)
        resetCart()
      } else {
        const cartSnapshot = cart.map((line) => ({ ...line }))
        const bundle = buildPosReceiptBundle({
          result,
          cart: cartSnapshot,
          customerName: result.customerName && result.customerName !== 'مهمان' ? result.customerName : customerName,
          customerPhone: result.customerPhone || customerPhone,
          orderNote,
          serviceType,
          tableName: selectedTable?.name ?? null,
          deliveryMethodName: selectedDeliveryMethod?.persianName ?? null,
          deliveryFee: action === 'InternalUse' || action === 'Complimentary' ? 0 : deliveryFee,
          taxTotal: action === 'InternalUse' || action === 'Complimentary' ? 0 : cartTotals.taxTotal,
          subtotalAmount: cartTotals.itemsTotal,
          discountTotal: result.discountTotal ?? totalCombinedDiscount,
          manualDiscountType: result.manualDiscountType ?? (manualDiscountPreview.amount > 0 ? manualDiscountType : null),
          manualDiscountValue: result.manualDiscountValue ?? (manualDiscountPreview.amount > 0 ? manualDiscountPreview.value : null),
          manualDiscountAmount: result.manualDiscountAmount ?? manualDiscountPreview.amount,
          manualDiscountReason: result.manualDiscountReason ?? (manualDiscountPreview.amount > 0 ? manualDiscountReason : ''),
          paymentLegs: result.paymentLegs ?? extraOptions.paymentAllocations ?? [],
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
              : `پرداخت سفارش ${result.trackingCode} ثبت شد${result.customerId ? ' و امتیاز/سابقهٔ باشگاه مشتریان بروزرسانی گردید' : ''}.`,
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
          paymentLegs: order.paymentLegs ?? [],
          orderKind: order.orderKind,
          complimentaryReason: order.complimentaryReason,
          discountTotal: order.discountTotal,
          manualDiscountType: order.manualDiscountType,
          manualDiscountValue: order.manualDiscountValue,
          manualDiscountAmount: order.manualDiscountAmount,
          manualDiscountReason: order.manualDiscountReason,
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
      isBundle: Boolean(item.isBundle && item.bundleItems?.length),
      bundleItems: item.bundleItems ?? [],
      bundleRegularTotal: item.bundleRegularTotal ?? null,
      bundleSavingsAmount: item.bundleSavingsAmount ?? null,
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
                disabled={orderAlertSoundSaving}
                aria-pressed={orderAlertSoundSettings.enabled}
                title={
                  orderAlertSoundSettings.enabled
                    ? 'صدای اعلان سفارش روشن است (کلیک برای قطع)'
                    : 'صدای اعلان سفارش خاموش است (کلیک برای روشن کردن)'
                }
                onClick={() => {
                  const nextEnabled = !orderAlertSoundSettings.enabled
                  if (nextEnabled) {
                    playOrderAlertSound({ force: true })
                  }
                  updateOrderAlertSoundSettings({ enabled: nextEnabled })
                }}
              >
                {orderAlertSoundSettings.enabled ? '🔊 صدا روشن' : '🔇 صدا خاموش'}
              </button>
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
                    {item.isBundle || item.variants?.length > 1 || hasOptionGroups ? (
                      <div className="pos-product-tags">
                        {item.isBundle ? (
                          <span className="pos-product-option-tag" style={{ background: 'linear-gradient(135deg,#7c4a2d,#aa3171)', color: '#fff' }}>
                            باندل{item.bundleSavingsPercent > 0 ? ` · ${toPersianNumber(item.bundleSavingsPercent)}٪-` : ''}
                          </span>
                        ) : null}
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
                      {item.isBundle && item.bundleItems?.length ? (
                        <small style={{ color: 'var(--text-muted)', fontSize: '0.74rem', lineHeight: 1.4, display: 'block', marginBottom: '0.25rem' }}>
                          {item.bundleItems.map((c) => `${toPersianNumber(c.quantity)}× ${c.persianName}`).join(' + ')}
                        </small>
                      ) : null}
                      <div className="pos-product-foot">
                        <p className="pos-product-price">
                          <strong>{formatPrice(resolveItemPrice(item, resolveDefaultSizeKey(item)))}</strong>
                          <small>تومان</small>
                          {item.isBundle && Number(item.bundleRegularTotal) > resolveItemPrice(item, resolveDefaultSizeKey(item)) ? (
                            <small style={{ textDecoration: 'line-through', opacity: 0.65, marginInlineStart: '0.35rem' }}>
                              {formatPrice(item.bundleRegularTotal)}
                            </small>
                          ) : null}
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
            میانبرها: <strong>/</strong> جستجو · <strong>F2</strong> نگه‌داشت · <strong>F4</strong> نقد · <strong>F5</strong> کارت‌خوان · <strong>F6</strong> کارت به کارت
          </p>
        </section>

        {/* ── ستون سایدبار: مدیریت سفارش، سبد، میزها و پرداخت ──────────── */}
        <aside className="pos-sidebar">
          {/* ── ناحیه ۱ (ثابت بالا): عنوان سفارش + تب‌های چهارگانه ── */}
          <div className="pos-sidebar-top">
            <div className="pos-sidebar-title-row">
              <div className="pos-order-badge-group">
                <h2>
                  {trackingCode ? (
                    <>
                      سفارش <span dir="ltr" data-latin-digits="true">#{trackingCode}</span>
                    </>
                  ) : (
                    'سفارش جدید'
                  )}
                </h2>
                {orderId ? (
                  <button
                    type="button"
                    className="pos-new-order-chip"
                    onClick={resetCart}
                    title="شروع سفارش جدید"
                  >
                    + جدید
                  </button>
                ) : null}
              </div>
              <Tip k="pos.side_panels" />
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

          {/* ── ناحیه ۲ (میانی کاملاً اسکرول‌پذیر): هرگز فشرده یا بریده نمی‌شود ── */}
          <div className="pos-sidebar-body">
            {sidePanel === 'cart' ? (
              <>
                {/* مشخصات فشرده و تمیز سفارش */}
                <div className="pos-meta-card">
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

                  <div className="pos-customer-fields-wrap">
                    <div className="pos-compact-fields">
                      <input
                        id="pos-customer-phone-input"
                        type="tel"
                        dir="ltr"
                        className="pos-meta-input ltr-input"
                        placeholder="شماره موبایل مشتری (جستجو از رقم ۵)"
                        value={customerPhone}
                        onFocus={() => {
                          setCustomerSearchMode('phone')
                          setCustomerSearchOpen(true)
                        }}
                        onBlur={() => {
                          window.setTimeout(() => setCustomerSearchOpen(false), 160)
                        }}
                        onChange={(event) => {
                          setCustomerSearchMode('phone')
                          setCustomerSearchOpen(true)
                          setCustomerPhone(event.target.value)
                        }}
                      />
                      <input
                        id="pos-customer-name-input"
                        type="text"
                        className="pos-meta-input"
                        placeholder="نام مشتری (جستجو در مشتریان قبلی)"
                        value={customerName}
                        onFocus={() => {
                          setCustomerSearchMode('name')
                          setCustomerSearchOpen(true)
                        }}
                        onBlur={() => {
                          window.setTimeout(() => setCustomerSearchOpen(false), 160)
                        }}
                        onChange={(event) => {
                          setCustomerSearchMode('name')
                          setCustomerSearchOpen(true)
                          setCustomerName(event.target.value)
                        }}
                      />
                    </div>

                    {customerSearchOpen && (customerSearchLoading || customerSearchResults.length > 0) ? (
                      <div className="pos-customer-suggest" role="listbox" aria-label="نتایج جستجوی مشتری">
                        {customerSearchLoading ? (
                          <div className="pos-customer-suggest-empty">در حال جستجوی مشتریان قبلی...</div>
                        ) : (
                          customerSearchResults.map((customer) => (
                            <button
                              key={customer.id}
                              type="button"
                              className="pos-customer-suggest-item"
                              onMouseDown={(event) => {
                                event.preventDefault()
                                handleSelectSearchedCustomer(customer)
                              }}
                            >
                              <div>
                                <div className="pos-customer-suggest-name">
                                  {customer.persianName || 'مشتری بدون نام'}
                                </div>
                                {customer.membershipTierName ? (
                                  <div className="pos-customer-suggest-meta">
                                    🏅 {customer.membershipTierName}
                                  </div>
                                ) : null}
                              </div>
                              <span className="pos-customer-suggest-phone" dir="ltr">
                                {customer.phoneNumber}
                              </span>
                            </button>
                          ))
                        )}
                      </div>
                    ) : null}
                  </div>

                  {customerLookupLoading ? (
                    <div className="pos-customer-club-banner pos-customer-club-banner--loading">
                      <span>در حال بررسی شماره در باشگاه مشتریان...</span>
                    </div>
                  ) : customerLookup?.normalizedPhone ? (
                    customerLookup.exists ? (
                      <div className="pos-customer-club-banner pos-customer-club-banner--member">
                        <div className="pos-club-banner-top">
                          <strong>
                            👤 {customerLookup.persianName || customerLookup.normalizedPhone}
                            <em className="pos-club-member-tag">عضو باشگاه</em>
                          </strong>
                          {customerLookup.paidOrdersCount > 0 ? (
                            <small>{toPersianNumber(customerLookup.paidOrdersCount)} خرید قبلی</small>
                          ) : null}
                        </div>
                        <div className="pos-club-chips">
                          {customerLookup.membershipTierName ? (
                            <span className="pos-club-chip">
                              🏅 {customerLookup.membershipTierName}
                              {customerLookup.membershipTierDiscountPercent > 0
                                ? ` (${toPersianNumber(customerLookup.membershipTierDiscountPercent)}٪)`
                                : ''}
                            </span>
                          ) : null}
                          {customerLookup.activeGroupName ? (
                            <span className="pos-club-chip">
                              👥 {customerLookup.activeGroupName}
                              {customerLookup.activeGroupDiscountPercent > 0
                                ? ` (${toPersianNumber(customerLookup.activeGroupDiscountPercent)}٪)`
                                : ''}
                            </span>
                          ) : null}
                          {customerLookup.isBirthdayDiscountActive ? (
                            <span className="pos-club-chip pos-club-chip--highlight">
                              🎂 هدیه تولد ({toPersianNumber(customerLookup.birthdayDiscountPercent)}٪)
                            </span>
                          ) : null}
                          {customerLookup.hasActiveStampReward ? (
                            <span className="pos-club-chip pos-club-chip--highlight">
                              🎫 پاداش کارت مهر ({toPersianNumber(customerLookup.stampRewardPercent)}٪)
                            </span>
                          ) : null}
                          {customerLookup.loyaltyEnabled && customerLookup.loyaltyPoints > 0 ? (
                            <button
                              type="button"
                              className={`pos-club-chip pos-club-chip--action ${redeemLoyaltyPoints ? 'is-active' : ''}`}
                              onClick={() => setRedeemLoyaltyPoints((prev) => !prev)}
                              title="کلیک برای مصرف امتیاز در این سفارش"
                            >
                              ⭐ امتیاز: {formatPrice(Math.floor(customerLookup.loyaltyPoints))}
                              {customerLookup.loyaltyRedeemTomanPerPoint > 0
                                ? redeemLoyaltyPoints
                                  ? ' · در حال کسر ✓'
                                  : ' · مصرف امتیاز'
                                : ''}
                            </button>
                          ) : null}
                          {customerLookup.subscriptionRemainingUnits > 0 ? (
                            <button
                              type="button"
                              className={`pos-club-chip pos-club-chip--action ${useSubscription ? 'is-active' : ''}`}
                              onClick={() => setUseSubscription((prev) => !prev)}
                            >
                              ☕ اشتراک: {toPersianNumber(customerLookup.subscriptionRemainingUnits)} واحد
                              {useSubscription ? ' ✓' : ''}
                            </button>
                          ) : null}
                          {customerLookup.walletEnabled && customerLookup.walletBalance > 0 ? (
                            <span className="pos-club-chip">
                              👛 کیف پول: {formatPrice(customerLookup.walletBalance)} ت
                            </span>
                          ) : null}
                        </div>
                      </div>
                    ) : (
                      <div className="pos-customer-club-banner pos-customer-club-banner--new">
                        <span>
                          ✨ مخاطب جدید — با ثبت سفارش خودکار به <strong>مشتریان</strong> و{' '}
                          <strong>مخاطبین پیامک</strong> اضافه می‌شود.
                        </span>
                      </div>
                    )
                  ) : null}

                  {serviceType === 'DineIn' ? (
                    <button
                      type="button"
                      className={`pos-table-picker-btn ${selectedTable ? 'has-table' : ''}`}
                      onClick={() => setSidePanel('tables')}
                    >
                      <span>{selectedTable ? `میز انتخاب‌شده: ${selectedTable.name}` : '🪑 انتخاب میز سالن از نقشه...'}</span>
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
                  ) : null}

                  {serviceType === 'TakeAway' && (deliveryOptions ?? []).length > 0 ? (
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
                  ) : null}

                  {/* کشوی جمع‌شوندهٔ یادداشت کلی و گزینه‌های ویژه (فضای اقلام سفارش را آزاد نگه می‌دارد) */}
                  <div className={`pos-extras-accordion ${orderExtrasOpen ? 'is-open' : ''}`}>
                    <button
                      type="button"
                      className="pos-extras-toggle"
                      aria-expanded={orderExtrasOpen}
                      onClick={() => setOrderExtrasOpen((open) => !open)}
                    >
                      <span className="pos-extras-toggle-label">
                        <span>یادداشت و گزینه‌های ویژه</span>
                        <span className="pos-extras-summary-badges">
                          {orderNote.trim() ? <em className="pos-mini-badge">یادداشت</em> : null}
                          {discountCode.trim() ? <em className="pos-mini-badge">کد تخفیف</em> : null}
                          {personalCup ? <em className="pos-mini-badge">لیوان شخصی</em> : null}
                          {useSubscription ? <em className="pos-mini-badge">اشتراک</em> : null}
                          {splitBill.isSplitBill ? (
                            <em className="pos-mini-badge">تقسیم {toPersianNumber(splitBill.guestCount)} نفر</em>
                          ) : null}
                        </span>
                      </span>
                      <small>{orderExtrasOpen ? 'بستن ▴' : 'باز کردن ▾'}</small>
                    </button>

                    {orderExtrasOpen ? (
                      <div className="pos-extras-body">
                        <input
                          type="text"
                          className="pos-meta-input"
                          placeholder="یادداشت کلی سفارش (چاپ روی فیش آشپزخانه)..."
                          value={orderNote}
                          onChange={(event) => setOrderNote(event.target.value)}
                        />
                        {orderKind === 'Standard' ? (
                          <div style={{ display: 'flex', flexDirection: 'column', gap: '0.35rem' }}>
                            <div style={{ display: 'flex', gap: '0.4rem', alignItems: 'center' }}>
                              <input
                                type="text"
                                dir="ltr"
                                className="pos-meta-input ltr-input"
                                style={{ flex: 1 }}
                                placeholder="کد معرف / تخفیف (مثلاً ALI15)..."
                                value={discountCode}
                                onChange={(event) => {
                                  setDiscountCode(event.target.value)
                                  setAffiliatePreview(null)
                                  setAffiliatePreviewError('')
                                }}
                              />
                              <button
                                type="button"
                                className="ghost-button"
                                style={{ whiteSpace: 'nowrap', padding: '0.4rem 0.65rem', fontSize: '0.78rem' }}
                                disabled={!discountCode.trim() || affiliatePreviewBusy}
                                onClick={async () => {
                                  if (!discountCode.trim() || !session?.token) return
                                  setAffiliatePreviewBusy(true)
                                  setAffiliatePreviewError('')
                                  try {
                                    const params = new URLSearchParams({
                                      code: discountCode.trim(),
                                      subtotal: String(cartTotals.itemsTotal || 0),
                                    })
                                    if (customerLookup?.customerId) {
                                      params.set('customerId', customerLookup.customerId)
                                    }
                                    const res = await apiRequest(`/admin/pos/affiliate-coupon?${params.toString()}`, {
                                      token: session.token,
                                    })
                                    setAffiliatePreview(res)
                                  } catch (err) {
                                    setAffiliatePreview(null)
                                    setAffiliatePreviewError(err.message || 'کد معرف یافت نشد (اگر کد تخفیف معمولی است هنگام ثبت اعمال می‌شود).')
                                  } finally {
                                    setAffiliatePreviewBusy(false)
                                  }
                                }}
                              >
                                {affiliatePreviewBusy ? '...' : 'بررسی معرف'}
                              </button>
                            </div>
                            {affiliatePreview ? (
                              <small style={{ color: '#059669', fontWeight: 600 }}>
                                ✓ کد معرف {affiliatePreview.code} ({affiliatePreview.affiliateName}) — تخفیف:{' '}
                                {toPersianNumber(affiliatePreview.discountAmount)} تومان
                              </small>
                            ) : null}
                            {affiliatePreviewError ? (
                              <small style={{ color: '#b45309' }}>{affiliatePreviewError}</small>
                            ) : null}
                          </div>
                        ) : null}

                        <div className="pos-options-strip" role="group" aria-label="گزینه‌های ویژه سفارش">
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
                  </div>
                </div>

                {/* لیست اقلام سفارش */}
                <div className="pos-cart-section">
                  <div className="pos-panel-head">
                    <span>
                      اقلام سفارش{' '}
                      {totalItemsCount > 0 ? `(${toPersianNumber(totalItemsCount)} مورد)` : ''}
                    </span>
                    <span className="pos-panel-tips">
                      <Tip k="pos.cart_qty" />
                    </span>
                  </div>

                  {cart.length === 0 ? (
                    <div className="pos-empty-cart">
                      <span className="pos-empty-cart-icon" aria-hidden="true">🧾</span>
                      <p>سبد سفارش خالی است</p>
                      <small>روی محصولات سمت راست کلیک کنید تا به سفارش اضافه شوند.</small>
                    </div>
                  ) : (
                    <div className="pos-cart-items-list">
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
                                  ✎ توضیح
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
                  )}
                </div>

                {/* تنظیمات تسویه: نوع ثبت سفارش + خارج از حسابداری + تخفیف دلخواه (داخل بخش اسکرول‌پذیر) */}
                <div className="pos-checkout-options">
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

                  {canApplyManualDiscount && orderKind === 'Standard' ? (
                    <div className={`pos-manual-discount-box ${manualDiscountPreview.amount > 0 ? 'is-active' : ''}`}>
                      <div className="pos-manual-discount-head">
                        <button
                          type="button"
                          className="pos-manual-discount-toggle"
                          onClick={() => setManualDiscountOpen((open) => !open)}
                        >
                          <span>🏷 تخفیف دلخواه در لحظه</span>
                          {manualDiscountPreview.amount > 0 ? (
                            <strong className="pos-manual-discount-badge">
                              −{formatPrice(manualDiscountPreview.amount)} ت
                              {manualDiscountType === 'Percent' && manualDiscountPreview.value > 0
                                ? ` (${toPersianNumber(manualDiscountPreview.value)}٪)`
                                : ''}
                            </strong>
                          ) : (
                            <small>{manualDiscountOpen ? 'بستن ▴' : 'افزودن ▾'}</small>
                          )}
                        </button>
                        {manualDiscountPreview.amount > 0 ? (
                          <button
                            type="button"
                            className="pos-manual-discount-clear"
                            title="حذف تخفیف دلخواه"
                            onClick={() => {
                              setManualDiscountValue('')
                              setManualDiscountReason('')
                              setManualDiscountOpen(false)
                            }}
                          >
                            حذف
                          </button>
                        ) : null}
                      </div>

                      {manualDiscountOpen ? (
                        <div className="pos-manual-discount-body">
                          <div className="pos-manual-discount-row">
                            <div className="pos-manual-discount-mode" role="group" aria-label="نوع تخفیف دلخواه">
                              <button
                                type="button"
                                className={manualDiscountType === 'Amount' ? 'active' : ''}
                                onClick={() => {
                                  setManualDiscountType('Amount')
                                  setManualDiscountValue('')
                                }}
                              >
                                مبلغ (تومان)
                              </button>
                              <button
                                type="button"
                                className={manualDiscountType === 'Percent' ? 'active' : ''}
                                onClick={() => {
                                  setManualDiscountType('Percent')
                                  setManualDiscountValue('')
                                }}
                              >
                                درصد (٪)
                              </button>
                            </div>
                            <input
                              type="text"
                              inputMode="decimal"
                              className="pos-manual-discount-input ltr-input"
                              placeholder={manualDiscountType === 'Percent' ? 'مثلاً ۱۰' : 'مبلغ به تومان'}
                              value={manualDiscountValue}
                              onChange={(event) => setManualDiscountValue(event.target.value)}
                            />
                          </div>

                          {manualDiscountType === 'Percent' ? (
                            <div className="pos-manual-discount-presets">
                              {[5, 10, 15, 20, 25, 50].map((pct) => (
                                <button
                                  key={pct}
                                  type="button"
                                  className={`pos-discount-preset-btn ${Number(manualDiscountValue) === pct ? 'active' : ''}`}
                                  onClick={() => setManualDiscountValue(String(pct))}
                                >
                                  {toPersianNumber(pct)}٪
                                </button>
                              ))}
                            </div>
                          ) : null}

                          <input
                            type="text"
                            className="pos-kind-reason-input"
                            placeholder="علت یا توضیح تخفیف (اختیاری، مثلاً مشتری وفادار...)"
                            value={manualDiscountReason}
                            onChange={(event) => setManualDiscountReason(event.target.value)}
                          />

                          {manualDiscountPreview.exceedsLimit ? (
                            <small className="pos-manual-discount-warn">
                              {manualDiscountType === 'Percent'
                                ? 'درصد تخفیف باید حداکثر ۱۰۰ باشد.'
                                : 'مبلغ تخفیف بیشتر از مبلغ سفارش است.'}
                            </small>
                          ) : null}
                        </div>
                      ) : null}
                    </div>
                  ) : null}

                  {canManageManagerNote ? (
                    <div className={`pos-manager-note-box ${managerNote.trim() ? 'is-active' : ''}`}>
                      <div className="pos-manager-note-head">
                        <button
                          type="button"
                          className="pos-manager-note-toggle"
                          aria-expanded={managerNoteOpen}
                          onClick={() => setManagerNoteOpen((open) => !open)}
                        >
                          <span>🔒 یادداشت محرمانه مدیریت</span>
                          {managerNote.trim() ? (
                            <strong className="pos-manager-note-badge">ثبت‌شده</strong>
                          ) : (
                            <small>{managerNoteOpen ? 'بستن ▴' : 'افزودن ▾'}</small>
                          )}
                        </button>
                        {managerNote.trim() ? (
                          <button
                            type="button"
                            className="pos-manual-discount-clear"
                            title="پاک کردن یادداشت مدیریت"
                            onClick={() => {
                              setManagerNote('')
                              setManagerNoteOpen(false)
                            }}
                          >
                            پاک
                          </button>
                        ) : null}
                      </div>
                      {managerNoteOpen ? (
                        <div className="pos-manager-note-body">
                          <small className="pos-manager-note-hint">
                            فقط کاربران دارای دسترسی مدیریت این متن را می‌بینند؛ روی هیچ فیشی چاپ نمی‌شود.
                          </small>
                          <textarea
                            id="pos-manager-note-input"
                            rows={2}
                            maxLength={1000}
                            className="pos-meta-input"
                            placeholder="یادداشت داخلی مدیریت برای این سفارش..."
                            value={managerNote}
                            onChange={(event) => setManagerNote(event.target.value)}
                          />
                        </div>
                      ) : null}
                    </div>
                  ) : null}
                </div>
              </>
            ) : null}

            {sidePanel === 'tables' ? (
              <div className="pos-side-panel-section">
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
              <div className="pos-side-panel-section">
                <div className="pos-panel-head">
                  <span>سفارش‌های نگه‌داشته ({toPersianNumber(heldOrders.length)})</span>
                  <Tip k="pos.held_orders" />
                </div>
                <div className="pos-held-list">
                  {heldOrders.length === 0 ? <p className="pos-empty-muted">سفارش نگه‌داشته‌شده‌ای وجود ندارد.</p> : null}
                  {heldOrders.map((order) => (
                    <div key={order.id} className="pos-held-item">
                      <div>
                        <strong dir="ltr" data-latin-digits="true">#{order.trackingCode}</strong>
                        <p>
                          {order.customerName}
                          {order.customerPhone ? ` (${toPersianNumber(order.customerPhone)})` : ''} ·{' '}
                          {toPersianNumber(order.lineCount)} قلم
                        </p>
                        <small>{formatPrice(order.totalAmount)} تومان</small>
                        {canManageManagerNote && order.managerNote ? (
                          <div className="pos-manager-note-snippet">🔒 {order.managerNote}</div>
                        ) : null}
                      </div>
                      <div className="pos-held-actions">
                        {canManageManagerNote ? (
                          <button
                            type="button"
                            className="pos-secondary-button pos-btn-sm"
                            title="یادداشت مدیریت"
                            onClick={() => setManagerNoteModalOrder(order)}
                          >
                            🔒
                          </button>
                        ) : null}
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
              <div className="pos-side-panel-section">
                <div className="pos-panel-head">
                  <span>سفارش‌های اخیر</span>
                  <Tip k="pos.recent_orders" />
                </div>
                <div className="pos-recent-list">
                  {recentOrders.length === 0 ? <p className="pos-empty-muted">سفارش اخیری ثبت نشده است.</p> : null}
                  {recentOrders.map((order) => (
                    <div key={order.id} className="pos-held-item">
                      <div>
                        <strong dir="ltr" data-latin-digits="true">#{order.trackingCode}</strong>
                        <p>
                          {order.customerName} ·{' '}
                          {order.orderKind === 'InternalUse'
                            ? 'مصرف داخلی'
                            : order.orderKind === 'Complimentary'
                              ? 'مهمان / رایگان'
                              : formatPaymentBreakdown(order.paymentMethod, order.paymentLegs)}
                          {order.isExcludedFromAccounting ? ' · خارج از حسابداری' : ''}
                        </p>
                        <small>
                          {formatPrice(order.totalAmount)} تومان · {formatDateTime(order.createdAt)}
                        </small>
                        {canManageManagerNote && order.managerNote ? (
                          <div className="pos-manager-note-snippet">🔒 {order.managerNote}</div>
                        ) : null}
                      </div>
                      <div className="pos-held-actions">
                        {canManageManagerNote ? (
                          <button
                            type="button"
                            className="pos-secondary-button pos-btn-sm"
                            title="مشاهده / ویرایش یادداشت مدیریت"
                            onClick={() => setManagerNoteModalOrder(order)}
                          >
                            🔒
                          </button>
                        ) : null}
                        <button type="button" className="pos-primary-button pos-btn-sm" onClick={() => reprintOrder(order.id)}>
                          چاپ فیش
                        </button>
                        <button type="button" className="pos-secondary-button pos-btn-sm" onClick={() => loadOrder(order.id)}>
                          بارگذاری
                        </button>
                        <button type="button" className="pos-secondary-button pos-btn-sm" onClick={() => copyOrder(order.id)}>
                          تکرار
                        </button>
                      </div>
                    </div>
                  ))}
                </div>
              </div>
            ) : null}
          </div>

          {/* ── ناحیه ۳ (فوتر ثابت و فشردهٔ پرداخت — همیشه ۱۰۰٪ قابل مشاهده) ── */}
          <div className="pos-cart-footer">
            {error ? (
              <div className="pos-action-feedback pos-feedback-error" role="alert">
                <span>{error}</span>
                <button type="button" className="pos-feedback-close" onClick={() => setError('')} aria-label="بستن پیام">
                  ✕
                </button>
              </div>
            ) : null}
            {message ? (
              <div className="pos-action-feedback pos-feedback-success" role="status">
                <span>{message}</span>
                <button type="button" className="pos-feedback-close" onClick={() => setMessage('')} aria-label="بستن پیام">
                  ✕
                </button>
              </div>
            ) : null}
            {!canSubmit ? (
              <div className="pos-action-feedback pos-feedback-error">
                <span>برای ثبت سفارش به مجوز «ثبت / نگه‌داشت» یا «ویرایش سفارش» در صندوق نیاز دارید.</span>
              </div>
            ) : null}

            <div className="pos-totals-box">
              {orderKind === 'Standard' &&
              (totalCombinedDiscount > 0 || cartTotals.taxTotal > 0 || deliveryFee > 0) ? (
                <div className="pos-totals-breakdown">
                  {totalCombinedDiscount > 0 ? (
                    <div className="pos-total-row">
                      <span>جمع قبل از تخفیف</span>
                      <span>{formatPrice(cartTotals.grandTotal)} ت</span>
                    </div>
                  ) : null}
                  {cartTotals.taxTotal > 0 ? (
                    <div className="pos-total-row">
                      <span>مالیات بر ارزش افزوده</span>
                      <span>{formatPrice(cartTotals.taxTotal)} ت</span>
                    </div>
                  ) : null}
                  {deliveryFee > 0 ? (
                    <div className="pos-total-row">
                      <span>هزینه تحویل{selectedDeliveryMethod ? ` (${selectedDeliveryMethod.persianName})` : ''}</span>
                      <span>{formatPrice(deliveryFee)} ت</span>
                    </div>
                  ) : null}
                  {clubPreview.breakdown.map((row) => (
                    <div key={row.key} className="pos-total-row pos-discount-row">
                      <span>{row.label}</span>
                      <strong>−{formatPrice(row.amount)} ت</strong>
                    </div>
                  ))}
                  {manualDiscountPreview.amount > 0 ? (
                    <div className="pos-total-row pos-discount-row">
                      <span>
                        تخفیف دلخواه
                        {manualDiscountType === 'Percent' && manualDiscountPreview.value > 0
                          ? ` (${toPersianNumber(manualDiscountPreview.value)}٪)`
                          : ''}
                      </span>
                      <strong>−{formatPrice(manualDiscountPreview.amount)} ت</strong>
                    </div>
                  ) : null}
                </div>
              ) : null}
              {orderKind !== 'Standard' && cartTotals.itemsTotal > 0 ? (
                <div className="pos-total-row">
                  <span>ارزش اقلام (کسر از انبار)</span>
                  <span>{formatPrice(cartTotals.itemsTotal)} ت</span>
                </div>
              ) : null}
              <div className="pos-total-row pos-grand-total-row">
                <span className="pos-grand-total-label">
                  {orderKind === 'InternalUse'
                    ? 'مبلغ دریافتی (مصرف داخلی)'
                    : orderKind === 'Complimentary'
                      ? 'مبلغ دریافتی (مهمان / رایگان)'
                      : 'جمع کل قابل پرداخت'}
                </span>
                <strong className="pos-grand-total-value">
                  {orderKind === 'Standard' ? formatPrice(effectivePayableTotal) : '۰'} <small>تومان</small>
                </strong>
              </div>
            </div>

            <div className="pos-actions">
              {orderKind === 'Standard' ? (
                <>
                  <button
                    type="button"
                    className="pos-primary-button pos-pay-btn"
                    onClick={() => submitOrder('PayCash')}
                    disabled={submitting || !cart.length}
                  >
                    <span>💵 نقد</span>
                    <kbd>F4</kbd>
                  </button>

                  <button
                    type="button"
                    className="pos-primary-button pos-pay-btn pos-pay-card-btn"
                    onClick={() => submitOrder('PayCard')}
                    disabled={submitting || !cart.length}
                  >
                    <span>💳 کارت‌خوان</span>
                    <kbd>F5</kbd>
                  </button>

                  <button
                    type="button"
                    className="pos-primary-button pos-pay-btn pos-pay-c2c-btn"
                    onClick={handleOpenCardToCard}
                    disabled={submitting || !cart.length}
                  >
                    <span>📲 کارت به کارت</span>
                    <kbd>F6</kbd>
                  </button>

                  <button
                    type="button"
                    className="pos-secondary-button pos-pay-btn pos-pay-split-btn"
                    onClick={() => setSplitPaymentModalOpen(true)}
                    disabled={submitting || !cart.length || effectivePayableTotal <= 0}
                  >
                    <span>⚖️ پرداخت ترکیبی</span>
                  </button>
                </>
              ) : (
                <button
                  type="button"
                  className={`pos-primary-button pos-pay-btn pos-action-cell--wide ${orderKind === 'InternalUse' ? 'pos-internal-submit-btn' : 'pos-complimentary-submit-btn'}`}
                  onClick={() => submitOrder(orderKind)}
                  disabled={submitting || !cart.length}
                >
                  <span>
                    {orderKind === 'InternalUse'
                      ? '☕ ثبت مصرف داخلی (رایگان)'
                      : '🎁 ثبت سفارش مهمان / آشنا (رایگان)'}
                  </span>
                </button>
              )}

              <button
                type="button"
                className="pos-secondary-button pos-sub-action-btn"
                onClick={() => submitOrder('Hold')}
                disabled={submitting || !cart.length}
              >
                <span>⏸ نگه‌داشت</span>
                <kbd>F2</kbd>
              </button>

              <button
                type="button"
                className="pos-danger-button pos-sub-action-btn"
                onClick={cancelCurrentOrder}
                disabled={submitting || (!orderId && !cart.length)}
              >
                <span>{orderId ? '🗑 حذف سفارش' : 'پاک کردن سبد'}</span>
              </button>
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
          <strong>{formatPrice(effectivePayableTotal)} ت</strong>
        </button>
      </nav>

      {cardToCardModalOpen ? (
        <div
          className="pos-modal-backdrop"
          onClick={() => !submitting && setCardToCardModalOpen(false)}
        >
          <div
            className="pos-modal pos-c2c-modal"
            onClick={(event) => event.stopPropagation()}
            role="dialog"
            aria-modal="true"
            aria-label="انتخاب کارت بانکی برای پرداخت کارت به کارت"
          >
            <div className="pos-modal-head">
              <div>
                <h3>📲 پرداخت کارت به کارت</h3>
                <p>کارت مقصد را انتخاب کنید و شماره کارت را برای مشتری بخوانید یا نمایش دهید.</p>
              </div>
              <button
                type="button"
                className="pos-icon-close"
                disabled={submitting}
                onClick={() => setCardToCardModalOpen(false)}
                aria-label="بستن"
              >
                ×
              </button>
            </div>

            <div className="pos-c2c-amount-bar">
              <span>مبلغ قابل پرداخت:</span>
              <strong>{formatPrice(effectivePayableTotal)} تومان</strong>
            </div>

            {adminBankCards.length > 0 ? (
              <>
                {adminBankCards.length > 1 ? (
                  <div className="pos-c2c-card-picker">
                    {adminBankCards.map((card) => (
                      <button
                        key={card.id}
                        type="button"
                        className={`pos-c2c-card-pill ${selectedBankCard?.id === card.id ? 'is-selected' : ''}`}
                        onClick={() => {
                          setSelectedBankCardId(card.id)
                          setCopiedCardNumber(false)
                        }}
                      >
                        <strong>{card.bankName}</strong>
                        <small>{card.cardHolderName}</small>
                        <small dir="ltr" data-latin-digits="true">•••• {card.cardNumber.slice(-4)}</small>
                      </button>
                    ))}
                  </div>
                ) : null}

                {selectedBankCard ? (
                  <div className="pos-c2c-stage">
                    <BankCardVisual card={selectedBankCard} size="large" />
                    <div className="pos-c2c-bold-strip">
                      <div>
                        <div style={{ fontSize: '0.72rem', color: 'var(--muted)' }}>
                          شماره کارت جهت واریز ({selectedBankCard.bankName} — {selectedBankCard.cardHolderName})
                        </div>
                        <div className="pos-c2c-bold-number" dir="ltr" data-latin-digits="true">
                          {formatCardNumberGroups(selectedBankCard.cardNumber)}
                        </div>
                      </div>
                      <button
                        type="button"
                        className="pos-secondary-button"
                        onClick={handleCopySelectedCardNumber}
                      >
                        {copiedCardNumber ? 'کپی شد ✓' : '📋 کپی شماره کارت'}
                      </button>
                    </div>
                  </div>
                ) : null}
              </>
            ) : (
              <div className="pos-empty-hint" style={{ textAlign: 'center', padding: '1rem' }}>
                هنوز کارت بانکی ادمین تعریف نشده است. می‌توانید همین الان پرداخت را ثبت کنید یا از{' '}
                <Link to="/site-settings" onClick={() => setCardToCardModalOpen(false)}>
                  تنظیمات سایت ← کارت‌های بانکی ادمین
                </Link>{' '}
                کارت‌های کافه را تعریف کنید.
              </div>
            )}

            <div className="pos-modal-actions" style={{ display: 'flex', gap: '0.6rem', justifyContent: 'flex-end' }}>
              <button
                type="button"
                className="pos-secondary-button"
                disabled={submitting}
                onClick={() => setCardToCardModalOpen(false)}
              >
                انصراف
              </button>
              <button
                type="button"
                className="pos-primary-button"
                disabled={submitting || !cart.length}
                onClick={async () => {
                  setCardToCardModalOpen(false)
                  await submitOrder('PayCardToCard')
                }}
              >
                {submitting ? 'در حال ثبت...' : '✓ تایید و ثبت پرداخت کارت به کارت'}
              </button>
            </div>
          </div>
        </div>
      ) : null}

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

      {splitPaymentModalOpen ? (
        <PosSplitPaymentModal
          payableTotal={effectivePayableTotal}
          submitting={submitting}
          onClose={() => setSplitPaymentModalOpen(false)}
          onConfirm={(legs) => submitOrder('PaySplit', { paymentAllocations: legs })}
        />
      ) : null}

      {managerNoteModalOrder && canManageManagerNote ? (
        <PosManagerNoteModal
          order={managerNoteModalOrder}
          token={session.token}
          onClose={() => setManagerNoteModalOrder(null)}
          onSaved={async () => {
            setManagerNoteModalOrder(null)
            setMessage('یادداشت محرمانه مدیریت ذخیره شد.')
            await loadBootstrap()
          }}
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
