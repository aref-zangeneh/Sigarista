import { useEffect, useRef, useState } from 'react'
import { NavLink, Outlet, useLocation, useNavigate } from 'react-router-dom'
import { OrderAlertsBar } from '../components/OrderAlertsBar'
import { LowStockAlertBar } from '../components/LowStockAlertBar'
import { OrderEditorModal } from '../components/OrderEditorModal'
import { adminRoutes, routeForPath } from '../adminRoutes'
import { API_BASE_URL } from '../utils/adminApi'
import { toPersianNumber } from '../utils/adminFormatters'
import { getAccessibleRoutes, hasPermission, PERMISSIONS } from '../utils/adminPermissions'
import { useAdmin } from '../context/AdminContext'

/* ── Minimal stroke icons (Tabler-style) ───────────────── */
const icon = (paths) => (
  <svg
    className="nav-link-icon"
    viewBox="0 0 24 24"
    fill="none"
    stroke="currentColor"
    strokeWidth="1.7"
    strokeLinecap="round"
    strokeLinejoin="round"
    aria-hidden="true"
  >
    {paths}
  </svg>
)

const ROUTE_ICONS = {
  overview: icon(
    <>
      <rect x="3" y="3" width="7" height="7" rx="1.5" />
      <rect x="14" y="3" width="7" height="7" rx="1.5" />
      <rect x="3" y="14" width="7" height="7" rx="1.5" />
      <rect x="14" y="14" width="7" height="7" rx="1.5" />
    </>,
  ),
  salon: icon(
    <>
      <rect x="3" y="4" width="18" height="6" rx="2" />
      <path d="M6 14h12M8 20h8" />
    </>,
  ),
  categories: icon(
    <>
      <path d="M3 7a2 2 0 0 1 2-2h4l2 2h8a2 2 0 0 1 2 2v8a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2z" />
    </>,
  ),
  'menu-items': icon(
    <>
      <path d="M4 6h16M4 12h16M4 18h10" />
    </>,
  ),
  'cost-dashboard': icon(
    <>
      <path d="M3 17l6-6 4 4 8-8" />
      <path d="M15 7h6v6" />
    </>,
  ),
  addons: icon(
    <>
      <circle cx="12" cy="12" r="9" />
      <path d="M12 8v8M8 12h8" />
    </>,
  ),
  modifiers: icon(
    <>
      <path d="M4 7h10M18 7h2M4 17h4M12 17h8" />
      <circle cx="16" cy="7" r="2" />
      <circle cx="10" cy="17" r="2" />
    </>,
  ),
  ingredients: icon(
    <>
      <path d="M12 3 4 7v10l8 4 8-4V7z" />
      <path d="M4 7l8 4 8-4M12 11v10" />
    </>,
  ),
  recipes: icon(
    <>
      <path d="M6 3h9l4 4v14H6z" />
      <path d="M14 3v5h5M9 13h6M9 17h6" />
    </>,
  ),
  'product-wiki': icon(
    <>
      <path d="M4 5a2 2 0 0 1 2-2h13v16H6a2 2 0 0 0-2 2z" />
      <path d="M4 19a2 2 0 0 1 2-2h13" />
    </>,
  ),
  inventory: icon(
    <>
      <rect x="3" y="4" width="18" height="5" rx="1.5" />
      <path d="M5 9v10a1 1 0 0 0 1 1h12a1 1 0 0 0 1-1V9M10 14h4" />
    </>,
  ),
  'inventory-purchases': icon(
    <>
      <path d="M3 4h2l2.5 11h10L20 7H6.5" />
      <circle cx="9" cy="19" r="1.5" />
      <circle cx="17" cy="19" r="1.5" />
    </>,
  ),
  production: icon(
    <>
      <path d="M4 20V10l5 3V10l5 3V10l5 3v7z" />
      <path d="M4 20h16" />
    </>,
  ),
  'record-sale': icon(
    <>
      <path d="M6 3h12v18l-3-2-3 2-3-2-3 2z" />
      <path d="M9 8h6M9 12h6" />
    </>,
  ),
  pos: icon(
    <>
      <rect x="3" y="5" width="18" height="12" rx="2" />
      <path d="M7 21h10M8 9h8M8 13h5" />
    </>,
  ),
  accounting: icon(
    <>
      <rect x="3" y="6" width="18" height="13" rx="2" />
      <path d="M3 10h18M16 15h2" />
    </>,
  ),
  orders: icon(
    <>
      <path d="M8 4h8a1 1 0 0 1 1 1v1H7V5a1 1 0 0 1 1-1z" />
      <path d="M7 6H5.5A1.5 1.5 0 0 0 4 7.5v11A1.5 1.5 0 0 0 5.5 20h13a1.5 1.5 0 0 0 1.5-1.5v-11A1.5 1.5 0 0 0 18.5 6H17" />
      <path d="M9 11h6M9 15h4" />
    </>,
  ),
  'site-settings': icon(
    <>
      <circle cx="12" cy="12" r="3" />
      <path d="M19 12a7 7 0 0 0-.14-1.4l2-1.56-2-3.46-2.36 1a7 7 0 0 0-2.42-1.4L13.6 2h-3.2l-.08 2.68a7 7 0 0 0-2.42 1.4l-2.36-1-2 3.46 2 1.56A7 7 0 0 0 5 12c0 .48.05.94.14 1.4l-2 1.56 2 3.46 2.36-1a7 7 0 0 0 2.42 1.4l.08 2.68h3.2l.08-2.68a7 7 0 0 0 2.42-1.4l2.36 1 2-3.46-2-1.56c.09-.46.14-.92.14-1.4z" />
    </>,
  ),
  hr: icon(

    <>
      <circle cx="8" cy="7.5" r="3" />
      <path d="M2.5 19a5.5 5.5 0 0 1 11 0" />
      <path d="M14.5 4.5a3 3 0 0 1 0 6M13.5 13.8a5.5 5.5 0 0 1 3.9 5.2" />
      <path d="M20 9.5v5M17.5 12h5" />
    </>,
  ),
  assets: icon(
    <>
      <rect x="4" y="7" width="16" height="13" rx="2" />
      <path d="M9 7V5a3 3 0 0 1 6 0v2" />
      <path d="M4 12h16" />
    </>,
  ),
  'activity-log': icon(
    <>
      <path d="M9 4h6a2 2 0 0 1 2 2v1" />
      <rect x="4" y="5" width="16" height="16" rx="3" />
      <path d="M8 10h8M8 14h8M8 18h5" />
    </>,
  ),
  'admin-users': icon(
    <>
      <circle cx="9" cy="8" r="3.2" />
      <path d="M3.5 19a5.5 5.5 0 0 1 11 0M16 5.5a3.2 3.2 0 0 1 0 5M17 13.6a5.5 5.5 0 0 1 3.5 5.4" />
    </>,
  ),
}

