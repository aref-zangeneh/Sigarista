import assert from 'node:assert/strict'
import test from 'node:test'
import {
  normalizePayloadDigits,
  normalizeUrlQueryDigits,
  sanitizeNumericInput,
  toEnglishDigits,
  toPersianDigits,
  toPersianDisplayText,
} from './persianDigits.js'

test('toEnglishDigits converts Persian and Arabic-Indic digits safely', () => {
  assert.equal(toEnglishDigits('۰۱۲۳۴۵۶۷۸۹'), '0123456789')
  assert.equal(toEnglishDigits('٠١٢٣٤٥٦٧٨٩'), '0123456789')
  assert.equal(toEnglishDigits('ساعت ۰۸:۳۰ تا ۲۳:۰۰'), 'ساعت 08:30 تا 23:00')
  assert.equal(toEnglishDigits('plain-123'), 'plain-123')
})

test('toPersianDigits converts English and Arabic-Indic digits to Persian', () => {
  assert.equal(toPersianDigits(0), '۰')
  assert.equal(toPersianDigits(125000), '۱۲۵۰۰۰')
  assert.equal(toPersianDigits('09123456789'), '۰۹۱۲۳۴۵۶۷۸۹')
  assert.equal(toPersianDigits('٠٥.٢٥'), '۰۵.۲۵')
})

test('toPersianDisplayText converts visible digits while preserving URLs, emails, UUIDs, and hex colors', () => {
  assert.equal(
    toPersianDisplayText('سفارش #1042 در ساعت 08:30 ثبت شد'),
    'سفارش #۱۰۴۲ در ساعت ۰۸:۳۰ ثبت شد',
  )
  assert.equal(
    toPersianDisplayText('آدرس: https://cafe123.example.com/v2/item45 و رنگ #aa3171 و #000000'),
    'آدرس: https://cafe123.example.com/v2/item45 و رنگ #aa3171 و #000000',
  )
  assert.equal(
    toPersianDisplayText('ایمیل user2026@sugarista.ir برای 2 نفر'),
    'ایمیل user2026@sugarista.ir برای ۲ نفر',
  )
  assert.equal(
    toPersianDisplayText('کد 123e4567-e89b-12d3-a456-426614174000 و مبلغ 50000 تومان'),
    'کد 123e4567-e89b-12d3-a456-426614174000 و مبلغ ۵۰۰۰۰ تومان',
  )
})

test('sanitizeNumericInput handles EN/FA/AR digits, decimals, negatives, and caret mapping', () => {
  assert.deepEqual(
    sanitizeNumericInput('۱۲a۳٫۴.۵', { allowDecimal: true, allowNegative: false, caret: 7 }),
    { english: '123.45', display: '۱۲۳.۴۵', caret: 5 },
  )

  assert.deepEqual(
    sanitizeNumericInput('−۱۲.۵', { allowDecimal: true, allowNegative: true, caret: 5 }),
    { english: '-12.5', display: '-۱۲.۵', caret: 5 },
  )

  assert.deepEqual(
    sanitizeNumericInput('-۱۲.۵', { allowDecimal: false, allowNegative: false, caret: 4 }),
    { english: '125', display: '۱۲۵', caret: 2 },
  )
})

test('normalizePayloadDigits converts nested values to ASCII while skipping passwords and secrets', () => {
  const input = {
    phone: '۰۹۱۲۳۴۵۶۷۸۹',
    hours: '۰۷:۳۰ - ۲۳:۰۰',
    lines: [{ note: 'سفارش ۲ عدد', qty: '۲' }],
    password: 'Pass۱۲۳!',
    currentPassword: 'Old۴۵۶',
    newPassword: 'New۷۸۹',
    apiKey: 'key-۰۱۲',
    deliveryWebhookSecret: 'whsec-۹۹',
    extraSettingsJson: '{"pin":"۱۲۳"}',
    token: 'tok-۸۸',
  }

  const normalized = normalizePayloadDigits(input)
  assert.equal(normalized.phone, '09123456789')
  assert.equal(normalized.hours, '07:30 - 23:00')
  assert.equal(normalized.lines[0].note, 'سفارش 2 عدد')
  assert.equal(normalized.password, 'Pass۱۲۳!')
  assert.equal(normalized.currentPassword, 'Old۴۵۶')
  assert.equal(normalized.newPassword, 'New۷۸۹')
  assert.equal(normalized.apiKey, 'key-۰۱۲')
  assert.equal(normalized.deliveryWebhookSecret, 'whsec-۹۹')
  assert.equal(normalized.extraSettingsJson, '{"pin":"۱۲۳"}')
  assert.equal(normalized.token, 'tok-۸۸')
})

test('normalizeUrlQueryDigits normalizes query parameters cleanly', () => {
  const raw = '/admin/orders?page=%DB%B2&search=%DB%B0%DB%B9%DB%B1%DB%B2&token=%DB%B9%DB%B9'
  const normalized = normalizeUrlQueryDigits(raw)
  assert.equal(normalized, '/admin/orders?page=2&search=0912&token=%DB%B9%DB%B9')
})
