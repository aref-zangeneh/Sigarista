import { createContext, useContext, useEffect, useMemo, useRef, useState } from 'react'
import { useLocation, useNavigate } from 'react-router-dom'
import {
  buildAddonColumns,
  buildCategoryColumns,
  buildDiscountColumns,
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
  getRepeatAlertSoundDelayMs,
  normalizeAlertSoundSettings,
  playOrderAlertSound,
  setOrderAlertSoundMuted,
  unlockOrderAlertSound,
} from '../utils/orderAlertSound'

const SOUND_OVERRIDE_STORAGE_KEY = 'sugarista.admin.orderAlertSoundOverride'
const SOUND_SYNC_STORAGE_KEY = 'sugarista.admin.orderAlertSoundSync'
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
  emptyDiscountForm,
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
  const [registerPaymentMethod, setRegisterPaymentMethod] = useState('Cash')
  const [orderEditForm, setOrderEditForm] = useState(null)
  const [orderAlerts, setOrderAlerts] = useState(null)
  const [busyAlertOrders, setBusyAlertOrders] = useState({})
  const [lowStockAlert, setLowStockAlert] = useState(null)
  const [orderAlertPulse, setOrderAlertPulse] = useState(false)
  const [dismissedOrderAlertIds, setDismissedOrderAlertIds] = useState(() => new Set())
  const notifiedKitchenOrderIdsRef = useRef(null)
  const lastOrderAlertPollRef = useRef(null)
  const orderAlertsRef = useRef(null)
  const busyAlertOrdersRef = useRef({})
  const pollOrderAlertsRef = useRef(null)
  const orderAlertPollSeqRef = useRef(0)
  const [salonStatus, setSalonStatus] = useState(emptySalonStatus)
  const [siteSettings, setSiteSettings] = useState(emptySettings)
  const [orderAlertSoundSaving, setOrderAlertSoundSaving] = useState(false)
  const orderAlertSoundSavingRef = useRef(false)
  const [localSoundOverride, setLocalSoundOverride] = useState(() => {
    try {
      const raw = window.localStorage.getItem(SOUND_OVERRIDE_STORAGE_KEY)
      return raw ? JSON.parse(raw) : null
    } catch {
      return null
    }
  })
  const [soundSettingsHydrated, setSoundSettingsHydrated] = useState(() => Boolean(localSoundOverride))
  const soundSettingsHydratedRef = useRef(Boolean(localSoundOverride))
  const localSoundOverrideRef = useRef(localSoundOverride)
  const orderAlertSoundSettingsRef = useRef(
    normalizeAlertSoundSettings(localSoundOverride ?? emptySettings),
  )
  const [tipOverrides, setTipOverrides] = useState({})
  const [categoryForm, setCategoryForm] = useState(emptyCategoryForm)
  const [menuItemForm, setMenuItemForm] = useState(emptyMenuItemForm)
  const [priceHistoryMenuItem, setPriceHistoryMenuItem] = useState(null)
  const [addonForm, setAddonForm] = useState(emptyAddonForm)
  const [discountForm, setDiscountForm] = useState(emptyDiscountForm)
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

  const orderAlertSoundSettings = useMemo(() => {
    if (localSoundOverride) {
      return normalizeAlertSoundSettings(localSoundOverride)
    }
    return normalizeAlertSoundSettings(siteSettings)
  }, [localSoundOverride, siteSettings.orderAlertSoundEnabled, siteSettings.orderAlertSoundIntervalSeconds])

  useEffect(() => {
    localSoundOverrideRef.current = localSoundOverride
  }, [localSoundOverride])

  useEffect(() => {
    orderAlertSoundSettingsRef.current = orderAlertSoundSettings
    setOrderAlertSoundMuted(!soundSettingsHydrated || !orderAlertSoundSettings.enabled)
  }, [orderAlertSoundSettings, soundSettingsHydrated])

  useEffect(() => {
    soundSettingsHydratedRef.current = soundSettingsHydrated
  }, [soundSettingsHydrated])

  useEffect(() => {
    const handleStorageSync = (event) => {
      if (event.key === SOUND_OVERRIDE_STORAGE_KEY) {
        try {
          const parsed = event.newValue ? JSON.parse(event.newValue) : null
          setLocalSoundOverride(parsed)
          if (parsed) {
            const normalized = normalizeAlertSoundSettings(parsed)
            orderAlertSoundSettingsRef.current = normalized
            soundSettingsHydratedRef.current = true
            setSoundSettingsHydrated(true)
            setOrderAlertSoundMuted(!normalized.enabled)
          }
        } catch {
          // ignore malformed storage values
        }
      } else if (event.key === SOUND_SYNC_STORAGE_KEY && event.newValue) {
        try {
          const parsed = JSON.parse(event.newValue)
          const normalized = normalizeAlertSoundSettings(parsed)
          setLocalSoundOverride(null)
          setSiteSettings((current) => ({
            ...current,
            orderAlertSoundEnabled: normalized.enabled,
            orderAlertSoundIntervalSeconds: normalized.intervalSeconds,
          }))
          orderAlertSoundSettingsRef.current = normalized
          soundSettingsHydratedRef.current = true
          setSoundSettingsHydrated(true)
          setOrderAlertSoundMuted(!normalized.enabled)
        } catch {
          // ignore malformed storage values
        }
      }
    }

    window.addEventListener('storage', handleStorageSync)
    return () => window.removeEventListener('storage', handleStorageSync)
  }, [])

  useEffect(() => {
    orderAlertsRef.current = orderAlerts
  }, [orderAlerts])

  useEffect(() => {
    if (!session?.token || !soundSettingsHydrated) {
      return undefined
    }

    const delayMs = getRepeatAlertSoundDelayMs(orderAlertSoundSettings)
    if (!delayMs) {
      return undefined
    }

    const soundIntervalId = window.setInterval(() => {
      if (
        soundSettingsHydratedRef.current &&
        orderAlertSoundSettingsRef.current.enabled &&
        countUnhandledKitchenOrders(orderAlertsRef.current) > 0
      ) {
        playOrderAlertSound()
      }
    }, delayMs)

    return () => window.clearInterval(soundIntervalId)
  }, [
    session?.token,
    soundSettingsHydrated,
    orderAlertSoundSettings.enabled,
    orderAlertSoundSettings.intervalSeconds,
  ])

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

  function markOrderAlertBusy(orderId, label = 'در حال پردازش...', { untilRemoved = false } = {}) {
    if (!orderId) {
      return
    }
    const key = String(orderId)
    setBusyAlertOrders((current) => {
      const next = { ...current, [key]: { label, untilRemoved } }
      busyAlertOrdersRef.current = next
      return next
    })
  }

  function clearOrderAlertBusy(orderId, { force = false } = {}) {
    if (!orderId) {
      return
    }
    const key = String(orderId)
    setBusyAlertOrders((current) => {
      const existing = current[key]
      if (!existing || (existing.untilRemoved && !force)) {
        return current
      }
      const next = { ...current }
      delete next[key]
      busyAlertOrdersRef.current = next
      return next
    })
  }

  async function refreshOrderAlerts() {
    if (pollOrderAlertsRef.current) {
      await pollOrderAlertsRef.current()
    }
  }

  useEffect(() => {
    if (!session?.token || !hasPermission(session, PERMISSIONS.orders.view)) {
      pollOrderAlertsRef.current = null
      return undefined
    }

    let cancelled = false

    async function pollOrderAlerts() {
      const seq = ++orderAlertPollSeqRef.current
      try {
        const since =
          lastOrderAlertPollRef.current ??
          new Date(Date.now() - 60_000).toISOString()

        const alerts = await apiRequest(
          `/admin/orders/alerts?since=${encodeURIComponent(since)}`,
          { token: session.token },
        )

        if (cancelled || seq < orderAlertPollSeqRef.current) {
          return
        }

        // Sync master sound settings directly from alerts response so every role,
        // tab, and POS screen stays aligned even without site-settings.view permission.
        if (
          !orderAlertSoundSavingRef.current &&
          typeof alerts.orderAlertSoundEnabled === 'boolean'
        ) {
          const serverSound = normalizeAlertSoundSettings({
            enabled: alerts.orderAlertSoundEnabled,
            intervalSeconds: alerts.orderAlertSoundIntervalSeconds,
          })
          setSiteSettings((current) => {
            if (
              current.orderAlertSoundEnabled === serverSound.enabled &&
              current.orderAlertSoundIntervalSeconds === serverSound.intervalSeconds
            ) {
              return current
            }
            return {
              ...current,
              orderAlertSoundEnabled: serverSound.enabled,
              orderAlertSoundIntervalSeconds: serverSound.intervalSeconds,
            }
          })
          if (!localSoundOverrideRef.current) {
            orderAlertSoundSettingsRef.current = serverSound
            setOrderAlertSoundMuted(!serverSound.enabled)
          }
          if (!soundSettingsHydratedRef.current) {
            soundSettingsHydratedRef.current = true
            setSoundSettingsHydrated(true)
          }
        }

        const kitchenOrders = alerts.activeKitchenOrders ?? []
        const visibleKitchenOrders = kitchenOrders.filter(
          (order) => !dismissedOrderAlertIds.has(order.id),
        )
        const visibleNewPaidOrders = (alerts.newPaidOrders ?? []).filter(
          (order) => !dismissedOrderAlertIds.has(order.id),
        )
        const readyOrders = alerts.readyForPickupOrders ?? []

        setOrderAlerts({
          ...alerts,
          activeKitchenOrders: visibleKitchenOrders,
          newPaidOrders: visibleNewPaidOrders,
        })

        // Release busy locks for orders that have now disappeared from the alerts bar
        const presentOrderIds = new Set([
          ...visibleKitchenOrders.map((order) => String(order.id)),
          ...readyOrders.map((order) => String(order.id)),
          ...visibleNewPaidOrders.map((order) => String(order.id)),
        ])
        setBusyAlertOrders((current) => {
          const keys = Object.keys(current)
          if (keys.length === 0) {
            return current
          }
          let changed = false
          const next = { ...current }
          for (const key of keys) {
            if (next[key]?.untilRemoved && !presentOrderIds.has(key)) {
              delete next[key]
              changed = true
            }
          }
          if (!changed) {
            return current
          }
          busyAlertOrdersRef.current = next
          return next
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

            const soundEnabledNow =
              soundSettingsHydratedRef.current && orderAlertSoundSettingsRef.current.enabled
            if (soundEnabledNow) {
              playOrderAlertSound()
            }
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
                  silent: !soundEnabledNow,
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

    pollOrderAlertsRef.current = pollOrderAlerts
    pollOrderAlerts()
    const intervalId = window.setInterval(pollOrderAlerts, 4000)
    return () => {
      cancelled = true
      if (pollOrderAlertsRef.current === pollOrderAlerts) {
        pollOrderAlertsRef.current = null
      }
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
    if (!session?.token || !orderRow?.id) {
      return
    }

    const orderKey = String(orderRow.id)
    if (busyAlertOrdersRef.current[orderKey]) {
      return
    }

    markOrderAlertBusy(orderKey, 'در حال باز کردن...')
    try {
      const order = await apiRequest(`/admin/orders/${orderRow.id}`, { token: session.token })
      setSelectedOrder(order)
      setOrderEditForm({
        customerName: order.customerName,
        customerPhone: order.customerPhone,
        note: order.note ?? '',
        managerNote: order.managerNote ?? '',
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
          optionLabels: line.optionLabels ?? [],
          customDrinkSummary: line.customDrinkSummary ?? [],
          customDrink: line.customDrink ?? null,
          remove: false,
        })),
      })
    } catch (requestError) {
      setError(getReadableError(requestError))
    } finally {
      clearOrderAlertBusy(orderKey)
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

    const orderId = selectedOrder.id
    setSaving(true)
    setError('')
    setMessage('')
    markOrderAlertBusy(orderId, 'در حال ذخیره...')

    try {
      await apiRequest(`/admin/orders/${orderId}`, {
        method: 'PUT',
        token: session.token,
        body: {
          customerName: orderEditForm.customerName,
          customerPhone: orderEditForm.customerPhone,
          note: orderEditForm.note,
          ...(hasPermission(session, PERMISSIONS.pos.managerNote)
            ? { managerNote: orderEditForm.managerNote ?? '' }
            : {}),
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
      await Promise.allSettled([refreshOrderAlerts(), loadAdminData()])
    } catch (requestError) {
      setError(getReadableError(requestError))
    } finally {
      clearOrderAlertBusy(orderId)
      setSaving(false)
    }
  }

  async function handleRegisterPayment({ orderKind = 'Standard', complimentaryReason = '' } = {}) {
    if (!session?.token || !selectedOrder) {
      return
    }

    const orderId = selectedOrder.id
    const isNonRevenue = orderKind === 'InternalUse' || orderKind === 'Complimentary'
    setSaving(true)
    setError('')
    setMessage('')
    markOrderAlertBusy(orderId, isNonRevenue ? 'در حال ثبت رایگان...' : 'در حال ثبت پرداخت...')

    try {
      await apiRequest(`/admin/orders/${orderId}/pay`, {
        method: 'POST',
        token: session.token,
        body: {
          paymentMethod: isNonRevenue ? 'Complimentary' : registerPaymentMethod,
          orderKind,
          complimentaryReason: isNonRevenue ? complimentaryReason.trim() || null : null,
        },
      })

      setMessage(
        orderKind === 'InternalUse'
          ? 'سفارش به عنوان مصرف داخلی (رایگان) ثبت و موجودی کسر شد.'
          : orderKind === 'Complimentary'
            ? 'سفارش به عنوان مهمان / رایگان ثبت و موجودی کسر شد.'
            : 'پرداخت ثبت و فاکتور نهایی شد؛ مالی و انبار همزمان به‌روزرسانی شدند.',
      )
      closeOrderEditor()
      setRegisterPaymentMethod('Cash')
      bumpTableReload()
      await Promise.allSettled([refreshOrderAlerts(), loadAdminData()])
    } catch (requestError) {
      setError(getReadableError(requestError))
    } finally {
      clearOrderAlertBusy(orderId)
      setSaving(false)
    }
  }

  async function updateOrderFulfillment(orderId, fulfillmentStatus) {
    if (!session?.token) {
      return
    }

    const removesFromAlerts = fulfillmentStatus === 'Completed'
    setSaving(true)
    setError('')
    setMessage('')
    markOrderAlertBusy(
      orderId,
      removesFromAlerts ? 'در حال ثبت تحویل...' : 'در حال بروزرسانی...',
      { untilRemoved: removesFromAlerts },
    )

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

      if (removesFromAlerts) {
        markOrderAlertBusy(orderId, 'تحویل شد — در حال حذف...', { untilRemoved: true })
      }

      setMessage(successMessage)
      bumpTableReload()
      lastOrderAlertPollRef.current = new Date().toISOString()
      await refreshOrderAlerts()
      if (!removesFromAlerts || !pollOrderAlertsRef.current) {
        clearOrderAlertBusy(orderId, { force: !pollOrderAlertsRef.current })
      }
    } catch (requestError) {
      clearOrderAlertBusy(orderId, { force: true })
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

    const orderId = selectedOrder.id
    setSaving(true)
    setError('')
    setMessage('')
    markOrderAlertBusy(orderId, 'در حال لغو...', { untilRemoved: true })

    try {
      await apiRequest(`/admin/orders/${orderId}/cancel`, {
        method: 'POST',
        token: session.token,
      })

      markOrderAlertBusy(orderId, 'لغو شد — در حال حذف...', { untilRemoved: true })
      setMessage('سفارش لغو شد و موجودی بازگردانده شد.')
      closeOrderEditor()
      bumpTableReload()
      await Promise.allSettled([refreshOrderAlerts(), loadAdminData()])
      if (!pollOrderAlertsRef.current) {
        clearOrderAlertBusy(orderId, { force: true })
      }
    } catch (requestError) {
      clearOrderAlertBusy(orderId, { force: true })
      setError(getReadableError(requestError))
    } finally {
      setSaving(false)
    }
  }

  async function handleDeleteOrder(orderOrId, { restoreInventory = true } = {}) {
    const orderId = typeof orderOrId === 'string' ? orderOrId : (orderOrId?.id ?? selectedOrder?.id)
    if (!session?.token || !orderId) {
      return
    }

    const confirmText = restoreInventory
      ? 'آیا از حذف کامل این سفارش مطمئن هستید؟ سند حسابداری مربوطه حذف و موجودی انبار بازگردانده می‌شود. این عملیات قابل بازگشت نیست.'
      : 'آیا از حذف کامل این سفارش (بدون بازگردانی موجودی انبار) مطمئن هستید؟ سند حسابداری مربوطه نیز حذف می‌شود. این عملیات قابل بازگشت نیست.'
    if (!window.confirm(confirmText)) {
      return
    }

    setSaving(true)
    setError('')
    setMessage('')
    markOrderAlertBusy(orderId, 'در حال حذف...', { untilRemoved: true })

    try {
      await apiRequest(`/admin/orders/${orderId}?restoreInventory=${restoreInventory ? 'true' : 'false'}`, {
        method: 'DELETE',
        token: session.token,
      })

      markOrderAlertBusy(orderId, 'حذف شد — در حال پاک‌سازی...', { untilRemoved: true })
      setMessage(
        restoreInventory
          ? 'سفارش به‌طور کامل حذف شد؛ سند حسابداری پاک و موجودی انبار بازگردانده شد.'
          : 'سفارش و سند حسابداری مربوطه به‌طور کامل حذف شدند (موجودی انبار تغییر نکرد).',
      )
      if (selectedOrder?.id === orderId) {
        closeOrderEditor()
      }
      bumpTableReload()
      await Promise.allSettled([refreshOrderAlerts(), loadAdminData()])
      if (!pollOrderAlertsRef.current) {
        clearOrderAlertBusy(orderId, { force: true })
      }
    } catch (requestError) {
      clearOrderAlertBusy(orderId, { force: true })
      setError(getReadableError(requestError))
    } finally {
      setSaving(false)
    }
  }

  async function handleToggleOrderAccountingExclusion(orderOrId, exclude, reason = '') {
    const orderId = typeof orderOrId === 'string' ? orderOrId : (orderOrId?.id ?? selectedOrder?.id)
    if (!session?.token || !orderId) {
      return
    }

    setSaving(true)
    setError('')
    setMessage('')

    try {
      const updated = await apiRequest(`/admin/orders/${orderId}/accounting-exclusion`, {
        method: 'PATCH',
        token: session.token,
        body: {
          exclude: Boolean(exclude),
          reason: exclude ? (reason?.trim() || null) : null,
        },
      })

      if (selectedOrder?.id === orderId) {
        setSelectedOrder(updated)
      }
      setMessage(
        exclude
          ? 'سفارش از حسابداری و گزارش‌های مالی خارج شد و سند حسابداری آن حذف گردید.'
          : 'سفارش دوباره به حسابداری و گزارش‌های مالی بازگردانده شد.',
      )
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

    // Tooltip overrides never block (or scare) the admin on failure — worst
    // case the help bubbles fall back to their registry default texts.
    fetchTooltipOverrides(token)

    try {
      const results = await Promise.allSettled(requests.map((request) => request.promise))
      const data = {}
      const failures = []

      // 401 failures are a session expiry, not a data problem: log the user
      // out (and let the login page explain itself) instead of listing the
      // keys — otherwise the login screen shows a scary «بخش‌ها ناموفق بود».
      let authExpired = false
      results.forEach((result, index) => {
        const { key } = requests[index]
        if (result.status === 'fulfilled') {
          data[key] = result.value
          return
        }

        if (result.reason?.message?.includes('401')) {
          authExpired = true
          setSession(null)
          return
        }

        failures.push(key)
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
        const serverSound = normalizeAlertSoundSettings(data.siteSettings)
        if (!localSoundOverrideRef.current) {
          orderAlertSoundSettingsRef.current = serverSound
          setOrderAlertSoundMuted(!serverSound.enabled)
        }
        soundSettingsHydratedRef.current = true
        setSoundSettingsHydrated(true)
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
      } else if (authExpired) {
        setError('نشست شما منقضی شده است؛ دوباره وارد شوید.')
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

  function fetchTooltipOverrides(token = session?.token) {
    if (!token) {
      return
    }

    apiRequest('/admin/tooltips/active', { token })
      .then((map) => setTipOverrides(map ?? {}))
      .catch(() => {})
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
      setBusyAlertOrders({})
      busyAlertOrdersRef.current = {}

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
    // best-effort: record the «خروج» event in the activity log, then drop the token
    if (session?.token) {
      fetch(`/api/auth/logout`, {
        method: 'POST',
        headers: { Authorization: `Bearer ${session.token}` },
        keepalive: true,
      }).catch(() => {})
    }

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
    setDiscountForm(emptyDiscountForm)
    setModifierForm(emptyModifierForm)
    setSalonTableForm(emptySalonTableForm)
    setOccupyForm(emptyOccupyForm)
    setIsSalonTableModalOpen(false)
    setIsOccupyModalOpen(false)
    setOrderAlerts(null)
    setBusyAlertOrders({})
    busyAlertOrdersRef.current = {}
    setOrderAlertPulse(false)
    setDismissedOrderAlertIds(new Set())
    setTipOverrides({})
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
          imageUrl: categoryForm.imageUrl || null,
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

  // Keeps the item's recipe (محتویات) in sync with the rows in the menu-item
  // form: upserts every chosen ingredient, deletes the ones removed from the
  // form. Same data as the "دستور پخت" page — one source of truth.
  async function syncMenuItemContents(menuItemId, desiredLines) {
    const desired = (desiredLines ?? []).filter((line) => line.ingredientId)

    const current = normalizeRecipeResponse(
      await apiRequest(`/admin/recipes/${menuItemId}`, { token: session.token }),
    ).lines

    const desiredIngredientIds = new Set(desired.map((line) => line.ingredientId))
    for (const line of current) {
      if (!desiredIngredientIds.has(line.ingredientId)) {
        await apiRequest(`/admin/recipes/${line.id}`, { method: 'DELETE', token: session.token })
      }
    }

    for (const line of desired) {
      await apiRequest(`/admin/recipes/${menuItemId}`, {
        method: 'POST',
        token: session.token,
        body: {
          menuItemId,
          ingredientId: line.ingredientId,
          quantity: Number(line.quantity) > 0 ? Number(line.quantity) : 1,
          showToCustomer: line.showToCustomer !== false,
        },
      })
    }
  }

  async function handleMenuItemSubmit(event) {
    event.preventDefault()
    setError('')
    setMessage('')

    if (!menuItemForm.persianName?.trim()) {
      setError('نام فارسی آیتم را وارد کنید.')
      return
    }

    if (!menuItemForm.categoryId) {
      setError('دسته‌بندی را انتخاب کنید.')
      return
    }

    setSaving(true)

    try {
      const path = menuItemForm.id
        ? `/admin/menu-items/${menuItemForm.id}`
        : '/admin/menu-items'

      const savedItemId = await apiRequest(path, {
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
          smallName: menuItemForm.smallName?.trim() || null,
          mediumPrice: optionalNumber(menuItemForm.mediumPrice),
          mediumSpecialOfferPrice: optionalNumber(menuItemForm.mediumSpecialOfferPrice),
          mediumVolume: menuItemForm.mediumVolume.trim() || null,
          mediumName: menuItemForm.mediumName?.trim() || null,
          largePrice: optionalNumber(menuItemForm.largePrice),
          largeSpecialOfferPrice: optionalNumber(menuItemForm.largeSpecialOfferPrice),
          largeVolume: menuItemForm.largeVolume.trim() || null,
          largeName: menuItemForm.largeName?.trim() || null,
          tags: splitTags(menuItemForm.tags),
          badge: menuItemForm.badge.trim() || null,
          featured: menuItemForm.featured,
          accent: menuItemForm.accent,
          shortLabel: menuItemForm.shortLabel,
          displayOrder: Number(menuItemForm.displayOrder),
          isActive: menuItemForm.isActive,
          price: optionalNumber(menuItemForm.price),
          purchasePrice: optionalNumber(menuItemForm.purchasePrice),
          trackInventory: Boolean(menuItemForm.trackInventory),
          stockQuantity: menuItemForm.trackInventory
            ? (optionalNumber(menuItemForm.stockQuantity) ?? 0)
            : null,
          minStockAlert: menuItemForm.trackInventory
            ? (optionalNumber(menuItemForm.minStockAlert) ?? 0)
            : null,
          priceChangeNote: menuItemForm.priceChangeNote?.trim() || null,
        },
      })

      await syncMenuItemContents(savedItemId, menuItemForm.recipeLines)

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

  async function handleDuplicateMenuItem(item) {
    if (!item?.id) return
    setSaving(true)
    setError('')
    setMessage('')

    try {
      const result = await apiRequest(`/admin/menu-items/${item.id}/duplicate`, {
        method: 'POST',
        token: session.token,
      })

      bumpTableReload()
      await loadAdminData()

      if (result?.item) {
        editMenuItem(result.item)
        window.scrollTo({ top: 0, behavior: 'smooth' })
      }

      const details = []
      if (result?.copiedOptionGroupCount > 0) {
        details.push(`${toPersianNumber(result.copiedOptionGroupCount)} گروه آپشن`)
      }
      if (result?.copiedRecipeLineCount > 0) {
        details.push(`${toPersianNumber(result.copiedRecipeLineCount)} قلم دستور پخت`)
      }
      if (result?.copiedBundleItemCount > 0) {
        details.push(`${toPersianNumber(result.copiedBundleItemCount)} آیتم باندل`)
      }

      const suffix = details.length > 0 ? ` (${details.join(' و ')} کپی شد)` : ''
      setMessage(`کپی «${result?.item?.persianName ?? item.persianName}» ایجاد شد${suffix} و آماده ویرایش است.`)
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

  async function updateOrderAlertSoundSettings(patch) {
    const next = normalizeAlertSoundSettings({
      ...orderAlertSoundSettingsRef.current,
      ...patch,
    })

    // Synchronous audio kill-switch + ref update so no in-flight timer/poll can beep
    orderAlertSoundSettingsRef.current = next
    soundSettingsHydratedRef.current = true
    setSoundSettingsHydrated(true)
    setOrderAlertSoundMuted(!next.enabled)

    // Instant optimistic update so sound/mute applies immediately in UI
    setSiteSettings((current) => ({
      ...current,
      orderAlertSoundEnabled: next.enabled,
      orderAlertSoundIntervalSeconds: next.intervalSeconds,
    }))

    const canPersistToServer =
      session?.token &&
      (hasPermission(session, PERMISSIONS['site-settings']?.update) ||
        hasPermission(session, PERMISSIONS.orders?.update))

    if (!canPersistToServer) {
      localSoundOverrideRef.current = next
      setLocalSoundOverride(next)
      try {
        window.localStorage.setItem(SOUND_OVERRIDE_STORAGE_KEY, JSON.stringify(next))
      } catch {
        // ignore storage quota errors
      }
      return
    }

    localSoundOverrideRef.current = null
    setLocalSoundOverride(null)
    try {
      window.localStorage.removeItem(SOUND_OVERRIDE_STORAGE_KEY)
    } catch {
      // ignore
    }

    orderAlertSoundSavingRef.current = true
    setOrderAlertSoundSaving(true)
    setError('')
    try {
      const saved = await apiRequest('/admin/site-settings/order-alert-sound', {
        method: 'PATCH',
        token: session.token,
        body: {
          enabled: next.enabled,
          intervalSeconds: next.intervalSeconds,
        },
      })
      const resolved = normalizeAlertSoundSettings({
        enabled: saved?.enabled ?? next.enabled,
        intervalSeconds: saved?.intervalSeconds ?? next.intervalSeconds,
      })
      orderAlertSoundSettingsRef.current = resolved
      setOrderAlertSoundMuted(!resolved.enabled)
      setSiteSettings((current) => ({
        ...current,
        orderAlertSoundEnabled: resolved.enabled,
        orderAlertSoundIntervalSeconds: resolved.intervalSeconds,
      }))
      try {
        window.localStorage.setItem(
          SOUND_SYNC_STORAGE_KEY,
          JSON.stringify({ ...resolved, updatedAt: Date.now() }),
        )
      } catch {
        // ignore storage quota errors
      }
    } catch (requestError) {
      // Fall back to local browser override so user's mute choice still works immediately
      localSoundOverrideRef.current = next
      setLocalSoundOverride(next)
      try {
        window.localStorage.setItem(SOUND_OVERRIDE_STORAGE_KEY, JSON.stringify(next))
      } catch {
        // ignore
      }
      setError(getReadableError(requestError))
    } finally {
      orderAlertSoundSavingRef.current = false
      setOrderAlertSoundSaving(false)
    }
  }

  async function handleSettingsSubmit(event) {
    event.preventDefault()
    setSaving(true)
    setError('')
    setMessage('')

    try {
      const normalizedSound = normalizeAlertSoundSettings(siteSettings)
      await apiRequest('/admin/site-settings', {
        method: 'PUT',
        token: session.token,
        body: {
          ...siteSettings,
          orderAlertSoundEnabled: normalizedSound.enabled,
          orderAlertSoundIntervalSeconds: normalizedSound.intervalSeconds,
        },
      })

      localSoundOverrideRef.current = null
      setLocalSoundOverride(null)
      orderAlertSoundSettingsRef.current = normalizedSound
      setOrderAlertSoundMuted(!normalizedSound.enabled)
      try {
        window.localStorage.removeItem(SOUND_OVERRIDE_STORAGE_KEY)
        window.localStorage.setItem(
          SOUND_SYNC_STORAGE_KEY,
          JSON.stringify({ ...normalizedSound, updatedAt: Date.now() }),
        )
      } catch {
        // ignore
      }
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
      imageUrl: category.imageUrl ?? '',
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
      price: (item.smallPrice || item.mediumPrice || item.largePrice) ? '' : (item.price ?? ''),
      smallPrice: item.smallPrice ?? '',
      smallSpecialOfferPrice: item.smallSpecialOfferPrice ?? '',
      smallVolume: item.smallVolume ?? '',
      smallName: item.smallName ?? '',
      mediumPrice: item.mediumPrice ?? '',
      mediumSpecialOfferPrice: item.mediumSpecialOfferPrice ?? '',
      mediumVolume: item.mediumVolume ?? '',
      mediumName: item.mediumName ?? '',
      largePrice: item.largePrice ?? '',
      largeSpecialOfferPrice: item.largeSpecialOfferPrice ?? '',
      largeVolume: item.largeVolume ?? '',
      largeName: item.largeName ?? '',
      tags: joinTags(item.tags),
      badge: item.badge ?? '',
      featured: item.featured,
      accent: item.accent,
      shortLabel: item.shortLabel,
      displayOrder: item.displayOrder,
      isActive: item.isActive,
      purchasePrice: item.purchasePrice ?? '',
      trackInventory: Boolean(item.trackInventory),
      stockQuantity: item.stockQuantity ?? '',
      minStockAlert: item.minStockAlert ?? '',
      priceChangeNote: '',
      recipeLines: [],
    })
    navigate('/menu-items')

    // Load the item's contents (recipe) into the form in the background —
    // rows the admin then edits are synced back on save.
    apiRequest(`/admin/recipes/${item.id}`, { token: session.token })
      .then((payload) => normalizeRecipeResponse(payload))
      .then((recipe) => {
        setMenuItemForm((currentForm) => {
          if (currentForm.id !== item.id) {
            return currentForm
          }
          return {
            ...currentForm,
            recipeLines: recipe.lines.map((line) => ({
              ingredientId: line.ingredientId,
              quantity: String(line.quantity ?? ''),
              showToCustomer: line.showToCustomer !== false,
              ingredientPersianName: line.ingredientPersianName,
              unit: line.unit,
            })),
          }
        })
      })
      .catch(() => {
        // Contents stay empty when they cannot be loaded; the admin can still
        // edit everything else and save without touching them.
      })
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

  const openMenuItemPriceHistory = (item) => {
    if (!item?.id) return
    setPriceHistoryMenuItem({
      id: item.id,
      persianName: item.persianName ?? item.name ?? '',
      englishName: item.englishName ?? '',
    })
  }

  const closeMenuItemPriceHistory = () => setPriceHistoryMenuItem(null)

  const menuItemColumns = useMemo(
    () =>
      buildMenuItemColumns({
        toPersianNumber,
        joinTags,
        editMenuItem,
        handleDuplicateMenuItem,
        openMenuItemPriceHistory,
        handleDeleteMenuItem,
      }),
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

  const discountColumns = useMemo(
    () =>
      buildDiscountColumns({
        setDiscountForm,
        emptyDiscountForm,
        bumpTableReload,
        discountForm,
        session,
        apiRequest,
        setSaving,
        setError,
        setMessage,
        getReadableError,
      }),
    [discountForm, session, tableReloadToken],
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
        recipeState,
      }),
    [recipeState, session],
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
        busyAlertOrders,
      }),
    [tableReloadToken, saving, busyAlertOrders],
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
    busyAlertOrders,
    orderAlertSoundSettings,
    orderAlertSoundSaving,
    updateOrderAlertSoundSettings,
    markOrderAlertBusy,
    clearOrderAlertBusy,
    refreshOrderAlerts,
    lowStockAlert,
    orderAlertPulse,
    salonStatus,
    siteSettings,
    setSiteSettings,
    tipOverrides,
    reloadTipOverrides: fetchTooltipOverrides,
    categoryForm,
    setCategoryForm,
    menuItemForm,
    setMenuItemForm,
    addonForm,
    setAddonForm,
    discountForm,
    setDiscountForm,
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
    handleRegisterPayment,
    registerPaymentMethod,
    setRegisterPaymentMethod,
    updateOrderFulfillment,
    dismissOrderAlert,
    handleCancelOrder,
    handleDeleteOrder,
    handleToggleOrderAccountingExclusion,
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
    handleDuplicateMenuItem,
    handleDeleteMenuItem,
    priceHistoryMenuItem,
    openMenuItemPriceHistory,
    closeMenuItemPriceHistory,
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
    discountColumns,
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
    emptyDiscountForm,
    emptyModifierForm,
    emptyIngredientForm,
    emptyRestockForm,
    emptyAdjustForm,
    setOrderEditForm,
  }

  return <AdminContext.Provider value={value}>{children}</AdminContext.Provider>
}
