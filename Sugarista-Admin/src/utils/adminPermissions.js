export const PERMISSIONS = {
  dashboard: {
    view: 'admin.dashboard.view',
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
  addons: {
    view: 'admin.addons.view',
    create: 'admin.addons.create',
    update: 'admin.addons.update',
    delete: 'admin.addons.delete',
  },
  modifiers: {
    view: 'admin.modifiers.view',
    create: 'admin.modifiers.create',
    update: 'admin.modifiers.update',
    delete: 'admin.modifiers.delete',
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
  inventory: {
    view: 'admin.inventory.view',
    create: 'admin.inventory.create',
    update: 'admin.inventory.update',
  },
  'inventory-purchases': {
    view: 'admin.inventory-purchases.view',
    create: 'admin.inventory-purchases.create',
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
  'admin-users': {
    view: 'admin.users.view',
    create: 'admin.users.create',
    update: 'admin.users.update',
    delete: 'admin.users.delete',
    managePermissions: 'admin.users.manage-permissions',
  },
}

export function viewPermissionForRoute(routeId) {
  if (routeId === 'admin-users') {
    return PERMISSIONS['admin-users'].view
  }

  if (routeId === 'overview') {
    return PERMISSIONS.dashboard.view
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
