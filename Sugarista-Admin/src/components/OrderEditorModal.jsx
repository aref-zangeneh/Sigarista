import { useEffect, useState } from 'react'
import { formatMoney } from './DataTable'
import { FulfillmentBadge } from './OrderAlertsBar'
import { toPersianNumber, formatDateTime } from '../utils/adminFormatters'
import { hasPermission, PERMISSIONS } from '../utils/adminPermissions'
import { useAdmin } from '../context/AdminContext'
import { useEscapeKey } from '../hooks/useEscapeKey'
import './OrderEditorModal.css'

function orderKindLabel(kind) {
  if (kind === 'InternalUse') return 'مصرف داخلی'
  if (kind === 'Complimentary') return 'مهمان / رایگان'
  return null
}

function orderStatusMeta(status) {
  switch (status) {
    case 'Paid':
      return { label: 'پرداخت‌شده', tone: 'paid' }
    case 'PendingPayment':
      return { label: 'در انتظار پرداخت', tone: 'pending' }
    case 'PartiallyPaid':
      return { label: 'پرداخت جزئی', tone: 'pending' }
    case 'Held':
      return { label: 'نگه‌داشته‌شده', tone: 'neutral' }
    case 'Cancelled':
      return { label: 'لغوشده', tone: 'cancelled' }
    case 'Failed':
      return { label: 'ناموفق', tone: 'cancelled' }
    default:
      return { label: status || '—', tone: 'neutral' }
  }
}

function paymentMethodPersian(method) {
  switch (method) {
    case 'Cash':
      return 'نقدی'
    case 'Card':
      return 'کارت‌خوان'
    case 'CardToCard':
      return 'کارت به کارت'
    case 'Split':
      return 'پرداخت ترکیبی'
    case 'OnlineGateway':
      return 'درگاه آنلاین'
    case 'Complimentary':
      return 'رایگان'
    default:
      return null
  }
}

