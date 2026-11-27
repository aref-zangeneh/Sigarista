import { formatPrice } from './posCart.js'
import { formatBundleSummaryLabels } from './bundlePricing.js'
import { buildQrSvg } from './qrSvg.js'

const PAYMENT_LABELS = {
  Cash: 'پرداخت نقدی',
  PayCash: 'پرداخت نقدی',
  Card: 'کارت‌خوان (پوز)',
  PayCard: 'کارت‌خوان (پوز)',
  CardToCard: 'کارت به کارت',
  PayCardToCard: 'کارت به کارت',
  Split: 'پرداخت ترکیبی',
  PaySplit: 'پرداخت ترکیبی',
  OnlineGateway: 'درگاه پرداخت آنلاین',
  Complimentary: 'رایگان (بدون دریافت وجه)',
  InternalUse: 'مصرف داخلی پرسنل (رایگان)',
}

const SHORT_LEG_LABELS = {
  Cash: 'نقدی',
  Card: 'کارت‌خوان (پوز)',
  CardToCard: 'کارت به کارت',
}

const SERVICE_LABELS = {
  DineIn: 'سرو در سالن',
  TakeAway: 'سفارش بیرون‌بر',
}

const PERSIAN_DIGITS = ['۰', '۱', '۲', '۳', '۴', '۵', '۶', '۷', '۸', '۹']

export function toPersianDigits(value) {
  return String(value ?? '')
    .replace(/[0-9]/g, (d) => PERSIAN_DIGITS[Number(d)])
    .replace(/[\u0660-\u0669]/g, (d) => PERSIAN_DIGITS[d.charCodeAt(0) - 0x0660])
}

export function formatReceiptMoney(amount) {
  const num = Math.round(Number(amount || 0))
  if (!Number.isFinite(num)) return '۰'
  const formatted = Math.abs(num)
    .toString()
    .replace(/\B(?=(\d{3})+(?!\d))/g, '٬')
  return (num < 0 ? '-' : '') + toPersianDigits(formatted)
}

function normalizeText(value) {
  return (value ?? '').toString().trim()
}

function formatReceiptDate(date = new Date()) {
  try {
    const parts = new Intl.DateTimeFormat('fa-IR-u-ca-persian', {
      year: 'numeric',
      month: '2-digit',
      day: '2-digit',
      hour: '2-digit',
      minute: '2-digit',
      hour12: false,
      timeZone: 'Asia/Tehran',
    }).formatToParts(date)
    const get = (type) => parts.find((p) => p.type === type)?.value ?? ''
    const y = get('year')
    const m = get('month')
    const d = get('day')
    const hh = get('hour')
    const mm = get('minute')
    if (y && m && d && hh && mm) {
      return toPersianDigits(`${y}/${m}/${d} — ${hh}:${mm}`)
    }
  } catch {
    // Fallback below
  }
  return toPersianDigits(
    date.toLocaleString('fa-IR', {
      year: 'numeric',
      month: '2-digit',
      day: '2-digit',
      hour: '2-digit',
      minute: '2-digit',
      timeZone: 'Asia/Tehran',
    }),
  )
}

export function formatPaymentBreakdown(method, paymentLegs = []) {
  const validLegs = Array.isArray(paymentLegs)
    ? paymentLegs.filter((leg) => leg && Number(leg.amount) > 0)
    : []

  const grouped = new Map()
  for (const leg of validLegs) {
    const key = leg.method || leg.Method
    if (!key) continue
    grouped.set(key, (grouped.get(key) || 0) + Number(leg.amount || leg.Amount || 0))
  }

  if (grouped.size > 1 || (method === 'Split' && grouped.size > 0)) {
    const parts = Array.from(grouped.entries()).map(([key, amt]) => {
      const label = SHORT_LEG_LABELS[key] ?? PAYMENT_LABELS[key] ?? key
      return `${formatReceiptMoney(amt)} ${label}`
    })
    return `پرداخت ترکیبی (${parts.join(' + ')})`
  }

  if (grouped.size === 1) {
    const [singleMethod] = grouped.keys()
    return PAYMENT_LABELS[singleMethod] ?? PAYMENT_LABELS[method] ?? singleMethod
  }

  return PAYMENT_LABELS[method] ?? method ?? 'پرداخت صندوق'
}

function paymentLabel(method, paymentLegs = []) {
  return formatPaymentBreakdown(method, paymentLegs)
}

function serviceLabel(serviceType, deliveryMethodName) {
  if (SERVICE_LABELS[serviceType]) {
    return SERVICE_LABELS[serviceType]
  }
  if (deliveryMethodName) {
    return `ارسال با ${deliveryMethodName}`
  }
  return serviceType ?? '—'
}

function lineDescriptor(line) {
  const parts = []
  if (line.sizeLabel) {
    parts.push(toPersianDigits(line.sizeLabel))
  }
  if (line.options?.length) {
    for (const opt of line.options) {
      const label = typeof opt === 'string' ? opt : opt?.optionName
      if (label) parts.push(`✦ ${toPersianDigits(label)}`)
    }
  }
  if (line.modifierLabels?.length) {
    for (const modifier of line.modifierLabels) {
      if (modifier) parts.push(`✦ ${toPersianDigits(modifier)}`)
    }
  }
  if (line.customDrinkSummary?.length) {
    parts.push(...line.customDrinkSummary.map((entry) => toPersianDigits(entry)))
  }
  if (line.bundleItems?.length) {
    parts.push(
      ...formatBundleSummaryLabels(line.bundleItems, line.quantity).map((entry) =>
        toPersianDigits(entry),
      ),
    )
  }
  return parts.filter(Boolean).join('   ')
}

