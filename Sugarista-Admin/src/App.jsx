import { useEffect } from 'react'
import { Navigate, Route, Routes, useLocation, useNavigate } from 'react-router-dom'
import { AdminProvider, useAdmin } from './context/AdminContext'
import { AdminLayout } from './layout/AdminLayout'
import { LoginPage } from './pages/LoginPage'
import { adminRoutes, isKnownAdminPath } from './adminRoutes'
import { DashboardPage } from './pages/DashboardPage'
import { SalonStatePage } from './pages/SalonStatePage'
import { CategoryPage } from './pages/CategoryPage'
import { MenuItemsPage } from './pages/MenuItemsPage'
import { AddonsPage } from './pages/AddonsPage'
import { ModifiersPage } from './pages/ModifiersPage'
import { IngredientsPage } from './pages/IngredientsPage'
import { RecipesPage } from './pages/RecipesPage'
import { ProductWikiPage } from './pages/ProductWikiPage'
import { InventoryPage } from './pages/InventoryPage'
import { InventoryPurchasesPage } from './pages/InventoryPurchasesPage'
import { ProductionPage } from './pages/ProductionPage'
import { RecordSalePage } from './pages/RecordSalePage'
import { OrdersPage } from './pages/OrdersPage'
import { SiteSettingsPage } from './pages/SiteSettingsPage'
import './App.css'

const pageComponents = {
  overview: DashboardPage,
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
  production: ProductionPage,
  'record-sale': RecordSalePage,
  orders: OrdersPage,
  'site-settings': SiteSettingsPage,
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
      navigate('/dashboard', { replace: true })
      return
    }

    if (!isKnownAdminPath(location.pathname)) {
      navigate('/dashboard', { replace: true })
    }
  }, [session?.token, location.pathname, navigate])

  if (!session?.token) {
    return <LoginPage />
  }

  return (
    <Routes>
      <Route element={<AdminLayout />}>
        {adminRoutes.map((route) => {
          const Page = pageComponents[route.id]
          return <Route key={route.id} path={route.path} element={<Page />} />
        })}
        <Route path="/" element={<Navigate to="/dashboard" replace />} />
        <Route path="*" element={<Navigate to="/dashboard" replace />} />
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
