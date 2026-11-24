import assert from 'node:assert/strict'
import test from 'node:test'
import {
  buildCartLineKey,
  buildUpsertPayload,
  computeCustomerClubPreview,
  computeSplitPaymentSummary,
  findMissingRequiredOptionGroup,
  orderToCart,
} from './posCart.js'

const americanoItem = {
  id: 'item-americano',
  persianName: 'آمریکانو',
  imageUrl: null,
  taxPercent: 0,
  optionGroups: [
    {
      id: 'grp-water',
      persianName: 'مقدار آب',
      isRequired: true,
      selectionMode: 'Single',
      minSelections: 1,
      maxSelections: 1,
      displayOrder: 0,
      options: [
        { id: 'opt-100ml', persianName: '۱۰۰ میل آب', priceDelta: 0, isDefault: false, isAvailable: false },
        { id: 'opt-200ml', persianName: '۲۰۰ میل آب', priceDelta: 0, isDefault: false, isAvailable: false },
      ],
    },
    {
      id: 'grp-syrup',
      persianName: 'سیروپ دلخواه',
      isRequired: false,
      selectionMode: 'Multi',
      minSelections: 0,
      maxSelections: 2,
      displayOrder: 1,
      options: [
        { id: 'opt-hazelnut', persianName: 'سیروپ فندوق', priceDelta: 35000, isDefault: false, isAvailable: true },
      ],
    },
  ],
}

test('findMissingRequiredOptionGroup catches missing required options and passes once selected', () => {
  const missing = findMissingRequiredOptionGroup(americanoItem, [])
  assert.ok(missing)
  assert.equal(missing.group.persianName, 'مقدار آب')
  assert.equal(missing.min, 1)

  assert.equal(findMissingRequiredOptionGroup(americanoItem, ['opt-100ml']), null)
  assert.equal(findMissingRequiredOptionGroup(americanoItem, ['opt-200ml', 'opt-hazelnut']), null)
})

test('buildCartLineKey distinguishes identical items with different selected options', () => {
  const k1 = buildCartLineKey('item-americano', null, [], ['opt-100ml'])
  const k2 = buildCartLineKey('item-americano', null, [], ['opt-200ml'])
  assert.notEqual(k1, k2)
})

test('orderToCart and buildUpsertPayload preserve optionIds and resolved option metadata', () => {
  const order = {
    lines: [
      {
        menuItemId: 'item-americano',
        persianName: 'آمریکانو',
        sizeKey: null,
        sizeLabel: null,
        quantity: 2,
        unitPrice: 125000,
        taxPercent: 0,
        modifierIds: [],
        modifierLabels: [],
        optionIds: ['opt-100ml', 'opt-hazelnut'],
        optionLabels: ['۱۰۰ میل آب', 'سیروپ فندوق'],
        baristaNote: '',
      },
    ],
  }

  const cart = orderToCart(order, [americanoItem])
  assert.equal(cart.length, 1)
  assert.deepEqual(cart[0].optionIds, ['opt-100ml', 'opt-hazelnut'])
  assert.equal(cart[0].options.length, 2)
  assert.equal(cart[0].options[0].optionName, '۱۰۰ میل آب')
  assert.equal(cart[0].options[1].priceDelta, 35000)

  const payload = buildUpsertPayload({
    serviceType: 'DineIn',
    cart,
    action: 'PayCash',
  })
  assert.deepEqual(payload.items[0].optionIds, ['opt-100ml', 'opt-hazelnut'])
})

