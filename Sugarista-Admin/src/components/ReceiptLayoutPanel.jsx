import { useCallback, useEffect, useState } from 'react'

// ── Key catalogs (must mirror the backend whitelists) ────────────────────────

const CUSTOMER_HEADER_ROWS = [
  { key: 'code', label: 'کد سفارش' },
  { key: 'createdAt', label: 'تاریخ و ساعت سفارش' },
  { key: 'channel', label: 'نوع سفارش (آنلاین/حضوری)' },
  { key: 'service', label: 'شیوهٔ سرو' },
  { key: 'table', label: 'میز' },
  { key: 'pickupTime', label: 'زمان تحویل آماده' },
  { key: 'customerName', label: 'نام مشتری' },
  { key: 'customerPhone', label: 'شمارهٔ مشتری' },
  { key: 'note', label: 'یادداشت سفارش' },
]

const CUSTOMER_PAYMENT_ROWS = [
  { key: 'paymentMethod', label: 'روش پرداخت' },
  { key: 'reference', label: 'کد پیگیری پرداخت' },
  { key: 'amountPaid', label: 'فقط مبلغ پرداخت‌شده (چندپرداختی)' },
]

const BARISTA_INFO_ROWS = [
  { key: 'paidAt', label: 'زمان پرداخت' },
  { key: 'createdAt', label: 'زمان ثبت سفارش' },
  { key: 'channel', label: 'نوع سفارش' },
  { key: 'service', label: 'شیوهٔ سرو' },
  { key: 'table', label: 'میز' },
  { key: 'customerName', label: 'نام مشتری' },
  { key: 'customerPhone', label: 'شمارهٔ مشتری' },
  { key: 'paymentMethod', label: 'روش پرداخت' },
]

const CUSTOMER_SUB_LINES = [
  { key: 'size', label: 'اندازهٔ نوشیدنی' },
  { key: 'options', label: 'گروه‌های آپشن' },
  { key: 'modifiers', label: 'افزودنی‌ها' },
  { key: 'customDrink', label: 'سفارشی‌سازی نوشیدنی‌ساز' },
  { key: 'payer', label: 'سهم پرداخت‌کننده (تقسیم صورت‌حساب)' },
]

const BARISTA_SUB_LINES = [
  { key: 'size', label: 'اندازهٔ نوشیدنی' },
  { key: 'modifiers', label: 'افزودنی‌ها' },
  { key: 'options', label: 'گروه‌های آپشن' },
  { key: 'customDrink', label: 'سفارشی‌سازی نوشیدنی‌ساز' },
  { key: 'baristaNote', label: 'یادداشت برای باریستا' },
]

const COLUMN_LABELS = {
  name: 'نام آیتم',
  qty: 'تعداد (اینلاین «۲ ×»)',
  unitPrice: 'قیمت واحد',
  total: 'جمع خط',
}

const TOTAL_LABELS = {
  subtotal: 'جمع اقلام',
  deliveryFee: 'هزینه ارسال',
  tax: 'مالیات',
  total: 'مبلغ نهایی',
}

