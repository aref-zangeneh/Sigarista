import { useEffect, useMemo, useState } from 'react'
import { DataTable, formatMoney } from './components/DataTable'
import { PaginatedAdminTable } from './components/PaginatedAdminTable'
import { DashboardOverview } from './components/DashboardOverview'
import { InventoryPurchasesPanel } from './components/InventoryPurchasesPanel'
import {
  buildAddonColumns,
  buildCategoryColumns,
  buildIngredientColumns,
  buildInventoryColumns,
  buildMenuItemColumns,
  buildModifierColumns,
  buildRecipeColumns,
  buildSalePreviewColumns,
  buildSaleResultColumns,
  buildStockHistoryColumns,
  buildOrderColumns,
} from './components/adminTableColumns'
import './App.css'

const API_BASE_URL = import.meta.env.VITE_API_BASE_URL ?? '/api'
const SESSION_STORAGE_KEY = 'sugarista.admin.session'

const tabOptions = [
  { id: 'overview', label: 'نمای کلی' },
  { id: 'salon', label: 'وضعیت سالن' },
  { id: 'categories', label: 'دسته‌بندی‌ها' },
  { id: 'menu-items', label: 'آیتم‌های منو' },
  { id: 'addons', label: 'افزودنی‌ها (اضافات)' },
  { id: 'modifiers', label: 'تغییرات سفارش' },
  { id: 'ingredients', label: 'مواد اولیه' },
  { id: 'recipes', label: 'دستور پخت' },
  { id: 'inventory', label: 'انبار موجودی' },
  { id: 'inventory-purchases', label: 'خرید مواد اولیه' },
  { id: 'record-sale', label: 'ثبت فروش' },
  { id: 'orders', label: 'سفارش‌ها' },
  { id: 'site-settings', label: 'تنظیمات سایت' },
]

const UNIT_OPTIONS = ['ml', 'l', 'gr', 'kg', 'oz', 'tsp', 'tbsp', 'cup', 'pieces']

const LOCATION_OPTIONS = [
  { value: 'main', label: 'انبار اصلی' },
  { value: 'bar', label: 'بار' },
  { value: 'kitchen', label: 'آشپزخانه' },
  { value: 'dry', label: 'انبار خشک' },
  { value: 'refrigerator', label: 'یخچال' },
]

const emptyRestockForm = { quantity: '', note: '' }
const emptyAdjustForm = { delta: '', note: '' }
const emptyAlertForm = { minAlertQuantity: '' }
const emptySaleForm = { menuItemId: '', quantitySold: 1 }

const emptyCategoryForm = {
  id: '',
  slug: '',
  name: '',
  displayOrder: 0,
  isActive: true,
}

const emptyMenuItemForm = {
  id: '',
  categoryId: '',
  persianName: '',
  englishName: '',
  description: '',
  imageUrl: '',
  galleryImageUrls: '',
  specialOfferPrice: '',
  taxPercent: 0,
  isOrderable: true,
  preparingDurationMinutes: '',
  calories: '',
  allergens: '',
  volume: '',
  smallPrice: '',
  smallSpecialOfferPrice: '',
  smallVolume: '',
  mediumPrice: '',
  mediumSpecialOfferPrice: '',
  mediumVolume: '',
  largePrice: '',
  largeSpecialOfferPrice: '',
  largeVolume: '',
  tags: '',
  badge: '',
  featured: false,
  accent: 'linear-gradient(135deg, #5b3425, #8a5339)',
  shortLabel: '',
  displayOrder: 0,
  isActive: true,
}

const emptyAddonForm = {
  id: '',
  persianName: '',
  englishName: '',
  description: '',
  price: 0,
  tags: '',
  displayOrder: 0,
  isActive: true,
}

const emptyModifierForm = {
  id: '',
  persianName: '',
  englishName: '',
  displayOrder: 0,
  isActive: true,
}

const emptyIngredientForm = {
  id: '',
  persianName: '',
  englishName: '',
  unit: 'ml',
  description: '',
  displayOrder: 0,
  isActive: true,
  inventoryLocation: 'main',
  unitCost: '',
}

const emptyRecipeLineForm = {
  ingredientId: '',
  quantity: '',
}

const emptyRecipeState = {
  selectedMenuItemId: '',
  lines: [],
  lineForm: emptyRecipeLineForm,
}

const emptySalonTableForm = {
  id: '',
  name: '',
  capacity: 2,
  displayOrder: 0,
  isActive: true,
}

const emptyOccupyForm = {
  tableId: '',
  peopleCount: 1,
  note: '',
}

const emptySalonStatus = {
  timestamp: '',
  totalTables: 0,
  occupiedTables: 0,
  freeTables: 0,
  currentCustomers: 0,
  tables: [],
}

const emptySettings = {
  id: '',
  cafeName: '',
  browserTitle: '',
  metaDescription: '',
  eyebrow: '',
  brandTitle: '',
  brandSubtitle: '',
  heroBadgeText: '',
  heroTitle: '',
  heroDescription: '',
  heroPrimaryButtonLabel: '',
  heroSecondaryButtonLabel: '',
  heroHours: '',
  howToOrderLabel: '',
  howToOrderTitle: '',
  howToOrderStatus: '',
  stepOneTitle: '',
  stepOneDescription: '',
  stepTwoTitle: '',
  stepTwoDescription: '',
  stepThreeTitle: '',
  stepThreeDescription: '',
  quickAccessLabel: '',
  quickAccessTitle: '',
  quickAccessDescription: '',
  popularLabel: '',
  popularTitle: '',
  popularDescription: '',
  menuLabel: '',
  menuTitle: '',
  menuToolbarHint: '',
  searchPlaceholder: '',
  visitLabel: '',
  visitTitle: '',
  visitDescription: '',
  contactPhone: '',
  address: '',
  breakfastHours: '',
  takeawayStatus: '',
  instagramUrl: '',
  footerText: '',
  footerPopularLabel: '',
  footerMenuLabel: '',
  footerCallLabel: '',
}

const settingsSections = [
  {
    title: 'برند و هویت',
    fields: [
      ['cafeName', 'نام کافه'],
      ['browserTitle', 'عنوان مرورگر'],
      ['metaDescription', 'متن توضیحات متا', 'textarea'],
      ['eyebrow', 'متن بالای عنوان'],
      ['brandTitle', 'عنوان اصلی برند'],
      ['brandSubtitle', 'زیرعنوان برند'],
    ],
  },
  {
    title: 'بخش هیرو',
    fields: [
      ['heroBadgeText', 'متن نشان هیرو'],
      ['heroTitle', 'عنوان هیرو'],
      ['heroDescription', 'توضیح هیرو', 'textarea'],
      ['heroPrimaryButtonLabel', 'متن دکمه اصلی'],
      ['heroSecondaryButtonLabel', 'متن دکمه دوم'],
      ['heroHours', 'ساعت کاری'],
    ],
  },
  {
    title: 'مراحل سفارش',
    fields: [
      ['howToOrderLabel', 'برچسب مراحل سفارش'],
      ['howToOrderTitle', 'عنوان مراحل سفارش'],
      ['howToOrderStatus', 'متن وضعیت'],
      ['stepOneTitle', 'عنوان مرحله ۱'],
      ['stepOneDescription', 'توضیح مرحله ۱', 'textarea'],
      ['stepTwoTitle', 'عنوان مرحله ۲'],
      ['stepTwoDescription', 'توضیح مرحله ۲', 'textarea'],
      ['stepThreeTitle', 'عنوان مرحله ۳'],
      ['stepThreeDescription', 'توضیح مرحله ۳', 'textarea'],
    ],
  },
  {
    title: 'بخش‌های محتوایی',
    fields: [
      ['quickAccessLabel', 'برچسب دسترسی سریع'],
      ['quickAccessTitle', 'عنوان دسترسی سریع'],
      ['quickAccessDescription', 'توضیح دسترسی سریع', 'textarea'],
      ['popularLabel', 'برچسب پرفروش‌ها'],
      ['popularTitle', 'عنوان پرفروش‌ها'],
      ['popularDescription', 'توضیح پرفروش‌ها', 'textarea'],
      ['menuLabel', 'برچسب منو'],
      ['menuTitle', 'عنوان منو'],
      ['menuToolbarHint', 'راهنمای نوار منو'],
      ['searchPlaceholder', 'متن جستجو'],
      ['visitLabel', 'برچسب اطلاعات کافه'],
      ['visitTitle', 'عنوان اطلاعات کافه'],
      ['visitDescription', 'توضیح اطلاعات کافه', 'textarea'],
    ],
  },
  {
    title: 'تماس و فوتر',
    fields: [
      ['contactPhone', 'شماره تماس', 'tel'],
      ['address', 'آدرس', 'textarea'],
      ['breakfastHours', 'ساعت صبحانه'],
      ['takeawayStatus', 'وضعیت بیرون‌بر'],
      ['instagramUrl', 'لینک اینستاگرام', 'url'],
      ['footerText', 'متن فوتر'],
      ['footerPopularLabel', 'متن لینک پرفروش‌ها'],
      ['footerMenuLabel', 'متن لینک منو'],
      ['footerCallLabel', 'متن لینک تماس'],
    ],
  },
]

function loadStoredSession() {
  try {
    const rawValue = window.localStorage.getItem(SESSION_STORAGE_KEY)
    return rawValue ? normalizeSession(JSON.parse(rawValue)) : null
  } catch {
    return null
  }
}

function normalizeSession(raw) {
  if (!raw) {
    return null
  }

  const token = raw.token ?? raw.Token
  if (!token) {
    return null
  }

  return {
    token,
    userId: raw.userId ?? raw.UserId,
    username: raw.username ?? raw.Username,
    displayName: raw.displayName ?? raw.DisplayName,
    role: raw.role ?? raw.Role,
  }
}

function splitTags(tagValue) {
  return tagValue
    .split(',')
    .map((tag) => tag.trim())
    .filter(Boolean)
}

function joinTags(tags) {
  return tags.join('، ')
}

function splitList(value) {
  return value
    .split(/[\n,،]+/)
    .map((item) => item.trim())
    .filter(Boolean)
}

function joinList(values) {
  return values.join('\n')
}

function optionalNumber(value) {
  if (value === '' || value === null || value === undefined) {
    return null
  }

  return Number(value)
}

function toPersianNumber(value) {
  return new Intl.NumberFormat('fa-IR').format(value)
}

function formatDuration(minutes) {
  if (!minutes) {
    return 'کمتر از ۱ دقیقه'
  }

  const hours = Math.floor(minutes / 60)
  const remainingMinutes = minutes % 60

  if (hours === 0) {
    return `${toPersianNumber(remainingMinutes)} دقیقه`
  }

  if (remainingMinutes === 0) {
    return `${toPersianNumber(hours)} ساعت`
  }

  return `${toPersianNumber(hours)} ساعت و ${toPersianNumber(remainingMinutes)} دقیقه`
}

function formatDateTime(value) {
  if (!value) {
    return 'ثبت نشده'
  }

  return new Intl.DateTimeFormat('fa-IR', {
    dateStyle: 'short',
    timeStyle: 'short',
  }).format(new Date(value))
}

function getReadableError(error) {
  const message = error instanceof Error ? error.message : 'خطای ناشناخته'

  if (message === 'Unauthorized.' || message.includes('401')) {
    return 'نشست شما منقضی شده یا دسترسی ندارید. دوباره وارد شوید.'
  }

  if (message === 'Invalid username or password.') {
    return 'نام کاربری یا رمز عبور نادرست است.'
  }

  if (message === 'Resource not found.') {
    return 'آیتم موردنظر پیدا نشد.'
  }

  return message
}

async function apiRequest(path, { method = 'GET', body, token } = {}) {
  const response = await fetch(`${API_BASE_URL}${path}`, {
    method,
    headers: {
      'Content-Type': 'application/json',
      ...(token ? { Authorization: `Bearer ${token}` } : {}),
    },
    body: body ? JSON.stringify(body) : undefined,
  })

  if (!response.ok) {
    let message = `درخواست با خطای ${response.status} مواجه شد.`

    try {
      const payload = await response.json()
      message =
        payload.detail ||
        payload.title ||
        (payload.errors ? Object.values(payload.errors).flat().join('، ') : message)
    } catch {
      // Ignore invalid JSON payloads and keep the fallback message.
    }

    throw new Error(message)
  }

  if (response.status === 204) {
    return null
  }

  return response.json()
}