export function buildPosReceiptBundle(input) {
  const {
    result,
    cart = [],
    customerName,
    customerPhone,
    orderNote,
    serviceType,
    tableName,
    deliveryMethodName,
    deliveryFee = 0,
    taxTotal = 0,
    subtotalAmount,
    siteSettings,
    createdAt,
  } = input

  const layout = siteSettings?.receiptLayout ?? {}
  const customerLayout = layout?.customer ?? {}
  const headerLayout = customerLayout?.header ?? {}
  const footerLayout = customerLayout?.footer ?? {}
  const totalsLayout = customerLayout?.totals ?? {}
  const styleLayout = customerLayout?.style ?? {}

  const cafeName =
    normalizeText(headerLayout.cafeName) || normalizeText(siteSettings?.cafeName) || 'شوگاریستا'
  const subtitle =
    headerLayout.subtitle !== undefined && headerLayout.subtitle !== null
      ? normalizeText(headerLayout.subtitle)
      : 'صورت‌حساب سفارش'
  const showAddress = headerLayout.showAddress !== false
  const showPhone = headerLayout.showPhone !== false
  const address = showAddress
    ? normalizeText(headerLayout.customAddress) || normalizeText(siteSettings?.address)
    : ''
  const contactPhone = showPhone
    ? normalizeText(headerLayout.customPhone) || normalizeText(siteSettings?.contactPhone)
    : ''
  const taxId = normalizeText(headerLayout.taxId)
  const showOrnament = headerLayout.showOrnament !== false
  const ornamentIcon = normalizeText(styleLayout.ornamentIcon) || '☕'
  const dividerStyle = normalizeText(styleLayout.dividerStyle) || 'dashed'
  const codeBoxStyle = normalizeText(styleLayout.codeBoxStyle) || 'rounded'
  const footerText = normalizeText(footerLayout.text) || normalizeText(siteSettings?.footerText)
  const showThanks = footerLayout.showThanks !== false
  const thanksText = normalizeText(footerLayout.thanksText) || 'نوش جان! از میزبانی شما خرسندیم ✨'
  const showCafeNameInFooter = footerLayout.showCafeName !== false
  const footerCafeName = normalizeText(footerLayout.cafeNameText) || cafeName
  const showWifi = footerLayout.showWifi === true
  const wifiSsid = normalizeText(footerLayout.wifiSsid)
  const wifiPassword = normalizeText(footerLayout.wifiPassword)
  const instagramHandle = normalizeText(footerLayout.instagramHandle)
  const websiteUrl = normalizeText(footerLayout.websiteUrl)
  const promoBanner = normalizeText(footerLayout.promoBanner)
  const showQrCode = footerLayout.showQrCode === true
  const qrCodeUrl = normalizeText(footerLayout.qrCodeUrl)
  const qrCodeLabel = normalizeText(footerLayout.qrCodeLabel)
  const showItemCount = totalsLayout.showItemCount === true
  const showSavingsBanner = totalsLayout.showSavingsBanner !== false
  const printedAt = formatReceiptDate(createdAt ? new Date(createdAt) : new Date())
  const trackingCode = result?.trackingCode ?? input?.trackingCode ?? '—'

  const computedSubtotal = cart.reduce(
    (sum, line) => sum + Number(line.unitPrice || 0) * Number(line.quantity || 1),
    0,
  )
  const resolvedSubtotal = Number(subtotalAmount ?? computedSubtotal) || 0
  const resolvedDeliveryFee = Number(deliveryFee || 0) || 0
  const resolvedTaxTotal = Number(taxTotal || 0) || 0
  const orderKind = result?.orderKind ?? input?.orderKind ?? 'Standard'
  const complimentaryReason = normalizeText(result?.complimentaryReason ?? input?.complimentaryReason)
  const isNonRevenue = orderKind === 'InternalUse' || orderKind === 'Complimentary'
  const manualDiscountType = isNonRevenue
    ? null
    : (result?.manualDiscountType ?? input?.manualDiscountType ?? null)
  const manualDiscountValue = isNonRevenue
    ? null
    : (Number(result?.manualDiscountValue ?? input?.manualDiscountValue ?? 0) || null)
  const manualDiscountAmount = isNonRevenue
    ? 0
    : (Number(result?.manualDiscountAmount ?? input?.manualDiscountAmount ?? 0) || 0)
  const manualDiscountReason = isNonRevenue
    ? ''
    : normalizeText(result?.manualDiscountReason ?? input?.manualDiscountReason)
  const discountTotal = isNonRevenue
    ? 0
    : (Number(result?.discountTotal ?? input?.discountTotal ?? manualDiscountAmount) || 0)
  const discountTitle =
    discountTotal > 0
      ? manualDiscountAmount > 0 && manualDiscountAmount === discountTotal
        ? manualDiscountType === 'Percent' && manualDiscountValue
          ? `تخفیف دلخواه (${toPersianDigits(manualDiscountValue)}٪)`
          : 'تخفیف دلخواه'
        : 'تخفیف'
      : null
  const totalAmount = isNonRevenue
    ? 0
    : Number(
        result?.totalAmount ??
          Math.max(0, resolvedSubtotal + resolvedDeliveryFee + resolvedTaxTotal - discountTotal),
      )
  const rawLegs = result?.paymentLegs ?? input?.paymentLegs ?? []
  const paymentMethod =
    orderKind === 'InternalUse'
      ? 'مصرف داخلی پرسنل (رایگان)'
      : orderKind === 'Complimentary'
        ? 'مهمان مجموعه / آشنا (رایگان)'
        : paymentLabel(result?.paymentMethod ?? input?.paymentMethod, rawLegs)
  const service = serviceLabel(serviceType, deliveryMethodName)
  const rawGuestName = normalizeText(customerName)
  const guestName = rawGuestName || (orderKind === 'InternalUse' ? 'مصرف داخلی' : 'مهمان گرامی')
  const rawPhone = normalizeText(customerPhone)
  const guestPhone = rawPhone.toUpperCase() === 'POS' ? '' : rawPhone
  const table = normalizeText(tableName)
  const baseNote = normalizeText(orderNote)
  const note =
    isNonRevenue && complimentaryReason
      ? baseNote
        ? `${baseNote} — بابت: ${complimentaryReason}`
        : `بابت: ${complimentaryReason}`
      : baseNote

  const normalizedLines = cart.map((line) => {
    const qty = Number(line.quantity || 1)
    const unitPrice = Number(line.unitPrice || 0)
    const lineTotal = Number(line.lineTotal ?? unitPrice * qty)
    const modifierLabels = Array.isArray(line.modifierLabels) ? line.modifierLabels : []
    const options = Array.isArray(line.options)
      ? line.options.map((opt) =>
          typeof opt === 'string'
            ? { groupName: null, optionName: opt, priceDeltaLabel: null }
            : {
                groupName: opt.groupName ?? null,
                optionName: opt.optionName ?? opt.label ?? '',
                priceDeltaLabel: opt.priceDelta > 0 ? formatReceiptMoney(opt.priceDelta) : (opt.priceDeltaLabel ?? null),
              },
        )
      : []
    const bundleItems = Array.isArray(line.bundleItems)
      ? line.bundleItems
      : Array.isArray(line.bundle?.components)
        ? line.bundle.components
        : []
    const bundleSummaryLines = formatBundleSummaryLabels(bundleItems, qty).map((entry) =>
      toPersianDigits(entry),
    )
    const customDrinkSummary = [
      ...(Array.isArray(line.customDrinkSummary) ? line.customDrinkSummary : []),
      ...bundleSummaryLines,
    ]
    const baristaNote = normalizeText(line.baristaNote)

    return {
      name: line.name || line.persianName || '',
      sizeLabel: line.sizeLabel ?? null,
      descriptor: lineDescriptor({ ...line, bundleItems }),
      quantity: qty,
      quantityLabel: toPersianDigits(qty),
      unitPrice,
      unitPriceLabel: formatReceiptMoney(unitPrice),
      lineTotal,
      totalLabel: formatReceiptMoney(lineTotal),
      modifierLabels,
      options,
      customDrinkSummary,
      bundleItems,
      baristaNote,
      payerLabel: normalizeText(line.payerLabel) || null,
    }
  })

  const customer = {
    type: 'customer',
    title: subtitle,
    cafeName,
    footerCafeName,
    address,
    contactPhone,
    taxId,
    footerText,
    showThanks,
    thanksText,
    showCafeNameInFooter,
    showOrnament,
    ornamentIcon,
    dividerStyle,
    codeBoxStyle,
    showWifi,
    wifiSsid,
    wifiPassword,
    instagramHandle,
    websiteUrl,
    promoBanner,
    showQrCode,
    qrCodeUrl,
    qrCodeLabel,
    showItemCount,
    showSavingsBanner,
    totalItemCount: normalizedLines.reduce((sum, line) => sum + (line.quantity || 1), 0),
    trackingCode,
    printedAt,
    guestName,
    guestPhone,
    service,
    table,
    note,
    paymentMethod,
    subtotalAmount: resolvedSubtotal,
    deliveryFee: resolvedDeliveryFee,
    taxTotal: resolvedTaxTotal,
    discountTotal,
    discountTitle,
    manualDiscountType,
    manualDiscountValue,
    manualDiscountAmount,
    manualDiscountReason,
    totalAmount,
    layout,
    lines: normalizedLines,
  }

  const barista = {
    type: 'barista',
    title: 'فیش آشپزخانه / باریستا',
    cafeName,
    trackingCode,
    printedAt,
    guestName,
    service,
    table,
    note,
    layout,
    lines: normalizedLines,
  }

  const renderPayload = {
    copyType: 'Customer',
    header: {
      cafeName,
      address: address || null,
      contactPhone: contactPhone || null,
    },
    order: {
      code: trackingCode,
      channelLabel: 'سفارش حضوری (صندوق)',
      serviceLabel: service || null,
      tableLabel: table || null,
      customerName: rawGuestName && rawGuestName !== 'مهمان' && rawGuestName !== 'مهمان گرامی' ? rawGuestName : null,
      customerPhone: guestPhone || null,
      note: note || null,
      createdAtLabel: printedAt,
      paidAtLabel: printedAt,
      paymentLabel: paymentMethod,
      reference: null,
      pickupAtLabel: null,
    },
    items: normalizedLines.map((line) => ({
      name: line.name,
      sizeLabel: line.sizeLabel,
      quantity: line.quantity,
      quantityLabel: line.quantityLabel,
      modifierLabels: line.modifierLabels,
      unitPriceLabel: line.unitPriceLabel,
      totalLabel: line.totalLabel,
      payerLabel: line.payerLabel,
      baristaNote: line.baristaNote || null,
      options: line.options,
      optionTotalLabel: null,
      customDrinkSummary: line.customDrinkSummary,
    })),
    totals: {
      subtotalLabel: formatReceiptMoney(resolvedSubtotal),
      deliveryFeeLabel: resolvedDeliveryFee > 0 ? formatReceiptMoney(resolvedDeliveryFee) : null,
      taxLabel: resolvedTaxTotal > 0 ? formatReceiptMoney(resolvedTaxTotal) : null,
      discountLabel: discountTotal > 0 ? formatReceiptMoney(discountTotal) : null,
      discountTitle: discountTotal > 0 ? discountTitle : null,
      discountNote: discountTotal > 0 && manualDiscountReason ? manualDiscountReason : null,
      totalLabel: formatReceiptMoney(totalAmount),
      amountPaidLabel: formatReceiptMoney(totalAmount),
    },
    footerText: footerText || null,
    timeZoneId: 'Asia/Tehran',
    layout,
  }

  return { customer, barista, renderPayload, trackingCode }
}

