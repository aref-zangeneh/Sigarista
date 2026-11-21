/**
 * Pure SEO analyzer, slug normalizer, and lightweight XSS-safe Markdown preview
 * renderer for the Sugarista Coffee Magazine (/mag) Admin CMS.
 */

export function normalizeBlogSlug(value) {
  if (!value || typeof value !== 'string') {
    return ''
  }

  return value
    .trim()
    .toLowerCase()
    .replace(/[۰-۹]/g, (d) => String(d.charCodeAt(0) - 0x06f0))
    .replace(/[٠-٩]/g, (d) => String(d.charCodeAt(0) - 0x0660))
    .replace(/[\s_/\\—–|+]+/g, '-')
    .replace(/[^\u0600-\u06ffa-z0-9-]/g, '')
    .replace(/-{2,}/g, '-')
    .replace(/^-+|-+$/g, '')
}

export function countMarkdownWords(markdown) {
  if (!markdown || typeof markdown !== 'string') {
    return 0
  }

  const plain = markdown
    .replace(/!\[([^\]]*)\]\([^)]+\)/g, '$1')
    .replace(/\[([^\]]+)\]\([^)]+\)/g, '$1')
    .replace(/[#>*`_|~-]/g, ' ')
    .replace(/\s+/g, ' ')
    .trim()

  if (!plain) {
    return 0
  }

  return plain.split(' ').filter(Boolean).length
}

export function estimateReadingMinutes(markdown) {
  const words = countMarkdownWords(markdown)
  return Math.max(1, Math.ceil(words / 200))
}

export function extractMarkdownHeadings(markdown) {
  if (!markdown || typeof markdown !== 'string') {
    return []
  }

  const headings = []
  for (const rawLine of markdown.replace(/\r\n/g, '\n').split('\n')) {
    const match = rawLine.match(/^(#{2,4})\s+(.+?)\s*#*$/)
    if (match) {
      headings.push({
        level: match[1].length,
        text: match[2].trim(),
      })
    }
  }
  return headings
}

/**
 * Computes a 0-100 SEO score + Persian checklist for a blog article draft.
 */
export function analyzeBlogSeo(post = {}) {
  const title = (post.title ?? '').trim()
  const metaTitle = (post.metaTitle ?? '').trim() || title
  const metaDescription = (post.metaDescription ?? '').trim() || (post.excerpt ?? '').trim()
  const slug = normalizeBlogSlug(post.slug || title)
  const focusKeyword = (post.focusKeyword ?? '').trim().toLowerCase()
  const content = (post.contentMarkdown ?? '').trim()
  const contentLower = content.toLowerCase()
  const firstParagraphLower = contentLower.slice(0, 360)
  const headings = extractMarkdownHeadings(content)
  const h2Count = headings.filter((h) => h.level === 2).length
  const wordCount = countMarkdownWords(content)
  const faqCount = (post.faqItems ?? []).filter((f) => f?.question?.trim() && f?.answer?.trim()).length
  const relatedCount = (post.relatedMenuItemIds ?? []).length
  const hasInternalLink = /\]\(\/[^)]*\)/.test(content) || relatedCount > 0

  const checks = []

  // 1. Meta title length (30..65 chars ideal for Persian Google SERP)
  const titleLen = metaTitle.length
  checks.push({
    id: 'meta-title',
    label: 'طول عنوان سئو (۳۰ تا ۶۵ کاراکتر)',
    points: 12,
    earned: titleLen >= 30 && titleLen <= 65 ? 12 : titleLen >= 18 && titleLen <= 75 ? 7 : 0,
    status: titleLen >= 30 && titleLen <= 65 ? 'pass' : titleLen >= 18 ? 'warn' : 'fail',
    hint: `فعلی: ${titleLen} کاراکتر`,
  })

  // 2. Meta description length (110..165 chars ideal)
  const descLen = metaDescription.length
  checks.push({
    id: 'meta-description',
    label: 'توضیحات متا برای گوگل (۱۱۰ تا ۱۶۵ کاراکتر)',
    points: 12,
    earned: descLen >= 110 && descLen <= 165 ? 12 : descLen >= 70 && descLen <= 190 ? 7 : 0,
    status: descLen >= 110 && descLen <= 165 ? 'pass' : descLen >= 70 ? 'warn' : 'fail',
    hint: `فعلی: ${descLen} کاراکتر`,
  })

  // 3. Focus keyword defined & placed in Title + Intro + Headings
  const hasKeyword = focusKeyword.length >= 2
  const kwInTitle = hasKeyword && metaTitle.toLowerCase().includes(focusKeyword)
  const kwInIntro = hasKeyword && firstParagraphLower.includes(focusKeyword)
  const kwInHeading = hasKeyword && headings.some((h) => h.text.toLowerCase().includes(focusKeyword))
  const kwEarned = !hasKeyword ? 0 : (kwInTitle ? 8 : 0) + (kwInIntro ? 5 : 0) + (kwInHeading ? 5 : 0)
  checks.push({
    id: 'focus-keyword',
    label: 'کلیدواژه کانونی در عنوان، پاراگراف اول و زیرتیترها',
    points: 18,
    earned: kwEarned,
    status: kwEarned >= 18 ? 'pass' : kwEarned >= 8 ? 'warn' : 'fail',
    hint: !hasKeyword
      ? 'کلیدواژه کانونی (مثلاً «قهوه تخصصی») را وارد کنید'
      : [
          kwInTitle ? 'عنوان ✓' : 'عنوان ✕',
          kwInIntro ? 'مقدمه ✓' : 'مقدمه ✕',
          kwInHeading ? 'زیرتیتر ✓' : 'زیرتیتر ✕',
        ].join(' · '),
  })

  // 4. Content depth (word count)
  const depthEarned = wordCount >= 700 ? 18 : wordCount >= 350 ? 12 : wordCount >= 150 ? 6 : 0
  checks.push({
    id: 'word-count',
    label: 'عمق محتوا (حداقل ۷۰۰ کلمه برای مقالات مرجع)',
    points: 18,
    earned: depthEarned,
    status: wordCount >= 700 ? 'pass' : wordCount >= 350 ? 'warn' : 'fail',
    hint: `${wordCount} کلمه (حدود ${estimateReadingMinutes(content)} دقیقه مطالعه)`,
  })

  // 5. Heading structure (H2/H3 for TOC)
  checks.push({
    id: 'headings',
    label: 'ساختار زیرتیترهای H2/H3 (ساخت فهرست مطالب خودکار)',
    points: 10,
    earned: h2Count >= 2 ? 10 : h2Count === 1 ? 5 : 0,
    status: h2Count >= 2 ? 'pass' : h2Count === 1 ? 'warn' : 'fail',
    hint: `${h2Count} تیتر H2 و ${headings.length - h2Count} زیرتیتر`,
  })

  // 6. Clean URL slug
  checks.push({
    id: 'slug',
    label: 'اسلاگ کوتاه و تمیز (آدرس URL)',
    points: 8,
    earned: slug.length >= 3 && slug.length <= 75 ? 8 : slug.length > 0 ? 4 : 0,
    status: slug.length >= 3 && slug.length <= 75 ? 'pass' : 'warn',
    hint: slug ? `/mag/${slug}` : 'اسلاگ تنظیم نشده است',
  })

  // 7. Cover image + Alt text
  const hasCover = Boolean((post.coverImageUrl ?? '').trim())
  const hasAlt = Boolean((post.coverImageAlt ?? '').trim())
  checks.push({
    id: 'cover-image',
    label: 'تصویر شاخص و متن جایگزین (Alt)',
    points: 8,
    earned: hasCover && hasAlt ? 8 : hasCover ? 4 : 0,
    status: hasCover && hasAlt ? 'pass' : hasCover ? 'warn' : 'fail',
    hint: !hasCover ? 'تصویر شاخص آپلود کنید' : hasAlt ? 'تصویر و Alt کامل است' : 'متن Alt تصویر را پر کنید',
  })

  // 8. FAQ Schema items (Rich Results in Google)
  checks.push({
    id: 'faq-schema',
    label: 'سوالات متداول (اسکیما FAQPage برای نمایش غنی در گوگل)',
    points: 8,
    earned: faqCount >= 2 ? 8 : faqCount === 1 ? 4 : 0,
    status: faqCount >= 2 ? 'pass' : faqCount === 1 ? 'warn' : 'fail',
    hint: faqCount > 0 ? `${faqCount} سوال ثبت شده` : 'حداقل ۲ سوال متداول اضافه کنید',
  })

  // 9. Internal links / Related cafe menu items
  checks.push({
    id: 'internal-links',
    label: 'لینک داخلی یا پیشنهاد آیتم منوی کافه',
    points: 6,
    earned: hasInternalLink ? 6 : 0,
    status: hasInternalLink ? 'pass' : 'warn',
    hint: relatedCount > 0 ? `${relatedCount} آیتم منو متصل شده` : hasInternalLink ? 'لینک داخلی در متن وجود دارد' : 'حداقل یک آیتم منو یا لینک به / اضافه کنید',
  })

  const score = Math.min(
    100,
    checks.reduce((sum, item) => sum + item.earned, 0),
  )

  const grade =
    score >= 85
      ? { level: 'excellent', label: 'عالی برای رتبه گوگل' }
      : score >= 65
        ? { level: 'good', label: 'خوب — قابل بهبود' }
        : { level: 'weak', label: 'نیاز به بهینه‌سازی سئو' }

  return {
    score,
    grade,
    wordCount,
    readingTimeMinutes: estimateReadingMinutes(content),
    checks,
    serp: {
      title: metaTitle || 'عنوان مقاله در گوگل',
      description: metaDescription || 'توضیحات متا مقاله در نتایج جستجوی گوگل در این بخش نمایش داده می‌شود.',
      url: `sugarista.ir/mag/${slug || 'article-slug'}`,
    },
  }
}

function escapeHtml(text) {
  return String(text ?? '')
    .replace(/&/g, '&amp;')
    .replace(/</g, '&lt;')
    .replace(/>/g, '&gt;')
    .replace(/"/g, '&quot;')
    .replace(/'/g, '&#39;')
}

function sanitizePreviewUrl(url) {
  const trimmed = String(url ?? '').trim()
  if (trimmed.startsWith('/') && !trimmed.startsWith('//')) {
    return trimmed
  }
  if (/^https?:\/\//i.test(trimmed)) {
    return trimmed
  }
  return null
}

function renderInlinePreview(raw) {
  let html = escapeHtml(raw)
  html = html.replace(/!\[([^\]]*)\]\(([^)\s]+)\)/g, (_, alt, url) => {
    const safe = sanitizePreviewUrl(url)
    if (!safe) return ''
    return `<img src="${escapeHtml(safe)}" alt="${alt}" loading="lazy" />`
  })
  html = html.replace(/\[([^\]]+)\]\(([^)\s]+)\)/g, (_, label, url) => {
    const safe = sanitizePreviewUrl(url)
    if (!safe) return label
    const external = /^https?:\/\//i.test(safe) ? ' target="_blank" rel="noopener noreferrer"' : ''
    return `<a href="${escapeHtml(safe)}"${external}>${label}</a>`
  })
  html = html.replace(/`([^`]+)`/g, '<code>$1</code>')
  html = html.replace(/\*\*(.+?)\*\*/g, '<strong>$1</strong>')
  html = html.replace(/(?<!\*)\*(?!\*)([^*]+?)(?<!\*)\*(?!\*)/g, '<em>$1</em>')
  return html
}

/**
 * Lightweight XSS-safe Markdown-to-HTML renderer for live preview inside Admin CMS.
 */
export function renderMarkdownPreview(markdown) {
  if (!markdown || typeof markdown !== 'string') {
    return ''
  }

  const lines = markdown.replace(/\r\n/g, '\n').split('\n')
  const out = []
  let i = 0

  while (i < lines.length) {
    const line = lines[i]
    if (!line.trim()) {
      i += 1
      continue
    }

    const heading = line.match(/^(#{2,4})\s+(.+?)\s*#*$/)
    if (heading) {
      const level = heading[1].length
      out.push(`<h${level}>${renderInlinePreview(heading[2])}</h${level}>`)
      i += 1
      continue
    }

    if (/^\s*([-*_])\s*\1\s*\1[\s\-*_]*$/.test(line)) {
      out.push('<hr />')
      i += 1
      continue
    }

    if (line.trim().startsWith('>')) {
      const quoteLines = []
      while (i < lines.length && lines[i].trim().startsWith('>')) {
        quoteLines.push(lines[i].trim().replace(/^>\s?/, ''))
        i += 1
      }
      out.push(`<blockquote><p>${quoteLines.map(renderInlinePreview).join('<br />')}</p></blockquote>`)
      continue
    }

    if (
      /^\|(.+)\|$/.test(line.trim()) &&
      i + 1 < lines.length &&
      /^\|[\s:\-|]+\|$/.test(lines[i + 1].trim())
    ) {
      const splitCells = (row) =>
        row
          .trim()
          .replace(/^\|/, '')
          .replace(/\|$/, '')
          .split('|')
          .map((c) => c.trim())
      const headers = splitCells(line)
      i += 2
      const rows = []
      while (i < lines.length && /^\|(.+)\|$/.test(lines[i].trim())) {
        rows.push(splitCells(lines[i]))
        i += 1
      }
      const thead = `<thead><tr>${headers.map((h) => `<th>${renderInlinePreview(h)}</th>`).join('')}</tr></thead>`
      const tbody = `<tbody>${rows
        .map((r) => `<tr>${r.map((c) => `<td>${renderInlinePreview(c)}</td>`).join('')}</tr>`)
        .join('')}</tbody>`
      out.push(`<div class="blog-preview-table-wrap"><table>${thead}${tbody}</table></div>`)
      continue
    }

    if (/^\s*[-*•]\s+/.test(line)) {
      const items = []
      while (i < lines.length && /^\s*[-*•]\s+/.test(lines[i])) {
        items.push(lines[i].replace(/^\s*[-*•]\s+/, ''))
        i += 1
      }
      out.push(`<ul>${items.map((item) => `<li>${renderInlinePreview(item)}</li>`).join('')}</ul>`)
      continue
    }

    if (/^\s*\d+[.)]\s+/.test(line)) {
      const items = []
      while (i < lines.length && /^\s*\d+[.)]\s+/.test(lines[i])) {
        items.push(lines[i].replace(/^\s*\d+[.)]\s+/, ''))
        i += 1
      }
      out.push(`<ol>${items.map((item) => `<li>${renderInlinePreview(item)}</li>`).join('')}</ol>`)
      continue
    }

    const para = []
    while (
      i < lines.length &&
      lines[i].trim() &&
      !/^(#{2,4})\s+/.test(lines[i]) &&
      !lines[i].trim().startsWith('>') &&
      !/^\s*[-*•]\s+/.test(lines[i]) &&
      !/^\s*\d+[.)]\s+/.test(lines[i])
    ) {
      para.push(lines[i].trim())
      i += 1
    }
    out.push(`<p>${para.map(renderInlinePreview).join(' ')}</p>`)
  }

  return out.join('\n')
}
