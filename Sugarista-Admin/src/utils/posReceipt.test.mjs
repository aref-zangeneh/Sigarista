import test from 'node:test'
import assert from 'node:assert/strict'
import { buildCustomerReceiptHtml, buildPosReceiptBundle, formatCustomerReceiptText } from './posReceipt.js'
import { buildUpsertPayload, computeManualDiscount } from './posCart.js'
import { buildQrSvg, encodeQrMatrix } from './qrSvg.js'

test('buildUpsertPayload preserves InternalUse and Complimentary order kinds and disables discounts', () => {
  const cart = [
    {
      menuItemId: 'item-1',
      sizeKey: null,
      quantity: 2,
      modifierIds: [],
      optionIds: [],
      baristaNote: '',
    },
  ]

  const internalPayload = buildUpsertPayload({
    orderId: null,
    customerName: 'پرسنل',
    customerPhone: '',
    note: '',
    serviceType: 'DineIn',
    salonTableId: null,
    deliveryMethodId: null,
    cart,
    action: 'InternalUse',
    splitBill: false,
    personalCup: true,
    useSubscription: true,
    orderKind: 'InternalUse',
    complimentaryReason: 'شیفت صبح باریستا',
    excludeFromAccounting: true,
  })

  assert.equal(internalPayload.action, 'InternalUse')
  assert.equal(internalPayload.orderKind, 'InternalUse')
  assert.equal(internalPayload.complimentaryReason, 'شیفت صبح باریستا')
  assert.equal(internalPayload.excludeFromAccounting, true)
  assert.equal(internalPayload.personalCup, false)
  assert.equal(internalPayload.useSubscription, false)
})

test('buildPosReceiptBundle formats InternalUse and Complimentary receipts with zero total and clear labels', () => {
  const cart = [
    {
      name: 'لاته',
      quantity: 1,
      unitPrice: 180000,
      modifierLabels: [],
      options: [],
    },
  ]

  const internalBundle = buildPosReceiptBundle({
    result: {
      trackingCode: 'SG-INT01',
      totalAmount: 0,
      paymentMethod: 'Complimentary',
      orderKind: 'InternalUse',
      complimentaryReason: 'تست عصاره‌گیری',
    },
    cart,
    serviceType: 'DineIn',
    siteSettings: { cafeName: 'شوگریستا' },
  })

  assert.equal(internalBundle.customer.totalAmount, 0)
  assert.equal(internalBundle.customer.paymentMethod, 'مصرف داخلی پرسنل (رایگان)')
  assert.match(internalBundle.customer.note, /تست عصاره‌گیری/)

  const compBundle = buildPosReceiptBundle({
    result: {
      trackingCode: 'SG-CMP01',
      totalAmount: 0,
      paymentMethod: 'Complimentary',
      orderKind: 'Complimentary',
      complimentaryReason: 'مهمان مدیریت',
    },
    cart,
    serviceType: 'TakeAway',
    siteSettings: { cafeName: 'شوگریستا' },
  })

  assert.equal(compBundle.customer.totalAmount, 0)
  assert.equal(compBundle.customer.paymentMethod, 'مهمان مجموعه / آشنا (رایگان)')
  assert.match(compBundle.customer.note, /مهمان مدیریت/)
})

test('buildPosReceiptBundle includes bundle sub-items for barista and customer thermal receipts', () => {
  const bundleReceipt = buildPosReceiptBundle({
    result: {
      trackingCode: 'SG-BND01',
      totalAmount: 430000,
      paymentMethod: 'Card',
    },
    cart: [
      {
        name: 'باندل صبحانه کافه',
        quantity: 2,
        unitPrice: 430000,
        modifierLabels: [],
        options: [],
        isBundle: true,
        bundleItems: [
          { persianName: 'آمریکانو', quantity: 1, sizeLabel: null, unitPrice: 200000 },
          { persianName: 'چیزکیک سن‌سباستین', quantity: 1, sizeLabel: null, unitPrice: 300000 },
        ],
      },
    ],
    serviceType: 'DineIn',
    siteSettings: { cafeName: 'شوگریستا' },
  })

  const summary = bundleReceipt.renderPayload.items[0].customDrinkSummary
  assert.equal(summary.length, 2)
  assert.match(summary[0], /شامل: ۲ × آمریکانو/)
  assert.match(summary[1], /شامل: ۲ × چیزکیک سن‌سباستین/)
})