function padLine(left, right, width = 32) {
  const l = left.toString()
  const r = right.toString()
  const space = Math.max(1, width - l.length - r.length)
  return `${l}${' '.repeat(space)}${r}`
}

function divider(width = 32) {
  return '-'.repeat(width)
}

export function formatCustomerReceiptText(receipt) {
  const width = 32
  const lines = [
    receipt.cafeName,
    receipt.address,
    receipt.contactPhone,
    divider(width),
    receipt.title,
    `شماره: ${receipt.trackingCode}`,
    `زمان: ${receipt.printedAt}`,
    `نوع: ${receipt.service}${receipt.table ? ` · میز ${receipt.table}` : ''}`,
    `مشتری: ${receipt.guestName}`,
  ]

  if (receipt.guestPhone) {
    lines.push(`تلفن: ${receipt.guestPhone}`)
  }

  if (receipt.note) {
    lines.push(`یادداشت: ${receipt.note}`)
  }

  lines.push(divider(width))

  receipt.lines.forEach((line) => {
    lines.push(`${line.quantity} × ${line.name}`)
    if (line.descriptor && line.descriptor !== line.name) {
      lines.push(`  ${line.descriptor}`)
    }
    lines.push(padLine(formatPrice(line.unitPrice), formatPrice(line.lineTotal), width))
  })

  lines.push(divider(width))
  if (receipt.discountTotal > 0) {
    lines.push(padLine(receipt.discountTitle || 'تخفیف', `-${formatPrice(receipt.discountTotal)} تومان`, width))
    if (receipt.manualDiscountReason) {
      lines.push(`بابت تخفیف: ${receipt.manualDiscountReason}`)
    }
  }
  lines.push(padLine('مبلغ قابل پرداخت', `${formatPrice(receipt.totalAmount)} تومان`, width))
  lines.push(`شیوهٔ پرداخت: ${receipt.paymentMethod}`)
  lines.push('')
  lines.push('نوش جان! از میزبانی شما خرسندیم ✨')
  lines.push('')

  return lines.join('\n')
}

