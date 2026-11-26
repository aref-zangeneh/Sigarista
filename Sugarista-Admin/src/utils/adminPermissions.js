export const PERMISSIONS = {
  maintenance: {
    view: 'admin.maintenance.view',
    create: 'admin.maintenance.create',
    update: 'admin.maintenance.update',
    delete: 'admin.maintenance.delete',
  },
  exports: {
    view: 'admin.accounting.view',
    create: 'admin.accounting.create',
    update: 'admin.accounting.update',
    delete: 'admin.accounting.delete',
  },
  customers: {
    view: 'admin.orders.view',
    create: 'admin.orders.update',
    update: 'admin.orders.update',
    delete: 'admin.orders.delete',
  },
  'customer-groups': {
    view: 'admin.orders.view',
    create: 'admin.orders.update',
    update: 'admin.orders.update',
    delete: 'admin.orders.delete',
  },
  'subscription-packages': {
    view: 'admin.orders.view',
    create: 'admin.orders.update',
    update: 'admin.orders.update',
    delete: 'admin.orders.delete',
  },
  'delivery-methods': {
    view: 'admin.orders.view',
    create: 'admin.orders.update',
    update: 'admin.orders.update',
    delete: 'admin.orders.delete',
  },
  assets: {
    view: 'admin.assets.view',
    create: 'admin.assets.create',
    update: 'admin.assets.update',
    delete: 'admin.assets.delete',
  },
  hr: {
    view: 'admin.hr.view',
    create: 'admin.hr.create',
    update: 'admin.hr.update',
    delete: 'admin.hr.delete',
  },
  suppliers: {
    view: 'admin.ingredients.view',
    create: 'admin.ingredients.create',
    update: 'admin.ingredients.update',
    delete: 'admin.ingredients.delete',
  },
  dashboard: {
    view: 'admin.dashboard.view',
    update: 'admin.dashboard.update',
  },
  'cost-dashboard': {
    view: 'admin.dashboard.view',
    update: 'admin.dashboard.update',
  },
  salon: {
    view: 'admin.salon.view',
    create: 'admin.salon.create',
    update: 'admin.salon.update',
    delete: 'admin.salon.delete',
  },
  categories: {
    view: 'admin.categories.view',
    create: 'admin.categories.create',
    update: 'admin.categories.update',
    delete: 'admin.categories.delete',
  },
  'menu-items': {
    view: 'admin.menu-items.view',
    create: 'admin.menu-items.create',
    update: 'admin.menu-items.update',
    delete: 'admin.menu-items.delete',
  },
  'quick-stock': {
    view: 'admin.menu-items.view',
    create: 'admin.menu-items.update',
    update: 'admin.menu-items.update',
    delete: 'admin.menu-items.update',
  },
  'option-templates': {
    view: 'admin.menu-items.view',
    create: 'admin.menu-items.create',
    update: 'admin.menu-items.update',
    delete: 'admin.menu-items.delete',
  },
  addons: {
    view: 'admin.addons.view',
    create: 'admin.addons.create',
    update: 'admin.addons.update',
    delete: 'admin.addons.delete',
  },
  discounts: {
    view: 'admin.discounts.view',
    create: 'admin.discounts.create',
    update: 'admin.discounts.update',
    delete: 'admin.discounts.delete',
  },
  modifiers: {
    view: 'admin.modifiers.view',
    create: 'admin.modifiers.create',
    update: 'admin.modifiers.update',
    delete: 'admin.modifiers.delete',
  },
  'drink-builder': {
    view: 'admin.drink-builder.view',
    create: 'admin.drink-builder.create',
    update: 'admin.drink-builder.update',
    delete: 'admin.drink-builder.delete',
  },
  ingredients: {
    view: 'admin.ingredients.view',
    create: 'admin.ingredients.create',
    update: 'admin.ingredients.update',
    delete: 'admin.ingredients.delete',
  },
  recipes: {
    view: 'admin.recipes.view',
    create: 'admin.recipes.create',
    update: 'admin.recipes.update',
    delete: 'admin.recipes.delete',
  },
  'product-wiki': {
    view: 'admin.product-wiki.view',
    update: 'admin.product-wiki.update',
  },
  blog: {
    view: 'admin.blog.view',
    create: 'admin.blog.create',
    update: 'admin.blog.update',
    delete: 'admin.blog.delete',
  },
  inventory: {
    view: 'admin.inventory.view',
    create: 'admin.inventory.create',
    update: 'admin.inventory.update',
  },
  'inventory-purchases': {
    view: 'admin.inventory-purchases.view',
    create: 'admin.inventory-purchases.create',
  },
  warehouses: {
    view: 'admin.warehouses.view',
    create: 'admin.warehouses.create',
    update: 'admin.warehouses.update',
    delete: 'admin.warehouses.delete',
  },
  production: {
    view: 'admin.production.view',
    create: 'admin.production.create',
  },
  'record-sale': {
    view: 'admin.record-sale.view',
    create: 'admin.record-sale.create',
  },
  orders: {
    view: 'admin.orders.view',
    update: 'admin.orders.update',
    delete: 'admin.orders.delete',
  },
  pos: {
    view: 'admin.pos.view',
    create: 'admin.pos.create',
    update: 'admin.pos.update',
    delete: 'admin.pos.delete',
    pay: 'admin.pos.pay',
    applyDiscount: 'admin.pos.apply-discount',
    managerNote: 'admin.pos.manager-note',
  },
  accounting: {
    view: 'admin.accounting.view',
    create: 'admin.accounting.create',
    update: 'admin.accounting.update',
    delete: 'admin.accounting.delete',
  },
  'site-settings': {
    view: 'admin.site-settings.view',
    update: 'admin.site-settings.update',
  },
  tooltips: {
    view: 'admin.tooltips.view',
    update: 'admin.tooltips.update',
  },
  'special-routes': {
    view: 'admin.site-settings.view',
    update: 'admin.site-settings.update',
  },
  'system-logs': {
    view: 'admin.system-logs.view',
    delete: 'admin.system-logs.delete',
  },
  'sms-dashboard': {
    view: 'admin.sms.view-dashboard',
  },
  'sms-send': {
    view: 'admin.sms.send-single',
  },
  'sms-bulk': {
    view: 'admin.sms.send-bulk',
  },
  'sms-contacts': {
    view: 'admin.sms.manage-contacts',
  },
  'sms-groups': {
    view: 'admin.sms.manage-groups',
  },
  'sms-templates': {
    view: 'admin.sms.manage-templates',
  },
  'sms-history': {
    view: 'admin.sms.view-history',
  },
  'sms-providers': {
    view: 'admin.sms.manage-providers',
  },
  'sms-settings': {
    view: 'admin.sms.manage-settings',
  },
  'admin-users': {
    view: 'admin.users.view',
    create: 'admin.users.create',
    update: 'admin.users.update',
    delete: 'admin.users.delete',
    managePermissions: 'admin.users.manage-permissions',
  },
  'activity-log': {
    view: 'admin.activity-logs.view',
    delete: 'admin.activity-logs.delete',
  },
}

