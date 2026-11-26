export const SUGARISTA_BRAND_PALETTE = [
  { name: 'آبی نفتی شوگاریستا', hex: '#0C344F', role: 'پس‌زمینه هدر و هیرو' },
  { name: 'طلایی زعفرانی', hex: '#E5AA2F', role: 'لوگو، کلمه برجسته و دکمه‌ها' },
  { name: 'سرخابی شوگاریستا', hex: '#AA3171', role: 'خط زیر عنوان و بج‌ها' },
  { name: 'کرم روشن مجله', hex: '#FAF8F3', role: 'پس‌زمینه صفحه در حالت روز' },
  { name: 'سبز نعنایی تیره', hex: '#1F7A55', role: 'برچسب مکمل دسته‌بندی' },
]

export const HIGHLIGHT_THEME_OPTIONS = [
  { value: 'teal', label: 'آبی نفتی شوگاریستا (#0C344F)', color: '#0C344F' },
  { value: 'magenta', label: 'سرخابی شوگاریستا (#AA3171)', color: '#AA3171' },
  { value: 'gold', label: 'طلایی زعفرانی (#E5AA2F)', color: '#E5AA2F' },
]

export const DEFAULT_BLOG_APPEARANCE = {
  primaryDeepColor: '#0C344F',
  secondaryGoldColor: '#E5AA2F',
  accentMagentaColor: '#AA3171',
  pageBackgroundColor: '#FAF8F3',
  defaultColorMode: 'light',
  showThemeToggle: true,

  brandMonogram: 'S',
  brandLogoImageUrl: '',
  headerBrandTitle: 'Sugarista',
  headerBrandSubtitle: 'کافه و قنادی تخصصی شوگاریستا',
  headerHomeLinkLabel: 'صفحه اول مجله',
  headerSearchPlaceholder: 'جستجو در مقالات، قهوه، شیرینی...',
  headerCtaLabel: 'مشاهده منوی کافه',
  headerCtaUrl: '/',

  heroBadgeText: 'داستان دانه‌های قهوه، شیرینی‌های دست‌ساز و لحظه‌های شیرین',
  heroTitlePrefix: 'مجله طعم و زندگی',
  heroTitleHighlight: 'شوگاریستا',
  heroTitleSuffix: '',
  heroSubtitle:
    'اینجا درباره رازهای عصاره‌گیری اسپرسوی بی‌نقص، هنر قنادی، داستان‌های پشت بار کافه و سبک زندگی کافه‌نشینی می‌خوانید.',
  heroPrimaryButtonLabel: 'شروع مطالعه مقالات',
  heroPrimaryButtonUrl: '#mag-articles',
  heroSecondaryButtonLabel: 'سفارش آنلاین از منو',
  heroSecondaryButtonUrl: '/',
  showHeroStats: true,

  showCategoryRail: true,
  allCategoriesChipLabel: 'همه مطالب',
  showFeaturedSection: true,
  featuredBadgeLabel: 'پیشنهاد سرآشپز و باریستا',
  featuredReadButtonLabel: 'مطالعه کامل مقاله',
  showHighlightsSection: true,
  highlightsSectionEyebrow: 'پشت بار شوگاریستا',
  highlightsSectionTitle: 'نکات طلایی قهوه و شیرینی',
  archiveSectionEyebrow: 'تازه‌ترین نوشته‌ها',
  archiveSectionTitle: 'آرشیو مقالات مجله',
  postsPerPage: 9,
  cardReadMoreLabel: 'ادامه مطالعه',

  showReadingProgressBar: true,
  showTableOfContents: true,
  tocTitle: 'آنچه در این مقاله می‌خوانید',
  showViewCount: true,
  showShareButtons: true,
  relatedMenuEyebrow: 'پیشنهاد خوشمزه مرتبط با این مطلب',
  relatedMenuTitle: 'همین طعم را از منوی کافه سفارش دهید',
  faqSectionTitle: 'سوالات پرتکرار شما',
  relatedPostsTitle: 'مطالب مشابه که دوست خواهید داشت',

  showCafeCtaBanner: true,
  cafeCtaEyebrow: 'تجربه حضوری و سفارش آنلاین',
  cafeCtaTitle: 'هوس یک اسپرسوی تازه یا شیرینی دست‌ساز کردید؟',
  cafeCtaDescription:
    'در منوی دیجیتال شوگاریستا جدیدترین نوشیدنی‌های تخصصی، کیک‌ها و دسرهای پخت روز را ببینید و همین الان آنلاین سفارش دهید.',
  cafeCtaButtonLabel: 'ورود به منوی دیجیتال و سفارش',
  cafeCtaButtonUrl: '/',
  cafeCtaImageUrl: '',

  footerDescription:
    'مجله تخصصی طعم، قهوه و قنادی شوگاریستا؛ جایی برای کشف داستان دانه‌ها، رسپی‌های دقیق و لحظه‌های شیرین کافه‌نشینی.',
  footerCategoriesHeading: 'موضوعات مجله',
  footerQuickLinksHeading: 'لینک‌های کاربردی',
  footerCopyrightText: 'تمامی حقوق برای کافه و قنادی تخصصی شوگاریستا محفوظ است.',
  homeMetaTitle: '',
  homeMetaDescription: '',

  customLinks: [],
}

