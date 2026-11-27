import { useCallback, useEffect, useMemo, useRef, useState } from 'react'
import { useAdmin } from '../context/AdminContext'
import { PaginatedAdminTable } from '../components/PaginatedAdminTable'
import { ShamsiDatePicker } from '../components/ShamsiDatePicker.jsx'
import { formatDateTime, toPersianNumber } from '../utils/adminFormatters'
import { hasPermission, PERMISSIONS } from '../utils/adminPermissions.js'
import { fulfillmentStatusLabel } from '../components/OrderAlertsBar'
import {
  EMPTY_ORDER_FILTERS,
  buildOrdersApiPath,
  countActiveOrderFilters,
  getOrderDatePresetRange,
} from '../utils/orderFiltersHelpers.js'
import './OrdersPage.css'

const QUEUE_TABS = [
  { id: 'pending', label: 'در انتظار پرداخت' },
  { id: 'processing', label: 'در حال پردازش' },
  { id: 'ready', label: 'آماده تحویل' },
]

const REFRESH_INTERVAL_MS = 20000

export function OrdersPage() {
  const {
    session,
    tableReloadToken,
    bumpTableReload,
    orderColumns,
    apiRequest,
    getReadableError,
    openOrderEditor,
    markOrderAlertBusy,
    clearOrderAlertBusy,
    refreshOrderAlerts,
  } = useAdmin()

  const [queue, setQueue] = useState({ pending: [], processing: [], ready: [] })
  const [queueTab, setQueueTab] = useState('pending')
  const [activeTabId, setActiveTabId] = useState('pending')
  const [queueLoading, setQueueLoading] = useState(true)
  const [queueError, setQueueError] = useState('')
  const [message, setMessage] = useState('')
  const [actingOrderId, setActingOrderId] = useState(null)
  const [filters, setFilters] = useState(EMPTY_ORDER_FILTERS)
  const [datePreset, setDatePreset] = useState('all')
  const [showAdvancedFilters, setShowAdvancedFilters] = useState(false)
  const [deliveryMethods, setDeliveryMethods] = useState([])
  const refreshTimerRef = useRef(null)

  const canViewManagerNote = hasPermission(session, PERMISSIONS.pos?.managerNote)
  const activeFilterCount = useMemo(() => countActiveOrderFilters(filters), [filters])
  const ordersApiPath = useMemo(() => buildOrdersApiPath(filters), [filters])

  const updateFilter = (key, value) => {
    setFilters((prev) => ({ ...prev, [key]: value }))
    if (key === 'fromDate' || key === 'toDate') {
      setDatePreset('custom')
    }
  }

  const applyDatePreset = (preset) => {
    setDatePreset(preset)
    const range = getOrderDatePresetRange(preset)
    setFilters((prev) => ({ ...prev, ...range }))
  }

  const resetAllFilters = () => {
    setDatePreset('all')
    setFilters(EMPTY_ORDER_FILTERS)
  }

  useEffect(() => {
    if (!session?.token) return
    apiRequest('/admin/delivery-methods/options', { token: session.token })
      .then((list) => setDeliveryMethods(Array.isArray(list) ? list : []))
      .catch(() => {})
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [session?.token])

  const loadQueue = useCallback(async () => {
    if (!session?.token) return
    try {
      const data = await apiRequest('/admin/orders?page=1&pageSize=50', { token: session.token })
      const rows = Array.isArray(data?.items) ? data.items : []
      const next = { pending: [], processing: [], ready: [] }
      rows.forEach((order) => {
        if (order.status === 'PendingPayment' || order.status === 'PartiallyPaid') {
          next.pending.push(order)
        } else if (order.status === 'Paid') {
          if (order.fulfillmentStatus === 'Registered' || order.fulfillmentStatus === 'Preparing') {
            next.processing.push(order)
          } else if (order.fulfillmentStatus === 'Ready') {
            next.ready.push(order)
          }
        }
      })
      setQueue(next)
      setQueueError('')

      const counts = { pending: next.pending.length, processing: next.processing.length, ready: next.ready.length }
      setActiveTabId((current) => (counts[current] > 0 ? current : Object.keys(counts).find((id) => counts[id] > 0) ?? current))
    } catch (requestError) {
      setQueueError(getReadableError(requestError))
    } finally {
      setQueueLoading(false)
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [session?.token])

  useEffect(() => {
    loadQueue()
    refreshTimerRef.current = setInterval(loadQueue, REFRESH_INTERVAL_MS)
    return () => clearInterval(refreshTimerRef.current)
  }, [loadQueue])

  useEffect(() => {
    loadQueue()
  }, [tableReloadToken, loadQueue])

  async function runOrderAction(order, action) {
    setActingOrderId(order.id)
    setQueueError('')
    setMessage('')
    const removesFromAlerts = action === 'complete'
    if (action !== 'edit') {
      markOrderAlertBusy(
        order.id,
        removesFromAlerts ? 'در حال ثبت تحویل...' : 'در حال بروزرسانی...',
        { untilRemoved: removesFromAlerts },
      )
    }
    try {
      if (action === 'pay') {
        await apiRequest(`/admin/orders/${order.id}/pay`, {
          method: 'POST',
          token: session.token,
          body: { paymentMethod: 'Cash' },
        })
        setMessage(`پرداخت سفارش ${order.trackingCode} ثبت شد؛ مالی و انبار همزمان به‌روزرسانی شدند.`)
      } else if (action === 'edit') {
        openOrderEditor(order)
      } else {
        const statusByAction = {
          preparing: 'Preparing',
          ready: 'Ready',
          complete: 'Completed',
        }
        await apiRequest(`/admin/orders/${order.id}/fulfillment`, {
          method: 'PATCH',
          token: session.token,
          body: { fulfillmentStatus: statusByAction[action] },
        })
        if (removesFromAlerts) {
          markOrderAlertBusy(order.id, 'تحویل شد — در حال حذف...', { untilRemoved: true })
        }
        setMessage(`وضعیت سفارش ${order.trackingCode} به «${fulfillmentStatusLabel(statusByAction[action])}» تغییر کرد.`)
      }
      await Promise.allSettled([loadQueue(), refreshOrderAlerts()])
      if (!removesFromAlerts) {
        clearOrderAlertBusy(order.id)
      }
      bumpTableReload()
    } catch (requestError) {
      clearOrderAlertBusy(order.id, { force: true })
      setQueueError(getReadableError(requestError))
    } finally {
      setActingOrderId(null)
    }
  }

  const activeOrderList = queue[queueTab] ?? []

  return (
    <section className="panel-stack">
      <section className="panel online-order-queue">
        <div className="panel-head">
          <div>
            <h3>صف پردازش زنده (سفارش‌های آنلاین)</h3>
            <p>
              بررسی و پیشبرد سفارش‌ها پیش از ورود به فاکتور نهایی — بروزرسانی خودکار هر ۲۰ ثانیه.
            </p>
          </div>
          <button type="button" className="ghost-button" onClick={loadQueue}>
            بروزرسانی صفحه
          </button>
        </div>

        <div className="queue-tabs" role="tablist">
          {QUEUE_TABS.map((tab) => (
            <button
              key={tab.id}
              type="button"
              role="tab"
              aria-selected={queueTab === tab.id}
              className={queueTab === tab.id ? 'queue-tab queue-tab--active' : 'queue-tab'}
              onClick={() => {
                setQueueTab(tab.id)
                setActiveTabId(tab.id)
              }}
            >
              {tab.label}
              <span className="queue-tab-count">{toPersianNumber((queue[tab.id] ?? []).length)}</span>
            </button>
          ))}
        </div>

        {queueError ? <p className="feedback feedback-error">{queueError}</p> : null}
        {message ? <p className="feedback feedback-success">{message}</p> : null}
        {queueLoading ? <p>در حال دریافت صف سفارش‌ها...</p> : null}

        {!queueLoading && activeOrderList.length === 0 ? (
          <p>
            {queueTab === 'pending'
              ? 'سفارش آنلاین در انتظار پرداختی نیست.'
              : queueTab === 'processing'
                ? 'سفارشی در حال پردازش نیست.'
                : 'سفارش آماده‌ای در انتظار تحویل نیست.'}
          </p>
        ) : null}

        {activeOrderList.length > 0 ? (
          <table className="admin-table">
            <thead>
              <tr>
                <th>ردیف</th>
                <th>کد پیگیری</th>
                <th>مشتری</th>
                <th>مبلغ / مانده</th>
                <th>اقلام</th>
                <th>وضعیت پرداخت</th>
                <th>آماده‌سازی</th>
                <th>ثبت شده</th>
                <th>عملیات</th>
              </tr>
            </thead>
            <tbody>
              {activeOrderList.map((order, rowIndex) => (
                <tr key={order.id}>
                  <td>{toPersianNumber(rowIndex + 1)}</td>
                  <td>
                    {order.trackingCode}
                    {order.orderKind === 'InternalUse' ? <div className="table-subtext">مصرف داخلی</div> : null}
                    {order.orderKind === 'Complimentary' ? <div className="table-subtext">مهمان / رایگان</div> : null}
                    {order.isExcludedFromAccounting ? <div className="table-subtext">خارج از حسابداری</div> : null}
                  </td>
                  <td>
                    {order.customerName}
                    <div className="table-subtext ltr-text">{toPersianNumber(order.customerPhone)}</div>
                  </td>
                  <td>
                    {toPersianNumber(order.totalAmount)} تومان
                    {order.status === 'PartiallyPaid' ? (
                      <div className="table-subtext">
                        مانده: {toPersianNumber(order.totalAmount - order.amountPaid)} تومان
                      </div>
                    ) : null}
                  </td>
                  <td>{toPersianNumber(order.lineCount)}</td>
                  <td>{order.status === 'PendingPayment' ? 'در انتظار پرداخت' : 'پرداخت جزئی'}</td>
                  <td>{fulfillmentStatusLabel(order.fulfillmentStatus)}</td>
                  <td>{formatDateTime(order.createdAt)}</td>
                  <td className="table-actions">
                    {order.status === 'PendingPayment' || order.status === 'PartiallyPaid' ? (
                      <>
                        <button
                          type="button"
                          className="ghost-button"
                          disabled={actingOrderId === order.id}
                          onClick={() => openOrderEditor(order)}
                        >
                          بررسی فاکتور
                        </button>
                        <button
                          type="button"
                          className="ghost-button"
                          disabled={actingOrderId === order.id}
                          onClick={() => runOrderAction(order, 'pay')}
                        >
                          ثبت پرداخت
                        </button>
                      </>
                    ) : null}
                    {order.fulfillmentStatus === 'Registered' ? (
                      <button
                        type="button"
                        className="ghost-button"
                        disabled={actingOrderId === order.id}
                        onClick={() => runOrderAction(order, 'preparing')}
                      >
                        شروع آماده‌سازی
                      </button>
                    ) : null}
                    {order.fulfillmentStatus === 'Preparing' ? (
                      <button
                        type="button"
                        className="ghost-button"
                        disabled={actingOrderId === order.id}
                        onClick={() => runOrderAction(order, 'ready')}
                      >
                        آماده شد
                      </button>
                    ) : null}
                    {order.fulfillmentStatus === 'Ready' ? (
                      <button
                        type="button"
                        className="ghost-button"
                        disabled={actingOrderId === order.id}
                        onClick={() => runOrderAction(order, 'complete')}
                      >
                        تحویل شد
                      </button>
                    ) : null}
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        ) : null}
      </section>

      <section className="panel">
        <div className="panel-head">
          <div>
            <h3>همه سفارش‌ها</h3>
            <p>جستجو و فیلتر جامع بر اساس مشتری، آیتم سفارش، روش پرداخت، تاریخ، مبلغ، میز، تخفیف و یادداشت.</p>
          </div>
          <div className="ord-head-actions">
            <button
              type="button"
              className={`ghost-button ord-adv-toggle ${showAdvancedFilters ? 'is-open' : ''}`}
              onClick={() => setShowAdvancedFilters((v) => !v)}
            >
              فیلترهای پیشرفته{activeFilterCount > 0 ? ` (${toPersianNumber(activeFilterCount)})` : ''}
            </button>
            {activeFilterCount > 0 ? (
              <button type="button" className="ghost-button ord-reset-btn" onClick={resetAllFilters}>
                پاک کردن فیلترها
              </button>
            ) : null}
            <button type="button" className="ghost-button" onClick={() => bumpTableReload()}>
              بروزرسانی
            </button>
          </div>
        </div>

        {/* نوار فیلتر سریع همیشه در دسترس */}
        <div className="ord-quick-bar">
          <div className="ord-date-presets" role="group" aria-label="بازه زمانی سریع">
            {[
              { id: 'all', label: 'همه زمان‌ها' },
              { id: 'today', label: 'امروز' },
              { id: 'yesterday', label: 'دیروز' },
              { id: '7d', label: '۷ روز اخیر' },
              { id: '30d', label: '۳۰ روز اخیر' },
            ].map((p) => (
              <button
                key={p.id}
                type="button"
                className={`ord-preset-pill ${datePreset === p.id ? 'is-active' : ''}`}
                onClick={() => applyDatePreset(p.id)}
              >
                {p.label}
              </button>
            ))}
          </div>

          <div className="ord-quick-selects">
            <select
              aria-label="وضعیت پرداخت"
              value={filters.status}
              onChange={(e) => updateFilter('status', e.target.value)}
            >
              <option value="">همه وضعیت‌های پرداخت</option>
              <option value="Paid">پرداخت‌شده</option>
              <option value="PendingPayment">در انتظار پرداخت</option>
              <option value="PartiallyPaid">پرداخت جزئی</option>
              <option value="Held">نگه‌داشت‌شده (Held)</option>
              <option value="Failed">ناموفق</option>
              <option value="Cancelled">لغوشده</option>
            </select>

            <select
              aria-label="وضعیت آماده‌سازی"
              value={filters.fulfillment}
              onChange={(e) => updateFilter('fulfillment', e.target.value)}
            >
              <option value="">همه وضعیت‌های آماده‌سازی</option>
              <option value="Registered">ثبت‌شده</option>
              <option value="Preparing">در حال آماده‌سازی</option>
              <option value="Ready">آماده تحویل</option>
              <option value="Completed">تحویل‌شده</option>
              <option value="None">تعیین‌نشده</option>
            </select>

            <select
              aria-label="کانال ثبت سفارش"
              value={filters.channel}
              onChange={(e) => updateFilter('channel', e.target.value)}
            >
              <option value="">همه کانال‌ها (آنلاین / POS)</option>
              <option value="Pos">صندوق فروش (POS)</option>
              <option value="Online">منوی آنلاین</option>
            </select>

            <select
              aria-label="روش پرداخت"
              value={filters.paymentMethod}
              onChange={(e) => updateFilter('paymentMethod', e.target.value)}
            >
              <option value="">همه روش‌های پرداخت</option>
              <option value="Card">کارتخوان (POS)</option>
              <option value="Cash">نقد</option>
              <option value="CardToCard">کارت به کارت</option>
              <option value="OnlineGateway">درگاه آنلاین</option>
              <option value="Split">ترکیبی (Split)</option>
              <option value="Complimentary">مهمان / رایگان</option>
            </select>
          </div>
        </div>

        {/* پنل فیلترهای پیشرفته (بازشونده) */}
        {showAdvancedFilters ? (
          <div className="ord-adv-panel">
            <div className="ord-adv-grid">
              <div className="ord-adv-field">
                <span>از تاریخ (شمسی)</span>
                <ShamsiDatePicker
                  value={filters.fromDate}
                  onChange={(iso) => updateFilter('fromDate', iso)}
                  placeholder="از تاریخ..."
                />
              </div>

              <div className="ord-adv-field">
                <span>تا تاریخ (شمسی)</span>
                <ShamsiDatePicker
                  value={filters.toDate}
                  onChange={(iso) => updateFilter('toDate', iso)}
                  placeholder="تا تاریخ..."
                />
              </div>

              <label className="ord-adv-field">
                <span>نام آیتم داخل سفارش</span>
                <input
                  type="text"
                  placeholder="مثلاً: لاته، تیرامیسو..."
                  value={filters.menuItemQuery}
                  onChange={(e) => updateFilter('menuItemQuery', e.target.value)}
                />
              </label>

              <label className="ord-adv-field">
                <span>نام یا کد میز</span>
                <input
                  type="text"
                  placeholder="مثلاً: میز ۴..."
                  value={filters.tableQuery}
                  onChange={(e) => updateFilter('tableQuery', e.target.value)}
                />
              </label>

              <label className="ord-adv-field">
                <span>حداقل مبلغ (تومان)</span>
                <input
                  type="text"
                  inputMode="numeric"
                  dir="ltr"
                  placeholder="مثلاً 100000"
                  value={filters.minAmount}
                  onChange={(e) => updateFilter('minAmount', e.target.value)}
                />
              </label>

              <label className="ord-adv-field">
                <span>حداکثر مبلغ (تومان)</span>
                <input
                  type="text"
                  inputMode="numeric"
                  dir="ltr"
                  placeholder="مثلاً 500000"
                  value={filters.maxAmount}
                  onChange={(e) => updateFilter('maxAmount', e.target.value)}
                />
              </label>

              <label className="ord-adv-field">
                <span>نوع سفارش</span>
                <select
                  value={filters.orderKind}
                  onChange={(e) => updateFilter('orderKind', e.target.value)}
                >
                  <option value="">همه انواع</option>
                  <option value="Standard">فروش عادی</option>
                  <option value="InternalUse">مصرف داخلی</option>
                  <option value="Complimentary">مهمان / رایگان</option>
                </select>
              </label>

              <label className="ord-adv-field">
                <span>نوع سرویس</span>
                <select
                  value={filters.serviceType}
                  onChange={(e) => updateFilter('serviceType', e.target.value)}
                >
                  <option value="">همه</option>
                  <option value="DineIn">سالن (Dine-In)</option>
                  <option value="TakeAway">بیرون‌بر (TakeAway)</option>
                </select>
              </label>

              {deliveryMethods.length > 0 ? (
                <label className="ord-adv-field">
                  <span>روش تحویل</span>
                  <select
                    value={filters.deliveryMethodId}
                    onChange={(e) => updateFilter('deliveryMethodId', e.target.value)}
                  >
                    <option value="">همه روش‌های تحویل</option>
                    {deliveryMethods.map((dm) => (
                      <option key={dm.id} value={dm.id}>{dm.persianName || dm.name}</option>
                    ))}
                  </select>
                </label>
              ) : null}

              <label className="ord-adv-field">
                <span>وضعیت حسابداری</span>
                <select
                  value={filters.excludedFromAccounting}
                  onChange={(e) => updateFilter('excludedFromAccounting', e.target.value)}
                >
                  <option value="">همه</option>
                  <option value="false">لحاظ در حسابداری</option>
                  <option value="true">خارج از حسابداری</option>
                </select>
              </label>

              <label className="ord-adv-field">
                <span>تخفیف</span>
                <select
                  value={filters.hasDiscount}
                  onChange={(e) => updateFilter('hasDiscount', e.target.value)}
                >
                  <option value="">همه</option>
                  <option value="true">دارای تخفیف</option>
                  <option value="false">بدون تخفیف</option>
                </select>
              </label>

              <label className="ord-adv-field">
                <span>تقسیم دنگ (Split Bill)</span>
                <select
                  value={filters.isSplitBill}
                  onChange={(e) => updateFilter('isSplitBill', e.target.value)}
                >
                  <option value="">همه</option>
                  <option value="true">فقط سفارش‌های دنگ</option>
                  <option value="false">فقط تک‌فاکتور</option>
                </select>
              </label>

              <label className="ord-adv-field">
                <span>یادداشت مشتری / سفارش</span>
                <select
                  value={filters.hasNote}
                  onChange={(e) => updateFilter('hasNote', e.target.value)}
                >
                  <option value="">همه</option>
                  <option value="true">دارای یادداشت</option>
                  <option value="false">بدون یادداشت</option>
                </select>
              </label>

              {canViewManagerNote ? (
                <label className="ord-adv-field">
                  <span>یادداشت محرمانه مدیریت</span>
                  <select
                    value={filters.hasManagerNote}
                    onChange={(e) => updateFilter('hasManagerNote', e.target.value)}
                  >
                    <option value="">همه</option>
                    <option value="true">دارای یادداشت مدیریت</option>
                    <option value="false">بدون یادداشت مدیریت</option>
                  </select>
                </label>
              ) : null}

              <label className="ord-adv-field">
                <span>مرتب‌سازی</span>
                <select
                  value={filters.sortBy}
                  onChange={(e) => updateFilter('sortBy', e.target.value)}
                >
                  <option value="newest">جدیدترین اول</option>
                  <option value="oldest">قدیمی‌ترین اول</option>
                  <option value="amount_desc">بیشترین مبلغ</option>
                  <option value="amount_asc">کمترین مبلغ</option>
                </select>
              </label>
            </div>
          </div>
        ) : null}

        <PaginatedAdminTable
          apiPath={ordersApiPath}
          token={session.token}
          columns={orderColumns}
          enabled
          reloadToken={tableReloadToken}
          searchPlaceholder="جستجو با کد پیگیری، نام، موبایل، نام آیتم، کد پیگیری پرداخت، میز یا یادداشت..."
        />
      </section>
    </section>
  )
}
