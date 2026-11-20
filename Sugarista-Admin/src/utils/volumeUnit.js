import { toEnglishDigits } from './persianDigits.js'

export const VOLUME_UNITS = [
  'میلی‌لیتر',
  'گرم',
  'لیتر',
  'کیلوگرم',
  'عدد',
  'برش',
  'شات',
  'اسکوپ',
  'اونس',
]

const FOOD_PATTERN =
  /کیک|دسر|شیرینی|کوکی|کروسان|بیکری|تارت|براونی|مافین|دونات|وافل|پنکیک|چیزکیک|تیرامیسو|صبحانه|ساندویچ|سالاد|پاستا|برگر|پیتزا|اسنک|بشقاب|خوراکی|باگل|(?:^|[\s\-،/()])(?:پای|نان|تست|غذا)(?:$|[\s\-،/()])|\b(?:cake|dessert|pastry|cookie|croissant|bakery|sandwich|salad|breakfast|food|brownie|tart|pie|waffle|pancake|cheesecake|tiramisu)\b/i

const UNIT_ALIASES = {
  ml: 'میلی‌لیتر',
  cc: 'میلی‌لیتر',
  میل: 'میلی‌لیتر',
  'میلی لیتر': 'میلی‌لیتر',
  'میلی‌لیتر': 'میلی‌لیتر',
  'سی سی': 'میلی‌لیتر',
  'سی‌سی': 'میلی‌لیتر',
  g: 'گرم',
  gr: 'گرم',
  gm: 'گرم',
  gram: 'گرم',
  grams: 'گرم',
  گرم: 'گرم',
  kg: 'کیلوگرم',
  kilo: 'کیلوگرم',
  kilogram: 'کیلوگرم',
  کیلو: 'کیلوگرم',
  کیلوگرم: 'کیلوگرم',
  l: 'لیتر',
  lt: 'لیتر',
  ltr: 'لیتر',
  liter: 'لیتر',
  litre: 'لیتر',
  لیتر: 'لیتر',
  oz: 'اونس',
  اونس: 'اونس',
  pc: 'عدد',
  pcs: 'عدد',
  piece: 'عدد',
  pieces: 'عدد',
  عدد: 'عدد',
  shot: 'شات',
  shots: 'شات',
  شات: 'شات',
  scoop: 'اسکوپ',
  scoops: 'اسکوپ',
  اسکوپ: 'اسکوپ',
  slice: 'برش',
  slices: 'برش',
  برش: 'برش',
}

export function inferDefaultVolumeUnit(contextText = '') {
  return FOOD_PATTERN.test(String(contextText ?? '')) ? 'گرم' : 'میلی‌لیتر'
}

export function parseVolumeValue(rawVolume, fallbackUnit = 'میلی‌لیتر') {
  const trimmed = String(rawVolume ?? '').trim()
  if (!trimmed) {
    return { amount: '', unit: fallbackUnit }
  }

  const normalized = toEnglishDigits(trimmed).replace(/٫/g, '.')
  const match = normalized.match(/^(\d+(?:[.,]\d+)?)\s*(.*)$/)
  if (!match) {
    return { amount: normalized, unit: fallbackUnit }
  }

  const amount = match[1].replace(',', '.')
  const rawUnit = (match[2] || '').trim()
  if (!rawUnit) {
    return { amount, unit: fallbackUnit }
  }

  const key = rawUnit.toLowerCase().replace(/\./g, '').replace(/\s+/g, ' ')
  const mappedUnit = UNIT_ALIASES[key] ?? (VOLUME_UNITS.includes(rawUnit) ? rawUnit : fallbackUnit)
  return { amount, unit: mappedUnit }
}

export function formatVolumeValue(amount, unit) {
  const cleanedAmount = toEnglishDigits(String(amount ?? '')).trim()
  if (!cleanedAmount) {
    return ''
  }

  const resolvedUnit = unit || 'میلی‌لیتر'
  return `${cleanedAmount} ${resolvedUnit}`
}
