import { useEffect, useState } from 'react'
import { useAdmin } from '../context/AdminContext'
import { settingsSections } from '../constants/adminForms'

function SectionIcon({ name }) {
  const paths = {
    cart: (
      <>
        <circle cx="8" cy="21" r="1" />
        <circle cx="19" cy="21" r="1" />
        <path d="M2.05 2.05h2l2.66 12.42a2 2 0 0 0 2 1.58h9.78a2 2 0 0 0 1.95-1.57l1.65-7.43H5.12" />
      </>
    ),
    brand: (
      <>
        <path d="M12 2C6.5 2 2 6.5 2 12s4.5 10 10 10c.93 0 1.65-.75 1.65-1.69 0-.44-.18-.83-.44-1.12-.29-.29-.44-.65-.44-1.13a1.64 1.64 0 0 1 1.67-1.67h2c3.05 0 5.56-2.5 5.56-5.55C21.97 6.01 17.46 2 12 2z" />
        <circle cx="13.5" cy="6.5" r="0.6" />
        <circle cx="17.5" cy="10.5" r="0.6" />
        <circle cx="8.5" cy="7.5" r="0.6" />
        <circle cx="6.5" cy="12.5" r="0.6" />
      </>
    ),
    hero: <polygon points="12 2 15.09 8.26 22 9.27 17 14.14 18.18 21.02 12 17.77 5.82 21.02 7 14.14 2 9.27 8.91 8.26 12 2" />,
    steps: (
      <>
        <path d="M10 6h11" />
        <path d="M10 12h11" />
        <path d="M10 18h11" />
        <path d="M4 6h1v4" />
        <path d="M4 10h2" />
        <path d="M6 18H4c0-1 2-2 2-3s-1-1.5-2-1" />
      </>
    ),
    content: (
      <>
        <rect x="3" y="3" width="7" height="7" rx="1.5" />
        <rect x="14" y="3" width="7" height="7" rx="1.5" />
        <rect x="14" y="14" width="7" height="7" rx="1.5" />
        <rect x="3" y="14" width="7" height="7" rx="1.5" />
      </>
    ),
    contact: (
      <path d="M22 16.92v3a2 2 0 0 1-2.18 2 19.79 19.79 0 0 1-8.63-3.07 19.5 19.5 0 0 1-6-6 19.79 19.79 0 0 1-3.07-8.67A2 2 0 0 1 4.11 2h3a2 2 0 0 1 2 1.72 12.84 12.84 0 0 0 .7 2.81 2 2 0 0 1-.45 2.11L8.09 9.91a16 16 0 0 0 6 6l1.27-1.27a2 2 0 0 1 2.11-.45 12.84 12.84 0 0 0 2.81.7A2 2 0 0 1 22 16.92z" />
    ),
    theme: (
      <>
        <circle cx="12" cy="12" r="9" />
        <path d="M12 3a9 9 0 0 0 0 18c.55 0 1-.45 1-1 0-.26-.11-.5-.29-.68a1 1 0 0 1 0-1.41c.18-.18.42-.29.68-.29h1.61A5 5 0 0 0 20 13c0-2.76-2.24-5-5-5h-3V3z" />
      </>
    ),
    bell: (
      <>
        <path d="M18 8a6 6 0 0 0-12 0c0 7-3 9-3 9h18s-3-2-3-9" />
        <path d="M13.73 21a2 2 0 0 1-3.46 0" />
      </>
    ),
    clock: (
      <>
        <circle cx="12" cy="12" r="9" />
        <path d="M12 7v5l3 2" />
      </>
    ),
  }

  return (
    <svg
      className="settings-section-icon"
      viewBox="0 0 24 24"
      fill="none"
      stroke="currentColor"
      strokeWidth="1.7"
      strokeLinecap="round"
      strokeLinejoin="round"
      aria-hidden="true"
    >
      {paths[name] ?? paths.content}
    </svg>
  )
}

function scrollToSection(id) {
  const target = document.getElementById(id)
  if (target) target.scrollIntoView({ behavior: 'smooth', block: 'start' })
}