const HEX_COLOR_RE = /^#[0-9a-fA-F]{6}$/

export function isValidHexColor(value) {
  return typeof value === 'string' && HEX_COLOR_RE.test(value.trim())
}

export function normalizeHexColor(value, fallback) {
  if (!isValidHexColor(value)) return fallback
  return value.trim().toUpperCase()
}

export function normalizeAppearanceForm(raw) {
  const src = raw && typeof raw === 'object' ? raw : {}
  const def = DEFAULT_BLOG_APPEARANCE

  const boolOr = (val, fallback) => (typeof val === 'boolean' ? val : fallback)
  const strOr = (val, fallback) => (typeof val === 'string' ? val : fallback)

  const rawLinks = Array.isArray(src.customLinks) ? src.customLinks : []
  const customLinks = rawLinks.map((item, idx) => ({
    label: typeof item?.label === 'string' ? item.label : '',
    url: typeof item?.url === 'string' ? item.url : '',
    placement:
      item?.placement === 'footer' || item?.placement === 'both' ? item.placement : 'header',
    displayOrder: Number.isFinite(Number(item?.displayOrder)) ? Number(item.displayOrder) : idx,
  }))

  const parsedPerPage = Number(src.postsPerPage)
  const postsPerPage =
    Number.isFinite(parsedPerPage) && parsedPerPage >= 3 && parsedPerPage <= 24
      ? Math.round(parsedPerPage)
      : def.postsPerPage

  return {
    primaryDeepColor: normalizeHexColor(src.primaryDeepColor, def.primaryDeepColor),
    secondaryGoldColor: normalizeHexColor(src.secondaryGoldColor, def.secondaryGoldColor),
    accentMagentaColor: normalizeHexColor(src.accentMagentaColor, def.accentMagentaColor),
    pageBackgroundColor: normalizeHexColor(src.pageBackgroundColor, def.pageBackgroundColor),
    defaultColorMode: src.defaultColorMode === 'dark' ? 'dark' : 'light',
    showThemeToggle: boolOr(src.showThemeToggle, def.showThemeToggle),

    brandMonogram: strOr(src.brandMonogram, def.brandMonogram),
    brandLogoImageUrl: strOr(src.brandLogoImageUrl, ''),
    headerBrandTitle: strOr(src.headerBrandTitle, def.headerBrandTitle),
    headerBrandSubtitle: strOr(src.headerBrandSubtitle, def.headerBrandSubtitle),
    headerHomeLinkLabel: strOr(src.headerHomeLinkLabel, def.headerHomeLinkLabel),
    headerSearchPlaceholder: strOr(src.headerSearchPlaceholder, def.headerSearchPlaceholder),
    headerCtaLabel: strOr(src.headerCtaLabel, def.headerCtaLabel),
    headerCtaUrl: strOr(src.headerCtaUrl, def.headerCtaUrl),

    heroBadgeText: strOr(src.heroBadgeText, def.heroBadgeText),
    heroTitlePrefix: strOr(src.heroTitlePrefix, def.heroTitlePrefix),
    heroTitleHighlight: strOr(src.heroTitleHighlight, def.heroTitleHighlight),
    heroTitleSuffix: strOr(src.heroTitleSuffix, def.heroTitleSuffix),
    heroSubtitle: strOr(src.heroSubtitle, def.heroSubtitle),
    heroPrimaryButtonLabel: strOr(src.heroPrimaryButtonLabel, def.heroPrimaryButtonLabel),
    heroPrimaryButtonUrl: strOr(src.heroPrimaryButtonUrl, def.heroPrimaryButtonUrl),
    heroSecondaryButtonLabel: strOr(src.heroSecondaryButtonLabel, def.heroSecondaryButtonLabel),
    heroSecondaryButtonUrl: strOr(src.heroSecondaryButtonUrl, def.heroSecondaryButtonUrl),
    showHeroStats: boolOr(src.showHeroStats, def.showHeroStats),

    showCategoryRail: boolOr(src.showCategoryRail, def.showCategoryRail),
    allCategoriesChipLabel: strOr(src.allCategoriesChipLabel, def.allCategoriesChipLabel),
    showFeaturedSection: boolOr(src.showFeaturedSection, def.showFeaturedSection),
    featuredBadgeLabel: strOr(src.featuredBadgeLabel, def.featuredBadgeLabel),
    featuredReadButtonLabel: strOr(src.featuredReadButtonLabel, def.featuredReadButtonLabel),
    showHighlightsSection: boolOr(src.showHighlightsSection, def.showHighlightsSection),
    highlightsSectionEyebrow: strOr(src.highlightsSectionEyebrow, def.highlightsSectionEyebrow),
    highlightsSectionTitle: strOr(src.highlightsSectionTitle, def.highlightsSectionTitle),
    archiveSectionEyebrow: strOr(src.archiveSectionEyebrow, def.archiveSectionEyebrow),
    archiveSectionTitle: strOr(src.archiveSectionTitle, def.archiveSectionTitle),
    postsPerPage,
    cardReadMoreLabel: strOr(src.cardReadMoreLabel, def.cardReadMoreLabel),

    showReadingProgressBar: boolOr(src.showReadingProgressBar, def.showReadingProgressBar),
    showTableOfContents: boolOr(src.showTableOfContents, def.showTableOfContents),
    tocTitle: strOr(src.tocTitle, def.tocTitle),
    showViewCount: boolOr(src.showViewCount, def.showViewCount),
    showShareButtons: boolOr(src.showShareButtons, def.showShareButtons),
    relatedMenuEyebrow: strOr(src.relatedMenuEyebrow, def.relatedMenuEyebrow),
    relatedMenuTitle: strOr(src.relatedMenuTitle, def.relatedMenuTitle),
    faqSectionTitle: strOr(src.faqSectionTitle, def.faqSectionTitle),
    relatedPostsTitle: strOr(src.relatedPostsTitle, def.relatedPostsTitle),

    showCafeCtaBanner: boolOr(src.showCafeCtaBanner, def.showCafeCtaBanner),
    cafeCtaEyebrow: strOr(src.cafeCtaEyebrow, def.cafeCtaEyebrow),
    cafeCtaTitle: strOr(src.cafeCtaTitle, def.cafeCtaTitle),
    cafeCtaDescription: strOr(src.cafeCtaDescription, def.cafeCtaDescription),
    cafeCtaButtonLabel: strOr(src.cafeCtaButtonLabel, def.cafeCtaButtonLabel),
    cafeCtaButtonUrl: strOr(src.cafeCtaButtonUrl, def.cafeCtaButtonUrl),
    cafeCtaImageUrl: strOr(src.cafeCtaImageUrl, ''),

    footerDescription: strOr(src.footerDescription, def.footerDescription),
    footerCategoriesHeading: strOr(src.footerCategoriesHeading, def.footerCategoriesHeading),
    footerQuickLinksHeading: strOr(src.footerQuickLinksHeading, def.footerQuickLinksHeading),
    footerCopyrightText: strOr(src.footerCopyrightText, def.footerCopyrightText),
    homeMetaTitle: strOr(src.homeMetaTitle, ''),
    homeMetaDescription: strOr(src.homeMetaDescription, ''),

    customLinks,
  }
}

export function buildAppearancePayload(form) {
  const normalized = normalizeAppearanceForm(form)
  const rawPages = Number(form?.postsPerPage)
  const clampedPages = Number.isFinite(rawPages)
    ? Math.min(24, Math.max(3, Math.round(rawPages)))
    : DEFAULT_BLOG_APPEARANCE.postsPerPage

  return {
    ...normalized,
    brandLogoImageUrl: normalized.brandLogoImageUrl.trim() || null,
    cafeCtaImageUrl: normalized.cafeCtaImageUrl.trim() || null,
    postsPerPage: clampedPages,
    customLinks: normalized.customLinks
      .map((item, idx) => ({
        label: String(item.label ?? '').trim(),
        url: String(item.url ?? '').trim(),
        placement:
          item.placement === 'footer' || item.placement === 'both' ? item.placement : 'header',
        displayOrder: Number.isFinite(Number(item.displayOrder)) ? Number(item.displayOrder) : idx,
      }))
      .filter((item) => item.label && item.url),
  }
}
