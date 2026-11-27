import { toPersianDigits } from './persianDigits.js'

export const SERVING_TEMP_PRESETS = [
  { value: 'داغ', icon: '🔥', label: 'داغ' },
  { value: 'سرد', icon: '🧊', label: 'سرد / آیس' },
  { value: 'دمای محیط', icon: '🍰', label: 'دمای محیط' },
  { value: 'یخچالی', icon: '❄️', label: 'یخچالی' },
]

export function formatPrepDuration(seconds) {
  const sec = Math.floor(Number(seconds) || 0)
  if (sec <= 0) return ''
  if (sec < 60) return `${toPersianDigits(sec)} ثانیه`
  const mins = Math.floor(sec / 60)
  const rem = sec % 60
  if (rem === 0) return `${toPersianDigits(mins)} دقیقه`
  return `${toPersianDigits(mins)} دقیقه و ${toPersianDigits(rem)} ثانیه`
}

export function formatIngredientQty(quantity, unit) {
  const num = Number(quantity)
  if (!Number.isFinite(num)) return ''
  const formatted = Number.isInteger(num) ? String(num) : String(Number(num.toFixed(3)))
  return `${toPersianDigits(formatted)} ${unit || ''}`.trim()
}

export function normalizeBulletPoints(lines) {
  return (lines ?? [])
    .map((line) => String(line ?? '').trim())
    .filter((line) => line.length > 0)
    .slice(0, 25)
}

/**
 * Splits a legacy plain-text RecipeDescription into clean bullet lines when the user
 * clicks "تبدیل توضیحات قبلی به موارد شماره‌دار" (preserving existing data losslessly!).
 */
export function splitDescriptionToBullets(description) {
  if (!description) return []
  return String(description)
    .split(/\r?\n|[•·]+/)
    .map((part) => part.trim().replace(/^(?:[-–—*•]+\s*|[\d۰-۹]+[.)\-]\s*)/, '').trim())
    .filter((part) => part.length > 0)
    .slice(0, 25)
}

export function filterRecipeCards(cards = [], { categoryId = '', search = '', mode = 'all' } = {}) {
  const q = String(search || '').trim().toLowerCase()
  return (cards ?? []).filter((card) => {
    if (categoryId && card.categoryId !== categoryId) return false
    if (mode === 'has-card' && !card.hasCustomCard && !card.recipeDescription) return false
    if (mode === 'has-formula' && !(card.ingredients?.length > 0)) return false
    if (mode === 'missing' && (card.hasCustomCard || card.recipeDescription)) return false
    if (!q) return true

    return [
      card.persianName,
      card.englishName,
      card.categoryName,
      card.recipeDescription,
      card.baristaTip,
      card.glasswareOrPlate,
      ...(card.bulletPoints ?? []),
      ...(card.ingredients ?? []).map((i) => i.ingredientName),
    ]
      .filter(Boolean)
      .some((v) => String(v).toLowerCase().includes(q))
  })
}