const DEFAULT_LAYOUT = {
  customer: {
    header: { showAddress: true, showPhone: true },
    headerRows: CUSTOMER_HEADER_ROWS.map((row) => row.key),
    items: {
      columns: [
        { key: 'name', label: '', visible: true, widthPct: null },
        { key: 'qty', label: 'تعداد', visible: true, widthPct: null },
        { key: 'unitPrice', label: 'واحد', visible: false, widthPct: null },
        { key: 'total', label: 'جمع', visible: true, widthPct: null },
      ],
      subLines: CUSTOMER_SUB_LINES.map((row) => row.key),
      showHeaderRow: false,
      showOptionPrices: false,
      showUnitPriceNote: true,
      fontSize: 30,
    },
    totals: {
      rows: ['subtotal', 'deliveryFee', 'tax', 'total'].map((key) => ({
        key,
        label: TOTAL_LABELS[key],
        visible: true,
      })),
    },
    paymentRows: CUSTOMER_PAYMENT_ROWS.map((row) => row.key),
    footer: { text: '', showThanks: true, showCafeName: true },
    fonts: { header: 40, info: 22, item: 30, subtotal: 24, total: 36 },
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

function toPersianNumber(value) {
  return String(value ?? '').replace(/\d/g, (digit) => '۰۱۲۳۴۵۶۷۸۹'[Number(digit)])
}

// ── Sample order data used by the live preview ───────────────────────────────

const SAMPLE_ORDER = {
  cafeName: 'کافه شوگریستا',
  address: 'تهران، خیابان ولیعصر، پلاک ۱۲',
  phone: '۰۲۱-۱۲۳۴۵۶۷',
  code: 'SG-K2B7PX',
  createdAtLabel: '۱۴۰۵/۰۸/۱۷ — ۱۸:۳۰',
  paidAtLabel: '۱۴۰۵/۰۸/۱۷ — ۱۸:۳۲',
  channelLabel: 'سفارش آنلاین',
  serviceLabel: 'سرو در محل',
  tableLabel: '۴',
  pickupAtLabel: '۱۴۰۵/۰۸/۱۷ — ۱۹:۰۰',
  customerName: 'علی رضایی',
  customerPhone: '۰۹۱۲۰۰۰۰۰۰۰',
  note: 'ممنوع‌اللاکتوز',
  paymentLabel: 'پرداخت آنلاین',
  reference: 'ref-88213',
  items: [
    {
      name: 'لاته',
      sizeLabel: 'بزرگ',
      quantity: 2,
      quantityLabel: '۲',
      unitPriceLabel: '۳۱۰٬۰۰۰',
      totalLabel: '۶۲۰٬۰۰۰',
      modifierLabels: ['شیر بادام'],
      options: [
        { optionName: '۱۰۰٪ عربیکا', priceDeltaLabel: '۱۲۰٬۰۰۰' },
      ],
      customDrinkSummary: [],
      payerLabel: null,
      baristaNote: 'تک‌شات',
    },
  ],
  totals: {
    subtotalLabel: '۶۲۰٬۰۰۰',
    deliveryFeeLabel: '۳۰٬۰۰۰',
    taxLabel: '۶۲٬۰۰۰',
    totalLabel: '۷۱۲٬۰۰۰',
    amountPaidLabel: '۳۵۶٬۰۰۰',
  },
}

const SAMPLE_BARISTA_ITEMS = [
  {
    name: 'لاته',
    sizeLabel: 'بزرگ',
    quantity: 2,
    quantityLabel: '۲',
    modifierLabels: ['شیر بادام'],
    options: [{ optionName: '۱۰۰٪ عربیکا', priceDeltaLabel: '۱۲۰٬۰۰۰' }],
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

function keyCatalog(index, catalog) {
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

function OrderedListEditor({
  title,
  keys,
  catalog,
  onChange,
  emptyNote,
}) {
  const present = new Set(keys)
  const remaining = catalog.filter((row) => !present.has(row.key))
  const labelOf = keyCatalog(0, catalog)

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
        <select value="" onChange={(event) => {
          const newKey = event.target.value
          if (newKey) onChange([...keys, newKey])
        }}>
          <option value="">+ افزودن سطر…</option>
          {remaining.map((row) => (
            <option key={row.key} value={row.key}>{row.label}</option>
          ))}
        </select>
      ) : null}
      {emptyNote && keys.length === 0 ? <p className="receipt-layout-hint">{emptyNote}</p> : null}
    </div>
  )
}

function FontSizeField({ label, value, onChange }) {
  return (
    <label className="receipt-layout-field receipt-layout-font">
      <span>{label}</span>
      <input
        type="number"
        min={12}
        max={90}
        value={value}
        onChange={(event) => onChange(Math.max(12, Math.min(90, Number(event.target.value) || 12)))}
      />
    </label>
  )
}

// ── Live preview ─────────────────────────────────────────────────────────────

function ReceiptPreview({ layout, copy }) {
  if (copy === 'customer') {
    return <CustomerPreview layout={layout} />
  }
  return <BaristaPreview layout={layout} />
}

function PreviewRow({ left, right, bold, big, small }) {
  return (
    <div className={`receipt-preview-row${bold ? ' bold' : ''}${big ? ' big' : ''}${small ? ' small' : ''}`}>
      <span>{right}</span>
      {left ? <em>{left}</em> : null}
    </div>
  )
}

function CustomerPreview({ layout }) {
  const o = SAMPLE_ORDER
  const { header, headerRows, items, totals, paymentRows, footer, fonts } = layout
  const scale = Math.max(0.62, Math.min(1.1, fonts.header / 40))

  const columns = items.columns.filter((column) => column.visible)
  const qtyVisible = columns.some((column) => column.key === 'qty')

  const item = o.items[0]
  const optionText = item.options
    .map((option) =>
      items.showOptionPrices && option.priceDeltaLabel
        ? `${option.optionName} (+${option.priceDeltaLabel})`
        : option.optionName,
    )

  const subLines = []
  for (const key of items.subLines) {
    if (key === 'size' && item.sizeLabel) subLines.push(item.sizeLabel)
    else if (key === 'options') subLines.push(...optionText)
    else if (key === 'modifiers') subLines.push(...item.modifierLabels)
    else if (key === 'customDrink' && item.customDrinkSummary?.length) subLines.push(...item.customDrinkSummary)
    else if (key === 'payer' && item.payerLabel) subLines.push(`سهم ${item.payerLabel}`)
    else if (key === 'baristaNote' && item.baristaNote) subLines.push(`! ${item.baristaNote}`)
  }

  return (
    <div className="receipt-preview-paper" style={{ fontSize: `${scale}rem` }}>
      <div className="receipt-preview-center bold big">{o.cafeName}</div>
      {header.showAddress ? <div className="receipt-preview-center small">{o.address}</div> : null}
      {header.showPhone ? <div className="receipt-preview-center small">تلفن: {o.phone}</div> : null}
      <div className="receipt-preview-divider double" />

      {headerRows.map((rowKey, rowIndex) => {
        const rowId = `${rowKey}-${rowIndex}`
        if (rowKey === 'code') return <PreviewRow key={rowId} right="کد سفارش" left={o.code} bold />
        if (rowKey === 'createdAt') return <PreviewRow key={rowId} right={o.createdAtLabel} />
        if (rowKey === 'channel') return <PreviewRow key={rowId} right={o.channelLabel} />
        if (rowKey === 'service') return <PreviewRow key={rowId} right={o.serviceLabel} />
        if (rowKey === 'table') return <PreviewRow key={rowId} right={`میز ${o.tableLabel}`} />
        if (rowKey === 'pickupTime') return <PreviewRow key={rowId} right="تحویل" left={o.pickupAtLabel} bold />
        if (rowKey === 'customerName') return <PreviewRow key={rowId} right={o.customerName} />
        if (rowKey === 'customerPhone') return <PreviewRow key={rowId} right={o.customerPhone} />
        if (rowKey === 'note') return <div key={rowId} className="receipt-preview-note">یادداشت: {o.note}</div>
        return null
      })}
      <div className="receipt-preview-divider" />

      {items.showHeaderRow && columns.some((column) => column.key === 'total' || column.key === 'unitPrice') ? (
        <div className="receipt-preview-cols-head">
          {columns.filter((column) => column.key === 'unitPrice' || column.key === 'total').map((column) => (
            <em key={column.key} style={column.widthPct ? { width: `${column.widthPct}%` } : undefined}>
              {column.label || (column.key === 'unitPrice' ? 'واحد' : 'جمع')}
            </em>
          ))}
        </div>
      ) : null}

      <div className="receipt-preview-item">
        <div className="receipt-preview-name">
          <strong>{qtyVisible ? `${item.quantityLabel} × ` : ''}{item.name}</strong>
          {columns.some((column) => column.key === 'total') ? (
            <em className="receipt-preview-item-total">{item.totalLabel}</em>
          ) : null}
        </div>
        {columns.some((column) => column.key === 'unitPrice') ? (
          <div className="receipt-preview-unit-price">{item.unitPriceLabel}</div>
        ) : null}
        {subLines.length > 0 ? (
          <div className="receipt-preview-sublines">{subLines.join(' • ')}</div>
        ) : null}
        {items.showUnitPriceNote && !columns.some((column) => column.key === 'unitPrice') && item.quantity > 1 ? (
          <div className="receipt-preview-sublines">واحد: {item.unitPriceLabel}</div>
        ) : null}
      </div>
      <div className="receipt-preview-divider" />

      {totals.rows.map((row) => {
        if (!row.visible) return null
        if (row.key === 'subtotal') return <PreviewRow key="subtotal" right={row.label || TOTAL_LABELS.subtotal} left={o.totals.subtotalLabel} />
        if (row.key === 'deliveryFee') return <PreviewRow key="deliveryFee" right={row.label || TOTAL_LABELS.deliveryFee} left={o.totals.deliveryFeeLabel} />
        if (row.key === 'tax') return <PreviewRow key="tax" right={row.label || TOTAL_LABELS.tax} left={o.totals.taxLabel} />
        if (row.key === 'total') return <PreviewRow key="total" right={row.label || TOTAL_LABELS.total} left={o.totals.totalLabel} bold big />
        return null
      })}

      {paymentRows.map((rowKey) => {
        if (rowKey === 'paymentMethod') return <PreviewRow key={rowKey} right="روش پرداخت" left={o.paymentLabel} />
        if (rowKey === 'reference') return <div key={rowKey} className="receipt-preview-note">شمارهٔ پیگیری: {o.reference}</div>
        if (rowKey === 'amountPaid') return <PreviewRow key={rowKey} right="پرداخت شده" left={o.totals.amountPaidLabel} small />
        return null
      })}

      <div className="receipt-preview-divider" />
      {footer.text ? <div className="receipt-preview-center small">{footer.text}</div> : null}
      {footer.showThanks ? <div className="receipt-preview-center bold">از انتخاب شما سپاسگزاریم</div> : null}
      {footer.showCafeName ? <div className="receipt-preview-center small">{o.cafeName}</div> : null}
      <p className="receipt-preview-hint">
        اندازه‌ها: تیتر {toPersianNumber(fonts.header)} • اطلاعات {toPersianNumber(fonts.info)} • آیتم {toPersianNumber(fonts.item)} • جمع {toPersianNumber(fonts.total)}
      </p>
    </div>
  )
}

function BaristaPreview({ layout }) {
  const o = SAMPLE_ORDER
  const { title, infoRows, items, showOrderNote, footer, fonts } = layout
  const item = SAMPLE_BARISTA_ITEMS[0]
  const qtyVisible = items.columns.some((column) => column.key === 'qty' && column.visible)

  const subLines = []
  for (const key of items.subLines) {
    if (key === 'size' && item.sizeLabel) subLines.push(item.sizeLabel)
    else if (key === 'modifiers') subLines.push(...item.modifierLabels)
    else if (key === 'options') subLines.push(...item.options.map((option) =>
      items.showOptionPrices && option.priceDeltaLabel
        ? `${option.optionName} (+${option.priceDeltaLabel})`
        : option.optionName,
    ))
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
        <strong>{qtyVisible ? `${item.quantityLabel} × ` : ''}{item.name}</strong>
        {subLines.length > 0 ? <div className="receipt-preview-sublines">{subLines.join(' • ')}</div> : null}
      </div>
      <div className="receipt-preview-divider" />
      {showOrderNote ? <div className="receipt-preview-note bold">یادداشت سفارش: {o.note}</div> : null}
      {footer.text ? (
        <div className="receipt-preview-center small">{footer.text}</div>
      ) : footer.showSign ? (
        <div className="receipt-preview-center small">تهیه و تحویل — شوگریستا</div>
      ) : null}
      <p className="receipt-preview-hint">آیتم {toPersianNumber(fonts.item)} • کد {toPersianNumber(fonts.code)} • تیتر {toPersianNumber(fonts.title)}</p>
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
                        onChange={(event) => update(index, {
                          widthPct: event.target.value === '' ? null : Math.max(15, Math.min(90, Number(event.target.value) || 15)),
                        })}
                      />
                    ) : null}
                  </>
                ) : null}
                <button
                  type="button"
                  title="انتقال به بالا"
                  disabled={index === 0}
                  onClick={() => onChange(moveAt(columns, index, index - 1))}
                >
                  ↑
                </button>
                <button
                  type="button"
                  title="انتقال به پایین"
                  disabled={index === columns.length - 1}
                  onClick={() => onChange(moveAt(columns, index, index + 1))}
                >
                  ↓
                </button>
                <button
                  type="button"
                  className={column.visible ? 'ok' : 'danger'}
                  title={column.visible ? 'برداشتن ستون از فیش' : 'نمایش ستون'}
                  disabled={!allowMoney ? '' : column.key === 'name' || column.key === 'total'}
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

