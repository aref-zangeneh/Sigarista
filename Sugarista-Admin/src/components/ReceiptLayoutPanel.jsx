import { useCallback, useEffect, useMemo, useState } from 'react'
import { buildQrSvg } from '../utils/qrSvg.js'

// ── Key catalogs (must mirror the backend whitelists) ────────────────────────

const CUSTOMER_HEADER_ROWS = [
  { key: 'code', label: 'شماره سفارش' },
  { key: 'createdAt', label: 'تاریخ و زمان ثبت' },
  { key: 'channel', label: 'نوع سفارش (حضوری / آنلاین)' },
  { key: 'service', label: 'شیوهٔ پذیرایی (سالن / بیرون‌بر)' },
  { key: 'table', label: 'شماره میز' },
  { key: 'pickupTime', label: 'زمان آماده‌سازی / تحویل' },
  { key: 'customerName', label: 'مشتری گرامی' },
  { key: 'customerPhone', label: 'شماره همراه' },
  { key: 'note', label: 'توضیحات مهمان' },
]

const CUSTOMER_PAYMENT_ROWS = [
  { key: 'paymentMethod', label: 'شیوهٔ پرداخت' },
  { key: 'reference', label: 'کد رهگیری بانکی' },
  { key: 'amountPaid', label: 'مبلغ تسویه‌شده' },
]

const BARISTA_INFO_ROWS = [
  { key: 'paidAt', label: 'زمان پرداخت' },
  { key: 'createdAt', label: 'زمان ثبت سفارش' },
  { key: 'channel', label: 'نوع سفارش' },
  { key: 'service', label: 'شیوهٔ سرو' },
  { key: 'table', label: 'شماره میز' },
  { key: 'customerName', label: 'نام مشتری' },
  { key: 'customerPhone', label: 'شماره همراه' },
  { key: 'paymentMethod', label: 'روش پرداخت' },
]

const CUSTOMER_SUB_LINES = [
  { key: 'size', label: 'اندازه' },
  { key: 'options', label: 'گزینه‌ها و طعم‌ها (شیر بادام، سیروپ...)' },
  { key: 'modifiers', label: 'تغییرات آماده‌سازی' },
  { key: 'customDrink', label: 'سفارشی‌سازی نوشیدنی‌ساز' },
  { key: 'payer', label: 'سهم پرداخت‌کننده (دنگ صورت‌حساب)' },
]

const BARISTA_SUB_LINES = [
  { key: 'size', label: 'اندازه' },
  { key: 'modifiers', label: 'تغییرات آماده‌سازی' },
  { key: 'options', label: 'گزینه‌ها و طعم‌ها' },
  { key: 'customDrink', label: 'سفارشی‌سازی نوشیدنی‌ساز' },
  { key: 'baristaNote', label: 'یادداشت برای باریستا' },
]

const COLUMN_LABELS = {
  name: 'شرح اقلام',
  qty: 'تعداد (اینلاین «۲ ×»)',
  unitPrice: 'فی (تومان)',
  total: 'جمع کل (تومان)',
}

const TOTAL_LABELS = {
  subtotal: 'جمع کل اقلام',
  deliveryFee: 'هزینه ارسال / پیک',
  tax: 'مالیات بر ارزش افزوده',
  discount: 'تخفیف',
  total: 'مبلغ قابل پرداخت',
}

const DIVIDER_STYLES = [
  { value: 'dashed', label: 'خط‌چین کلاسیک (- - -)' },
  { value: 'dotted', label: 'نقطه‌چین لطیف (· · ·)' },
  { value: 'solid', label: 'خط ممتد مدرن (───)' },
  { value: 'double', label: 'دولایه رسمی (═══)' },
]

const ORNAMENT_ICONS = [
  { value: '☕', label: '☕ فنجان قهوه' },
  { value: '✦', label: '✦ ستاره بوتیک' },
  { value: '✨', label: '✨ درخشش' },
  { value: '♥', label: '♥ قلب' },
  { value: 'none', label: 'بدون نماد' },
]

const CODE_BOX_STYLES = [
  { value: 'rounded', label: 'کادر گرد بوتیک' },
  { value: 'solid', label: 'کادر مستطیلی پررنگ' },
  { value: 'minimal', label: 'ساده بدون کادر' },
]

const DEFAULT_LAYOUT = {
  customer: {
    header: {
      showAddress: true,
      showPhone: true,
      cafeName: '',
      subtitle: 'صورت‌حساب سفارش',
      customAddress: '',
      customPhone: '',
      taxId: '',
      showOrnament: true,
    },
    headerRows: CUSTOMER_HEADER_ROWS.map((row) => row.key),
    items: {
      columns: [
        { key: 'name', label: '', visible: true, widthPct: null },
        { key: 'qty', label: 'تعداد', visible: true, widthPct: null },
        { key: 'unitPrice', label: 'فی', visible: false, widthPct: null },
        { key: 'total', label: 'جمع کل', visible: true, widthPct: null },
      ],
      subLines: CUSTOMER_SUB_LINES.map((row) => row.key),
      showHeaderRow: false,
      showOptionPrices: false,
      showUnitPriceNote: true,
      fontSize: 30,
    },
    totals: {
      rows: ['subtotal', 'deliveryFee', 'tax', 'discount', 'total'].map((key) => ({
        key,
        label: TOTAL_LABELS[key],
        visible: true,
      })),
      showItemCount: false,
      showSavingsBanner: true,
    },
    paymentRows: CUSTOMER_PAYMENT_ROWS.map((row) => row.key),
    footer: {
      text: '',
      thanksText: 'نوش جان! از میزبانی شما خرسندیم ✨',
      showThanks: true,
      showCafeName: true,
      cafeNameText: '',
      showWifi: false,
      wifiSsid: '',
      wifiPassword: '',
      instagramHandle: '',
      websiteUrl: '',
      promoBanner: '',
      showQrCode: false,
      qrCodeUrl: '',
      qrCodeLabel: 'اسکن برای منوی دیجیتال و نظرسنجی',
    },
    fonts: {
      header: 40,
      info: 22,
      item: 30,
      subtotal: 24,
      total: 36,
      footerCafe: 22,
      topPadding: 24,
      bottomPadding: 96,
    },
    style: {
      dividerStyle: 'dashed',
      ornamentIcon: '☕',
      codeBoxStyle: 'rounded',
    },
  },
  barista: {
    title: 'فیش باریستا',
    infoRows: ['paidAt', 'channel', 'service', 'table', 'customerName'],
    items: {
      columns: [
        { key: 'name', label: '', visible: true, widthPct: null },
        { key: 'qty', label: '', visible: true, widthPct: null },
      ],
      subLines: BARISTA_SUB_LINES.map((row) => row.key),
      showHeaderRow: false,
      showOptionPrices: false,
      showUnitPriceNote: false,
      fontSize: 38,
    },
    showOrderNote: true,
    footer: { text: '', showSign: true },
    fonts: { title: 26, code: 56, info: 24, item: 38 },
  },
}

