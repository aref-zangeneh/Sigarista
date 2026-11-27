/**
 * Helper utilities for the Supplier Restock Order List (OrderList):
 * - WhatsApp phone normalization (Iran + international)
 * - Clean, human-friendly Persian WhatsApp message builder
 * - Printable / Save-as-PDF formal Persian purchase-order sheet generator
 */

export const ORDER_LIST_UNITS = [
  'عدد',
  'سینی',
  'جعبه',
  'قالب',
  'بسته',
  'کیلوگرم',
  'کارتن',
  'لیتر',
]

const PERSIAN_DIGITS = '۰۱۲۳۴۵۶۷۸۹'
const ARABIC_DIGITS = '٠١٢٣٤٥٦٧٨٩'

export function toLatinDigits(value) {
  return String(value ?? '')
    .replace(/[۰-۹]/g, (ch) => String(PERSIAN_DIGITS.indexOf(ch)))
    .replace(/[٠-٩]/g, (ch) => String(ARABIC_DIGITS.indexOf(ch)))
}

export function toPersianDigits(value) {
  return String(value ?? '').replace(/\d/g, (d) => PERSIAN_DIGITS[Number(d)] ?? d)
}

export function formatQuantity(qty) {
  const num = Number(qty)
  if (!Number.isFinite(num)) return toPersianDigits(qty ?? '0')
  const formatted = Number.isInteger(num) ? String(num) : String(Number(num.toFixed(2)))
  return toPersianDigits(formatted)
}

/**
 * Normalizes an Iranian or international phone number into WhatsApp's `wa.me/<digits>` format.
 * Examples:
 *  - "0912 345 6789"   -> "989123456789"
 *  - "+98 912-3456789" -> "989123456789"
 *  - "۰۹۱۲۳۴۵۶۷۸۹"     -> "989123456789"
 */
export function normalizeWhatsAppPhone(rawPhone) {
  const latin = toLatinDigits(rawPhone).trim()
  if (!latin) return ''

  let digits = latin.replace(/\D/g, '')
  if (!digits) return ''

  if (digits.startsWith('00')) {
    digits = digits.slice(2)
  }

  if (digits.startsWith('09') && digits.length === 11) {
    return `98${digits.slice(1)}`
  }

  if (digits.startsWith('9') && digits.length === 10) {
    return `98${digits}`
  }

  if (digits.startsWith('0') && digits.length >= 10) {
    return `98${digits.slice(1)}`
  }

  return digits
}

export function formatShamsiToday() {
  try {
    return new Intl.DateTimeFormat('fa-IR', {
      weekday: 'long',
      year: 'numeric',
      month: 'long',
      day: 'numeric',
    }).format(new Date())
  } catch {
    return new Date().toLocaleDateString('fa-IR')
  }
}

export function buildWhatsAppOrderMessage({
  items = [],
  supplierName = '',
  cafeName = 'کافه شوگاریستا',
  deliveryNote = '',
  generalNote = '',
}) {
  const lines = []
  lines.push('سلام و وقت بخیر 🌿')
  lines.push(`سفارش جدید از *${cafeName.trim() || 'کافه شوگاریستا'}*`)
  lines.push(`📅 ${formatShamsiToday()}`)

  if (supplierName.trim()) {
    lines.push(`🏪 تأمین‌کننده: ${supplierName.trim()}`)
  }

  lines.push('')
  lines.push('📋 *لیست اقلام درخواستی:*')

  items.forEach((item, index) => {
    const num = toPersianDigits(index + 1)
    const qty = formatQuantity(item.quantity)
    const unit = item.unit || 'عدد'
    lines.push(`${num}. *${item.itemName}* — ${qty} ${unit}`)
    if (item.notes && item.notes.trim()) {
      lines.push(`   ↳ ${item.notes.trim()}`)
    }
  })

  lines.push('')
  const totalQty = items.reduce((sum, item) => sum + (Number(item.quantity) || 0), 0)
  lines.push(`📊 جمع: ${toPersianDigits(items.length)} قلم (مجموع ${formatQuantity(totalQty)} واحد)`)

  if (deliveryNote.trim()) {
    lines.push(`🕒 زمان تحویل پیشنهادی: ${deliveryNote.trim()}`)
  }

  if (generalNote.trim()) {
    lines.push(`📝 توضیحات: ${generalNote.trim()}`)
  }

  lines.push('')
  lines.push('سپاس از همکاری شما 🙏')

  return lines.join('\n')
}

