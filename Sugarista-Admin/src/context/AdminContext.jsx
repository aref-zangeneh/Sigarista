import { createContext, useContext, useEffect, useMemo, useRef, useState } from 'react'
import { useLocation, useNavigate } from 'react-router-dom'
import {
  buildAddonColumns,
  buildCategoryColumns,
  buildIngredientColumns,
  buildInventoryColumns,
  buildInventoryConsumptionColumns,
  buildMenuItemColumns,
  buildModifierColumns,
  buildRecipeColumns,
  buildSalePreviewColumns,
  buildSaleResultColumns,
  buildStockHistoryColumns,
  buildOrderColumns,
} from '../components/adminTableColumns'
import {
  countUnhandledKitchenOrders,
  playOrderAlertSound,
  unlockOrderAlertSound,
} from '../utils/orderAlertSound'
import { isKnownAdminPath, pathForTab, tabForPath, adminRoutes } from '../adminRoutes'
import { apiRequest, getReadableError, normalizeRecipeResponse } from '../utils/adminApi'
import { getDefaultRoutePath, hasPermission, PERMISSIONS } from '../utils/adminPermissions'
import { formatDateTime, formatDuration, toPersianNumber } from '../utils/adminFormatters'
import { joinList, joinTags, optionalNumber, splitList, splitTags } from '../utils/adminFormHelpers'
import { loadStoredSession, normalizeSession, SESSION_STORAGE_KEY } from '../utils/session'
import {
  emptyAddonForm,
  emptyAdjustForm,
  emptyAlertForm,
  emptyCategoryForm,
  emptyIngredientForm,
  emptyMenuItemForm,
  emptyModifierForm,
  emptyOccupyForm,
  emptyRecipeLineForm,
  emptyRecipeState,
  emptyRestockForm,
  emptySaleForm,
  emptySalonStatus,
  emptySalonTableForm,
  emptySettings,
  KIND_OPTIONS,
  LOCATION_OPTIONS,
} from '../constants/adminForms'

const AdminContext = createContext(null)

export function useAdmin() {
  const context = useContext(AdminContext)
  if (!context) {
    throw new Error('useAdmin must be used within AdminProvider')
  }
  return context
}