test('computeManualDiscount handles fixed amount, percentage, Persian digits, and limit clamping', () => {
  const fixed = computeManualDiscount(250000, 'Amount', '۵۰۰۰۰')
  assert.equal(fixed.amount, 50000)
  assert.equal(fixed.finalTotal, 200000)
  assert.equal(fixed.exceedsLimit, false)

  const pct = computeManualDiscount(250000, 'Percent', '۱۰')
  assert.equal(pct.amount, 25000)
  assert.equal(pct.finalTotal, 225000)
  assert.equal(pct.exceedsLimit, false)

  const overPct = computeManualDiscount(250000, 'Percent', '120')
  assert.equal(overPct.exceedsLimit, true)
  assert.equal(overPct.amount, 250000)
  assert.equal(overPct.finalTotal, 0)

  const overAmt = computeManualDiscount(100000, 'Amount', '150000')
  assert.equal(overAmt.exceedsLimit, true)
  assert.equal(overAmt.amount, 100000)
  assert.equal(overAmt.finalTotal, 0)
})

test('buildUpsertPayload includes manual discount for Standard orders and strips it for non-revenue orders', () => {
  const cart = [{ menuItemId: 'item-1', sizeKey: null, quantity: 1, modifierIds: [], optionIds: [] }]

  const std = buildUpsertPayload({
    orderId: null,
    customerName: 'علی',
    customerPhone: '',
    note: '',
    serviceType: 'DineIn',
    salonTableId: null,
    deliveryMethodId: null,
    cart,
    action: 'PayCard',
    splitBill: false,
    orderKind: 'Standard',
    manualDiscountType: 'Percent',
    manualDiscountValue: '۱۵',
    manualDiscountReason: 'مشتری وفادار',
  })
  assert.equal(std.manualDiscountType, 'Percent')
  assert.equal(std.manualDiscountValue, 15)
  assert.equal(std.manualDiscountReason, 'مشتری وفادار')

  const comp = buildUpsertPayload({
    orderId: null,
    customerName: 'مهمان',
    customerPhone: '',
    note: '',
    serviceType: 'DineIn',
    salonTableId: null,
    deliveryMethodId: null,
    cart,
    action: 'Complimentary',
    splitBill: false,
    orderKind: 'Complimentary',
    manualDiscountType: 'Percent',
    manualDiscountValue: '10',
    manualDiscountReason: 'نباید ارسال شود',
  })
  assert.equal(comp.manualDiscountType, null)
  assert.equal(comp.manualDiscountValue, null)
  assert.equal(comp.manualDiscountReason, null)
})

test('buildPosReceiptBundle renders manual POS discount on customer receipt, HTML, text, and print payload', () => {
  const bundle = buildPosReceiptBundle({
    result: {
      trackingCode: 'SG-DSC01',
      totalAmount: 180000,
      paymentMethod: 'Card',
      discountTotal: 20000,
      manualDiscountType: 'Percent',
      manualDiscountValue: 10,
      manualDiscountAmount: 20000,
      manualDiscountReason: 'مشتری وفادار',
    },
    cart: [
      {
        name: 'لاته',
        quantity: 1,
        unitPrice: 200000,
        modifierLabels: [],
        options: [],
      },
    ],
    serviceType: 'DineIn',
    siteSettings: { cafeName: 'شوگریستا' },
  })

  assert.equal(bundle.customer.discountTotal, 20000)
  assert.equal(bundle.customer.discountTitle, 'تخفیف دلخواه (۱۰٪)')
  assert.equal(bundle.customer.totalAmount, 180000)
  assert.equal(bundle.renderPayload.totals.discountLabel, '۲۰٬۰۰۰')
  assert.equal(bundle.renderPayload.totals.discountTitle, 'تخفیف دلخواه (۱۰٪)')
  assert.equal(bundle.renderPayload.totals.discountNote, 'مشتری وفادار')

  const text = formatCustomerReceiptText(bundle.customer)
  assert.match(text, /تخفیف دلخواه \(۱۰٪\)/)
  assert.match(text, /مشتری وفادار/)

  const html = buildCustomerReceiptHtml(bundle.customer)
  assert.match(html, /تخفیف دلخواه \(۱۰٪\)/)
  assert.match(html, /مشتری وفادار/)
})