export function formatBaristaReceiptText(receipt) {
  const width = 32
  const lines = [
    receipt.cafeName,
    '*** باریستا ***',
    divider(width),
    `سفارش #${receipt.trackingCode}`,
    `زمان: ${receipt.printedAt}`,
    `${receipt.service}${receipt.table ? ` · میز ${receipt.table}` : ''}`,
    `مشتری: ${receipt.guestName}`,
  ]

  if (receipt.note) {
    lines.push(`یادداشت سفارش: ${receipt.note}`)
  }

  lines.push(divider(width))

  receipt.lines.forEach((line, index) => {
    lines.push(`${line.quantity} × ${line.name}`)
    if (line.sizeLabel) {
      lines.push(`  اندازه: ${line.sizeLabel}`)
    }
    if (line.modifierLabels.length) {
      lines.push(`  تغییرات: ${line.modifierLabels.join('، ')}`)
    }
    if (line.baristaNote) {
      lines.push(`  >> ${line.baristaNote}`)
    }
    if (index < receipt.lines.length - 1) {
      lines.push('')
    }
  })

  lines.push(divider(width))
  lines.push('')

  return lines.join('\n')
}

function escapeHtml(value) {
  return (value ?? '')
    .toString()
    .replace(/&/g, '&amp;')
    .replace(/</g, '&lt;')
    .replace(/>/g, '&gt;')
    .replace(/"/g, '&quot;')
}

function buildCustomerBodyHtml(receipt) {
  const dividerClass =
    receipt.dividerStyle === 'dotted'
      ? 'divider divider--dotted'
      : receipt.dividerStyle === 'solid'
        ? 'divider divider--solid'
        : receipt.dividerStyle === 'double'
          ? 'divider divider--double'
          : 'divider'
  const ornamentIcon = receipt.ornamentIcon && receipt.ornamentIcon !== 'none' ? receipt.ornamentIcon : ''
  const ornamentHtml =
    receipt.showOrnament !== false && ornamentIcon
      ? `<div class="cafe-ornament">✦ ${escapeHtml(ornamentIcon)} ✦</div>`
      : `<div class="${dividerClass}"></div>`
  const codeBadgeClass =
    receipt.codeBoxStyle === 'solid'
      ? 'ticket-badge ticket-badge--solid'
      : receipt.codeBoxStyle === 'minimal'
        ? 'ticket-badge ticket-badge--minimal'
        : 'ticket-badge'
  const qrSvg = receipt.showQrCode && receipt.qrCodeUrl ? buildQrSvg(receipt.qrCodeUrl, 128) : ''
  const cleanIg = receipt.instagramHandle
    ? receipt.instagramHandle.startsWith('@')
      ? receipt.instagramHandle
      : `@${receipt.instagramHandle}`
    : ''

  return `
    <div class="center brand">${escapeHtml(toPersianDigits(receipt.cafeName))}</div>
    ${receipt.title ? `<div class="center section-title" style="font-size:12px;color:#444;margin-bottom:6px;">${escapeHtml(toPersianDigits(receipt.title))}</div>` : ''}
    ${receipt.address ? `<div class="center muted">${escapeHtml(toPersianDigits(receipt.address))}</div>` : ''}
    ${receipt.contactPhone ? `<div class="center muted">تلفن تماس: ${escapeHtml(toPersianDigits(receipt.contactPhone))}</div>` : ''}
    ${receipt.taxId ? `<div class="center muted">شناسه اقتصادی / صنفی: ${escapeHtml(toPersianDigits(receipt.taxId))}</div>` : ''}
    ${ornamentHtml}
    <div class="${codeBadgeClass}">شماره سفارش: #${escapeHtml(receipt.trackingCode)}</div>
    <div class="row"><span>تاریخ و ساعت</span><span>${escapeHtml(receipt.printedAt)}</span></div>
    <div class="row"><span>شیوهٔ پذیرایی</span><strong>${escapeHtml(toPersianDigits(receipt.service))}${receipt.table ? ` · میز شماره ${escapeHtml(toPersianDigits(receipt.table))}` : ''}</strong></div>
    <div class="row"><span>مشتری گرامی</span><span>${escapeHtml(toPersianDigits(receipt.guestName))}</span></div>
    ${receipt.guestPhone ? `<div class="row"><span>شماره همراه</span><span>${escapeHtml(toPersianDigits(receipt.guestPhone))}</span></div>` : ''}
    ${receipt.note ? `<div class="line-meta" style="margin-top:4px;">توضیحات مهمان: ${escapeHtml(toPersianDigits(receipt.note))}</div>` : ''}
    <div class="${dividerClass}"></div>
    ${receipt.lines
      .map(
        (line) => `
          <div class="line-title">${escapeHtml(toPersianDigits(line.quantity))} × ${escapeHtml(toPersianDigits(line.name))}</div>
          ${line.descriptor ? `<div class="line-meta" style="padding-right:8px;">${escapeHtml(line.descriptor)}</div>` : ''}
          ${line.quantity > 1 ? `<div class="line-meta" style="padding-right:8px;font-size:10px;color:#555;">(نرخ واحد: ${escapeHtml(formatPrice(line.unitPrice))} تومان)</div>` : ''}
          <div class="row" style="margin-top:2px;"><span>جمع:</span><strong>${escapeHtml(formatPrice(line.lineTotal))} تومان</strong></div>
        `,
      )
      .join('')}
    <div class="${dividerClass}"></div>
    ${receipt.showItemCount ? `<div class="row"><span>تعداد کل اقلام</span><span>${escapeHtml(toPersianDigits(receipt.totalItemCount ?? receipt.lines.length))} عدد</span></div>` : ''}
    <div class="row"><span>جمع کل اقلام</span><span>${escapeHtml(formatPrice(receipt.subtotalAmount))} تومان</span></div>
    ${receipt.deliveryFee > 0 ? `<div class="row"><span>هزینه ارسال / پیک</span><span>${escapeHtml(formatPrice(receipt.deliveryFee))} تومان</span></div>` : ''}
    ${receipt.taxTotal > 0 ? `<div class="row"><span>مالیات بر ارزش افزوده</span><span>${escapeHtml(formatPrice(receipt.taxTotal))} تومان</span></div>` : ''}
    ${receipt.discountTotal > 0 ? `<div class="row" style="font-weight:700;"><span>${escapeHtml(receipt.discountTitle || 'تخفیف')}</span><span>−${escapeHtml(formatPrice(receipt.discountTotal))} تومان</span></div>` : ''}
    ${receipt.discountTotal > 0 && receipt.manualDiscountReason ? `<div class="line-meta" style="margin-bottom:4px;">بابت تخفیف: ${escapeHtml(toPersianDigits(receipt.manualDiscountReason))}</div>` : ''}
    <div class="row total"><span>مبلغ قابل پرداخت</span><span>${escapeHtml(formatPrice(receipt.totalAmount))} تومان</span></div>
    ${receipt.discountTotal > 0 && receipt.showSavingsBanner !== false ? `<div class="receipt-box">✨ سود شما از این سفارش: ${escapeHtml(formatPrice(receipt.discountTotal))} تومان ✨</div>` : ''}
    <div class="row" style="margin-top:6px;"><span>شیوهٔ پرداخت</span><span>${escapeHtml(receipt.paymentMethod)}</span></div>
    <div class="row" style="font-weight:700;"><span>وضعیت پرداخت</span><span>تسویه شد ✓</span></div>
    <div class="divider divider--dotted" style="margin:10px 0;"></div>
    ${receipt.promoBanner ? `<div class="receipt-box receipt-box--promo">${escapeHtml(toPersianDigits(receipt.promoBanner))}</div>` : ''}
    ${receipt.showWifi && (receipt.wifiSsid || receipt.wifiPassword) ? `<div class="receipt-box"><div style="font-weight:800;">وای‌فای رایگان مهمان (Wi-Fi)</div>${receipt.wifiSsid ? `<div>شبکه: <span dir="ltr">${escapeHtml(receipt.wifiSsid)}</span></div>` : ''}${receipt.wifiPassword ? `<div>رمز عبور: <span dir="ltr">${escapeHtml(receipt.wifiPassword)}</span></div>` : ''}</div>` : ''}
    ${qrSvg ? `<div class="center" style="margin:8px 0;">${qrSvg}${receipt.qrCodeLabel ? `<div class="muted" style="margin-top:3px;">${escapeHtml(toPersianDigits(receipt.qrCodeLabel))}</div>` : ''}</div>` : ''}
    ${cleanIg ? `<div class="center muted">اینستاگرام: <span dir="ltr">${escapeHtml(cleanIg)}</span></div>` : ''}
    ${receipt.websiteUrl ? `<div class="center muted">وب‌سایت: <span dir="ltr">${escapeHtml(receipt.websiteUrl)}</span></div>` : ''}
    ${receipt.footerText ? `<div class="center muted" style="margin-top:4px;margin-bottom:4px;">${escapeHtml(toPersianDigits(receipt.footerText))}</div>` : ''}
    ${receipt.showThanks !== false ? `<div class="center" style="font-weight:700;font-size:12px;margin:6px 0;">${escapeHtml(toPersianDigits(receipt.thanksText || receipt.layout?.customer?.footer?.thanksText || 'نوش جان! از میزبانی شما خرسندیم ✨'))}</div>` : ''}
    ${receipt.showCafeNameInFooter !== false ? `<div class="center" style="font-weight:700;font-size:11px;margin:4px 0;">« ${escapeHtml(toPersianDigits(receipt.footerCafeName || receipt.cafeName))} »</div>` : ''}
    ${ornamentHtml}
  `
}

function buildBaristaBodyHtml(receipt) {
  return `
    <div class="center brand">${escapeHtml(toPersianDigits(receipt.cafeName))}</div>
    <div class="center section-title">*** فیش آشپزخانه / باریستا ***</div>
    <div class="divider"></div>
    <div class="center ticket-code">#${escapeHtml(receipt.trackingCode)}</div>
    <div class="row"><span>زمان</span><span>${escapeHtml(receipt.printedAt)}</span></div>
    <div class="row"><span>نوع</span><span>${escapeHtml(toPersianDigits(receipt.service))}${receipt.table ? ` · میز ${escapeHtml(toPersianDigits(receipt.table))}` : ''}</span></div>
    <div class="row"><span>مشتری</span><span>${escapeHtml(toPersianDigits(receipt.guestName))}</span></div>
    ${receipt.note ? `<div class="barista-note">یادداشت سفارش: ${escapeHtml(toPersianDigits(receipt.note))}</div>` : ''}
    <div class="divider"></div>
    ${receipt.lines
      .map(
        (line) => `
          <div class="line-title">${escapeHtml(toPersianDigits(line.quantity))} × ${escapeHtml(toPersianDigits(line.name))}</div>
          ${line.sizeLabel ? `<div class="line-meta">اندازه: ${escapeHtml(toPersianDigits(line.sizeLabel))}</div>` : ''}
          ${line.options?.length ? `<div class="line-meta">${escapeHtml(line.options.map((o) => `✦ ${toPersianDigits(typeof o === 'string' ? o : o.optionName)}`).join('   '))}</div>` : ''}
          ${line.modifierLabels?.length ? `<div class="line-meta">${escapeHtml(line.modifierLabels.map((m) => `✦ ${toPersianDigits(m)}`).join('   '))}</div>` : ''}
          ${line.baristaNote ? `<div class="barista-note">! ${escapeHtml(toPersianDigits(line.baristaNote))}</div>` : ''}
        `,
      )
      .join('')}
  `
}

function wrapReceiptHtmlDocument(title, bodyHtml) {
  return `<!DOCTYPE html>
<html lang="fa" dir="rtl">
<head>
  <meta charset="utf-8" />
  <title>${escapeHtml(title)}</title>
  <style>
    @page { size: 72mm auto; margin: 0; }
    html, body { margin: 0; padding: 0; background: #fff; }
    body {
      font-family: Vazirmatn, Tahoma, Arial, sans-serif;
      font-size: 12px;
      line-height: 1.5;
      color: #000;
      width: 72mm;
      box-sizing: border-box;
    }
    .receipt {
      padding: 3mm 2mm;
      box-sizing: border-box;
    }
    .receipt-page-break {
      page-break-after: always;
      break-after: page;
    }
    .center { text-align: center; }
    .brand { font-size: 16px; font-weight: 800; margin-bottom: 4px; }
    .section-title { font-weight: 700; margin-bottom: 4px; }
    .muted { color: #222; font-size: 11px; }
    .divider { border-top: 1px dashed #000; margin: 8px 0; }
    .divider--dotted { border-top-style: dotted; }
    .divider--solid { border-top-style: solid; }
    .divider--double { border-top: 3px double #000; }
    .ticket-badge--solid { border-radius: 0; border-width: 2px; }
    .ticket-badge--minimal { border: none; padding: 2px 0; }
    .receipt-box {
      border: 1.5px solid #000;
      border-radius: 6px;
      padding: 5px 8px;
      text-align: center;
      font-size: 11px;
      margin: 6px 0;
    }
    .receipt-box--promo {
      border-style: double;
      border-width: 3px;
      font-weight: 700;
    }
    .row { display: flex; justify-content: space-between; gap: 8px; }
    .line-title { font-weight: 700; margin-top: 6px; font-size: 13px; }
    .line-meta { color: #111; font-size: 11px; margin-top: 2px; }
    .barista-note {
      margin-top: 4px;
      padding: 4px 6px;
      border: 1.5px solid #000;
      font-weight: 700;
      font-size: 12px;
    }
    .ticket-code {
      font-size: 22px;
      font-weight: 800;
      letter-spacing: 1px;
      margin: 6px 0;
    }
    .ticket-badge {
      border: 1.5px solid #000;
      border-radius: 6px;
      padding: 4px 8px;
      text-align: center;
      font-weight: 800;
      font-size: 13px;
      margin: 6px 0;
    }
    .total {
      font-size: 15px;
      font-weight: 800;
      border-top: 2px solid #000;
      border-bottom: 2px solid #000;
      padding: 5px 0;
      margin: 6px 0;
    }
    .cafe-ornament {
      display: flex;
      align-items: center;
      justify-content: center;
      gap: 8px;
      margin: 8px 0;
      font-size: 11px;
      letter-spacing: 3px;
      color: #000;
    }
    .cafe-ornament::before,
    .cafe-ornament::after {
      content: '';
      flex: 1;
      border-top: 1.5px solid #000;
    }
  </style>
</head>
<body>
  ${bodyHtml}
</body>
</html>`
}

export function buildCustomerReceiptHtml(receipt) {
  return wrapReceiptHtmlDocument(receipt.title, `<div class="receipt">${buildCustomerBodyHtml(receipt)}</div>`)
}

export function buildBaristaReceiptHtml(receipt) {
  return wrapReceiptHtmlDocument(receipt.title, `<div class="receipt">${buildBaristaBodyHtml(receipt)}</div>`)
}

export function buildCombinedReceiptsHtml(bundle, copies = ['customer', 'barista']) {
  const blocks = []
  if (copies.includes('customer')) {
    blocks.push(
      `<div class="receipt ${copies.includes('barista') ? 'receipt-page-break' : ''}">${buildCustomerBodyHtml(bundle.customer)}</div>`,
    )
  }
  if (copies.includes('barista')) {
    blocks.push(`<div class="receipt">${buildBaristaBodyHtml(bundle.barista)}</div>`)
  }
  return wrapReceiptHtmlDocument(`فیش سفارش #${bundle.trackingCode}`, blocks.join('\n'))
}

const PRINT_AGENT_URL =
  (typeof import.meta !== 'undefined' && import.meta.env?.VITE_POS_PRINT_AGENT_URL) ||
  'http://127.0.0.1:9101'

export async function fetchPrintAgentHealth() {
  const controller = new AbortController()
  const timeout = window.setTimeout(() => controller.abort(), 2500)
  try {
    const response = await fetch(`${PRINT_AGENT_URL}/health`, {
      method: 'GET',
      signal: controller.signal,
    })
    if (!response.ok) {
      return { connected: false }
    }
    const data = await response.json()
    return {
      connected: Boolean(data?.ok),
      printerName: data?.printerName || 'GA-E200I',
      printerReady: data?.printerReady !== false,
      printerStatus: data?.printerStatus || 'ready',
      paperWidthMm: data?.paperWidthMm || 80,
      printableWidthMm: data?.printableWidthMm || 72,
      format: data?.format || 'escpos-raw',
    }
  } catch {
    return { connected: false }
  } finally {
    window.clearTimeout(timeout)
  }
}

export async function fetchPosReceiptPreviews(bundle) {
  const controller = new AbortController()
  const timeout = window.setTimeout(() => controller.abort(), 6000)
  try {
    const response = await fetch(`${PRINT_AGENT_URL}/preview`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({
        payload: bundle.renderPayload,
        customer: bundle.customer,
        barista: bundle.barista,
        trackingCode: bundle.trackingCode,
        holdAutoPrint: true,
      }),
      signal: controller.signal,
    })
    if (!response.ok) {
      return null
    }
    const data = await response.json()
    if (!data?.ok) return null
    return {
      customerPng: data.customerPng || null,
      baristaPng: data.baristaPng || null,
    }
  } catch {
    return null
  } finally {
    window.clearTimeout(timeout)
  }
}