export function AdminProvider({ children }) {
  const navigate = useNavigate()
  const location = useLocation()
  const activeTab = useMemo(() => tabForPath(location.pathname), [location.pathname])
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
  const [orderAlerts, setOrderAlerts] = useState(null)
  const [lowStockAlert, setLowStockAlert] = useState(null)
  const [orderAlertPulse, setOrderAlertPulse] = useState(false)
  const [dismissedOrderAlertIds, setDismissedOrderAlertIds] = useState(() => new Set())
  const notifiedKitchenOrderIdsRef = useRef(null)
  const lastOrderAlertPollRef = useRef(null)
  const orderAlertsRef = useRef(null)
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
    orderAlertsRef.current = orderAlerts
  }, [orderAlerts])

  useEffect(() => {
    if (!session?.token) {
      return undefined
    }

    const soundIntervalId = window.setInterval(() => {
      if (countUnhandledKitchenOrders(orderAlertsRef.current) > 0) {
        playOrderAlertSound()
      }
    }, 10_000)

    return () => window.clearInterval(soundIntervalId)
  }, [session?.token])

  useEffect(() => {
    document.documentElement.lang = 'fa'
    document.documentElement.dir = 'rtl'
    document.title = 'پنل مدیریت شوگریستا'
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
    }
  }, [session?.token, session?.permissions, session?.isSuperAdmin, location.pathname, navigate])

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
    if (!session?.token || !hasPermission(session, PERMISSIONS.orders.view)) {
      return undefined
    }

    let cancelled = false

    async function pollOrderAlerts() {
      try {
        const since =
          lastOrderAlertPollRef.current ??
          new Date(Date.now() - 60_000).toISOString()

        const alerts = await apiRequest(
          `/admin/orders/alerts?since=${encodeURIComponent(since)}`,
          { token: session.token },
        )

        if (cancelled) {
          return
        }

        const kitchenOrders = alerts.activeKitchenOrders ?? []
        const visibleKitchenOrders = kitchenOrders.filter(
          (order) => !dismissedOrderAlertIds.has(order.id),
        )

        setOrderAlerts({
          ...alerts,
          activeKitchenOrders: visibleKitchenOrders,
          newPaidOrders: (alerts.newPaidOrders ?? []).filter(
            (order) => !dismissedOrderAlertIds.has(order.id),
          ),
        })

        if (notifiedKitchenOrderIdsRef.current === null) {
          notifiedKitchenOrderIdsRef.current = new Set(kitchenOrders.map((order) => order.id))
        } else {
          const freshOrders = kitchenOrders.filter(
            (order) => !notifiedKitchenOrderIdsRef.current.has(order.id),
          )

          if (freshOrders.length > 0) {
            for (const order of freshOrders) {
              notifiedKitchenOrderIdsRef.current.add(order.id)
            }

            playOrderAlertSound()
            setOrderAlertPulse(true)
            window.setTimeout(() => setOrderAlertPulse(false), 4000)
            bumpTableReload()

            const summary = freshOrders
              .map((order) => `${order.trackingCode} (${order.customerName})`)
              .join('، ')
            setMessage(`سفارش جدید پرداخت شد: ${summary}`)

            if (
              typeof window !== 'undefined' &&
              'Notification' in window &&
              Notification.permission === 'granted'
            ) {
              for (const order of freshOrders) {
                new Notification('سفارش جدید پرداخت شد', {
                  body: `${order.trackingCode} — ${order.customerName}`,
                  tag: `order-${order.id}`,
                })
              }
            }
          }
        }

        lastOrderAlertPollRef.current = alerts.serverTime ?? new Date().toISOString()
      } catch (pollError) {
        console.error('Order alerts polling failed:', pollError)
      }
    }

    pollOrderAlerts()
    const intervalId = window.setInterval(pollOrderAlerts, 4000)
    return () => {
      cancelled = true
      window.clearInterval(intervalId)
    }
  }, [session?.token, dismissedOrderAlertIds])

  useEffect(() => {
    if (!session?.token || !hasPermission(session, PERMISSIONS.inventory.view)) {
      return undefined
    }

    let cancelled = false
    let lastLowStockCount = null

    async function pollInventoryAlerts() {
      try {
        const status = await apiRequest('/admin/inventory', { token: session.token })
        if (cancelled || !status) {
          return
        }

        const lowItems = []
        for (const group of status.groups ?? []) {
          for (const item of group.items ?? []) {
            if (item.isLowStock) {
              lowItems.push({
                id: item.ingredientId,
                persianName: item.persianName,
                englishName: item.englishName,
                unit: item.unit,
                kind: item.kind,
                currentQuantity: item.currentQuantity,
                minAlertQuantity: item.minAlertQuantity,
                locationLabel: group.locationLabel,
              })
            }
          }
        }

        const alert = {
          count: lowItems.length,
          items: lowItems,
          totalIngredients: status.totalIngredients ?? 0,
        }

        setInventoryStatus(status)
        setLowStockAlert(alert)

        if (lastLowStockCount === null) {
          lastLowStockCount = alert.count
        } else if (alert.count > lastLowStockCount) {
          // Surface a system notification when new items drop below their threshold.
          if (typeof window !== 'undefined' && 'Notification' in window && Notification.permission === 'granted') {
            const names = lowItems.slice(0, 3).map((item) => item.persianName).join('، ')
            new Notification('افت موجودی ثبت شد', {
              body: `کیفیت هشدار: ${alert.count} قلم زیر حد آلارم${names ? ` — از جمله: ${names}` : ''}`,
              tag: 'low-stock',
            })
          }
          setMessage('هشدار انبار: اقلام جدیدی زیر حد آلارم قرار گرفتند.')
        }
        lastLowStockCount = alert.count
      } catch {
        // inventory alert polling is best-effort; ignore transient failures
      }
    }

    pollInventoryAlerts()
    const intervalId = window.setInterval(pollInventoryAlerts, 60_000)
    return () => {
      cancelled = true
      window.clearInterval(intervalId)
    }
  }, [session?.token])

  useEffect(() => {
    if (!session?.token || activeTab !== 'record-sale' || !saleForm.menuItemId || saleResult) {
      setSalePreview([])
      return undefined
    }

    let cancelled = false

    async function loadSalePreview() {
      setSalePreviewLoading(true)
      try {
        const recipe = normalizeRecipeResponse(
          await apiRequest(`/admin/recipes/${saleForm.menuItemId}`, {
            token: session.token,
          }),
        )

        if (cancelled) {
          return
        }

        const soldCount = Number(saleForm.quantitySold) || 1
        setSalePreview(
          recipe.lines.map((line) => ({
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
    navigate(`/${pathForTab(tabId)}`)
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

  async function updateOrderFulfillment(orderId, fulfillmentStatus) {
    if (!session?.token) {
      return
    }

    setSaving(true)
    setError('')
    setMessage('')

    try {
      const updated = await apiRequest(`/admin/orders/${orderId}/fulfillment`, {
        method: 'PATCH',
        token: session.token,
        body: { fulfillmentStatus },
      })

      if (selectedOrder?.id === orderId) {
        setSelectedOrder(updated)
      }

      const successMessage =
        fulfillmentStatus === 'Ready'
          ? 'سفارش آماده شد و پیامک به مشتری ارسال شد (در صورت پیکربندی سرویس پیامک).'
          : fulfillmentStatus === 'Preparing'
            ? 'سفارش وارد مرحله آماده‌سازی شد.'
            : fulfillmentStatus === 'Completed'
              ? 'سفارش به عنوان تحویل‌داده‌شده ثبت شد.'
              : 'وضعیت سفارش بروزرسانی شد.'

      setMessage(successMessage)
      bumpTableReload()
      lastOrderAlertPollRef.current = new Date().toISOString()
    } catch (requestError) {
      setError(getReadableError(requestError))
    } finally {
      setSaving(false)
    }
  }

  function dismissOrderAlert(orderId) {
    setDismissedOrderAlertIds((current) => new Set([...current, orderId]))
    setOrderAlerts((current) =>
      current
        ? {
            ...current,
            newPaidOrders: (current.newPaidOrders ?? []).filter((order) => order.id !== orderId),
          }
        : current,
    )
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
      unlockOrderAlertSound()

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
      const defaultPath = getDefaultRoutePath(nextSession, adminRoutes) ?? 'dashboard'
      navigate(`/${defaultPath}`, { replace: true })
      notifiedKitchenOrderIdsRef.current = null
      lastOrderAlertPollRef.current = null
      setDismissedOrderAlertIds(new Set())
      setOrderAlerts(null)

      if (typeof window !== 'undefined' && 'Notification' in window && Notification.permission === 'default') {
        Notification.requestPermission().catch(() => {})
      }
    } catch (requestError) {
      setError(getReadableError(requestError))
    } finally {
      setSaving(false)
    }
  }

  function handleLogout() {
    setSession(null)
    navigate('/', { replace: true })
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
    setOrderAlerts(null)
    setOrderAlertPulse(false)
    setDismissedOrderAlertIds(new Set())
    notifiedKitchenOrderIdsRef.current = null
    lastOrderAlertPollRef.current = null
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
      navigate('/category')
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
      navigate('/menu-items')
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
    navigate('/category')
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
    navigate('/menu-items')
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

  const consumptionColumns = useMemo(
    () =>
      buildInventoryConsumptionColumns({
        toPersianNumber,
        KIND_OPTIONS,
        LOCATION_OPTIONS,
      }),
    [],
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
    () =>
      buildOrderColumns({
        toPersianNumber,
        formatDateTime,
        openOrderEditor,
        updateOrderFulfillment,
        saving,
      }),
    [tableReloadToken, saving],
  )

  const value = {
    session,
    setSession,
    loginForm,
    setLoginForm,
    categoryOptions,
    menuItemOptions,
    ingredientOptions,
    purchaseExpiringOnly,
    setPurchaseExpiringOnly,
    tableReloadToken,
    dashboard,
    dashboardLoading,
    dashboardError,
    recipeState,
    setRecipeState,
    ingredientForm,
    setIngredientForm,
    inventoryStatus,
    setInventoryStatus,
    inventoryAction,
    setInventoryAction,
    restockForm,
    setRestockForm,
    adjustForm,
    setAdjustForm,
    alertForm,
    setAlertForm,
    stockHistory,
    setStockHistory,
    saleForm,
    setSaleForm,
    salePreview,
    salePreviewLoading,
    saleResult,
    setSaleResult,
    selectedOrder,
    orderEditForm,
    orderAlerts,
    lowStockAlert,
    orderAlertPulse,
    salonStatus,
    siteSettings,
    setSiteSettings,
    categoryForm,
    setCategoryForm,
    menuItemForm,
    setMenuItemForm,
    addonForm,
    setAddonForm,
    modifierForm,
    setModifierForm,
    salonTableForm,
    setSalonTableForm,
    occupyForm,
    setOccupyForm,
    isSalonTableModalOpen,
    setIsSalonTableModalOpen,
    isOccupyModalOpen,
    setIsOccupyModalOpen,
    loading,
    saving,
    setSaving,
    message,
    error,
    setMessage,
    setError,
    activeSalonTables,
    activeTab,
    loadSalonStatus,
    bumpTableReload,
    navigateToTab,
    loadDashboard,
    openOrderEditor,
    closeOrderEditor,
    updateOrderLineQuantity,
    toggleOrderLineRemoval,
    handleSaveOrder,
    updateOrderFulfillment,
    dismissOrderAlert,
    handleCancelOrder,
    loadAdminData,
    handleLogin,
    handleLogout,
    handleSalonTableSubmit,
    handleDeleteSalonTable,
    handleOccupyTable,
    handleReleaseTable,
    handleCategorySubmit,
    handleDeleteCategory,
    handleMenuItemSubmit,
    handleDeleteMenuItem,
    handleSettingsSubmit,
    editCategory,
    editMenuItem,
    editSalonTable,
    openCreateSalonTableModal,
    closeSalonTableModal,
    openOccupyTableModal,
    closeOccupyModal,
    resetCategoryForm,
    resetMenuItemForm,
    resetSalonTableForm,
    categoryColumns,
    menuItemColumns,
    addonColumns,
    modifierColumns,
    ingredientColumns,
    recipeColumns,
    inventoryColumns,
    stockHistoryColumns,
    consumptionColumns,
    salePreviewColumns,
    saleResultColumns,
    orderColumns,
    apiRequest,
    getReadableError,
    normalizeRecipeResponse,
    toPersianNumber,
    formatDateTime,
    formatDuration,
    splitTags,
    joinTags,
    splitList,
    joinList,
    optionalNumber,
    emptyRecipeLineForm,
    emptyAddonForm,
    emptyModifierForm,
    emptyIngredientForm,
    emptyRestockForm,
    emptyAdjustForm,
    setOrderEditForm,
  }

  return <AdminContext.Provider value={value}>{children}</AdminContext.Provider>
}
