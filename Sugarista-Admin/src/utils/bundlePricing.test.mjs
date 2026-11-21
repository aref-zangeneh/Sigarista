import assert from 'node:assert/strict'
import test from 'node:test'
import {
  computeDiscountedBundlePrice,
  evaluateBundleDraft,
  formatBundleSummaryLabels,
  getAvailableSizesForOption,
  resolveComponentRealPrice,
} from './bundlePricing.js'

test('evaluateBundleDraft computes exact real total, savings, and percent for 200k + 300k at 430k', () => {
  const americano = { id: 'a1', persianName: 'آمریکانو', englishName: 'Americano', effectivePrice: 200000, isActive: true }
  const cheesecake = { id: 'c1', persianName: 'چیزکیک سن‌سباستین', englishName: 'San Sebastian', effectivePrice: 300000, isActive: true }
  const optionsById = new Map([
    [americano.id, americano],
    [cheesecake.id, cheesecake],
  ])
  const costByComponentId = new Map([
    [americano.id, 45000],
    [cheesecake.id, 110000],
  ])

  const result = evaluateBundleDraft({
    components: [
      { componentMenuItemId: 'a1', quantity: 1, sizeKey: null },
      { componentMenuItemId: 'c1', quantity: 1, sizeKey: null },
    ],
    optionsById,
    costByComponentId,
    bundlePrice: 430000,
  })

  assert.equal(result.regularTotal, 500000)
  assert.equal(result.bundlePrice, 430000)
  assert.equal(result.savingsAmount, 70000)
  assert.equal(result.savingsPercent, 14)
  assert.equal(result.totalCost, 155000)
  assert.equal(result.estimatedProfit, 275000)
  assert.equal(result.warnings.length, 0)
})

test('evaluateBundleDraft warns when bundle price is not cheaper than standalone total or below COGS', () => {
  const item = { id: 'a1', persianName: 'لاته', effectivePrice: 200000, isActive: true }
  const optionsById = new Map([[item.id, item]])
  const costByComponentId = new Map([[item.id, 180000]])

  const overpriced = evaluateBundleDraft({
    components: [{ componentMenuItemId: 'a1', quantity: 1 }],
    optionsById,
    costByComponentId,
    bundlePrice: 210000,
  })
  assert.equal(overpriced.savingsAmount, 0)
  assert.ok(overpriced.warnings.some((w) => w.includes('کمتر نیست')))

  const belowCost = evaluateBundleDraft({
    components: [{ componentMenuItemId: 'a1', quantity: 1 }],
    optionsById,
    costByComponentId,
    bundlePrice: 150000,
  })
  assert.ok(belowCost.warnings.some((w) => w.includes('زیان')))
})

test('resolveComponentRealPrice and getAvailableSizesForOption pick requested variant', () => {
  const drink = {
    id: 'd1',
    persianName: 'لاته',
    smallPrice: 170000,
    smallName: 'کوچک',
    mediumPrice: 200000,
    mediumName: 'متوسط',
    largePrice: 240000,
    largeName: 'بزرگ',
  }

  assert.equal(getAvailableSizesForOption(drink).length, 3)
  const large = resolveComponentRealPrice(drink, 'large')
  assert.equal(large.unitPrice, 240000)
  assert.equal(large.sizeLabel, 'بزرگ')
})

test('computeDiscountedBundlePrice rounds cleanly to 1,000 Toman', () => {
  assert.equal(computeDiscountedBundlePrice(500000, 14), 430000)
  assert.equal(computeDiscountedBundlePrice(500000, 15), 425000)
  assert.equal(computeDiscountedBundlePrice(475000, 10), 427000)
})

test('formatBundleSummaryLabels multiplies component quantity by order line quantity', () => {
  const labels = formatBundleSummaryLabels(
    [
      { persianName: 'آمریکانو', quantity: 1, sizeLabel: 'بزرگ' },
      { persianName: 'چیزکیک سن‌سباستین', quantity: 1, sizeLabel: null },
    ],
    2,
  )
  assert.deepEqual(labels, [
    'شامل: 2 × آمریکانو (بزرگ)',
    'شامل: 2 × چیزکیک سن‌سباستین',
  ])
})
