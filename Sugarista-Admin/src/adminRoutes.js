import { viewPermissionForRoute } from './utils/adminPermissions'

export const adminRoutes = [
  { id: 'overview', path: 'dashboard', label: 'نمای کلی' },
  { id: 'cost-dashboard', path: 'cost-dashboard', label: 'کاست کنترل و قیمت‌گذاری' },
  { id: 'salon', path: 'salon-state', label: 'وضعیت سالن' },
  { id: 'categories', path: 'category', label: 'دسته‌بندی‌ها' },
  { id: 'menu-items', path: 'menu-items', label: 'آیتم‌های منو' },
  { id: 'option-templates', path: 'option-templates', label: 'قالب‌های آپشن' },
  { id: 'addons', path: 'addons', label: 'افزودنی‌ها (اضافات)' },
  { id: 'discounts', path: 'discounts', label: 'تخفیف‌ها' },
  { id: 'discount-reports', path: 'discount-reports', label: 'گزارش تخفیف‌ها' },
  { id: 'discount-security', path: 'discount-security', label: 'امنیت تخفیف‌ها' },
  { id: 'modifiers', path: 'modifiers', label: 'تغییرات سفارش' },
  { id: 'drink-builder', path: 'drink-builder', label: 'سازندهٔ نوشیدنی' },
  { id: 'ingredients', path: 'ingredients', label: 'مواد اولیه' },
  { id: 'suppliers', path: 'suppliers', label: 'تامین‌کنندگان' },
  { id: 'recipes', path: 'recipes', label: 'دستور پخت' },
  { id: 'product-wiki', path: 'product-wiki', label: 'ویکی محصولات' },
  { id: 'blog', path: 'blog', label: 'مجله و بلاگ (SEO)' },
  { id: 'inventory', path: 'inventory', label: 'انبار موجودی' },
  { id: 'inventory-purchases', path: 'inventory-purchases', label: 'خرید مواد اولیه' },
  { id: 'warehouses', path: 'warehouses', label: 'انبارها' },
  { id: 'warehouse-vouchers', path: 'warehouse-vouchers', label: 'رسید ورود و سند خروج' },
  { id: 'production', path: 'production', label: 'تولید' },
  { id: 'record-sale', path: 'record-sale', label: 'ثبت فروش' },
  { id: 'pos', path: 'pos', label: 'صندوق فروش' },
  { id: 'delivery-methods', path: 'delivery-methods', label: 'روش‌های تحویل' },
  { id: 'accounting', path: 'accounting', label: 'حسابداری' },
  { id: 'exports', path: 'exports', label: 'خروجی اکسل' },
  { id: 'orders', path: 'orders', label: 'سفارش‌ها' },
  { id: 'customers', path: 'customers', label: 'باشگاه مشتریان' },
  { id: 'customer-groups', path: 'customer-groups', label: 'گروه‌های مشتری' },
  { id: 'subscription-packages', path: 'subscription-packages', label: 'اشتراک ماهانه' },
  { id: 'badges', path: 'badges', label: 'بج‌های باشگاه' },
  { id: 'assets', path: 'assets-registry', label: 'اموال و تجهیزات' },
  { id: 'maintenance', path: 'equipment-maintenance', label: 'سرویس دوره‌ای تجهیزات' },
  { id: 'hr', path: 'hr', label: 'منابع انسانی' },
  { id: 'site-settings', path: 'site-settings', label: 'تنظیمات سایت' },
  { id: 'tooltips', path: 'tooltips', label: 'راهنماهای صفحه‌ها' },
  { id: 'special-routes', path: 'special-routes', label: 'مسیرهای متفرقه' },
  { id: 'system-logs', path: 'system-logs', label: 'لاگ‌ها و سلامت سیستم' },
  { id: 'sms-dashboard', path: 'sms-dashboard', label: 'داشبورد پیامک' },
  { id: 'sms-send', path: 'sms-send', label: 'ارسال پیامک' },
  { id: 'sms-bulk', path: 'sms-bulk', label: 'ارسال گروهی' },
  { id: 'sms-contacts', path: 'sms-contacts', label: 'دفترچه مخاطبین' },
  { id: 'sms-groups', path: 'sms-groups', label: 'گروه‌های مخاطبین' },
  { id: 'sms-templates', path: 'sms-templates', label: 'قالب‌های پیامکی' },
  { id: 'sms-history', path: 'sms-history', label: 'تاریخچه ارسال‌ها' },
  { id: 'sms-providers', path: 'sms-providers', label: 'سرویس‌دهندگان پیامک' },
  { id: 'sms-settings', path: 'sms-settings', label: 'تنظیمات پیامک' },
  { id: 'admin-users', path: 'admin-users', label: 'مدیریت کاربران' },
  { id: 'activity-log', path: 'activity-log', label: 'لاگ فعالیت‌ها' },
].map((route) => ({
  ...route,
  viewPermission: viewPermissionForRoute(route.id),
}))

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

export function routeForPath(pathname) {
  const normalized = (pathname ?? '').replace(/\/+$/, '')
  const segment = normalized.split('/').filter(Boolean).pop() ?? 'dashboard'
  return routeByPath[segment] ?? routeById.overview
}