function CustomerEditor({ layout, update }) {
  const { header, headerRows, items, totals, paymentRows, footer, fonts } = layout
  return (
    <div className="receipt-layout-editor">
      <h4>سربرگ فیش</h4>
      <div className="receipt-layout-grid">
        <ToggleField label="نمایش آدرس کافه" value={header.showAddress} onChange={(v) => update('customer.header.showAddress', v)} />
        <ToggleField label="نمایش تلفن کافه" value={header.showPhone} onChange={(v) => update('customer.header.showPhone', v)} />
      </div>
      <OrderedListEditor
        title="سطرهای اطلاعات سفارش (بالای فیش)"
        keys={headerRows}
        catalog={CUSTOMER_HEADER_ROWS}
        onChange={(keys) => update('customer.headerRows', keys)}
      />

      <h4>جدول آیتم‌ها</h4>
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
        <FontSizeField label="اندازهٔ آیتم‌ها" value={items.fontSize} onChange={(v) => update('customer.items.fontSize', v)} />
      </div>

      <h4>جمع و سطرهای پرداخت</h4>
      <div className="receipt-layout-totals">
        <p className="receipt-layout-ordered-title">ردیف‌های جمع</p>
        {totals.rows.map((row, index) => (
          <div key={row.key} className={`receipt-layout-total-row${row.visible ? '' : ' off'}`}>
            <input
              type="checkbox"
              checked={row.visible}
              disabled={row.key === 'total'}
              onChange={(event) => update(`customer.totals.rows.${index}.visible`, event.target.checked)}
            />
            <span>{TOTAL_LABELS[row.key]}</span>
            <input
              type="text"
              value={row.label ?? ''}
              placeholder={TOTAL_LABELS[row.key]}
              onChange={(event) => update(`customer.totals.rows.${index}.label`, event.target.value)}
            />
          </div>
        ))}
      </div>
      <OrderedListEditor
        title="سطرهای پرداخت (زیر جمع)"
        keys={paymentRows}
        catalog={CUSTOMER_PAYMENT_ROWS}
        onChange={(keys) => update('customer.paymentRows', keys)}
      />

      <h4>پانویس</h4>
      <div className="receipt-layout-grid">
        <label className="receipt-layout-field">
          <span>متن پانویس (خالی = متن صفحهٔ تنظیمات)</span>
          <input type="text" value={footer.text} onChange={(event) => update('customer.footer.text', event.target.value)} />
        </label>
        <ToggleField label="جملهٔ سپاسگزاری" value={footer.showThanks} onChange={(v) => update('customer.footer.showThanks', v)} />
        <ToggleField label="نام کافه در انتها" value={footer.showCafeName} onChange={(v) => update('customer.footer.showCafeName', v)} />
      </div>

      <h4>اندازهٔ فونت‌ها</h4>
      <div className="receipt-layout-grid">
        <FontSizeField label="تیتر (نام کافه)" value={fonts.header} onChange={(v) => update('customer.fonts.header', v)} />
        <FontSizeField label="اطلاعات" value={fonts.info} onChange={(v) => update('customer.fonts.info', v)} />
        <FontSizeField label="جمع اقلام" value={fonts.subtotal} onChange={(v) => update('customer.fonts.subtotal', v)} />
        <FontSizeField label="مبلغ نهایی" value={fonts.total} onChange={(v) => update('customer.fonts.total', v)} />
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
        <FontSizeField label="اندازهٔ آیتم‌ها" value={items.fontSize} onChange={(v) => update('barista.items.fontSize', v)} />
        <ToggleField label="یادداشت سفارش در انتها" value={showOrderNote} onChange={(v) => update('barista.showOrderNote', v)} />
      </div>
      <h4>پانویس و اندازه‌ها</h4>
      <div className="receipt-layout-grid">
        <label className="receipt-layout-field">
          <span>متن پانویس (خالی = امضای پیش‌فرض)</span>
          <input type="text" value={footer.text} onChange={(event) => update('barista.footer.text', event.target.value)} />
        </label>
        <ToggleField label="نمایش امضای پیش‌فرض" value={footer.showSign} onChange={(v) => update('barista.footer.showSign', v)} />
        <FontSizeField label="عنوان" value={fonts.title} onChange={(v) => update('barista.fonts.title', v)} />
        <FontSizeField label="کد سفارش" value={fonts.code} onChange={(v) => update('barista.fonts.code', v)} />
        <FontSizeField label="اطلاعات" value={fonts.info} onChange={(v) => update('barista.fonts.info', v)} />
      </div>
    </div>
  )
}

