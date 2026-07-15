import './OrderAlertsBar.css'

export function fulfillmentStatusLabel(status) {
  switch (status) {
    case 'Registered':
    case 'None':
    case '':
      return 'سفارش ثبت شده — منتظر آماده‌سازی'
    case 'Preparing':
      return 'در حال آماده‌سازی'
    case 'Ready':
      return 'سفارش آماده'
    case 'Completed':
      return 'تحویل داده شد'
    default:
      return '—'
  }
}

export function FulfillmentBadge({ status }) {
  if (!status || status === 'Completed') {
    return status === 'Completed' ? (
      <span className="fulfillment-badge fulfillment-badge--completed">تحویل داده شد</span>
    ) : null
  }

  const normalized =
    status === 'None' || status === '' ? 'Registered' : status

  const className =
    normalized === 'Registered'
      ? 'fulfillment-badge fulfillment-badge--registered'
      : normalized === 'Preparing'
        ? 'fulfillment-badge fulfillment-badge--preparing'
        : normalized === 'Ready'
          ? 'fulfillment-badge fulfillment-badge--ready'
          : 'fulfillment-badge'

  return <span className={className}>{fulfillmentStatusLabel(status)}</span>
}

function OrderAlertItem({ order, tone = 'default', onOpenOrder, onDismissNew }) {
  const isNew = tone === 'new'

  return (
    <article className={`order-alert-item ${isNew ? 'order-alert-item--new' : ''}`}>
      <div>
        <strong>
          {isNew ? 'سفارش جدید پرداخت شد — ' : ''}
          {order.trackingCode}
        </strong>
        <div className="table-subtext">
          {order.customerName} · {order.customerPhone}
        </div>
        <FulfillmentBadge status={order.fulfillmentStatus} />
      </div>
      <div className="order-fulfillment-actions">
        <button type="button" className="ghost-button" onClick={() => onOpenOrder(order)}>
          مدیریت
        </button>
        {isNew && onDismissNew ? (
          <button type="button" className="ghost-button" onClick={() => onDismissNew(order.id)}>
            متوجه شدم
          </button>
        ) : null}
      </div>
    </article>
  )
}

export function OrderAlertsBar({
  alerts,
  pulse,
  onOpenOrders,
  onOpenOrder,
  onDismissNew,
}) {
  if (!alerts) {
    return null
  }

  const kitchenOrders = alerts.activeKitchenOrders ?? []
  const readyOrders = alerts.readyForPickupOrders ?? []
  const newOrders = alerts.newPaidOrders ?? []
  const hasKitchen = kitchenOrders.length > 0
  const hasReady = readyOrders.length > 0

  if (!hasKitchen && !hasReady) {
    return null
  }

  return (
    <section className={`order-alerts-bar ${pulse ? 'order-alerts-bar--pulse' : ''}`}>
      <div className="order-alerts-head">
        <div>
          <strong>اعلان سفارش</strong>
          <p className="table-subtext">سفارش‌های پرداخت‌شده در صف آشپزخانه یا آماده تحویل — صدای کوتاه هر ۱۰ ثانیه تا زمان «شروع آماده‌سازی» تکرار می‌شود.</p>
        </div>
        <div className="order-alerts-stats">
          <span className="order-alert-stat order-alert-stat--urgent">
            نیاز به آماده‌سازی: <strong>{alerts.pendingKitchenCount ?? kitchenOrders.length}</strong>
          </span>
          <span className="order-alert-stat">
            آماده تحویل: <strong>{alerts.readyCount ?? readyOrders.length}</strong>
          </span>
          <button type="button" className="ghost-button" onClick={onOpenOrders}>
            مشاهده همه سفارش‌ها
          </button>
        </div>
      </div>

      {newOrders.length > 0 ? (
        <div className="order-alerts-list">
          <p className="order-alerts-section-title">سفارش‌های تازه پرداخت‌شده</p>
          {newOrders.map((order) => (
            <OrderAlertItem
              key={`new-${order.id}`}
              order={order}
              tone="new"
              onOpenOrder={onOpenOrder}
              onDismissNew={onDismissNew}
            />
          ))}
        </div>
      ) : null}

      {hasKitchen ? (
        <div className="order-alerts-list">
          <p className="order-alerts-section-title">در صف آماده‌سازی</p>
          {kitchenOrders.map((order) => (
            <OrderAlertItem key={`kitchen-${order.id}`} order={order} onOpenOrder={onOpenOrder} />
          ))}
        </div>
      ) : null}

      {hasReady ? (
        <div className="order-alerts-list">
          <p className="order-alerts-section-title">آماده تحویل به مشتری</p>
          {readyOrders.map((order) => (
            <OrderAlertItem key={`ready-${order.id}`} order={order} onOpenOrder={onOpenOrder} />
          ))}
        </div>
      ) : null}
    </section>
  )
}

export function OrderFulfillmentActions({ order, saving, onUpdateStatus }) {
  if (!order || order.status !== 'Paid') {
    return null
  }

  const status =
    !order.fulfillmentStatus || order.fulfillmentStatus === 'None'
      ? 'Registered'
      : order.fulfillmentStatus

  return (
    <div className="order-fulfillment-panel">
      <div>
        <strong>وضعیت آماده‌سازی</strong>
        <div>
          <FulfillmentBadge status={status} />
        </div>
      </div>

      {order.readySmsSentAt ? (
        <p>پیامک «سفارش آماده» ارسال شده است.</p>
      ) : status === 'Ready' ? (
        <p>با علامت‌گذاری «آماده شد»، پیامک به مشتری ارسال می‌شود.</p>
      ) : null}

      <div className="order-fulfillment-actions">
        {status === 'Registered' ? (
          <button type="button" disabled={saving} onClick={() => onUpdateStatus('Preparing')}>
            شروع آماده‌سازی
          </button>
        ) : null}
        {status === 'Registered' || status === 'Preparing' ? (
          <button type="button" disabled={saving} onClick={() => onUpdateStatus('Ready')}>
            آماده شد (ارسال پیامک)
          </button>
        ) : null}
        {status === 'Ready' ? (
          <button type="button" disabled={saving} onClick={() => onUpdateStatus('Completed')}>
            تحویل داده شد
          </button>
        ) : null}
      </div>
    </div>
  )
}