export function viewPermissionForRoute(routeId) {
  if (routeId === 'admin-users') {
    return PERMISSIONS['admin-users'].view
  }

  if (routeId === 'overview') {
    return PERMISSIONS.dashboard.view
  }

  if (routeId === 'discount-reports' || routeId === 'discount-security') {
    return PERMISSIONS.discounts.view
  }

  if (routeId === 'warehouse-vouchers') {
    return PERMISSIONS.warehouses.view
  }

  return PERMISSIONS[routeId]?.view ?? null
}

export function hasPermission(session, permission) {
  if (!session?.token) {
    return false
  }

  if (session.isSuperAdmin) {
    return true
  }

  if (!permission) {
    return false
  }

  return (session.permissions ?? []).includes(permission)
}

export function hasAnyPermission(session, permissions = []) {
  return permissions.some((permission) => hasPermission(session, permission))
}

export function canViewRoute(session, routeId) {
  if (routeId === 'warehouse-vouchers') {
    return hasAnyPermission(session, [PERMISSIONS.warehouses.view, PERMISSIONS.inventory.view])
  }
  return hasPermission(session, viewPermissionForRoute(routeId))
}

export function getAccessibleRoutes(session, routes) {
  return routes.filter((route) => canViewRoute(session, route.id))
}

export function getDefaultRoutePath(session, routes) {
  const accessible = getAccessibleRoutes(session, routes)
  return accessible[0]?.path ?? null
}

export function canManageSection(session, routeId) {
  const sectionPermissions = PERMISSIONS[routeId]
  if (!sectionPermissions || session?.isSuperAdmin) {
    return Boolean(session?.isSuperAdmin)
  }

  return Object.entries(sectionPermissions)
    .filter(([action]) => action !== 'view')
    .some(([, permission]) => hasPermission(session, permission))
}

export function canUsePosActions(session) {
  if (session?.isSuperAdmin) {
    return true
  }

  return hasAnyPermission(session, [
    PERMISSIONS.pos.create,
    PERMISSIONS.pos.update,
    PERMISSIONS.pos.pay,
    PERMISSIONS.orders.update,
  ])
}

export function canSubmitPosOrder(session, orderId) {
  if (session?.isSuperAdmin) {
    return true
  }

  if (orderId) {
    return hasAnyPermission(session, [
      PERMISSIONS.pos.update,
      PERMISSIONS.pos.create,
      PERMISSIONS.pos.pay,
      PERMISSIONS.orders.update,
    ])
  }

  return hasAnyPermission(session, [
    PERMISSIONS.pos.create,
    PERMISSIONS.pos.pay,
    PERMISSIONS.orders.update,
  ])
}
