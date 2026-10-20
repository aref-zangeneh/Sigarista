import { useCallback, useEffect, useRef, useState } from 'react'
import { useAdmin } from '../context/AdminContext'
import { PaginatedAdminTable } from '../components/PaginatedAdminTable'
import { formatDateTime, toPersianNumber } from '../utils/adminFormatters'
import { fulfillmentStatusLabel } from '../components/OrderAlertsBar'

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
    loadAdminData,
  } = useAdmin()

  const [queue, setQueue] = useState({ pending: [], processing: [], ready: [] })
  const [queueTab, setQueueTab] = useState('pending')
  const [activeTabId, setActiveTabId] = useState('pending')
  const [queueLoading, setQueueLoading] = useState(true)
  const [queueError, setQueueError] = useState('')
  const [message, setMessage] = useState('')
  const [actingOrderId, setActingOrderId] = useState(null)
  const refreshTimerRef = useRef(null)

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
        setMessage(`وضعیت سفارش ${order.trackingCode} به «${fulfillmentStatusLabel(statusByAction[action])}» تغییر کرد.`)
      }
      await loadQueue()
      bumpTableReload()
    } catch (requestError) {
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
              {activeOrderList.map((order) => (
                <tr key={order.id}>
                  <td>{order.trackingCode}</td>
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
            <p>پرداخت، آماده‌سازی و تحویل سفارش — با اعلان لحظه‌ای برای سفارش‌های جدید.</p>
          </div>
          <button type="button" className="ghost-button" onClick={() => bumpTableReload()}>
            بروزرسانی
          </button>
        </div>
        <PaginatedAdminTable
          apiPath="/admin/orders"
          token={session.token}
          columns={orderColumns}
          enabled
          reloadToken={tableReloadToken}
          searchPlaceholder="جستجو بر اساس کد پیگیری، نام یا شماره تماس..."
        />
      </section>
    </section>
  )
}