export function buildWhatsAppUrl(phone, message) {
  const normalized = normalizeWhatsAppPhone(phone)
  const encoded = encodeURIComponent(message || '')
  return normalized ? `https://wa.me/${normalized}?text=${encoded}` : `https://wa.me/?text=${encoded}`
}

function escapeHtml(value) {
  return String(value ?? '')
    .replace(/&/g, '&amp;')
    .replace(/</g, '&lt;')
    .replace(/>/g, '&gt;')
    .replace(/"/g, '&quot;')
    .replace(/'/g, '&#39;')
}

/**
 * Opens a clean, print-ready Persian A4 Purchase Order sheet so the user can
 * save directly as PDF or print immediately.
 */
export function openOrderListPdfWindow({
  items = [],
  supplierName = '',
  supplierPhone = '',
  cafeName = 'کافه شوگاریستا',
  deliveryNote = '',
  generalNote = '',
  batchCode = '',
}) {
  if (!items.length) return false

  const win = window.open('', '_blank', 'width=900,height=980')
  if (!win) return false

  const today = formatShamsiToday()
  const timeStr = new Intl.DateTimeFormat('fa-IR', {
    hour: '2-digit',
    minute: '2-digit',
  }).format(new Date())

  const totalQty = items.reduce((sum, item) => sum + (Number(item.quantity) || 0), 0)

  const rowsHtml = items
    .map((item, idx) => {
      return `
        <tr>
          <td class="col-idx">${escapeHtml(toPersianDigits(idx + 1))}</td>
          <td class="col-name">
            <div class="item-title">${escapeHtml(item.itemName)}</div>
            ${item.categoryName ? `<div class="item-cat">${escapeHtml(item.categoryName)}</div>` : ''}
          </td>
          <td class="col-qty"><strong>${escapeHtml(formatQuantity(item.quantity))}</strong> ${escapeHtml(item.unit || 'عدد')}</td>
          <td class="col-supplier">${escapeHtml(item.supplierName || supplierName || '—')}</td>
          <td class="col-note">${escapeHtml(item.notes || '—')}</td>
        </tr>`
    })
    .join('')

  const html = `<!doctype html>
<html lang="fa" dir="rtl">
<head>
  <meta charset="utf-8" />
  <title>لیست سفارش تأمین‌کننده — ${escapeHtml(cafeName)}</title>
  <style>
    @page { size: A4; margin: 16mm; }
    * { box-sizing: border-box; }
    body {
      margin: 0;
      padding: 24px;
      font-family: 'Vazirmatn', 'IRANSans', Tahoma, Arial, sans-serif;
      color: #111827;
      background: #ffffff;
      direction: rtl;
      font-size: 13px;
      line-height: 1.65;
    }
    .sheet {
      max-width: 820px;
      margin: 0 auto;
    }
    .header {
      display: flex;
      justify-content: space-between;
      align-items: flex-start;
      gap: 16px;
      padding-bottom: 16px;
      border-bottom: 2px solid #aa3171;
      margin-bottom: 18px;
    }
    .brand h1 {
      margin: 0;
      font-size: 20px;
      font-weight: 800;
      color: #aa3171;
    }
    .brand p {
      margin: 4px 0 0;
      font-size: 12.5px;
      color: #4b5563;
    }
    .meta {
      text-align: left;
      font-size: 12px;
      color: #374151;
    }
    .meta div { margin-bottom: 3px; }
    .meta strong { color: #111827; }
    .info-bar {
      display: grid;
      grid-template-columns: repeat(3, minmax(0, 1fr));
      gap: 10px;
      margin-bottom: 18px;
      padding: 12px 14px;
      border-radius: 10px;
      background: #f9fafb;
      border: 1px solid #e5e7eb;
    }
    .info-cell span {
      display: block;
      font-size: 11px;
      color: #6b7280;
    }
    .info-cell strong {
      font-size: 13px;
      color: #111827;
    }
    table {
      width: 100%;
      border-collapse: collapse;
      margin-bottom: 18px;
    }
    th {
      background: #f3f4f6;
      color: #374151;
      font-size: 12px;
      font-weight: 700;
      text-align: right;
      padding: 10px 10px;
      border: 1px solid #d1d5db;
    }
    td {
      padding: 9px 10px;
      border: 1px solid #e5e7eb;
      vertical-align: middle;
    }
    tr:nth-child(even) td {
      background: #fcfcfd;
    }
    .col-idx { width: 46px; text-align: center; font-weight: 700; color: #6b7280; }
    .col-qty { width: 115px; white-space: nowrap; }
    .col-supplier { width: 150px; }
    .item-title { font-weight: 700; color: #111827; }
    .item-cat { font-size: 11px; color: #6b7280; }
    .summary-box {
      display: flex;
      justify-content: space-between;
      align-items: center;
      flex-wrap: wrap;
      gap: 12px;
      padding: 12px 14px;
      border-radius: 10px;
      background: #fdf2f8;
      border: 1px solid #fbcfe8;
      margin-bottom: 18px;
      font-size: 13px;
    }
    .note-box {
      padding: 10px 14px;
      border-radius: 8px;
      border: 1px dashed #d1d5db;
      background: #ffffff;
      margin-bottom: 22px;
      font-size: 12.5px;
    }
    .signatures {
      display: grid;
      grid-template-columns: repeat(2, minmax(0, 1fr));
      gap: 24px;
      margin-top: 32px;
      padding-top: 14px;
      border-top: 1px solid #e5e7eb;
      font-size: 12px;
      color: #4b5563;
    }
    .sig-box {
      min-height: 64px;
    }
    .print-actions {
      display: flex;
      gap: 8px;
      justify-content: flex-end;
      margin-bottom: 16px;
    }
    .print-btn {
      padding: 8px 16px;
      border-radius: 8px;
      border: 1px solid #aa3171;
      background: #aa3171;
      color: #fff;
      font-family: inherit;
      font-size: 13px;
      font-weight: 700;
      cursor: pointer;
    }
    .close-btn {
      padding: 8px 14px;
      border-radius: 8px;
      border: 1px solid #d1d5db;
      background: #fff;
      color: #374151;
      font-family: inherit;
      font-size: 13px;
      cursor: pointer;
    }
    @media print {
      body { padding: 0; }
      .print-actions { display: none !important; }
    }
  </style>
</head>
<body>
  <div class="sheet">
    <div class="print-actions">
      <button class="print-btn" onclick="window.print()">چاپ / ذخیره به صورت PDF</button>
      <button class="close-btn" onclick="window.close()">بستن</button>
    </div>

    <div class="header">
      <div class="brand">
        <h1>${escapeHtml(cafeName || 'کافه شوگاریستا')}</h1>
        <p>برگه لیست سفارش به تأمین‌کننده (Order List)</p>
      </div>
      <div class="meta">
        <div>تاریخ: <strong>${escapeHtml(today)}</strong></div>
        <div>ساعت صدور: <strong>${escapeHtml(timeStr)}</strong></div>
        ${batchCode ? `<div>کد سفارش: <strong dir="ltr">${escapeHtml(batchCode)}</strong></div>` : ''}
      </div>
    </div>

    <div class="info-bar">
      <div class="info-cell">
        <span>تأمین‌کننده</span>
        <strong>${escapeHtml(supplierName || 'همه تأمین‌کنندگان / انتخابی')}</strong>
      </div>
      <div class="info-cell">
        <span>شماره تماس / واتساپ</span>
        <strong dir="ltr">${escapeHtml(supplierPhone || '—')}</strong>
      </div>
      <div class="info-cell">
        <span>زمان تحویل پیشنهادی</span>
        <strong>${escapeHtml(deliveryNote || 'هماهنگی هنگام ارسال')}</strong>
      </div>
    </div>

    <table>
      <thead>
        <tr>
          <th class="col-idx">ردیف</th>
          <th>نام محصول / کالا</th>
          <th class="col-qty">تعداد درخواستی</th>
          <th class="col-supplier">تأمین‌کننده</th>
          <th>توضیحات</th>
        </tr>
      </thead>
      <tbody>
        ${rowsHtml}
      </tbody>
    </table>

    <div class="summary-box">
      <div>تعداد ردیف اقلام: <strong>${escapeHtml(toPersianDigits(items.length))} قلم</strong></div>
      <div>جمع کل مقادیر: <strong>${escapeHtml(formatQuantity(totalQty))} واحد</strong></div>
    </div>

    ${generalNote ? `<div class="note-box"><strong>یادداشت کلی سفارش:</strong> ${escapeHtml(generalNote)}</div>` : ''}

    <div class="signatures">
      <div class="sig-box">تنظیم‌کننده / مدیر خرید کافه:</div>
      <div class="sig-box">تایید دریافت توسط تأمین‌کننده:</div>
    </div>
  </div>
  <script>
    window.addEventListener('load', () => {
      setTimeout(() => { window.print(); }, 250);
    });
  </script>
</body>
</html>`

  win.document.open()
  win.document.write(html)
  win.document.close()
  return true
}