export function OrderEditorModal() {
  const admin = useAdmin()
  const {
    selectedOrder,
    orderEditForm,
    saving,
    closeOrderEditor,
    handleSaveOrder,
    handleRegisterPayment,
    registerPaymentMethod,
    setRegisterPaymentMethod,
    updateOrderFulfillment,
    setOrderEditForm,
    updateOrderLineQuantity,
    toggleOrderLineRemoval,
    handleCancelOrder,
    handleDeleteOrder,
    handleToggleOrderAccountingExclusion,
  } = admin

  const [exclusionReason, setExclusionReason] = useState('')
  const [restoreOnDelete, setRestoreOnDelete] = useState(true)
  const [settleReason, setSettleReason] = useState('')
  const [advancedOpen, setAdvancedOpen] = useState(false)

  useEffect(() => {
    setExclusionReason(selectedOrder?.excludedFromAccountingReason ?? '')
    setRestoreOnDelete(true)
    setSettleReason(selectedOrder?.complimentaryReason ?? '')
    setAdvancedOpen(false)
  }, [selectedOrder?.id, selectedOrder?.excludedFromAccountingReason, selectedOrder?.complimentaryReason])

  useEscapeKey(Boolean(selectedOrder && orderEditForm), closeOrderEditor)

  if (!selectedOrder || !orderEditForm) {
    return null
  }

  const isNonRevenue =
    selectedOrder.orderKind === 'InternalUse' || selectedOrder.orderKind === 'Complimentary'
  const settleAsNonRevenue =
    registerPaymentMethod === 'InternalUse' || registerPaymentMethod === 'Complimentary'
  const canManageManagerNote = hasPermission(admin.session, PERMISSIONS.pos.managerNote)
  const statusInfo = orderStatusMeta(selectedOrder.status)
  const kindBadge = orderKindLabel(selectedOrder.orderKind)
  const methodLabel = paymentMethodPersian(selectedOrder.paymentMethod)
  const isCancelled = selectedOrder.status === 'Cancelled'
  const canSettleInvoice = ['PendingPayment', 'PartiallyPaid', 'Held'].includes(selectedOrder.status)

  const fulfillmentStatus =
    !selectedOrder.fulfillmentStatus || selectedOrder.fulfillmentStatus === 'None'
      ? 'Registered'
      : selectedOrder.fulfillmentStatus

  return (
    <div className="modal-backdrop oem-backdrop" onClick={closeOrderEditor}>
      <div
        className="modal-card oem-modal"
        role="dialog"
        aria-modal="true"
        aria-labelledby="order-editor-title"
        onClick={(event) => event.stopPropagation()}
      >
        <form className="oem-form" onSubmit={handleSaveOrder}>
          {/* ── Zone 1: Sticky Minimal Header ── */}
          <header className="oem-header">
            <div className="oem-header-main">
              <div className="oem-title-row">
                <h3 id="order-editor-title" className="oem-title">
                  <span>سفارش</span>
                  <span className="oem-code" dir="ltr" data-latin-digits="true">
                    #{selectedOrder.trackingCode}
                  </span>
                </h3>
                <span className={`oem-status-pill oem-status-pill--${statusInfo.tone}`}>
                  {statusInfo.label}
                </span>
                {selectedOrder.status === 'Paid' ? (
                  <FulfillmentBadge status={fulfillmentStatus} />
                ) : null}
                {kindBadge ? (
                  <span className="oem-status-pill oem-status-pill--neutral">{kindBadge}</span>
                ) : null}
                {selectedOrder.isExcludedFromAccounting ? (
                  <span className="oem-status-pill oem-status-pill--amber">خارج از حسابداری</span>
                ) : null}
              </div>

              <div className="oem-submeta">
                <span>ثبت: {formatDateTime(selectedOrder.createdAt)}</span>
                {methodLabel ? <span>· پرداخت: {methodLabel}</span> : null}
                {selectedOrder.deliveryAt ? (
                  <span>· زمان تحویل: {formatDateTime(selectedOrder.deliveryAt)}</span>
                ) : null}
                {selectedOrder.isSplitBill ? <span>· تسویه جداگانه</span> : null}
                {isNonRevenue && selectedOrder.complimentaryReason ? (
                  <span>· بابت: {selectedOrder.complimentaryReason}</span>
                ) : null}
              </div>
            </div>

            <button
              type="button"
              className="oem-close-btn"
              onClick={closeOrderEditor}
              aria-label="بستن"
              title="بستن (Esc)"
            >
              ✕
            </button>
          </header>

          {/* ── Zone 2: Scrollable Compact Body ── */}
          <div className="oem-body">
            {/* نوار سریع تغییر وضعیت آماده‌سازی */}
            {selectedOrder.status === 'Paid' && fulfillmentStatus !== 'Completed' ? (
              <div className="oem-fulfillment-bar">
                <div className="oem-fulfillment-info">
                  <strong>مرحله آماده‌سازی:</strong>
                  <span className="oem-fulfillment-sms-hint">
                    {selectedOrder.readySmsSentAt
                      ? 'پیامک آماده شدن ارسال شد ✓'
                      : fulfillmentStatus === 'Ready'
                        ? 'منتظر تحویل به مشتری'
                        : 'با زدن «آماده شد» پیامک اطلاع‌رسانی ارسال می‌شود'}
                  </span>
                </div>

                <div className="oem-fulfillment-btns">
                  {fulfillmentStatus === 'Registered' ? (
                    <button
                      type="button"
                      className="oem-step-btn"
                      disabled={saving}
                      onClick={() => updateOrderFulfillment(selectedOrder.id, 'Preparing')}
                    >
                      شروع آماده‌سازی
                    </button>
                  ) : null}
                  {fulfillmentStatus === 'Registered' || fulfillmentStatus === 'Preparing' ? (
                    <button
                      type="button"
                      className="oem-step-btn oem-step-btn--primary"
                      disabled={saving}
                      onClick={() => updateOrderFulfillment(selectedOrder.id, 'Ready')}
                    >
                      آماده شد (پیامک)
                    </button>
                  ) : null}
                  {fulfillmentStatus === 'Ready' ? (
                    <button
                      type="button"
                      className="oem-step-btn oem-step-btn--success"
                      disabled={saving}
                      onClick={() => updateOrderFulfillment(selectedOrder.id, 'Completed')}
                    >
                      ✓ تحویل داده شد
                    </button>
                  ) : null}
                </div>
              </div>
            ) : null}

            {/* خلاصه اعداد مالی در یک نوار تمیز */}
            <div className="oem-metrics">
              <div className="oem-metric">
                <span>جمع اقلام</span>
                <strong>{formatMoney(selectedOrder.itemsTotal ?? 0)} ت</strong>
              </div>
              {selectedOrder.discountTotal > 0 ? (
                <div className="oem-metric oem-metric--discount">
                  <span>تخفیف کل</span>
                  <strong>−{formatMoney(selectedOrder.discountTotal)} ت</strong>
                </div>
              ) : null}
              {selectedOrder.taxTotal > 0 || selectedOrder.deliveryFee > 0 ? (
                <div className="oem-metric">
                  <span>
                    {selectedOrder.taxTotal > 0 && selectedOrder.deliveryFee > 0
                      ? 'مالیات + ارسال'
                      : selectedOrder.taxTotal > 0
                        ? 'مالیات'
                        : 'هزینه ارسال'}
                  </span>
                  <strong>
                    {formatMoney((selectedOrder.taxTotal ?? 0) + (selectedOrder.deliveryFee ?? 0))} ت
                  </strong>
                </div>
              ) : null}
              <div className="oem-metric oem-metric--total">
                <span>مبلغ قابل پرداخت</span>
                <strong>{formatMoney(selectedOrder.totalAmount)} ت</strong>
              </div>
              {selectedOrder.status !== 'Paid' || (selectedOrder.amountPaid ?? 0) < selectedOrder.totalAmount ? (
                <div className="oem-metric">
                  <span>پرداخت‌شده</span>
                  <strong>{formatMoney(selectedOrder.amountPaid ?? 0)} ت</strong>
                </div>
              ) : null}
            </div>

            {/* تگ‌های تخفیف در صورت وجود */}
            {selectedOrder.manualDiscountAmount > 0 || (selectedOrder.discounts?.length ?? 0) > 0 ? (
              <div className="oem-discount-pills">
                {selectedOrder.manualDiscountAmount > 0 ? (
                  <span className="oem-discount-pill">
                    🏷 تخفیف صندوق: −{formatMoney(selectedOrder.manualDiscountAmount)} ت
                    {selectedOrder.manualDiscountType === 'Percent' && selectedOrder.manualDiscountValue
                      ? ` (${toPersianNumber(selectedOrder.manualDiscountValue)}٪)`
                      : ''}
                    {selectedOrder.manualDiscountReason ? ` · ${selectedOrder.manualDiscountReason}` : ''}
                  </span>
                ) : null}
                {selectedOrder.discounts
                  ?.filter((d) => d.kind !== 'ManualPos')
                  .map((d) => (
                    <span key={d.id} className="oem-discount-pill">
                      ✦ {d.name}: −{formatMoney(d.amount)} ت
                    </span>
                  ))}
              </div>
            ) : null}

            {/* اقلام سفارش */}
            <div className="oem-section">
              <div className="oem-section-head">
                <h4 className="oem-section-title">
                  اقلام سفارش ({toPersianNumber(orderEditForm.lines.length)})
                </h4>
                <span className="oem-section-hint">تغییر تعداد، موجودی انبار را خودکار همگام می‌کند</span>
              </div>

              <div className="oem-lines-box">
                {orderEditForm.lines.map((line) => {
                  const subParts = [
                    ...(line.optionLabels ?? []),
                    ...(line.modifierLabels ?? []),
                    ...(line.customDrinkSummary ?? []),
                  ].filter(Boolean)

                  return (
                    <div
                      key={line.id}
                      className={`oem-line-row ${line.remove ? 'is-removed' : ''}`}
                    >
                      <div className="oem-line-info">
                        <div className="oem-line-name-row">
                          <span className="oem-line-name">{line.persianName}</span>
                          {line.sizeLabel ? <span className="oem-line-tag">{line.sizeLabel}</span> : null}
                          {line.payerLabel ? <span className="oem-line-tag">{line.payerLabel}</span> : null}
                        </div>
                        {subParts.length > 0 ? (
                          <div className="oem-line-sub">{subParts.join(' · ')}</div>
                        ) : null}
                      </div>

                      <div className="oem-line-controls">
                        <div className="oem-qty-stepper">
                          <button
                            type="button"
                            disabled={isCancelled || line.remove || line.quantity <= 1}
                            onClick={() => updateOrderLineQuantity(line.id, Math.max(1, line.quantity - 1))}
                            aria-label="کاهش تعداد"
                          >
                            −
                          </button>
                          <span>{toPersianNumber(line.quantity)}</span>
                          <button
                            type="button"
                            disabled={isCancelled || line.remove}
                            onClick={() => updateOrderLineQuantity(line.id, line.quantity + 1)}
                            aria-label="افزایش تعداد"
                          >
                            +
                          </button>
                        </div>

                        <span className="oem-line-price">
                          {formatMoney(line.lineTotal)} ت
                        </span>

                        {orderEditForm.lines.length > 1 || line.remove ? (
                          <button
                            type="button"
                            className={`oem-remove-btn ${line.remove ? 'is-active' : ''}`}
                            disabled={isCancelled}
                            onClick={() => toggleOrderLineRemoval(line.id)}
                          >
                            {line.remove ? 'بازگردانی' : 'حذف'}
                          </button>
                        ) : null}
                      </div>
                    </div>
                  )
                })}
              </div>
            </div>

            {/* پرداخت‌های ثبت‌شده (در پرداخت ترکیبی یا تسویه جداگانه) */}
            {selectedOrder.payments?.length > 0 ? (
              <div className="oem-section">
                <h4 className="oem-section-title">ریز تراکنش‌های پرداخت</h4>
                <div className="oem-payments-list">
                  {selectedOrder.payments.map((p) => (
                    <div key={p.id} className="oem-payment-chip">
                      <strong>{p.payerLabel}</strong>
                      <span>{formatMoney(p.amount)} ت</span>
                      {p.referenceId && !String(p.referenceId).startsWith('pos-') && !String(p.referenceId).startsWith('manual-') ? (
                        <small dir="ltr">({p.referenceId})</small>
                      ) : null}
                      <small>· {p.status === 'Paid' ? 'پرداخت‌شده' : p.status}</small>
                    </div>
                  ))}
                </div>
              </div>
            ) : null}

            {/* مشخصات مشتری و یادداشت‌ها */}
            <div className="oem-fields-grid">
              <label className="oem-field">
                <span>نام مشتری</span>
                <input
                  value={orderEditForm.customerName}
                  onChange={(event) =>
                    setOrderEditForm((current) => ({ ...current, customerName: event.target.value }))
                  }
                  disabled={isCancelled}
                />
              </label>

              <label className="oem-field">
                <span>شماره تماس</span>
                <input
                  dir="ltr"
                  className="ltr-input"
                  value={orderEditForm.customerPhone === 'POS' ? '' : orderEditForm.customerPhone}
                  placeholder="بدون شماره"
                  onChange={(event) =>
                    setOrderEditForm((current) => ({
                      ...current,
                      customerPhone: event.target.value || 'POS',
                    }))
                  }
                  disabled={isCancelled}
                />
              </label>

              <label className="oem-field oem-field--full">
                <span>یادداشت سفارش (چاپ روی فیش)</span>
                <textarea
                  rows={1}
                  placeholder="توضیحات مشتری یا آشپزخانه..."
                  value={orderEditForm.note}
                  onChange={(event) =>
                    setOrderEditForm((current) => ({ ...current, note: event.target.value }))
                  }
                  disabled={isCancelled}
                />
              </label>

              {canManageManagerNote ? (
                <label className="oem-field oem-field--full oem-field--manager">
                  <span>🔒 یادداشت محرمانه مدیریت (فقط قابل مشاهده برای مدیریت · چاپ نمی‌شود)</span>
                  <textarea
                    rows={1}
                    placeholder="یادداشت داخلی ویژه مدیریت..."
                    value={orderEditForm.managerNote ?? ''}
                    onChange={(event) =>
                      setOrderEditForm((current) => ({
                        ...current,
                        managerNote: event.target.value,
                      }))
                    }
                    disabled={isCancelled}
                  />
                </label>
              ) : null}
            </div>

            {/* کشوی جمع‌وجور تنظیمات حسابداری و عملیات حساس */}
            <div className="oem-advanced">
              <button
                type="button"
                className="oem-advanced-toggle"
                aria-expanded={advancedOpen}
                onClick={() => setAdvancedOpen((prev) => !prev)}
              >
                <span>
                  ⚙️ تنظیمات حسابداری و عملیات حساس
                  {selectedOrder.isExcludedFromAccounting ? ' (خارج از حسابداری)' : ''}
                </span>
                <span>{advancedOpen ? 'بستن ▴' : 'باز کردن ▾'}</span>
              </button>

              {advancedOpen ? (
                <div className="oem-advanced-body">
                  <div className="oem-accounting-row">
                    <div className="oem-accounting-text">
                      <strong>وضعیت در اسناد حسابداری</strong>
                      <span>
                        {selectedOrder.isExcludedFromAccounting
                          ? `خارج شده از گزارش مالی${selectedOrder.excludedFromAccountingReason ? ` (${selectedOrder.excludedFromAccountingReason})` : ''}`
                          : 'در اسناد حسابداری و فروش لحاظ شده است.'}
                      </span>
                    </div>

                    {selectedOrder.isExcludedFromAccounting ? (
                      <button
                        type="button"
                        className="oem-btn-sm"
                        disabled={saving}
                        onClick={() => handleToggleOrderAccountingExclusion(selectedOrder, false)}
                      >
                        بازگرداندن به حسابداری
                      </button>
                    ) : (
                      <div className="oem-accounting-controls">
                        <input
                          type="text"
                          placeholder="علت خروج از حسابداری (اختیاری)..."
                          value={exclusionReason}
                          onChange={(event) => setExclusionReason(event.target.value)}
                          disabled={saving}
                        />
                        <button
                          type="button"
                          className="oem-btn-sm"
                          disabled={saving}
                          onClick={() =>
                            handleToggleOrderAccountingExclusion(selectedOrder, true, exclusionReason)
                          }
                        >
                          خروج از حسابداری
                        </button>
                      </div>
                    )}
                  </div>

                  <div className="oem-danger-row">
                    {!isCancelled ? (
                      <button
                        type="button"
                        className="oem-btn-sm oem-btn-sm--danger"
                        disabled={saving}
                        onClick={handleCancelOrder}
                      >
                        لغو سفارش و بازگردانی موجودی
                      </button>
                    ) : (
                      <span className="oem-section-hint">این سفارش لغو شده است.</span>
                    )}

                    <div className="oem-danger-group">
                      {!isCancelled ? (
                        <label className="oem-checkbox-inline">
                          <input
                            type="checkbox"
                            checked={restoreOnDelete}
                            onChange={(event) => setRestoreOnDelete(event.target.checked)}
                            disabled={saving}
                          />
                          <span>بازگردانی موجودی هنگام حذف</span>
                        </label>
                      ) : null}
                      <button
                        type="button"
                        className="oem-btn-sm oem-btn-sm--danger"
                        disabled={saving}
                        onClick={() =>
                          handleDeleteOrder(selectedOrder, { restoreInventory: restoreOnDelete })
                        }
                      >
                        حذف کامل سفارش
                      </button>
                    </div>
                  </div>
                </div>
              ) : null}
            </div>
          </div>

          {/* ── Zone 3: Sticky Footer ── */}
          <footer className="oem-footer">
            {canSettleInvoice ? (
              <div className="oem-settle-bar">
                <select
                  aria-label="روش تسویه فاکتور"
                  value={registerPaymentMethod}
                  onChange={(event) => setRegisterPaymentMethod(event.target.value)}
                >
                  <option value="Cash">تسویه نقدی</option>
                  <option value="Card">تسویه کارت‌خوان</option>
                  <option value="CardToCard">تسویه کارت به کارت</option>
                  <option value="InternalUse">مصرف داخلی (رایگان)</option>
                  <option value="Complimentary">مهمان / آشنا (رایگان)</option>
                </select>

                {settleAsNonRevenue ? (
                  <input
                    type="text"
                    placeholder="بابت / علت (اختیاری)..."
                    value={settleReason}
                    onChange={(event) => setSettleReason(event.target.value)}
                  />
                ) : null}

                <button
                  type="button"
                  className="oem-settle-btn"
                  disabled={saving}
                  onClick={() =>
                    handleRegisterPayment(
                      settleAsNonRevenue
                        ? { orderKind: registerPaymentMethod, complimentaryReason: settleReason }
                        : { orderKind: 'Standard' },
                    )
                  }
                >
                  {settleAsNonRevenue ? 'ثبت رایگان' : 'ثبت پرداخت و نهایی‌سازی'}
                </button>
              </div>
            ) : null}

            <div className="oem-footer-actions">
              <button
                type="button"
                className="oem-footer-btn oem-footer-btn--ghost"
                onClick={closeOrderEditor}
              >
                بستن
              </button>
              {!isCancelled ? (
                <button
                  type="submit"
                  className="oem-footer-btn oem-footer-btn--primary"
                  disabled={saving}
                >
                  {saving ? 'در حال ذخیره...' : 'ذخیره تغییرات'}
                </button>
              ) : null}
            </div>
          </footer>
        </form>
      </div>
    </div>
  )
}
