import assert from 'node:assert/strict'
import test from 'node:test'
import {
  analyzeBlogSeo,
  countMarkdownWords,
  normalizeBlogSlug,
  renderMarkdownPreview,
} from './blogSeoAnalyzer.js'

test('normalizeBlogSlug handles Persian, Latin, and Persian digits', () => {
  assert.equal(normalizeBlogSlug('Complete Coffee Guide!'), 'complete-coffee-guide')
  assert.equal(normalizeBlogSlug('  راهنمای دم‌آوری قهوه V60  '), 'راهنمای-دمآوری-قهوه-v60')
  assert.equal(normalizeBlogSlug('اسپرسو ۱۲۳'), 'اسپرسو-123')
})

test('analyzeBlogSeo gives high score for well-optimized Persian coffee article', () => {
  const longBody = [
    '## راهنمای کامل قهوه تخصصی در ایران',
    'قهوه تخصصی تجربه‌ای متفاوت از عطر و طعم است. ' + 'قهوه تازه برشته‌شده طعم متعادل و دلپذیر دارد. '.repeat(90),
    '## روش‌های دم‌آوری قهوه',
    'برای مشاهده نوشیدنی‌ها به [منوی کافه](/) سر بزنید.',
  ].join('\n\n')

  const result = analyzeBlogSeo({
    title: 'راهنمای کامل قهوه تخصصی؛ از دانه عربیکا تا دم‌آوری در کافه',
    slug: 'complete-coffee-guide',
    focusKeyword: 'قهوه تخصصی',
    metaDescription:
      'در این راهنمای جامع با قهوه تخصصی، تفاوت عربیکا و روبوستا، روش‌های دم‌آوری وی ۶۰ و اسپرسو و نکات انتخاب دانه تازه در کافه شوگریستا آشنا شوید.',
    contentMarkdown: longBody,
    coverImageUrl: '/api/files/coffee.webp',
    coverImageAlt: 'فنجان اسپرسو تخصصی کافه شوگریستا',
    faqItems: [
      { question: 'قهوه تخصصی چیست؟', answer: 'قهوه‌ای با امتیاز بالای ۸۰ از ۱۰۰ در ارزیابی SCA.' },
      { question: 'بهترین روش دم‌آوری کدام است؟', answer: 'بسته به ذائقه، اسپرسو یا وی ۶۰.' },
    ],
    relatedMenuItemIds: ['11111111-1111-1111-1111-111111111111'],
  })

  assert.ok(result.score >= 95, `Expected high SEO score, got ${result.score}`)
  assert.equal(result.grade.level, 'excellent')
  assert.ok(countMarkdownWords(longBody) >= 700)
  assert.equal(result.serp.url, 'sugarista.ir/mag/complete-coffee-guide')
})

test('renderMarkdownPreview escapes XSS HTML and blocks javascript URLs', () => {
  const html = renderMarkdownPreview(
    '## تیتر قهوه\n<script>alert(1)</script>\n[بد](javascript:alert(2))\n[منو](/)',
  )

  assert.ok(html.includes('<h2>تیتر قهوه</h2>'))
  assert.ok(!html.includes('<script>'))
  assert.ok(html.includes('&lt;script&gt;'))
  assert.ok(!html.includes('javascript:'))
  assert.ok(html.includes('<a href="/">منو</a>'))
})