const CUSTOMER_PRESETS = [
  {
    id: 'boutique',
    label: '🏛 کلاسیک بوتیک',
    hint: 'متعادل با آیکون قهوه و کادر گرد',
    apply: (current) => ({
      ...current,
      header: { ...current.header, showOrnament: true, subtitle: current.header.subtitle || 'صورت‌حساب سفارش' },
      style: { dividerStyle: 'dashed', ornamentIcon: '☕', codeBoxStyle: 'rounded' },
      totals: { ...current.totals, showItemCount: false, showSavingsBanner: true },
      fonts: { ...current.fonts, header: 40, info: 22, item: 30, subtotal: 24, total: 36, footerCafe: 22, topPadding: 24, bottomPadding: 96 },
    }),
  },
  {
    id: 'specialty',
    label: '✨ مینیمال مدرن',
    hint: 'سبک کافی‌شاپ‌های تخصصی، ساده و لوکس',
    apply: (current) => ({
      ...current,
      header: { ...current.header, showOrnament: true },
      headerRows: ['code', 'createdAt', 'service', 'table', 'customerName'],
      items: { ...current.items, showHeaderRow: false, showUnitPriceNote: true, fontSize: 28 },
      style: { dividerStyle: 'dotted', ornamentIcon: '✨', codeBoxStyle: 'minimal' },
      totals: { ...current.totals, showItemCount: false, showSavingsBanner: true },
      fonts: { ...current.fonts, header: 42, info: 20, item: 28, subtotal: 22, total: 34, footerCafe: 22, topPadding: 32, bottomPadding: 104 },
    }),
  },
  {
    id: 'official',
    label: '📋 فاکتور رسمی و کامل',
    hint: 'ستون فی، عنوان ستون‌ها، تعداد کل و مالیات',
    apply: (current) => ({
      ...current,
      header: { ...current.header, showAddress: true, showPhone: true, showOrnament: false, subtitle: 'صورت‌حساب رسمی فروش' },
      items: {
        ...current.items,
        showHeaderRow: true,
        showOptionPrices: true,
        columns: current.items.columns.map((col) => ({ ...col, visible: true })),
      },
      style: { dividerStyle: 'solid', ornamentIcon: 'none', codeBoxStyle: 'solid' },
      totals: { ...current.totals, showItemCount: true, showSavingsBanner: true },
      fonts: { ...current.fonts, header: 38, info: 21, item: 28, subtotal: 24, total: 36, footerCafe: 22, topPadding: 24, bottomPadding: 96 },
    }),
  },
  {
    id: 'compact',
    label: '🌿 فشرده (صرفه‌جویی کاغذ)',
    hint: 'کوتاه، سریع و کم‌مصرف در ساعت شلوغی',
    apply: (current) => ({
      ...current,
      header: { ...current.header, showAddress: false, showOrnament: false },
      headerRows: ['code', 'createdAt', 'service', 'table'],
      items: { ...current.items, showHeaderRow: false, showUnitPriceNote: false, fontSize: 26 },
      style: { dividerStyle: 'dashed', ornamentIcon: 'none', codeBoxStyle: 'minimal' },
      totals: { ...current.totals, showItemCount: false, showSavingsBanner: false },
      fonts: { ...current.fonts, header: 34, info: 18, item: 26, subtotal: 20, total: 30, footerCafe: 18, topPadding: 16, bottomPadding: 72 },
    }),
  },
]

function mergeLoadedLayout(data) {
  const dc = DEFAULT_LAYOUT.customer
  const db = DEFAULT_LAYOUT.barista
  const c = data?.customer ?? {}
  const b = data?.barista ?? {}

  const rawRows = Array.isArray(c?.totals?.rows) ? c.totals.rows : []
  const byKey = new Map(rawRows.map((row) => [row.key, row]))
  const mergedTotalsRows = ['subtotal', 'deliveryFee', 'tax', 'discount', 'total'].map((key) => {
    const existing = byKey.get(key)
    const fallback = dc.totals.rows.find((row) => row.key === key)
    return existing ? { ...fallback, ...existing } : { ...fallback }
  })

  return {
    customer: {
      ...dc,
      ...c,
      header: { ...dc.header, ...(c.header ?? {}) },
      items: { ...dc.items, ...(c.items ?? {}) },
      totals: {
        ...dc.totals,
        ...(c.totals ?? {}),
        rows: mergedTotalsRows,
      },
      footer: { ...dc.footer, ...(c.footer ?? {}) },
      fonts: { ...dc.fonts, ...(c.fonts ?? {}) },
      style: { ...dc.style, ...(c.style ?? {}) },
    },
    barista: {
      ...db,
      ...b,
      items: { ...db.items, ...(b.items ?? {}) },
      footer: { ...db.footer, ...(b.footer ?? {}) },
      fonts: { ...db.fonts, ...(b.fonts ?? {}) },
    },
  }
}

function toPersianNumber(value) {
  return String(value ?? '').replace(/\d/g, (digit) => '۰۱۲۳۴۵۶۷۸۹'[Number(digit)])
}

// ── Sample order data used by the live preview ───────────────────────────────

const SAMPLE_ORDER = {
  cafeName: 'کافه شوگریستا',
  address: 'تهران، خیابان ولیعصر، پلاک ۱۲',
  phone: '۰۲۱-۲۲۰۰۳۳۰۰',
  code: 'SG-K2B7PX',
  createdAtLabel: '۱۴۰۵/۰۹/۰۲ — ۱۸:۳۰',
  paidAtLabel: '۱۴۰۵/۰۹/۰۲ — ۱۸:۳۲',
  channelLabel: 'سفارش حضوری (صندوق)',
  serviceLabel: 'سرو در سالن',
  tableLabel: '۴',
  pickupAtLabel: '۱۴۰۵/۰۹/۰۲ — ۱۸:۴۵',
  customerName: 'علی رضایی',
  customerPhone: '۰۹۱۲۰۰۰۰۰۰۰',
  note: 'کم‌شیرین سرو شود',
  paymentLabel: 'کارت‌خوان (پوز)',
  reference: '۸۸۲۱۳-بانک ملت',
  items: [
    {
      name: 'لاته',
      sizeLabel: 'بزرگ',
      quantity: 2,
      quantityLabel: '۲',
      unitPriceLabel: '۳۵۰٬۰۰۰',
      totalLabel: '۷۰۰٬۰۰۰',
      modifierLabels: ['داغ سرو شود'],
      options: [
        { optionName: 'شیر بادام', priceDeltaLabel: '۳۰٬۰۰۰' },
        { optionName: 'سیروپ فندق', priceDeltaLabel: '۲۰٬۰۰۰' },
      ],
      customDrinkSummary: [],
      payerLabel: null,
      baristaNote: 'تک‌شات',
    },
  ],
  totals: {
    subtotalLabel: '۷۰۰٬۰۰۰',
    deliveryFeeLabel: null,
    taxLabel: '۷۰٬۰۰۰',
    discountLabel: '۷۷٬۰۰۰',
    totalLabel: '۶۹۳٬۰۰۰',
    amountPaidLabel: '۶۹۳٬۰۰۰',
  },
}

const SAMPLE_BARISTA_ITEMS = [
  {
    name: 'لاته',
    sizeLabel: 'بزرگ',
    quantity: 2,
    quantityLabel: '۲',
    modifierLabels: ['داغ سرو شود'],
    options: [
      { optionName: 'شیر بادام', priceDeltaLabel: '۳۰٬۰۰۰' },
      { optionName: 'سیروپ فندق', priceDeltaLabel: '۲۰٬۰۰۰' },
    ],
    customDrinkSummary: [],
    baristaNote: 'تک‌شات',
  },
]