export function SiteSettingsPage() {
  const {
    siteSettings,
    setSiteSettings,
    saving,
    handleSettingsSubmit,
  } = useAdmin()

  const updateField = (fieldName, value) =>
    setSiteSettings((currentSettings) => ({
      ...currentSettings,
      [fieldName]: value,
    }))

  const salesPaused = siteSettings.isOnlineSalesEnabled === false

  // ── بسته‌های شارژ کیف پول ────────────────────────────────
  const [walletPackages, setWalletPackages] = useState([])
  const [packagesSaving, setPackagesSaving] = useState(false)
  const [packagesMessage, setPackagesMessage] = useState('')
  const [packagesError, setPackagesError] = useState('')
  const [removedPackageIds, setRemovedPackageIds] = useState([])
  const [cupCategoryOptions, setCupCategoryOptions] = useState([])

  const loadPackages = async () => {
    if (!session?.token) return
    try {
      const data = await apiRequest('/admin/wallet-packages', { token: session.token })
      setWalletPackages((Array.isArray(data) ? data : []).map((row) => ({
        id: row.id,
        amount: row.amount ?? 0,
        bonusAmount: row.bonusAmount ?? 0,
        displayOrder: row.displayOrder ?? 0,
        isEnabled: row.isEnabled !== false,
      })))
    } catch (requestError) {
      setPackagesError(getReadableError(requestError))
    }
  }

  const updatePackageRow = (index, field, value) =>
    setWalletPackages((current) => current.map((row, position) => (position === index ? { ...row, [field]: value } : row)))

  const addPackageRow = () =>
    setWalletPackages((current) => [
      ...current,
      { id: '', amount: 0, bonusAmount: 0, displayOrder: current.length, isEnabled: true },
    ])

  const removePackageRow = (index) => {
    const target = walletPackages[index]
    if (target?.id) {
      setRemovedPackageIds((existing) => (existing.includes(target.id) ? existing : [...existing, target.id]))
    }
    setWalletPackages((current) => current.filter((_, position) => position !== index))
  }

  const savePackages = async () => {
    setPackagesError('')
    setPackagesMessage('')
    if (walletPackages.some((row) => !(Number(row.amount) > 0))) {
      setPackagesError('مبلغ همهٔ بسته‌ها را مثبت وارد کنید.')
      return
    }
    setPackagesSaving(true)
    try {
      for (const row of walletPackages) {
        const payload = {
          id: row.id || '00000000-0000-0000-0000-000000000000',
          amount: Number(row.amount) || 0,
          bonusAmount: Number(row.bonusAmount) || 0,
          displayOrder: Number(row.displayOrder) || 0,
          isEnabled: row.isEnabled,
        }
        await apiRequest('/admin/wallet-packages', { method: 'POST', body: payload, token: session.token })
      }
      const loadedNow = await apiRequest('/admin/wallet-packages', { token: session.token })
      const keepIds = new Set((Array.isArray(loadedNow) ? loadedNow : []).map((row) => row.id))
      const removedIds = removedPackageIds.filter((id) => !keepIds.has(id))
      for (const id of removedIds) {
        await apiRequest(`/admin/wallet-packages/${id}`, { method: 'DELETE', token: session.token })
      }
      await loadPackages()
      setRemovedPackageIds([])
      setPackagesMessage('بسته‌های شارژ ذخیره شد.')
    } catch (requestError) {
      setPackagesError(getReadableError(requestError))
    } finally {
      setPackagesSaving(false)
    }
  }

  // ── سطح‌بندی عضویت ──────────────────────────────────────
  const [tierRows, setTierRows] = useState([])
  const [originalTierIds, setOriginalTierIds] = useState([])
  const [tierMessage, setTierMessage] = useState('')
  const [tierError, setTierError] = useState('')
  const [tiersSaving, setTiersSaving] = useState(false)

  const { session, apiRequest, getReadableError } = useAdmin()

  const loadTiers = async () => {
    if (!session?.token) return
    try {
      const data = await apiRequest('/admin/membership-tiers', { token: session.token })
      const rows = Array.isArray(data) ? data : []
      setTierRows(rows.map((row) => ({
        id: row.id,
        name: row.name ?? '',
        description: row.description ?? '',
        minTotalPaidSpent: row.minTotalPaidSpent ?? 0,
        minPaidOrders: row.minPaidOrders ?? 0,
        discountPercent: row.discountPercent ?? 0,
        cashbackPercent: row.cashbackPercent ?? '',
        displayOrder: row.displayOrder ?? 0,
        isEnabled: row.isEnabled !== false,
      })))
      setOriginalTierIds(rows.map((row) => row.id))
    } catch (requestError) {
      setTierError(getReadableError(requestError))
    }
  }

  useEffect(() => {
    loadTiers()
    loadPackages()

    const loadCupCategories = async () => {
      if (!session?.token) return
      try {
        const data = await apiRequest('/admin/categories/options', { token: session.token })
        setCupCategoryOptions(Array.isArray(data) ? data : [])
      } catch {
        setCupCategoryOptions([])
      }
    }
    loadCupCategories()
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [session?.token])

  const updateTierRow = (index, field, value) =>
    setTierRows((current) => current.map((row, position) => (position === index ? { ...row, [field]: value } : row)))

  const addTierRow = () =>
    setTierRows((current) => [
      ...current,
      { id: '', name: '', description: '', minTotalPaidSpent: 0, minPaidOrders: 0, discountPercent: 0, cashbackPercent: '', displayOrder: current.length, isEnabled: true },
    ])

  const removeTierRow = (index) =>
    setTierRows((current) => current.filter((_, position) => position !== index))

  const saveTiers = async () => {
    setTierError('')
    setTierMessage('')
    if (tierRows.some((row) => !row.name.trim())) {
      setTierError('نام همهٔ سطح‌ها را وارد کنید.')
      return
    }
    setTiersSaving(true)
    try {
      for (const row of tierRows) {
        const payload = {
          id: row.id || '00000000-0000-0000-0000-000000000000',
          name: row.name.trim(),
          description: row.description?.trim() || null,
          minTotalPaidSpent: Number(row.minTotalPaidSpent) || 0,
          minPaidOrders: Number(row.minPaidOrders) || 0,
          discountPercent: Number(row.discountPercent) || 0,
          cashbackPercent:
            row.cashbackPercent === '' || row.cashbackPercent === null || row.cashbackPercent === undefined
              ? null
              : Number(row.cashbackPercent),
          displayOrder: Number(row.displayOrder) || 0,
          isEnabled: row.isEnabled,
        }
        if (row.id) {
          await apiRequest(`/admin/membership-tiers/${row.id}`, { method: 'PUT', body: payload, token: session.token })
        } else {
          await apiRequest('/admin/membership-tiers', { method: 'POST', body: payload, token: session.token })
        }
      }
      for (const originalId of originalTierIds) {
        if (!tierRows.some((row) => row.id === originalId)) {
          await apiRequest(`/admin/membership-tiers/${originalId}`, { method: 'DELETE', token: session.token })
        }
      }
      await loadTiers()
      setTierMessage('سطح‌های عضویت ذخیره شد.')
    } catch (requestError) {
      setTierError(getReadableError(requestError))
    } finally {
      setTiersSaving(false)
    }
  }

  const navItems = [
    { id: 'settings-sales', title: 'فروش آنلاین', icon: 'cart' },
    { id: 'settings-pickup', title: 'سفارش پیش از مراجعه', icon: 'clock' },
    { id: 'settings-abandoned-cart', title: 'سبد رهاشده', icon: 'bell' },
    { id: 'settings-loyalty', title: 'باشگاه مشتریان', icon: 'brand' },
    { id: 'settings-theme', title: 'تم و نمایش', icon: 'theme' },
    ...settingsSections.map((section) => ({
      id: `settings-${section.id}`,
      title: section.title,
      icon: section.icon,
    })),
  ]

  return (
    <div className="settings-page">
      <header className="settings-header">
        <div>
          <p className="settings-eyebrow">Site Settings</p>
          <h1>تنظیمات سایت</h1>
          <p className="settings-header-sub">
            ظاهر منوی عمومی، محتوای صفحه و وضعیت فروش آنلاین را اینجا مدیریت کنید.
          </p>
        </div>
        <span className={`settings-status ${salesPaused ? 'is-off' : 'is-on'}`}>
          <span className="settings-status-dot" />
          فروش آنلاین {salesPaused ? 'غیرفعال' : 'فعال'}
        </span>
      </header>

      <nav className="settings-nav" aria-label="بخش‌های تنظیمات">
        {navItems.map((item) => (
          <button
            key={item.id}
            type="button"
            className="settings-nav-chip"
            onClick={() => scrollToSection(item.id)}
          >
            <SectionIcon name={item.icon} />
            {item.title}
          </button>
        ))}
      </nav>

      <form className="settings-form" onSubmit={handleSettingsSubmit}>
        {/* ---------- فروش آنلاین ---------- */}
        <section id="settings-sales" className="panel settings-section">
          <div className="panel-head">
            <div className="settings-section-head">
              <span className="settings-section-badge">
                <SectionIcon name="cart" />
              </span>
              <div>
                <h2>
                  فروش آنلاین
                  <span className={`settings-pill ${salesPaused ? 'is-off' : 'is-on'}`}>
                    {salesPaused ? 'غیرفعال' : 'فعال'}
                  </span>
                </h2>
                <p>کامل کنترل سفارش و پرداخت آنلاین روی سایت مشتریان</p>
              </div>
            </div>
          </div>

          <div className="settings-sales-body">
            {siteSettings.isOnlineOrderingAvailable === false ? (
              <p className="feedback feedback-error" role="status">
                درگاه پرداخت آنلاین پیکربندی نشده است؛ تا زمان اتصال درگاه واقعی، سفارش آنلاین در
                محیط عملیاتی غیرفعال می‌ماند.
              </p>
            ) : null}
            <label className="settings-switch-row">
              <div className="settings-switch-text">
                <strong>توقف فروش آنلاین</strong>
                <p>
                  با فعال کردن این گزینه، فروش آنلاین روی سایت به‌صورت کامل قطع می‌شود:
                </p>
                <ul className="settings-effects">
                  <li>دکمه «افزودن به سبد خرید» روی کارت محصولات محو می‌شود</li>
                  <li>نوار و کشوی سبد خرید از سایت حذف می‌شود</li>
                  <li>ثبت سفارش آنلاین در سرور نیز مسدود می‌شود</li>
                  <li>بنر اطلاع‌رسانی بالای منو برای مشتریان نمایش داده می‌شود</li>
                </ul>
              </div>
              <span className={`switch ${salesPaused ? 'switch-on' : ''}`}>
                <input
                  type="checkbox"
                  checked={salesPaused}
                  onChange={(event) => updateField('isOnlineSalesEnabled', !event.target.checked)}
                />
                <span className="switch-track">
                  <span className="switch-thumb" />
                </span>
              </span>
            </label>

            {salesPaused && (
              <label className="settings-notice settings-fade-in">
                <span>
                  پیام اطلاع‌رسانی به مشتریان
                  <em>بنر بالای منوی مشتریان با این متن نمایش داده می‌شود.</em>
                </span>
                <textarea
                  rows="2"
                  maxLength="500"
                  placeholder="فروش آنلاین موقتاً غیرفعال است؛ می‌توانید حضوری سفارش دهید."
                  value={siteSettings.onlineSalesNotice ?? ''}
                  onChange={(event) => updateField('onlineSalesNotice', event.target.value)}
                />
              </label>
            )}
          </div>
        </section>

        {/* ---------- سفارش پیش از مراجعه ---------- */}
        <section id="settings-pickup" className="panel settings-section">
          <div className="panel-head">
            <div className="settings-section-head">
              <span className="settings-section-badge">
                <SectionIcon name="clock" />
              </span>
              <div>
                <h2>
                  سفارش پیش از مراجعه
                  <span
                    className={`settings-pill ${
                      siteSettings.pickupSchedulingEnabled ? 'is-on' : 'is-off'
                    }`}
                  >
                    {siteSettings.pickupSchedulingEnabled ? 'فعال' : 'غیرفعال'}
                  </span>
                </h2>
                <p>
                  مشتری زمان تحویل حضوری را انتخاب می‌کند؛ ظرفیت هر بازه، ساعت کاری و مهلت
                  لغو/تغییر از همین‌جا کنترل می‌شود.
                </p>
              </div>
            </div>
          </div>

          <div className="settings-grid">
            <label className="settings-switch-row full-width">
              <div className="settings-switch-text">
                <strong>فعال کردن زمان‌بندی تحویل حضوری</strong>
                <p>
                  با فعال شدن این گزینه، در مرحلهٔ پرداخت منوی مشتریان انتخاب روز و بازهٔ تحویل
                  نمایش داده می‌شود. اگر غیرفعال باشد، سفارش‌ها فقط به‌صورت «در اسرع وقت» ثبت
                  می‌شوند.
                </p>
              </div>
              <span className={`switch ${siteSettings.pickupSchedulingEnabled ? 'switch-on' : ''}`}>
                <input
                  type="checkbox"
                  checked={Boolean(siteSettings.pickupSchedulingEnabled)}
                  onChange={(event) => updateField('pickupSchedulingEnabled', event.target.checked)}
                />
                <span className="switch-track">
                  <span className="switch-thumb" />
                </span>
              </span>
            </label>

            <label>
              <span>طول هر بازه (دقیقه)</span>
              <input
                className="ltr-input"
                dir="ltr"
                type="number"
                min="5"
                max="120"
                value={siteSettings.pickupSlotMinutes ?? 15}
                onChange={(event) => updateField('pickupSlotMinutes', Number(event.target.value))}
              />
              <small className="settings-hint">
                بازه‌ها از ساعت شروع کار کافه تا ساعت پایان، پشت‌سرهم ساخته می‌شوند (۵ تا ۱۲۰ دقیقه).
              </small>
            </label>

            <label>
              <span>ظرفیت هر بازه</span>
              <input
                className="ltr-input"
                dir="ltr"
                type="number"
                min="1"
                max="1000"
                value={siteSettings.pickupSlotCapacity ?? 5}
                onChange={(event) => updateField('pickupSlotCapacity', Number(event.target.value))}
              />
              <small className="settings-hint">
                حداکثر بار مجاز هر بازه؛ وقتی پر شد، بازه برای مشتری با برچسب «تکمیل» غیرفعال می‌شود.
              </small>
            </label>

            <label>
              <span>واحد ظرفیت</span>
              <select
                value={siteSettings.pickupCapacityMode ?? 'Orders'}
                onChange={(event) => updateField('pickupCapacityMode', event.target.value)}
              >
                <option value="Orders">بر اساس تعداد سفارش</option>
                <option value="Items">بر اساس تعداد آیتم</option>
              </select>
              <small className="settings-hint">
                «سفارش» هر سفارش را یک واحد می‌شمارد؛ «آیتم» مجموع تعداد اقلام سفارش‌ها را.
              </small>
            </label>

            <label>
              <span>حداقل فاصلهٔ سفارش تا تحویل (دقیقه)</span>
              <input
                className="ltr-input"
                dir="ltr"
                type="number"
                min="0"
                max="1440"
                value={siteSettings.pickupMinLeadMinutes ?? 30}
                onChange={(event) => updateField('pickupMinLeadMinutes', Number(event.target.value))}
              />
              <small className="settings-hint">
                بازه‌هایی که کمتر از این مدت تا شروعشان مانده باشد به مشتری نمایش داده نمی‌شوند.
              </small>
            </label>

            <label>
              <span>حداکثر روزهای آینده</span>
              <input
                className="ltr-input"
                dir="ltr"
                type="number"
                min="0"
                max="60"
                value={siteSettings.pickupMaxDaysAhead ?? 7}
                onChange={(event) => updateField('pickupMaxDaysAhead', Number(event.target.value))}
              />
              <small className="settings-hint">
                مشتری تا چند روز بعد می‌تواند سفارش زمان‌بندی‌شده ثبت کند (۰ = فقط امروز).
              </small>
            </label>

            <label>
              <span>مهلت لغو یا تغییر (دقیقه قبل از تحویل)</span>
              <input
                className="ltr-input"
                dir="ltr"
                type="number"
                min="0"
                max="10080"
                value={siteSettings.pickupCancelCutoffMinutes ?? 60}
                onChange={(event) => updateField('pickupCancelCutoffMinutes', Number(event.target.value))}
              />
              <small className="settings-hint">
                بعد از این مهلت، دکمه‌های لغو و تغییر زمان برای مشتری غیرفعال می‌شوند.
              </small>
            </label>

            <label className="settings-switch-row full-width">
              <div className="settings-switch-text">
                <strong>برگشت خودکار مبلغ به کیف پول هنگام لغو</strong>
                <p>
                  اگر سفارش از کیف پول پرداخت شده باشد، مبلغ به‌صورت خودکار برگشت داده می‌شود.
                  بازپرداخت پرداخت‌های درگاه آنلاین همچنان توسط کافه انجام می‌شود.
                </p>
              </div>
              <span
                className={`switch ${siteSettings.pickupAutoRefundOnCancel ? 'switch-on' : ''}`}
              >
                <input
                  type="checkbox"
                  checked={siteSettings.pickupAutoRefundOnCancel !== false}
                  onChange={(event) =>
                    updateField('pickupAutoRefundOnCancel', event.target.checked)
                  }
                />
                <span className="switch-track">
                  <span className="switch-thumb" />
                </span>
              </span>
            </label>

            <label className="full-width">
              <span>متن سیاست بازپرداخت برای مشتری</span>
              <textarea
                rows="2"
                maxLength={1000}
                value={siteSettings.pickupRefundPolicyNote ?? ''}
                onChange={(event) => updateField('pickupRefundPolicyNote', event.target.value)}
              />
              <small className="settings-hint">
                این متن در صفحهٔ پیگیری سفارش و پیش از لغو به مشتری نمایش داده می‌شود.
              </small>
            </label>
          </div>
        </section>

        {/* ---------- سبد رهاشده ---------- */}
        <section id="settings-abandoned-cart" className="panel settings-section">
          <div className="panel-head">
            <div className="settings-section-head">
              <span className="settings-section-badge">
                <SectionIcon name="bell" />
              </span>
              <div>
                <h2>
                  سبد رهاشده
                  <span
                    className={`settings-pill ${
                      siteSettings.abandonedCartReminderEnabled === false ? 'is-off' : 'is-on'
                    }`}
                  >
                    {siteSettings.abandonedCartReminderEnabled === false ? 'غیرفعال' : 'فعال'}
                  </span>
                </h2>
                <p>
                  اگر سبد مشتری بیش از مدت تعیین‌شده بدون پرداخت بماند، پیامک یا ایمیل حاوی کد
                  تخفیف یک‌بارمصرف برای او ارسال می‌شود.
                </p>
              </div>
            </div>
          </div>

          <div className="settings-grid">
            <label className="settings-switch-row full-width">
              <div className="settings-switch-text">
                <strong>یادآوری خودکار سبد رهاشده</strong>
                <p>
                  با غیرفعال کردن این گزینه، هیچ پیام یادآوری و کد تخفیفی برای سبدهای رهاشده
                  ارسال نمی‌شود.
                </p>
              </div>
              <span
                className={`switch ${
                  siteSettings.abandonedCartReminderEnabled === false ? '' : 'switch-on'
                }`}
              >
                <input
                  type="checkbox"
                  checked={siteSettings.abandonedCartReminderEnabled !== false}
                  onChange={(event) =>
                    updateField('abandonedCartReminderEnabled', event.target.checked)
                  }
                />
                <span className="switch-track">
                  <span className="switch-thumb" />
                </span>
              </span>
            </label>

            <label>
              <span>مدت رهاشدگی سبد (ساعت)</span>
              <input
                className="ltr-input"
                dir="ltr"
                type="number"
                min="0"
                max="720"
                value={siteSettings.abandonedCartHours ?? 4}
                onChange={(event) => updateField('abandonedCartHours', Number(event.target.value))}
              />
              <small className="settings-hint">
                سبدی که بیش از این تعداد ساعت بدون پرداخت بماند یادآوری می‌گیرد — مقدار ۰
                یادآوری را متوقف می‌کند
              </small>
            </label>
            <label>
              <span>درصد تخفیف کد بازگشت</span>
              <input
                className="ltr-input"
                dir="ltr"
                type="number"
                min="0"
                max="100"
                step="0.5"
                value={siteSettings.abandonedCartDiscountPercent ?? 10}
                onChange={(event) =>
                  updateField('abandonedCartDiscountPercent', Number(event.target.value))
                }
              />
              <small className="settings-hint">
                کد تخفیف یک‌بارمصرفی که همراه پیام ارسال می‌شود، این درصد را از مبلغ سبد کم
                می‌کند
              </small>
            </label>
            <label>
              <span>اعتبار کد بازگشت (ساعت)</span>
              <input
                className="ltr-input"
                dir="ltr"
                type="number"
                min="1"
                max="720"
                value={siteSettings.abandonedCartCodeValidityHours ?? 48}
                onChange={(event) =>
                  updateField('abandonedCartCodeValidityHours', Number(event.target.value))
                }
              />
              <small className="settings-hint">
                کد تخفیف پس از این تعداد ساعت منقضی می‌شود (۱ تا ۷۲۰ ساعت)
              </small>
            </label>

            <label className="settings-switch-row full-width">
              <div className="settings-switch-text">
                <strong>ارسال با پیامک</strong>
                <p>برای سبدهایی که شماره موبایل مشتری ثبت شده است.</p>
              </div>
              <span
                className={`switch ${siteSettings.abandonedCartSmsEnabled === false ? '' : 'switch-on'}`}
              >
                <input
                  type="checkbox"
                  checked={siteSettings.abandonedCartSmsEnabled !== false}
                  onChange={(event) => updateField('abandonedCartSmsEnabled', event.target.checked)}
                />
                <span className="switch-track">
                  <span className="switch-thumb" />
                </span>
              </span>
            </label>

            <label className="settings-switch-row full-width">
              <div className="settings-switch-text">
                <strong>ارسال با ایمیل</strong>
                <p>برای سبدهایی که ایمیل مشتری (اختیاری) ثبت شده است.</p>
              </div>
              <span
                className={`switch ${
                  siteSettings.abandonedCartEmailEnabled ? 'switch-on' : ''
                }`}
              >
                <input
                  type="checkbox"
                  checked={siteSettings.abandonedCartEmailEnabled === true}
                  onChange={(event) =>
                    updateField('abandonedCartEmailEnabled', event.target.checked)
                  }
                />
                <span className="switch-track">
                  <span className="switch-thumb" />
                </span>
              </span>
            </label>

            <label className="full-width">
              <span>متن پیامک یادآوری</span>
              <textarea
                rows="3"
                maxLength="1000"
                value={siteSettings.abandonedCartSmsTemplate ?? ''}
                onChange={(event) =>
                  updateField('abandonedCartSmsTemplate', event.target.value)
                }
              />
              <small className="settings-hint">
                جای‌نگهدارها: {'{name}'} نام مشتری، {'{cafe}'} نام کافه، {'{code}'} کد تخفیف،{' '}
                {'{percent}'} درصد تخفیف، {'{validityHours}'} ساعت اعتبار، {'{itemCount}'} تعداد
                اقلام، {'{itemsTotal}'} مبلغ سبد، {'{items}'} فهرست اقلام
              </small>
            </label>
            <label className="full-width">
              <span>موضوع ایمیل یادآوری</span>
              <input
                value={siteSettings.abandonedCartEmailSubject ?? ''}
                onChange={(event) =>
                  updateField('abandonedCartEmailSubject', event.target.value)
                }
              />
            </label>
            <label className="full-width">
              <span>متن ایمیل یادآوری</span>
              <textarea
                rows="6"
                maxLength="4000"
                value={siteSettings.abandonedCartEmailTemplate ?? ''}
                onChange={(event) =>
                  updateField('abandonedCartEmailTemplate', event.target.value)
                }
              />
              <small className="settings-hint">
                همین جای‌نگهدارها در متن ایمیل هم قابل استفاده است؛ هر خط جدید به‌صورت خط جدید
                در ایمیل نمایش داده می‌شود.
              </small>
            </label>
          </div>
        </section>

        {/* ---------- باشگاه مشتریان ---------- */}
        <section id="settings-loyalty" className="panel settings-section">
          <div className="panel-head">
            <div className="settings-section-head">
              <span className="settings-section-badge">
                <SectionIcon name="brand" />
              </span>
              <div>
                <h2>
                  باشگاه مشتریان
                  <span
                    className={`settings-pill ${
                      siteSettings.loyaltyEnabled ? 'is-on' : 'is-off'
                    }`}
                  >
                    {siteSettings.loyaltyEnabled ? 'فعال' : 'غیرفعال'}
                  </span>
                </h2>
                <p>نرخ کسب و مصرف امتیاز وفاداری برای مشتریان</p>
              </div>
            </div>
          </div>

          <div className="settings-grid">
            <label className="settings-switch-row full-width">
              <div className="settings-switch-text">
                <strong>فعال‌سازی باشگاه مشتریان</strong>
                <p>با غیرفعال کردن، امتیازی کسب یا مصرف نمی‌شود.</p>
              </div>
              <span className={`switch ${siteSettings.loyaltyEnabled ? 'switch-on' : ''}`}>
                <input
                  type="checkbox"
                  checked={siteSettings.loyaltyEnabled === true}
                  onChange={(event) => updateField('loyaltyEnabled', event.target.checked)}
                />
                <span className="switch-track">
                  <span className="switch-thumb" />
                </span>
              </span>
            </label>

            <label>
              <span>هر چند تومان = ۱ امتیاز (کسب)</span>
              <input
                className="ltr-input"
                dir="ltr"
                type="number"
                min="0"
                value={siteSettings.loyaltyEarnTomanPerPoint ?? 10000}
                onChange={(event) =>
                  updateField('loyaltyEarnTomanPerPoint', Number(event.target.value))
                }
              />
              <small className="settings-hint">مقدار ۰ کسب امتیاز را متوقف می‌کند</small>
            </label>
            <label>
              <span>ارزش هر امتیاز (تومان) هنگام مصرف</span>
              <input
                className="ltr-input"
                dir="ltr"
                type="number"
                min="0"
                value={siteSettings.loyaltyRedeemTomanPerPoint ?? 1000}
                onChange={(event) =>
                  updateField('loyaltyRedeemTomanPerPoint', Number(event.target.value))
                }
              />
              <small className="settings-hint">مقدار ۰ مصرف امتیاز را متوقف می‌کند</small>
            </label>
            <label>
              <span>مدت اعتبار امتیاز (روز)</span>
              <input
                className="ltr-input"
                dir="ltr"
                type="number"
                min="0"
                value={siteSettings.loyaltyPointsExpiryDays ?? 365}
                onChange={(event) =>
                  updateField('loyaltyPointsExpiryDays', Number(event.target.value))
                }
              />
              <small className="settings-hint">مقدار ۰ = بدون انقضا</small>
            </label>

            <h3 className="settings-subheading full-width">کارت مهر</h3>
            <label className="settings-switch-row full-width">
              <div className="settings-switch-text">
                <strong>فعال‌سازی کارت مهر</strong>
                <p>هر مشتری با سفارش‌های پرداخت‌شده مهر می‌گیرد؛ هر کارت کامل جایزهٔ خودکار دارد.</p>
              </div>
              <span className={`switch ${siteSettings.stampCardEnabled ? 'switch-on' : ''}`}>
                <input
                  type="checkbox"
                  checked={siteSettings.stampCardEnabled === true}
                  onChange={(event) => updateField('stampCardEnabled', event.target.checked)}
                />
                <span className="switch-track">
                  <span className="switch-thumb" />
                </span>
              </span>
            </label>
            <label>
              <span>تعداد سفارش برای هر کارت</span>
              <input
                className="ltr-input"
                dir="ltr"
                type="number"
                min="1"
                value={siteSettings.stampCardOrdersPerCard ?? 10}
                onChange={(event) =>
                  updateField('stampCardOrdersPerCard', Number(event.target.value))
                }
              />
            </label>
            <label>
              <span>درصد جایزهٔ کارت کامل</span>
              <input
                className="ltr-input"
                dir="ltr"
                type="number"
                min="0"
                max="100"
                value={siteSettings.stampCardRewardPercent ?? 10}
                onChange={(event) =>
                  updateField('stampCardRewardPercent', Number(event.target.value))
                }
              />
            </label>
            <label>
              <span>اعتبار جایزه (روز)</span>
              <input
                className="ltr-input"
                dir="ltr"
                type="number"
                min="0"
                value={siteSettings.stampCardRewardValidityDays ?? 30}
                onChange={(event) =>
                  updateField('stampCardRewardValidityDays', Number(event.target.value))
                }
              />
              <small className="settings-hint">مقدار ۰ = بدون انقضا؛ جایزه در پرداخت بعدی خودکار اعمال می‌شود</small>
            </label>

            <h3 className="settings-subheading full-width">سطح‌بندی عضویت</h3>
            <label className="settings-switch-row full-width">
              <div className="settings-switch-text">
                <strong>ارتقای خودکار سطح</strong>
                <p>مشتری بر اساس مجموع خرید در بازهٔ زیر ارتقا می‌گیرد و تخفیف دائمی سطحش خودکار اعمال می‌شود.</p>
              </div>
              <span className={`switch ${siteSettings.membershipTiersEnabled ? 'switch-on' : ''}`}>
                <input
                  type="checkbox"
                  checked={siteSettings.membershipTiersEnabled === true}
                  onChange={(event) => updateField('membershipTiersEnabled', event.target.checked)}
                />
                <span className="switch-track">
                  <span className="switch-thumb" />
                </span>
              </span>
            </label>
            <label>
              <span>بازهٔ ارزیابی خرید (روز)</span>
              <input
                className="ltr-input"
                dir="ltr"
                type="number"
                min="0"
                value={siteSettings.membershipWindowDays ?? 365}
                onChange={(event) =>
                  updateField('membershipWindowDays', Number(event.target.value))
                }
              />
              <small className="settings-hint">مثلاً ۳۶۵ = مجموع خرید یک سال گذشته؛ مقدار ۰ = کل عمر مشتری</small>
            </label>
            <label className="settings-switch-row full-width">
              <div className="settings-switch-text">
                <strong>اجازهٔ تنزل سطح</strong>
                <p>اگر فعالیت مشتری در بازهٔ ارزیابی زیر شرط سطحش برود، سطحش به‌طور خودکار پایین می‌آید. خاموش کنید تا سطح‌ها فقط بالا بروند.</p>
              </div>
              <span className={`switch ${siteSettings.membershipDowngradeEnabled ? 'switch-on' : ''}`}>
                <input
                  type="checkbox"
                  checked={siteSettings.membershipDowngradeEnabled !== false}
                  onChange={(event) => updateField('membershipDowngradeEnabled', event.target.checked)}
                />
                <span className="switch-track">
                  <span className="switch-thumb" />
                </span>
              </span>
            </label>

            <div className="full-width tiers-editor">
              {tierRows.map((row, index) => (
                <div key={row.id || `new-${index}`} className="tier-row">
                  <label>
                    <span>نام سطح</span>
                    <input
                      maxLength={100}
                      placeholder="مثلاً: نقره‌ای"
                      value={row.name}
                      onChange={(event) => updateTierRow(index, 'name', event.target.value)}
                    />
                  </label>
                  <label>
                    <span>حداقل خرید در بازه (تومان)</span>
                    <input
                      className="ltr-input"
                      dir="ltr"
                      type="number"
                      min="0"
                      step="10000"
                      value={row.minTotalPaidSpent}
                      onChange={(event) => updateTierRow(index, 'minTotalPaidSpent', event.target.value)}
                    />
                  </label>
                  <label>
                    <span>تخفیف دائمی (٪)</span>
                    <input
                      className="ltr-input"
                      dir="ltr"
                      type="number"
                      min="0"
                      max="100"
                      value={row.discountPercent}
                      onChange={(event) => updateTierRow(index, 'discountPercent', event.target.value)}
                    />
                  </label>
                  <label>
                    <span>حداقل سفارش در بازه</span>
                    <input
                      className="ltr-input"
                      dir="ltr"
                      type="number"
                      min="0"
                      value={row.minPaidOrders}
                      onChange={(event) => updateTierRow(index, 'minPaidOrders', event.target.value)}
                    />
                  </label>
                  <label>
                    <span>کش‌بک اختصاصی (٪)</span>
                    <input
                      className="ltr-input"
                      dir="ltr"
                      type="number"
                      min="0"
                      max="100"
                      placeholder="کلی"
                      value={row.cashbackPercent}
                      onChange={(event) => updateTierRow(index, 'cashbackPercent', event.target.value)}
                    />
                    <small className="settings-hint">خالی = درصد کلی کش‌بک</small>
                  </label>
                  <label>
                    <span>ترتیب</span>
                    <input
                      className="ltr-input"
                      dir="ltr"
                      type="number"
                      min="0"
                      value={row.displayOrder}
                      onChange={(event) => updateTierRow(index, 'displayOrder', event.target.value)}
                    />
                  </label>
                  <label className="checkbox-row">
                    <input
                      type="checkbox"
                      checked={row.isEnabled}
                      onChange={(event) => updateTierRow(index, 'isEnabled', event.target.checked)}
                    />
                    <span>فعال</span>
                  </label>
                  <button type="button" className="link-button link-danger" onClick={() => removeTierRow(index)}>
                    حذف
                  </button>
                </div>
              ))}
              <div className="tier-editor-actions">
                <button type="button" className="ghost-button" onClick={addTierRow}>
                  افزودن سطح
                </button>
                <button type="button" disabled={tiersSaving} onClick={saveTiers}>
                  {tiersSaving ? 'در حال ذخیره...' : 'ذخیره سطح‌ها'}
                </button>
              </div>
              {tierError ? <p className="feedback feedback-error">{tierError}</p> : null}
              {tierMessage ? <p className="feedback feedback-success">{tierMessage}</p> : null}
              <p className="table-subtext">
                بالاترین سطحی که مشتری شرط ارتقای آن (مبلغ خرید یا تعداد سفارش در بازهٔ ارزیابی) را رد کرده باشد، خودکار به او اختصاص می‌یابد. کش‌بک اختصاصی سطح، درصد کلی را جایگزین می‌کند. سطح پایه = مشتری بدون سطح.
              </p>
            </div>
            <h3 className="settings-subheading full-width">کیف پول و کش‌بک</h3>
            <label className="settings-switch-row full-width">
              <div className="settings-switch-text">
                <strong>فعال‌سازی کیف پول</strong>
                <p>مشتری می‌تواند موجودی خود را شارژ کند، کش‌بک بگیرد و سفارش را با کیف پول پرداخت کند.</p>
              </div>
              <span className={`switch ${siteSettings.walletEnabled ? 'switch-on' : ''}`}>
                <input
                  type="checkbox"
                  checked={siteSettings.walletEnabled === true}
                  onChange={(event) => updateField('walletEnabled', event.target.checked)}
                />
                <span className="switch-track">
                  <span className="switch-thumb" />
                </span>
              </span>
            </label>
            <label>
              <span>درصد کش‌بک هر سفارش</span>
              <input
                className="ltr-input"
                dir="ltr"
                type="number"
                min="0"
                max="100"
                step="0.5"
                value={siteSettings.walletCashbackPercent ?? 2}
                onChange={(event) =>
                  updateField('walletCashbackPercent', Number(event.target.value))
                }
              />
              <small className="settings-hint">بعد از پذیرش سفارش، این درصد به‌صورت اعتبار (تومان) به کیف پول برمی‌گردد؛ صفر = غیرفعال</small>
            </label>
            <label>
              <span>حداقل مبلغ سفارش برای کش‌بک (تومان)</span>
              <input
                className="ltr-input"
                dir="ltr"
                type="number"
                min="0"
                step="10000"
                value={siteSettings.walletCashbackMinOrderAmount ?? 0}
                onChange={(event) =>
                  updateField('walletCashbackMinOrderAmount', Number(event.target.value))
                }
              />
              <small className="settings-hint">مقدار ۰ = بدون حداقل</small>
            </label>
            <label>
              <span>سقف کش‌بک هر سفارش (تومان)</span>
              <input
                className="ltr-input"
                dir="ltr"
                type="number"
                min="0"
                step="5000"
                value={siteSettings.walletCashbackMaxPerOrderAmount ?? 0}
                onChange={(event) =>
                  updateField('walletCashbackMaxPerOrderAmount', Number(event.target.value))
                }
              />
              <small className="settings-hint">کش‌بک هر سفارش بیشتر از این مبلغ نمی‌شود؛ مقدار ۰ = بدون سقف</small>
            </label>
            <label>
              <span>انقضای کش‌بک (روز)</span>
              <input
                className="ltr-input"
                dir="ltr"
                type="number"
                min="0"
                value={siteSettings.walletCashbackExpiryDays ?? 0}
                onChange={(event) =>
                  updateField('walletCashbackExpiryDays', Number(event.target.value))
                }
              />
              <small className="settings-hint">پس از این روزها کش‌بک‌های مصرف‌نشده از کیف پول حذف می‌شوند؛ مقدار ۰ = بدون انقضا</small>
            </label>
            <label>
              <span>سقف پرداخت از کیف پول (٪ سفارش)</span>
              <input
                className="ltr-input"
                dir="ltr"
                type="number"
                min="1"
                max="100"
                value={siteSettings.walletPaymentMaxPercent ?? 100}
                onChange={(event) =>
                  updateField('walletPaymentMaxPercent', Number(event.target.value))
                }
              />
              <small className="settings-hint">مثلاً ۵۰ = حداکثر نصف هر سفارش با کیف پول پرداخت می‌شود؛ ۱۰۰ = بدون محدودیت</small>
            </label>

            <h3 className="settings-subheading full-width">بسته‌های شارژ با پاداش</h3>
            <div className="full-width tiers-editor">
              {(walletPackages ?? []).map((row, index) => (
                <div key={row.id || `pkg-${index}`} className="tier-row">
                  <label>
                    <span>مبلغ شارژ (تومان)</span>
                    <input
                      className="ltr-input"
                      dir="ltr"
                      type="number"
                      min="0"
                      step="10000"
                      value={row.amount}
                      onChange={(event) => updatePackageRow(index, 'amount', event.target.value)}
                    />
                  </label>
                  <label>
                    <span>هدیه (تومان)</span>
                    <input
                      className="ltr-input"
                      dir="ltr"
                      type="number"
                      min="0"
                      step="10000"
                      value={row.bonusAmount}
                      onChange={(event) => updatePackageRow(index, 'bonusAmount', event.target.value)}
                    />
                  </label>
                  <label>
                    <span>ترتیب</span>
                    <input
                      className="ltr-input"
                      dir="ltr"
                      type="number"
                      min="0"
                      value={row.displayOrder}
                      onChange={(event) => updatePackageRow(index, 'displayOrder', event.target.value)}
                    />
                  </label>
                  <label className="checkbox-row">
                    <input
                      type="checkbox"
                      checked={row.isEnabled}
                      onChange={(event) => updatePackageRow(index, 'isEnabled', event.target.checked)}
                    />
                    <span>فعال</span>
                  </label>
                  <button type="button" className="link-button link-danger" onClick={() => removePackageRow(index)}>
                    حذف
                  </button>
                </div>
              ))}
              <div className="tier-editor-actions">
                <button type="button" className="ghost-button" onClick={addPackageRow}>
                  افزودن بسته
                </button>
                <button type="button" disabled={packagesSaving} onClick={savePackages}>
                  {packagesSaving ? 'در حال ذخیره...' : 'ذخیره بسته‌ها'}
                </button>
              </div>
              {packagesError ? <p className="feedback feedback-error">{packagesError}</p> : null}
              {packagesMessage ? <p className="feedback feedback-success">{packagesMessage}</p> : null}
              <p className="table-subtext">
                این بسته‌ها در دکمه‌های شارژ سریع (پنل مشتری و پیشخوان ادمین) پیشنهاد می‌شوند؛ هدیه به‌طور خودکار وارد کیف پول مشتری می‌شود.
              </p>
            </div>

            <h3 className="settings-subheading full-width">تخفیف تولد</h3>
            <label className="settings-switch-row full-width">
              <div className="settings-switch-text">
                <strong>فعال‌سازی تخفیف تولد</strong>
                <p>در روز تولد (یا بازهٔ دلخواه اطراف آن) مشتری هدیهٔ تخفیف می‌گیرد. تاریخ تولد کاربر را خودش یا شما در پرونده مشتری ثبت می‌کند.</p>
              </div>
              <span className={`switch ${siteSettings.birthdayDiscountEnabled ? 'switch-on' : ''}`}>
                <input
                  type="checkbox"
                  checked={siteSettings.birthdayDiscountEnabled === true}
                  onChange={(event) => updateField('birthdayDiscountEnabled', event.target.checked)}
                />
                <span className="switch-track">
                  <span className="switch-thumb" />
                </span>
              </span>
            </label>
            <label>
              <span>درصد تخفیف تولد (٪)</span>
              <input
                className="ltr-input"
                dir="ltr"
                type="number"
                min="0"
                max="100"
                step="0.5"
                value={siteSettings.birthdayDiscountPercent ?? 0}
                onChange={(event) =>
                  updateField('birthdayDiscountPercent', Number(event.target.value))
                }
              />
            </label>
            <label>
              <span>بازهٔ اعتبار اطراف تولد (روز)</span>
              <input
                className="ltr-input"
                dir="ltr"
                type="number"
                min="0"
                max="60"
                value={siteSettings.birthdayDiscountWindowDays ?? 0}
                onChange={(event) =>
                  updateField('birthdayDiscountWindowDays', Number(event.target.value))
                }
              />
              <small className="settings-hint">۰ = فقط روز تولد؛ ۷ = هفتهٔ تولد</small>
            </label>
            <label className="settings-switch-row full-width">
              <div className="settings-switch-text">
                <strong>نحوهٔ اعطا</strong>
                <p>خودکار = تخفیف در تسویه‌حساب بی‌درنگ اعمال می‌شود. کد = کد تخفیف شخصیِ یک‌مصرف به مشتری داده می‌شود تا هنگام سفارش وارد کند.</p>
              </div>
              <span className={`switch ${(siteSettings.birthdayDiscountMode ?? 0) === 0 ? 'switch-on' : ''}`}>
                <input
                  type="checkbox"
                  checked={(siteSettings.birthdayDiscountMode ?? 0) === 0}
                  onChange={(event) => updateField('birthdayDiscountMode', event.target.checked ? 0 : 1)}
                />
                <span className="switch-track">
                  <span className="switch-thumb" />
                </span>
              </span>
              <span className="settings-switch-text">
                <strong>{(siteSettings.birthdayDiscountMode ?? 0) === 0 ? 'خودکار' : 'کد تخفیف شخصی'}</strong>
              </span>
            </label>
            <label className="full-width">
              <span>متن پیام تبریک تولد</span>
              <textarea
                rows="2"
                maxLength={500}
                value={siteSettings.birthdayGreetingMessage ?? ''}
                onChange={(event) => updateField('birthdayGreetingMessage', event.target.value)}
                placeholder="تولدت مبارک! 🎉"
              />
            </label>
            <label className="settings-switch-row full-width">
              <div className="settings-switch-text">
                <strong>امکان تغییر تاریخ تولد توسط کاربر</strong>
                <p>روشن = مشتری می‌تواند در منوی دیجیتال تاریخ تولدش را ثبت یا تغییر دهد. خاموش = تاریخ تولد فقط توسط شما در پنل ثبت/اصلاح می‌شود.</p>
              </div>
              <span className={`switch ${siteSettings.birthdayEditableByCustomer ? 'switch-on' : ''}`}>
                <input
                  type="checkbox"
                  checked={siteSettings.birthdayEditableByCustomer !== false}
                  onChange={(event) => updateField('birthdayEditableByCustomer', event.target.checked)}
                />
                <span className="switch-track">
                  <span className="switch-thumb" />
                </span>
              </span>
            </label>

            <h3 className="settings-subheading full-width">تخفیف لیوان شخصی</h3>
            <label className="settings-switch-row full-width">
              <div className="settings-switch-text">
                <strong>فعال‌سازی تخفیف لیوان شخصی</strong>
                <p>اگر مشتری لیوان شخصی بیاورد، هنگام سفارش (آنلاین و صندوق) می‌تواند این تخفیف را انتخاب کند.</p>
              </div>
              <span className={`switch ${siteSettings.personalCupDiscountEnabled ? 'switch-on' : ''}`}>
                <input
                  type="checkbox"
                  checked={siteSettings.personalCupDiscountEnabled === true}
                  onChange={(event) => updateField('personalCupDiscountEnabled', event.target.checked)}
                />
                <span className="switch-track">
                  <span className="switch-thumb" />
                </span>
              </span>
            </label>
            <label>
              <span>مبلغ تخفیف لیوان شخصی (تومان)</span>
              <input
                className="ltr-input"
                dir="ltr"
                type="number"
                min="0"
                step="1000"
                value={siteSettings.personalCupDiscountAmount ?? 0}
                onChange={(event) => updateField('personalCupDiscountAmount', Number(event.target.value))}
              />
            </label>
            <label className="full-width">
              <span>متن راهنمای تحویل</span>
              <textarea
                rows="2"
                maxLength={500}
                value={siteSettings.personalCupDeliveryNote ?? ''}
                onChange={(event) => updateField('personalCupDeliveryNote', event.target.value)}
                placeholder="لیوان شخصی را هنگام تحویل به باریستا نشان دهید."
              />
            </label>
            <div className="full-width">
              <span className="field-label">دسته‌های مشمول تخفیف لیوان</span>
              <div className="checkbox-grid">
                {cupCategoryOptions.map((category) => {
                  const selected = (siteSettings.personalCupCategoryIds ?? '')
                    .split(',')
                    .map((value) => value.trim())
                    .filter(Boolean)
                    .includes(category.id)
                  return (
                    <label key={category.id} className="checkbox-row">
                      <input
                        type="checkbox"
                        checked={selected}
                        onChange={() => {
                          const current = (siteSettings.personalCupCategoryIds ?? '')
                            .split(',')
                            .map((value) => value.trim())
                            .filter(Boolean)
                          const next = selected
                            ? current.filter((id) => id !== category.id)
                            : [...current, category.id]
                          updateField('personalCupCategoryIds', next.join(','))
                        }}
                      />
                      <span>{category.name}</span>
                    </label>
                  )
                })}
              </div>
              <small className="settings-hint">اگر هیچ دسته‌ای انتخاب نشود، تخفیف روی همهٔ محصولات اعمال می‌شود.</small>
            </div>

            <h3 className="settings-subheading full-width">شرایط اشتراک ماهانه</h3>
            <label className="full-width">
              <span>سیاست تمدید</span>
              <textarea
                rows="2"
                maxLength={1000}
                value={siteSettings.subscriptionRenewalPolicy ?? ''}
                onChange={(event) => updateField('subscriptionRenewalPolicy', event.target.value)}
              />
              <small className="settings-hint">این متن در پنل مشتری نمایش داده می‌شود.</small>
            </label>
            <label className="full-width">
              <span>شرایط لغو و بازپرداخت</span>
              <textarea
                rows="2"
                maxLength={1000}
                value={siteSettings.subscriptionCancelRefundPolicy ?? ''}
                onChange={(event) => updateField('subscriptionCancelRefundPolicy', event.target.value)}
              />
              <small className="settings-hint">مشتری هنگام لغو، معادل استفاده‌های باقی‌مانده را به کیف پول دریافت می‌کند.</small>
            </label>

            <h3 className="settings-subheading full-width">امنیت تخفیف و سقف روزانه</h3>
            <label>
              <span>محافظت از سوءاستفاده</span>
              <span className={`switch ${siteSettings.discountAbuseProtectionEnabled ? 'switch-on' : ''}`}>
                <input
                  type="checkbox"
                  checked={siteSettings.discountAbuseProtectionEnabled !== false}
                  onChange={(event) => updateField('discountAbuseProtectionEnabled', event.target.checked)}
                />
              </span>
              <small className="settings-hint">
                قفل موقت پس از تلاش‌های ناموفق، لیست سیاه و سقف روزانه را فعال می‌کند.
              </small>
            </label>
            <label>
              <span>حداکثر تلاش ناموفق برای کد</span>
              <input
                className="ltr-input"
                dir="ltr"
                type="number"
                min="1"
                max="100"
                value={siteSettings.discountMaxFailedAttempts ?? 5}
                onChange={(event) => updateField('discountMaxFailedAttempts', Number(event.target.value))}
              />
              <small className="settings-hint">بعد از این تعداد کد اشتباه، استفاده از تخفیف موقتاً قفل می‌شود.</small>
            </label>
            <label>
              <span>مدت قفل موقت (دقیقه)</span>
              <input
                className="ltr-input"
                dir="ltr"
                type="number"
                min="1"
                max="10080"
                value={siteSettings.discountLockoutMinutes ?? 15}
                onChange={(event) => updateField('discountLockoutMinutes', Number(event.target.value))}
              />
              <small className="settings-hint">پس از این مدت، کاربر می‌تواند دوباره کد وارد کند.</small>
            </label>
            <label>
              <span>سقف کل تخفیف روزانه هر کاربر (تومان)</span>
              <input
                className="ltr-input"
                dir="ltr"
                type="number"
                min="0"
                value={siteSettings.discountDailyCapPerCustomer ?? 0}
                onChange={(event) => updateField('discountDailyCapPerCustomer', Number(event.target.value))}
              />
              <small className="settings-hint">
                مجموع همهٔ تخفیف‌ها (کد، گروه، سطح، لیوان، اشتراک و…) در یک روز. مقدار ۰ یعنی بدون محدودیت.
              </small>
            </label>
          </div>
        </section>

        {/* ---------- تم و نمایش ---------- */}
        <section id="settings-theme" className="panel settings-section">
          <div className="panel-head">
            <div className="settings-section-head">
              <span className="settings-section-badge">
                <SectionIcon name="theme" />
              </span>
              <div>
                <h2>تم و نمایش</h2>
                <p>تم روشن/تیرهٔ منوی مشتریان بر اساس ساعت محلی کافه، با امکان انتخاب دستی</p>
              </div>
            </div>
          </div>

          <div className="settings-grid">
            <label>
              <span>منطقه زمانی کافه</span>
              <input
                className="ltr-input"
                dir="ltr"
                placeholder="Asia/Tehran"
                value={siteSettings.timeZoneId ?? 'Asia/Tehran'}
                onChange={(event) => updateField('timeZoneId', event.target.value)}
              />
              <small className="settings-hint">
                شناسهٔ منطقه زمانی IANA (مثل Asia/Tehran یا Europe/Berlin) — مبنای محاسبهٔ ساعت تم
              </small>
            </label>
            <label>
              <span>شروع تم روشن (ساعت)</span>
              <input
                className="ltr-input"
                dir="ltr"
                type="number"
                min="0"
                max="23"
                value={siteSettings.lightThemeStartHour ?? 6}
                onChange={(event) => updateField('lightThemeStartHour', Number(event.target.value))}
              />
              <small className="settings-hint">از این ساعت محلی کافه، تم روشن فعال می‌شود (۰ تا ۲۳)</small>
            </label>
            <label>
              <span>شروع تم تیره (ساعت)</span>
              <input
                className="ltr-input"
                dir="ltr"
                type="number"
                min="0"
                max="23"
                value={siteSettings.darkThemeStartHour ?? 20}
                onChange={(event) => updateField('darkThemeStartHour', Number(event.target.value))}
              />
              <small className="settings-hint">
                از این ساعت محلی کافه، تم تیره فعال می‌شود — می‌تواند از شروع تم روشن کوچک‌تر باشد (تم تیره از شب تا صبح)
              </small>
            </label>
            <label>
              <span>حالت تم</span>
              <select
                value={siteSettings.themeMode ?? 'auto'}
                onChange={(event) => updateField('themeMode', event.target.value)}
              >
                <option value="auto">خودکار (طبق برنامهٔ زمانی)</option>
                <option value="light">همیشه روشن</option>
                <option value="dark">همیشه تیره</option>
              </select>
              <small className="settings-hint">
                در حالت خودکار، تم بر اساس ساعت محلی کافه بین روشن و تیره جابه‌جا می‌شود
              </small>
            </label>
            <label className="settings-switch-row full-width">
              <div className="settings-switch-text">
                <strong>اجازهٔ تغییر دستی توسط کاربر</strong>
                <p>دکمهٔ تغییر تم روی منوی مشتریان نمایش داده شود و انتخاب او در بازدیدهای بعدی هم حفظ شود.</p>
              </div>
              <span className={`switch ${siteSettings.allowManualThemeOverride ? 'switch-on' : ''}`}>
                <input
                  type="checkbox"
                  checked={siteSettings.allowManualThemeOverride !== false}
                  onChange={(event) => updateField('allowManualThemeOverride', event.target.checked)}
                />
                <span className="switch-track">
                  <span className="switch-thumb" />
                </span>
              </span>
            </label>
          </div>
        </section>

        {/* ---------- بقیه بخش‌ها ---------- */}
        {settingsSections.map((section) => (
          <section
            key={section.id}
            id={`settings-${section.id}`}
            className="panel settings-section"
          >
            <div className="panel-head">
              <div className="settings-section-head">
                <span className="settings-section-badge">
                  <SectionIcon name={section.icon} />
                </span>
                <div>
                  <h2>{section.title}</h2>
                  <p>{section.description}</p>
                </div>
              </div>
            </div>
            <div className="settings-grid">
              {section.fields.map(([fieldName, fieldLabel, fieldType, fieldHint]) => {
                const isTextarea = fieldType === 'textarea'
                const isRequired = section.required?.includes(fieldName)
                return (
                  <label
                    key={fieldName}
                    className={isTextarea ? 'full-width' : undefined}
                  >
                    <span>
                      {fieldLabel}
                      {isRequired ? <i className="settings-required">*</i> : null}
                    </span>
                    {isTextarea ? (
                      <textarea
                        className={fieldName === 'instagramUrl' ? 'ltr-input' : ''}
                        rows="4"
                        value={siteSettings[fieldName] ?? ''}
                        onChange={(event) => updateField(fieldName, event.target.value)}
                      />
                    ) : (
                      <input
                        className={
                          fieldType === 'url' || fieldName === 'contactPhone'
                            ? 'ltr-input'
                            : undefined
                        }
                        type={fieldType ?? 'text'}
                        value={siteSettings[fieldName] ?? ''}
                        onChange={(event) => updateField(fieldName, event.target.value)}
                      />
                    )}
                    {fieldHint ? <small className="settings-hint">{fieldHint}</small> : null}
                  </label>
                )
              })}
            </div>
          </section>
        ))}

        <div className="settings-savebar">
          <p>
            تغییرات پس از ذخیره، بلافاصله روی سایت مشتریان اعمال می‌شود.
          </p>
          <button type="submit" disabled={saving}>
            {saving ? 'در حال ذخیره…' : 'ذخیره تنظیمات'}
          </button>
        </div>
      </form>
    </div>
  )
}
