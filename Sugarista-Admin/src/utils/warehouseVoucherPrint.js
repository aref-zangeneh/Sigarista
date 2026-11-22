export const MAIN_WAREHOUSE_KEY = '__MAIN__'

export const RECEIPT_REASONS = [
  { value: 'Purchase', label: 'خرید از تأمین‌کننده' },
  { value: 'ProductionReturn', label: 'برگشت از تولید / آشپزخانه' },
  { value: 'OpeningBalance', label: 'موجودی اول دوره' },
  { value: 'InventorySurplus', label: 'اضافه انبارگردانی' },
  { value: 'SampleOrGift', label: 'نمونه / هدیه / امانی' },
  { value: 'OtherReceipt', label: 'سایر ورودها' },
]

export const ISSUE_REASONS = [
  { value: 'KitchenConsumption', label: 'مصرف روزانه بار و آشپزخانه' },
  { value: 'ProductionIssue', label: 'ارسال به خط تولید / قنادی' },
  { value: 'WastageOrSpoilage', label: 'ضایعات / فساد / انقضا' },
  { value: 'InternalConsumption', label: 'مصرف پرسنلی / داخلی' },
  { value: 'ReturnToSupplier', label: 'برگشت به تأمین‌کننده' },
  { value: 'InventoryShortage', label: 'کسری انبارگردانی' },
  { value: 'SampleOrPromo', label: 'نمونه / تست کیفیت' },
  { value: 'OtherIssue', label: 'سایر خروج‌ها' },
]

export function getReasonsForVoucherType(voucherType) {
  return voucherType === 'Issue' ? ISSUE_REASONS : RECEIPT_REASONS
}

const PERSIAN_DIGITS = ['۰', '۱', '۲', '۳', '۴', '۵', '۶', '۷', '۸', '۹']

export function toFaDigits(value) {
  return String(value ?? '')
    .replace(/[0-9]/g, (d) => PERSIAN_DIGITS[Number(d)])
    .replace(/[\u0660-\u0669]/g, (d) => PERSIAN_DIGITS[d.charCodeAt(0) - 0x0660])
}

export function parsePositiveDecimal(raw) {
  if (raw === null || raw === undefined || raw === '') return 0
  const ascii = String(raw)
    .replace(/[۰-۹]/g, (d) => String('۰۱۲۳۴۵۶۷۸۹'.indexOf(d)))
    .replace(/[٠-٩]/g, (d) => String('٠١٢٣٤٥٦٧٨٩'.indexOf(d)))
    .replace(/[,٬\s]/g, '')
  const parsed = Number(ascii)
  return Number.isFinite(parsed) && parsed > 0 ? parsed : 0
}

export function parseNonNegativeDecimal(raw) {
  if (raw === null || raw === undefined || raw === '') return null
  const ascii = String(raw)
    .replace(/[۰-۹]/g, (d) => String('۰۱۲۳۴۵۶۷۸۹'.indexOf(d)))
    .replace(/[٠-٩]/g, (d) => String('٠١٢٣٤٥٦٧٨٩'.indexOf(d)))
    .replace(/[,٬\s]/g, '')
  const parsed = Number(ascii)
  return Number.isFinite(parsed) && parsed >= 0 ? parsed : null
}

export function formatVoucherMoney(amount) {
  const num = Math.round(Number(amount || 0))
  if (!Number.isFinite(num)) return '۰'
  const formatted = Math.abs(num)
    .toString()
    .replace(/\B(?=(\d{3})+(?!\d))/g, '٬')
  return (num < 0 ? '−' : '') + toFaDigits(formatted)
}

export function formatVoucherQty(qty) {
  const num = Number(qty ?? 0)
  if (!Number.isFinite(num)) return '۰'
  const rounded = Math.round(num * 1000) / 1000
  return toFaDigits(rounded.toLocaleString('en-US', { maximumFractionDigits: 3 })).replace(/,/g, '٬')
}

export function computeVoucherLinePreview(voucherType, currentStock, rawQty, rawUnitPrice) {
  const stock = Number(currentStock ?? 0) || 0
  const qty = parsePositiveDecimal(rawQty)
  const unitPrice = parseNonNegativeDecimal(rawUnitPrice)
  const lineTotal = qty > 0 && unitPrice !== null ? Math.round(qty * unitPrice * 100) / 100 : 0
  const stockAfter = voucherType === 'Issue' ? stock - qty : stock + qty
  const insufficient = voucherType === 'Issue' && qty > 0 && qty > stock
  return { qty, unitPrice, lineTotal, stockAfter, insufficient }
}

