import { useCallback, useEffect, useState } from 'react'
import { useAdmin } from '../context/AdminContext'
import { formatPrice } from '../utils/posCart'
import { formatDateTime, toPersianNumber } from '../utils/adminFormatters'

const CHANNEL_LABELS = {
  pos: 'صندوق فروش',
  online: 'سفارش آنلاین',
  admin: 'ویرایش ادمین',
}

const STATUS_LABELS = {
  PendingPayment: 'در انتظار پرداخت',
  PartiallyPaid: 'پرداخت جزئی',
  Paid: 'پرداخت‌شده',
  Failed: 'ناموفق',
  Cancelled: 'لغوشده',
  Held: 'نگه‌داشته',
}

function channelLabel(channel) {
  return CHANNEL_LABELS[channel] ?? channel
}

function statusLabel(status) {
  return STATUS_LABELS[status] ?? status
}

export function CustomersPage() {
  const { session, apiRequest, getReadableError } = useAdmin()

  const [customers, setCustomers] = useState([])
  const [totalCount, setTotalCount] = useState(0)
  const [page, setPage] = useState(1)
  const [pageSize] = useState(25)
  const [search, setSearch] = useState('')
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState('')
  const [message, setMessage] = useState('')
  const [detail, setDetail] = useState(null)
  const [detailLoading, setDetailLoading] = useState(false)
  const [savingId, setSavingId] = useState(null)

  const loadCustomers = useCallback(
    async (nextPage = page, nextSearch = search) => {
      if (!session?.token) return
      setLoading(true)
      setError('')
      try {
        const params = new URLSearchParams({
          page: String(nextPage),
          pageSize: String(pageSize),
        })
        if (nextSearch.trim()) {
          params.set('search', nextSearch.trim())
        }
        const data = await apiRequest(`/admin/customers?${params.toString()}`, { token: session.token })
        setCustomers(Array.isArray(data.items) ? data.items : [])
        setTotalCount(data.totalCount ?? (Array.isArray(data.items) ? data.items.length : 0))
      } catch (requestError) {
        setError(getReadableError(requestError))
      } finally {
        setLoading(false)
      }
    },
    // eslint-disable-next-line react-hooks/exhaustive-deps
    [session?.token, page, search, pageSize],
  )

  useEffect(() => {
    loadCustomers(page, search)
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [page])

  const handleSearch = (event) => {
    event.preventDefault()
    setPage(1)
    loadCustomers(1, search)
  }

  const openDetail = async (customer) => {
    setDetailLoading(true)
    setError('')
    try {
      const data = await apiRequest(`/admin/customers/${customer.id}`, { token: session.token })
      setDetail(data)
    } catch (requestError) {
      setError(getReadableError(requestError))
    } finally {
      setDetailLoading(false)
    }
  }

  const toggleActive = async (customer) => {
    setSavingId(customer.id)
    setError('')
    setMessage('')
    try {
      await apiRequest(`/admin/customers/${customer.id}`, {
        method: 'PUT',
        token: session.token,
        body: {
          id: customer.id,
          persianName: customer.persianName,
          email: customer.email,
          notes: customer.notes,
          isActive: !customer.isActive,
        },
      })
      setMessage(customer.isActive ? 'مشتری غیرفعال شد.' : 'مشتری فعال شد.')
      loadCustomers()
    } catch (requestError) {
      setError(getReadableError(requestError))
    } finally {
      setSavingId(null)
    }
  }

  const totalPages = Math.max(1, Math.ceil(totalCount / pageSize))

  return (
    <section className="panel-stack">
      {detail ? (
        <section className="panel customer-detail-panel">
          <div className="panel-head">
            <div>
              <h3>پرونده مشتری: {detail.persianName}</h3>
              <p>
                {toPersianNumber(detail.phoneNumber)} — {channelLabel(detail.firstChannel)} تا {channelLabel(detail.lastChannel)}
              </p>
            </div>
            <button type="button" className="ghost-button" onClick={() => setDetail(null)}>
              بستن پرونده
            </button>
          </div>
          <div className="stat-grid">
            <div className="stat-card">
              <span>مجموع خرید (پرداخت‌شده)</span>
              <strong>{formatPrice(detail.totalPaidAmount)} تومان</strong>
            </div>
            <div className="stat-card">
              <span>سفارش‌های پرداخت‌شده</span>
              <strong>{toPersianNumber(detail.paidOrders)}</strong>
            </div>
            <div className="stat-card">
              <span>میانگین هر سفارش</span>
              <strong>{formatPrice(detail.averageOrderValue)} تومان</strong>
            </div>
            <div className="stat-card">
              <span>امتیاز باشگاه مشتریان</span>
              <strong>{toPersianNumber(detail.loyaltyPoints)}</strong>
            </div>
          </div>
          <h4>سفارش‌های اخیر</h4>
          <table className="admin-table">
            <thead>
              <tr>
                <th>کد پیگیری</th>
                <th>مبلغ کل</th>
                <th>مالیات</th>
                <th>هزینه تحویل</th>
                <th>وضعیت</th>
                <th>کانال</th>
                <th>تاریخ</th>
              </tr>
            </thead>
            <tbody>
              {(detail.recentOrders ?? []).map((order) => (
                <tr key={order.orderId}>
                  <td>{order.trackingCode}</td>
                  <td>{formatPrice(order.totalAmount)}</td>
                  <td>{formatPrice(order.taxTotal)}</td>
                  <td>{formatPrice(order.deliveryFee)}</td>
                  <td>{statusLabel(order.status)}</td>
                  <td>
                    {order.channel === 'Pos' ? 'صندوق' : order.channel === 'Online' ? 'آنلاین' : order.channel}
                    {order.serviceType ? ` (${order.serviceType === 'InPerson' ? 'حضوری' : order.serviceType === 'Online' ? 'آنلاین' : order.serviceType})` : ''}
                  </td>
                  <td>{formatDateTime(order.createdAt)}</td>
                </tr>
              ))}
              {(detail.recentOrders ?? []).length === 0 && (
                <tr>
                  <td colSpan={7}>هنوز سفارشی برای این مشتری ثبت نشده است.</td>
                </tr>
              )}
            </tbody>
          </table>
        </section>
      ) : null}

      <section className="panel">
        <div className="panel-head">
          <div>
            <h3>باشگاه مشتریان</h3>
            <p>مشتریان یکپارچه از همه کانال‌ها (صندوق فروش، سفارش آنلاین، ویرایش ادمین) — همراه با آمادۀ خرید و امتیاز وفاداری.</p>
          </div>
          <form className="inline-form" onSubmit={handleSearch}>
            <input
              type="search"
              value={search}
              placeholder="جستجو: نام، شماره تماس یا ایمیل..."
              onChange={(event) => setSearch(event.target.value)}
            />
            <button type="submit" className="ghost-button">
              جستجو
            </button>
            <button type="button" className="ghost-button" onClick={() => loadCustomers()}>
              بروزرسانی
            </button>
          </form>
        </div>

        {error ? <p className="feedback feedback-error">{error}</p> : null}
        {message ? <p className="feedback feedback-success">{message}</p> : null}
        {loading ? <p>در حال بارگذاری مشتریان...</p> : null}

        {!loading && customers.length === 0 ? (
          <p>هنوز مشتری‌ای ثبت نشده است؛ با اولین سفارش صندوق فروش یا سفارش آنلاین، پروندهٔ مشتری خودکار ساخته می‌شود.</p>
        ) : null}

        {customers.length > 0 ? (
          <table className="admin-table">
            <thead>
              <tr>
                <th>مشتری</th>
                <th>شماره تماس</th>
                <th>کانال‌ها</th>
                <th>سفارش‌ها</th>
                <th>مجموع خرید</th>
                <th>میانگین سفارش</th>
                <th>مالیات پرداختی</th>
                <th>امتیاز</th>
                <th>آخرین خرید</th>
                <th>وضعیت</th>
                <th>عملیات</th>
              </tr>
            </thead>
            <tbody>
              {customers.map((customer) => (
                <tr key={customer.id} className={customer.isActive ? '' : 'row-muted'}>
                  <td>{customer.persianName}</td>
                  <td>{toPersianNumber(customer.phoneNumber)}</td>
                  <td>
                    {channelLabel(customer.firstChannel)}
                    {customer.lastChannel !== customer.firstChannel ? ` → ${channelLabel(customer.lastChannel)}` : ''}
                  </td>
                  <td>
                    {toPersianNumber(customer.paidOrders)} از {toPersianNumber(customer.totalOrders)}
                  </td>
                  <td>{formatPrice(customer.totalPaidAmount)}</td>
                  <td>{formatPrice(customer.averageOrderValue)}</td>
                  <td>{formatPrice(customer.totalTax)}</td>
                  <td className="loyalty-points">{toPersianNumber(customer.loyaltyPoints)}</td>
                  <td>{formatDateTime(customer.lastOrderAt)}</td>
                  <td>{customer.isActive ? 'فعال' : 'غیرفعال'}</td>
                  <td className="table-actions">
                    <button type="button" className="ghost-button" onClick={() => openDetail(customer)}>
                      پرونده
                    </button>
                    <button
                      type="button"
                      className="ghost-button"
                      disabled={savingId === customer.id}
                      onClick={() => toggleActive(customer)}
                    >
                      {customer.isActive ? 'غیرفعال' : 'فعال'}
                    </button>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        ) : null}

        {totalPages > 1 ? (
          <div className="pagination-row">
            <button type="button" className="ghost-button" disabled={page <= 1} onClick={() => setPage(page - 1)}>
              قبلی
            </button>
            <span>
              صفحهٔ {toPersianNumber(page)} از {toPersianNumber(totalPages)} — مجموع {toPersianNumber(totalCount)} مشتری
            </span>
            <button
              type="button"
              className="ghost-button"
              disabled={page >= totalPages}
              onClick={() => setPage(page + 1)}
            >
              بعدی
            </button>
          </div>
        ) : null}

        {detailLoading ? <p>در حال دریافت پرونده مشتری...</p> : null}
      </section>
    </section>
  )
}
