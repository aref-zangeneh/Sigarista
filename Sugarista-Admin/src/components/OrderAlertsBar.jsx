import { useEffect, useMemo, useState } from 'react'
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
      return 'ثبت‌شده (منتظر آماده‌سازی)'
    case 'Preparing':
      return 'در حال آماده‌سازی'
    case 'Ready':
      return 'آماده تحویل'
    case 'Completed':
      return 'تحویل داده شد'
    default:
      return '—'
  }
}

function shortFulfillmentLabel(status, isNew) {
  if (isNew) return 'جدید'
  switch (status) {
    case 'Registered':
    case 'None':
    case '':
      return 'منتظر آماده‌سازی'
    case 'Preparing':
      return 'در حال آماده‌سازی'
    case 'Ready':
      return 'آماده تحویل'
    case 'Completed':
      return 'تحویل شد'
    default:
      return 'ثبت‌شده'
  }
}

export function FulfillmentBadge({ status }) {
  if (!status || status === 'Completed') {
    return status === 'Completed' ? (
      <span className="fulfillment-badge fulfillment-badge--completed">تحویل داده شد</span>
    ) : null
  }

  const normalized = status === 'None' || status === '' ? 'Registered' : status

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

function OrderAlertRow({ order, isNew = false, busyState = null, onOpenOrder, onDismissNew }) {
  const isBusy = Boolean(busyState)
  const normalizedStatus =
    !order.fulfillmentStatus || order.fulfillmentStatus === 'None'
      ? 'Registered'
      : order.fulfillmentStatus
  const cleanPhone =
    order.customerPhone && String(order.customerPhone).toUpperCase() !== 'POS'
      ? order.customerPhone
      : null

  const statusMod = isNew
    ? 'new'
    : normalizedStatus === 'Ready'
      ? 'ready'
      : normalizedStatus === 'Preparing'
        ? 'preparing'
        : 'registered'

  return (
    <div
      className={`order-alert-row ${isNew ? 'is-new' : ''} ${isBusy ? 'is-busy' : ''}`}
      aria-busy={isBusy || undefined}
    >
      <div className="order-alert-row-main">
        <span className={`order-alert-pill order-alert-pill--${statusMod}`}>
          {shortFulfillmentLabel(normalizedStatus, isNew)}
        </span>
        <strong className="order-alert-code" dir="ltr" data-latin-digits="true">
          #{order.trackingCode}
        </strong>
        <span className="order-alert-customer">
          {order.customerName || 'مهمان'}
          {cleanPhone ? (
            <small className="order-alert-phone" dir="ltr">
              {cleanPhone}
            </small>
          ) : null}
        </span>
        {order.orderKind === 'InternalUse' ? (
          <span className="order-alert-kind">مصرف داخلی</span>
        ) : order.orderKind === 'Complimentary' ? (
          <span className="order-alert-kind">مهمان</span>
        ) : null}
      </div>

      <div className="order-alert-row-actions">
        <button
          type="button"
          className="order-alert-btn order-alert-btn--primary"
          disabled={isBusy}
          onClick={() => {
            if (!isBusy) onOpenOrder(order)
          }}
        >
          {busyState?.label || 'بررسی'}
        </button>
        {isNew && onDismissNew ? (
          <button
            type="button"
            className="order-alert-btn order-alert-btn--ghost"
            disabled={isBusy}
            title="متوجه شدم"
            onClick={() => onDismissNew(order.id)}
          >
            ✓ دیدم
          </button>
        ) : null}
      </div>
    </div>
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
  const kitchenOrders = alerts?.activeKitchenOrders ?? []
  const readyOrders = alerts?.readyForPickupOrders ?? []
  const newOrders = alerts?.newPaidOrders ?? []
  const hasKitchen = kitchenOrders.length > 0
  const hasReady = readyOrders.length > 0

  const [expanded, setExpanded] = useState(() => newOrders.length > 0)

  useEffect(() => {
    if (pulse && newOrders.length > 0) {
      setExpanded(true)
    }
  }, [pulse, newOrders.length])

  const unifiedOrders = useMemo(() => {
    const newIdSet = new Set(newOrders.map((o) => o.id))
    const seen = new Set()
    const list = []

    for (const order of newOrders) {
      if (!seen.has(order.id)) {
        seen.add(order.id)
        list.push({ order, isNew: true })
      }
    }
    for (const order of kitchenOrders) {
      if (!seen.has(order.id)) {
        seen.add(order.id)
        list.push({ order, isNew: newIdSet.has(order.id) })
      }
    }
    for (const order of readyOrders) {
      if (!seen.has(order.id)) {
        seen.add(order.id)
        list.push({ order, isNew: newIdSet.has(order.id) })
      }
    }
    return list
  }, [newOrders, kitchenOrders, readyOrders])

  if (!alerts || (!hasKitchen && !hasReady)) {
    return null
  }

  const normalizedSound = normalizeAlertSoundSettings(soundSettings)
  const intervalOptions = ALERT_SOUND_INTERVAL_PRESETS.includes(normalizedSound.intervalSeconds)
    ? ALERT_SOUND_INTERVAL_PRESETS
    : [...ALERT_SOUND_INTERVAL_PRESETS, normalizedSound.intervalSeconds].sort((a, b) => a - b)

  const kitchenCount = alerts.pendingKitchenCount ?? kitchenOrders.length
  const readyCount = alerts.readyCount ?? readyOrders.length
  const isUrgent = newOrders.length > 0 || kitchenCount > 0

  return (
    <section
      className={`order-alerts-bar ${isUrgent ? 'is-urgent' : 'is-ready-only'} ${pulse ? 'order-alerts-bar--pulse' : ''}`}
      aria-label="نوار اعلان سفارش‌ها"
    >
      <div className="order-alerts-strip">
        <div className="order-alerts-strip-start">
          <button
            type="button"
            className="order-alerts-toggle-btn"
            aria-expanded={expanded}
            onClick={() => setExpanded((prev) => !prev)}
            title={expanded ? 'جمع کردن لیست سفارش‌ها' : 'نمایش لیست سفارش‌های فعال'}
          >
            <span className={`order-alerts-dot ${isUrgent ? 'is-urgent' : 'is-ready'}`} />
            <span className="order-alerts-title">سفارش‌های فعال</span>
            <svg
              className={`order-alerts-chevron ${expanded ? 'is-open' : ''}`}
              viewBox="0 0 20 20"
              fill="none"
              stroke="currentColor"
              strokeWidth="2"
              strokeLinecap="round"
              strokeLinejoin="round"
              aria-hidden="true"
            >
              <path d="m5 7.5 5 5 5-5" />
            </svg>
          </button>

          <div className="order-alerts-counters">
            {newOrders.length > 0 ? (
              <button
                type="button"
                className="order-alert-chip order-alert-chip--new"
                onClick={() => setExpanded(true)}
              >
                <span>جدید</span>
                <strong>{toPersianDigits(newOrders.length)}</strong>
              </button>
            ) : null}

            {kitchenCount > 0 ? (
              <button
                type="button"
                className="order-alert-chip order-alert-chip--kitchen"
                onClick={() => setExpanded((prev) => !prev)}
              >
                <span>در صف آماده‌سازی</span>
                <strong>{toPersianDigits(kitchenCount)}</strong>
              </button>
            ) : null}

            {readyCount > 0 ? (
              <button
                type="button"
                className="order-alert-chip order-alert-chip--ready"
                onClick={() => setExpanded((prev) => !prev)}
              >
                <span>آماده تحویل</span>
                <strong>{toPersianDigits(readyCount)}</strong>
              </button>
            ) : null}
          </div>

          {!expanded && unifiedOrders.length > 0 ? (
            <div className="order-alerts-inline-preview">
              {unifiedOrders.slice(0, 2).map(({ order, isNew }) => (
                <button
                  key={order.id}
                  type="button"
                  className={`order-alerts-mini-order ${isNew ? 'is-new' : ''}`}
                  onClick={() => onOpenOrder(order)}
                  title="کلیک برای بررسی سریع سفارش"
                >
                  <span dir="ltr" data-latin-digits="true">#{order.trackingCode}</span>
                  <small>{order.customerName || 'مهمان'}</small>
                </button>
              ))}
              {unifiedOrders.length > 2 ? (
                <button
                  type="button"
                  className="order-alerts-more-btn"
                  onClick={() => setExpanded(true)}
                >
                  +{toPersianDigits(unifiedOrders.length - 2)} مورد دیگر
                </button>
              ) : null}
            </div>
          ) : null}
        </div>

        <div className="order-alerts-strip-end">
          {onUpdateSoundSettings ? (
            <div className="order-alert-sound-pill">
              <button
                type="button"
                className={`order-alert-sound-btn ${normalizedSound.enabled ? 'is-on' : 'is-off'}`}
                disabled={soundSaving}
                aria-pressed={normalizedSound.enabled}
                title={formatAlertSoundDescription(normalizedSound)}
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
                {normalizedSound.enabled ? (
                  <svg viewBox="0 0 20 20" fill="none" stroke="currentColor" strokeWidth="1.75" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true">
                    <path d="M9.5 4.5 5.8 7.5H3.2v5h2.6l3.7 3V4.5Z" />
                    <path d="M13 7.2a4 4 0 0 1 0 5.6M15.4 5a7.2 7.2 0 0 1 0 10" />
                  </svg>
                ) : (
                  <svg viewBox="0 0 20 20" fill="none" stroke="currentColor" strokeWidth="1.75" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true">
                    <path d="M9.5 4.5 5.8 7.5H3.2v5h2.6l3.7 3V4.5Z" />
                    <path d="m13 7.5 4 5m0-5-4 5" />
                  </svg>
                )}
                <span>{normalizedSound.enabled ? 'صدا' : 'بی‌صدا'}</span>
              </button>

              {normalizedSound.enabled ? (
                <select
                  className="order-alert-interval-mini"
                  aria-label="فاصله تکرار صدای اعلان سفارش"
                  value={String(normalizedSound.intervalSeconds)}
                  disabled={soundSaving}
                  onChange={(event) =>
                    onUpdateSoundSettings({
                      enabled: true,
                      intervalSeconds: Number(event.target.value),
                    })
                  }
                >
                  {intervalOptions.map((seconds) => (
                    <option key={seconds} value={String(seconds)}>
                      {seconds === 0 ? 'یک‌بار' : `هر ${toPersianDigits(seconds)}ث`}
                    </option>
                  ))}
                </select>
              ) : null}
            </div>
          ) : null}

          <button
            type="button"
            className="order-alerts-all-btn"
            onClick={onOpenOrders}
          >
            <span>همه سفارش‌ها</span>
            <span aria-hidden="true">←</span>
          </button>
        </div>
      </div>

      {expanded && unifiedOrders.length > 0 ? (
        <div className="order-alerts-drawer">
          {unifiedOrders.map(({ order, isNew }) => (
            <OrderAlertRow
              key={order.id}
              order={order}
              isNew={isNew}
              busyState={busyOrders?.[String(order.id)] ?? null}
              onOpenOrder={onOpenOrder}
              onDismissNew={onDismissNew}
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
      <div className="order-fulfillment-head">
        <strong>وضعیت آماده‌سازی</strong>
        <FulfillmentBadge status={status} />
      </div>

      {order.readySmsSentAt ? (
        <p className="order-fulfillment-note">پیامک «سفارش آماده» ارسال شده است.</p>
      ) : status === 'Ready' ? (
        <p className="order-fulfillment-note">با علامت‌گذاری «آماده شد»، پیامک به مشتری ارسال می‌شود.</p>
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
