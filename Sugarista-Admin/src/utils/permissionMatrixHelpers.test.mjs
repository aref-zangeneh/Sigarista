import test from 'node:test'
import assert from 'node:assert/strict'
import {
  applyCategoryPreset,
  applySectionPreset,
  groupCatalogByCategory,
  toggleSectionPermission,
} from './permissionMatrixHelpers.js'

const SAMPLE_CATALOG = [
  {
    sectionId: 'orders',
    sectionLabel: 'سفارش‌ها',
    categoryId: 'main',
    categoryLabel: 'اصلی',
    permissions: [
      { key: 'admin.orders.view', action: 'view', actionLabel: 'مشاهده' },
      { key: 'admin.orders.update', action: 'update', actionLabel: 'ویرایش' },
      { key: 'admin.orders.delete', action: 'delete', actionLabel: 'لغو' },
    ],
  },
  {
    sectionId: 'customers',
    sectionLabel: 'باشگاه مشتریان',
    categoryId: 'main',
    categoryLabel: 'اصلی',
    permissions: [
      { key: 'admin.customers.view', action: 'view', actionLabel: 'مشاهده' },
      { key: 'admin.customers.create', action: 'create', actionLabel: 'ایجاد' },
    ],
  },
]

test('applySectionPreset("viewer") grants ONLY view permission for Orders without touching Customers', () => {
  const keys = applySectionPreset([], SAMPLE_CATALOG[0], 'viewer')
  assert.deepEqual(keys, ['admin.orders.view'])
  const grouped = groupCatalogByCategory(SAMPLE_CATALOG, keys)
  assert.equal(grouped[0].sections[0].isViewerOnly, true)
  assert.equal(grouped[0].sections[1].selectedCount, 0)
})

test('toggleSectionPermission auto-enables view when enabling update, and clears writes when unchecking view', () => {
  const afterUpdate = toggleSectionPermission([], SAMPLE_CATALOG[0], 'admin.orders.update', true)
  assert.ok(afterUpdate.includes('admin.orders.view'))
  assert.ok(afterUpdate.includes('admin.orders.update'))

  const afterUncheckView = toggleSectionPermission(afterUpdate, SAMPLE_CATALOG[0], 'admin.orders.view', false)
  assert.deepEqual(afterUncheckView, [])
})

test('applyCategoryPreset applies viewer, full, and none across all sections in a category', () => {
  const viewerAll = applyCategoryPreset([], SAMPLE_CATALOG, 'viewer')
  assert.deepEqual(viewerAll.sort(), ['admin.customers.view', 'admin.orders.view'])

  const fullAll = applyCategoryPreset([], SAMPLE_CATALOG, 'full')
  assert.equal(fullAll.length, 5)

  const cleared = applyCategoryPreset(fullAll, SAMPLE_CATALOG, 'none')
  assert.deepEqual(cleared, [])
})

test('groupCatalogByCategory filters accurately by Persian search query', () => {
  const filtered = groupCatalogByCategory(SAMPLE_CATALOG, [], 'سفارش')
  assert.equal(filtered.length, 1)
  assert.equal(filtered[0].sections.length, 1)
  assert.equal(filtered[0].sections[0].sectionId, 'orders')
})