// ── Helpers ──────────────────────────────────────────────────────────────────

function moveAt(array, from, to) {
  const copy = [...array]
  const [item] = copy.splice(from, 1)
  copy.splice(to < 0 ? copy.length : to, 0, item)
  return copy
}

function keyCatalog(catalog) {
  const byKey = new Map(catalog.map((row) => [row.key, row]))
  return (key) => byKey.get(key)?.label ?? key
}

function ToggleField({ label, value, onChange, hint }) {
  return (
    <label className="receipt-layout-field receipt-layout-toggle">
      <input type="checkbox" checked={!!value} onChange={(event) => onChange(event.target.checked)} />
      <span>
        {label}
        {hint ? <small>{hint}</small> : null}
      </span>
    </label>
  )
}

function OrderedListEditor({ title, keys, catalog, onChange, emptyNote }) {
  const present = new Set(keys)
  const remaining = catalog.filter((row) => !present.has(row.key))
  const labelOf = keyCatalog(catalog)

  return (
    <div className="receipt-layout-ordered">
      <p className="receipt-layout-ordered-title">{title}</p>
      <ul>
        {keys.map((key, index) => (
          <li key={key}>
            <span className="receipt-layout-ordered-label">{labelOf(key)}</span>
            <span className="receipt-layout-ordered-actions">
              <button
                type="button"
                title="انتقال به بالا"
                disabled={index === 0}
                onClick={() => onChange(moveAt(keys, index, index - 1))}
              >
                ↑
              </button>
              <button
                type="button"
                title="انتقال به پایین"
                disabled={index === keys.length - 1}
                onClick={() => onChange(moveAt(keys, index, index + 1))}
              >
                ↓
              </button>
              <button
                type="button"
                className="danger"
                title="حذف از فیش"
                onClick={() => onChange(keys.filter((candidateKey) => candidateKey !== key))}
              >
                ✕
              </button>
            </span>
          </li>
        ))}
      </ul>
      {remaining.length > 0 ? (
        <select
          value=""
          onChange={(event) => {
            const newKey = event.target.value
            if (newKey) onChange([...keys, newKey])
          }}
        >
          <option value="">+ افزودن سطر…</option>
          {remaining.map((row) => (
            <option key={row.key} value={row.key}>
              {row.label}
            </option>
          ))}
        </select>
      ) : null}
      {emptyNote && keys.length === 0 ? <p className="receipt-layout-hint">{emptyNote}</p> : null}
    </div>
  )
}

function NumberRangeField({ label, value, min = 12, max = 90, hint, onChange }) {
  return (
    <label className="receipt-layout-field receipt-layout-font">
      <span>
        {label}
        {hint ? <small>{hint}</small> : null}
      </span>
      <input
        type="number"
        min={min}
        max={max}
        value={value}
        onChange={(event) => onChange(Math.max(min, Math.min(max, Number(event.target.value) || min)))}
      />
    </label>
  )
}

// ── Live preview ─────────────────────────────────────────────────────────────

function ReceiptPreview({ layout, copy, siteSettings }) {
  if (copy === 'customer') {
    return <CustomerPreview layout={layout} siteSettings={siteSettings} />
  }
  return <BaristaPreview layout={layout} siteSettings={siteSettings} />
}

function PreviewRow({ left, right, bold, big, small }) {
  return (
    <div className={`receipt-preview-row${bold ? ' bold' : ''}${big ? ' big' : ''}${small ? ' small' : ''}`}>
      <span>{right}</span>
      {left ? <em>{left}</em> : null}
    </div>
  )
}

