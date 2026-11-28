/**
 * Realistic Iranian Bank Card presets & BIN (first-6-digit) auto-detection.
 * Supports Persian, Arabic, and English digits seamlessly.
 */

export const IRANIAN_BANKS = [
  {
    key: 'saman',
    name: 'بانک سامان',
    englishName: 'SAMAN BANK',
    bins: ['621986'],
    themeClass: 'bc-bank-saman',
    chipTone: 'gold',
    accentColor: '#38bdf8',
    pattern: 'waves',
  },
  {
    key: 'blu',
    name: 'بلوبانک سامان',
    englishName: 'blu',
    bins: [], // Manual selection (shares Saman BIN 621986)
    themeClass: 'bc-bank-blu',
    chipTone: 'silver',
    accentColor: '#60a5fa',
    pattern: 'minimal',
  },
  {
    key: 'mellat',
    name: 'بانک ملت',
    englishName: 'BANK MELLAT',
    bins: ['610433', '991975'],
    themeClass: 'bc-bank-mellat',
    chipTone: 'gold',
    accentColor: '#fbbf24',
    pattern: 'diamonds',
  },
  {
    key: 'melli',
    name: 'بانک ملی ایران',
    englishName: 'BANK MELLI IRAN',
    bins: ['603799'],
    themeClass: 'bc-bank-melli',
    chipTone: 'gold',
    accentColor: '#eab308',
    pattern: 'arches',
  },
  {
    key: 'pasargad',
    name: 'بانک پاسارگاد',
    englishName: 'BANK PASARGAD',
    bins: ['502229', '639347'],
    themeClass: 'bc-bank-pasargad',
    chipTone: 'gold',
    accentColor: '#f59e0b',
    pattern: 'sunburst',
  },
  {
    key: 'parsian',
    name: 'بانک پارسیان',
    englishName: 'PARSIAN BANK',
    bins: ['622106', '639194', '627884'],
    themeClass: 'bc-bank-parsian',
    chipTone: 'gold',
    accentColor: '#fb923c',
    pattern: 'rings',
  },
  {
    key: 'tejarat',
    name: 'بانک تجارت',
    englishName: 'TEJARAT BANK',
    bins: ['627353', '585983'],
    themeClass: 'bc-bank-tejarat',
    chipTone: 'silver',
    accentColor: '#60a5fa',
    pattern: 'diagonal',
  },
  {
    key: 'saderat',
    name: 'بانک صادرات ایران',
    englishName: 'BANK SADERAT',
    bins: ['603769'],
    themeClass: 'bc-bank-saderat',
    chipTone: 'gold',
    accentColor: '#818cf8',
    pattern: 'rings',
  },
  {
    key: 'sepah',
    name: 'بانک سپه',
    englishName: 'BANK SEPAH',
    bins: ['589210'],
    themeClass: 'bc-bank-sepah',
    chipTone: 'gold',
    accentColor: '#fbbf24',
    pattern: 'diagonal',
  },
  {
    key: 'keshavarzi',
    name: 'بانک کشاورزی',
    englishName: 'BANK KESHAVARZI',
    bins: ['603770', '639217'],
    themeClass: 'bc-bank-keshavarzi',
    chipTone: 'gold',
    accentColor: '#4ade80',
    pattern: 'waves',
  },
  {
    key: 'maskan',
    name: 'بانک مسکن',
    englishName: 'BANK MASKAN',
    bins: ['628023'],
    themeClass: 'bc-bank-maskan',
    chipTone: 'gold',
    accentColor: '#fdba74',
    pattern: 'diamonds',
  },
  {
    key: 'shahr',
    name: 'بانک شهر',
    englishName: 'SHAHR BANK',
    bins: ['502806', '504706'],
    themeClass: 'bc-bank-shahr',
    chipTone: 'silver',
    accentColor: '#fda4af',
    pattern: 'arches',
  },
  {
    key: 'ayandeh',
    name: 'بانک آینده',
    englishName: 'AYANDEH BANK',
    bins: ['636214'],
    themeClass: 'bc-bank-ayandeh',
    chipTone: 'gold',
    accentColor: '#f59e0b',
    pattern: 'sunburst',
  },
  {
    key: 'enbank',
    name: 'بانک اقتصاد نوین',
    englishName: 'EN BANK',
    bins: ['627412'],
    themeClass: 'bc-bank-enbank',
    chipTone: 'silver',
    accentColor: '#c084fc',
    pattern: 'diagonal',
  },
  {
    key: 'custom',
    name: 'سایر بانک‌ها (دلخواه)',
    englishName: 'SHETAB CARD',
    bins: [],
    themeClass: 'bc-theme-ocean',
    chipTone: 'gold',
    accentColor: '#38bdf8',
    pattern: 'waves',
  },
]

const PERSIAN_DIGITS = '۰۱۲۳۴۵۶۷۸۹'
const ARABIC_DIGITS = '٠١٢٣٤٥٦٧٨٩'

export function normalizeCardDigits(raw) {
  return String(raw ?? '')
    .replace(/[۰-۹]/g, (c) => String(PERSIAN_DIGITS.indexOf(c)))
    .replace(/[٠-٩]/g, (c) => String(ARABIC_DIGITS.indexOf(c)))
    .replace(/\D/g, '')
    .slice(0, 16)
}

export function formatCardInputDisplay(raw) {
  const digits = normalizeCardDigits(raw)
  return digits.replace(/(\d{4})(?=\d)/g, '$1 - ')
}

export function formatCardNumberGroups(raw) {
  const digits = normalizeCardDigits(raw)
  if (!digits) return '••••   ••••   ••••   ••••'
  const padded = digits.padEnd(16, '•')
  return `${padded.slice(0, 4)}   ${padded.slice(4, 8)}   ${padded.slice(8, 12)}   ${padded.slice(12, 16)}`
}

export function detectBankByCardNumber(rawCardNumber) {
  const digits = normalizeCardDigits(rawCardNumber)
  if (digits.length < 6) return null
  const bin = digits.slice(0, 6)
  return IRANIAN_BANKS.find((b) => b.bins.includes(bin)) || null
}

export function resolveBankPreset(card) {
  const themeKey = String(card?.cardTheme || '').trim().toLowerCase()
  const byKey = IRANIAN_BANKS.find((b) => b.key === themeKey)
  if (byKey && byKey.key !== 'custom') return byKey

  const byName = IRANIAN_BANKS.find(
    (b) => b.key !== 'custom' && card?.bankName && card.bankName.includes(b.name.replace('بانک ', '')),
  )
  if (byName) return byName

  const byBin = detectBankByCardNumber(card?.cardNumber)
  if (byBin) return byBin

  return IRANIAN_BANKS[IRANIAN_BANKS.length - 1]
}
