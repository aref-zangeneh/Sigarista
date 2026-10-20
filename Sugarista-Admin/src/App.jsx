import { useEffect } from 'react'
import { Navigate, Route, Routes, useLocation, useNavigate } from 'react-router-dom'
import { AdminProvider, useAdmin } from './context/AdminContext'
import { AdminLayout } from './layout/AdminLayout'
import { LoginPage } from './pages/LoginPage'
import { adminRoutes, isKnownAdminPath, routeForPath } from './adminRoutes'
import { canViewRoute, getDefaultRoutePath } from './utils/adminPermissions'
import { DashboardPage } from './pages/DashboardPage'
import { CostDashboardPage } from './pages/CostDashboardPage'
import { SalonStatePage } from './pages/SalonStatePage'
import { CategoryPage } from './pages/CategoryPage'
import { MenuItemsPage } from './pages/MenuItemsPage'
import { AddonsPage } from './pages/AddonsPage'
import { ModifiersPage } from './pages/ModifiersPage'
import { IngredientsPage } from './pages/IngredientsPage'
import { SuppliersPage } from './pages/SuppliersPage'
import { DeliveryMethodsPage } from './pages/DeliveryMethodsPage'
import { RecipesPage } from './pages/RecipesPage'
import { ProductWikiPage } from './pages/ProductWikiPage'
import { InventoryPage } from './pages/InventoryPage'
import { InventoryPurchasesPage } from './pages/InventoryPurchasesPage'
import { ProductionPage } from './pages/ProductionPage'
import { RecordSalePage } from './pages/RecordSalePage'
import { PosPage } from './pages/PosPage'
import { AccountingPage } from './pages/AccountingPage'
import { OrdersPage } from './pages/OrdersPage'
import { CustomersPage } from './pages/CustomersPage'
import { ExportsPage } from './pages/ExportsPage'
import { SiteSettingsPage } from './pages/SiteSettingsPage'
import { AdminUsersPage } from './pages/AdminUsersPage'
import './App.css'

const pageComponents = {
  overview: DashboardPage,
  'cost-dashboard': CostDashboardPage,
  salon: SalonStatePage,
  categories: CategoryPage,
  'menu-items': MenuItemsPage,
  addons: AddonsPage,
  modifiers: ModifiersPage,
  ingredients: IngredientsPage,
  recipes: RecipesPage,
  'product-wiki': ProductWikiPage,
  inventory: InventoryPage,
  'inventory-purchases': InventoryPurchasesPage,
  suppliers: SuppliersPage,
  production: ProductionPage,
  'record-sale': RecordSalePage,
  pos: PosPage,
  'delivery-methods': DeliveryMethodsPage,
  accounting: AccountingPage,
  orders: OrdersPage,
  customers: CustomersPage,
  exports: ExportsPage,
  'site-settings': SiteSettingsPage,
  'admin-users': AdminUsersPage,
}

function ProtectedRoute({ route, children }) {
  const { session } = useAdmin()

  if (!canViewRoute(session, route.id)) {
    const fallbackPath = getDefaultRoutePath(session, adminRoutes)
    return <Navigate to={fallbackPath ? `/${fallbackPath}` : '/login'} replace />
  }

  return children
}

function NoAccessPage() {
  const { handleLogout, session } = useAdmin()

  return (
    <main className="login-shell">
      <section className="login-card">
        <p className="eyebrow">دسترسی محدود</p>
        <h1>مجوزی برای پنل تعریف نشده</h1>
        <p className="login-copy">
          حساب {session?.displayName} فعال است اما هیچ بخشی از پنل برای آن مجاز نشده است.
          با مدیر ارشد تماس بگیرید.
        </p>
        <button type="button" onClick={handleLogout}>
          خروج
        </button>
      </section>
    </main>
  )
}

function AdminRoutes() {
  const { session } = useAdmin()
  const location = useLocation()
  const navigate = useNavigate()

  useEffect(() => {
    document.documentElement.lang = 'fa'
    document.documentElement.dir = 'rtl'
    document.title = 'پنل مدیریت شوگاریستا'
  }, [])

  useEffect(() => {
    if (!session?.token) {
      return
    }

    const normalizedPath = location.pathname.replace(/\/+$/, '') || '/'
    if (normalizedPath === '/' || normalizedPath === '') {
      const defaultPath = getDefaultRoutePath(session, adminRoutes) ?? 'dashboard'
      navigate(`/${defaultPath}`, { replace: true })
      return
    }

    if (!isKnownAdminPath(location.pathname)) {
      const defaultPath = getDefaultRoutePath(session, adminRoutes) ?? 'dashboard'
      navigate(`/${defaultPath}`, { replace: true })
      return
    }

    const route = routeForPath(location.pathname)
    if (!canViewRoute(session, route.id)) {
      const defaultPath = getDefaultRoutePath(session, adminRoutes)
      navigate(defaultPath ? `/${defaultPath}` : '/login', { replace: true })
    }
  }, [session, location.pathname, navigate])

  if (!session?.token) {
    return <LoginPage />
  }

  const defaultPath = getDefaultRoutePath(session, adminRoutes)
  if (!defaultPath) {
    return <NoAccessPage />
  }

  return (
    <Routes>
      <Route element={<AdminLayout />}>
        {adminRoutes.map((route) => {
          const Page = pageComponents[route.id]
          return (
            <Route
              key={route.id}
              path={route.path}
              element={
                <ProtectedRoute route={route}>
                  <Page />
                </ProtectedRoute>
              }
            />
          )
        })}
        <Route
          path="/"
          element={<Navigate to={defaultPath ? `/${defaultPath}` : '/login'} replace />}
        />
        <Route
          path="*"
          element={<Navigate to={defaultPath ? `/${defaultPath}` : '/login'} replace />}
        />
      </Route>
    </Routes>
  )
}

function App() {
  return (
    <AdminProvider>
      <AdminRoutes />
    </AdminProvider>
  )
}

export default App
