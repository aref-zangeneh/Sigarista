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

  const navItems = [
    { id: 'settings-sales', title: 'فروش آنلاین', icon: 'cart' },
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
