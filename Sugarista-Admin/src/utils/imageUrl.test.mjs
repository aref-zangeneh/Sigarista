import test from 'node:test'
import assert from 'node:assert/strict'
import {
  DEFAULT_PUBLIC_ORIGIN,
  isRelativeSitePath,
  normalizeImageUrl,
  resolveImageOrigin,
} from './imageUrl.js'

test('resolveImageOrigin defaults to https://sugarista.ir when window is absent', () => {
  assert.equal(resolveImageOrigin(), DEFAULT_PUBLIC_ORIGIN)
  assert.equal(resolveImageOrigin('https://sugarista.ir/'), 'https://sugarista.ir')
})

test('normalizeImageUrl prepends site origin to root-relative upload paths', () => {
  assert.equal(
    normalizeImageUrl('/api/files/images/abc.webp'),
    'https://sugarista.ir/api/files/images/abc.webp',
  )
  assert.equal(
    normalizeImageUrl('/api/files/images/abc.webp', 'http://localhost'),
    'http://localhost/api/files/images/abc.webp',
  )
})

test('normalizeImageUrl handles relative paths without leading slash', () => {
  assert.equal(
    normalizeImageUrl('api/files/images/abc.webp', 'https://sugarista.ir/'),
    'https://sugarista.ir/api/files/images/abc.webp',
  )
})

test('normalizeImageUrl prepends https:// for bare domain paths', () => {
  assert.equal(
    normalizeImageUrl('sugarista.ir/api/files/images/abc.webp'),
    'https://sugarista.ir/api/files/images/abc.webp',
  )
})

test('normalizeImageUrl preserves absolute http(s) URLs untouched', () => {
  assert.equal(
    normalizeImageUrl('https://sugarista.ir/api/files/images/abc.webp'),
    'https://sugarista.ir/api/files/images/abc.webp',
  )
  assert.equal(
    normalizeImageUrl('http://localhost/api/files/images/abc.webp'),
    'http://localhost/api/files/images/abc.webp',
  )
})

test('normalizeImageUrl returns empty string for blank inputs', () => {
  assert.equal(normalizeImageUrl(''), '')
  assert.equal(normalizeImageUrl('   '), '')
  assert.equal(normalizeImageUrl(null), '')
  assert.equal(normalizeImageUrl(undefined), '')
})

test('isRelativeSitePath detects root-relative and api/uploads paths only', () => {
  assert.equal(isRelativeSitePath('/api/files/images/abc.webp'), true)
  assert.equal(isRelativeSitePath('api/files/images/abc.webp'), true)
  assert.equal(isRelativeSitePath('uploads/images/abc.webp'), true)
  assert.equal(isRelativeSitePath('https://sugarista.ir/api/files/images/abc.webp'), false)
  assert.equal(isRelativeSitePath('http'), false)
  assert.equal(isRelativeSitePath(''), false)
})
