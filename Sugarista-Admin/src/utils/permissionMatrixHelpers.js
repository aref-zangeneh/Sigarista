const VIEW_ACTIONS = new Set(['view', 'view-dashboard', 'view-history'])

export function isViewAction(action) {
  return VIEW_ACTIONS.has(String(action || '').toLowerCase())
}

/**
 * Groups flat catalog sections into ordered categories (matching the admin sidebar).
 */
export function groupCatalogByCategory(catalog = [], selectedKeys = [], searchQuery = '') {
  const selectedSet = new Set(selectedKeys ?? [])
  const q = String(searchQuery || '').trim().toLowerCase()

  const categoryMap = new Map()
  for (const section of catalog ?? []) {
    const perms = (section.permissions ?? []).map((p) => ({
      ...p,
      checked: selectedSet.has(p.key),
      isView: isViewAction(p.action),
    }))

    const matchesSearch =
      !q ||
      String(section.sectionLabel || '').toLowerCase().includes(q) ||
      String(section.categoryLabel || '').toLowerCase().includes(q) ||
      perms.some((p) => String(p.actionLabel || '').toLowerCase().includes(q))

    if (!matchesSearch) continue

    const catId = section.categoryId || 'other'
    const catLabel = section.categoryLabel || 'سایر'

    if (!categoryMap.has(catId)) {
      categoryMap.set(catId, {
        categoryId: catId,
        categoryLabel: catLabel,
        sections: [],
        totalCount: 0,
        selectedCount: 0,
      })
    }

    const cat = categoryMap.get(catId)
    const sectionSelected = perms.filter((p) => p.checked).length
    const viewPerms = perms.filter((p) => p.isView)
    const writePerms = perms.filter((p) => !p.isView)

    const isViewerOnly =
      viewPerms.length > 0 &&
      viewPerms.every((p) => p.checked) &&
      writePerms.every((p) => !p.checked)

    const isFull = perms.length > 0 && sectionSelected === perms.length

    cat.sections.push({
      ...section,
      permissions: perms,
      selectedCount: sectionSelected,
      totalCount: perms.length,
      isViewerOnly,
      isFull,
    })
    cat.totalCount += perms.length
    cat.selectedCount += sectionSelected
  }

  return Array.from(categoryMap.values())
}

/**
 * Smart permission toggle with CRUD integrity:
 * - Checking any write/special action (create/update/delete/...) automatically enables the section's `view` permission.
 * - Unchecking the section's `view` permission automatically clears write actions in that section so there are no broken half-states.
 */
export function toggleSectionPermission(currentKeys, section, permissionKey, checked) {
  const next = new Set(currentKeys ?? [])
  const perms = section?.permissions ?? []
  const target = perms.find((p) => p.key === permissionKey)
  const viewPerm = perms.find((p) => p.action === 'view')

  if (checked) {
    next.add(permissionKey)
    if (target && target.action !== 'view' && viewPerm) {
      next.add(viewPerm.key)
    }
  } else {
    next.delete(permissionKey)
    if (target && target.action === 'view') {
      for (const p of perms) {
        next.delete(p.key)
      }
    }
  }

  return Array.from(next)
}

/**
 * Applies a preset ('viewer' | 'full' | 'none') to a single section.
 */
export function applySectionPreset(currentKeys, section, mode) {
  const next = new Set(currentKeys ?? [])
  const perms = section?.permissions ?? []

  for (const p of perms) {
    next.delete(p.key)
  }

  if (mode === 'viewer') {
    for (const p of perms) {
      if (isViewAction(p.action)) {
        next.add(p.key)
      }
    }
  } else if (mode === 'full') {
    for (const p of perms) {
      next.add(p.key)
    }
  }

  return Array.from(next)
}

/**
 * Applies a preset ('viewer' | 'full' | 'none') to a whole category (or entire catalog).
 */
export function applyCategoryPreset(currentKeys, sections, mode) {
  let result = [...(currentKeys ?? [])]
  for (const section of sections ?? []) {
    result = applySectionPreset(result, section, mode)
  }
  return result
}