function CustomerPreview({ layout, siteSettings }) {
  const o = SAMPLE_ORDER
  const { header, headerRows, items, totals, paymentRows, footer, fonts, style } = layout
  const scale = Math.max(0.66, Math.min(1.06, (fonts.header || 40) / 40))

  const cafeName = header.cafeName?.trim() || siteSettings?.cafeName?.trim() || o.cafeName
  const footerCafeName = footer.cafeNameText?.trim() || cafeName
  const subtitle = header.subtitle !== undefined && header.subtitle !== null ? header.subtitle.trim() : 'صورت‌حساب سفارش'
  const resolvedAddress = header.customAddress?.trim() || siteSettings?.address?.trim() || o.address
  const resolvedPhone = header.customPhone?.trim() || siteSettings?.contactPhone?.trim() || o.phone
  const dividerStyle = style?.dividerStyle || 'dashed'
  const ornamentIcon = style?.ornamentIcon || '☕'
  const codeBoxStyle = style?.codeBoxStyle || 'rounded'

  const qrSvg = useMemo(
    () => (footer.showQrCode && footer.qrCodeUrl?.trim() ? buildQrSvg(footer.qrCodeUrl.trim(), 108) : ''),
    [footer.showQrCode, footer.qrCodeUrl],
  )

  const columns = items.columns.filter((column) => column.visible)
  const qtyVisible = columns.some((column) => column.key === 'qty')

  const item = o.items[0]
  const optionText = item.options.map((option) =>
    items.showOptionPrices && option.priceDeltaLabel
      ? `✦ ${option.optionName} (+${option.priceDeltaLabel} ت)`
      : `✦ ${option.optionName}`,
  )

  const subLines = []
  for (const key of items.subLines) {
    if (key === 'size' && item.sizeLabel) subLines.push(`اندازه: ${item.sizeLabel}`)
    else if (key === 'options') subLines.push(...optionText)
    else if (key === 'modifiers') subLines.push(...item.modifierLabels.map((m) => `✦ ${m}`))
    else if (key === 'customDrink' && item.customDrinkSummary?.length) subLines.push(...item.customDrinkSummary)
    else if (key === 'payer' && item.payerLabel) subLines.push(`سهم ${item.payerLabel}`)
    else if (key === 'baristaNote' && item.baristaNote) subLines.push(`! ${item.baristaNote}`)
  }

  const dividerClass = `receipt-preview-divider receipt-preview-divider--${dividerStyle}`
  const cleanIg = footer.instagramHandle?.trim()
    ? footer.instagramHandle.trim().startsWith('@')
      ? footer.instagramHandle.trim()
      : `@${footer.instagramHandle.trim()}`
    : ''

  return (
    <div
      className="receipt-preview-paper"
      style={{
        fontSize: `${scale}rem`,
        paddingTop: `${Math.max(10, Math.round((fonts.topPadding ?? 24) * 0.5))}px`,
        paddingBottom: `${Math.max(14, Math.round((fonts.bottomPadding ?? 96) * 0.25))}px`,
      }}
    >
      <div className="receipt-preview-center bold big">{cafeName}</div>
      {subtitle ? (
        <div className="receipt-preview-center" style={{ fontSize: '0.85em', color: '#555', marginBottom: 4 }}>
          {subtitle}
        </div>
      ) : null}
      {header.showAddress && resolvedAddress ? <div className="receipt-preview-center small">{resolvedAddress}</div> : null}
      {header.showPhone && resolvedPhone ? <div className="receipt-preview-center small">تلفن تماس: {toPersianNumber(resolvedPhone)}</div> : null}
      {header.taxId?.trim() ? (
        <div className="receipt-preview-center small">شناسه اقتصادی / صنفی: {toPersianNumber(header.taxId.trim())}</div>
      ) : null}
      {header.showOrnament !== false && ornamentIcon !== 'none' ? (
        <div className="receipt-preview-ornament" aria-hidden="true">✦ {ornamentIcon} ✦</div>
      ) : (
        <div className={dividerClass} />
      )}

      {headerRows.map((rowKey, rowIndex) => {
        const rowId = `${rowKey}-${rowIndex}`
        if (rowKey === 'code') {
          if (codeBoxStyle === 'minimal') {
            return (
              <div key={rowId} className="receipt-preview-center bold" style={{ margin: '4px 0' }}>
                شماره سفارش: <span dir="ltr" data-latin-digits="true">#{o.code}</span>
              </div>
            )
          }
          return (
            <div
              key={rowId}
              style={{
                border: codeBoxStyle === 'solid' ? '2px solid #000' : '1.5px solid #000',
                borderRadius: codeBoxStyle === 'solid' ? 0 : 6,
                padding: '4px 8px',
                textAlign: 'center',
                fontWeight: 'bold',
                margin: '4px 0',
                letterSpacing: '0.5px',
              }}
            >
              شماره سفارش: <span dir="ltr" data-latin-digits="true">#{o.code}</span>
            </div>
          )
        }
        if (rowKey === 'createdAt') return <PreviewRow key={rowId} right="تاریخ و زمان" left={o.createdAtLabel} />
        if (rowKey === 'channel') return <PreviewRow key={rowId} right="نوع سفارش" left={o.channelLabel} />
        if (rowKey === 'service') return <PreviewRow key={rowId} right="شیوهٔ پذیرایی" left={o.serviceLabel} bold />
        if (rowKey === 'table') return <PreviewRow key={rowId} right="شماره میز" left={`میز شماره ${o.tableLabel}`} bold />
        if (rowKey === 'pickupTime') return <PreviewRow key={rowId} right="زمان تحویل" left={o.pickupAtLabel} bold />
        if (rowKey === 'customerName') return <PreviewRow key={rowId} right="مشتری گرامی" left={o.customerName} />
        if (rowKey === 'customerPhone') return <PreviewRow key={rowId} right="شماره همراه" left={o.customerPhone} />
        if (rowKey === 'note') return <div key={rowId} className="receipt-preview-note">توضیحات مهمان: {o.note}</div>
        return null
      })}
      <div className={dividerClass} />

      {items.showHeaderRow && columns.some((column) => column.key === 'total' || column.key === 'unitPrice') ? (
        <div className="receipt-preview-cols-head">
          {columns
            .filter((column) => column.key === 'unitPrice' || column.key === 'total')
            .map((column) => (
              <em key={column.key} style={column.widthPct ? { width: `${column.widthPct}%` } : undefined}>
                {column.label || (column.key === 'unitPrice' ? 'فی' : 'جمع کل')}
              </em>
            ))}
        </div>
      ) : null}

      <div className="receipt-preview-item">
        <div className="receipt-preview-name">
          <strong>
            {qtyVisible ? `${item.quantityLabel} × ` : ''}
            {item.name}
          </strong>
          {columns.some((column) => column.key === 'total') ? (
            <em className="receipt-preview-item-total">{item.totalLabel}</em>
          ) : null}
        </div>
        {columns.some((column) => column.key === 'unitPrice') ? (
          <div className="receipt-preview-unit-price">{item.unitPriceLabel}</div>
        ) : null}
        {subLines.length > 0 ? <div className="receipt-preview-sublines">{subLines.join('   ')}</div> : null}
        {items.showUnitPriceNote && !columns.some((column) => column.key === 'unitPrice') && item.quantity > 1 ? (
          <div className="receipt-preview-sublines">نرخ واحد (فی): {item.unitPriceLabel} تومان</div>
        ) : null}
      </div>
      <div className={dividerClass} />

      {totals.showItemCount ? <PreviewRow right="تعداد کل اقلام" left="۲ عدد" /> : null}

      {totals.rows.map((row) => {
        if (!row.visible) return null
        if (row.key === 'subtotal') return <PreviewRow key="subtotal" right={row.label || TOTAL_LABELS.subtotal} left={`${o.totals.subtotalLabel} ت`} />
        if (row.key === 'deliveryFee') return <PreviewRow key="deliveryFee" right={row.label || TOTAL_LABELS.deliveryFee} left={o.totals.deliveryFeeLabel ? `${o.totals.deliveryFeeLabel} ت` : null} />
        if (row.key === 'tax') return <PreviewRow key="tax" right={row.label || TOTAL_LABELS.tax} left={`${o.totals.taxLabel} ت`} />
        if (row.key === 'discount') return <PreviewRow key="discount" right={row.label || TOTAL_LABELS.discount} left={`−${o.totals.discountLabel} ت`} bold />
        if (row.key === 'total') {
          return (
            <div key="total" style={{ borderTop: '2px solid #000', borderBottom: '2px solid #000', padding: '4px 0', margin: '4px 0' }}>
              <PreviewRow right={row.label || TOTAL_LABELS.total} left={`${o.totals.totalLabel} تومان`} bold big />
            </div>
          )
        }
        return null
      })}

      {totals.showSavingsBanner !== false ? (
        <div className="receipt-preview-card-box">✨ سود شما از این سفارش: {o.totals.discountLabel} تومان ✨</div>
      ) : null}

      {paymentRows.map((rowKey) => {
        if (rowKey === 'paymentMethod') return <PreviewRow key={rowKey} right="شیوهٔ پرداخت" left={o.paymentLabel} />
        if (rowKey === 'reference') return <PreviewRow key={rowKey} right="کد رهگیری بانکی" left={o.reference} />
        if (rowKey === 'amountPaid') return <PreviewRow key={rowKey} right="وضعیت پرداخت" left={`${o.totals.amountPaidLabel} تومان (تسویه شد)`} bold small />
        return null
      })}

      <div className="receipt-preview-divider receipt-preview-divider--dotted" />

      {footer.promoBanner?.trim() ? (
        <div className="receipt-preview-card-box receipt-preview-card-box--promo">{footer.promoBanner.trim()}</div>
      ) : null}

      {footer.showWifi && (footer.wifiSsid?.trim() || footer.wifiPassword?.trim()) ? (
        <div className="receipt-preview-card-box">
          <div className="bold">وای‌فای رایگان مهمان (Wi-Fi)</div>
          {footer.wifiSsid?.trim() ? <div>شبکه: <span dir="ltr" data-latin-digits="true">{footer.wifiSsid.trim()}</span></div> : null}
          {footer.wifiPassword?.trim() ? <div>رمز عبور: <span dir="ltr" data-latin-digits="true">{footer.wifiPassword.trim()}</span></div> : null}
        </div>
      ) : null}

      {qrSvg ? (
        <div className="receipt-preview-qr">
          <div dangerouslySetInnerHTML={{ __html: qrSvg }} />
          {footer.qrCodeLabel?.trim() ? <div className="small">{footer.qrCodeLabel.trim()}</div> : null}
        </div>
      ) : null}

      {cleanIg ? <div className="receipt-preview-center small">اینستاگرام: <span dir="ltr" data-latin-digits="true">{cleanIg}</span></div> : null}
      {footer.websiteUrl?.trim() ? <div className="receipt-preview-center small">وب‌سایت: <span dir="ltr" data-latin-digits="true">{footer.websiteUrl.trim()}</span></div> : null}
      {footer.text ? <div className="receipt-preview-center small">{footer.text}</div> : null}
      {footer.showThanks ? <div className="receipt-preview-center bold">{footer.thanksText || 'نوش جان! از میزبانی شما خرسندیم ✨'}</div> : null}
      {footer.showCafeName ? <div className="receipt-preview-center bold small">« {footerCafeName} »</div> : null}
      {header.showOrnament !== false && ornamentIcon !== 'none' ? (
        <div className="receipt-preview-ornament" aria-hidden="true">✦ {ornamentIcon} ✦</div>
      ) : null}
      <p className="receipt-preview-hint">
        تیتر {toPersianNumber(fonts.header)} • اطلاعات {toPersianNumber(fonts.info)} • آیتم {toPersianNumber(fonts.item)} • نام انتها {toPersianNumber(fonts.footerCafe ?? 22)} • حاشیه بالا/پایین {toPersianNumber(fonts.topPadding ?? 24)}/{toPersianNumber(fonts.bottomPadding ?? 96)}
      </p>
    </div>
  )
}

