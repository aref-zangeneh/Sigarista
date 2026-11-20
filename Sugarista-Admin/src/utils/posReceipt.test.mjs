import test from 'node:test'
import assert from 'node:assert/strict'
import { buildPosReceiptBundle } from './posReceipt.js'
import { buildUpsertPayload } from './posCart.js'

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