function escapeHtml(value) {
  return String(value ?? '')
    .replace(/&/g, '&amp;')
    .replace(/</g, '&lt;')
    .replace(/>/g, '&gt;')
    .replace(/"/g, '&quot;')
}

function formatShamsiDateTime(isoString) {
  if (!isoString) return '—'
  const date = new Date(isoString)
  if (Number.isNaN(date.getTime())) return toFaDigits(isoString)
  try {
    return toFaDigits(
      new Intl.DateTimeFormat('fa-IR-u-ca-persian', {
        year: 'numeric',
        month: '2-digit',
        day: '2-digit',
        hour: '2-digit',
        minute: '2-digit',
        hour12: false,
        timeZone: 'Asia/Tehran',
      }).format(date),
    )
  } catch {
    return toFaDigits(date.toLocaleString('fa-IR'))
  }
}

function formatShamsiDateOnly(dateOnlyStr) {
  if (!dateOnlyStr) return '—'
  const date = new Date(`${dateOnlyStr}T12:00:00Z`)
  if (Number.isNaN(date.getTime())) return toFaDigits(dateOnlyStr)
  try {
    return toFaDigits(
      new Intl.DateTimeFormat('fa-IR-u-ca-persian', {
        year: 'numeric',
        month: '2-digit',
        day: '2-digit',
        timeZone: 'Asia/Tehran',
      }).format(date),
    )
  } catch {
    return toFaDigits(dateOnlyStr)
  }
}

export function buildWarehouseVoucherHtml(voucher, siteSettings = {}) {
  const isReceipt = voucher.voucherType === 'Receipt'
  const isCancelled = voucher.status === 'Cancelled'
  const cafeName = siteSettings?.cafeName || 'شوگاریستا'
  const title = isReceipt ? 'رسید ورود کالا به انبار' : 'سند / حواله خروج کالا از انبار'
  const counterpartyTitle = isReceipt ? 'تحویل‌دهنده' : 'تحویل‌گیرنده / واحد مقصد'
  const accent = isReceipt ? '#047857' : '#b45309'
  const lines = Array.isArray(voucher.lines) ? voucher.lines : []

  const rowsHtml = lines
    .map(
      (line, idx) => `
      <tr>
        <td class="c">${escapeHtml(toFaDigits(idx + 1))}</td>
        <td>
          <strong>${escapeHtml(line.ingredientName)}</strong>
          ${line.ingredientEnglishName ? `<div class="sub ltr">${escapeHtml(line.ingredientEnglishName)}</div>` : ''}
          ${line.lineNote ? `<div class="sub">توضیح: ${escapeHtml(line.lineNote)}</div>` : ''}
        </td>
        <td class="c">${escapeHtml(line.unit || '—')}</td>
        <td class="c num"><strong>${isReceipt ? '+' : '−'}${escapeHtml(formatVoucherQty(line.quantity))}</strong></td>
        <td class="c num">${escapeHtml(formatVoucherQty(line.stockBefore))}</td>
        <td class="c num"><strong>${escapeHtml(formatVoucherQty(line.stockAfter))}</strong></td>
        <td class="c num">${line.unitPrice != null ? escapeHtml(formatVoucherMoney(line.unitPrice)) : '—'}</td>
        <td class="c num"><strong>${line.lineTotal > 0 ? escapeHtml(formatVoucherMoney(line.lineTotal)) : '—'}</strong></td>
        <td class="c">
          ${line.batchNumber ? `<div>بچ: ${escapeHtml(line.batchNumber)}</div>` : ''}
          ${line.expirationDate ? `<div>انقضا: ${escapeHtml(formatShamsiDateOnly(line.expirationDate))}</div>` : ''}
          ${!line.batchNumber && !line.expirationDate ? '—' : ''}
        </td>
      </tr>`,
    )
    .join('')

  return `<!DOCTYPE html>
<html lang="fa" dir="rtl">
<head>
  <meta charset="utf-8" />
  <title>${escapeHtml(title)} — ${escapeHtml(voucher.voucherNumber)}</title>
  <style>
    @page { size: A4 portrait; margin: 14mm; }
    * { box-sizing: border-box; }
    body {
      margin: 0;
      padding: 0;
      font-family: Vazirmatn, Tahoma, Arial, sans-serif;
      color: #111827;
      background: #fff;
      font-size: 12px;
      line-height: 1.6;
    }
    .sheet {
      position: relative;
      border: 1.5px solid #1f2937;
      border-radius: 10px;
      padding: 18px 20px;
    }
    .watermark {
      position: absolute;
      top: 42%;
      left: 50%;
      transform: translate(-50%, -50%) rotate(-24deg);
      font-size: 54px;
      font-weight: 900;
      color: rgba(220, 38, 38, 0.16);
      border: 5px solid rgba(220, 38, 38, 0.2);
      padding: 6px 26px;
      border-radius: 14px;
      pointer-events: none;
    }
    .header {
      display: flex;
      justify-content: space-between;
      align-items: flex-start;
      gap: 16px;
      border-bottom: 2px solid ${accent};
      padding-bottom: 12px;
      margin-bottom: 14px;
    }
    .brand { font-size: 18px; font-weight: 800; color: #111827; }
    .doc-title { font-size: 16px; font-weight: 800; color: ${accent}; margin-top: 2px; }
    .meta-box {
      border: 1px solid #d1d5db;
      border-radius: 8px;
      padding: 8px 12px;
      min-width: 210px;
      background: #f9fafb;
    }
    .meta-row { display: flex; justify-content: space-between; gap: 10px; font-size: 11.5px; }
    .meta-row strong { font-weight: 800; }
    .ltr { direction: ltr; unicode-bidi: embed; }
    .info-grid {
      display: grid;
      grid-template-columns: repeat(3, minmax(0, 1fr));
      gap: 8px 14px;
      padding: 10px 12px;
      border: 1px solid #e5e7eb;
      border-radius: 8px;
      background: #fcfcfd;
      margin-bottom: 14px;
      font-size: 11.5px;
    }
    .info-cell span { display: block; color: #6b7280; font-size: 10.5px; }
    .info-cell strong { color: #111827; font-weight: 700; }
    table {
      width: 100%;
      border-collapse: collapse;
      margin-bottom: 14px;
    }
    th, td {
      border: 1px solid #d1d5db;
      padding: 6px 7px;
      vertical-align: middle;
    }
    th {
      background: #f3f4f6;
      font-weight: 800;
      font-size: 11px;
      color: #1f2937;
      text-align: center;
    }
    td.c { text-align: center; }
    td.num { font-variant-numeric: tabular-nums; white-space: nowrap; }
    .sub { font-size: 10px; color: #4b5563; margin-top: 2px; }
    .totals-bar {
      display: flex;
      justify-content: space-between;
      align-items: center;
      padding: 10px 14px;
      border-radius: 8px;
      background: #f9fafb;
      border: 1px solid #d1d5db;
      margin-bottom: 14px;
      font-size: 13px;
    }
    .totals-bar strong { font-size: 14px; color: ${accent}; }
    .note-box {
      border: 1px dashed #9ca3af;
      border-radius: 8px;
      padding: 8px 12px;
      margin-bottom: 16px;
      font-size: 11.5px;
    }
    .cancel-box {
      border: 1.5px solid #dc2626;
      background: #fef2f2;
      color: #991b1b;
      border-radius: 8px;
      padding: 8px 12px;
      margin-bottom: 14px;
      font-size: 11.5px;
    }
    .signatures {
      display: grid;
      grid-template-columns: repeat(3, minmax(0, 1fr));
      gap: 14px;
      margin-top: 24px;
    }
    .sig-box {
      border: 1px solid #d1d5db;
      border-radius: 8px;
      padding: 10px 12px 46px;
      text-align: center;
      font-size: 11px;
      color: #374151;
    }
    .sig-box strong { display: block; margin-bottom: 4px; color: #111827; }
  </style>
</head>
<body>
  <div class="sheet">
    ${isCancelled ? '<div class="watermark">ابطال‌شده</div>' : ''}
    <div class="header">
      <div>
        <div class="brand">${escapeHtml(cafeName)}</div>
        <div class="doc-title">${escapeHtml(title)}</div>
        ${siteSettings?.address ? `<div class="sub">${escapeHtml(siteSettings.address)}</div>` : ''}
      </div>
      <div class="meta-box">
        <div class="meta-row"><span>شماره سند:</span><strong class="ltr">${escapeHtml(voucher.voucherNumber)}</strong></div>
        <div class="meta-row"><span>تاریخ سند:</span><strong>${escapeHtml(formatShamsiDateTime(voucher.voucherDate))}</strong></div>
        <div class="meta-row"><span>وضعیت:</span><strong>${isCancelled ? 'ابطال‌شده' : 'ثبت قطعی'}</strong></div>
      </div>
    </div>

    <div class="info-grid">
      <div class="info-cell"><span>انبار</span><strong>${escapeHtml(voucher.warehouseName || 'انبار اصلی')}</strong></div>
      <div class="info-cell"><span>بابت / دلیل سند</span><strong>${escapeHtml(voucher.reasonLabel || voucher.reason)}</strong></div>
      <div class="info-cell"><span>تأمین‌کننده</span><strong>${escapeHtml(voucher.supplierName || '—')}</strong></div>
      <div class="info-cell"><span>${escapeHtml(counterpartyTitle)}</span><strong>${escapeHtml(voucher.counterpartyName || '—')}</strong></div>
      <div class="info-cell"><span>شماره فاکتور / مرجع</span><strong class="ltr">${escapeHtml(voucher.externalReference || '—')}</strong></div>
      <div class="info-cell"><span>ثبت‌کننده سند</span><strong>${escapeHtml(voucher.createdByName || '—')}</strong></div>
    </div>

    ${
      isCancelled
        ? `<div class="cancel-box">
            <strong>این سند ابطال شده و موجودی اقلام آن به‌صورت خودکار برگشت خورده است.</strong><br/>
            علت ابطال: ${escapeHtml(voucher.cancellationReason || '—')}
            ${voucher.cancelledByName ? ` · ابطال توسط: ${escapeHtml(voucher.cancelledByName)}` : ''}
            ${voucher.cancelledAt ? ` · زمان ابطال: ${escapeHtml(formatShamsiDateTime(voucher.cancelledAt))}` : ''}
          </div>`
        : ''
    }

    <table>
      <thead>
        <tr>
          <th style="width:36px;">ردیف</th>
          <th>شرح قلم / ماده اولیه</th>
          <th style="width:58px;">واحد</th>
          <th style="width:82px;">مقدار</th>
          <th style="width:76px;">موجودی قبل</th>
          <th style="width:76px;">موجودی بعد</th>
          <th style="width:92px;">قیمت واحد (ت)</th>
          <th style="width:98px;">مبلغ کل (ت)</th>
          <th style="width:105px;">بچ / انقضا</th>
        </tr>
      </thead>
      <tbody>
        ${rowsHtml}
      </tbody>
    </table>

    <div class="totals-bar">
      <span>تعداد اقلام سند: <strong>${escapeHtml(toFaDigits(lines.length))} قلم</strong></span>
      <span>جمع کل ریالی سند: <strong>${escapeHtml(formatVoucherMoney(voucher.totalAmount))} تومان</strong></span>
    </div>

    ${voucher.note ? `<div class="note-box"><strong>توضیحات سند:</strong> ${escapeHtml(voucher.note)}</div>` : ''}

    <div class="signatures">
      <div class="sig-box">
        <strong>تنظیم‌کننده و ثبت‌کننده</strong>
        ${escapeHtml(voucher.createdByName || '')}
      </div>
      <div class="sig-box">
        <strong>مسئول انبار / انباردار</strong>
        نام و امضا
      </div>
      <div class="sig-box">
        <strong>${escapeHtml(counterpartyTitle)}</strong>
        ${escapeHtml(voucher.counterpartyName || voucher.supplierName || 'نام و امضا')}
      </div>
    </div>
  </div>
</body>
</html>`
}

export function printWarehouseVoucher(voucher, siteSettings = {}) {
  const html = buildWarehouseVoucherHtml(voucher, siteSettings)
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
      window.setTimeout(() => iframe.remove(), 500)
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
      }, 60000)
    }

    if (frameWindow.document.readyState === 'complete') {
      triggerPrint()
    } else {
      iframe.onload = triggerPrint
    }
  })
}
