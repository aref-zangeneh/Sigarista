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
                <h3>
                  فروش آنلاین
                  <span className={`settings-pill ${salesPaused ? 'is-off' : 'is-on'}`}>
                    {salesPaused ? 'غیرفعال' : 'فعال'}
                  </span>
                </h3>
                <p>کامل کنترل سفارش و پرداخت آنلاین روی سایت مشتریان</p>
              </div>
            </div>
          </div>

          <div className="settings-sales-body">
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
                  <h3>{section.title}</h3>
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
