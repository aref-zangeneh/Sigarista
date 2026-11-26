import { useEffect, useState } from 'react'
import { ImageUploader } from './ImageUploader.jsx'
import { apiRequest, getReadableError } from '../utils/adminApi.js'
import {
  DEFAULT_BLOG_APPEARANCE,
  SUGARISTA_BRAND_PALETTE,
  buildAppearancePayload,
  normalizeAppearanceForm,
} from '../utils/blogAppearance.js'

export function BlogAppearancePanel({ token, canEdit, setError, setMessage }) {
  const [form, setForm] = useState(() => normalizeAppearanceForm(DEFAULT_BLOG_APPEARANCE))
  const [loading, setLoading] = useState(true)
  const [saving, setSaving] = useState(false)
  const [resetting, setResetting] = useState(false)

  useEffect(() => {
    if (!token) return
    let cancelled = false
    setLoading(true)
    apiRequest('/admin/blog/appearance', { token })
      .then((data) => {
        if (!cancelled) {
          setForm(normalizeAppearanceForm(data))
        }
      })
      .catch((err) => {
        if (!cancelled) setError(getReadableError(err))
      })
      .finally(() => {
        if (!cancelled) setLoading(false)
      })
    return () => {
      cancelled = true
    }
  }, [token, setError])

  const updateField = (key, value) => {
    setForm((prev) => ({ ...prev, [key]: value }))
  }

  const handleAddCustomLink = () => {
    setForm((prev) => ({
      ...prev,
      customLinks: [
        ...prev.customLinks,
        {
          label: '',
          url: '/',
          placement: 'header',
          displayOrder: prev.customLinks.length + 1,
        },
      ],
    }))
  }

  const handleUpdateCustomLink = (index, key, value) => {
    setForm((prev) => ({
      ...prev,
      customLinks: prev.customLinks.map((item, idx) =>
        idx === index ? { ...item, [key]: value } : item,
      ),
    }))
  }

  const handleRemoveCustomLink = (index) => {
    setForm((prev) => ({
      ...prev,
      customLinks: prev.customLinks.filter((_, idx) => idx !== index),
    }))
  }

  const handleSave = async (event) => {
    event.preventDefault()
    if (!token || !canEdit) return
    setSaving(true)
    setError('')
    setMessage('')
    try {
      const payload = buildAppearancePayload(form)
      const saved = await apiRequest('/admin/blog/appearance', {
        method: 'PUT',
        body: payload,
        token,
      })
      setForm(normalizeAppearanceForm(saved))
      setMessage('تمام تنظیمات ظاهر، رنگ‌ها و متن‌های مجله با موفقیت ذخیره شد و هم‌اکنون در /mag فعال است.')
    } catch (err) {
      setError(getReadableError(err))
    } finally {
      setSaving(false)
    }
  }

  const handleResetDefaults = async () => {
    if (!token || !canEdit) return
    if (
      !window.confirm(
        'آیا مطمئن هستید که می‌خواهید تمام رنگ‌ها و متن‌های قالب مجله به مقادیر پیش‌فرض استاندارد شوگاریستا برگردند؟',
      )
    ) {
      return
    }
    setResetting(true)
    setError('')
    setMessage('')
    try {
      const defaults = await apiRequest('/admin/blog/appearance/reset', {
        method: 'POST',
        token,
      })
      setForm(normalizeAppearanceForm(defaults))
      setMessage('ظاهر و متن‌های مجله به پالت استاندارد شوگاریستا بازنشانی شد.')
    } catch (err) {
      setError(getReadableError(err))
    } finally {
      setResetting(false)
    }
  }

  if (loading) {
    return (
      <section className="panel">
        <p style={{ color: 'var(--muted)' }}>در حال بارگذاری تنظیمات ظاهر و متن‌های مجله...</p>
      </section>
    )
  }

  return (
    <form className="blog-appearance-form" onSubmit={handleSave}>
      {/* ── پیش‌نمایش زنده بنر بالای مجله و پالت شوگاریستا ── */}
      <section className="panel">
        <div className="panel-head">
          <div>
            <h3>پیش‌نمایش زنده هویت بصری مجله (/mag)</h3>
            <p>
              هر تغییری که در رنگ‌ها، عنوان‌ها یا دکمه‌های زیر بدهید بلافاصله در این پیش‌نمایش دیده می‌شود و پس از ذخیره روی سایت اعمال می‌گردد.
            </p>
          </div>
          <div className="blog-hero-actions">
            {canEdit ? (
              <>
                <button
                  type="button"
                  className="ghost-button"
                  disabled={resetting || saving}
                  onClick={handleResetDefaults}
                >
                  {resetting ? 'در حال بازنشانی...' : 'بازگردانی به پیش‌فرض شوگاریستا'}
                </button>
                <button type="submit" className="blog-primary-btn" disabled={saving}>
                  {saving ? 'در حال ذخیره...' : 'ذخیره تمام تنظیمات مجله'}
                </button>
              </>
            ) : null}
          </div>
        </div>

        {/* نوار مرجع پالت رنگ شوگاریستا */}
        <div className="blog-palette-strip">
          <span className="blog-palette-title">پالت پیشنهادی شوگاریستا:</span>
          {SUGARISTA_BRAND_PALETTE.map((sw) => (
            <button
              key={sw.hex}
              type="button"
              className="blog-swatch-pill"
              title={`${sw.name} (${sw.role}) — کلیک برای کپی کد رنگ`}
              onClick={() => {
                navigator.clipboard?.writeText(sw.hex)
                setMessage(`کد رنگ ${sw.hex} (${sw.name}) کپی شد.`)
              }}
            >
              <span className="blog-color-swatch" style={{ background: sw.hex }} />
              <strong>{sw.name}</strong>
              <code className="ltr-input">{sw.hex}</code>
            </button>
          ))}
        </div>

        {/* پیش‌نمایش زنده هدر و هیرو */}
        <div
          className="blog-live-hero-preview"
          style={{
            '--pv-deep': form.primaryDeepColor || '#0C344F',
            '--pv-gold': form.secondaryGoldColor || '#E5AA2F',
            '--pv-magenta': form.accentMagentaColor || '#AA3171',
            '--pv-bg': form.pageBackgroundColor || '#FAF8F3',
          }}
        >
          <div className="blog-pv-topbar">
            <div className="blog-pv-brand">
              <span className="blog-pv-emblem">{form.brandMonogram || 'S'}</span>
              <div>
                <strong>{form.headerBrandTitle || 'Sugarista'}</strong>
                {form.headerBrandSubtitle ? <small>{form.headerBrandSubtitle}</small> : null}
              </div>
            </div>
            <span className="blog-pv-cta">{form.headerCtaLabel || 'مشاهده منوی کافه'}</span>
          </div>

          <div className="blog-pv-body">
            {form.heroBadgeText ? (
              <span className="blog-pv-badge">
                <i /> {form.heroBadgeText}
              </span>
            ) : null}
            <h4>
              {form.heroTitlePrefix}{' '}
              {form.heroTitleHighlight ? (
                <em className="blog-pv-highlight">{form.heroTitleHighlight}</em>
              ) : null}{' '}
              {form.heroTitleSuffix}
            </h4>
            {form.heroSubtitle ? <p>{form.heroSubtitle}</p> : null}
            <div className="blog-pv-btns">
              {form.heroPrimaryButtonLabel ? (
                <span className="blog-pv-btn-gold">{form.heroPrimaryButtonLabel}</span>
              ) : null}
              {form.heroSecondaryButtonLabel ? (
                <span className="blog-pv-btn-frost">{form.heroSecondaryButtonLabel}</span>
              ) : null}
            </div>
          </div>
        </div>
      </section>

      {/* ── بخش ۱: پالت رنگ و حالت نمایش ── */}
      <section className="panel form-panel">
        <div className="panel-head">
          <div>
            <h3>۱. پالت رنگ و حالت نمایش مجله</h3>
            <p>تعیین ۴ رنگ اصلی قالب مجله و وضعیت حالت روز / شب برای بازدیدکنندگان.</p>
          </div>
        </div>

        <div className="blog-four-col-grid">
          <label>
            <span>رنگ اصلی هدر، هیرو و فوتر (آبی نفتی)</span>
            <div className="blog-color-picker-row">
              <input
                type="color"
                value={form.primaryDeepColor}
                onChange={(e) => updateField('primaryDeepColor', e.target.value)}
              />
              <input
                className="ltr-input"
                type="text"
                maxLength={7}
                value={form.primaryDeepColor}
                onChange={(e) => updateField('primaryDeepColor', e.target.value)}
              />
            </div>
          </label>

          <label>
            <span>رنگ طلایی تاکیدی (لوگو، کلمه برجسته و دکمه اصلی)</span>
            <div className="blog-color-picker-row">
              <input
                type="color"
                value={form.secondaryGoldColor}
                onChange={(e) => updateField('secondaryGoldColor', e.target.value)}
              />
              <input
                className="ltr-input"
                type="text"
                maxLength={7}
                value={form.secondaryGoldColor}
                onChange={(e) => updateField('secondaryGoldColor', e.target.value)}
              />
            </div>
          </label>

          <label>
            <span>رنگ سرخابی برند (خط زیر عنوان، بج‌ها و لینک‌ها)</span>
            <div className="blog-color-picker-row">
              <input
                type="color"
                value={form.accentMagentaColor}
                onChange={(e) => updateField('accentMagentaColor', e.target.value)}
              />
              <input
                className="ltr-input"
                type="text"
                maxLength={7}
                value={form.accentMagentaColor}
                onChange={(e) => updateField('accentMagentaColor', e.target.value)}
              />
            </div>
          </label>

          <label>
            <span>رنگ پس‌زمینه بدنه مجله در حالت روز</span>
            <div className="blog-color-picker-row">
              <input
                type="color"
                value={form.pageBackgroundColor}
                onChange={(e) => updateField('pageBackgroundColor', e.target.value)}
              />
              <input
                className="ltr-input"
                type="text"
                maxLength={7}
                value={form.pageBackgroundColor}
                onChange={(e) => updateField('pageBackgroundColor', e.target.value)}
              />
            </div>
          </label>
        </div>

        <div className="field-grid">
          <label>
            <span>حالت نمایش پیش‌فرض برای بازدیدکنندگان جدید</span>
            <select
              value={form.defaultColorMode}
              onChange={(e) => updateField('defaultColorMode', e.target.value)}
            >
              <option value="light">حالت روشن استاندارد (هدر نفتی + بدنه کرم روشن)</option>
              <option value="dark">حالت تیره شبانه (سرمه‌ای تیره یکپارچه)</option>
            </select>
          </label>

          <label className="checkbox-row" style={{ alignSelf: 'end', paddingBottom: '10px' }}>
            <input
              type="checkbox"
              checked={form.showThemeToggle}
              onChange={(e) => updateField('showThemeToggle', e.target.checked)}
            />
            <span>نمایش دکمه تغییر حالت روز / شب (◐) در هدر مجله</span>
          </label>
        </div>
      </section>

      {/* ── بخش ۲: برندینگ و نوار بالای مجله (هدر) ── */}
      <section className="panel form-panel">
        <div className="panel-head">
          <div>
            <h3>۲. نوار بالای مجله و برندینگ هدر</h3>
            <p>مشخصات لوگو، نام برند، متن کادر جستجو و دکمه بازگشت به منوی کافه که بالای تمام صفحات مجله ثابت است.</p>
          </div>
        </div>

        <div className="field-grid">
          <label>
            <span>حرف نماد داخل مربع طلایی لوگو (حداکثر ۴ کاراکتر)</span>
            <input
              type="text"
              maxLength={4}
              placeholder="S"
              value={form.brandMonogram}
              onChange={(e) => updateField('brandMonogram', e.target.value)}
            />
          </label>

          <label>
            <span>عنوان اصلی برند در هدر مجله</span>
            <input
              type="text"
              maxLength={80}
              placeholder="Sugarista"
              value={form.headerBrandTitle}
              onChange={(e) => updateField('headerBrandTitle', e.target.value)}
            />
          </label>
        </div>

        <div className="field-grid">
          <label>
            <span>زیرعنوان برند در هدر مجله (زیر نام برند)</span>
            <input
              type="text"
              maxLength={140}
              placeholder="کافه و قنادی تخصصی شوگاریستا"
              value={form.headerBrandSubtitle}
              onChange={(e) => updateField('headerBrandSubtitle', e.target.value)}
            />
          </label>

          <label>
            <span>عنوان لینک «صفحه اول مجله» در منوی بالا</span>
            <input
              type="text"
              maxLength={50}
              value={form.headerHomeLinkLabel}
              onChange={(e) => updateField('headerHomeLinkLabel', e.target.value)}
            />
          </label>
        </div>

        <ImageUploader
          label="تصویر لوگو اختصاصی در هدر مجله (اختیاری — در صورت بارگذاری جایگزین حرف نماد طلایی می‌شود)"
          hint="PNG یا WebP مربعی"
          value={form.brandLogoImageUrl}
          token={token}
          onChange={(url) => updateField('brandLogoImageUrl', url ?? '')}
        />

        <div className="blog-three-col-grid">
          <label>
            <span>متن راهنمای داخل کادر جستجوی مقالات</span>
            <input
              type="text"
              maxLength={90}
              value={form.headerSearchPlaceholder}
              onChange={(e) => updateField('headerSearchPlaceholder', e.target.value)}
            />
          </label>

          <label>
            <span>متن دکمه طلایی سمت چپ هدر</span>
            <input
              type="text"
              maxLength={50}
              value={form.headerCtaLabel}
              onChange={(e) => updateField('headerCtaLabel', e.target.value)}
            />
          </label>

          <label>
            <span>آدرس لینک دکمه طلایی هدر</span>
            <input
              className="ltr-input"
              type="text"
              maxLength={200}
              placeholder="/"
              value={form.headerCtaUrl}
              onChange={(e) => updateField('headerCtaUrl', e.target.value)}
            />
          </label>
        </div>
      </section>

      {/* ── بخش ۳: بنر اصلی بالای صفحه اول مجله (بخش هیرو) ── */}
      <section className="panel form-panel">
        <div className="panel-head">
          <div>
            <h3>۳. بنر اصلی بالای صفحه اول مجله (بخش هیرو)</h3>
            <p>تنظیم عنوان بزرگ خوش‌آمدگویی، کلمه طلایی با خط سرخابی، توضیحات و دکمه‌های بنر اول صفحه /mag.</p>
          </div>
        </div>

        <label>
          <span>متن کپسول بالای عنوان اصلی (در صورت خالی بودن نمایش داده نمی‌شود)</span>
          <input
            type="text"
            maxLength={160}
            placeholder="داستان دانه‌های قهوه، شیرینی‌های دست‌ساز و لحظه‌های شیرین"
            value={form.heroBadgeText}
            onChange={(e) => updateField('heroBadgeText', e.target.value)}
          />
        </label>

        <div className="blog-three-col-grid">
          <label>
            <span>بخش اول عنوان بزرگ هیرو (سفید)</span>
            <input
              type="text"
              maxLength={120}
              placeholder="مجله طعم و زندگی"
              value={form.heroTitlePrefix}
              onChange={(e) => updateField('heroTitlePrefix', e.target.value)}
            />
          </label>

          <label>
            <span>کلمه برجسته طلایی (با خط سرخابی زیر آن)</span>
            <input
              type="text"
              maxLength={80}
              placeholder="شوگاریستا"
              value={form.heroTitleHighlight}
              onChange={(e) => updateField('heroTitleHighlight', e.target.value)}
            />
          </label>

          <label>
            <span>ادامه عنوان بعد از کلمه طلایی (اختیاری)</span>
            <input
              type="text"
              maxLength={100}
              placeholder="اختیاری"
              value={form.heroTitleSuffix}
              onChange={(e) => updateField('heroTitleSuffix', e.target.value)}
            />
          </label>
        </div>

        <label>
          <span>توضیحات زیر عنوان اصلی در هیرو</span>
          <textarea
            rows={2}
            maxLength={400}
            value={form.heroSubtitle}
            onChange={(e) => updateField('heroSubtitle', e.target.value)}
          />
        </label>

        <div className="blog-four-col-grid">
          <label>
            <span>متن دکمه اول هیرو (طلایی)</span>
            <input
              type="text"
              maxLength={60}
              value={form.heroPrimaryButtonLabel}
              onChange={(e) => updateField('heroPrimaryButtonLabel', e.target.value)}
            />
          </label>

          <label>
            <span>لینک دکمه اول هیرو</span>
            <input
              className="ltr-input"
              type="text"
              maxLength={200}
              value={form.heroPrimaryButtonUrl}
              onChange={(e) => updateField('heroPrimaryButtonUrl', e.target.value)}
            />
          </label>

          <label>
            <span>متن دکمه دوم هیرو (شیشه‌ای — اختیاری)</span>
            <input
              type="text"
              maxLength={60}
              value={form.heroSecondaryButtonLabel}
              onChange={(e) => updateField('heroSecondaryButtonLabel', e.target.value)}
            />
          </label>

          <label>
            <span>لینک دکمه دوم هیرو</span>
            <input
              className="ltr-input"
              type="text"
              maxLength={200}
              value={form.heroSecondaryButtonUrl}
              onChange={(e) => updateField('heroSecondaryButtonUrl', e.target.value)}
            />
          </label>
        </div>

        <label className="checkbox-row">
          <input
            type="checkbox"
            checked={form.showHeroStats}
            onChange={(e) => updateField('showHeroStats', e.target.checked)}
          />
          <span>نمایش باکس‌های شمارنده «تعداد مقالات منتشرشده» و «تعداد موضوعات» در بنر هیرو</span>
        </label>
      </section>

      {/* ── بخش ۴: چیدمان صفحه اول، نوار دسته‌ها، مقاله منتخب و لیست مقالات ── */}
      <section className="panel form-panel">
        <div className="panel-head">
          <div>
            <h3>۴. بخش‌های میانی صفحه اول مجله (دسته‌بندی‌ها، کارت‌های ویژه، مقاله منتخب و آرشیو)</h3>
            <p>مدیریت عنوان‌ها، تعداد کارت در هر صفحه و فعال یا مخفی کردن هر بخش در صفحه اصلی مجله.</p>
          </div>
        </div>

        <div className="blog-switches-grid">
          <label className="checkbox-row">
            <input
              type="checkbox"
              checked={form.showCategoryRail}
              onChange={(e) => updateField('showCategoryRail', e.target.checked)}
            />
            <span>نمایش نوار شناور فیلتر دسته‌بندی‌ها و جستجو زیر بنر بالا</span>
          </label>

          <label className="checkbox-row">
            <input
              type="checkbox"
              checked={form.showHighlightsSection}
              onChange={(e) => updateField('showHighlightsSection', e.target.checked)}
            />
            <span>نمایش بخش «کارت‌های ویژه و نکات کوتاه» در صفحه اول</span>
          </label>

          <label className="checkbox-row">
            <input
              type="checkbox"
              checked={form.showFeaturedSection}
              onChange={(e) => updateField('showFeaturedSection', e.target.checked)}
            />
            <span>نمایش بنر بزرگ «مقاله منتخب» بالای شبکه مقالات</span>
          </label>
        </div>

        <div className="blog-three-col-grid">
          <label>
            <span>متن دکمه «همه مطالب» در نوار دسته‌بندی</span>
            <input
              type="text"
              maxLength={50}
              value={form.allCategoriesChipLabel}
              onChange={(e) => updateField('allCategoriesChipLabel', e.target.value)}
            />
          </label>

          <label>
            <span>متن برچسب روی مقاله منتخب (سرخابی)</span>
            <input
              type="text"
              maxLength={60}
              value={form.featuredBadgeLabel}
              onChange={(e) => updateField('featuredBadgeLabel', e.target.value)}
            />
          </label>

          <label>
            <span>متن دکمه مطالعه در مقاله منتخب</span>
            <input
              type="text"
              maxLength={60}
              value={form.featuredReadButtonLabel}
              onChange={(e) => updateField('featuredReadButtonLabel', e.target.value)}
            />
          </label>
        </div>

        <div className="blog-four-col-grid">
          <label>
            <span>زیرعنوان بالای بخش کارت‌های ویژه</span>
            <input
              type="text"
              maxLength={80}
              value={form.highlightsSectionEyebrow}
              onChange={(e) => updateField('highlightsSectionEyebrow', e.target.value)}
            />
          </label>

          <label>
            <span>عنوان بخش کارت‌های ویژه</span>
            <input
              type="text"
              maxLength={120}
              value={form.highlightsSectionTitle}
              onChange={(e) => updateField('highlightsSectionTitle', e.target.value)}
            />
          </label>

          <label>
            <span>زیرعنوان بالای لیست مقالات</span>
            <input
              type="text"
              maxLength={80}
              value={form.archiveSectionEyebrow}
              onChange={(e) => updateField('archiveSectionEyebrow', e.target.value)}
            />
          </label>

          <label>
            <span>عنوان اصلی لیست مقالات</span>
            <input
              type="text"
              maxLength={120}
              value={form.archiveSectionTitle}
              onChange={(e) => updateField('archiveSectionTitle', e.target.value)}
            />
          </label>
        </div>

        <div className="field-grid">
          <label>
            <span>تعداد کارت مقاله در هر صفحه (بین ۳ تا ۲۴)</span>
            <input
              className="ltr-input"
              type="number"
              min={3}
              max={24}
              value={form.postsPerPage}
              onChange={(e) => updateField('postsPerPage', e.target.value)}
            />
          </label>

          <label>
            <span>متن لینک ادامه مطلب روی هر کارت مقاله</span>
            <input
              type="text"
              maxLength={50}
              value={form.cardReadMoreLabel}
              onChange={(e) => updateField('cardReadMoreLabel', e.target.value)}
            />
          </label>
        </div>
      </section>

      {/* ── بخش ۵: صفحه داخلی مطالعه مقاله (/mag/slug) ── */}
      <section className="panel form-panel">
        <div className="panel-head">
          <div>
            <h3>۵. تنظیمات صفحه داخلی مطالعه هر مقاله</h3>
            <p>کنترل اجزای کمکی مطالعه، فهرست مطالب (TOC)، پیشنهاد محصولات منو و سوالات متداول در صفحه تکی مقالات.</p>
          </div>
        </div>

        <div className="blog-switches-grid">
          <label className="checkbox-row">
            <input
              type="checkbox"
              checked={form.showReadingProgressBar}
              onChange={(e) => updateField('showReadingProgressBar', e.target.checked)}
            />
            <span>نمایش نوار رنگی پیشرفت مطالعه در بالای صفحه مقاله</span>
          </label>

          <label className="checkbox-row">
            <input
              type="checkbox"
              checked={form.showTableOfContents}
              onChange={(e) => updateField('showTableOfContents', e.target.checked)}
            />
            <span>نمایش باکس «فهرست سرفصل‌های مقاله» در کنار متن</span>
          </label>

          <label className="checkbox-row">
            <input
              type="checkbox"
              checked={form.showViewCount}
              onChange={(e) => updateField('showViewCount', e.target.checked)}
            />
            <span>نمایش شمارنده تعداد بازدید بالای مقاله</span>
          </label>

          <label className="checkbox-row">
            <input
              type="checkbox"
              checked={form.showShareButtons}
              onChange={(e) => updateField('showShareButtons', e.target.checked)}
            />
            <span>نمایش دکمه‌های اشتراک‌گذاری (کپی لینک، تلگرام، واتساپ، X)</span>
          </label>
        </div>

        <div className="blog-three-col-grid">
          <label>
            <span>عنوان باکس فهرست سرفصل‌ها (TOC)</span>
            <input
              type="text"
              maxLength={90}
              value={form.tocTitle}
              onChange={(e) => updateField('tocTitle', e.target.value)}
            />
          </label>

          <label>
            <span>عنوان بخش سوالات متداول پایین مقاله</span>
            <input
              type="text"
              maxLength={100}
              value={form.faqSectionTitle}
              onChange={(e) => updateField('faqSectionTitle', e.target.value)}
            />
          </label>

          <label>
            <span>عنوان بخش مقالات مشابه در انتهای مقاله</span>
            <input
              type="text"
              maxLength={120}
              value={form.relatedPostsTitle}
              onChange={(e) => updateField('relatedPostsTitle', e.target.value)}
            />
          </label>
        </div>

        <div className="field-grid">
          <label>
            <span>زیرعنوان بخش پیشنهاد آیتم‌های منوی کافه در مقاله</span>
            <input
              type="text"
              maxLength={80}
              value={form.relatedMenuEyebrow}
              onChange={(e) => updateField('relatedMenuEyebrow', e.target.value)}
            />
          </label>

          <label>
            <span>عنوان اصلی بخش پیشنهاد آیتم‌های منوی کافه در مقاله</span>
            <input
              type="text"
              maxLength={120}
              value={form.relatedMenuTitle}
              onChange={(e) => updateField('relatedMenuTitle', e.target.value)}
            />
          </label>
        </div>
      </section>

      {/* ── بخش ۶: بنر دعوت به منوی کافه ── */}
      <section className="panel form-panel">
        <div className="panel-head">
          <div>
            <h3>۶. بنر دعوت به سفارش از منوی کافه (پایین مجله)</h3>
            <p>بنر تبدیلی پایین صفحات مجله برای هدایت خوانندگان مقالات به ثبت سفارش از منوی دیجیتال.</p>
          </div>
        </div>

        <label className="checkbox-row">
          <input
            type="checkbox"
            checked={form.showCafeCtaBanner}
            onChange={(e) => updateField('showCafeCtaBanner', e.target.checked)}
          />
          <span>نمایش بنر دعوت به منوی کافه در پایین صفحات مجله</span>
        </label>

        <div className="field-grid">
          <label>
            <span>متن برچسب طلایی بالای بنر دعوت</span>
            <input
              type="text"
              maxLength={90}
              value={form.cafeCtaEyebrow}
              onChange={(e) => updateField('cafeCtaEyebrow', e.target.value)}
            />
          </label>

          <label>
            <span>عنوان اصلی بنر دعوت به کافه</span>
            <input
              type="text"
              maxLength={160}
              value={form.cafeCtaTitle}
              onChange={(e) => updateField('cafeCtaTitle', e.target.value)}
            />
          </label>
        </div>

        <label>
          <span>توضیحات بنر دعوت به کافه</span>
          <textarea
            rows={2}
            maxLength={360}
            value={form.cafeCtaDescription}
            onChange={(e) => updateField('cafeCtaDescription', e.target.value)}
          />
        </label>

        <div className="field-grid">
          <label>
            <span>متن دکمه بنر دعوت به کافه</span>
            <input
              type="text"
              maxLength={60}
              value={form.cafeCtaButtonLabel}
              onChange={(e) => updateField('cafeCtaButtonLabel', e.target.value)}
            />
          </label>

          <label>
            <span>لینک دکمه بنر دعوت به کافه</span>
            <input
              className="ltr-input"
              type="text"
              maxLength={200}
              value={form.cafeCtaButtonUrl}
              onChange={(e) => updateField('cafeCtaButtonUrl', e.target.value)}
            />
          </label>
        </div>

        <ImageUploader
          label="تصویر تزئینی کنار بنر دعوت به کافه (اختیاری)"
          hint="تصویر مربعی از قهوه یا شیرینی"
          value={form.cafeCtaImageUrl}
          token={token}
          onChange={(url) => updateField('cafeCtaImageUrl', url ?? '')}
        />
      </section>

      {/* ── بخش ۷: فوتر مجله و سئو پایه صفحه اول ── */}
      <section className="panel form-panel">
        <div className="panel-head">
          <div>
            <h3>۷. فوتر مجله و تنظیمات سئو صفحه اول (/mag)</h3>
            <p>متون فوتر پایین مجله و عنوان/توضیحات متا اختصاصی صفحه اصلی مجله در نتایج گوگل.</p>
          </div>
        </div>

        <label>
          <span>متن معرفی کوتاه مجله در فوتر</span>
          <textarea
            rows={2}
            maxLength={360}
            value={form.footerDescription}
            onChange={(e) => updateField('footerDescription', e.target.value)}
          />
        </label>

        <div className="blog-three-col-grid">
          <label>
            <span>عنوان ستون دسته‌بندی‌ها در فوتر</span>
            <input
              type="text"
              maxLength={70}
              value={form.footerCategoriesHeading}
              onChange={(e) => updateField('footerCategoriesHeading', e.target.value)}
            />
          </label>

          <label>
            <span>عنوان ستون لینک‌های سریع در فوتر</span>
            <input
              type="text"
              maxLength={70}
              value={form.footerQuickLinksHeading}
              onChange={(e) => updateField('footerQuickLinksHeading', e.target.value)}
            />
          </label>

          <label>
            <span>متن کپی‌رایت پایین فوتر</span>
            <input
              type="text"
              maxLength={180}
              value={form.footerCopyrightText}
              onChange={(e) => updateField('footerCopyrightText', e.target.value)}
            />
          </label>
        </div>

        <div className="field-grid">
          <label>
            <span>عنوان سئو صفحه اول مجله در گوگل (اختیاری — در صورت خالی بودن خودکار ساخته می‌شود)</span>
            <input
              type="text"
              maxLength={160}
              placeholder="مجله طعم و زندگی شوگاریستا | راهنمای قهوه و قنادی"
              value={form.homeMetaTitle}
              onChange={(e) => updateField('homeMetaTitle', e.target.value)}
            />
          </label>

          <label>
            <span>توضیحات متا سئو صفحه اول مجله (اختیاری)</span>
            <input
              type="text"
              maxLength={320}
              placeholder="آموزش تخصصی دم‌آوری قهوه، شناخت دانه‌ها و هنر قنادی در مجله شوگاریستا."
              value={form.homeMetaDescription}
              onChange={(e) => updateField('homeMetaDescription', e.target.value)}
            />
          </label>
        </div>
      </section>

      {/* ── بخش ۸: لینک‌های سفارشی منوی بالا و فوتر مجله ── */}
      <section className="panel form-panel">
        <div className="panel-head">
          <div>
            <h3>۸. لینک‌های دلخواه در هدر و فوتر مجله</h3>
            <p>می‌توانید لینک‌های سفارشی (مثل باشگاه مشتریان، اینستاگرام، درباره ما و...) را به منوی بالای مجله یا فوتر اضافه کنید.</p>
          </div>
          {canEdit ? (
            <button type="button" className="ghost-button" onClick={handleAddCustomLink}>
              + افزودن لینک سفارشی
            </button>
          ) : null}
        </div>

        {form.customLinks.length === 0 ? (
          <p className="blog-field-hint">
            هنوز لینک سفارشی اضافه نشده است. دسته‌بندی‌های فعال مجله به‌صورت خودکار در هدر و فوتر نمایش داده می‌شوند.
          </p>
        ) : (
          <div className="blog-custom-links-list">
            {form.customLinks.map((link, idx) => (
              <div key={idx} className="blog-custom-link-row">
                <label>
                  <span>عنوان لینک</span>
                  <input
                    type="text"
                    maxLength={60}
                    placeholder="مثلاً: باشگاه مشتریان"
                    value={link.label}
                    onChange={(e) => handleUpdateCustomLink(idx, 'label', e.target.value)}
                  />
                </label>

                <label>
                  <span>آدرس مقصد (URL)</span>
                  <input
                    className="ltr-input"
                    type="text"
                    maxLength={300}
                    placeholder="/ یا https://..."
                    value={link.url}
                    onChange={(e) => handleUpdateCustomLink(idx, 'url', e.target.value)}
                  />
                </label>

                <label>
                  <span>محل نمایش</span>
                  <select
                    value={link.placement}
                    onChange={(e) => handleUpdateCustomLink(idx, 'placement', e.target.value)}
                  >
                    <option value="header">فقط در هدر بالا</option>
                    <option value="footer">فقط در فوتر پایین</option>
                    <option value="both">هم در هدر و هم در فوتر</option>
                  </select>
                </label>

                <label>
                  <span>ترتیب</span>
                  <input
                    className="ltr-input"
                    type="number"
                    min={0}
                    value={link.displayOrder}
                    onChange={(e) => handleUpdateCustomLink(idx, 'displayOrder', e.target.value)}
                  />
                </label>

                <button
                  type="button"
                  className="link-button link-danger"
                  style={{ alignSelf: 'end', marginBottom: '8px' }}
                  onClick={() => handleRemoveCustomLink(idx)}
                >
                  حذف
                </button>
              </div>
            ))}
          </div>
        )}

        {canEdit ? (
          <div className="blog-sticky-save-bar">
            <button type="submit" className="blog-primary-btn" disabled={saving}>
              {saving ? 'در حال ذخیره تنظیمات...' : 'ذخیره تمام تنظیمات ظاهر و متن‌های مجله'}
            </button>
          </div>
        ) : null}
      </section>
    </form>
  )
}
