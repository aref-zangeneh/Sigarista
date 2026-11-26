import assert from 'node:assert/strict'
import test from 'node:test'
import {
  DEFAULT_BLOG_APPEARANCE,
  buildAppearancePayload,
  isValidHexColor,
  normalizeAppearanceForm,
  normalizeHexColor,
} from './blogAppearance.js'

test('DEFAULT_BLOG_APPEARANCE matches Sugarista signature palette', () => {
  assert.equal(DEFAULT_BLOG_APPEARANCE.primaryDeepColor, '#0C344F')
  assert.equal(DEFAULT_BLOG_APPEARANCE.secondaryGoldColor, '#E5AA2F')
  assert.equal(DEFAULT_BLOG_APPEARANCE.accentMagentaColor, '#AA3171')
  assert.equal(DEFAULT_BLOG_APPEARANCE.pageBackgroundColor, '#FAF8F3')
})

test('normalizeHexColor validates 6-digit hex and falls back safely', () => {
  assert.equal(isValidHexColor('#0c344f'), true)
  assert.equal(isValidHexColor('red; background:url(x)'), false)
  assert.equal(normalizeHexColor('#aa3171', '#000000'), '#AA3171')
  assert.equal(normalizeHexColor('invalid', '#0C344F'), '#0C344F')
})

test('normalizeAppearanceForm fills missing values and sanitizes invalid colors', () => {
  const form = normalizeAppearanceForm({
    primaryDeepColor: 'not-a-hex',
    heroTitleHighlight: 'شوگاریستا پلاس',
    postsPerPage: 12,
    showHeroStats: false,
  })
  assert.equal(form.primaryDeepColor, '#0C344F')
  assert.equal(form.heroTitleHighlight, 'شوگاریستا پلاس')
  assert.equal(form.postsPerPage, 12)
  assert.equal(form.showHeroStats, false)
  assert.equal(form.secondaryGoldColor, '#E5AA2F')
})

test('buildAppearancePayload filters empty customLinks and clamps postsPerPage', () => {
  const payload = buildAppearancePayload({
    ...DEFAULT_BLOG_APPEARANCE,
    postsPerPage: 99,
    brandLogoImageUrl: '   ',
    customLinks: [
      { label: 'اینستاگرام', url: 'https://instagram.com/sugarista', placement: 'both', displayOrder: 1 },
      { label: '   ', url: '/empty', placement: 'header', displayOrder: 2 },
    ],
  })

  assert.equal(payload.postsPerPage, 24)
  assert.equal(payload.brandLogoImageUrl, null)
  assert.equal(payload.customLinks.length, 1)
  assert.equal(payload.customLinks[0].label, 'اینستاگرام')
  assert.equal(payload.customLinks[0].placement, 'both')
})