function BaristaPreview({ layout, siteSettings }) {
  const o = SAMPLE_ORDER
  const { title, infoRows, items, showOrderNote, footer, fonts } = layout
  const item = SAMPLE_BARISTA_ITEMS[0]
  const qtyVisible = items.columns.some((column) => column.key === 'qty' && column.visible)
  const cafeName = siteSettings?.cafeName?.trim() || 'شوگریستا'

  const subLines = []
  for (const key of items.subLines) {
    if (key === 'size' && item.sizeLabel) subLines.push(item.sizeLabel)
    else if (key === 'modifiers') subLines.push(...item.modifierLabels.map((m) => `✦ ${m}`))
    else if (key === 'options')
      subLines.push(
        ...item.options.map((option) =>
          items.showOptionPrices && option.priceDeltaLabel
            ? `✦ ${option.optionName} (+${option.priceDeltaLabel})`
            : `✦ ${option.optionName}`,
        ),
      )
    else if (key === 'customDrink' && item.customDrinkSummary?.length) subLines.push(...item.customDrinkSummary)
    else if (key === 'baristaNote' && item.baristaNote) subLines.push(`! ${item.baristaNote}`)
  }

  return (
    <div className="receipt-preview-paper barista">
      <div className="receipt-preview-center bold">{title || 'فیش باریستا'}</div>
      <div className="receipt-preview-center bold big">{o.code}</div>
      {infoRows.map((rowKey) => {
        if (rowKey === 'paidAt') return <PreviewRow key="paidAt" right={o.paidAtLabel} />
        if (rowKey === 'createdAt') return <PreviewRow key="createdAt" right={o.createdAtLabel} />
        if (rowKey === 'channel') return <PreviewRow key="channel" right={o.channelLabel} />
        if (rowKey === 'service') return <PreviewRow key="service" right={o.serviceLabel} bold />
        if (rowKey === 'table') return <PreviewRow key="table" right={`میز ${o.tableLabel}`} />
        if (rowKey === 'customerName') return <PreviewRow key="customerName" right={`مشتری: ${o.customerName}`} />
        if (rowKey === 'customerPhone') return <PreviewRow key="customerPhone" right={o.customerPhone} />
        if (rowKey === 'paymentMethod') return <PreviewRow key="paymentMethod" right="روش پرداخت" left={o.paymentLabel} />
        return null
      })}
      <div className="receipt-preview-divider double" />
      <div className="receipt-preview-item barista-item">
        <strong>
          {qtyVisible ? `${item.quantityLabel} × ` : ''}
          {item.name}
        </strong>
        {subLines.length > 0 ? <div className="receipt-preview-sublines">{subLines.join('   ')}</div> : null}
      </div>
      <div className="receipt-preview-divider" />
      {showOrderNote ? <div className="receipt-preview-note bold">یادداشت سفارش: {o.note}</div> : null}
      {footer.text ? (
        <div className="receipt-preview-center small">{footer.text}</div>
      ) : footer.showSign ? (
        <div className="receipt-preview-center small">تهیه و تحویل — {cafeName}</div>
      ) : null}
      <p className="receipt-preview-hint">
        آیتم {toPersianNumber(fonts.item)} • کد {toPersianNumber(fonts.code)} • تیتر {toPersianNumber(fonts.title)}
      </p>
    </div>
  )
}

// ── Editor panels ────────────────────────────────────────────────────────────

function ColumnEditor({ columns, onChange, allowMoney }) {
  const update = (index, patch) => {
    onChange(columns.map((column, i) => (i === index ? { ...column, ...patch } : column)))
  }

  return (
    <div className="receipt-layout-columns">
      <p className="receipt-layout-ordered-title">ستون‌های جدول آیتم‌ها</p>
      <ul>
        {columns.map((column, index) => {
          const isMoney = column.key === 'unitPrice' || column.key === 'total'
          return (
            <li key={column.key} className={column.visible ? '' : 'off'}>
              <span className="receipt-layout-ordered-label">{COLUMN_LABELS[column.key]}</span>
              <span className="receipt-layout-column-controls">
                {allowMoney && (column.key === 'qty' || isMoney) ? (
                  <>
                    <input
                      type="text"
                      placeholder="عنوان ستون"
                      value={column.label ?? ''}
                      disabled={!column.visible}
                      onChange={(event) => update(index, { label: event.target.value })}
                    />
                    {isMoney ? (
                      <input
                        type="number"
                        min={15}
                        max={90}
                        placeholder="عرض ٪"
                        value={column.widthPct ?? ''}
                        disabled={!column.visible}
                        style={{ width: 74 }}
                        onChange={(event) =>
                          update(index, {
                            widthPct: event.target.value === '' ? null : Math.max(15, Math.min(90, Number(event.target.value) || 15)),
                          })
                        }
                      />
                    ) : null}
                  </>
                ) : null}
                <button type="button" title="انتقال به بالا" disabled={index === 0} onClick={() => onChange(moveAt(columns, index, index - 1))}>
                  ↑
                </button>
                <button type="button" title="انتقال به پایین" disabled={index === columns.length - 1} onClick={() => onChange(moveAt(columns, index, index + 1))}>
                  ↓
                </button>
                <button
                  type="button"
                  className={column.visible ? 'ok' : 'danger'}
                  title={column.visible ? 'برداشتن ستون از فیش' : 'نمایش ستون'}
                  disabled={!allowMoney ? false : column.key === 'name' || column.key === 'total'}
                  onClick={() => update(index, { visible: !column.visible })}
                >
                  {column.visible ? 'نمایش' : 'حذف‌شده'}
                </button>
              </span>
            </li>
          )
        })}
      </ul>
    </div>
  )
}

