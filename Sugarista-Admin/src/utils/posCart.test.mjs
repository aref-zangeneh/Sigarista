import assert from 'node:assert/strict'
import test from 'node:test'
import {
  buildCartLineKey,
  buildUpsertPayload,
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