function printHtmlDocument(html) {
  return new Promise((resolve) => {
    const iframe = document.createElement('iframe')
    iframe.setAttribute('aria-hidden', 'true')
    iframe.style.position = 'fixed'
    iframe.style.right = '0'
    iframe.style.bottom = '0'
    iframe.style.width = '0'
    iframe.style.height = '0'
    iframe.style.border = '0'
    document.body.appendChild(iframe)

    const frameWindow = iframe.contentWindow
    let settled = false
    const cleanup = () => {
      if (settled) return
      settled = true
      window.setTimeout(() => {
        iframe.remove()
      }, 500)
      resolve()
    }

    if (!frameWindow) {
      cleanup()
      return
    }

    frameWindow.document.open()
    frameWindow.document.write(html)
    frameWindow.document.close()

    const triggerPrint = () => {
      let fallbackTimer = null
      let focusTimer = null
      const mediaQueryList =
        typeof frameWindow.matchMedia === 'function' ? frameWindow.matchMedia('print') : null

      const finish = () => {
        if (fallbackTimer) window.clearTimeout(fallbackTimer)
        if (focusTimer) window.clearTimeout(focusTimer)
        frameWindow.removeEventListener('afterprint', finish)
        window.removeEventListener('afterprint', finish)
        window.removeEventListener('focus', handleParentFocus)
        if (mediaQueryList) {
          if (mediaQueryList.removeEventListener) {
            mediaQueryList.removeEventListener('change', handleMediaChange)
          } else if (mediaQueryList.removeListener) {
            mediaQueryList.removeListener(handleMediaChange)
          }
        }
        cleanup()
      }

      const handleMediaChange = (event) => {
        if (!event.matches) finish()
      }

      // On Safari / some Windows Chrome & Edge setups, iframe afterprint never fires;
      // when the OS print dialog closes, focus returns to the parent window immediately.
      const handleParentFocus = () => {
        if (focusTimer) window.clearTimeout(focusTimer)
        focusTimer = window.setTimeout(finish, 350)
      }

      frameWindow.addEventListener('afterprint', finish)
      window.addEventListener('afterprint', finish)
      window.addEventListener('focus', handleParentFocus)
      if (mediaQueryList) {
        if (mediaQueryList.addEventListener) {
          mediaQueryList.addEventListener('change', handleMediaChange)
        } else if (mediaQueryList.addListener) {
          mediaQueryList.addListener(handleMediaChange)
        }
      }

      try {
        frameWindow.focus()
        frameWindow.print()
      } catch {
        finish()
        return
      }

      // Hard safety cleanup (15s) so the modal can never stay stuck on any system
      fallbackTimer = window.setTimeout(finish, 15000)
    }

    if (frameWindow.document.readyState === 'complete') {
      triggerPrint()
    } else {
      iframe.onload = triggerPrint
    }
  })
}