function CustomerEditor({ layout, update, onApplyPreset, siteSettings }) {
  const { header, headerRows, items, totals, paymentRows, footer, fonts, style } = layout
  return (
    <div className="receipt-layout-editor">
      <div className="receipt-presets-bar">
        <p className="receipt-layout-ordered-title">قالب‌های آمادهٔ طراحی فیش (یک کلیک)</p>
        <div className="receipt-presets-grid">
          {CUSTOMER_PRESETS.map((preset) => (
            <button
              key={preset.id}
              type="button"
              className="receipt-preset-btn"
              onClick={() => onApplyPreset(preset)}
            >
              <strong>{preset.label}</strong>
              <small>{preset.hint}</small>
            </button>
          ))}
        </div>
      </div>

      <h4>۱. هویت برند و سربرگ فیش</h4>
      <div className="receipt-layout-grid">
        <label className="receipt-layout-field">
          <span>نام کامل کافه روی فیش (بالا)</span>
          <input
            type="text"
            value={header.cafeName ?? ''}
            placeholder={siteSettings?.cafeName || 'خالی = نام کافه از تنظیمات سایت'}
            onChange={(event) => update('customer.header.cafeName', event.target.value)}
          />
        </label>
        <label className="receipt-layout-field">
          <span>زیرعنوان / شعار زیر نام کافه</span>
          <input
            type="text"
            value={header.subtitle ?? ''}
            placeholder="مثلاً صورت‌حساب سفارش یا Specialty Coffee"
            onChange={(event) => update('customer.header.subtitle', event.target.value)}
          />
        </label>
        <label className="receipt-layout-field">
          <span>آدرس اختصاصی فیش (اختیاری)</span>
          <input
            type="text"
            value={header.customAddress ?? ''}
            placeholder={siteSettings?.address || 'خالی = آدرس تنظیمات سایت'}
            onChange={(event) => update('customer.header.customAddress', event.target.value)}
          />
        </label>
        <label className="receipt-layout-field">
          <span>تلفن اختصاصی فیش (اختیاری)</span>
          <input
            type="text"
            className="ltr-input"
            value={header.customPhone ?? ''}
            placeholder={siteSettings?.contactPhone || 'خالی = تلفن تنظیمات سایت'}
            onChange={(event) => update('customer.header.customPhone', event.target.value)}
          />
        </label>
        <label className="receipt-layout-field">
          <span>شناسه اقتصادی / شماره صنفی (اختیاری)</span>
          <input
            type="text"
            className="ltr-input"
            value={header.taxId ?? ''}
            placeholder="مثلاً ۴۱۱۱۲۲۲۲۳۳۳۳"
            onChange={(event) => update('customer.header.taxId', event.target.value)}
          />
        </label>
        <div className="receipt-layout-toggle-stack">
          <ToggleField label="نمایش آدرس کافه" value={header.showAddress} onChange={(v) => update('customer.header.showAddress', v)} />
          <ToggleField label="نمایش تلفن کافه" value={header.showPhone} onChange={(v) => update('customer.header.showPhone', v)} />
          <ToggleField label="نمایش خط تزئینی (✦ ☕ ✦)" value={header.showOrnament !== false} onChange={(v) => update('customer.header.showOrnament', v)} />
        </div>
      </div>

      <h4>۲. سبک بصری و دکوراسیون فیش</h4>
      <div className="receipt-layout-grid receipt-layout-grid--3">
        <label className="receipt-layout-field">
          <span>سبک خطوط جداکننده</span>
          <select value={style?.dividerStyle || 'dashed'} onChange={(event) => update('customer.style.dividerStyle', event.target.value)}>
            {DIVIDER_STYLES.map((opt) => (
              <option key={opt.value} value={opt.value}>{opt.label}</option>
            ))}
          </select>
        </label>
        <label className="receipt-layout-field">
          <span>نماد تزئینی کافه</span>
          <select value={style?.ornamentIcon || '☕'} onChange={(event) => update('customer.style.ornamentIcon', event.target.value)}>
            {ORNAMENT_ICONS.map((opt) => (
              <option key={opt.value} value={opt.value}>{opt.label}</option>
            ))}
          </select>
        </label>
        <label className="receipt-layout-field">
          <span>سبک نمایش کد سفارش</span>
          <select value={style?.codeBoxStyle || 'rounded'} onChange={(event) => update('customer.style.codeBoxStyle', event.target.value)}>
            {CODE_BOX_STYLES.map((opt) => (
              <option key={opt.value} value={opt.value}>{opt.label}</option>
            ))}
          </select>
        </label>
      </div>

      <OrderedListEditor
        title="سطرهای اطلاعات سفارش (بالای فیش)"
        keys={headerRows}
        catalog={CUSTOMER_HEADER_ROWS}
        onChange={(keys) => update('customer.headerRows', keys)}
      />

      <h4>۳. جدول اقلام و جزئیات</h4>
      <ColumnEditor columns={items.columns} onChange={(columns) => update('customer.items.columns', columns)} allowMoney />
      <OrderedListEditor
        title="جزئیات زیر نام آیتم"
        keys={items.subLines}
        catalog={CUSTOMER_SUB_LINES}
        onChange={(keys) => update('customer.items.subLines', keys)}
        emptyNote="حداقل یک جزئیات را انتخاب کنید."
      />
      <div className="receipt-layout-grid">
        <ToggleField label="ردیف عنوان ستون‌ها" value={items.showHeaderRow} onChange={(v) => update('customer.items.showHeaderRow', v)} />
        <ToggleField label="مبلغ هر آپشن (+۱۲۰٬۰۰۰)" value={items.showOptionPrices} onChange={(v) => update('customer.items.showOptionPrices', v)} hint="گروه‌های آپشن آیتم منو" />
        <ToggleField label="یادداشت «واحد: …» برای تعداد بیش از ۱" value={items.showUnitPriceNote} onChange={(v) => update('customer.items.showUnitPriceNote', v)} />
        <NumberRangeField label="اندازهٔ آیتم‌ها" value={items.fontSize} onChange={(v) => update('customer.items.fontSize', v)} />
      </div>

      <h4>۴. جمع‌ها، تخفیف و سطرهای پرداخت</h4>
      <div className="receipt-layout-totals">
        <p className="receipt-layout-ordered-title">ردیف‌های جمع و تخفیف</p>
        {totals.rows.map((row, index) => (
          <div key={row.key} className={`receipt-layout-total-row${row.visible ? '' : ' off'}`}>
            <input
              type="checkbox"
              checked={row.visible}
              disabled={row.key === 'total'}
              onChange={(event) => update(`customer.totals.rows.${index}.visible`, event.target.checked)}
            />
            <span>{TOTAL_LABELS[row.key] ?? row.key}</span>
            <input
              type="text"
              value={row.label ?? ''}
              placeholder={TOTAL_LABELS[row.key] ?? row.key}
              onChange={(event) => update(`customer.totals.rows.${index}.label`, event.target.value)}
            />
          </div>
        ))}
      </div>
      <div className="receipt-layout-grid">
        <ToggleField label="نمایش تعداد کل اقلام سفارش" value={totals.showItemCount} onChange={(v) => update('customer.totals.showItemCount', v)} hint="مثلاً «تعداد کل اقلام: ۳ عدد»" />
        <ToggleField label="کادر انگیزشی سود مشتری از تخفیف" value={totals.showSavingsBanner !== false} onChange={(v) => update('customer.totals.showSavingsBanner', v)} hint="وقتی سفارش تخفیف دارد نمایش داده می‌شود" />
      </div>
      <OrderedListEditor
        title="سطرهای پرداخت (زیر جمع)"
        keys={paymentRows}
        catalog={CUSTOMER_PAYMENT_ROWS}
        onChange={(keys) => update('customer.paymentRows', keys)}
      />

      <h4>۵. پانویس مدرن (وای‌فای، QR کد، شبکه اجتماعی، کوپن و نام انتهای فیش)</h4>
      <div className="receipt-layout-grid">
        <label className="receipt-layout-field receipt-layout-field--full">
          <span>کادر پیام تبلیغاتی / کوپن خرید بعدی (اختیاری)</span>
          <input
            type="text"
            value={footer.promoBanner ?? ''}
            placeholder="مثلاً با ارائه این فیش در خرید بعدی ۱۰٪ تخفیف بگیرید!"
            onChange={(event) => update('customer.footer.promoBanner', event.target.value)}
          />
        </label>

        <ToggleField label="نمایش کارت وای‌فای رایگان مهمان (Wi-Fi)" value={footer.showWifi} onChange={(v) => update('customer.footer.showWifi', v)} />
        <div />
        {footer.showWifi ? (
          <>
            <label className="receipt-layout-field">
              <span>نام شبکه Wi-Fi (SSID)</span>
              <input
                type="text"
                className="ltr-input"
                data-latin-digits="true"
                value={footer.wifiSsid ?? ''}
                placeholder="Sugarista_Guest"
                onChange={(event) => update('customer.footer.wifiSsid', event.target.value)}
              />
            </label>
            <label className="receipt-layout-field">
              <span>رمز عبور Wi-Fi</span>
              <input
                type="text"
                className="ltr-input"
                data-latin-digits="true"
                value={footer.wifiPassword ?? ''}
                placeholder="coffee2026"
                onChange={(event) => update('customer.footer.wifiPassword', event.target.value)}
              />
            </label>
          </>
        ) : null}

        <ToggleField label="نمایش کد QR قابل اسکن روی فیش" value={footer.showQrCode} onChange={(v) => update('customer.footer.showQrCode', v)} hint="برای منوی دیجیتال، اینستاگرام یا نظرسنجی" />
        <div />
        {footer.showQrCode ? (
          <>
            <label className="receipt-layout-field">
              <span>لینک کد QR (URL)</span>
              <input
                type="url"
                className="ltr-input"
                data-latin-digits="true"
                value={footer.qrCodeUrl ?? ''}
                placeholder="https://sugarista.ir"
                onChange={(event) => update('customer.footer.qrCodeUrl', event.target.value)}
              />
            </label>
            <label className="receipt-layout-field">
              <span>متن زیر کد QR</span>
              <input
                type="text"
                value={footer.qrCodeLabel ?? ''}
                placeholder="اسکن برای منوی دیجیتال و باشگاه مشتریان"
                onChange={(event) => update('customer.footer.qrCodeLabel', event.target.value)}
              />
            </label>
          </>
        ) : null}

        <label className="receipt-layout-field">
          <span>آیدی اینستاگرام (اختیاری)</span>
          <input
            type="text"
            className="ltr-input"
            data-latin-digits="true"
            value={footer.instagramHandle ?? ''}
            placeholder="@sugarista.cafe"
            onChange={(event) => update('customer.footer.instagramHandle', event.target.value)}
          />
        </label>
        <label className="receipt-layout-field">
          <span>آدرس وب‌سایت (اختیاری)</span>
          <input
            type="text"
            className="ltr-input"
            data-latin-digits="true"
            value={footer.websiteUrl ?? ''}
            placeholder="sugarista.ir"
            onChange={(event) => update('customer.footer.websiteUrl', event.target.value)}
          />
        </label>

        <label className="receipt-layout-field">
          <span>متن پانویس (خالی = متن صفحهٔ تنظیمات)</span>
          <input type="text" value={footer.text ?? ''} onChange={(event) => update('customer.footer.text', event.target.value)} />
        </label>
        <label className="receipt-layout-field">
          <span>متن جملهٔ سپاسگزاری</span>
          <input
            type="text"
            value={footer.thanksText ?? ''}
            placeholder="نوش جان! از میزبانی شما خرسندیم ✨"
            onChange={(event) => update('customer.footer.thanksText', event.target.value)}
          />
        </label>

        <ToggleField label="نمایش جملهٔ سپاسگزاری" value={footer.showThanks} onChange={(v) => update('customer.footer.showThanks', v)} />
        <ToggleField label="نمایش نام کافه در انتهای فیش" value={footer.showCafeName} onChange={(v) => update('customer.footer.showCafeName', v)} />

        {footer.showCafeName ? (
          <label className="receipt-layout-field receipt-layout-field--full">
            <span>نام کامل کافه در انتهای فیش (اختیاری — خالی = همان نام بالای فیش)</span>
            <input
              type="text"
              value={footer.cafeNameText ?? ''}
              placeholder={header.cafeName || siteSettings?.cafeName || 'مثلاً کافه و شیرینی‌پزی شوگاریستا'}
              onChange={(event) => update('customer.footer.cafeNameText', event.target.value)}
            />
          </label>
        ) : null}
      </div>

      <h4>۶. اندازهٔ فونت‌ها و حاشیه‌های ایمن کاغذ (جلوگیری از بریدگی نام کافه)</h4>
      <div className="receipt-layout-grid">
        <NumberRangeField label="تیتر بالای فیش (نام کافه)" value={fonts.header} onChange={(v) => update('customer.fonts.header', v)} />
        <NumberRangeField label="نام کافه در انتهای فیش" value={fonts.footerCafe ?? 22} onChange={(v) => update('customer.fonts.footerCafe', v)} />
        <NumberRangeField label="سطرهای اطلاعات" value={fonts.info} onChange={(v) => update('customer.fonts.info', v)} />
        <NumberRangeField label="جمع اقلام و تخفیف" value={fonts.subtotal} onChange={(v) => update('customer.fonts.subtotal', v)} />
        <NumberRangeField label="مبلغ قابل پرداخت نهایی" value={fonts.total} onChange={(v) => update('customer.fonts.total', v)} />
        <NumberRangeField
          label="حاشیهٔ ایمن بالای فیش (پیکسل)"
          value={fonts.topPadding ?? 24}
          min={0}
          max={160}
          hint="برای جلوگیری از بریدگی نام کافه در بالا"
          onChange={(v) => update('customer.fonts.topPadding', v)}
        />
        <NumberRangeField
          label="حاشیهٔ ایمن پایین فیش (پیکسل)"
          value={fonts.bottomPadding ?? 96}
          min={0}
          max={160}
          hint="فاصله از تیغهٔ کاتر چاپگر در انتها"
          onChange={(v) => update('customer.fonts.bottomPadding', v)}
        />
      </div>
    </div>
  )
}

