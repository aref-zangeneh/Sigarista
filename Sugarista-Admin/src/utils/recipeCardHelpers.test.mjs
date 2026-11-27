import test from 'node:test'
import assert from 'node:assert/strict'
import {
  filterRecipeCards,
  formatIngredientQty,
  formatPrepDuration,
  normalizeBulletPoints,
  splitDescriptionToBullets,
} from './recipeCardHelpers.js'

test('formatPrepDuration formats seconds, exact minutes, and mixed durations in Persian', () => {
  assert.equal(formatPrepDuration(0), '')
  assert.equal(formatPrepDuration(45), '۴۵ ثانیه')
  assert.equal(formatPrepDuration(120), '۲ دقیقه')
  assert.equal(formatPrepDuration(150), '۲ دقیقه و ۳۰ ثانیه')
})

test('formatIngredientQty formats integer and decimal quantities cleanly with Persian digits', () => {
  assert.equal(formatIngredientQty(18, 'گرم'), '۱۸ گرم')
  assert.equal(formatIngredientQty(0.25, 'لیتر'), '۰.۲۵ لیتر')
})

test('splitDescriptionToBullets converts legacy multiline descriptions losslessly', () => {
  const bullets = splitDescriptionToBullets('۱. ۴۵ میلی‌لیتر اسپرسو\n- ۱۵۰ میلی‌لیتر شیر سرد\n• یخ مکعبی')
  assert.deepEqual(bullets, ['۴۵ میلی‌لیتر اسپرسو', '۱۵۰ میلی‌لیتر شیر سرد', 'یخ مکعبی'])
})

test('normalizeBulletPoints trims blanks and preserves valid order', () => {
  assert.deepEqual(normalizeBulletPoints(['  مرحله اول ', '', '  ', 'مرحله دوم']), ['مرحله اول', 'مرحله دوم'])
})

test('filterRecipeCards filters by category, search (including ingredients), and mode', () => {
  const cards = [
    {
      menuItemId: '1',
      persianName: 'آیس لاته',
      englishName: 'Ice Latte',
      categoryId: 'coffee',
      categoryName: 'قهوه',
      hasCustomCard: true,
      recipeDescription: null,
      bulletPoints: ['اسپرسو دبل'],
      ingredients: [{ ingredientName: 'شیر تازه' }],
    },
    {
      menuItemId: '2',
      persianName: 'تیرامیسو',
      englishName: 'Tiramisu',
      categoryId: 'dessert',
      categoryName: 'دسر',
      hasCustomCard: false,
      recipeDescription: 'پخت روز',
      bulletPoints: [],
      ingredients: [],
    },
  ]

  assert.equal(filterRecipeCards(cards, { search: 'شیر' }).length, 1)
  assert.equal(filterRecipeCards(cards, { categoryId: 'dessert' }).length, 1)
  assert.equal(filterRecipeCards(cards, { mode: 'has-formula' }).length, 1)
  assert.equal(filterRecipeCards(cards, { mode: 'has-card' }).length, 2)
})