test('encodeQrMatrix and buildQrSvg generate valid scannable ISO QR SVG markup', () => {
  const matrix = encodeQrMatrix('https://sugarista.ir')
  assert.ok(Array.isArray(matrix))
  assert.ok(matrix.length >= 21 && (matrix.length - 17) % 4 === 0)
  // Top-left finder pattern 7x7 corners must be dark (true)
  assert.equal(matrix[0][0], true)
  assert.equal(matrix[0][6], true)
  assert.equal(matrix[6][0], true)
  assert.equal(matrix[6][6], true)

  const svg = buildQrSvg('https://sugarista.ir', 120)
  assert.match(svg, /^<svg\s/)
  assert.match(svg, /width="120"/)
  assert.match(svg, /<path d="/)

  assert.equal(buildQrSvg(''), '')
})

test('buildPosReceiptBundle and buildCustomerReceiptHtml honor dynamic customer receipt layout fields', () => {
  const bundle = buildPosReceiptBundle({
    result: {
      trackingCode: 'SG-DYN01',
      totalAmount: 180000,
      discountTotal: 20000,
      paymentMethod: 'Card',
    },
    cart: [
      {
        name: 'لاته',
        quantity: 2,
        unitPrice: 100000,
        modifierLabels: [],
        options: [],
      },
    ],
    serviceType: 'DineIn',
    siteSettings: {
      cafeName: 'نام قدیمی',
      address: 'آدرس قدیمی',
      contactPhone: '02100000000',
      receiptLayout: {
        customer: {
          header: {
            cafeName: 'کافه و شیرینی‌پزی شوگریستا',
            subtitle: 'Specialty Coffee & Bakery',
            showAddress: true,
            customAddress: 'تهران، بلوار اندرزگو، پلاک ۴۲',
            showPhone: true,
            customPhone: '021-22446688',
            taxId: '411122223333',
            showOrnament: true,
          },
          totals: {
            showItemCount: true,
            showSavingsBanner: true,
          },
          footer: {
            promoBanner: 'با ارائه این فیش ۱۰٪ تخفیف بگیرید',
            showWifi: true,
            wifiSsid: 'Sugarista_VIP',
            wifiPassword: 'latte2026',
            showQrCode: true,
            qrCodeUrl: 'https://sugarista.ir/menu',
            qrCodeLabel: 'اسکن برای منوی دیجیتال',
            instagramHandle: 'sugarista.cafe',
            websiteUrl: 'sugarista.ir',
            showThanks: true,
            thanksText: 'روزتان شیرین!',
            showCafeName: true,
            cafeNameText: 'شوگریستا — شعبه اندرزگو',
          },
          style: {
            dividerStyle: 'double',
            ornamentIcon: '✨',
            codeBoxStyle: 'solid',
          },
        },
      },
    },
  })

  assert.equal(bundle.customer.cafeName, 'کافه و شیرینی‌پزی شوگریستا')
  assert.equal(bundle.customer.footerCafeName, 'شوگریستا — شعبه اندرزگو')
  assert.equal(bundle.customer.title, 'Specialty Coffee & Bakery')
  assert.equal(bundle.customer.address, 'تهران، بلوار اندرزگو، پلاک ۴۲')
  assert.equal(bundle.customer.contactPhone, '021-22446688')
  assert.equal(bundle.customer.taxId, '411122223333')
  assert.equal(bundle.customer.instagramHandle, 'sugarista.cafe')
  assert.equal(bundle.customer.showWifi, true)
  assert.equal(bundle.customer.wifiSsid, 'Sugarista_VIP')
  assert.equal(bundle.customer.showQrCode, true)
  assert.equal(bundle.customer.qrCodeUrl, 'https://sugarista.ir/menu')
  assert.equal(bundle.renderPayload.header.cafeName, 'کافه و شیرینی‌پزی شوگریستا')
  assert.equal(bundle.renderPayload.header.address, 'تهران، بلوار اندرزگو، پلاک ۴۲')

  const html = buildCustomerReceiptHtml(bundle.customer)
  assert.match(html, /کافه و شیرینی‌پزی شوگریستا/)
  assert.match(html, /Specialty Coffee &amp; Bakery/)
  assert.match(html, /۴۱۱۱۲۲۲۲۳۳۳۳/)
  assert.match(html, /تعداد کل اقلام/)
  assert.match(html, /سود شما از این سفارش/)
  assert.match(html, /با ارائه این فیش ۱۰٪ تخفیف بگیرید/)
  assert.match(html, /Sugarista_VIP/)
  assert.match(html, /latte2026/)
  assert.match(html, /<svg\s/)
  assert.match(html, /اسکن برای منوی دیجیتال/)
  assert.match(html, /@sugarista\.cafe/)
  assert.match(html, /شوگریستا — شعبه اندرزگو/)
})
