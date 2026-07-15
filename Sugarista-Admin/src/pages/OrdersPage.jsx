import { useAdmin } from '../context/AdminContext'
import { PaginatedAdminTable } from '../components/PaginatedAdminTable'

export function OrdersPage() {
  const {
    session,
    tableReloadToken,
    bumpTableReload,
    orderColumns,
  } = useAdmin()

  return (
      <section className="panel-stack">
        <section className="panel">
          <div className="panel-head">
            <div>
              <h3>سفارش‌های آنلاین</h3>
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
