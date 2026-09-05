import sharp from 'sharp'

const WIDTH = 576
const PADDING = 28
const RIGHT = WIDTH - PADDING
const CENTER = WIDTH / 2
const FONT = 'Tahoma, Arial, Helvetica, sans-serif'

function escapeXml(value) {
  return (value ?? '')
    .toString()
    .replace(/&/g, '&amp;')
    .replace(/</g, '&lt;')
    .replace(/>/g, '&gt;')
    .replace(/"/g, '&quot;')
}

function wrapText(text, maxChars = 34) {
  const words = escapeXml(text).split(/\s+/)
  const lines = []
  let current = ''

  for (const word of words) {
    const candidate = current ? `${current} ${word}` : word
    if (candidate.length > maxChars && current) {
      lines.push(current)
      current = word
    } else {
      current = candidate
    }
  }

  if (current) {
    lines.push(current)
  }

  return lines.length ? lines : ['']
}

function textLine(y, text, { size = 22, bold = false, center = false, x = RIGHT } = {}) {
  const weight = bold ? 'font-weight="700"' : ''
  const anchor = center ? 'middle' : 'end'
  const posX = center ? CENTER : x
  return `<text x="${posX}" y="${y}" text-anchor="${anchor}" font-family="${FONT}" font-size="${size}" ${weight} fill="#111">${escapeXml(text)}</text>`
}

function multilineBlock(startY, lines, options = {}) {
  const lineHeight = options.lineHeight ?? (options.size || 22) + 8
  return lines
    .map((line, index) => textLine(startY + index * lineHeight, line, options))
    .join('\n')
}

function divider(y) {
  return `<line x1="${PADDING}" y1="${y}" x2="${RIGHT}" y2="${y}" stroke="#999" stroke-width="1" stroke-dasharray="4 4" />`
}

function formatMoney(value) {
  return `${Number(value || 0).toLocaleString('fa-IR')} تومان`
}

function estimateCustomerHeight(receipt) {
  let height = 420
  height += receipt.lines.length * 72
  if (receipt.note) height += 36
  if (receipt.address) height += 28
  if (receipt.contactPhone) height += 28
  return Math.max(height, 640)
}

function estimateBaristaHeight(receipt) {
  let height = 360
  height += receipt.lines.length * 96
  if (receipt.note) height += 48
  return Math.max(height, 560)
}

export function buildCustomerReceiptSvg(receipt) {
  const height = estimateCustomerHeight(receipt)
  let y = 42
  const parts = [
    `<svg xmlns="http://www.w3.org/2000/svg" width="${WIDTH}" height="${height}">`,
    `<rect width="100%" height="100%" fill="#fff" />`,
    textLine(y, receipt.cafeName, { size: 30, bold: true, center: true }),
  ]

  y += 34
  if (receipt.address) {
    y += 24
    parts.push(textLine(y, receipt.address, { size: 18, center: true }))
  }
  if (receipt.contactPhone) {
    y += 24
    parts.push(textLine(y, receipt.contactPhone, { size: 18, center: true }))
  }

  y += 28
  parts.push(divider(y))
  y += 28
  parts.push(textLine(y, receipt.title, { size: 24, bold: true, center: true }))
  y += 34
  parts.push(textLine(y, `شماره: #${receipt.trackingCode}`, { size: 22 }))
  y += 30
  parts.push(textLine(y, `زمان: ${receipt.printedAt}`, { size: 20 }))
  y += 28
  parts.push(
    textLine(
      y,
      `نوع: ${receipt.service}${receipt.table ? ` · میز ${receipt.table}` : ''}`,
      { size: 20 },
    ),
  )
  y += 28
  parts.push(textLine(y, `مشتری: ${receipt.guestName}`, { size: 20 }))
  if (receipt.guestPhone) {
    y += 28
    parts.push(textLine(y, `تلفن: ${receipt.guestPhone}`, { size: 20 }))
  }
  if (receipt.note) {
    y += 28
    parts.push(...wrapText(`یادداشت: ${receipt.note}`, 30).map((line, index) => textLine(y + index * 26, line, { size: 20 })))
    y += wrapText(`یادداشت: ${receipt.note}`, 30).length * 26 - 28
  }

  y += 24
  parts.push(divider(y))
  y += 32

  receipt.lines.forEach((line) => {
    parts.push(textLine(y, `${line.quantity} × ${line.name}`, { size: 24, bold: true }))
    y += 30
    if (line.descriptor && line.descriptor !== line.name) {
      const metaLines = wrapText(line.descriptor, 30)
      parts.push(multilineBlock(y, metaLines, { size: 18 }))
      y += metaLines.length * 26
    }
    parts.push(textLine(y, `${formatMoney(line.unitPrice)}    ${formatMoney(line.lineTotal)}`, { size: 20 }))
    y += 34
  })

  parts.push(divider(y))
  y += 34
  parts.push(textLine(y, `جمع کل: ${formatMoney(receipt.totalAmount)}`, { size: 26, bold: true }))
  y += 32
  parts.push(textLine(y, `پرداخت: ${receipt.paymentMethod}`, { size: 22 }))
  y += 40
  parts.push(textLine(y, 'با تشکر از حضور شما', { size: 20, center: true }))
  parts.push('</svg>')
  return parts.join('\n')
}

export function buildBaristaReceiptSvg(receipt) {
  const height = estimateBaristaHeight(receipt)
  let y = 42
  const parts = [
    `<svg xmlns="http://www.w3.org/2000/svg" width="${WIDTH}" height="${height}">`,
    `<rect width="100%" height="100%" fill="#fff" />`,
    textLine(y, receipt.cafeName, { size: 28, bold: true, center: true }),
    textLine(y + 32, '*** باریستا ***', { size: 22, bold: true, center: true }),
  ]

  y += 72
  parts.push(divider(y))
  y += 42
  parts.push(textLine(y, `#${receipt.trackingCode}`, { size: 40, bold: true, center: true }))
  y += 42
  parts.push(textLine(y, `زمان: ${receipt.printedAt}`, { size: 20 }))
  y += 28
  parts.push(
    textLine(
      y,
      `${receipt.service}${receipt.table ? ` · میز ${receipt.table}` : ''}`,
      { size: 22, bold: true },
    ),
  )
  y += 28
  parts.push(textLine(y, `مشتری: ${receipt.guestName}`, { size: 20 }))
  if (receipt.note) {
    y += 30
    parts.push(
      `<rect x="${PADDING}" y="${y - 18}" width="${WIDTH - PADDING * 2}" height="42" fill="#fff" stroke="#111" stroke-width="2" />`,
    )
    parts.push(textLine(y + 10, `یادداشت: ${receipt.note}`, { size: 20, bold: true }))
    y += 42
  }

  y += 18
  parts.push(divider(y))
  y += 34

  receipt.lines.forEach((line) => {
    parts.push(textLine(y, `${line.quantity} × ${line.name}`, { size: 28, bold: true }))
    y += 32
    if (line.sizeLabel) {
      parts.push(textLine(y, `اندازه: ${line.sizeLabel}`, { size: 20 }))
      y += 26
    }
    if (line.modifierLabels?.length) {
      const modLines = wrapText(`تغییرات: ${line.modifierLabels.join('، ')}`, 30)
      parts.push(multilineBlock(y, modLines, { size: 20 }))
      y += modLines.length * 26
    }
    if (line.baristaNote) {
      parts.push(
        `<rect x="${PADDING}" y="${y - 4}" width="${WIDTH - PADDING * 2}" height="40" fill="#fff" stroke="#111" stroke-width="2" />`,
      )
      parts.push(textLine(y + 22, line.baristaNote, { size: 22, bold: true }))
      y += 46
    }
    y += 12
  })

  parts.push('</svg>')
  return parts.join('\n')
}

export async function renderReceiptPng(receipt, type = 'customer') {
  const svg = type === 'barista' ? buildBaristaReceiptSvg(receipt) : buildCustomerReceiptSvg(receipt)
  return sharp(Buffer.from(svg)).png().toBuffer()
}