function App() {
  const [activeTab, setActiveTab] = useState('overview')
  const [session, setSession] = useState(loadStoredSession)
  const [loginForm, setLoginForm] = useState({ username: 'admin', password: 'ChangeMe123!' })
  const [categoryOptions, setCategoryOptions] = useState([])
  const [menuItemOptions, setMenuItemOptions] = useState([])
  const [ingredientOptions, setIngredientOptions] = useState([])
  const [purchaseExpiringOnly, setPurchaseExpiringOnly] = useState(false)
  const [tableReloadToken, setTableReloadToken] = useState(0)
  const [dashboard, setDashboard] = useState(null)
  const [dashboardLoading, setDashboardLoading] = useState(false)
  const [dashboardError, setDashboardError] = useState('')
  const [recipeState, setRecipeState] = useState(emptyRecipeState)
  const [ingredientForm, setIngredientForm] = useState(emptyIngredientForm)
  const [inventoryStatus, setInventoryStatus] = useState(null)
  const [inventoryAction, setInventoryAction] = useState({ ingredientId: null, mode: null }) // mode: 'restock' | 'adjust' | 'alert' | 'history'
  const [restockForm, setRestockForm] = useState(emptyRestockForm)
  const [adjustForm, setAdjustForm] = useState(emptyAdjustForm)
  const [alertForm, setAlertForm] = useState(emptyAlertForm)
  const [stockHistory, setStockHistory] = useState([])
  const [saleForm, setSaleForm] = useState(emptySaleForm)
  const [salePreview, setSalePreview] = useState([])
  const [salePreviewLoading, setSalePreviewLoading] = useState(false)
  const [saleResult, setSaleResult] = useState(null)
  const [selectedOrder, setSelectedOrder] = useState(null)
  const [orderEditForm, setOrderEditForm] = useState(null)
  const [salonStatus, setSalonStatus] = useState(emptySalonStatus)
  const [siteSettings, setSiteSettings] = useState(emptySettings)
  const [categoryForm, setCategoryForm] = useState(emptyCategoryForm)
  const [menuItemForm, setMenuItemForm] = useState(emptyMenuItemForm)
  const [addonForm, setAddonForm] = useState(emptyAddonForm)
  const [modifierForm, setModifierForm] = useState(emptyModifierForm)
  const [salonTableForm, setSalonTableForm] = useState(emptySalonTableForm)
  const [occupyForm, setOccupyForm] = useState(emptyOccupyForm)
  const [isSalonTableModalOpen, setIsSalonTableModalOpen] = useState(false)
  const [isOccupyModalOpen, setIsOccupyModalOpen] = useState(false)
  const [loading, setLoading] = useState(false)
  const [saving, setSaving] = useState(false)
  const [message, setMessage] = useState('')
  const [error, setError] = useState('')

  const activeSalonTables = salonStatus.tables.filter((table) => table.isActive)

  useEffect(() => {
    document.documentElement.lang = 'fa'
    document.documentElement.dir = 'rtl'
    document.title = 'پنل مدیریت شوگریستا'
  }, [])

  useEffect(() => {
    if (session?.token) {
      window.localStorage.setItem(SESSION_STORAGE_KEY, JSON.stringify(session))
      loadAdminData(session.token)
      return
    }

    window.localStorage.removeItem(SESSION_STORAGE_KEY)
  }, [session])

  useEffect(() => {
    if (session?.token && activeTab === 'overview') {
      loadDashboard(session.token)
    }
  }, [session, activeTab, tableReloadToken])

  useEffect(() => {
    if (!session?.token || activeTab !== 'salon') {
      return undefined
    }

    const intervalId = window.setInterval(() => {
      loadSalonStatus(session.token)
    }, 5000)

    return () => window.clearInterval(intervalId)
  }, [activeTab, session])

  useEffect(() => {
    if (!session?.token || activeTab !== 'record-sale' || !saleForm.menuItemId || saleResult) {
      setSalePreview([])
      return undefined
    }

    let cancelled = false

    async function loadSalePreview() {
      setSalePreviewLoading(true)
      try {
        const recipeLines = await apiRequest(`/admin/recipes/${saleForm.menuItemId}`, {
          token: session.token,
        })

        if (cancelled) {
          return
        }

        const soldCount = Number(saleForm.quantitySold) || 1
        setSalePreview(
          recipeLines.map((line) => ({
            ingredientPersianName: line.ingredientPersianName,
            ingredientEnglishName: line.ingredientEnglishName,
            unit: line.unit,
            perItemQuantity: line.quantity,
            totalQuantity: line.quantity * soldCount,
          })),
        )
      } catch {
        if (!cancelled) {
          setSalePreview([])
        }
      } finally {
        if (!cancelled) {
          setSalePreviewLoading(false)
        }
      }
    }

    loadSalePreview()

    return () => {
      cancelled = true
    }
  }, [activeTab, saleForm.menuItemId, saleForm.quantitySold, saleResult, session?.token])

  async function loadSalonStatus(token = session?.token) {
    if (!token) {
      return
    }

    const statusData = await apiRequest('/admin/salon/status', { token })
    setSalonStatus(statusData)
    setSalonTableForm((currentForm) =>
      currentForm.id
        ? currentForm
        : { ...emptySalonTableForm, displayOrder: statusData.tables.length },
    )
  }

  function bumpTableReload() {
    setTableReloadToken((current) => current + 1)
  }

  function navigateToTab(tabId, options) {
    setActiveTab(tabId)
    if (tabId === 'inventory-purchases' && options?.expiringOnly) {
      setPurchaseExpiringOnly(true)
    }
  }

  async function loadDashboard(token = session?.token) {
    if (!token) {
      return
    }

    setDashboardLoading(true)
    setDashboardError('')
    try {
      const data = await apiRequest('/admin/analytics/dashboard', { token })
      setDashboard(data)
    } catch (requestError) {
      setDashboard(null)
      setDashboardError(getReadableError(requestError))
    } finally {
      setDashboardLoading(false)
    }
  }

  async function openOrderEditor(orderRow) {
    if (!session?.token) {
      return
    }

    try {
      const order = await apiRequest(`/admin/orders/${orderRow.id}`, { token: session.token })
      setSelectedOrder(order)
      setOrderEditForm({
        customerName: order.customerName,
        customerPhone: order.customerPhone,
        note: order.note ?? '',
        lines: order.lines.map((line) => ({
          id: line.id,
          menuItemId: line.menuItemId,
          persianName: line.persianName,
          englishName: line.englishName,
          sizeKey: line.sizeKey,
          sizeLabel: line.sizeLabel,
          quantity: line.quantity,
          unitPrice: line.unitPrice,
          lineTotal: line.lineTotal,
          payerKey: line.payerKey,
          payerLabel: line.payerLabel,
          inventoryDeductedQuantity: line.inventoryDeductedQuantity,
          modifierLabels: line.modifierLabels ?? [],
          remove: false,
        })),
      })
    } catch (requestError) {
      setError(getReadableError(requestError))
    }
  }

  function closeOrderEditor() {
    setSelectedOrder(null)
    setOrderEditForm(null)
  }

  function updateOrderLineQuantity(lineId, quantity) {
    setOrderEditForm((current) => ({
      ...current,
      lines: current.lines.map((line) =>
        line.id === lineId
          ? {
              ...line,
              quantity: Math.max(1, Number(quantity) || 1),
              lineTotal: line.unitPrice * Math.max(1, Number(quantity) || 1),
            }
          : line,
      ),
    }))
  }

  function toggleOrderLineRemoval(lineId) {
    setOrderEditForm((current) => ({
      ...current,
      lines: current.lines.map((line) =>
        line.id === lineId ? { ...line, remove: !line.remove } : line,
      ),
    }))
  }

  async function handleSaveOrder(event) {
    event.preventDefault()
    if (!session?.token || !selectedOrder || !orderEditForm) {
      return
    }

    setSaving(true)
    setError('')
    setMessage('')

    try {
      const updated = await apiRequest(`/admin/orders/${selectedOrder.id}`, {
        method: 'PUT',
        token: session.token,
        body: {
          customerName: orderEditForm.customerName,
          customerPhone: orderEditForm.customerPhone,
          note: orderEditForm.note,
          lines: orderEditForm.lines.map((line) => ({
            id: line.id,
            menuItemId: line.menuItemId,
            sizeKey: line.sizeKey,
            quantity: line.quantity,
            modifierIds: [],
            payerKey: line.payerKey,
            payerLabel: line.payerLabel,
            remove: line.remove,
          })),
        },
      })

      setMessage('سفارش با موفقیت بروزرسانی شد.')
      closeOrderEditor()
      bumpTableReload()
      await loadAdminData()
    } catch (requestError) {
      setError(getReadableError(requestError))
    } finally {
      setSaving(false)
    }
  }

  async function handleCancelOrder() {
    if (!session?.token || !selectedOrder) {
      return
    }

    if (!window.confirm('آیا از لغو این سفارش و بازگردانی موجودی اطمینان دارید؟')) {
      return
    }

    setSaving(true)
    setError('')
    setMessage('')

    try {
      const updated = await apiRequest(`/admin/orders/${selectedOrder.id}/cancel`, {
        method: 'POST',
        token: session.token,
      })

      setMessage('سفارش لغو شد و موجودی بازگردانده شد.')
      closeOrderEditor()
      bumpTableReload()
      await loadAdminData()
    } catch (requestError) {
      setError(getReadableError(requestError))
    } finally {
      setSaving(false)
    }
  }

  async function loadAdminData(token = session?.token) {
    if (!token) {
      return
    }

    setLoading(true)
    setError('')

    const requests = [
      { key: 'categoryOptions', promise: apiRequest('/admin/categories/options', { token }) },
      { key: 'menuItemOptions', promise: apiRequest('/admin/menu-items/options', { token }) },
      { key: 'ingredientOptions', promise: apiRequest('/admin/ingredients/options', { token }) },
      { key: 'inventoryStatus', promise: apiRequest('/admin/inventory', { token }) },
      { key: 'siteSettings', promise: apiRequest('/admin/site-settings', { token }) },
      { key: 'salonStatus', promise: apiRequest('/admin/salon/status', { token }) },
    ]

    try {
      const results = await Promise.allSettled(requests.map((request) => request.promise))
      const data = {}
      const failures = []

      results.forEach((result, index) => {
        const { key } = requests[index]
        if (result.status === 'fulfilled') {
          data[key] = result.value
          return
        }

        failures.push(key)
        if (result.reason?.message?.includes('401')) {
          setSession(null)
        }
      })

      if (data.categoryOptions) {
        setCategoryOptions(data.categoryOptions)
        setCategoryForm((currentForm) =>
          currentForm.id ? currentForm : { ...emptyCategoryForm, displayOrder: data.categoryOptions.length },
        )
      }

      if (data.menuItemOptions) {
        setMenuItemOptions(data.menuItemOptions)
        setMenuItemForm((currentForm) =>
          currentForm.id
            ? currentForm
            : {
                ...emptyMenuItemForm,
                categoryId: data.categoryOptions?.[0]?.id ?? '',
                displayOrder: data.menuItemOptions.length,
              },
        )
      }

      if (data.ingredientOptions) {
        setIngredientOptions(data.ingredientOptions)
      }

      if (data.inventoryStatus) {
        setInventoryStatus(data.inventoryStatus)
      }

      if (data.siteSettings) {
        setSiteSettings(data.siteSettings)
      }

      if (data.salonStatus) {
        setSalonStatus(data.salonStatus)
        setSalonTableForm((currentForm) =>
          currentForm.id
            ? currentForm
            : { ...emptySalonTableForm, displayOrder: data.salonStatus.tables.length },
        )
      }

      if (failures.length > 0) {
        setError(`بارگذاری برخی بخش‌ها ناموفق بود: ${failures.join('، ')}`)
      }
    } catch (requestError) {
      if (requestError.message.includes('401')) {
        setSession(null)
      }

      setError(getReadableError(requestError))
    } finally {
      setLoading(false)
    }
  }

  async function handleLogin(event) {
    event.preventDefault()
    setSaving(true)
    setError('')
    setMessage('')

    try {
      const nextSession = normalizeSession(
        await apiRequest('/auth/login', {
          method: 'POST',
          body: loginForm,
        }),
      )

      if (!nextSession) {
        throw new Error('پاسخ ورود نامعتبر بود.')
      }

      setSession(nextSession)
      setMessage('ورود با موفقیت انجام شد.')
    } catch (requestError) {
      setError(getReadableError(requestError))
    } finally {
      setSaving(false)
    }
  }

  function handleLogout() {
    setSession(null)
    setCategoryOptions([])
    setMenuItemOptions([])
    setRecipeState(emptyRecipeState)
    setIngredientForm(emptyIngredientForm)
    setInventoryStatus(null)
    setInventoryAction({ ingredientId: null, mode: null })
    setStockHistory([])
    setSaleForm(emptySaleForm)
    setSaleResult(null)
    setSalonStatus(emptySalonStatus)
    setSiteSettings(emptySettings)
    setCategoryForm(emptyCategoryForm)
    setMenuItemForm(emptyMenuItemForm)
    setAddonForm(emptyAddonForm)
    setModifierForm(emptyModifierForm)
    setSalonTableForm(emptySalonTableForm)
    setOccupyForm(emptyOccupyForm)
    setIsSalonTableModalOpen(false)
    setIsOccupyModalOpen(false)
    setMessage('از حساب کاربری خارج شدید.')
  }

  async function handleSalonTableSubmit(event) {
    event.preventDefault()
    setSaving(true)
    setError('')
    setMessage('')

    try {
      const path = salonTableForm.id
        ? `/admin/salon/tables/${salonTableForm.id}`
        : '/admin/salon/tables'

      await apiRequest(path, {
        method: salonTableForm.id ? 'PUT' : 'POST',
        token: session.token,
        body: {
          name: salonTableForm.name,
          capacity: Number(salonTableForm.capacity),
          displayOrder: Number(salonTableForm.displayOrder),
          isActive: salonTableForm.isActive,
        },
      })

      setSalonTableForm({ ...emptySalonTableForm, displayOrder: salonStatus.tables.length })
      setIsSalonTableModalOpen(false)
      setMessage('میز سالن با موفقیت ذخیره شد.')
      await loadSalonStatus()
    } catch (requestError) {
      setError(getReadableError(requestError))
    } finally {
      setSaving(false)
    }
  }

  async function handleDeleteSalonTable(tableId) {
    if (!window.confirm('از حذف این میز مطمئن هستید؟ میزهای دارای سابقه نشست بهتر است غیرفعال شوند.')) {
      return
    }

    setSaving(true)
    setError('')
    setMessage('')

    try {
      await apiRequest(`/admin/salon/tables/${tableId}`, {
        method: 'DELETE',
        token: session.token,
      })

      if (salonTableForm.id === tableId) {
        setSalonTableForm(emptySalonTableForm)
      }

      setIsSalonTableModalOpen(false)
      setMessage('میز حذف شد.')
      await loadSalonStatus()
    } catch (requestError) {
      setError(getReadableError(requestError))
    } finally {
      setSaving(false)
    }
  }

  async function handleOccupyTable(event) {
    event.preventDefault()
    setSaving(true)
    setError('')
    setMessage('')

    try {
      await apiRequest(`/admin/salon/tables/${occupyForm.tableId}/occupy`, {
        method: 'POST',
        token: session.token,
        body: {
          peopleCount: Number(occupyForm.peopleCount),
          note: occupyForm.note.trim() || null,
        },
      })

      setOccupyForm(emptyOccupyForm)
      setIsOccupyModalOpen(false)
      setMessage('میز اشغال شد و زمان حضور مشتریان ثبت شد.')
      await loadSalonStatus()
    } catch (requestError) {
      setError(getReadableError(requestError))
    } finally {
      setSaving(false)
    }
  }

  async function handleReleaseTable(tableId) {
    if (!window.confirm('این میز آزاد شود؟')) {
      return
    }

    setSaving(true)
    setError('')
    setMessage('')

    try {
      await apiRequest(`/admin/salon/tables/${tableId}/release`, {
        method: 'POST',
        token: session.token,
      })

      setMessage('میز آزاد شد.')
      await loadSalonStatus()
    } catch (requestError) {
      setError(getReadableError(requestError))
    } finally {
      setSaving(false)
    }
  }

  async function handleCategorySubmit(event) {
    event.preventDefault()
    setSaving(true)
    setError('')
    setMessage('')

    try {
      const path = categoryForm.id
        ? `/admin/categories/${categoryForm.id}`
        : '/admin/categories'

      await apiRequest(path, {
        method: categoryForm.id ? 'PUT' : 'POST',
        token: session.token,
        body: {
          slug: categoryForm.slug,
          name: categoryForm.name,
          displayOrder: Number(categoryForm.displayOrder),
          isActive: categoryForm.isActive,
        },
      })

      setCategoryForm({ ...emptyCategoryForm, displayOrder: categoryOptions.length })
      setMessage('دسته‌بندی با موفقیت ذخیره شد.')
      bumpTableReload()
      await loadAdminData()
      setActiveTab('categories')
    } catch (requestError) {
      setError(getReadableError(requestError))
    } finally {
      setSaving(false)
    }
  }

  async function handleDeleteCategory(categoryId) {
    if (!window.confirm('از حذف این دسته‌بندی مطمئن هستید؟')) {
      return
    }

    setSaving(true)
    setError('')
    setMessage('')

    try {
      await apiRequest(`/admin/categories/${categoryId}`, {
        method: 'DELETE',
        token: session.token,
      })

      if (categoryForm.id === categoryId) {
        setCategoryForm(emptyCategoryForm)
      }

      setMessage('دسته‌بندی حذف شد.')
      bumpTableReload()
      await loadAdminData()
    } catch (requestError) {
      setError(getReadableError(requestError))
    } finally {
      setSaving(false)
    }
  }

  async function handleMenuItemSubmit(event) {
    event.preventDefault()
    setSaving(true)
    setError('')
    setMessage('')

    try {
      const path = menuItemForm.id
        ? `/admin/menu-items/${menuItemForm.id}`
        : '/admin/menu-items'

      await apiRequest(path, {
        method: menuItemForm.id ? 'PUT' : 'POST',
        token: session.token,
        body: {
          categoryId: menuItemForm.categoryId,
          persianName: menuItemForm.persianName,
          englishName: menuItemForm.englishName,
          description: menuItemForm.description,
          imageUrl: menuItemForm.imageUrl.trim() || null,
          galleryImageUrls: splitList(menuItemForm.galleryImageUrls),
          specialOfferPrice: optionalNumber(menuItemForm.specialOfferPrice),
          taxPercent: Number(menuItemForm.taxPercent),
          isOrderable: menuItemForm.isOrderable,
          preparingDurationMinutes: optionalNumber(menuItemForm.preparingDurationMinutes),
          calories: optionalNumber(menuItemForm.calories),
          allergens: splitList(menuItemForm.allergens),
          volume: menuItemForm.volume.trim() || null,
          smallPrice: optionalNumber(menuItemForm.smallPrice),
          smallSpecialOfferPrice: optionalNumber(menuItemForm.smallSpecialOfferPrice),
          smallVolume: menuItemForm.smallVolume.trim() || null,
          mediumPrice: optionalNumber(menuItemForm.mediumPrice),
          mediumSpecialOfferPrice: optionalNumber(menuItemForm.mediumSpecialOfferPrice),
          mediumVolume: menuItemForm.mediumVolume.trim() || null,
          largePrice: optionalNumber(menuItemForm.largePrice),
          largeSpecialOfferPrice: optionalNumber(menuItemForm.largeSpecialOfferPrice),
          largeVolume: menuItemForm.largeVolume.trim() || null,
          tags: splitTags(menuItemForm.tags),
          badge: menuItemForm.badge.trim() || null,
          featured: menuItemForm.featured,
          accent: menuItemForm.accent,
          shortLabel: menuItemForm.shortLabel,
          displayOrder: Number(menuItemForm.displayOrder),
          isActive: menuItemForm.isActive,
        },
      })

      setMenuItemForm({
        ...emptyMenuItemForm,
        categoryId: categoryOptions[0]?.id ?? '',
        displayOrder: menuItemOptions.length,
      })
      setMessage('آیتم منو با موفقیت ذخیره شد.')
      bumpTableReload()
      await loadAdminData()
      setActiveTab('menu-items')
    } catch (requestError) {
      setError(getReadableError(requestError))
    } finally {
      setSaving(false)
    }
  }

  async function handleDeleteMenuItem(itemId) {
    if (!window.confirm('از حذف این آیتم منو مطمئن هستید؟')) {
      return
    }

    setSaving(true)
    setError('')
    setMessage('')

    try {
      await apiRequest(`/admin/menu-items/${itemId}`, {
        method: 'DELETE',
        token: session.token,
      })

      if (menuItemForm.id === itemId) {
        setMenuItemForm({
          ...emptyMenuItemForm,
          categoryId: categoryOptions[0]?.id ?? '',
          displayOrder: Math.max(menuItemOptions.length - 1, 0),
        })
      }

      setMessage('آیتم منو حذف شد.')
      bumpTableReload()
      await loadAdminData()
    } catch (requestError) {
      setError(getReadableError(requestError))
    } finally {
      setSaving(false)
    }
  }

  async function handleSettingsSubmit(event) {
    event.preventDefault()
    setSaving(true)
    setError('')
    setMessage('')

    try {
      await apiRequest('/admin/site-settings', {
        method: 'PUT',
        token: session.token,
        body: { ...siteSettings },
      })

      setMessage('تنظیمات سایت با موفقیت ذخیره شد.')
      await loadAdminData()
    } catch (requestError) {
      setError(getReadableError(requestError))
    } finally {
      setSaving(false)
    }
  }

  function editCategory(category) {
    setCategoryForm({
      id: category.id,
      slug: category.slug,
      name: category.name,
      displayOrder: category.displayOrder,
      isActive: category.isActive,
    })
    setActiveTab('categories')
  }

  function editMenuItem(item) {
    setMenuItemForm({
      id: item.id,
      categoryId: item.categoryId,
      persianName: item.persianName ?? item.name,
      englishName: item.englishName ?? '',
      description: item.description,
      imageUrl: item.imageUrl ?? '',
      galleryImageUrls: joinList(item.galleryImageUrls ?? []),
      specialOfferPrice: item.specialOfferPrice ?? '',
      taxPercent: item.taxPercent ?? 0,
      isOrderable: item.isOrderable ?? true,
      preparingDurationMinutes: item.preparingDurationMinutes ?? '',
      calories: item.calories ?? '',
      allergens: joinList(item.allergens ?? []),
      volume: item.volume ?? '',
      smallPrice: item.smallPrice ?? '',
      smallSpecialOfferPrice: item.smallSpecialOfferPrice ?? '',
      smallVolume: item.smallVolume ?? '',
      mediumPrice: item.mediumPrice ?? item.price ?? '',
      mediumSpecialOfferPrice: item.mediumSpecialOfferPrice ?? '',
      mediumVolume: item.mediumVolume ?? '',
      largePrice: item.largePrice ?? '',
      largeSpecialOfferPrice: item.largeSpecialOfferPrice ?? '',
      largeVolume: item.largeVolume ?? '',
      tags: joinTags(item.tags),
      badge: item.badge ?? '',
      featured: item.featured,
      accent: item.accent,
      shortLabel: item.shortLabel,
      displayOrder: item.displayOrder,
      isActive: item.isActive,
    })
    setActiveTab('menu-items')
  }

  function editSalonTable(table) {
    setSalonTableForm({
      id: table.id,
      name: table.name,
      capacity: table.capacity,
      displayOrder: table.displayOrder,
      isActive: table.isActive,
    })
    setIsSalonTableModalOpen(true)
  }

  function openCreateSalonTableModal() {
    setSalonTableForm({
      ...emptySalonTableForm,
      displayOrder: salonStatus.tables.length,
    })
    setIsSalonTableModalOpen(true)
  }

  function closeSalonTableModal() {
    setIsSalonTableModalOpen(false)
    resetSalonTableForm()
  }

  function openOccupyTableModal(table) {
    setOccupyForm({
      tableId: table.id,
      peopleCount: Math.min(table.capacity, 1),
      note: '',
    })
    setIsOccupyModalOpen(true)
  }

  function closeOccupyModal() {
    setIsOccupyModalOpen(false)
    setOccupyForm(emptyOccupyForm)
  }

  function resetCategoryForm() {
    setCategoryForm({
      ...emptyCategoryForm,
      displayOrder: categoryOptions.length,
    })
  }

  function resetMenuItemForm() {
    setMenuItemForm({
      ...emptyMenuItemForm,
      categoryId: categoryOptions[0]?.id ?? '',
      displayOrder: menuItemOptions.length,
    })
  }

  function resetSalonTableForm() {
    setSalonTableForm({
      ...emptySalonTableForm,
      displayOrder: salonStatus.tables.length,
    })
  }

  const categoryColumns = useMemo(
    () => buildCategoryColumns({ toPersianNumber, editCategory, handleDeleteCategory }),
    [categoryOptions.length, tableReloadToken],
  )

  const menuItemColumns = useMemo(
    () => buildMenuItemColumns({ toPersianNumber, joinTags, editMenuItem, handleDeleteMenuItem }),
    [categoryOptions.length, tableReloadToken],
  )

  const addonColumns = useMemo(
    () =>
      buildAddonColumns({
        joinTags,
        setAddonForm,
        emptyAddonForm,
        bumpTableReload,
        addonForm,
        session,
        apiRequest,
        setSaving,
        setError,
        setMessage,
        getReadableError,
      }),
    [addonForm, session, tableReloadToken],
  )

  const modifierColumns = useMemo(
    () =>
      buildModifierColumns({
        toPersianNumber,
        setModifierForm,
        emptyModifierForm,
        bumpTableReload,
        modifierForm,
        session,
        apiRequest,
        setSaving,
        setError,
        setMessage,
        getReadableError,
      }),
    [modifierForm, session, tableReloadToken],
  )

  const ingredientColumns = useMemo(
    () =>
      buildIngredientColumns({
        setIngredientForm,
        emptyIngredientForm,
        bumpTableReload,
        ingredientForm,
        session,
        apiRequest,
        setSaving,
        setError,
        setMessage,
        getReadableError,
        LOCATION_OPTIONS,
      }),
    [ingredientForm, session, tableReloadToken],
  )

  const recipeColumns = useMemo(
    () =>
      buildRecipeColumns({
        setRecipeState,
        session,
        apiRequest,
        setSaving,
        setError,
        setMessage,
        getReadableError,
      }),
    [recipeState.lines, session],
  )

  const inventoryColumns = useMemo(
    () =>
      buildInventoryColumns({
        setInventoryAction,
        setRestockForm,
        setAdjustForm,
        setAlertForm,
        emptyRestockForm,
        emptyAdjustForm,
        setStockHistory,
        session,
        apiRequest,
        setError,
        getReadableError,
      }),
    [session],
  )

  const stockHistoryColumns = useMemo(
    () => buildStockHistoryColumns({ formatDateTime }),
    [stockHistory],
  )

  const salePreviewColumns = useMemo(
    () => buildSalePreviewColumns({ toPersianNumber, quantitySold: saleForm.quantitySold }),
    [salePreview, saleForm.quantitySold],
  )

  const saleResultColumns = useMemo(
    () => buildSaleResultColumns({ toPersianNumber }),
    [saleResult],
  )

  const orderColumns = useMemo(
    () => buildOrderColumns({ toPersianNumber, formatDateTime, openOrderEditor }),
    [tableReloadToken],
  )

  if (!session?.token) {
    return (
      <main className="login-shell">
        <section className="login-card">
          <p className="eyebrow">پنل مدیریت شوگریستا</p>
          <h1>مدیریت کامل رابط کاربری کافه</h1>
          <p className="login-copy">
            برای مدیریت دسته‌بندی‌ها، آیتم‌های منو و محتوای عمومی منوی QR وارد شوید.
          </p>
          <form className="login-form" onSubmit={handleLogin}>
            <label>
              <span>نام کاربری</span>
              <input
                className="ltr-input"
                value={loginForm.username}
                onChange={(event) =>
                  setLoginForm((currentForm) => ({ ...currentForm, username: event.target.value }))
                }
                autoComplete="username"
              />
            </label>
            <label>
              <span>رمز عبور</span>
              <input
                className="ltr-input"
                type="password"
                value={loginForm.password}
                onChange={(event) =>
                  setLoginForm((currentForm) => ({ ...currentForm, password: event.target.value }))
                }
                autoComplete="current-password"
              />
            </label>
            <button type="submit" disabled={saving}>
              {saving ? 'در حال ورود...' : 'ورود'}
            </button>
          </form>
          {error ? <p className="feedback feedback-error">{error}</p> : null}
          <p className="login-hint">
            حساب پیش‌فرض: <code>admin</code> / <code>ChangeMe123!</code>
          </p>
        </section>
      </main>
    )
  }

  return (
    <div className="admin-shell">
      <aside className="sidebar">
        <div>
          <p className="eyebrow">پنل مدیریت شوگریستا</p>
          <h1>کنترل محتوا</h1>
          <p className="sidebar-copy">
            از اینجا می‌توانید منوی QR، آیتم‌های ویژه و تمام متن‌های عمومی سایت را مدیریت کنید.
          </p>
        </div>

        <nav className="tab-list" aria-label="ناوبری مدیریت">
          {tabOptions.map((tabOption) => (
            <button
              key={tabOption.id}
              type="button"
              className={tabOption.id === activeTab ? 'tab-button tab-button-active' : 'tab-button'}
              onClick={() => setActiveTab(tabOption.id)}
            >
              {tabOption.label}
            </button>
          ))}
        </nav>

        <div className="session-card">
          <p className="session-title">{session.displayName}</p>
          <p className="session-copy ltr-text">@{session.username}</p>
          <button type="button" className="ghost-button" onClick={handleLogout}>
            خروج
          </button>
        </div>
      </aside>

      <main className="content">
        <header className="topbar">
          <div>
            <p className="eyebrow">آدرس API متصل</p>
            <h2 className="ltr-text">{API_BASE_URL}</h2>
          </div>
          <button type="button" className="ghost-button" onClick={() => loadAdminData()}>
            تازه‌سازی اطلاعات
          </button>
        </header>

        {message ? <p className="feedback feedback-success">{message}</p> : null}
        {error ? <p className="feedback feedback-error">{error}</p> : null}
        {loading ? <p className="feedback">در حال بارگذاری جدیدترین اطلاعات...</p> : null}

        {activeTab === 'overview' ? (
          <DashboardOverview
            dashboard={dashboard}
            loading={dashboardLoading}
            error={dashboardError}
            onNavigate={navigateToTab}
            onRefresh={() => loadDashboard()}
          />
        ) : null}

        {activeTab === 'salon' ? (
          <section className="panel-stack">
            <section className="stat-grid">
              <article className="stat-card">
                <span>کل میزها</span>
                <strong>{toPersianNumber(salonStatus.totalTables)}</strong>
              </article>
              <article className="stat-card">
                <span>میزهای اشغال</span>
                <strong>{toPersianNumber(salonStatus.occupiedTables)}</strong>
              </article>
              <article className="stat-card">
                <span>میزهای آزاد</span>
                <strong>{toPersianNumber(salonStatus.freeTables)}</strong>
              </article>
              <article className="stat-card">
                <span>مشتریان حاضر</span>
                <strong>{toPersianNumber(salonStatus.currentCustomers)}</strong>
              </article>
            </section>

            <section className="panel">
              <div className="panel-head">
                <div>
                  <h3>وضعیت زنده سالن</h3>
                  <p>
                    این بخش هر ۵ ثانیه تازه‌سازی می‌شود. آخرین بروزرسانی:{' '}
                    {formatDateTime(salonStatus.timestamp)}
                  </p>
                </div>
                <button type="button" className="ghost-button" onClick={() => loadSalonStatus()}>
                  بروزرسانی دستی
                </button>
              </div>

              <div className="salon-grid">
                {activeSalonTables.map((table) => (
                  <article
                    key={table.id}
                    className={table.isOccupied ? 'salon-card salon-card-occupied' : 'salon-card'}
                  >
                    <div className="salon-card-head">
                      <div>
                        <h4>{table.name}</h4>
                        <p>ظرفیت {toPersianNumber(table.capacity)} نفر</p>
                      </div>
                      <span className={table.isOccupied ? 'status-badge busy' : 'status-badge free'}>
                        {table.isOccupied ? 'اشغال' : 'آزاد'}
                      </span>
                    </div>

                    {table.isOccupied ? (
                      <div className="salon-card-body">
                        <div>
                          <span className="field-label">تعداد افراد</span>
                          <strong>{toPersianNumber(table.peopleCount)} نفر</strong>
                        </div>
                        <div>
                          <span className="field-label">شروع حضور</span>
                          <strong>{formatDateTime(table.startedAt)}</strong>
                        </div>
                        <div>
                          <span className="field-label">مدت استفاده</span>
                          <strong>{formatDuration(table.durationMinutes)}</strong>
                        </div>
                        {table.note ? (
                          <div className="full-width">
                            <span className="field-label">یادداشت</span>
                            <strong>{table.note}</strong>
                          </div>
                        ) : null}
                      </div>
                    ) : (
                      <p className="salon-empty">این میز آماده پذیرش مشتری است.</p>
                    )}

                    <div className="salon-card-actions">
                      {table.isOccupied ? (
                        <button
                          type="button"
                          className="ghost-button"
                          onClick={() => handleReleaseTable(table.id)}
                          disabled={saving}
                        >
                          آزاد کردن میز
                        </button>
                      ) : (
                        <button
                          type="button"
                          className="ghost-button"
                          onClick={() => openOccupyTableModal(table)}
                        >
                          اشغال کردن
                        </button>
                      )}
                      <button type="button" className="link-button" onClick={() => editSalonTable(table)}>
                        ویرایش میز
                      </button>
                    </div>
                  </article>
                ))}
              </div>
            </section>

            <section className="panel">
              <div className="panel-head">
                <div>
                  <h3>مدیریت میزها</h3>
                  <p>برای افزودن میز جدید یا ویرایش میزهای موجود از پنجره popup استفاده کنید.</p>
                </div>
                <button type="button" className="ghost-button" onClick={openCreateSalonTableModal}>
                  افزودن میز جدید
                </button>
              </div>
            </section>

            {isOccupyModalOpen ? (
              <div className="modal-backdrop" role="presentation">
                <form className="panel form-panel modal-card" onSubmit={handleOccupyTable}>
                  <div className="panel-head">
                    <div>
                      <h3>ثبت اشغال میز</h3>
                      <p>تعداد افراد را وارد کنید تا زمان شروع حضور مشتریان ذخیره شود.</p>
                    </div>
                    <button type="button" className="ghost-button" onClick={closeOccupyModal}>
                      بستن
                    </button>
                  </div>
                  <label>
                    <span>میز</span>
                    <select
                      value={occupyForm.tableId}
                      onChange={(event) =>
                        setOccupyForm((currentForm) => ({
                          ...currentForm,
                          tableId: event.target.value,
                        }))
                      }
                    >
                      <option value="">یک میز آزاد انتخاب کنید</option>
                      {activeSalonTables
                        .filter((table) => !table.isOccupied)
                        .map((table) => (
                          <option key={table.id} value={table.id}>
                            {table.name} - ظرفیت {toPersianNumber(table.capacity)} نفر
                          </option>
                        ))}
                    </select>
                  </label>
                  <label>
                    <span>تعداد افراد</span>
                    <input
                      className="ltr-input"
                      type="number"
                      min="1"
                      value={occupyForm.peopleCount}
                      onChange={(event) =>
                        setOccupyForm((currentForm) => ({
                          ...currentForm,
                          peopleCount: Number(event.target.value),
                        }))
                      }
                    />
                  </label>
                  <label>
                    <span>یادداشت اختیاری</span>
                    <textarea
                      rows="3"
                      value={occupyForm.note}
                      onChange={(event) =>
                        setOccupyForm((currentForm) => ({
                          ...currentForm,
                          note: event.target.value,
                        }))
                      }
                    />
                  </label>
                  <button type="submit" disabled={saving || !occupyForm.tableId}>
                    {saving ? 'در حال ثبت...' : 'ثبت اشغال میز'}
                  </button>
                </form>
              </div>
            ) : null}

            {isSalonTableModalOpen ? (
              <div className="modal-backdrop" role="presentation">
                <form className="panel form-panel modal-card" onSubmit={handleSalonTableSubmit}>
                <div className="panel-head">
                  <div>
                    <h3>{salonTableForm.id ? 'ویرایش میز' : 'میز جدید'}</h3>
                    <p>میزها، ظرفیت سالن و نقشه عملیاتی کافه را مشخص می‌کنند.</p>
                  </div>
                  <button type="button" className="ghost-button" onClick={closeSalonTableModal}>
                    بستن
                  </button>
                </div>
                <label>
                  <span>نام میز</span>
                  <input
                    value={salonTableForm.name}
                    onChange={(event) =>
                      setSalonTableForm((currentForm) => ({
                        ...currentForm,
                        name: event.target.value,
                      }))
                    }
                  />
                </label>
                <div className="field-grid">
                  <label>
                    <span>ظرفیت</span>
                    <input
                      className="ltr-input"
                      type="number"
                      min="1"
                      value={salonTableForm.capacity}
                      onChange={(event) =>
                        setSalonTableForm((currentForm) => ({
                          ...currentForm,
                          capacity: Number(event.target.value),
                        }))
                      }
                    />
                  </label>
                  <label>
                    <span>ترتیب نمایش</span>
                    <input
                      className="ltr-input"
                      type="number"
                      min="0"
                      value={salonTableForm.displayOrder}
                      onChange={(event) =>
                        setSalonTableForm((currentForm) => ({
                          ...currentForm,
                          displayOrder: Number(event.target.value),
                        }))
                      }
                    />
                  </label>
                </div>
                <label className="checkbox-row">
                  <input
                    type="checkbox"
                    checked={salonTableForm.isActive}
                    onChange={(event) =>
                      setSalonTableForm((currentForm) => ({
                        ...currentForm,
                        isActive: event.target.checked,
                      }))
                    }
                  />
                  <span>میز فعال باشد</span>
                </label>
                <div className="form-actions">
                  <button type="submit" disabled={saving}>
                    {saving ? 'در حال ذخیره...' : 'ذخیره میز'}
                  </button>
                  {salonTableForm.id ? (
                    <button
                      type="button"
                      className="link-button link-danger"
                      onClick={() => handleDeleteSalonTable(salonTableForm.id)}
                    >
                      حذف میز
                    </button>
                  ) : null}
                </div>
              </form>
              </div>
            ) : null}
          </section>
        ) : null}

        {activeTab === 'categories' ? (
          <section className="two-column-layout">
            <form className="panel form-panel" onSubmit={handleCategorySubmit}>
              <div className="panel-head">
                <div>
                  <h3>{categoryForm.id ? 'ویرایش دسته‌بندی' : 'دسته‌بندی جدید'}</h3>
                  <p>دسته‌بندی‌ها فیلترها و گروه‌بندی منوی عمومی را کنترل می‌کنند.</p>
                </div>
                <button type="button" className="ghost-button" onClick={resetCategoryForm}>
                  پاک کردن فرم
                </button>
              </div>
              <label>
                <span>اسلاگ انگلیسی</span>
                <input
                  className="ltr-input"
                  value={categoryForm.slug}
                  onChange={(event) =>
                    setCategoryForm((currentForm) => ({ ...currentForm, slug: event.target.value }))
                  }
                />
              </label>
              <label>
                <span>نام دسته‌بندی</span>
                <input
                  value={categoryForm.name}
                  onChange={(event) =>
                    setCategoryForm((currentForm) => ({ ...currentForm, name: event.target.value }))
                  }
                />
              </label>
              <label>
                <span>ترتیب نمایش</span>
                <input
                  className="ltr-input"
                  type="number"
                  value={categoryForm.displayOrder}
                  onChange={(event) =>
                    setCategoryForm((currentForm) => ({
                      ...currentForm,
                      displayOrder: Number(event.target.value),
                    }))
                  }
                />
              </label>
              <label className="checkbox-row">
                <input
                  type="checkbox"
                  checked={categoryForm.isActive}
                  onChange={(event) =>
                    setCategoryForm((currentForm) => ({
                      ...currentForm,
                      isActive: event.target.checked,
                    }))
                  }
                />
                <span>دسته‌بندی فعال باشد</span>
              </label>
              <button type="submit" disabled={saving}>
                {saving ? 'در حال ذخیره...' : 'ذخیره دسته‌بندی'}
              </button>
            </form>

            <section className="panel list-panel">
              <div className="panel-head">
                <div>
                  <h3>دسته‌بندی‌های موجود</h3>
                  <p>بارگذاری صفحه‌بندی شده از سرور</p>
                </div>
              </div>
              <PaginatedAdminTable
                apiPath="/admin/categories"
                token={session.token}
                columns={categoryColumns}
                enabled={activeTab === 'categories'}
                reloadToken={tableReloadToken}
                searchPlaceholder="جستجو در دسته‌بندی‌ها..."
              />
            </section>
          </section>
        ) : null}

        {activeTab === 'menu-items' ? (
          <section className="two-column-layout">
            <form className="panel form-panel" onSubmit={handleMenuItemSubmit}>
              <div className="panel-head">
                <div>
                  <h3>{menuItemForm.id ? 'ویرایش آیتم منو' : 'آیتم جدید منو'}</h3>
                  <p>آیتم‌های منو هم در بخش پرفروش‌ها و هم در منوی عمومی نمایش داده می‌شوند.</p>
                </div>
                <button type="button" className="ghost-button" onClick={resetMenuItemForm}>
                  پاک کردن فرم
                </button>
              </div>
              <label>
                <span>دسته‌بندی</span>
                <select
                  value={menuItemForm.categoryId}
                  onChange={(event) =>
                    setMenuItemForm((currentForm) => ({
                      ...currentForm,
                      categoryId: event.target.value,
                    }))
                  }
                >
                  <option value="">یک دسته‌بندی انتخاب کنید</option>
                  {categoryOptions.map((category) => (
                    <option key={category.id} value={category.id}>
                      {category.name}
                    </option>
                  ))}
                </select>
              </label>
              <div className="form-section">
                <h4>هویت آیتم</h4>
                <div className="field-grid">
                  <label>
                    <span>نام فارسی</span>
                    <input
                      value={menuItemForm.persianName}
                      onChange={(event) =>
                        setMenuItemForm((currentForm) => ({
                          ...currentForm,
                          persianName: event.target.value,
                        }))
                      }
                    />
                  </label>
                  <label>
                    <span>نام انگلیسی</span>
                    <input
                      className="ltr-input"
                      value={menuItemForm.englishName}
                      onChange={(event) =>
                        setMenuItemForm((currentForm) => ({
                          ...currentForm,
                          englishName: event.target.value,
                        }))
                      }
                    />
                  </label>
                </div>
              </div>
              <label>
                <span>توضیحات</span>
                <textarea
                  rows="4"
                  value={menuItemForm.description}
                  onChange={(event) =>
                    setMenuItemForm((currentForm) => ({
                      ...currentForm,
                      description: event.target.value,
                    }))
                  }
                />
              </label>
              <div className="form-section">
                <h4>تصاویر</h4>
                <label>
                  <span>آدرس تصویر اصلی</span>
                  <input
                    className="ltr-input"
                    type="url"
                    value={menuItemForm.imageUrl}
                    onChange={(event) =>
                      setMenuItemForm((currentForm) => ({
                        ...currentForm,
                        imageUrl: event.target.value,
                      }))
                    }
                  />
                </label>
                <label>
                  <span>گالری تصاویر (هر آدرس در یک خط)</span>
                  <textarea
                    className="ltr-input"
                    rows="3"
                    value={menuItemForm.galleryImageUrls}
                    onChange={(event) =>
                      setMenuItemForm((currentForm) => ({
                        ...currentForm,
                        galleryImageUrls: event.target.value,
                      }))
                    }
                  />
                </label>
              </div>

              <div className="form-section">
                <h4>قیمت‌گذاری و سایزها</h4>
                <div className="variant-grid">
                  {[
                    ['small', 'کوچک'],
                    ['medium', 'متوسط'],
                    ['large', 'بزرگ'],
                  ].map(([variantKey, variantLabel]) => (
                    <div key={variantKey} className="variant-card">
                      <strong>{variantLabel}</strong>
                      <label>
                        <span>قیمت</span>
                        <input
                          className="ltr-input"
                          type="number"
                          min="0"
                          value={menuItemForm[`${variantKey}Price`]}
                          onChange={(event) =>
                            setMenuItemForm((currentForm) => ({
                              ...currentForm,
                              [`${variantKey}Price`]: event.target.value,
                            }))
                          }
                        />
                      </label>
                      <label>
                        <span>قیمت پیشنهادی</span>
                        <input
                          className="ltr-input"
                          type="number"
                          min="0"
                          value={menuItemForm[`${variantKey}SpecialOfferPrice`]}
                          onChange={(event) =>
                            setMenuItemForm((currentForm) => ({
                              ...currentForm,
                              [`${variantKey}SpecialOfferPrice`]: event.target.value,
                            }))
                          }
                        />
                      </label>
                      <label>
                        <span>حجم همین سایز</span>
                        <input
                          value={menuItemForm[`${variantKey}Volume`]}
                          onChange={(event) =>
                            setMenuItemForm((currentForm) => ({
                              ...currentForm,
                              [`${variantKey}Volume`]: event.target.value,
                            }))
                          }
                        />
                      </label>
                    </div>
                  ))}
                </div>
                <div className="field-grid">
                  <label>
                    <span>قیمت پیشنهادی عمومی</span>
                    <input
                      className="ltr-input"
                      type="number"
                      min="0"
                      value={menuItemForm.specialOfferPrice}
                      onChange={(event) =>
                        setMenuItemForm((currentForm) => ({
                          ...currentForm,
                          specialOfferPrice: event.target.value,
                        }))
                      }
                    />
                  </label>
                  <label>
                    <span>درصد مالیات</span>
                    <input
                      className="ltr-input"
                      type="number"
                      min="0"
                      step="0.01"
                      value={menuItemForm.taxPercent}
                      onChange={(event) =>
                        setMenuItemForm((currentForm) => ({
                          ...currentForm,
                          taxPercent: event.target.value,
                        }))
                      }
                    />
                  </label>
                </div>
              </div>

              <div className="form-section">
                <h4>جزئیات محصول</h4>
                <div className="field-grid">
                  <label>
                    <span>زمان آماده‌سازی (دقیقه)</span>
                    <input
                      className="ltr-input"
                      type="number"
                      min="0"
                      value={menuItemForm.preparingDurationMinutes}
                      onChange={(event) =>
                        setMenuItemForm((currentForm) => ({
                          ...currentForm,
                          preparingDurationMinutes: event.target.value,
                        }))
                      }
                    />
                  </label>
                  <label>
                    <span>کالری</span>
                    <input
                      className="ltr-input"
                      type="number"
                      min="0"
                      value={menuItemForm.calories}
                      onChange={(event) =>
                        setMenuItemForm((currentForm) => ({
                          ...currentForm,
                          calories: event.target.value,
                        }))
                      }
                    />
                  </label>
                  <label>
                    <span>حجم عمومی</span>
                    <input
                      value={menuItemForm.volume}
                      onChange={(event) =>
                        setMenuItemForm((currentForm) => ({
                          ...currentForm,
                          volume: event.target.value,
                        }))
                      }
                    />
                  </label>
                  <label>
                    <span>آلرژن‌ها (با کاما یا خط جدید جدا کنید)</span>
                    <textarea
                      rows="3"
                      value={menuItemForm.allergens}
                      onChange={(event) =>
                        setMenuItemForm((currentForm) => ({
                          ...currentForm,
                          allergens: event.target.value,
                        }))
                      }
                    />
                  </label>
                </div>
              </div>

              <div className="form-section">
                <h4>نمایش و وضعیت</h4>
                <label>
                  <span>برچسب‌ها (با کاما جدا کنید)</span>
                  <input
                    value={menuItemForm.tags}
                    onChange={(event) =>
                      setMenuItemForm((currentForm) => ({ ...currentForm, tags: event.target.value }))
                    }
                  />
                </label>
                <div className="field-grid">
                  <label>
                    <span>نشان ویژه</span>
                    <input
                      value={menuItemForm.badge}
                      onChange={(event) =>
                        setMenuItemForm((currentForm) => ({ ...currentForm, badge: event.target.value }))
                      }
                    />
                  </label>
                  <label>
                    <span>حروف کوتاه</span>
                    <input
                      className="ltr-input"
                      value={menuItemForm.shortLabel}
                      onChange={(event) =>
                        setMenuItemForm((currentForm) => ({
                          ...currentForm,
                          shortLabel: event.target.value,
                        }))
                      }
                    />
                  </label>
                  <label>
                    <span>ترتیب نمایش</span>
                    <input
                      className="ltr-input"
                      type="number"
                      value={menuItemForm.displayOrder}
                      onChange={(event) =>
                        setMenuItemForm((currentForm) => ({
                          ...currentForm,
                          displayOrder: Number(event.target.value),
                        }))
                      }
                    />
                  </label>
                  <label>
                    <span>گرادینت پس‌زمینه</span>
                    <input
                      className="ltr-input"
                      value={menuItemForm.accent}
                      onChange={(event) =>
                        setMenuItemForm((currentForm) => ({ ...currentForm, accent: event.target.value }))
                      }
                    />
                  </label>
                </div>
                <div className="field-grid compact-grid">
                  <label className="checkbox-row">
                    <input
                      type="checkbox"
                      checked={menuItemForm.featured}
                      onChange={(event) =>
                        setMenuItemForm((currentForm) => ({
                          ...currentForm,
                          featured: event.target.checked,
                        }))
                      }
                    />
                    <span>در بخش ویژه نمایش داده شود</span>
                  </label>
                  <label className="checkbox-row">
                    <input
                      type="checkbox"
                      checked={menuItemForm.isActive}
                      onChange={(event) =>
                        setMenuItemForm((currentForm) => ({
                          ...currentForm,
                          isActive: event.target.checked,
                        }))
                      }
                    />
                    <span>قابل نمایش باشد</span>
                  </label>
                  <label className="checkbox-row">
                    <input
                      type="checkbox"
                      checked={menuItemForm.isOrderable}
                      onChange={(event) =>
                        setMenuItemForm((currentForm) => ({
                          ...currentForm,
                          isOrderable: event.target.checked,
                        }))
                      }
                    />
                    <span>قابل سفارش باشد</span>
                  </label>
                </div>
              </div>
              <button type="submit" disabled={saving || !categoryOptions.length}>
                {saving ? 'در حال ذخیره...' : 'ذخیره آیتم منو'}
              </button>
            </form>

            <section className="panel list-panel">
              <div className="panel-head">
                <div>
                  <h3>آیتم‌های موجود منو</h3>
                  <p>بارگذاری صفحه‌بندی شده از سرور</p>
                </div>
              </div>
              <PaginatedAdminTable
                apiPath="/admin/menu-items"
                token={session.token}
                columns={menuItemColumns}
                enabled={activeTab === 'menu-items'}
                reloadToken={tableReloadToken}
                searchPlaceholder="جستجو در آیتم‌های منو..."
              />
            </section>
          </section>
        ) : null}

        {activeTab === 'addons' ? (
          <section className="two-column-layout">
            <form
              className="panel form-panel"
              onSubmit={async (event) => {
                event.preventDefault()
                setSaving(true)
                setError('')
                setMessage('')

                try {
                  const path = addonForm.id ? `/admin/addons/${addonForm.id}` : '/admin/addons'

                  await apiRequest(path, {
                    method: addonForm.id ? 'PUT' : 'POST',
                    token: session.token,
                    body: {
                      persianName: addonForm.persianName,
                      englishName: addonForm.englishName,
                      description: addonForm.description,
                      price: Number(addonForm.price),
                      tags: splitTags(addonForm.tags),
                      displayOrder: Number(addonForm.displayOrder),
                      isActive: addonForm.isActive,
                    },
                  })

                  setAddonForm({ ...emptyAddonForm, displayOrder: 0 })
                  setMessage('افزودنی با موفقیت ذخیره شد.')
                  bumpTableReload()
                } catch (requestError) {
                  setError(getReadableError(requestError))
                } finally {
                  setSaving(false)
                }
              }}
            >
              <div className="panel-head">
                <div>
                  <h3>{addonForm.id ? 'ویرایش افزودنی' : 'افزودنی جدید'}</h3>
                  <p>افزودنی‌ها مثل شات اضافه، شربت و ... به آیتم‌های اصلی منو اضافه می‌شوند.</p>
                </div>
                <button
                  type="button"
                  className="ghost-button"
                  onClick={() =>
                    setAddonForm({
                      ...emptyAddonForm,
                      displayOrder: 0,
                    })
                  }
                >
                  پاک کردن فرم
                </button>
              </div>
              <div className="field-grid">
                <label>
                  <span>نام فارسی</span>
                  <input
                    value={addonForm.persianName}
                    onChange={(event) =>
                      setAddonForm((currentForm) => ({
                        ...currentForm,
                        persianName: event.target.value,
                      }))
                    }
                  />
                </label>
                <label>
                  <span>نام انگلیسی</span>
                  <input
                    className="ltr-input"
                    value={addonForm.englishName}
                    onChange={(event) =>
                      setAddonForm((currentForm) => ({
                        ...currentForm,
                        englishName: event.target.value,
                      }))
                    }
                  />
                </label>
              </div>
              <label>
                <span>توضیحات</span>
                <textarea
                  rows="3"
                  value={addonForm.description}
                  onChange={(event) =>
                    setAddonForm((currentForm) => ({
                      ...currentForm,
                      description: event.target.value,
                    }))
                  }
                />
              </label>
              <div className="field-grid">
                <label>
                  <span>قیمت</span>
                  <input
                    className="ltr-input"
                    type="number"
                    min="0"
                    value={addonForm.price}
                    onChange={(event) =>
                      setAddonForm((currentForm) => ({
                        ...currentForm,
                        price: Number(event.target.value),
                      }))
                    }
                  />
                </label>
                <label>
                  <span>ترتیب نمایش</span>
                  <input
                    className="ltr-input"
                    type="number"
                    min="0"
                    value={addonForm.displayOrder}
                    onChange={(event) =>
                      setAddonForm((currentForm) => ({
                        ...currentForm,
                        displayOrder: Number(event.target.value),
                      }))
                    }
                  />
                </label>
              </div>
              <label>
                <span>برچسب‌ها (با کاما جدا کنید)</span>
                <input
                  value={addonForm.tags}
                  onChange={(event) =>
                    setAddonForm((currentForm) => ({
                      ...currentForm,
                      tags: event.target.value,
                    }))
                  }
                />
              </label>
              <label className="checkbox-row">
                <input
                  type="checkbox"
                  checked={addonForm.isActive}
                  onChange={(event) =>
                    setAddonForm((currentForm) => ({
                      ...currentForm,
                      isActive: event.target.checked,
                    }))
                  }
                />
                <span>افزودنی فعال باشد</span>
              </label>
              <button type="submit" disabled={saving}>
                {saving ? 'در حال ذخیره...' : 'ذخیره افزودنی'}
              </button>
            </form>

            <section className="panel list-panel">
              <div className="panel-head">
                <div>
                  <h3>افزودنی‌های موجود</h3>
                  <p>بارگذاری صفحه‌بندی شده از سرور</p>
                </div>
              </div>
              <PaginatedAdminTable
                apiPath="/admin/addons"
                token={session.token}
                columns={addonColumns}
                enabled={activeTab === 'addons'}
                reloadToken={tableReloadToken}
                searchPlaceholder="جستجو در افزودنی‌ها..."
              />
            </section>
          </section>
        ) : null}

        {activeTab === 'modifiers' ? (
          <section className="two-column-layout">
            <form
              className="panel form-panel"
              onSubmit={async (event) => {
                event.preventDefault()
                setSaving(true)
                setError('')
                setMessage('')

                try {
                  const path = modifierForm.id
                    ? `/admin/modifiers/${modifierForm.id}`
                    : '/admin/modifiers'

                  await apiRequest(path, {
                    method: modifierForm.id ? 'PUT' : 'POST',
                    token: session.token,
                    body: {
                      persianName: modifierForm.persianName,
                      englishName: modifierForm.englishName,
                      displayOrder: Number(modifierForm.displayOrder),
                      isActive: modifierForm.isActive,
                    },
                  })

                  setModifierForm({ ...emptyModifierForm, displayOrder: 0 })
                  setMessage('گزینه تغییر سفارش با موفقیت ذخیره شد.')
                  bumpTableReload()
                } catch (requestError) {
                  setError(getReadableError(requestError))
                } finally {
                  setSaving(false)
                }
              }}
            >
              <div className="panel-head">
                <div>
                  <h3>{modifierForm.id ? 'ویرایش تغییر سفارش' : 'تغییر سفارش جدید'}</h3>
                  <p>این گزینه‌ها برای مشتری در منوی دیجیتال قابل انتخاب هستند.</p>
                </div>
                <button
                  type="button"
                  className="ghost-button"
                  onClick={() => setModifierForm({ ...emptyModifierForm, displayOrder: 0 })}
                >
                  پاک کردن فرم
                </button>
              </div>
              <div className="field-grid">
                <label>
                  <span>عنوان فارسی</span>
                  <input
                    value={modifierForm.persianName}
                    onChange={(event) =>
                      setModifierForm((currentForm) => ({
                        ...currentForm,
                        persianName: event.target.value,
                      }))
                    }
                  />
                </label>
                <label>
                  <span>عنوان انگلیسی</span>
                  <input
                    className="ltr-input"
                    value={modifierForm.englishName}
                    onChange={(event) =>
                      setModifierForm((currentForm) => ({
                        ...currentForm,
                        englishName: event.target.value,
                      }))
                    }
                  />
                </label>
              </div>
              <label>
                <span>ترتیب نمایش</span>
                <input
                  className="ltr-input"
                  type="number"
                  min="0"
                  value={modifierForm.displayOrder}
                  onChange={(event) =>
                    setModifierForm((currentForm) => ({
                      ...currentForm,
                      displayOrder: Number(event.target.value),
                    }))
                  }
                />
              </label>
              <label className="checkbox-row">
                <input
                  type="checkbox"
                  checked={modifierForm.isActive}
                  onChange={(event) =>
                    setModifierForm((currentForm) => ({
                      ...currentForm,
                      isActive: event.target.checked,
                    }))
                  }
                />
                <span>برای مشتری نمایش داده شود</span>
              </label>
              <button type="submit" disabled={saving}>
                {saving ? 'در حال ذخیره...' : 'ذخیره تغییر سفارش'}
              </button>
            </form>

            <section className="panel list-panel">
              <div className="panel-head">
                <div>
                  <h3>گزینه‌های تغییر سفارش</h3>
                  <p>بارگذاری صفحه‌بندی شده از سرور</p>
                </div>
              </div>
              <PaginatedAdminTable
                apiPath="/admin/modifiers"
                token={session.token}
                columns={modifierColumns}
                enabled={activeTab === 'modifiers'}
                reloadToken={tableReloadToken}
                searchPlaceholder="جستجو در تغییرات سفارش..."
              />
            </section>
          </section>
        ) : null}

        {activeTab === 'ingredients' ? (
          <section className="two-column-layout">
            <form
              className="panel form-panel"
              onSubmit={async (event) => {
                event.preventDefault()
                setSaving(true)
                setError('')
                setMessage('')

                try {
                  const path = ingredientForm.id
                    ? `/admin/ingredients/${ingredientForm.id}`
                    : '/admin/ingredients'

                  await apiRequest(path, {
                    method: ingredientForm.id ? 'PUT' : 'POST',
                    token: session.token,
                    body: {
                      persianName: ingredientForm.persianName,
                      englishName: ingredientForm.englishName,
                      unit: ingredientForm.unit,
                      description: ingredientForm.description,
                      displayOrder: Number(ingredientForm.displayOrder),
                      isActive: ingredientForm.isActive,
                      inventoryLocation: ingredientForm.inventoryLocation,
                      unitCost: optionalNumber(ingredientForm.unitCost) ?? 0,
                    },
                  })

                  setIngredientForm({ ...emptyIngredientForm, displayOrder: 0 })
                  setMessage('ماده اولیه با موفقیت ذخیره شد.')
                  bumpTableReload()
                } catch (requestError) {
                  setError(getReadableError(requestError))
                } finally {
                  setSaving(false)
                }
              }}
            >
              <div className="panel-head">
                <div>
                  <h3>{ingredientForm.id ? 'ویرایش ماده اولیه' : 'ماده اولیه جدید'}</h3>
                  <p>مواد اولیه در دستور پخت آیتم‌های منو استفاده می‌شوند.</p>
                </div>
                <button
                  type="button"
                  className="ghost-button"
                  onClick={() => setIngredientForm({ ...emptyIngredientForm, displayOrder: 0 })}
                >
                  پاک کردن فرم
                </button>
              </div>
              <div className="field-grid">
                <label>
                  <span>نام فارسی</span>
                  <input
                    required
                    value={ingredientForm.persianName}
                    onChange={(event) =>
                      setIngredientForm((currentForm) => ({ ...currentForm, persianName: event.target.value }))
                    }
                  />
                </label>
                <label>
                  <span>نام انگلیسی</span>
                  <input
                    className="ltr-input"
                    required
                    value={ingredientForm.englishName}
                    onChange={(event) =>
                      setIngredientForm((currentForm) => ({ ...currentForm, englishName: event.target.value }))
                    }
                  />
                </label>
              </div>
              <div className="field-grid">
                <label>
                  <span>واحد اندازه‌گیری</span>
                  <select
                    className="ltr-input"
                    value={ingredientForm.unit}
                    onChange={(event) =>
                      setIngredientForm((currentForm) => ({ ...currentForm, unit: event.target.value }))
                    }
                  >
                    {UNIT_OPTIONS.map((unit) => (
                      <option key={unit} value={unit}>{unit}</option>
                    ))}
                  </select>
                </label>
                <label>
                  <span>محل نگهداری</span>
                  <select
                    value={ingredientForm.inventoryLocation}
                    onChange={(event) =>
                      setIngredientForm((currentForm) => ({ ...currentForm, inventoryLocation: event.target.value }))
                    }
                  >
                    {LOCATION_OPTIONS.map((loc) => (
                      <option key={loc.value} value={loc.value}>{loc.label}</option>
                    ))}
                  </select>
                </label>
              </div>
              <label>
                <span>توضیح (اختیاری)</span>
                <input
                  value={ingredientForm.description}
                  onChange={(event) =>
                    setIngredientForm((currentForm) => ({ ...currentForm, description: event.target.value }))
                  }
                />
              </label>
              <label>
                <span>بهای واحد (تومان — برای محاسبه سود)</span>
                <input
                  className="ltr-input"
                  type="number"
                  min="0"
                  step="0.01"
                  value={ingredientForm.unitCost}
                  onChange={(event) =>
                    setIngredientForm((currentForm) => ({ ...currentForm, unitCost: event.target.value }))
                  }
                />
              </label>
              <label>
                <span>ترتیب نمایش</span>
                <input
                  className="ltr-input"
                  type="number"
                  min="0"
                  value={ingredientForm.displayOrder}
                  onChange={(event) =>
                    setIngredientForm((currentForm) => ({ ...currentForm, displayOrder: Number(event.target.value) }))
                  }
                />
              </label>
              <label className="checkbox-row">
                <input
                  type="checkbox"
                  checked={ingredientForm.isActive}
                  onChange={(event) =>
                    setIngredientForm((currentForm) => ({ ...currentForm, isActive: event.target.checked }))
                  }
                />
                <span>فعال (قابل استفاده در دستور پخت)</span>
              </label>
              <button type="submit" disabled={saving}>
                {saving ? 'در حال ذخیره...' : 'ذخیره ماده اولیه'}
              </button>
            </form>

            <section className="panel list-panel">
              <div className="panel-head">
                <div>
                  <h3>مواد اولیه</h3>
                  <p>بارگذاری صفحه‌بندی شده از سرور</p>
                </div>
              </div>
              <PaginatedAdminTable
                apiPath="/admin/ingredients"
                token={session.token}
                columns={ingredientColumns}
                enabled={activeTab === 'ingredients'}
                reloadToken={tableReloadToken}
                searchPlaceholder="جستجو در مواد اولیه..."
              />
            </section>
          </section>
        ) : null}

        {activeTab === 'recipes' ? (
          <section className="panel-stack">
            <section className="panel">
              <div className="panel-head">
                <div>
                  <h3>انتخاب آیتم منو</h3>
                  <p>آیتمی را انتخاب کنید تا دستور پخت آن را مشاهده یا ویرایش کنید.</p>
                </div>
              </div>
              <label>
                <span>آیتم منو</span>
                <select
                  value={recipeState.selectedMenuItemId}
                  onChange={async (event) => {
                    const menuItemId = event.target.value
                    setRecipeState((current) => ({
                      ...current,
                      selectedMenuItemId: menuItemId,
                      lines: [],
                      lineForm: emptyRecipeLineForm,
                    }))

                    if (!menuItemId) {
                      return
                    }

                    try {
                      const lines = await apiRequest(`/admin/recipes/${menuItemId}`, { token: session.token })
                      setRecipeState((current) => ({ ...current, lines }))
                    } catch (requestError) {
                      setError(getReadableError(requestError))
                    }
                  }}
                >
                  <option value="">-- انتخاب آیتم منو --</option>
                  {menuItemOptions.map((item) => (
                    <option key={item.id} value={item.id}>
                      {item.persianName} — {item.englishName}
                    </option>
                  ))}
                </select>
              </label>
            </section>

            {recipeState.selectedMenuItemId ? (
              <section className="two-column-layout">
                <form
                  className="panel form-panel"
                  onSubmit={async (event) => {
                    event.preventDefault()
                    if (!recipeState.lineForm.ingredientId || !recipeState.lineForm.quantity) {
                      return
                    }

                    setSaving(true)
                    setError('')
                    setMessage('')

                    try {
                      await apiRequest(`/admin/recipes/${recipeState.selectedMenuItemId}`, {
                        method: 'POST',
                        token: session.token,
                        body: {
                          menuItemId: recipeState.selectedMenuItemId,
                          ingredientId: recipeState.lineForm.ingredientId,
                          quantity: Number(recipeState.lineForm.quantity),
                        },
                      })

                      setRecipeState((current) => ({
                        ...current,
                        lineForm: emptyRecipeLineForm,
                      }))
                      setMessage('خط دستور پخت ذخیره شد.')

                      const lines = await apiRequest(`/admin/recipes/${recipeState.selectedMenuItemId}`, {
                        token: session.token,
                      })
                      setRecipeState((current) => ({ ...current, lines }))
                    } catch (requestError) {
                      setError(getReadableError(requestError))
                    } finally {
                      setSaving(false)
                    }
                  }}
                >
                  <div className="panel-head">
                    <div>
                      <h3>افزودن / ویرایش ماده</h3>
                      <p>اگر این ماده قبلاً در دستور پخت بوده، مقدار آن به‌روزرسانی می‌شود.</p>
                    </div>
                  </div>
                  <label>
                    <span>ماده اولیه</span>
                    <select
                      required
                      value={recipeState.lineForm.ingredientId}
                      onChange={(event) =>
                        setRecipeState((current) => ({
                          ...current,
                          lineForm: { ...current.lineForm, ingredientId: event.target.value },
                        }))
                      }
                    >
                      <option value="">-- انتخاب ماده اولیه --</option>
                      {ingredients
                        .filter((ingredient) => ingredient.isActive)
                        .map((ingredient) => (
                          <option key={ingredient.id} value={ingredient.id}>
                            {ingredient.persianName} ({ingredient.unit})
                          </option>
                        ))}
                    </select>
                  </label>
                  <label>
                    <span>مقدار</span>
                    <input
                      className="ltr-input"
                      type="number"
                      min="0.001"
                      step="0.001"
                      required
                      value={recipeState.lineForm.quantity}
                      onChange={(event) =>
                        setRecipeState((current) => ({
                          ...current,
                          lineForm: { ...current.lineForm, quantity: event.target.value },
                        }))
                      }
                    />
                  </label>
                  <button type="submit" disabled={saving}>
                    {saving ? 'در حال ذخیره...' : 'ذخیره در دستور پخت'}
                  </button>
                </form>

                <section className="panel list-panel">
                  <div className="panel-head">
                    <div>
                      <h3>دستور پخت</h3>
                      <p>
                        {recipeState.lines.length === 0
                          ? 'هنوز هیچ ماده‌ای ثبت نشده.'
                          : `${toPersianNumber(recipeState.lines.length)} ماده`}
                      </p>
                    </div>
                  </div>
                  <DataTable
                    data={recipeState.lines}
                    columns={recipeColumns}
                    searchPlaceholder="جستجو در دستور پخت..."
                    emptyMessage="هنوز هیچ ماده‌ای ثبت نشده."
                  />
                </section>
              </section>
            ) : null}
          </section>
        ) : null}

        {activeTab === 'inventory' ? (
          <section className="panel-stack">
            {inventoryStatus ? (
              <>
                <div className="inventory-summary">
                  <div className="stat-card">
                    <span className="stat-value">{toPersianNumber(inventoryStatus.totalIngredients)}</span>
                    <span className="stat-label">مواد اولیه</span>
                  </div>
                  {inventoryStatus.lowStockCount > 0 ? (
                    <div className="stat-card stat-card--danger">
                      <span className="stat-value">{toPersianNumber(inventoryStatus.lowStockCount)}</span>
                      <span className="stat-label">کم موجودی</span>
                    </div>
                  ) : (
                    <div className="stat-card stat-card--ok">
                      <span className="stat-value">موجودی سالم</span>
                      <span className="stat-label">همه مواد در سطح مناسب</span>
                    </div>
                  )}
                  <button
                    type="button"
                    className="ghost-button"
                    onClick={async () => {
                      const data = await apiRequest('/admin/inventory', { token: session.token })
                      setInventoryStatus(data)
                    }}
                  >
                    بروزرسانی
                  </button>
                </div>

                {inventoryStatus.groups.map((group) => (
                  <section key={group.location} className="panel">
                    <div className="panel-head">
                      <div>
                        <h3>{group.locationLabel}</h3>
                        <p>{toPersianNumber(group.items.length)} ماده اولیه</p>
                      </div>
                    </div>
                    <DataTable
                      data={group.items}
                      columns={inventoryColumns}
                      searchPlaceholder={`جستجو در ${group.locationLabel}...`}
                    />
                  </section>
                ))}
              </>
            ) : (
              <section className="panel"><p>در حال بارگذاری...</p></section>
            )}

            {inventoryAction.mode === 'restock' ? (
              <div className="modal-backdrop" onClick={() => setInventoryAction({ ingredientId: null, mode: null })}>
                <div className="modal-card" onClick={(e) => e.stopPropagation()}>
                  <h3>شارژ موجودی</h3>
                  <form
                    onSubmit={async (event) => {
                      event.preventDefault()
                      setSaving(true)
                      setError('')
                      try {
                        await apiRequest(`/admin/inventory/${inventoryAction.ingredientId}/restock`, {
                          method: 'POST',
                          token: session.token,
                          body: { ingredientId: inventoryAction.ingredientId, quantity: Number(restockForm.quantity), note: restockForm.note },
                        })
                        setMessage('موجودی شارژ شد.')
                        setInventoryAction({ ingredientId: null, mode: null })
                        const data = await apiRequest('/admin/inventory', { token: session.token })
                        setInventoryStatus(data)
                      } catch (requestError) {
                        setError(getReadableError(requestError))
                      } finally {
                        setSaving(false)
                      }
                    }}
                  >
                    <label>
                      <span>مقدار اضافه شده</span>
                      <input className="ltr-input" type="number" min="0.001" step="0.001" required value={restockForm.quantity}
                        onChange={(e) => setRestockForm((f) => ({ ...f, quantity: e.target.value }))} />
                    </label>
                    <label>
                      <span>توضیح (اختیاری)</span>
                      <input value={restockForm.note} onChange={(e) => setRestockForm((f) => ({ ...f, note: e.target.value }))} />
                    </label>
                    <div className="modal-actions">
                      <button type="submit" disabled={saving}>{saving ? 'در حال ذخیره...' : 'ذخیره'}</button>
                      <button type="button" className="ghost-button" onClick={() => setInventoryAction({ ingredientId: null, mode: null })}>لغو</button>
                    </div>
                  </form>
                </div>
              </div>
            ) : null}

            {inventoryAction.mode === 'adjust' ? (
              <div className="modal-backdrop" onClick={() => setInventoryAction({ ingredientId: null, mode: null })}>
                <div className="modal-card" onClick={(e) => e.stopPropagation()}>
                  <h3>تصحیح موجودی</h3>
                  <p>مقدار مثبت = افزایش، مقدار منفی = کاهش</p>
                  <form
                    onSubmit={async (event) => {
                      event.preventDefault()
                      setSaving(true)
                      setError('')
                      try {
                        await apiRequest(`/admin/inventory/${inventoryAction.ingredientId}/adjust`, {
                          method: 'POST',
                          token: session.token,
                          body: { ingredientId: inventoryAction.ingredientId, delta: Number(adjustForm.delta), note: adjustForm.note },
                        })
                        setMessage('موجودی تصحیح شد.')
                        setInventoryAction({ ingredientId: null, mode: null })
                        const data = await apiRequest('/admin/inventory', { token: session.token })
                        setInventoryStatus(data)
                      } catch (requestError) {
                        setError(getReadableError(requestError))
                      } finally {
                        setSaving(false)
                      }
                    }}
                  >
                    <label>
                      <span>مقدار تغییر</span>
                      <input className="ltr-input" type="number" step="0.001" required value={adjustForm.delta}
                        onChange={(e) => setAdjustForm((f) => ({ ...f, delta: e.target.value }))} />
                    </label>
                    <label>
                      <span>دلیل تصحیح</span>
                      <input required value={adjustForm.note} onChange={(e) => setAdjustForm((f) => ({ ...f, note: e.target.value }))} />
                    </label>
                    <div className="modal-actions">
                      <button type="submit" disabled={saving}>{saving ? 'در حال ذخیره...' : 'ذخیره'}</button>
                      <button type="button" className="ghost-button" onClick={() => setInventoryAction({ ingredientId: null, mode: null })}>لغو</button>
                    </div>
                  </form>
                </div>
              </div>
            ) : null}

            {inventoryAction.mode === 'alert' ? (
              <div className="modal-backdrop" onClick={() => setInventoryAction({ ingredientId: null, mode: null })}>
                <div className="modal-card" onClick={(e) => e.stopPropagation()}>
                  <h3>تنظیم حد هشدار موجودی</h3>
                  <form
                    onSubmit={async (event) => {
                      event.preventDefault()
                      setSaving(true)
                      setError('')
                      try {
                        await apiRequest(`/admin/inventory/${inventoryAction.ingredientId}/alert-threshold`, {
                          method: 'POST',
                          token: session.token,
                          body: { ingredientId: inventoryAction.ingredientId, minAlertQuantity: Number(alertForm.minAlertQuantity) },
                        })
                        setMessage('حد هشدار ذخیره شد.')
                        setInventoryAction({ ingredientId: null, mode: null })
                        const data = await apiRequest('/admin/inventory', { token: session.token })
                        setInventoryStatus(data)
                      } catch (requestError) {
                        setError(getReadableError(requestError))
                      } finally {
                        setSaving(false)
                      }
                    }}
                  >
                    <label>
                      <span>حداقل موجودی قابل قبول</span>
                      <input className="ltr-input" type="number" min="0" step="0.001" required value={alertForm.minAlertQuantity}
                        onChange={(e) => setAlertForm((f) => ({ ...f, minAlertQuantity: e.target.value }))} />
                    </label>
                    <div className="modal-actions">
                      <button type="submit" disabled={saving}>{saving ? 'در حال ذخیره...' : 'ذخیره'}</button>
                      <button type="button" className="ghost-button" onClick={() => setInventoryAction({ ingredientId: null, mode: null })}>لغو</button>
                    </div>
                  </form>
                </div>
              </div>
            ) : null}

            {inventoryAction.mode === 'history' ? (
              <div className="modal-backdrop" onClick={() => setInventoryAction({ ingredientId: null, mode: null })}>
                <div className="modal-card modal-card--wide" onClick={(e) => e.stopPropagation()}>
                  <div className="panel-head">
                    <h3>تاریخچه موجودی</h3>
                    <button type="button" className="ghost-button" onClick={() => setInventoryAction({ ingredientId: null, mode: null })}>بستن</button>
                  </div>
                  <DataTable
                    data={stockHistory}
                    columns={stockHistoryColumns}
                    enableSearch={false}
                    enablePagination={stockHistory.length > 10}
                    pageSize={10}
                    compact
                    emptyMessage="تاریخچه‌ای ثبت نشده."
                  />
                </div>
              </div>
            ) : null}
          </section>
        ) : null}

        {activeTab === 'inventory-purchases' ? (
          <InventoryPurchasesPanel
            session={session}
            ingredientOptions={ingredientOptions}
            unitOptions={UNIT_OPTIONS}
            apiRequest={apiRequest}
            getReadableError={getReadableError}
            tableReloadToken={tableReloadToken}
            bumpTableReload={bumpTableReload}
            setMessage={setMessage}
            setError={setError}
            saving={saving}
            setSaving={setSaving}
            showExpiringOnly={purchaseExpiringOnly}
            onShowExpiringOnlyChange={setPurchaseExpiringOnly}
          />
        ) : null}

        {activeTab === 'record-sale' ? (
          <section className="panel-stack">
            <section className="panel">
              <div className="panel-head">
                <div>
                  <h3>ثبت فروش</h3>
                  <p>پس از ثبت، موجودی مواد اولیه مطابق دستور پخت کسر می‌شود.</p>
                </div>
                {saleResult ? (
                  <button type="button" className="ghost-button" onClick={() => setSaleResult(null)}>فروش جدید</button>
                ) : null}
              </div>

              {!saleResult ? (
                <form
                  onSubmit={async (event) => {
                    event.preventDefault()
                    setSaving(true)
                    setError('')
                    setMessage('')
                    try {
                      const result = await apiRequest('/admin/sales/record', {
                        method: 'POST',
                        token: session.token,
                        body: {
                          menuItemId: saleForm.menuItemId,
                          quantitySold: Number(saleForm.quantitySold),
                        },
                      })
                      setSaleResult(result)
                      setMessage('فروش ثبت شد و موجودی بروزرسانی شد.')
                      const data = await apiRequest('/admin/inventory', { token: session.token })
                      setInventoryStatus(data)
                    } catch (requestError) {
                      setError(getReadableError(requestError))
                    } finally {
                      setSaving(false)
                    }
                  }}
                >
                  <div className="field-grid">
                    <label>
                      <span>آیتم منو</span>
                      <select
                        required
                        value={saleForm.menuItemId}
                        onChange={(event) => setSaleForm((f) => ({ ...f, menuItemId: event.target.value }))}
                      >
                        <option value="">-- انتخاب آیتم --</option>
                        {menuItemOptions.map((item) => (
                          <option key={item.id} value={item.id}>{item.persianName} — {item.englishName}</option>
                        ))}
                      </select>
                    </label>
                    <label>
                      <span>تعداد فروخته شده</span>
                      <input
                        className="ltr-input"
                        type="number"
                        min="1"
                        required
                        value={saleForm.quantitySold}
                        onChange={(event) => setSaleForm((f) => ({ ...f, quantitySold: event.target.value }))}
                      />
                    </label>
                  </div>

                  {salePreviewLoading ? (
                    <p className="sale-preview-note">در حال بارگذاری پیش‌نمایش دستور پخت...</p>
                  ) : saleForm.menuItemId && salePreview.length === 0 ? (
                    <p className="sale-preview-note sale-preview-note--warning">
                      برای این آیتم دستور پختی ثبت نشده. ابتدا در تب «دستور پخت» مواد را تعریف کنید.
                    </p>
                  ) : salePreview.length > 0 ? (
                    <div className="sale-preview">
                      <h4>پیش‌نمایش کسر از موجودی</h4>
                      <DataTable
                        data={salePreview}
                        columns={salePreviewColumns}
                        enableSearch={false}
                        enablePagination={false}
                        compact
                      />
                    </div>
                  ) : null}

                  <button type="submit" disabled={saving || salePreview.length === 0}>
                    {saving ? 'در حال ثبت...' : 'تأیید و ثبت فروش'}
                  </button>
                </form>
              ) : (
                <div className="sale-result">
                  <h4>نتیجه ثبت فروش: {saleResult.menuItemName} × {toPersianNumber(saleResult.quantitySold)}</h4>
                  <DataTable
                    data={saleResult.deductions}
                    columns={saleResultColumns}
                    enableSearch={false}
                    enablePagination={false}
                    compact
                  />
                </div>
              )}
            </section>
          </section>
        ) : null}

        {activeTab === 'orders' ? (
          <section className="panel-stack">
            <section className="panel">
              <div className="panel-head">
                <div>
                  <h3>سفارش‌های آنلاین</h3>
                  <p>لیست سفارش‌های ثبت‌شده از منوی دیجیتال با وضعیت پرداخت.</p>
                </div>
                <button type="button" className="ghost-button" onClick={() => bumpTableReload()}>
                  بروزرسانی
                </button>
              </div>
              <PaginatedAdminTable
                apiPath="/admin/orders"
                token={session.token}
                columns={orderColumns}
                enabled={activeTab === 'orders'}
                reloadToken={tableReloadToken}
                searchPlaceholder="جستجو بر اساس کد پیگیری، نام یا شماره تماس..."
              />
            </section>
          </section>
        ) : null}

        {selectedOrder && orderEditForm ? (
          <div className="modal-backdrop" onClick={closeOrderEditor}>
            <div className="modal-card modal-card--wide" onClick={(event) => event.stopPropagation()}>
              <form onSubmit={handleSaveOrder}>
                <div className="panel-head">
                  <div>
                    <h3>مدیریت سفارش {selectedOrder.trackingCode}</h3>
                    <p>
                      {selectedOrder.isSplitBill ? 'تسویه جداگانه · ' : ''}
                      {selectedOrder.status}
                    </p>
                  </div>
                  <button type="button" className="ghost-button" onClick={closeOrderEditor}>
                    بستن
                  </button>
                </div>

                <div className="order-detail-meta">
                  <span>وضعیت: {selectedOrder.status}</span>
                  <span>مبلغ کل: {formatMoney(selectedOrder.totalAmount)} تومان</span>
                  <span>پرداخت شده: {formatMoney(selectedOrder.amountPaid ?? 0)} تومان</span>
                  <span>تاریخ: {formatDateTime(selectedOrder.createdAt)}</span>
                </div>

                <div className="settings-grid">
                  <label>
                    <span>نام مشتری</span>
                    <input
                      value={orderEditForm.customerName}
                      onChange={(event) =>
                        setOrderEditForm((current) => ({ ...current, customerName: event.target.value }))
                      }
                      disabled={selectedOrder.status === 'Cancelled'}
                    />
                  </label>
                  <label>
                    <span>شماره تماس</span>
                    <input
                      className="ltr-input"
                      value={orderEditForm.customerPhone}
                      onChange={(event) =>
                        setOrderEditForm((current) => ({ ...current, customerPhone: event.target.value }))
                      }
                      disabled={selectedOrder.status === 'Cancelled'}
                    />
                  </label>
                  <label className="full-width">
                    <span>توضیحات</span>
                    <textarea
                      rows="3"
                      value={orderEditForm.note}
                      onChange={(event) =>
                        setOrderEditForm((current) => ({ ...current, note: event.target.value }))
                      }
                      disabled={selectedOrder.status === 'Cancelled'}
                    />
                  </label>
                </div>

                <div className="panel-head">
                  <div>
                    <h4>اقلام سفارش</h4>
                    <p>تغییر تعداد یا حذف آیتم، موجودی را متناسب با وضعیت پرداخت تنظیم می‌کند.</p>
                  </div>
                </div>

                <div className="order-lines-editor">
                  {orderEditForm.lines.map((line) => (
                    <div key={line.id} className={`order-line-editor ${line.remove ? 'order-line-editor--removed' : ''}`}>
                      <div>
                        <strong>{line.persianName}</strong>
                        <div className="table-subtext ltr-text">{line.englishName}</div>
                        {line.sizeLabel ? <div className="table-subtext">{line.sizeLabel}</div> : null}
                        {line.payerLabel ? <div className="table-subtext">مهمان: {line.payerLabel}</div> : null}
                        {line.inventoryDeductedQuantity > 0 ? (
                          <div className="table-subtext">
                            موجودی کسر شده: {toPersianNumber(line.inventoryDeductedQuantity)} عدد
                          </div>
                        ) : null}
                      </div>
                      <label>
                        <span>تعداد</span>
                        <input
                          type="number"
                          min="1"
                          className="ltr-input"
                          value={line.quantity}
                          disabled={selectedOrder.status === 'Cancelled' || line.remove}
                          onChange={(event) => updateOrderLineQuantity(line.id, event.target.value)}
                        />
                      </label>
                      <strong>{toPersianNumber(line.lineTotal)} تومان</strong>
                      <label className="order-line-remove">
                        <input
                          type="checkbox"
                          checked={line.remove}
                          disabled={selectedOrder.status === 'Cancelled'}
                          onChange={() => toggleOrderLineRemoval(line.id)}
                        />
                        <span>حذف</span>
                      </label>
                    </div>
                  ))}
                </div>

                {selectedOrder.payments?.length ? (
                  <>
                    <div className="panel-head">
                      <div>
                        <h4>پرداخت‌های جداگانه</h4>
                      </div>
                    </div>
                    <DataTable
                      data={selectedOrder.payments}
                      columns={[
                        { accessorKey: 'payerLabel', header: 'مهمان' },
                        {
                          accessorKey: 'amount',
                          header: 'مبلغ',
                          cell: ({ getValue }) => `${toPersianNumber(getValue())} تومان`,
                        },
                        { accessorKey: 'status', header: 'وضعیت' },
                        {
                          accessorKey: 'paidAt',
                          header: 'زمان پرداخت',
                          cell: ({ getValue }) => (getValue() ? formatDateTime(getValue()) : '—'),
                        },
                      ]}
                      enableSearch={false}
                      enablePagination={false}
                      compact
                    />
                  </>
                ) : null}

                <div className="order-editor-actions">
                  {selectedOrder.status !== 'Cancelled' ? (
                    <>
                      <button type="submit" disabled={saving}>
                        {saving ? 'در حال ذخیره...' : 'ذخیره تغییرات'}
                      </button>
                      <button type="button" className="ghost-button link-danger" disabled={saving} onClick={handleCancelOrder}>
                        لغو سفارش و بازگردانی موجودی
                      </button>
                    </>
                  ) : null}
                </div>
              </form>
            </div>
          </div>
        ) : null}

        {activeTab === 'site-settings' ? (
          <form className="panel-stack" onSubmit={handleSettingsSubmit}>
            {settingsSections.map((section) => (
              <section key={section.title} className="panel">
                <div className="panel-head">
                  <div>
                    <h3>{section.title}</h3>
                    <p>این مقدارها مستقیما ظاهر منوی QR عمومی را کنترل می‌کنند.</p>
                  </div>
                </div>
                <div className="settings-grid">
                  {section.fields.map(([fieldName, fieldLabel, fieldType]) => (
                    <label
                      key={fieldName}
                      className={fieldType === 'textarea' ? 'full-width' : undefined}
                    >
                      <span>{fieldLabel}</span>
                      {fieldType === 'textarea' ? (
                        <textarea
                          className={fieldName === 'instagramUrl' ? 'ltr-input' : ''}
                          rows="4"
                          value={siteSettings[fieldName]}
                          onChange={(event) =>
                            setSiteSettings((currentSettings) => ({
                              ...currentSettings,
                              [fieldName]: event.target.value,
                            }))
                          }
                        />
                      ) : (
                        <input
                          className={fieldType === 'url' || fieldName === 'contactPhone' ? 'ltr-input' : ''}
                          type={fieldType ?? 'text'}
                          value={siteSettings[fieldName]}
                          onChange={(event) =>
                            setSiteSettings((currentSettings) => ({
                              ...currentSettings,
                              [fieldName]: event.target.value,
                            }))
                          }
                        />
                      )}
                    </label>
                  ))}
                </div>
              </section>
            ))}
            <button type="submit" className="save-settings-button" disabled={saving}>
              {saving ? 'در حال ذخیره...' : 'ذخیره تنظیمات سایت'}
            </button>
          </form>
        ) : null}
      </main>
    </div>
  )
}

export default App