async function tryPrintAgent(bundle, copies = ['customer', 'barista']) {
  const controller = new AbortController()
  const timeout = window.setTimeout(() => controller.abort(), 15000)

  try {
    const response = await fetch(`${PRINT_AGENT_URL}/print`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({
        payload: bundle.renderPayload,
        customer: bundle.customer,
        barista: bundle.barista,
        copies,
        trackingCode: bundle.trackingCode,
      }),
      signal: controller.signal,
    })

    if (!response.ok) {
      const errBody = await response.json().catch(() => ({}))
      throw new Error(errBody?.error || `Print agent responded with ${response.status}`)
    }

    return true
  } catch {
    return false
  } finally {
    window.clearTimeout(timeout)
  }
}

export async function printPosReceipts(inputOrBundle, options = {}) {
  const bundle =
    inputOrBundle?.customer && inputOrBundle?.barista
      ? inputOrBundle
      : buildPosReceiptBundle(inputOrBundle)
  const copies =
    Array.isArray(options.copies) && options.copies.length
      ? options.copies
      : ['customer', 'barista']

  if (!options.forceBrowser) {
    const agentPrinted = await tryPrintAgent(bundle, copies)
    if (agentPrinted) {
      return { method: 'agent', bundle, copies }
    }
  }

  await printHtmlDocument(buildCombinedReceiptsHtml(bundle, copies))
  return { method: 'browser', bundle, copies }
}
