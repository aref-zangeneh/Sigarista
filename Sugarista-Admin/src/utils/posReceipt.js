import { formatPrice } from './posCart.js'

const PAYMENT_LABELS = {
  Cash: 'نقدی (صندوق)',
  PayCash: 'نقدی (صندوق)',
  Card: 'کارت (صندوق)',
  PayCard: 'کارت (صندوق)',
  OnlineGateway: 'پرداخت آنلاین',
}

const SERVICE_LABELS = {
  DineIn: 'سرو در محل',
  TakeAway: 'بیرون‌بر',
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

function paymentLabel(method) {
  return PAYMENT_LABELS[method] ?? method ?? 'پرداخت صندوق'
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
    parts.push(line.sizeLabel)
  }
  if (line.modifierLabels?.length) {
    parts.push(line.modifierLabels.join('، '))
  }
  if (line.options?.length) {
    parts.push(
      line.options
        .map((opt) => (typeof opt === 'string' ? opt : opt?.optionName))
        .filter(Boolean)
        .join('، '),
    )
  }
  if (line.customDrinkSummary?.length) {
    parts.push(line.customDrinkSummary.join('، '))
  }
  return parts.filter(Boolean).join(' · ')
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

  const cafeName = normalizeText(siteSettings?.cafeName) || 'شوگاریستا'
  const address = normalizeText(siteSettings?.address)
  const contactPhone = normalizeText(siteSettings?.contactPhone)
  const footerText = normalizeText(siteSettings?.footerText)
  const layout = siteSettings?.receiptLayout ?? {}
  const printedAt = formatReceiptDate(createdAt ? new Date(createdAt) : new Date())
  const trackingCode = result?.trackingCode ?? input?.trackingCode ?? '—'

  const computedSubtotal = cart.reduce(
    (sum, line) => sum + Number(line.unitPrice || 0) * Number(line.quantity || 1),
    0,
  )
  const resolvedSubtotal = Number(subtotalAmount ?? computedSubtotal) || 0
  const resolvedDeliveryFee = Number(deliveryFee || 0) || 0
  const resolvedTaxTotal = Number(taxTotal || 0) || 0
  const totalAmount = Number(
    result?.totalAmount ?? resolvedSubtotal + resolvedDeliveryFee + resolvedTaxTotal,
  )
  const paymentMethod = paymentLabel(result?.paymentMethod ?? input?.paymentMethod)
  const service = serviceLabel(serviceType, deliveryMethodName)
  const rawGuestName = normalizeText(customerName)
  const guestName = rawGuestName || 'مهمان'
  const rawPhone = normalizeText(customerPhone)
  const guestPhone = rawPhone.toUpperCase() === 'POS' ? '' : rawPhone
  const table = normalizeText(tableName)
  const note = normalizeText(orderNote)

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
    const customDrinkSummary = Array.isArray(line.customDrinkSummary) ? line.customDrinkSummary : []
    const baristaNote = normalizeText(line.baristaNote)

    return {
      name: line.name || line.persianName || '',
      sizeLabel: line.sizeLabel ?? null,
      descriptor: lineDescriptor(line),
      quantity: qty,
      quantityLabel: toPersianDigits(qty),
      unitPrice,
      unitPriceLabel: formatReceiptMoney(unitPrice),
      lineTotal,
      totalLabel: formatReceiptMoney(lineTotal),
      modifierLabels,
      options,
      customDrinkSummary,
      baristaNote,
      payerLabel: normalizeText(line.payerLabel) || null,
    }
  })

  const customer = {
    type: 'customer',
    title: 'فیش مشتری',
    cafeName,
    address,
    contactPhone,
    footerText,
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
      channelLabel: 'سفارش صندوق (POS)',
      serviceLabel: service || null,
      tableLabel: table || null,
      customerName: rawGuestName && rawGuestName !== 'مهمان' ? rawGuestName : null,
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
  lines.push(padLine('جمع کل', `${formatPrice(receipt.totalAmount)} تومان`, width))
  lines.push(`پرداخت: ${receipt.paymentMethod}`)
  lines.push('')
  lines.push('با تشکر از حضور شما')
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
  return `
    <div class="center brand">${escapeHtml(receipt.cafeName)}</div>
    ${receipt.address ? `<div class="center muted">${escapeHtml(receipt.address)}</div>` : ''}
    ${receipt.contactPhone ? `<div class="center muted">${escapeHtml(receipt.contactPhone)}</div>` : ''}
    <div class="divider"></div>
    <div class="center section-title">${escapeHtml(receipt.title)}</div>
    <div class="row"><span>کد سفارش</span><strong>#${escapeHtml(receipt.trackingCode)}</strong></div>
    <div class="row"><span>زمان</span><span>${escapeHtml(receipt.printedAt)}</span></div>
    <div class="row"><span>نوع سفارش</span><span>${escapeHtml(receipt.service)}${receipt.table ? ` · میز ${escapeHtml(receipt.table)}` : ''}</span></div>
    <div class="row"><span>مشتری</span><span>${escapeHtml(receipt.guestName)}</span></div>
    ${receipt.guestPhone ? `<div class="row"><span>تلفن</span><span>${escapeHtml(receipt.guestPhone)}</span></div>` : ''}
    ${receipt.note ? `<div class="line-meta">یادداشت: ${escapeHtml(receipt.note)}</div>` : ''}
    <div class="divider"></div>
    ${receipt.lines
      .map(
        (line) => `
          <div class="line-title">${escapeHtml(toPersianDigits(line.quantity))} × ${escapeHtml(line.name)}</div>
          ${line.descriptor ? `<div class="line-meta">${escapeHtml(line.descriptor)}</div>` : ''}
          <div class="row"><span>${escapeHtml(formatPrice(line.unitPrice))}</span><span>${escapeHtml(formatPrice(line.lineTotal))}</span></div>
        `,
      )
      .join('')}
    <div class="divider"></div>
    ${receipt.deliveryFee > 0 ? `<div class="row"><span>هزینه ارسال</span><span>${escapeHtml(formatPrice(receipt.deliveryFee))}</span></div>` : ''}
    ${receipt.taxTotal > 0 ? `<div class="row"><span>مالیات</span><span>${escapeHtml(formatPrice(receipt.taxTotal))}</span></div>` : ''}
    <div class="row total"><span>مبلغ نهایی</span><span>${escapeHtml(formatPrice(receipt.totalAmount))} تومان</span></div>
    <div class="row"><span>روش پرداخت</span><span>${escapeHtml(receipt.paymentMethod)}</span></div>
    <div class="center muted" style="margin-top:10px;">${escapeHtml(receipt.layout?.customer?.footer?.thanksText || 'FEEL QUALITY AS YOU DRINK')}</div>
  `
}

