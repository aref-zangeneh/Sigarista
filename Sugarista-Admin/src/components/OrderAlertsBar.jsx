import './OrderAlertsBar.css'
import {
  ALERT_SOUND_INTERVAL_PRESETS,
  formatAlertSoundDescription,
  normalizeAlertSoundSettings,
  playOrderAlertSound,
} from '../utils/orderAlertSound'
import { toPersianDigits } from '../utils/persianDigits'

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

function OrderAlertItem({ order, tone = 'default', busyState = null, onOpenOrder, onDismissNew }) {
  const isNew = tone === 'new'
  const isBusy = Boolean(busyState)

  return (
    <article
      className={`order-alert-item ${isNew ? 'order-alert-item--new' : ''} ${isBusy ? 'order-alert-item--busy' : ''}`}
      aria-busy={isBusy || undefined}
    >
      <div>
        <strong>
          {isNew ? 'سفارش جدید پرداخت شد — ' : ''}
          {order.trackingCode}
        </strong>
        <div className="table-subtext">
          {order.customerName} · {order.customerPhone}
          {order.orderKind === 'InternalUse' ? ' · مصرف داخلی' : null}
          {order.orderKind === 'Complimentary' ? ' · مهمان / رایگان' : null}
          {order.complimentaryReason ? ` (${order.complimentaryReason})` : null}
        </div>
        <FulfillmentBadge status={order.fulfillmentStatus} />
      </div>
      <div className="order-fulfillment-actions">
        <button
          type="button"
          className="ghost-button"
          disabled={isBusy}
          aria-disabled={isBusy}
          onClick={() => {
            if (!isBusy) {
              onOpenOrder(order)
            }
          }}
        >
          {busyState?.label || 'مدیریت'}
        </button>
        {isNew && onDismissNew ? (
          <button
            type="button"
            className="ghost-button"
            disabled={isBusy}
            onClick={() => onDismissNew(order.id)}
          >
            متوجه شدم
          </button>
        ) : null}
      </div>
    </article>
  )
}

export function OrderAlertsBar({
  alerts,
  busyOrders = {},
  pulse,
  soundSettings,
  soundSaving = false,
  onUpdateSoundSettings,
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

  const normalizedSound = normalizeAlertSoundSettings(soundSettings)
  const intervalOptions = ALERT_SOUND_INTERVAL_PRESETS.includes(normalizedSound.intervalSeconds)
    ? ALERT_SOUND_INTERVAL_PRESETS
    : [...ALERT_SOUND_INTERVAL_PRESETS, normalizedSound.intervalSeconds].sort((a, b) => a - b)

  return (
    <section className={`order-alerts-bar ${pulse ? 'order-alerts-bar--pulse' : ''}`}>
      <div className="order-alerts-head">
        <div>
          <strong>اعلان سفارش</strong>
          <p className="table-subtext">{formatAlertSoundDescription(normalizedSound)}</p>
        </div>
        <div className="order-alerts-stats">
          <span className="order-alert-stat order-alert-stat--urgent">
            نیاز به آماده‌سازی: <strong>{alerts.pendingKitchenCount ?? kitchenOrders.length}</strong>
          </span>
          <span className="order-alert-stat">
            آماده تحویل: <strong>{alerts.readyCount ?? readyOrders.length}</strong>
          </span>
          {onUpdateSoundSettings ? (
            <div className="order-alert-sound-controls">
              <button
                type="button"
                className={`ghost-button order-alert-sound-toggle ${normalizedSound.enabled ? 'is-on' : 'is-muted'}`}
                disabled={soundSaving}
                aria-pressed={normalizedSound.enabled}
                onClick={() => {
                  const nextEnabled = !normalizedSound.enabled
                  if (nextEnabled) {
                    playOrderAlertSound({ force: true })
                  }
                  onUpdateSoundSettings({
                    enabled: nextEnabled,
                    intervalSeconds: normalizedSound.intervalSeconds,
                  })
                }}
              >
                {normalizedSound.enabled ? '🔊 صدا روشن' : '🔇 صدا خاموش'}
              </button>
              <select
                className="order-alert-interval-select"
                aria-label="فاصله تکرار صدای اعلان سفارش"
                value={String(normalizedSound.intervalSeconds)}
                disabled={soundSaving || !normalizedSound.enabled}
                onChange={(event) =>
                  onUpdateSoundSettings({
                    enabled: normalizedSound.enabled,
                    intervalSeconds: Number(event.target.value),
                  })
                }
              >
                {intervalOptions.map((seconds) => (
                  <option key={seconds} value={String(seconds)}>
                    {seconds === 0 ? 'فقط یک‌بار (بدون تکرار)' : `هر ${toPersianDigits(seconds)} ثانیه`}
                  </option>
                ))}
              </select>
            </div>
          ) : null}
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
              busyState={busyOrders?.[String(order.id)] ?? null}
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
            <OrderAlertItem
              key={`kitchen-${order.id}`}
              order={order}
              busyState={busyOrders?.[String(order.id)] ?? null}
              onOpenOrder={onOpenOrder}
            />
          ))}
        </div>
      ) : null}

      {hasReady ? (
        <div className="order-alerts-list">
          <p className="order-alerts-section-title">آماده تحویل به مشتری</p>
          {readyOrders.map((order) => (
            <OrderAlertItem
              key={`ready-${order.id}`}
              order={order}
              busyState={busyOrders?.[String(order.id)] ?? null}
              onOpenOrder={onOpenOrder}
            />
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
