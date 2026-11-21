import test from 'node:test'
import assert from 'node:assert/strict'
import { applyOptionGroupRuleChange } from './optionGroupRules.js'

test('toggling isRequired ON sets minSelections to 1 and toggling OFF resets minSelections to 0', () => {
  const base = { isRequired: false, selectionMode: 'Single', minSelections: 0, maxSelections: 1 }

  const on = applyOptionGroupRuleChange(base, { isRequired: true })
  assert.equal(on.isRequired, true)
  assert.equal(on.minSelections, 1)
  assert.equal(on.maxSelections, 1)

  const off = applyOptionGroupRuleChange(on, { isRequired: false })
  assert.equal(off.isRequired, false)
  assert.equal(off.minSelections, 0)
  assert.equal(off.maxSelections, 1)
})

test('stepping minSelections up/down keeps isRequired, maxSelections, and selectionMode in sync', () => {
  const base = { isRequired: false, selectionMode: 'Single', minSelections: 0, maxSelections: 1 }

  const min1 = applyOptionGroupRuleChange(base, { minSelections: 1 })
  assert.equal(min1.isRequired, true)
  assert.equal(min1.minSelections, 1)

  const min2 = applyOptionGroupRuleChange(min1, { minSelections: 2 })
  assert.equal(min2.isRequired, true)
  assert.equal(min2.maxSelections, 2)
  assert.equal(min2.selectionMode, 'Multi')

  const min0 = applyOptionGroupRuleChange(min2, { minSelections: 0 })
  assert.equal(min0.isRequired, false)
  assert.equal(min0.minSelections, 0)
})

test('stepping maxSelections above 1 switches Single to Multi and switching back to Single clamps max to 1', () => {
  const base = { isRequired: true, selectionMode: 'Single', minSelections: 1, maxSelections: 1 }

  const max3 = applyOptionGroupRuleChange(base, { maxSelections: 3 })
  assert.equal(max3.maxSelections, 3)
  assert.equal(max3.selectionMode, 'Multi')

  const single = applyOptionGroupRuleChange(max3, { selectionMode: 'Single' })
  assert.equal(single.selectionMode, 'Single')
  assert.equal(single.maxSelections, 1)
  assert.equal(single.minSelections, 1)
})