function buildBaristaBodyHtml(receipt) {
  return `
    <div class="center brand">${escapeHtml(receipt.cafeName)}</div>
    <div class="center section-title">*** فیش آشپزخانه / باریستا ***</div>
    <div class="divider"></div>
    <div class="center ticket-code">#${escapeHtml(receipt.trackingCode)}</div>
    <div class="row"><span>زمان</span><span>${escapeHtml(receipt.printedAt)}</span></div>
    <div class="row"><span>نوع</span><span>${escapeHtml(receipt.service)}${receipt.table ? ` · میز ${escapeHtml(receipt.table)}` : ''}</span></div>
    <div class="row"><span>مشتری</span><span>${escapeHtml(receipt.guestName)}</span></div>
    ${receipt.note ? `<div class="barista-note">یادداشت سفارش: ${escapeHtml(receipt.note)}</div>` : ''}
    <div class="divider"></div>
    ${receipt.lines
      .map(
        (line) => `
          <div class="line-title">${escapeHtml(toPersianDigits(line.quantity))} × ${escapeHtml(line.name)}</div>
          ${line.sizeLabel ? `<div class="line-meta">اندازه: ${escapeHtml(line.sizeLabel)}</div>` : ''}
          ${line.options?.length ? `<div class="line-meta">گزینه‌ها: ${escapeHtml(line.options.map((o) => (typeof o === 'string' ? o : o.optionName)).join('، '))}</div>` : ''}
          ${line.modifierLabels?.length ? `<div class="line-meta">تغییرات: ${escapeHtml(line.modifierLabels.join('، '))}</div>` : ''}
          ${line.baristaNote ? `<div class="barista-note">! ${escapeHtml(line.baristaNote)}</div>` : ''}
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
    .total {
      font-size: 14px;
      font-weight: 800;
      margin-top: 4px;
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
      const handleAfterPrint = () => {
        frameWindow.removeEventListener('afterprint', handleAfterPrint)
        cleanup()
      }

      frameWindow.addEventListener('afterprint', handleAfterPrint)
      frameWindow.focus()
      frameWindow.print()

      // Fallback cleanup only after 60s in case browser does not fire afterprint
      window.setTimeout(() => {
        frameWindow.removeEventListener('afterprint', handleAfterPrint)
        cleanup()
      }, 60000)
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