test('computeSplitPaymentSummary handles Cash + CardToCard + Card combinations and Persian digits', () => {
  const partial = computeSplitPaymentSummary(255000, {
    Cash: '۱۰۰۰۰۰',
    CardToCard: '',
    Card: '',
  })
  assert.equal(partial.isExactMatch, false)
  assert.equal(partial.remainingAmount, 155000)
  assert.equal(partial.excessAmount, 0)

  const exact = computeSplitPaymentSummary(255000, {
    Cash: '۱۰۰۰۰۰',
    Card: '',
    CardToCard: '155000',
    cardToCardReference: 'TRK-8899',
  })
  assert.equal(exact.isExactMatch, true)
  assert.equal(exact.remainingAmount, 0)
  assert.equal(exact.excessAmount, 0)
  assert.equal(exact.effectiveMethod, 'Split')
  assert.equal(exact.legs.length, 2)
  assert.deepEqual(exact.legs[0], { method: 'Cash', label: 'نقدی', amount: 100000, reference: null })
  assert.deepEqual(exact.legs[1], {
    method: 'CardToCard',
    label: 'کارت به کارت',
    amount: 155000,
    reference: 'TRK-8899',
  })

  const excess = computeSplitPaymentSummary(200000, {
    Cash: '120000',
    Card: '100000',
  })
  assert.equal(excess.isExactMatch, false)
  assert.equal(excess.excessAmount, 20000)
})

test('computeCustomerClubPreview stacks loyalty points, stamp reward, tier, birthday, group, and daily cap', () => {
  const preview = computeCustomerClubPreview({
    itemsTotal: 300000,
    grandTotalBeforeDiscounts: 300000,
    customerLookup: {
      exists: true,
      loyaltyEnabled: true,
      loyaltyPoints: 50,
      loyaltyRedeemTomanPerPoint: 1000,
      hasActiveStampReward: true,
      stampRewardPercent: 10,
      membershipTierName: 'طلایی',
      membershipTierDiscountPercent: 5,
      isBirthdayDiscountActive: false,
      birthdayDiscountPercent: 0,
      activeGroupName: 'VIP',
      activeGroupDiscountPercent: 10,
      remainingDailyDiscountAllowance: 100000,
    },
    personalCup: false,
    redeemPoints: 50,
    discountCode: '',
  })

  // 50,000 loyalty + 30,000 stamp + 15,000 tier + 5,000 group (clamped at 100,000 daily cap)
  assert.equal(preview.totalClubDiscount, 100000)
  assert.equal(preview.afterClubTotal, 200000)
  assert.equal(preview.cappedRedeemPoints, 50)
  assert.equal(preview.breakdown.length, 4)
})

test('buildUpsertPayload builds PayCardToCard, PaySplit allocations, club fields, and confidential managerNote', () => {
  const cart = [{ menuItemId: 'item-1', sizeKey: null, quantity: 1, modifierIds: [], optionIds: [] }]

  const splitPayload = buildUpsertPayload({
    orderId: null,
    customerName: 'سارا محمدی',
    customerPhone: '09123334455',
    note: 'کم شیرین',
    serviceType: 'DineIn',
    salonTableId: 'tbl-1',
    cart,
    action: 'PaySplit',
    paymentAllocations: [
      { method: 'Cash', amount: 100000, reference: null },
      { method: 'CardToCard', amount: 150000, reference: '9988' },
    ],
    discountCode: 'CAFE10',
    redeemPoints: 25,
    managerNote: '  مشتری ویژه مدیریت  ',
  })

  assert.equal(splitPayload.action, 'PaySplit')
  assert.equal(splitPayload.customerPhone, '09123334455')
  assert.equal(splitPayload.discountCode, 'CAFE10')
  assert.equal(splitPayload.redeemPoints, 25)
  assert.equal(splitPayload.managerNote, 'مشتری ویژه مدیریت')
  assert.deepEqual(splitPayload.paymentAllocations, [
    { method: 'Cash', amount: 100000, reference: null },
    { method: 'CardToCard', amount: 150000, reference: '9988' },
  ])

  // Non-manager omits managerNote completely so existing DB note is never overwritten
  const cashierPayload = buildUpsertPayload({
    serviceType: 'TakeAway',
    cart,
    action: 'PayCardToCard',
  })
  assert.equal(cashierPayload.action, 'PayCardToCard')
  assert.equal('managerNote' in cashierPayload, false)
})
