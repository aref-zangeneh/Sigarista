import { useEffect, useState } from 'react'
import { DataTable, formatMoney } from './DataTable'
import { OrderFulfillmentActions, fulfillmentStatusLabel } from './OrderAlertsBar'
import { toPersianNumber, formatDateTime } from '../utils/adminFormatters'
import { useAdmin } from '../context/AdminContext'
import { useEscapeKey } from '../hooks/useEscapeKey'

function orderKindLabel(kind) {
  if (kind === 'InternalUse') return 'مصرف داخلی (رایگان)'
  if (kind === 'Complimentary') return 'مهمان / آشنا (رایگان)'
  return 'فروش عادی'
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

  useEffect(() => {
    setExclusionReason(selectedOrder?.excludedFromAccountingReason ?? '')
    setRestoreOnDelete(true)
    setSettleReason(selectedOrder?.complimentaryReason ?? '')
  }, [selectedOrder?.id, selectedOrder?.excludedFromAccountingReason, selectedOrder?.complimentaryReason])

  useEscapeKey(Boolean(selectedOrder && orderEditForm), closeOrderEditor)

  if (!selectedOrder || !orderEditForm) {
    return null
  }

  const isNonRevenue = selectedOrder.orderKind === 'InternalUse' || selectedOrder.orderKind === 'Complimentary'
  const settleAsNonRevenue = registerPaymentMethod === 'InternalUse' || registerPaymentMethod === 'Complimentary'

  return (
      <div className="modal-backdrop" onClick={closeOrderEditor}>
        <div
          className="modal-card modal-card--wide"
          role="dialog"
          aria-modal="true"
          aria-labelledby="order-editor-title"
          onClick={(event) => event.stopPropagation()}
        >
          <form onSubmit={handleSaveOrder}>
            <div className="panel-head">
              <div>
                <h3 id="order-editor-title">مدیریت سفارش {selectedOrder.trackingCode}</h3>
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
              <span>پرداخت: {selectedOrder.status}</span>
              <span>نوع سفارش: {orderKindLabel(selectedOrder.orderKind)}</span>
              {isNonRevenue && selectedOrder.complimentaryReason ? (
                <span>بابت: {selectedOrder.complimentaryReason}</span>
              ) : null}
              <span>
                حسابداری: {selectedOrder.isExcludedFromAccounting ? 'خارج از حسابداری' : 'لحاظ در حسابداری'}
              </span>
              <span>آماده‌سازی: {fulfillmentStatusLabel(selectedOrder.fulfillmentStatus)}</span>
              {selectedOrder.deliveryAt ? (
                <span>زمان تحویل: {formatDateTime(selectedOrder.deliveryAt)}</span>
              ) : null}
              <span>مبلغ کل: {formatMoney(selectedOrder.totalAmount)} تومان</span>
              <span>پرداخت شده: {formatMoney(selectedOrder.amountPaid ?? 0)} تومان</span>
              {selectedOrder.taxTotal > 0 ? (
                <span>مالیات: {formatMoney(selectedOrder.taxTotal)} تومان</span>
              ) : null}
              {selectedOrder.deliveryFee > 0 ? (
                <span>هزینه تحویل: {formatMoney(selectedOrder.deliveryFee)} تومان</span>
              ) : null}
              <span>تاریخ: {formatDateTime(selectedOrder.createdAt)}</span>
            </div>

            <div className="order-accounting-box">
              <div>
                <strong>وضعیت در حسابداری و گزارش‌های مالی</strong>
                <p className="table-subtext">
                  {selectedOrder.isExcludedFromAccounting
                    ? `این سفارش از حسابداری و گزارش‌های فروش خارج شده است${selectedOrder.excludedFromAccountingReason ? ` (${selectedOrder.excludedFromAccountingReason})` : ''}.`
                    : 'این سفارش در اسناد حسابداری و گزارش‌های مالی لحاظ می‌شود. در صورت نیاز می‌توانید آن را بدون حذف سفارش از حسابداری خارج کنید.'}
                </p>
              </div>
              {selectedOrder.isExcludedFromAccounting ? (
                <button
                  type="button"
                  className="ghost-button"
                  disabled={saving}
                  onClick={() => handleToggleOrderAccountingExclusion(selectedOrder, false)}
                >
                  بازگرداندن به حسابداری
                </button>
              ) : (
                <div className="order-accounting-controls">
                  <input
                    type="text"
                    placeholder="علت خروج از حسابداری (اختیاری، مثلاً سفارش تستی)"
                    value={exclusionReason}
                    onChange={(event) => setExclusionReason(event.target.value)}
                    disabled={saving}
                  />
                  <button
                    type="button"
                    className="ghost-button"
                    disabled={saving}
                    onClick={() => handleToggleOrderAccountingExclusion(selectedOrder, true, exclusionReason)}
                  >
                    خروج از حسابداری
                  </button>
                </div>
              )}
            </div>
    
            <OrderFulfillmentActions
              order={selectedOrder}
              saving={saving}
              onUpdateStatus={(status) => updateOrderFulfillment(selectedOrder.id, status)}
            />
    
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
                    {line.optionLabels?.length ? (
                      <div className="table-subtext">
                        گزینه‌ها: {line.optionLabels.join('، ')}
                      </div>
                    ) : null}
                    {line.customDrinkSummary?.length ? (
                      <div className="table-subtext">
                        ترکیب سفارشی: {line.customDrinkSummary.join('، ')}
                      </div>
                    ) : null}
                    {line.customDrink ? (
                      <div className="table-subtext">
                        قیمت پایه: {toPersianNumber(line.customDrink.basePrice)} تومان + اجزا:{' '}
                        {toPersianNumber(line.customDrink.componentsTotal)} تومان
                      </div>
                    ) : null}
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
    
            {selectedOrder.taxTotal > 0 ? (
              <p className="table-subtext">
                مالیات بر ارزش افزوده بر اساس درصد مالیات آیتم‌ها محاسبه شده است: {formatMoney(selectedOrder.itemsTotal)} تومان اقلام + {formatMoney(selectedOrder.taxTotal)} مالیات
                {selectedOrder.deliveryFee > 0 ? ` + ${formatMoney(selectedOrder.deliveryFee)} هزینه تحویل` : ''}.
              </p>
            ) : null}

            <div className="order-editor-actions">
              {['PendingPayment', 'PartiallyPaid', 'Held'].includes(selectedOrder.status) ? (
                <div className="invoice-pay-row">
                  <label>
                    <span>روش تسویه</span>
                    <select
                      value={registerPaymentMethod}
                      onChange={(event) => setRegisterPaymentMethod(event.target.value)}
                    >
                      <option value="Cash">نقدی</option>
                      <option value="Card">کارت‌خوان</option>
                      <option value="InternalUse">مصرف داخلی (رایگان)</option>
                      <option value="Complimentary">مهمان / آشنا (رایگان)</option>
                    </select>
                  </label>
                  {settleAsNonRevenue ? (
                    <label>
                      <span>بابت / علت (اختیاری)</span>
                      <input
                        type="text"
                        placeholder="مثلاً پرسنل شیفت صبح یا مهمان"
                        value={settleReason}
                        onChange={(event) => setSettleReason(event.target.value)}
                      />
                    </label>
                  ) : null}
                  <button
                    type="button"
                    disabled={saving}
                    onClick={() =>
                      handleRegisterPayment(
                        settleAsNonRevenue
                          ? { orderKind: registerPaymentMethod, complimentaryReason: settleReason }
                          : { orderKind: 'Standard' },
                      )
                    }
                  >
                    {settleAsNonRevenue ? 'ثبت نهایی (بدون دریافت وجه)' : 'نهایی‌سازی فاکتور و ثبت پرداخت'}
                  </button>
                </div>
              ) : null}
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
              <div className="order-delete-group">
                {selectedOrder.status !== 'Cancelled' ? (
                  <label className="checkbox-row">
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
                  className="ghost-button link-danger"
                  disabled={saving}
                  onClick={() => handleDeleteOrder(selectedOrder, { restoreInventory: restoreOnDelete })}
                >
                  حذف کامل سفارش
                </button>
              </div>
            </div>
          </form>
        </div>
      </div>
  )
}
