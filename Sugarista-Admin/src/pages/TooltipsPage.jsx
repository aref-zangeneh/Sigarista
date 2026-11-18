import { useEffect, useMemo, useState } from 'react'
import { useAdmin } from '../context/AdminContext'
import { apiRequest, getReadableError } from '../utils/adminApi'
import { hasPermission, PERMISSIONS } from '../utils/adminPermissions'
import { TOOLTIP_REGISTRY, tooltipDefault, tooltipPageLabel } from '../utils/tooltipRegistry'
import { Tip } from '../components/Tip'
import './TooltipsPage.css'

export function TooltipsPage() {
  const { session, reloadTipOverrides } = useAdmin()
  const canUpdate = hasPermission(session, PERMISSIONS.tooltips.update)

  const [overrides, setOverrides] = useState([])
  const [loading, setLoading] = useState(true)
  const [savingKey, setSavingKey] = useState(null)
  const [error, setError] = useState('')
  const [message, setMessage] = useState('')
  const [search, setSearch] = useState('')
  const [activePage, setActivePage] = useState('customer-menu')
  const [activeSection, setActiveSection] = useState('all')
  // Draft text per key while editing (falls back to current effective text)
  const [drafts, setDrafts] = useState({})

  const overrideByKey = useMemo(
    () => Object.fromEntries(overrides.map((override) => [override.key, override])),
    [overrides],
  )

  async function loadOverrides() {
    setLoading(true)
    setError('')
    try {
      const data = await apiRequest('/admin/tooltips', { token: session.token })
      setOverrides(Array.isArray(data) ? data : [])
    } catch (loadError) {
      setError(getReadableError(loadError))
    } finally {
      setLoading(false)
    }
  }

  useEffect(() => {
    loadOverrides()
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [])

  function draftFor(entry) {
    if (drafts[entry.key] !== undefined) {
      return drafts[entry.key]
    }
    const override = overrideByKey[entry.key]
    return override?.text ?? tooltipDefault(entry.key)
  }

  function setDraft(key, text) {
    setDrafts((current) => ({ ...current, [key]: text }))
  }

  async function save(entry) {
    const text = draftFor(entry).trim()
    if (!text) {
      setError('متن راهنما نمی‌تواند خالی باشد.')
      return
    }

    setSavingKey(entry.key)
    setError('')
    setMessage('')
    try {
      await apiRequest(`/admin/tooltips/${encodeURIComponent(entry.key)}`, {
        method: 'PUT',
        body: { key: entry.key, text, isActive: overrideByKey[entry.key]?.isActive ?? true },
        token: session.token,
      })
      await loadOverrides()
      reloadTipOverrides()
      setMessage(`راهنمای «${entry.label}» ذخیره شد.`)
    } catch (saveError) {
      setError(getReadableError(saveError))
    } finally {
      setSavingKey(null)
    }
  }

  async function toggleActive(entry) {
    const override = overrideByKey[entry.key]
    const currentIsActive = override ? override.isActive : true
    const effectiveText = (override?.text ?? draftFor(entry).trim()) || tooltipDefault(entry.key)

    setSavingKey(entry.key)
    setError('')
    setMessage('')
    try {
      await apiRequest(`/admin/tooltips/${encodeURIComponent(entry.key)}`, {
        method: 'PUT',
        body: { key: entry.key, text: effectiveText, isActive: !currentIsActive },
        token: session.token,
      })
      await loadOverrides()
      reloadTipOverrides()
      setMessage(`راهنمای «${entry.label}» ${!currentIsActive ? 'فعال' : 'غیرفعال'} شد.`)
    } catch (toggleError) {
      setError(getReadableError(toggleError))
    } finally {
      setSavingKey(null)
    }
  }

  async function reset(entry) {
    setSavingKey(entry.key)
    setError('')
    setMessage('')
    try {
      await apiRequest(`/admin/tooltips/${encodeURIComponent(entry.key)}`, {
        method: 'DELETE',
        token: session.token,
      })
      setDrafts((current) => {
        const next = { ...current }
        delete next[entry.key]
        return next
      })
      await loadOverrides()
      reloadTipOverrides()
      setMessage(`راهنمای «${entry.label}» به متن پیش‌فرض برگشت.`)
    } catch (resetError) {
      setError(getReadableError(resetError))
    } finally {
      setSavingKey(null)
    }
  }

  const pages = useMemo(() => [...new Set(TOOLTIP_REGISTRY.map((entry) => entry.page))], [])

  const sectionsForActivePage = useMemo(() => {
    const sections = TOOLTIP_REGISTRY
      .filter((entry) => entry.page === activePage && entry.section)
      .map((entry) => entry.section)
    return [...new Set(sections)]
  }, [activePage])

  const visibleEntries = useMemo(() => {
    const query = search.trim().toLowerCase()
    return TOOLTIP_REGISTRY.filter((entry) => {
      if (entry.page !== activePage) {
        return false
      }
      if (activeSection !== 'all' && entry.section && entry.section !== activeSection) {
        return false
      }
      if (!query) {
        return true
      }
      return [entry.label, entry.section || '', entry.text, tooltipDefault(entry.key), entry.key]
        .join(' ')
        .toLowerCase()
        .includes(query)
    })
  }, [search, activePage, activeSection])

  const customizedCount = overrides.filter((override) => override.text !== tooltipDefault(override.key)).length

  return (
    <div className="tooltips-page">
      <header className="tooltips-page-head">
        <div>
          <h1>مدیریت راهنماها (Tooltip) — منوی مشتری و پنل ادمین</h1>
          <p>
            متن راهنماهای بخش‌های مختلف «منوی دیجیتال مشتری» و «پنل ادمین» را بنویسید، ویرایش کنید یا هر کدام را خاموش کنید.
            {' '}
            {customizedCount > 0 ? `${customizedCount} راهنما سفارشی شده است.` : 'فعلاً همه روی متن پیش‌فرض هستند.'}
          </p>
        </div>
        <Tip k="tooltips-page.search" />
      </header>

      {error ? <p className="tooltips-alert tooltips-alert-error">{error}</p> : null}
      {message ? <p className="tooltips-alert tooltips-alert-ok">{message}</p> : null}

      <div className="tooltips-toolbar">
        <input
          type="search"
          placeholder="جستجو در عنوان، بخش یا متن راهنماها..."
          value={search}
          onChange={(event) => setSearch(event.target.value)}
        />
        <div className="tooltips-page-tabs">
          {pages.map((page) => {
            const count = TOOLTIP_REGISTRY.filter((e) => e.page === page).length
            return (
              <button
                key={page}
                type="button"
                className={activePage === page ? 'active' : ''}
                onClick={() => {
                  setActivePage(page)
                  setActiveSection('all')
                }}
              >
                <span>{tooltipPageLabel(page)}</span>
                <span className="tooltips-tab-count">{count}</span>
              </button>
            )
          })}
        </div>
      </div>

      {sectionsForActivePage.length > 0 ? (
        <div className="tooltips-section-chips" role="tablist" aria-label="فیلتر بخش‌های منوی مشتری">
          <button
            type="button"
            className={activeSection === 'all' ? 'active' : ''}
            onClick={() => setActiveSection('all')}
          >
            همه بخش‌ها
          </button>
          {sectionsForActivePage.map((section) => (
            <button
              key={section}
              type="button"
              className={activeSection === section ? 'active' : ''}
              onClick={() => setActiveSection(section)}
            >
              {section}
            </button>
          ))}
        </div>
      ) : null}

      {loading ? <p className="tooltips-hint">در حال بارگذاری...</p> : null}

      {!loading && visibleEntries.length === 0 ? (
        <p className="tooltips-hint">راهنمایی مطابق جستجو پیدا نشد.</p>
      ) : null}

      <div className="tooltips-list">
        {visibleEntries.map((entry) => {
          const override = overrideByKey[entry.key]
          const isActive = override ? override.isActive : true
          const customized = Boolean(override) && override.text !== tooltipDefault(entry.key)
          const currentDraft = draftFor(entry)
          return (
            <article
              key={entry.key}
              className={`tooltip-card ${!isActive ? 'tooltip-card-off' : ''}`}
            >
              <div className="tooltip-card-head">
                <div className="tooltip-card-title-group">
                  {entry.section ? (
                    <span className="tooltip-card-section-badge">{entry.section}</span>
                  ) : null}
                  <strong>{entry.label}</strong>
                </div>
                <span className={`tooltip-card-status ${isActive ? 'is-on' : 'is-off'}`}>
                  {isActive ? 'فعال در صفحه' : 'غیرفعال (مخفی)'}
                  {customized ? ' · متن سفارشی' : ' · متن پیش‌فرض'}
                </span>
              </div>

              <p className="tooltip-card-default">
                پیش‌فرض: {tooltipDefault(entry.key)}
              </p>

              <textarea
                rows={2}
                value={currentDraft}
                placeholder="متن راهنما برای نمایش به کاربر..."
                onChange={(event) => setDraft(entry.key, event.target.value)}
                disabled={!canUpdate || savingKey === entry.key}
              />

              {isActive && currentDraft.trim() ? (
                <div className="tooltip-live-preview" aria-label="پیش‌نمایش راهنما">
                  <span className="tooltip-live-preview-badge">؟</span>
                  <div className="tooltip-live-preview-bubble">
                    <small className="tooltip-live-preview-title">{entry.label}</small>
                    <p>{currentDraft}</p>
                  </div>
                </div>
              ) : null}

              <div className="tooltip-card-actions">
                <button
                  type="button"
                  className="tooltips-btn tooltips-btn-primary"
                  onClick={() => save(entry)}
                  disabled={!canUpdate || savingKey === entry.key || currentDraft.trim() === (override?.text ?? tooltipDefault(entry.key))}
                >
                  {savingKey === entry.key ? '...' : 'ذخیره متن'}
                </button>
                <button
                  type="button"
                  className="tooltips-btn"
                  onClick={() => toggleActive(entry)}
                  disabled={!canUpdate || savingKey === entry.key}
                >
                  {isActive ? 'غیرفعال کن' : 'فعال کن'}
                </button>
                {override ? (
                  <button
                    type="button"
                    className="tooltips-btn tooltips-btn-ghost"
                    onClick={() => reset(entry)}
                    disabled={!canUpdate || savingKey === entry.key}
                  >
                    بازنشانی به پیش‌فرض
                  </button>
                ) : null}
                <code className="tooltip-card-key">{entry.key}</code>
              </div>
            </article>
          )
        })}
      </div>

      {!canUpdate ? (
        <p className="tooltips-hint">
          برای ویرایش راهنماها به مجوز «ویرایش / غیرفعال‌سازی راهنماها» نیاز دارید.
        </p>
      ) : null}
    </div>
  )
}