function BaristaEditor({ layout, update }) {
  const { title, infoRows, items, showOrderNote, footer, fonts } = layout
  return (
    <div className="receipt-layout-editor">
      <div className="receipt-layout-grid">
        <label className="receipt-layout-field">
          <span>عنوان بالای فیش</span>
          <input type="text" value={title} onChange={(event) => update('barista.title', event.target.value)} />
        </label>
      </div>
      <OrderedListEditor
        title="سطرهای اطلاعات"
        keys={infoRows}
        catalog={BARISTA_INFO_ROWS}
        onChange={(keys) => update('barista.infoRows', keys)}
      />
      <h4>آیتم‌ها</h4>
      <OrderedListEditor
        title="جزئیات زیر نام آیتم"
        keys={items.subLines}
        catalog={BARISTA_SUB_LINES}
        onChange={(keys) => update('barista.items.subLines', keys)}
      />
      <div className="receipt-layout-grid">
        <ToggleField label="مبلغ هر آپشن" value={items.showOptionPrices} onChange={(v) => update('barista.items.showOptionPrices', v)} hint="در آشپزخانه معمولاً خاموش می‌ماند" />
        <NumberRangeField label="اندازهٔ آیتم‌ها" value={items.fontSize} onChange={(v) => update('barista.items.fontSize', v)} />
        <ToggleField label="یادداشت سفارش در انتها" value={showOrderNote} onChange={(v) => update('barista.showOrderNote', v)} />
      </div>
      <h4>پانویس و اندازه‌ها</h4>
      <div className="receipt-layout-grid">
        <label className="receipt-layout-field">
          <span>متن پانویس (خالی = امضای پیش‌فرض)</span>
          <input type="text" value={footer.text} onChange={(event) => update('barista.footer.text', event.target.value)} />
        </label>
        <ToggleField label="نمایش امضای پیش‌فرض" value={footer.showSign} onChange={(v) => update('barista.footer.showSign', v)} />
        <NumberRangeField label="عنوان" value={fonts.title} onChange={(v) => update('barista.fonts.title', v)} />
        <NumberRangeField label="کد سفارش" value={fonts.code} onChange={(v) => update('barista.fonts.code', v)} />
        <NumberRangeField label="اطلاعات" value={fonts.info} onChange={(v) => update('barista.fonts.info', v)} />
      </div>
    </div>
  )
}

