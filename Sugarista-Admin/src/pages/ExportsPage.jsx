import { useCallback, useEffect, useRef, useState } from 'react'
import { useAdmin } from '../context/AdminContext'
import { hasPermission, PERMISSIONS } from '../utils/adminPermissions'
import { apiRequest } from '../utils/adminApi'
import { formatDateTime } from '../utils/adminFormatters'
import { ShamsiDatePicker } from '../components/ShamsiDatePicker'

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

const STATUS_LABELS = {
  Queued: 'در صف ساخت',
  Processing: 'در حال ساخت...',
  Completed: 'آماده دانلود',
  Failed: 'ناموفق',
}

const PENDING_STATUSES = new Set(['Queued', 'Processing'])

function formatBytes(bytes) {
  if (!bytes || bytes <= 0) {
    return '—'
  }

  if (bytes < 1024) {
    return `${bytes} بایت`
  }

  return `${(bytes / 1024).toFixed(1)} کیلوبایت`
}

function formatDurationSeconds(completedAt, startedAt) {
  if (!completedAt || !startedAt) {
    return '—'
  }

  const seconds = Math.max(0, Math.round((new Date(completedAt) - new Date(startedAt)) / 1000))
  if (seconds < 60) {
    return `${seconds} ثانیه`
  }

  const minutes = Math.floor(seconds / 60)
  const rest = seconds % 60
  return rest === 0 ? `${minutes} دقیقه` : `${minutes} دقیقه و ${rest} ثانیه`
}

function formatPeriod(fromDate, toDate) {
  if (!fromDate && !toDate) {
    return 'کل دیتا'
  }

  return `از ${fromDate ?? '...'} تا ${toDate ?? '...'}`
}

