import { useState } from 'react'
import { useAdmin } from '../context/AdminContext'
import { hasPermission, PERMISSIONS } from '../utils/adminPermissions'

const API_BASE_URL = import.meta.env.VITE_API_BASE_URL ?? '/api'

const EXPORT_SECTIONS = [
  {
    kind: 'orders',
    title: 'سفارش‌ها و فاکتورها',
    description: 'سفارش‌ها، ردیف آیتم‌ها و پرداخت‌ها — همراه مالیات، هزینه تحویل و مانده',
    permission: PERMISSIONS.orders.view,
    period: true,
  },
  {
    kind: 'accounting',
    title: 'گزارش‌های مالی (برای مراجع قانونی)',
    description: 'دفتر روزنامه + تراز آزمایشی + سود و زیان + ترازنامه در یک فایل',
    permission: 'admin.accounting.view',
    period: true,
  },
  {
    kind: 'purchases',
    title: 'خرید مواد اولیه',
    description: 'فاکتورهای خرید با بهای کل، مالیات خرید و خلاصه دوره',
    permission: 'admin.inventory-purchases.view',
    period: true,
  },
  {
    kind: 'movements',
    title: 'گردش انبار',
    description: 'همه حرکات موجودی: ورود، کسر فروش، بازگردانی، مصرف بسته‌بندی و تولید',
    permission: 'admin.inventory.view',
    period: true,
  },
  {
    kind: 'inventory',
    title: 'موجودی انبار و بهای مواد',
    description: 'پیمانه‌ها و بسته‌بندی با موجودی فعلی، حد هشدار و ارزش ریالی',
    permission: 'admin.inventory.view',
    period: false,
  },
  {
    kind: 'menu-items',
    title: 'آیتم‌های منو و قیمت‌گذاری',
    description: 'قیمت‌ها، درصد مالیات، درصد سود، موجودی و فرمولاسیون هر آیتم',
    permission: 'admin.menu-items.view',
    period: false,
  },
  {
    kind: 'recipes',
    title: 'دستورهای پخت',
    description: 'هر پیمانه با واحد، مقدار و بهای ردیف',
    permission: 'admin.recipes.view',
    period: false,
  },
  {
    kind: 'customers',
    title: 'باشگاه مشتریان',
    description: 'مشتریان یکپارچه با مجموع خرید، مالیات پرداختی و امتیاز وفاداری',
    permission: PERMISSIONS.customers.view,
    period: false,
  },
  {
    kind: 'suppliers',
    title: 'تامین‌کنندگان',
    description: 'اطلاعات تماس و پیمانه‌های هر تامین‌کننده',
    permission: 'admin.suppliers.view',
    period: false,
  },
  {
    kind: 'delivery-methods',
    title: 'روش‌های تحویل',
    description: 'هزینه تحویل و اقلام بسته‌بندی هر روش',
    permission: PERMISSIONS['delivery-methods'].view,
    period: false,
  },
]

const SECTION_LABELS = {
  orders: 'سفارش‌ها',
  accounting: 'گزارش‌های مالی',
  purchases: 'خرید مواد اولیه',
  movements: 'گردش انبار',
  inventory: 'موجودی انبار',
  'menu-items': 'آیتم‌های منو',
  recipes: 'دستور پخت',
  customers: 'باشگاه مشتریان',
  suppliers: 'تامین‌کنندگان',
  'delivery-methods': 'روش‌های تحویل',
}

async function requestExportBlob(path, token) {
  const response = await fetch(`${API_BASE_URL}${path}`, {
    headers: {
      Accept: 'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet',
      ...(token ? { Authorization: `Bearer ${token}` } : {}),
    },
  })

  if (!response.ok) {
    let message = `درخواست با خطای ${response.status} مواجه شد.`
    try {
      const payload = await response.json()
      message = payload.detail || payload.title || message
    } catch {
      // keep fallback message
    }
    throw new Error(message)
  }

  const blob = await response.blob()
  const disposition = response.headers.get('Content-Disposition') ?? ''
  const match = /filename\*?=(?:UTF-8''|")?([^";]+)/i.exec(disposition)
  const fileName = match ? decodeURIComponent(match[1].replaceAll('"', '')) : 'sugarista-export.xlsx'
  return { blob, fileName }
}

function saveBlob(blob, fileName) {
  const url = URL.createObjectURL(blob)
  const link = document.createElement('a')
  link.href = url
  link.download = fileName
  document.body.appendChild(link)
  link.click()
  link.remove()
  URL.revokeObjectURL(url)
}

export function ExportsPage() {
  const { session, getReadableError } = useAdmin()

  const [from, setFrom] = useState('')
  const [to, setTo] = useState('')
  const [busyKind, setBusyKind] = useState(null)
  const [error, setError] = useState('')
  const [message, setMessage] = useState('')

  const download = async (section) => {
    if (!session?.token || busyKind) return
    setBusyKind(section.kind)
    setError('')
    setMessage('')
    try {
      const params = new URLSearchParams()
      if (section.period && from) params.set('from', from)
      if (section.period && to) params.set('to', to)
      const suffix = params.toString() ? `?${params.toString()}` : ''
      const result = await requestExportBlob(`/admin/exports/${section.kind}${suffix}`, session.token)
      saveBlob(result.blob, result.fileName)
      setMessage(`خروجی «${SECTION_LABELS[section.kind] ?? section.kind}» با نام ${result.fileName} دانلود شد.`)
    } catch (requestError) {
      setError(getReadableError(requestError))
    } finally {
      setBusyKind(null)
    }
  }

  const sections = EXPORT_SECTIONS.filter((section) => hasPermission(session, section.permission))

  return (
    <section className="panel-stack">
      <section className="panel">
        <div className="panel-head">
          <div>
            <h3>خروجی اکسل از تمامی بخش‌ها</h3>
            <p>
              فایل‌های Excel دقیق برای ارائه به مراجع قانونی و بک‌آپ‌گیری — هر فایل دارای «برگه شناسنامه» با تاریخ خروجی و هزینه است.
            </p>
          </div>
        </div>

        <div className="exports-period-row">
          <label>
            <span>از تاریخ (برای بخش‌های دوره‌ای)</span>
            <input type="date" value={from} onChange={(event) => setFrom(event.target.value)} />
          </label>
          <label>
            <span>تا تاریخ</span>
            <input type="date" value={to} onChange={(event) => setTo(event.target.value)} />
          </label>
        </div>

        {error ? <p className="feedback feedback-error">{error}</p> : null}
        {message ? <p className="feedback feedback-success">{message}</p> : null}

        <div className="exports-grid">
          {sections.map((section) => (
            <div key={section.kind} className="exports-card">
              <div>
                <strong>{section.title}</strong>
                <p>{section.description}</p>
                {section.period ? <span className="table-subtext">دارای بازه زمانی از/تا</span> : null}
              </div>
              <button
                type="button"
                className="ghost-button"
                disabled={busyKind === section.kind}
                onClick={() => download(section)}
              >
                {busyKind === section.kind ? 'در حال ساخت...' : 'دریافت اکسل'}
              </button>
            </div>
          ))}
        </div>
      </section>
    </section>
  )
}
