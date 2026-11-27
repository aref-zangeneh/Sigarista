import test from 'node:test'
import assert from 'node:assert/strict'
import {
  buildWalletsQueryString,
  buildWalletTransactionsQueryString,
  computeBalancePreview,
  getWalletTxMeta,
  normalizeDigits,
  parseWalletAmount,
} from './walletHelpers.js'

test('normalizeDigits converts Persian and Arabic digits cleanly', () => {
  assert.equal(normalizeDigits('۰۹۱۲۳۴۵۶۷۸۹'), '09123456789')
  assert.equal(normalizeDigits('١٢٥٠٠٠'), '125000')
  assert.equal(normalizeDigits('علی 123'), 'علی 123')
})

test('parseWalletAmount parses Persian digits, commas, and rejects invalid/zero', () => {
  assert.equal(parseWalletAmount('۱۵۰,۰۰۰'), 150000)
  assert.equal(parseWalletAmount('50٬000'), 50000)
  assert.equal(parseWalletAmount('250000'), 250000)
  assert.equal(parseWalletAmount('0'), null)
  assert.equal(parseWalletAmount('-5000'), null)
  assert.equal(parseWalletAmount('abc'), null)
  assert.equal(parseWalletAmount(''), null)
})

test('computeBalancePreview computes charge and debit with overdraft guard', () => {
  const charge = computeBalancePreview(100_000, '۵۰,۰۰۰', 'charge')
  assert.deepEqual(charge, {
    amount: 50_000,
    signedAmount: 50_000,
    nextBalance: 150_000,
    isOverdraft: false,
    isValid: true,
  })

  const validDebit = computeBalancePreview(100_000, '40000', 'debit')
  assert.deepEqual(validDebit, {
    amount: 40_000,
    signedAmount: -40_000,
    nextBalance: 60_000,
    isOverdraft: false,
    isValid: true,
  })

  const overdraft = computeBalancePreview(100_000, '120000', 'debit')
  assert.equal(overdraft.isOverdraft, true)
  assert.equal(overdraft.isValid, false)
  assert.equal(overdraft.nextBalance, 0)
  assert.equal(overdraft.signedAmount, -120_000)

  const invalid = computeBalancePreview(100_000, '', 'charge')
  assert.equal(invalid.isValid, false)
  assert.equal(invalid.nextBalance, 100_000)
})

test('getWalletTxMeta resolves both string and numeric transaction types', () => {
  assert.equal(getWalletTxMeta('TopUp').label, 'شارژ کیف پول')
  assert.equal(getWalletTxMeta(2).label, 'پرداخت سفارش')
  assert.equal(getWalletTxMeta('Refund').tone, 'success')
})

test('buildWalletsQueryString encodes filters and normalizes Persian search/amounts', () => {
  const qs = buildWalletsQueryString({
    page: 2,
    pageSize: 25,
    search: '۰۹۱۲',
    balanceFilter: 'positive',
    minBalance: '۵۰,۰۰۰',
    maxBalance: '۵۰۰,۰۰۰',
    sortBy: 'recent',
  })
  const params = new URLSearchParams(qs)
  assert.equal(params.get('page'), '2')
  assert.equal(params.get('search'), '0912')
  assert.equal(params.get('balanceFilter'), 'positive')
  assert.equal(params.get('minBalance'), '50000')
  assert.equal(params.get('maxBalance'), '500000')
  assert.equal(params.get('sortBy'), 'recent')
})

test('buildWalletTransactionsQueryString encodes type, direction, amount and dates', () => {
  const qs = buildWalletTransactionsQueryString({
    page: 1,
    pageSize: 30,
    type: 'Cashback',
    direction: 'in',
    search: 'کد ۱۲۳',
    minAmount: '۱۰,۰۰۰',
    fromDate: '2026-03-21',
    toDate: '2026-03-22',
  })
  const params = new URLSearchParams(qs)
  assert.equal(params.get('type'), 'Cashback')
  assert.equal(params.get('direction'), 'in')
  assert.equal(params.get('search'), 'کد 123')
  assert.equal(params.get('minAmount'), '10000')
  assert.ok(params.get('fromDate'))
  assert.ok(params.get('toDate'))
})