async function requestExportBlob(path, token) {
  const response = await fetch(`${import.meta.env.VITE_API_BASE_URL ?? '/api'}${path}`, {
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
  const token = session?.token

  const [from, setFrom] = useState('')
  const [to, setTo] = useState('')
  const [busyKind, setBusyKind] = useState(null)
  const [error, setError] = useState('')
  const [message, setMessage] = useState('')
  const [jobs, setJobs] = useState([])
  const [downloadingJobId, setDownloadingJobId] = useState(null)
  const jobsRef = useRef([])
  const jobsLoadedRef = useRef(false)
  const isMountedRef = useRef(true)

  useEffect(() => {
    isMountedRef.current = true
    return () => {
      isMountedRef.current = false
    }
  }, [])

  const fetchJobs = useCallback(async () => {
    if (!token) return
    try {
      const payload = await apiRequest('/admin/exports/jobs', { token })
      if (isMountedRef.current && payload?.items) {
        setJobs(payload.items)
      }
    } catch (requestError) {
      if (isMountedRef.current) {
        setError(getReadableError(requestError))
      }
    }
  }, [getReadableError, token])

  // initial load + polling: fast while queued/processing jobs exist, slow otherwise
  useEffect(() => {
    if (!token) return

    let alive = true
    let timer = null

    const tick = async () => {
      if (!alive) return
      const hasPending = jobsRef.current.some((job) => PENDING_STATUSES.has(job.status))
      const jobsAreStale = !jobsLoadedRef.current

      if (hasPending || jobsAreStale) {
        await fetchJobs()
        jobsLoadedRef.current = true
        if (hasPending && alive) {
          timer = setTimeout(tick, 2500)
          return
        }
      }

      if (alive) {
        timer = setTimeout(tick, 15000)
      }
    }

    tick().catch(() => {})

    return () => {
      alive = false
      if (timer) clearTimeout(timer)
    }
  }, [fetchJobs, token])

  useEffect(() => {
    jobsRef.current = jobs
  }, [jobs])

  const createJob = async (section) => {
    if (!token || busyKind) return
    setBusyKind(section.kind)
    setError('')
    setMessage('')
    try {
      const payload = {}
      if (section.period && from) payload.From = from
      if (section.period && to) payload.To = to
      await apiRequest('/admin/exports/jobs', {
        method: 'POST',
        token,
        body: { kind: section.kind, ...payload },
      })
      jobsLoadedRef.current = false
      await fetchJobs()
      setMessage(
        `درخواست خروجی «${SECTION_LABELS[section.kind] ?? section.kind}» ثبت شد و در پس‌زمینه ساخته می‌شود؛ زمانی که آماده شد از جدول «سوابق» دانلود کنید.`,
      )
    } catch (requestError) {
      setError(getReadableError(requestError))
    } finally {
      setBusyKind(null)
    }
  }

  const downloadJob = async (job) => {
    if (downloadingJobId) return
    setDownloadingJobId(job.id)
    setError('')
    try {
      const result = await requestExportBlob(`/admin/exports/jobs/${job.id}/download`, token)
      saveBlob(result.blob, result.fileName || job.fileName || 'sugarista-export.xlsx')
      setMessage(`فایل ${result.fileName} دانلود شد (${SECTION_LABELS[job.kind] ?? job.kind}).`)
    } catch (requestError) {
      setError(getReadableError(requestError))
    } finally {
      setDownloadingJobId(null)
    }
  }

  const deleteJob = async (job) => {
    if (PENDING_STATUSES.has(job.status)) return
    setError('')
    try {
      await apiRequest(`/admin/exports/jobs/${job.id}`, { method: 'DELETE', token })
      setJobs((current) => current.filter((entry) => entry.id !== job.id))
      setMessage('سابقه خروجی حذف شد و فایل آن از سرور پاک شد.')
    } catch (requestError) {
      setError(getReadableError(requestError))
    }
  }

  const sections = EXPORT_SECTIONS.filter((section) => hasPermission(session, section.permission))
  const canDownload = hasPermission(session, 'admin.accounting.view')
    || EXPORT_SECTIONS.some((section) => hasPermission(session, section.permission))

  if (!canDownload) {
    return (
      <section className="panel-stack">
        <section className="panel">
          <div className="panel-head">
            <div>
              <h3>خروجی اکسل از تمامی بخش‌ها</h3>
            </div>
          </div>
          <p className="table-subtext">شما دسترسی لازم برای این بخش را ندارید.</p>
        </section>
      </section>
    )
  }

  return (
    <section className="panel-stack">
      <section className="panel">
        <div className="panel-head">
          <div>
            <h3>خروجی اکسل از تمامی بخش‌ها</h3>
            <p>
              خروجی‌ها به‌صورت صفی و در پس‌زمینه ساخته می‌شوند — حتی با دیتای بسیار زیاد، سرور هیچ‌گاه timeout نمی‌دهد.
              هر فایل دارای «برگه شناسنامه» با تاریخ خروجی و هزینه است.
            </p>
          </div>
        </div>

        <div className="exports-period-row">
          <label>
            <span>از تاریخ (برای بخش‌های دوره‌ای)</span>
            <ShamsiDatePicker value={from} onChange={setFrom} showHint={false} />
          </label>
          <label>
            <span>تا تاریخ</span>
            <ShamsiDatePicker value={to} onChange={setTo} showHint={false} />
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
                onClick={() => createJob(section)}
              >
                {busyKind === section.kind ? 'در حال ثبت...' : 'ثبت درخواست خروجی'}
              </button>
            </div>
          ))}
        </div>
      </section>

      <section className="panel">
        <div className="panel-head">
          <div>
            <h3>سوابق خروجی‌ها</h3>
            <p>
              فایل‌های در حال ساخت و آماده دانلود — به‌صورت خودکار بروزرسانی می‌شود.
            </p>
          </div>
          <button type="button" className="ghost-button" onClick={() => fetchJobs()}>
            بروزرسانی
          </button>
        </div>

        {jobs.length === 0 ? (
          <p className="table-subtext">هنوز خروجی‌ای ثبت نشده است.</p>
        ) : (
          <div className="table-wrap">
            <table className="data-table">
              <thead>
                <tr>
                  <th>بخش</th>
                  <th>بازه</th>
                  <th>وضعیت</th>
                  <th>ردیف‌ها</th>
                  <th>حجم فایل</th>
                  <th>مدت ساخت</th>
                  <th>تاریخ درخواست</th>
                  <th>عملیات</th>
                </tr>
              </thead>
              <tbody>
                {jobs.map((job) => (
                  <tr key={job.id}>
                    <td>{SECTION_LABELS[job.kind] ?? job.kind}</td>
                    <td>{formatPeriod(job.fromDate, job.toDate)}</td>
                    <td>
                      <span
                        className={
                          job.status === 'Completed'
                            ? 'status-badge status-active'
                            : job.status === 'Failed'
                              ? 'status-badge status-cancelled'
                              : 'status-badge status-pending'
                        }
                      >
                        {STATUS_LABELS[job.status] ?? job.status}
                      </span>
                      {job.errorMessage ? (
                        <p className="table-subtext">{job.errorMessage}</p>
                      ) : null}
                    </td>
                    <td>{job.rowsCount ? Number(job.rowsCount).toLocaleString('fa-IR') : '—'}</td>
                    <td>{formatBytes(job.fileSizeBytes)}</td>
                    <td>{formatDurationSeconds(job.completedAt, job.startedAt)}</td>
                    <td>{formatDateTime(job.createdAt)}</td>
                    <td>
                      <div className="table-actions">
                        <button
                          type="button"
                          className="ghost-button"
                          disabled={job.status !== 'Completed' || downloadingJobId}
                          onClick={() => downloadJob(job)}
                        >
                          {downloadingJobId === job.id ? 'در حال دانلود...' : 'دانلود'}
                        </button>
                        <button
                          type="button"
                          className="ghost-button danger"
                          disabled={PENDING_STATUSES.has(job.status)}
                          onClick={() => deleteJob(job)}
                        >
                          حذف
                        </button>
                      </div>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </section>
    </section>
  )
}