/* ── Sidebar groups (world-class admin nav pattern) ─────── */
const NAV_GROUPS = [
  { title: 'اصلی', routes: ['overview', 'cost-dashboard', 'orders', 'pos', 'delivery-methods', 'customers', 'salon'] },
  { title: 'منو و محصولات', routes: ['categories', 'menu-items', 'addons', 'modifiers'] },
  { title: 'آشپزخانه و تولید', routes: ['ingredients', 'recipes', 'production', 'product-wiki'] },
  { title: 'انبار', routes: ['inventory', 'inventory-purchases', 'suppliers', 'assets'] },
  { title: 'فروش و مالی', routes: ['record-sale', 'accounting', 'exports'] },
  { title: 'منابع انسانی', routes: ['hr'] },
  { title: 'سیستم', routes: ['site-settings', 'admin-users', 'activity-log'] },
]

export function AdminLayout() {
  const navigate = useNavigate()
  const location = useLocation()
  const [mobileNavOpen, setMobileNavOpen] = useState(false)
  const burgerRef = useRef(null)
  const sidebarCloseRef = useRef(null)
  const wasMobileNavOpen = useRef(false)
  const [theme, setTheme] = useState(() => (document.documentElement.dataset.theme === 'dark' ? 'dark' : 'light'))

  const toggleTheme = () => {
    const next = theme === 'dark' ? 'light' : 'dark'
    document.documentElement.dataset.theme = next
    localStorage.setItem('sugarista.admin.theme', next)
    setTheme(next)
  }
  const {
    session,
    handleLogout,
    orderAlerts,
    lowStockAlert,
    orderAlertPulse,
    openOrderEditor,
    dismissOrderAlert,
    message,
    error,
    loading,
    loadAdminData,
  } = useAdmin()

  const visibleRoutes = getAccessibleRoutes(session, adminRoutes)
  const canViewOrders = hasPermission(session, PERMISSIONS.orders.view)
  const canViewInventory = hasPermission(session, PERMISSIONS.inventory.view)
  const currentRoute = routeForPath(location.pathname)
  const isPosRoute = location.pathname.includes('/pos')

  const groupedIds = new Set(NAV_GROUPS.flatMap((group) => group.routes))
  const extraRoutes = visibleRoutes.filter((route) => !groupedIds.has(route.id))

  useEffect(() => {
    setMobileNavOpen(false)
  }, [location.pathname])

  useEffect(() => {
    if (!mobileNavOpen) return undefined
    const onKeyDown = (event) => {
      if (event.key === 'Escape') setMobileNavOpen(false)
    }
    window.addEventListener('keydown', onKeyDown)
    return () => window.removeEventListener('keydown', onKeyDown)
  }, [mobileNavOpen])

  useEffect(() => {
    if (mobileNavOpen) {
      wasMobileNavOpen.current = true
      sidebarCloseRef.current?.focus()
      return undefined
    }

    if (wasMobileNavOpen.current) {
      wasMobileNavOpen.current = false
      burgerRef.current?.focus()
    }

    return undefined
  }, [mobileNavOpen])

  if (isPosRoute) {
    return (
      <div className="pos-layout">
        <Outlet />
        <OrderEditorModal />
      </div>
    )
  }

  const renderNavLink = (route) => (
    <NavLink
      key={route.id}
      to={`/${route.path}`}
      className={({ isActive }) =>
        isActive ? 'nav-link nav-link--active' : 'nav-link'
      }
      onClick={() => setMobileNavOpen(false)}
    >
      {ROUTE_ICONS[route.id] ?? ROUTE_ICONS.overview}
      <span className="nav-link-label">{route.label}</span>
      {route.id === 'orders' && orderAlerts?.pendingKitchenCount > 0 ? (
        <span className="nav-badge">{toPersianNumber(orderAlerts.pendingKitchenCount)}</span>
      ) : null}
    </NavLink>
  )

  return (
    <div className="admin-shell">
      <a className="skip-link" href="#admin-main">
        رفتن به محتوای اصلی
      </a>
      <aside
        className={mobileNavOpen ? 'sidebar sidebar--open' : 'sidebar'}
        aria-label="ناوبری مدیریت"
      >
        <div className="sidebar-head">
          <span className="sidebar-logo">S</span>
          <span className="sidebar-brand-text">Sugarista</span>
          <button
            ref={sidebarCloseRef}
            type="button"
            className="topbar-icon-btn sidebar-close"
            onClick={() => setMobileNavOpen(false)}
            aria-label="بستن منو"
          >
            <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.8">
              <path d="M6 6l12 12M18 6L6 18" />
            </svg>
          </button>
        </div>

        <nav className="sidebar-nav">
          {NAV_GROUPS.map((group) => {
            const items = visibleRoutes.filter((route) => group.routes.includes(route.id))
            if (items.length === 0) return null
            return (
              <div key={group.title} className="nav-group">
                <p className="nav-group-title">{group.title}</p>
                {items.map(renderNavLink)}
              </div>
            )
          })}
          {extraRoutes.length > 0 ? (
            <div className="nav-group">
              {extraRoutes.map(renderNavLink)}
            </div>
          ) : null}
        </nav>

        <div className="sidebar-footer">
          <div className="sidebar-user">
            <span className="sidebar-avatar">
              {(session?.displayName || session?.username || 'S').trim().charAt(0)}
            </span>
            <div className="sidebar-user-meta">
              <strong>{session?.displayName || 'کاربر'}</strong>
              <small className="ltr-text">@{session?.username}</small>
            </div>
          </div>
          {session?.isSuperAdmin ? <span className="session-role">مدیر ارشد</span> : null}
          <button type="button" className="logout-button" onClick={handleLogout}>
            خروج
          </button>
        </div>
      </aside>

      {mobileNavOpen ? (
        <button
          type="button"
          className="sidebar-overlay"
          onClick={() => setMobileNavOpen(false)}
          aria-label="بستن منو"
        />
      ) : null}

      <div className="main-wrap" inert={mobileNavOpen ? true : undefined}>
        <header className="topbar">
          <div className="topbar-start">
            <button
              ref={burgerRef}
              type="button"
              className="topbar-burger"
              onClick={() => setMobileNavOpen((open) => !open)}
              aria-label="باز کردن منو"
            >
              <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.8">
                <path d="M4 7h16M4 12h16M4 17h16" />
              </svg>
            </button>
            <div className="topbar-heading">
              <h1 className="topbar-title">{currentRoute?.label ?? 'پنل مدیریت'}</h1>
              <p className="topbar-sub">پنل مدیریت شوگاریستا</p>
            </div>
          </div>

          <div className="topbar-end">
            <code className="topbar-chip ltr-text">{API_BASE_URL}</code>
            <button
              type="button"
              className="topbar-icon-btn"
              onClick={toggleTheme}
              title={theme === 'dark' ? 'تم روشن' : 'تم تیره'}
              aria-label={theme === 'dark' ? 'فعال‌سازی تم روشن' : 'فعال‌سازی تم تیره'}
            >
              {theme === 'dark' ? (
                <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.8" strokeLinecap="round">
                  <circle cx="12" cy="12" r="4.2" />
                  <path d="M12 2.5v2M12 19.5v2M2.5 12h2M19.5 12h2M5.2 5.2l1.4 1.4M17.4 17.4l1.4 1.4M18.8 5.2l-1.4 1.4M6.6 17.4l-1.4 1.4" />
                </svg>
              ) : (
                <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.8" strokeLinecap="round" strokeLinejoin="round">
                  <path d="M20.4 14.2A8.4 8.4 0 0 1 9.8 3.6a8.5 8.5 0 1 0 10.6 10.6Z" />
                </svg>
              )}
            </button>
            <button
              type="button"
              className="topbar-icon-btn"
              onClick={() => loadAdminData()}
              title="تازه‌سازی اطلاعات"
              aria-label="تازه‌سازی اطلاعات"
            >
              <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.8">
                <path d="M20 11a8 8 0 1 0-.7 4.3" />
                <path d="M20 5v6h-6" />
              </svg>
            </button>
            <span className="topbar-user">
              <span className="topbar-avatar">
                {(session?.displayName || session?.username || 'S').trim().charAt(0)}
              </span>
              <span className="topbar-user-meta">
                <span className="topbar-username">{session?.displayName || 'کاربر'}</span>
                <span className="topbar-role">
                  {session?.isSuperAdmin ? 'مدیر ارشد' : 'کاربر'}
                </span>
              </span>
            </span>
          </div>
        </header>

        <main className="content" id="admin-main" tabIndex={-1}>
          {message ? (
            <p className="feedback feedback-success" role="status" aria-live="polite">
              {message}
            </p>
          ) : null}
          {error ? (
            <p className="feedback feedback-error" role="alert" aria-live="assertive">
              {error}
            </p>
          ) : null}
          {loading ? (
            <p className="feedback" role="status" aria-live="polite">
              در حال بارگذاری جدیدترین اطلاعات...
            </p>
          ) : null}

          {canViewOrders ? (
            <OrderAlertsBar
              alerts={orderAlerts}
              pulse={orderAlertPulse}
              onOpenOrders={() => navigate('/orders')}
              onOpenOrder={(order) => {
                navigate('/orders')
                openOrderEditor(order)
              }}
              onDismissNew={dismissOrderAlert}
            />
          ) : null}

          {canViewInventory ? (
            <LowStockAlertBar alert={lowStockAlert} onOpenInventory={() => navigate('/inventory')} />
          ) : null}

          <Outlet />
          <OrderEditorModal />
        </main>
      </div>
    </div>
  )
}
