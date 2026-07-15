import { useAdmin } from '../context/AdminContext'
import { settingsSections } from '../constants/adminForms'

export function SiteSettingsPage() {
  const {
    siteSettings,
    setSiteSettings,
    saving,
    handleSettingsSubmit,
  } = useAdmin()

  return (
      <form className="panel-stack" onSubmit={handleSettingsSubmit}>
        {settingsSections.map((section) => (
          <section key={section.title} className="panel">
            <div className="panel-head">
              <div>
                <h3>{section.title}</h3>
                <p>این مقدارها مستقیما ظاهر منوی QR عمومی را کنترل می‌کنند.</p>
              </div>
            </div>
            <div className="settings-grid">
              {section.fields.map(([fieldName, fieldLabel, fieldType]) => (
                <label
                  key={fieldName}
                  className={fieldType === 'textarea' ? 'full-width' : undefined}
                >
                  <span>{fieldLabel}</span>
                  {fieldType === 'textarea' ? (
                    <textarea
                      className={fieldName === 'instagramUrl' ? 'ltr-input' : ''}
                      rows="4"
                      value={siteSettings[fieldName]}
                      onChange={(event) =>
                        setSiteSettings((currentSettings) => ({
                          ...currentSettings,
                          [fieldName]: event.target.value,
                        }))
                      }
                    />
                  ) : (
                    <input
                      className={fieldType === 'url' || fieldName === 'contactPhone' ? 'ltr-input' : ''}
                      type={fieldType ?? 'text'}
                      value={siteSettings[fieldName]}
                      onChange={(event) =>
                        setSiteSettings((currentSettings) => ({
                          ...currentSettings,
                          [fieldName]: event.target.value,
                        }))
                      }
                    />
                  )}
                </label>
              ))}
            </div>
          </section>
        ))}
        <button type="submit" className="save-settings-button" disabled={saving}>
          {saving ? 'در حال ذخیره...' : 'ذخیره تنظیمات سایت'}
        </button>
      </form>
  )
}
