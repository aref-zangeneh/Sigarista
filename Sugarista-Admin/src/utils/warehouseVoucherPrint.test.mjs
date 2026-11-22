import test from 'node:test'
import assert from 'node:assert/strict'
import {
  buildWarehouseVoucherHtml,
  computeVoucherLinePreview,
  getReasonsForVoucherType,
} from './warehouseVoucherPrint.js'

test('getReasonsForVoucherType separates receipt and issue reasons cleanly', () => {
  const receiptReasons = getReasonsForVoucherType('Receipt').map((r) => r.value)
  const issueReasons = getReasonsForVoucherType('Issue').map((r) => r.value)

  assert.ok(receiptReasons.includes('Purchase'))
  assert.ok(!receiptReasons.includes('WastageOrSpoilage'))
  assert.ok(issueReasons.includes('WastageOrSpoilage'))
  assert.ok(!issueReasons.includes('Purchase'))
})

test('computeVoucherLinePreview computes lineTotal, post-stock, and insufficient stock warning with Persian digits', () => {
  const receipt = computeVoucherLinePreview('Receipt', 10, '۲.۵', '۴۰۰۰۰۰')
  assert.equal(receipt.qty, 2.5)
  assert.equal(receipt.lineTotal, 1000000)
  assert.equal(receipt.stockAfter, 12.5)
  assert.equal(receipt.insufficient, false)

  const okIssue = computeVoucherLinePreview('Issue', 10, '۴', '100000')
  assert.equal(okIssue.stockAfter, 6)
  assert.equal(okIssue.insufficient, false)

  const badIssue = computeVoucherLinePreview('Issue', 5, '۷.۵', '100000')
  assert.equal(badIssue.stockAfter, -2.5)
  assert.equal(badIssue.insufficient, true)
})

test('buildWarehouseVoucherHtml renders formal Persian GRN/GIV and cancelled watermark', () => {
  const receiptHtml = buildWarehouseVoucherHtml(
    {
      voucherNumber: 'GRN-20261122-0001',
      voucherType: 'Receipt',
      status: 'Posted',
      reasonLabel: 'خرید از تأمین‌کننده',
      warehouseName: 'انبار اصلی (موجودی مرکزی)',
      supplierName: 'پخش قهوه مرکزی',
      counterpartyName: 'آقای رضایی',
      externalReference: 'INV-554',
      voucherDate: '2026-11-22T10:00:00Z',
      totalAmount: 1500000,
      createdByName: 'مدیر انبار',
      note: 'تحویل کامل و سالم',
      lines: [
        {
          ingredientName: 'دانه قهوه عربیکا',
          ingredientEnglishName: 'Arabica Beans',
          unit: 'kg',
          quantity: 3,
          unitPrice: 500000,
          lineTotal: 1500000,
          stockBefore: 10,
          stockAfter: 13,
          batchNumber: 'LOT-99',
          expirationDate: '2027-06-01',
        },
      ],
    },
    { cafeName: 'شوگاریستا' },
  )

  assert.match(receiptHtml, /رسید ورود کالا به انبار/)
  assert.match(receiptHtml, /GRN-20261122-0001/)
  assert.match(receiptHtml, /دانه قهوه عربیکا/)
  assert.match(receiptHtml, /۱٬۵۰۰٬۰۰۰ تومان/)
  assert.doesNotMatch(receiptHtml, /class="watermark"/)

  const cancelledHtml = buildWarehouseVoucherHtml({
    voucherNumber: 'GIV-20261122-0002',
    voucherType: 'Issue',
    status: 'Cancelled',
    reasonLabel: 'ضایعات / فساد / انقضا',
    warehouseName: 'انبار بار',
    voucherDate: '2026-11-22T11:00:00Z',
    totalAmount: 200000,
    cancellationReason: 'ثبت اشتباه',
    cancelledByName: 'مدیر کل',
    lines: [],
  })

  assert.match(cancelledHtml, /سند \/ حواله خروج کالا از انبار/)
  assert.match(cancelledHtml, /class="watermark">ابطال‌شده/)
  assert.match(cancelledHtml, /ثبت اشتباه/)
})
