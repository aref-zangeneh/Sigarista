import { DataTable, formatMoney } from './DataTable'
import { OrderFulfillmentActions, fulfillmentStatusLabel } from './OrderAlertsBar'
import { toPersianNumber, formatDateTime } from '../utils/adminFormatters'
import { useAdmin } from '../context/AdminContext'

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
  } = admin

  if (!selectedOrder || !orderEditForm) {
    return null
  }

  return (
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
              <span>پرداخت: {selectedOrder.status}</span>
              <span>آماده‌سازی: {fulfillmentStatusLabel(selectedOrder.fulfillmentStatus)}</span>
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
                    <span>روش پرداخت</span>
                    <select
                      value={registerPaymentMethod}
                      onChange={(event) => setRegisterPaymentMethod(event.target.value)}
                    >
                      <option value="Cash">نقدی</option>
                      <option value="Card">کارت</option>
                    </select>
                  </label>
                  <button type="button" disabled={saving} onClick={handleRegisterPayment}>
                    نهایی‌سازی فاکتور و ثبت پرداخت
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
            </div>
          </form>
        </div>
      </div>
  )
}
