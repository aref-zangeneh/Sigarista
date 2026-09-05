import { formatPrice } from './posCart.js'

const PAYMENT_LABELS = {
  Cash: 'نقد',
  PayCash: 'نقد',
  Card: 'کارت',
  PayCard: 'کارت',
}

const SERVICE_LABELS = {
  DineIn: 'داخل سالن',
  TakeAway: 'بیرون‌بر',
}

function normalizeText(value) {
  return (value ?? '').toString().trim()
}

function formatReceiptDate(date = new Date()) {
  return date.toLocaleString('fa-IR', {
    year: 'numeric',
    month: '2-digit',
    day: '2-digit',
    hour: '2-digit',
    minute: '2-digit',
  })
}

function paymentLabel(method) {
  return PAYMENT_LABELS[method] ?? method ?? '—'
}

function serviceLabel(serviceType) {
  return SERVICE_LABELS[serviceType] ?? serviceType ?? '—'
}

function lineDescriptor(line) {
  const parts = [line.name]
  if (line.sizeLabel) {
    parts.push(line.sizeLabel)
  }
  if (line.modifierLabels?.length) {
    parts.push(line.modifierLabels.join('، '))
  }
  return parts.filter(Boolean).join(' · ')
}

export function buildPosReceiptBundle(input) {
  const {
    result,
    cart,
    customerName,
    customerPhone,
    orderNote,
    serviceType,
    tableName,
    siteSettings,
  } = input

  const cafeName = normalizeText(siteSettings?.cafeName) || 'شوگاریستا'
  const address = normalizeText(siteSettings?.address)
  const contactPhone = normalizeText(siteSettings?.contactPhone)
  const printedAt = formatReceiptDate()
  const trackingCode = result?.trackingCode ?? '—'
  const totalAmount = Number(result?.totalAmount ?? cart.reduce((sum, line) => sum + line.unitPrice * line.quantity, 0))
  const paymentMethod = paymentLabel(result?.paymentMethod)
  const service = serviceLabel(serviceType)
  const guestName = normalizeText(customerName) || 'مهمان'
  const guestPhone = normalizeText(customerPhone)
  const table = normalizeText(tableName)
  const note = normalizeText(orderNote)

  const customer = {
    type: 'customer',
    title: 'رسید مشتری',
    cafeName,
    address,
    contactPhone,
    trackingCode,
    printedAt,
    guestName,
    guestPhone,
    service,
    table,
    note,
    paymentMethod,
    totalAmount,
    lines: cart.map((line) => ({
      name: line.name,
      descriptor: lineDescriptor(line),
      quantity: line.quantity,
      unitPrice: line.unitPrice,
      lineTotal: line.unitPrice * line.quantity,
    })),
  }

  const barista = {
    type: 'barista',
    title: 'رسید باریستا',
    cafeName,
    trackingCode,
    printedAt,
    guestName,
    service,
    table,
    note,
    lines: cart.map((line) => ({
      name: line.name,
      sizeLabel: line.sizeLabel ?? null,
      quantity: line.quantity,
      modifierLabels: line.modifierLabels ?? [],
      baristaNote: normalizeText(line.baristaNote),
    })),
  }

  return { customer, barista }
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

function buildReceiptDocument(receipt, bodyHtml) {
  return `<!DOCTYPE html>
<html lang="fa" dir="rtl">
<head>
  <meta charset="utf-8" />
  <title>${escapeHtml(receipt.title)}</title>
  <style>
    @page { size: 80mm auto; margin: 4mm; }
    html, body { margin: 0; padding: 0; }
    body {
      font-family: Tahoma, Arial, sans-serif;
      font-size: 12px;
      line-height: 1.45;
      color: #111;
      width: 72mm;
    }
    .receipt { padding: 2mm 0; }
    .center { text-align: center; }
    .brand { font-size: 16px; font-weight: 700; margin-bottom: 4px; }
    .muted { color: #444; font-size: 11px; }
    .divider { border-top: 1px dashed #999; margin: 8px 0; }
    .row { display: flex; justify-content: space-between; gap: 8px; }
    .line-title { font-weight: 700; margin-top: 6px; }
    .line-meta { color: #333; font-size: 11px; margin-top: 2px; }
    .barista-note {
      margin-top: 4px;
      padding: 4px 6px;
      border: 1px solid #111;
      font-weight: 700;
      font-size: 13px;
    }
    .ticket-code {
      font-size: 22px;
      font-weight: 800;
      letter-spacing: 1px;
      margin: 6px 0;
    }
    .total {
      font-size: 14px;
      font-weight: 700;
      margin-top: 4px;
    }
  </style>
</head>
<body>
  <div class="receipt">${bodyHtml}</div>
</body>
</html>`
}

export function buildCustomerReceiptHtml(receipt) {
  const body = `
    <div class="center brand">${escapeHtml(receipt.cafeName)}</div>
    ${receipt.address ? `<div class="center muted">${escapeHtml(receipt.address)}</div>` : ''}
    ${receipt.contactPhone ? `<div class="center muted">${escapeHtml(receipt.contactPhone)}</div>` : ''}
    <div class="divider"></div>
    <div class="center">${escapeHtml(receipt.title)}</div>
    <div class="row"><span>شماره</span><span>#${escapeHtml(receipt.trackingCode)}</span></div>
    <div class="row"><span>زمان</span><span>${escapeHtml(receipt.printedAt)}</span></div>
    <div class="row"><span>نوع</span><span>${escapeHtml(receipt.service)}${receipt.table ? ` · میز ${escapeHtml(receipt.table)}` : ''}</span></div>
    <div class="row"><span>مشتری</span><span>${escapeHtml(receipt.guestName)}</span></div>
    ${receipt.guestPhone ? `<div class="row"><span>تلفن</span><span>${escapeHtml(receipt.guestPhone)}</span></div>` : ''}
    ${receipt.note ? `<div class="line-meta">یادداشت: ${escapeHtml(receipt.note)}</div>` : ''}
    <div class="divider"></div>
    ${receipt.lines
      .map(
        (line) => `
          <div class="line-title">${escapeHtml(String(line.quantity))} × ${escapeHtml(line.name)}</div>
          ${line.descriptor && line.descriptor !== line.name ? `<div class="line-meta">${escapeHtml(line.descriptor)}</div>` : ''}
          <div class="row"><span>${escapeHtml(formatPrice(line.unitPrice))}</span><span>${escapeHtml(formatPrice(line.lineTotal))}</span></div>
        `,
      )
      .join('')}
    <div class="divider"></div>
    <div class="row total"><span>جمع کل</span><span>${escapeHtml(formatPrice(receipt.totalAmount))} تومان</span></div>
    <div class="row"><span>پرداخت</span><span>${escapeHtml(receipt.paymentMethod)}</span></div>
    <div class="center muted" style="margin-top:10px;">با تشکر از حضور شما</div>
  `

  return buildReceiptDocument(receipt, body)
}

export function buildBaristaReceiptHtml(receipt) {
  const body = `
    <div class="center brand">${escapeHtml(receipt.cafeName)}</div>
    <div class="center">*** باریستا ***</div>
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
          <div class="line-title">${escapeHtml(String(line.quantity))} × ${escapeHtml(line.name)}</div>
          ${line.sizeLabel ? `<div class="line-meta">اندازه: ${escapeHtml(line.sizeLabel)}</div>` : ''}
          ${line.modifierLabels.length ? `<div class="line-meta">تغییرات: ${escapeHtml(line.modifierLabels.join('، '))}</div>` : ''}
          ${line.baristaNote ? `<div class="barista-note">${escapeHtml(line.baristaNote)}</div>` : ''}
        `,
      )
      .join('')}
  `

  return buildReceiptDocument(receipt, body)
}

const PRINT_AGENT_URL =
  (typeof import.meta !== 'undefined' && import.meta.env?.VITE_POS_PRINT_AGENT_URL) ||
  'http://127.0.0.1:9101'

function wait(ms) {
  return new Promise((resolve) => {
    window.setTimeout(resolve, ms)
  })
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
    const cleanup = () => {
      iframe.remove()
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

      window.setTimeout(() => {
        frameWindow.removeEventListener('afterprint', handleAfterPrint)
        cleanup()
      }, 1500)
    }

    if (frameWindow.document.readyState === 'complete') {
      triggerPrint()
    } else {
      iframe.onload = triggerPrint
    }
  })
}

async function tryPrintAgent(bundle) {
  const controller = new AbortController()
  const timeout = window.setTimeout(() => controller.abort(), 8000)

  try {
    const response = await fetch(`${PRINT_AGENT_URL}/print`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({
        customer: bundle.customer,
        barista: bundle.barista,
      }),
      signal: controller.signal,
    })

    if (!response.ok) {
      throw new Error(`Print agent responded with ${response.status}`)
    }

    return true
  } catch {
    return false
  } finally {
    window.clearTimeout(timeout)
  }
}

export async function printPosReceipts(input) {
  const bundle = buildPosReceiptBundle(input)

  const agentPrinted = await tryPrintAgent(bundle)
  if (agentPrinted) {
    return { method: 'agent', bundle }
  }

  await printHtmlDocument(buildCustomerReceiptHtml(bundle.customer))
  await wait(350)
  await printHtmlDocument(buildBaristaReceiptHtml(bundle.barista))

  return { method: 'browser', bundle }
}
