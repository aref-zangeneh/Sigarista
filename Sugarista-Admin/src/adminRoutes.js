export const adminRoutes = [
  { id: 'overview', path: 'dashboard', label: 'نمای کلی' },
  { id: 'salon', path: 'salon-state', label: 'وضعیت سالن' },
  { id: 'categories', path: 'category', label: 'دسته‌بندی‌ها' },
  { id: 'menu-items', path: 'menu-items', label: 'آیتم‌های منو' },
  { id: 'addons', path: 'addons', label: 'افزودنی‌ها (اضافات)' },
  { id: 'modifiers', path: 'modifiers', label: 'تغییرات سفارش' },
  { id: 'ingredients', path: 'ingredients', label: 'مواد اولیه' },
  { id: 'recipes', path: 'recipes', label: 'دستور پخت' },
  { id: 'product-wiki', path: 'product-wiki', label: 'ویکی محصولات' },
  { id: 'inventory', path: 'inventory', label: 'انبار موجودی' },
  { id: 'inventory-purchases', path: 'inventory-purchases', label: 'خرید مواد اولیه' },
  { id: 'production', path: 'production', label: 'تولید' },
  { id: 'record-sale', path: 'record-sale', label: 'ثبت فروش' },
  { id: 'orders', path: 'orders', label: 'سفارش‌ها' },
  { id: 'site-settings', path: 'site-settings', label: 'تنظیمات سایت' },
]

const routeById = Object.fromEntries(adminRoutes.map((route) => [route.id, route]))
const routeByPath = Object.fromEntries(adminRoutes.map((route) => [route.path, route]))

export function pathForTab(tabId) {
  return routeById[tabId]?.path ?? 'dashboard'
}

export function tabForPath(pathname) {
  const normalized = (pathname ?? '').replace(/\/+$/, '')
  const segment = normalized.split('/').filter(Boolean).pop() ?? 'dashboard'
  return routeByPath[segment]?.id ?? 'overview'
}

export function isKnownAdminPath(pathname) {
  const normalized = (pathname ?? '').replace(/\/+$/, '')
  if (!normalized || normalized === '/') {
    return true
  }

  const segment = normalized.split('/').filter(Boolean).pop()
  return Boolean(routeByPath[segment])
}