// ── Root panel ──────────────────────────────────────────────────────────────

export function ReceiptLayoutPanel({ session, siteSettings, onLayoutSaved, apiRequest, getReadableError }) {
  const [layout, setLayout] = useState(null)
  const [loaded, setLoaded] = useState(false)
  const [saving, setSaving] = useState(false)
  const [message, setMessage] = useState('')
  const [error, setError] = useState('')
  const [tab, setTab] = useState('customer')

  const load = useCallback(async () => {
    if (!session?.token) return
    try {
      const data = await apiRequest('/admin/receipt-layout', { token: session.token })
      setLayout(mergeLoadedLayout(data))
    } catch (requestError) {
      setError(getReadableError(requestError))
    } finally {
      setLoaded(true)
    }
  }, [apiRequest, session?.token, getReadableError])

  useEffect(() => {
    load()
  }, [load])

  const update = useCallback((path, value) => {
    setLayout((current) => {
      const next = structuredClone(current)
      const segments = path.split('.')
      let container = next
      for (let i = 0; i < segments.length - 1; i += 1) {
        const key = segments[i]
        const nextIsNumeric = /^\d+$/.test(segments[i + 1] ?? '')
        if (nextIsNumeric) {
          container = Array.isArray(container?.[key]) ? container[key][Number(segments[i + 1])] : undefined
          i += 1
        } else {
          container = container?.[key]
        }
        if (container === undefined || container === null) {
          return current
        }
      }
      const leafKey = segments[segments.length - 1]
      if (container === undefined || container === null) {
        return current
      }
      container[leafKey] = value
      return next
    })
  }, [])

  const applyCustomerPreset = useCallback((preset) => {
    setLayout((current) => {
      if (!current) return current
      return {
        ...current,
        customer: preset.apply(structuredClone(current.customer)),
      }
    })
    setMessage(`قالب آماده «${preset.label}» روی پیش‌نمایش اعمال شد (برای ثبت نهایی ذخیره کنید).`)
    setError('')
  }, [])

  const handleSave = async () => {
    if (!session?.token || !layout) return
    setSaving(true)
    setError('')
    setMessage('')
    try {
      const saved = await apiRequest('/admin/receipt-layout', {
        method: 'PUT',
        body: { customer: layout.customer, barista: layout.barista },
        token: session.token,
      })
      const merged = mergeLoadedLayout(saved ?? layout)
      setLayout(merged)
      onLayoutSaved?.(merged)
      setMessage('طرح فیش ذخیره شد و بلافاصله روی صندوق فروش و چاپگر حرارتی اعمال می‌شود.')
    } catch (requestError) {
      setError(getReadableError(requestError))
    } finally {
      setSaving(false)
    }
  }

  const handleReset = () => {
    setLayout(structuredClone(DEFAULT_LAYOUT))
    setMessage('پیش‌نمایش به طرح پیش‌فرض برگشت (برای اعمال، ذخیره کنید).')
    setError('')
  }

  if (!loaded) {
    return (
      <section id="settings-receipt" className="panel settings-section">
        <div className="panel-head">
          <div className="settings-section-head">
            <h3>طرح فیش‌های چاپی</h3>
          </div>
        </div>
        <p className="feedback">در حال دریافت طرح فعلی…</p>
      </section>
    )
  }

  return (
    <section id="settings-receipt" className="panel settings-section">
      <div className="panel-head">
        <div className="settings-section-head">
          <span className="settings-section-badge">
            <span className="receipt-layout-badge-icon" aria-hidden="true">
              🧾
            </span>
          </span>
          <div>
            <h3>استودیو طراحی فیش‌های چاپی</h3>
            <p>
              هویت برند، نام کامل کافه در بالا و پایین، وای‌فای مهمان، کد QR قابل اسکن، پیام تبلیغاتی، سبک خطوط، حاشیه‌های ایمن برش و چیدمان ستون‌ها را زنده طراحی کنید.
            </p>
          </div>
        </div>
      </div>

      <div className="receipt-layout-tabs">
        <button type="button" className={tab === 'customer' ? 'active' : ''} onClick={() => setTab('customer')}>
          فیش مشتری (فاکتور)
        </button>
        <button type="button" className={tab === 'barista' ? 'active' : ''} onClick={() => setTab('barista')}>
          فیش آشپزخانه (باریستا)
        </button>
      </div>

      {layout ? (
        <div className="receipt-layout-body">
          <div className="receipt-layout-form">
            {tab === 'customer' ? (
              <CustomerEditor
                layout={layout?.customer ?? DEFAULT_LAYOUT.customer}
                update={update}
                onApplyPreset={applyCustomerPreset}
                siteSettings={siteSettings}
              />
            ) : (
              <BaristaEditor layout={layout?.barista ?? DEFAULT_LAYOUT.barista} update={update} />
            )}
            <div className="receipt-layout-actions">
              <button type="button" className="receipt-layout-save" onClick={handleSave} disabled={saving}>
                {saving ? 'در حال ذخیره…' : 'ذخیرهٔ طرح فیش'}
              </button>
              <button type="button" className="receipt-layout-reset" onClick={handleReset} disabled={saving}>
                بازگشت به پیش‌فرض
              </button>
            </div>
            {message ? <p className="feedback ok">{message}</p> : null}
            {error ? <p className="feedback err">{error}</p> : null}
          </div>
          <div className="receipt-layout-preview">
            <p className="receipt-layout-ordered-title">پیش‌نمایش زنده (کاغذ حرارتی ۸۰ میلی‌متری)</p>
            <ReceiptPreview
              layout={tab === 'customer' ? (layout?.customer ?? DEFAULT_LAYOUT.customer) : (layout?.barista ?? DEFAULT_LAYOUT.barista)}
              copy={tab}
              siteSettings={siteSettings}
            />
          </div>
        </div>
      ) : (
        <p className="feedback">{error || 'طرح فیش بارگذاری نشد.'}</p>
      )}
    </section>
  )
}