// ── Root panel ──────────────────────────────────────────────────────────────

export function ReceiptLayoutPanel({ session, apiRequest, getReadableError }) {
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
      setLayout({
        customer: { ...DEFAULT_LAYOUT.customer, ...data?.customer, items: { ...DEFAULT_LAYOUT.customer?.items, ...data?.customer?.items } },
        barista: { ...DEFAULT_LAYOUT.barista, ...data?.barista, items: { ...DEFAULT_LAYOUT.barista?.items, ...data?.barista?.items } },
      })
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
          return current // invalid path — never corrupt state
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

  const handleSave = async () => {
    if (!session?.token || !layout) return
    setSaving(true)
    setError('')
    setMessage('')
    try {
      await apiRequest('/admin/receipt-layout', { method: 'PUT', body: { customer: layout.customer, barista: layout.barista }, token: session.token })
      setMessage('طرح فیش ذخیره شد؛ از این پس روی چاپگر اعمال می‌شود.')
    } catch (requestError) {
      setError(getReadableError(requestError))
    } finally {
      setSaving(false)
    }
  }

  const handleReset = () => {
    setLayout(structuredClone(DEFAULT_LAYOUT))
    setMessage('پیش‌نمایش به طرح پیش‌فرض برگشت (برای اعمال، ذخیره کنید).')
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
            <h3>طرح فیش‌های چاپی</h3>
            <p>چارچوب ستون‌بندی، متن‌ها و اندازه‌های فیش مشتری (فاکتور) و فیش آشپزخانه را زنده ویرایش کنید.</p>
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
            {tab === 'customer'
              ? <CustomerEditor layout={layout?.customer ?? DEFAULT_LAYOUT.customer} update={update} />
              : <BaristaEditor layout={layout?.barista ?? DEFAULT_LAYOUT.barista} update={update} />}
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
            <p className="receipt-layout-ordered-title">پیش‌نمایش زنده</p>
            <ReceiptPreview layout={tab === 'customer' ? (layout?.customer ?? DEFAULT_LAYOUT.customer) : (layout?.barista ?? DEFAULT_LAYOUT.barista)} copy={tab} />
          </div>
        </div>
      ) : (
        <p className="feedback">{error || 'طرح فیش بارگذاری نشد.'}</p>
      )}
    </section>
  )
}
