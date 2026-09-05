import { NavLink, Outlet, useLocation, useNavigate } from 'react-router-dom'
import { OrderAlertsBar } from '../components/OrderAlertsBar'
import { OrderEditorModal } from '../components/OrderEditorModal'
import { adminRoutes } from '../adminRoutes'
import { API_BASE_URL } from '../utils/adminApi'
import { toPersianNumber } from '../utils/adminFormatters'
import { getAccessibleRoutes, hasPermission, PERMISSIONS } from '../utils/adminPermissions'
import { useAdmin } from '../context/AdminContext'

export function AdminLayout() {
  const navigate = useNavigate()
  const location = useLocation()
  const isPosRoute = location.pathname.includes('/pos')
  const {
    session,
    handleLogout,
    orderAlerts,
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

  if (isPosRoute) {
    return (
      <div className="pos-layout">
        <Outlet />
        <OrderEditorModal />
      </div>
    )
  }

  return (
    <div className="admin-shell">
      <aside className="sidebar">

        <nav className="tab-list" aria-label="ناوبری مدیریت">
          {visibleRoutes.map((route) => (
            <NavLink
              key={route.id}
              to={`/${route.path}`}
              className={({ isActive }) =>
                isActive ? 'tab-button tab-button-active' : 'tab-button'
              }
            >
              {route.label}
              {route.id === 'orders' && orderAlerts?.pendingKitchenCount > 0 ? (
                <span className="tab-badge">{toPersianNumber(orderAlerts.pendingKitchenCount)}</span>
              ) : null}
            </NavLink>
          ))}
        </nav>

        <div className="session-card">
          <p className="session-title">{session.displayName}</p>
          <p className="session-copy ltr-text">@{session.username}</p>
          {session.isSuperAdmin ? <p className="session-role">مدیر ارشد</p> : null}
          <button type="button" className="ghost-button" onClick={handleLogout}>
            خروج
          </button>
        </div>
      </aside>

      <main className="content">
        <header className="topbar">
          <div>
            <p className="eyebrow">آدرس API</p>
            <h2 className="ltr-text">{API_BASE_URL}</h2>
          </div>
          <button type="button" className="ghost-button" onClick={() => loadAdminData()}>
            تازه‌سازی اطلاعات
          </button>
        </header>

        {message ? <p className="feedback feedback-success">{message}</p> : null}
        {error ? <p className="feedback feedback-error">{error}</p> : null}
        {loading ? <p className="feedback">در حال بارگذاری جدیدترین اطلاعات...</p> : null}

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

        <Outlet />
        <OrderEditorModal />
      </main>
    </div>
  )
}
