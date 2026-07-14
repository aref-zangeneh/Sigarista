import { formatMoney } from './DataTable'
import './DashboardOverview.css'

function toPersianNumber(value) {
  return new Intl.NumberFormat('fa-IR').format(value ?? 0)
}

function severityClass(severity) {
  if (severity === 'critical') return 'dashboard-alert dashboard-alert--critical'
  if (severity === 'warning') return 'dashboard-alert dashboard-alert--warning'
  if (severity === 'success') return 'dashboard-alert dashboard-alert--success'
  return 'dashboard-alert dashboard-alert--info'
}

function StatCard({ label, value, hint, accent }) {
  return (
    <article className={`dashboard-stat ${accent ? `dashboard-stat--${accent}` : ''}`}>
      <span>{label}</span>
      <strong>{value}</strong>
      {hint ? <small>{hint}</small> : null}
    </article>
  )
}

export function DashboardOverview({ dashboard, loading, error, onNavigate, onRefresh }) {
  if (loading && !dashboard) {
    return <p className="feedback">در حال بارگذاری داشبورد...</p>
  }

  if (error) {
    return (
      <section className="panel">
        <p className="feedback feedback-error">{error}</p>
        <button type="button" onClick={onRefresh}>تلاش مجدد</button>
      </section>
    )
  }

  if (!dashboard) {
    return (
      <section className="panel">
        <p className="feedback">داده‌ای برای نمایش داشبورد موجود نیست.</p>
        <button type="button" onClick={onRefresh}>تلاش مجدد</button>
      </section>
    )
  }

  const { sales, finance, orders, customers, profitByCategory, topSoldItems, leastSoldItems, lowStockItems, warehouseInventory, peakTimes, debtors, creditors, alerts } = dashboard

  return (
    <section className="dashboard">
      <div className="dashboard-head">
        <div>
          <h3>داشبورد مدیریتی</h3>
          <p>خلاصه فروش، مالی، موجودی و مشتریان</p>
        </div>
        <button type="button" className="ghost-button" onClick={onRefresh}>
          بروزرسانی داشبورد
        </button>
      </div>

      <section className="dashboard-section">
        <h4>فروش</h4>
        <div className="dashboard-stat-grid">
          <StatCard label="فروش امروز" value={`${formatMoney(sales.dailySales)} تومان`} hint={`${toPersianNumber(sales.dailyOrderCount)} سفارش`} accent="sales" />
          <StatCard label="فروش هفتگی" value={`${formatMoney(sales.weeklySales)} تومان`} hint={`${toPersianNumber(sales.weeklyOrderCount)} سفارش`} />
          <StatCard label="فروش ماهانه" value={`${formatMoney(sales.monthlySales)} تومان`} hint={`${toPersianNumber(sales.monthlyOrderCount)} سفارش`} />
          <StatCard label="میانگین هر سفارش" value={`${formatMoney(orders.averageOrderValue)} تومان`} hint={`${toPersianNumber(orders.paidOrders)} سفارش پرداخت‌شده`} />
        </div>
      </section>

      <section className="dashboard-section">
        <h4>مالی</h4>
        <div className="dashboard-stat-grid">
          <StatCard label="موجودی نقد" value={`${formatMoney(finance.cashBalance)} تومان`} accent="cash" />
          <StatCard label="بدهکاران (دریافتنی)" value={`${formatMoney(finance.totalReceivables)} تومان`} hint={`${toPersianNumber(debtors.length)} مورد فعال`} accent="debt" />
          <StatCard label="بستانکاران (پرداختنی)" value={`${formatMoney(finance.totalPayables)} تومان`} hint="برآورد خرید مواد کم‌موجود" accent="credit" />
        </div>
      </section>

      <section className="dashboard-section">
        <h4>سفارش‌ها و مشتریان</h4>
        <div className="dashboard-stat-grid dashboard-stat-grid--compact">
          <StatCard label="کل سفارش‌ها" value={toPersianNumber(orders.totalOrders)} />
          <StatCard label="در انتظار پرداخت" value={toPersianNumber(orders.pendingOrders)} />
          <StatCard label="پرداخت جزئی" value={toPersianNumber(orders.partiallyPaidOrders)} />
          <StatCard label="مشتریان جدید (ماه)" value={toPersianNumber(customers.newCustomers)} />
          <StatCard label="مشتریان وفادار (۳+ سفارش)" value={toPersianNumber(customers.loyalCustomers)} />
          <StatCard label="کل مشتریان یکتا" value={toPersianNumber(customers.totalUniqueCustomers)} />
        </div>
      </section>

      <div className="dashboard-columns">
        <section className="panel dashboard-panel">
          <div className="panel-head">
            <h4>سود به تفکیک دسته‌بندی</h4>
          </div>
          {profitByCategory.length === 0 ? (
            <p className="dashboard-empty">داده فروش پرداخت‌شده‌ای وجود ندارد.</p>
          ) : (
            <div className="dashboard-table-scroll">
              <table className="dashboard-table">
                <thead>
                  <tr>
                    <th>دسته</th>
                    <th>فروش</th>
                    <th>هزینه</th>
                    <th>سود</th>
                    <th>حاشیه</th>
                  </tr>
                </thead>
                <tbody>
                  {profitByCategory.map((row) => (
                    <tr key={row.categoryId}>
                      <td>{row.categoryName}</td>
                      <td>{formatMoney(row.revenue)}</td>
                      <td>{formatMoney(row.cost)}</td>
                      <td className={row.profit >= 0 ? 'dashboard-positive' : 'dashboard-negative'}>{formatMoney(row.profit)}</td>
                      <td>{toPersianNumber(row.marginPercent)}٪</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          )}
          <p className="dashboard-note">سود بر اساس بهای واحد مواد اولیه در دستور پخت محاسبه می‌شود.</p>
        </section>

        <section className="panel dashboard-panel">
          <div className="panel-head">
            <h4>ساعات اوج فروش</h4>
          </div>
          {peakTimes.length === 0 ? (
            <p className="dashboard-empty">داده‌ای ثبت نشده.</p>
          ) : (
            <ul className="dashboard-peak-list">
              {peakTimes.map((peak) => (
                <li key={peak.hour}>
                  <span>{peak.label}</span>
                  <strong>{toPersianNumber(peak.orderCount)} سفارش</strong>
                  <small>{formatMoney(peak.revenue)} تومان</small>
                </li>
              ))}
            </ul>
          )}
        </section>
      </div>

      <div className="dashboard-columns">
        <section className="panel dashboard-panel">
          <div className="panel-head">
            <h4>پرفروش‌ترین آیتم‌ها</h4>
          </div>
          {topSoldItems.length === 0 ? (
            <p className="dashboard-empty">داده‌ای ثبت نشده.</p>
          ) : (
            <ul className="dashboard-rank-list">
              {topSoldItems.map((item, index) => (
                <li key={item.menuItemId}>
                  <span className="dashboard-rank">{toPersianNumber(index + 1)}</span>
                  <div>
                    <strong>{item.persianName}</strong>
                    <small className="ltr-text">{item.englishName}</small>
                  </div>
                  <div className="dashboard-rank-meta">
                    <span>{toPersianNumber(item.quantitySold)} عدد</span>
                    <small>{formatMoney(item.revenue)}</small>
                  </div>
                </li>
              ))}
            </ul>
          )}
        </section>

        <section className="panel dashboard-panel">
          <div className="panel-head">
            <h4>کم‌فروش‌ترین آیتم‌ها</h4>
          </div>
          {leastSoldItems.length === 0 ? (
            <p className="dashboard-empty">داده‌ای ثبت نشده.</p>
          ) : (
            <ul className="dashboard-rank-list">
              {leastSoldItems.map((item, index) => (
                <li key={item.menuItemId}>
                  <span className="dashboard-rank">{toPersianNumber(index + 1)}</span>
                  <div>
                    <strong>{item.persianName}</strong>
                    <small className="ltr-text">{item.englishName}</small>
                  </div>
                  <div className="dashboard-rank-meta">
                    <span>{toPersianNumber(item.quantitySold)} عدد</span>
                    <small>{formatMoney(item.revenue)}</small>
                  </div>
                </li>
              ))}
            </ul>
          )}
        </section>
      </div>

      <div className="dashboard-columns">
        <section className="panel dashboard-panel">
          <div className="panel-head">
            <h4>مواد اولیه کم‌موجود</h4>
            <button type="button" className="link-button" onClick={() => onNavigate?.('inventory')}>مدیریت انبار</button>
          </div>
          {lowStockItems.length === 0 ? (
            <p className="dashboard-empty">همه مواد در سطح مناسب هستند.</p>
          ) : (
            <ul className="dashboard-stock-list">
              {lowStockItems.slice(0, 8).map((item) => (
                <li key={item.ingredientId}>
                  <div>
                    <strong>{item.persianName}</strong>
                    <small>{item.inventoryLocation}</small>
                  </div>
                  <span className="dashboard-stock-qty">
                    {toPersianNumber(item.currentQuantity)} / {toPersianNumber(item.minAlertQuantity)} {item.unit}
                  </span>
                </li>
              ))}
            </ul>
          )}
        </section>

        <section className="panel dashboard-panel">
          <div className="panel-head">
            <h4>موجودی انبار</h4>
          </div>
          {warehouseInventory.length === 0 ? (
            <p className="dashboard-empty">انبار خالی است.</p>
          ) : (
            <ul className="dashboard-warehouse-list">
              {warehouseInventory.map((warehouse) => (
                <li key={warehouse.location}>
                  <strong>{warehouse.locationLabel}</strong>
                  <span>{toPersianNumber(warehouse.itemCount)} قلم</span>
                  <small>{toPersianNumber(warehouse.lowStockCount)} کم‌موجود</small>
                </li>
              ))}
            </ul>
          )}
        </section>
      </div>

      <div className="dashboard-columns">
        <section className="panel dashboard-panel">
          <div className="panel-head">
            <h4>بدهکاران</h4>
          </div>
          {debtors.length === 0 ? (
            <p className="dashboard-empty">بدهی فعالی وجود ندارد.</p>
          ) : (
            <ul className="dashboard-finance-list">
              {debtors.map((debtor) => (
                <li key={`${debtor.trackingCode}-${debtor.customerPhone}`}>
                  <div>
                    <strong>{debtor.customerName}</strong>
                    <small className="ltr-text">{debtor.trackingCode} · {debtor.customerPhone}</small>
                  </div>
                  <span>{formatMoney(debtor.outstandingAmount)}</span>
                </li>
              ))}
            </ul>
          )}
        </section>

        <section className="panel dashboard-panel">
          <div className="panel-head">
            <h4>بستانکاران (برآورد)</h4>
          </div>
          {creditors.length === 0 ? (
            <p className="dashboard-empty">بدهی تأمین‌کننده‌ای ثبت نشده.</p>
          ) : (
            <ul className="dashboard-finance-list">
              {creditors.map((creditor, index) => (
                <li key={`${creditor.description}-${index}`}>
                  <div>
                    <strong>{creditor.supplierName}</strong>
                    <small>{creditor.description}</small>
                  </div>
                  <span>{formatMoney(creditor.amount)}</span>
                </li>
              ))}
            </ul>
          )}
        </section>
      </div>

      <section className="panel dashboard-panel">
        <div className="panel-head">
          <h4>هشدارهای مدیریتی</h4>
        </div>
        <div className="dashboard-alerts">
          {alerts.map((alert, index) => (
            <article key={`${alert.title}-${index}`} className={severityClass(alert.severity)}>
              <div>
                <strong>{alert.title}</strong>
                <p>{alert.message}</p>
              </div>
              {alert.actionTab ? (
                <button
                  type="button"
                  className="link-button"
                  onClick={() =>
                    onNavigate?.(
                      alert.actionTab,
                      alert.actionTab === 'inventory-purchases' ? { expiringOnly: true } : undefined,
                    )
                  }
                >
                  مشاهده
                </button>
              ) : null}
            </article>
          ))}
        </div>
      </section>
    </section>
  )
}
