import { useCallback, useEffect, useMemo, useState } from 'react'
import { useAdmin } from '../context/AdminContext'
import { apiRequest, API_BASE_URL } from '../utils/adminApi'
import { formatDateTime, toPersianNumber } from '../utils/adminFormatters'
import { ShamsiDatePicker } from '../components/ShamsiDatePicker'

const KIND_OPTIONS = [
  { value: '', label: 'همهٔ انواع' },
  { value: 'Code', label: 'کد تخفیف' },
  { value: 'Tier', label: 'سطح عضویت' },
  { value: 'Group', label: 'گروه مشتری' },
  { value: 'Birthday', label: 'تولد' },
  { value: 'Cup', label: 'لیوان شخصی' },
  { value: 'Subscription', label: 'اشتراک ماهانه' },
  { value: 'LoyaltyPoints', label: 'امتیاز باشگاه' },
  { value: 'StampCard', label: 'کارت مهر' },
  { value: 'Referral', label: 'معرفی دوستان' },
]

const CHANNEL_OPTIONS = [
  { value: '', label: 'همهٔ کانال‌ها' },
  { value: 'Online', label: 'آنلاین' },
  { value: 'Pos', label: 'حضوری (POS)' },
]

const FREQUENCY_OPTIONS = [
  { value: 'Daily', label: 'روزانه (دیروز)' },
  { value: 'Weekly', label: 'هفتگی (۷ روز اخیر)' },
  { value: 'Monthly', label: 'ماهانه (ماه گذشته)' },
]

const WEEK_DAYS = [
  { value: 0, label: 'یکشنبه' },
  { value: 1, label: 'دوشنبه' },
  { value: 2, label: 'سه‌شنبه' },
  { value: 3, label: 'چهارشنبه' },
  { value: 4, label: 'پنجشنبه' },
  { value: 5, label: 'جمعه' },
  { value: 6, label: 'شنبه' },
]

const TEHRAN_OFFSET = '+03:30'

function startOfDayIso(date) {
  return date ? `${date}T00:00:00${TEHRAN_OFFSET}` : null
}

function endOfDayIso(date) {
  return date ? `${date}T23:59:59${TEHRAN_OFFSET}` : null
}

function formatToman(value) {
  return `${Number(value || 0).toLocaleString('fa-IR')} تومان`
}

async function requestReportBlob(path, token, body) {
  const response = await fetch(`${API_BASE_URL}${path}`, {
    method: 'POST',
    headers: {
      'Content-Type': 'application/json',
      ...(token ? { Authorization: `Bearer ${token}` } : {}),
    },
    body: JSON.stringify(body),
  })

  if (!response.ok) {
    let message = `درخواست با خطای ${response.status} مواجه شد.`
    try {
      const payload = await response.json()
      message = payload.detail || payload.title || message
    } catch {
      // keep fallback
    }
    throw new Error(message)
  }

  const disposition = response.headers.get('Content-Disposition') || ''
  const match = disposition.match(/filename\*?=(?:UTF-8'')?"?([^";]+)"?/i)
  return {
    blob: await response.blob(),
    fileName: match ? decodeURIComponent(match[1]) : 'discount-report.xlsx',
  }
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

export function DiscountReportsPage() {
  const { session, getReadableError } = useAdmin()
  const token = session?.token

  const [from, setFrom] = useState('')
  const [to, setTo] = useState('')
  const [discountId, setDiscountId] = useState('')
  const [categoryId, setCategoryId] = useState('')
  const [kind, setKind] = useState('')
  const [channel, setChannel] = useState('')
  const [code, setCode] = useState('')
  const [onlyUnscoped, setOnlyUnscoped] = useState(false)
  const [columns, setColumns] = useState([])
  const [availableColumns, setAvailableColumns] = useState([])
  const [discounts, setDiscounts] = useState([])
  const [categories, setCategories] = useState([])

  const [report, setReport] = useState(null)
  const [loading, setLoading] = useState(false)
  const [downloading, setDownloading] = useState(false)
  const [error, setError] = useState('')
  const [message, setMessage] = useState('')

  const [schedules, setSchedules] = useState([])
  const [scheduleForm, setScheduleForm] = useState(null)
  const [savingSchedule, setSavingSchedule] = useState(false)
  const [sendingId, setSendingId] = useState(null)

  const loadReferenceData = useCallback(async () => {
    if (!token) return
    try {
      const [columnsData, discountsData, categoriesData] = await Promise.all([
        apiRequest('/admin/discount-reports/columns', { token }),
        apiRequest('/admin/discounts?page=1&pageSize=200', { token }),
        apiRequest('/admin/categories/options', { token }),
      ])
      setAvailableColumns(Array.isArray(columnsData) ? columnsData : [])
      setColumns((current) => (current.length ? current : (columnsData ?? []).map((column) => column.key)))
      setDiscounts(Array.isArray(discountsData?.items) ? discountsData.items : Array.isArray(discountsData) ? discountsData : [])
      setCategories(Array.isArray(categoriesData) ? categoriesData : [])
    } catch (requestError) {
      setError(getReadableError(requestError))
    }
  }, [getReadableError, token])

  const loadSchedules = useCallback(async () => {
    if (!token) return
    try {
      const data = await apiRequest('/admin/discount-reports/schedules', { token })
      setSchedules(Array.isArray(data) ? data : [])
    } catch (requestError) {
      setError(getReadableError(requestError))
    }
  }, [getReadableError, token])

  useEffect(() => {
    loadReferenceData()
    loadSchedules()
  }, [loadReferenceData, loadSchedules])

  const filterPayload = useMemo(
    () => ({
      from: startOfDayIso(from),
      to: endOfDayIso(to),
      discountId: discountId || null,
      categoryId: categoryId || null,
      onlyUnscopedCategories: onlyUnscoped,
      kind: kind || null,
      channel: channel || null,
      code: code.trim() || null,
      columns,
    }),
    [categoryId, channel, code, columns, discountId, from, kind, onlyUnscoped, to],
  )

  const runPreview = async () => {
    if (!token || loading) return
    setLoading(true)
    setError('')
    setMessage('')
    try {
      const data = await apiRequest('/admin/discount-reports/preview', {
        method: 'POST',
        token,
        body: filterPayload,
      })
      setReport(data)
      setMessage('گزارش با فیلترهای انتخاب‌شده ساخته شد.')
    } catch (requestError) {
      setError(getReadableError(requestError))
    } finally {
      setLoading(false)
    }
  }

  const downloadExcel = async () => {
    if (!token || downloading) return
    setDownloading(true)
    setError('')
    setMessage('')
    try {
      const { blob, fileName } = await requestReportBlob('/admin/discount-reports/export', token, filterPayload)
      saveBlob(blob, fileName)
      setMessage(`فایل ${fileName} دانلود شد.`)
    } catch (requestError) {
      setError(getReadableError(requestError))
    } finally {
      setDownloading(false)
    }
  }

  const toggleColumn = (key) => {
    setColumns((current) =>
      current.includes(key) ? current.filter((entry) => entry !== key) : [...current, key],
    )
  }

  const openNewSchedule = () => {
    setScheduleForm({
      id: '',
      name: 'گزارش عملکرد تخفیف‌ها',
      kind: 'discount-report',
      frequency: 'Weekly',
      hour: 9,
      dayOfWeek: 6,
      dayOfMonth: 1,
      recipientEmails: '',
      isEnabled: true,
    })
    setMessage('')
    setError('')
  }

  const openEditSchedule = (schedule) => {
    setScheduleForm({
      id: schedule.id,
      name: schedule.name ?? '',
      kind: schedule.kind ?? 'discount-report',
      frequency: schedule.frequency ?? 'Weekly',
      hour: schedule.hour ?? 9,
      dayOfWeek: schedule.dayOfWeek ?? 6,
      dayOfMonth: schedule.dayOfMonth ?? 1,
      recipientEmails: schedule.recipientEmails ?? '',
      isEnabled: schedule.isEnabled !== false,
    })
    setMessage('')
    setError('')
  }

  const updateScheduleField = (field, value) =>
    setScheduleForm((current) => (current ? { ...current, [field]: value } : current))

  const saveSchedule = async () => {
    if (!token || !scheduleForm || savingSchedule) return
    if (!scheduleForm.name.trim()) {
      setError('نام گزارش را وارد کنید.')
      return
    }
    if (!scheduleForm.recipientEmails.trim()) {
      setError('حداقل یک ایمیل دریافت‌کننده وارد کنید.')
      return
    }

    setSavingSchedule(true)
    setError('')
    setMessage('')
    try {
      const optionsJson = JSON.stringify({
        columns,
        discountId: discountId || null,
        categoryId: categoryId || null,
        onlyUnscopedCategories: onlyUnscoped,
        kind: kind || null,
        channel: channel || null,
        code: code.trim() || null,
      })
      const payload = {
        id: scheduleForm.id || null,
        name: scheduleForm.name.trim(),
        kind: 'discount-report',
        frequency: scheduleForm.frequency,
        hour: Number(scheduleForm.hour) || 0,
        dayOfWeek: scheduleForm.frequency === 'Weekly' ? Number(scheduleForm.dayOfWeek) : null,
        dayOfMonth: scheduleForm.frequency === 'Monthly' ? Number(scheduleForm.dayOfMonth) : null,
        recipientEmails: scheduleForm.recipientEmails.trim(),
        optionsJson,
        isEnabled: scheduleForm.isEnabled,
      }

      if (scheduleForm.id) {
        await apiRequest(`/admin/discount-reports/schedules/${scheduleForm.id}`, {
          method: 'PUT',
          token,
          body: payload,
        })
      } else {
        await apiRequest('/admin/discount-reports/schedules', { method: 'POST', token, body: payload })
      }

      setScheduleForm(null)
      await loadSchedules()
      setMessage('گزارش دوره‌ای ذخیره شد و از همین لحظه بدون ری‌استارت فعال است.')
    } catch (requestError) {
      setError(getReadableError(requestError))
    } finally {
      setSavingSchedule(false)
    }
  }

  const deleteSchedule = async (schedule) => {
    if (!token) return
    setError('')
    try {
      await apiRequest(`/admin/discount-reports/schedules/${schedule.id}`, { method: 'DELETE', token })
      await loadSchedules()
      setMessage('گزارش دوره‌ای حذف شد.')
    } catch (requestError) {
      setError(getReadableError(requestError))
    }
  }

  const sendNow = async (schedule) => {
    if (!token || sendingId) return
    setSendingId(schedule.id)
    setError('')
    setMessage('')
    try {
      await apiRequest(`/admin/discount-reports/schedules/${schedule.id}/send-now`, {
        method: 'POST',
        token,
      })
      await loadSchedules()
      setMessage('گزارش همین حالا برای دریافت‌کنندگان ارسال شد.')
    } catch (requestError) {
      setError(getReadableError(requestError))
    } finally {
      setSendingId(null)
    }
  }

  const toggleEnabled = async (schedule) => {
    if (!token) return
    setError('')
    try {
      await apiRequest(`/admin/discount-reports/schedules/${schedule.id}`, {
        method: 'PUT',
        token,
        body: {
          id: schedule.id,
          name: schedule.name,
          kind: schedule.kind,
          frequency: schedule.frequency,
          hour: schedule.hour,
          dayOfWeek: schedule.dayOfWeek,
          dayOfMonth: schedule.dayOfMonth,
          recipientEmails: schedule.recipientEmails,
          optionsJson: schedule.optionsJson,
          isEnabled: !(schedule.isEnabled !== false),
        },
      })
      await loadSchedules()
    } catch (requestError) {
      setError(getReadableError(requestError))
    }
  }

  return (
    <section className="panel-stack">
      <section className="panel">
        <div className="panel-head">
          <div>
            <h3>گزارش عملکرد تخفیف‌ها</h3>
            <p>
              تفکیک هر تخفیف، بازهٔ زمانی و دسته‌بندی — با انتخاب ستون‌ها، فیلترها، دانلود Excel و ارسال
              خودکار دوره‌ای به ایمیل. فقط سفارش‌های پرداخت‌شده در گزارش محاسبه می‌شوند.
            </p>
          </div>
        </div>

        <div className="field-grid">
          <label>
            <span>از تاریخ</span>
            <ShamsiDatePicker value={from} onChange={setFrom} showHint={false} />
          </label>
          <label>
            <span>تا تاریخ</span>
            <ShamsiDatePicker value={to} onChange={setTo} showHint={false} />
          </label>
          <label>
            <span>تخفیف</span>
            <select value={discountId} onChange={(event) => setDiscountId(event.target.value)}>
              <option value="">همهٔ تخفیف‌ها</option>
              {discounts.map((discount) => (
                <option key={discount.id} value={discount.id}>
                  {discount.name}{discount.code ? ` (${discount.code})` : ''}
                </option>
              ))}
            </select>
          </label>
          <label>
            <span>دسته‌بندی</span>
            <select value={categoryId} onChange={(event) => setCategoryId(event.target.value)}>
              <option value="">همهٔ دسته‌ها</option>
              {categories.map((category) => (
                <option key={category.id} value={category.id}>
                  {category.name}
                </option>
              ))}
            </select>
          </label>
          <label>
            <span>نوع تخفیف</span>
            <select value={kind} onChange={(event) => setKind(event.target.value)}>
              {KIND_OPTIONS.map((option) => (
                <option key={option.value} value={option.value}>
                  {option.label}
                </option>
              ))}
            </select>
          </label>
          <label>
            <span>کانال فروش</span>
            <select value={channel} onChange={(event) => setChannel(event.target.value)}>
              {CHANNEL_OPTIONS.map((option) => (
                <option key={option.value} value={option.value}>
                  {option.label}
                </option>
              ))}
            </select>
          </label>
          <label>
            <span>کد تخفیف</span>
            <input
              className="ltr-input"
              dir="ltr"
              value={code}
              placeholder="مثلاً WELCOME10"
              onChange={(event) => setCode(event.target.value)}
            />
          </label>
          <label>
            <span>فقط تخفیف‌های کل سفارش</span>
            <span className={`switch ${onlyUnscoped ? 'switch-on' : ''}`}>
              <input
                type="checkbox"
                checked={onlyUnscoped}
                onChange={(event) => setOnlyUnscoped(event.target.checked)}
              />
            </span>
            <small className="settings-hint">تخفیف‌هایی که به دسته‌بندی خاصی محدود نشده‌اند.</small>
          </label>
        </div>

        <div style={{ marginTop: '18px' }}>
          <strong>ستون‌های گزارش</strong>
          <div className="field-grid" style={{ marginTop: '10px' }}>
            {availableColumns.map((column) => (
              <label key={column.key} style={{ display: 'flex', alignItems: 'center', gap: '8px' }}>
                <input
                  type="checkbox"
                  checked={columns.includes(column.key)}
                  onChange={() => toggleColumn(column.key)}
                />
                <span>{column.title}</span>
              </label>
            ))}
          </div>
        </div>

        <div className="table-actions" style={{ marginTop: '18px' }}>
          <button type="button" onClick={runPreview} disabled={loading}>
            {loading ? 'در حال ساخت گزارش...' : 'نمایش گزارش'}
          </button>
          <button type="button" className="secondary" onClick={downloadExcel} disabled={downloading}>
            {downloading ? 'در حال ساخت فایل...' : 'دانلود Excel'}
          </button>
        </div>

        {error ? <p className="feedback feedback-error">{error}</p> : null}
        {message ? <p className="feedback feedback-success">{message}</p> : null}
      </section>

      {report ? (
        <>
          <section className="panel">
            <div className="panel-head">
              <div>
                <h3>خلاصهٔ دوره</h3>
                <p>
                  {new Date(report.from).toLocaleDateString('fa-IR')} تا{' '}
                  {new Date(report.to).toLocaleDateString('fa-IR')}
                </p>
              </div>
            </div>
            <div className="stat-grid">
              <div className="stat-card">
                <span>مجموع تخفیف</span>
                <strong>{formatToman(report.totalDiscount)}</strong>
              </div>
              <div className="stat-card">
                <span>تعداد استفاده</span>
                <strong>{toPersianNumber(report.totalUsage)}</strong>
              </div>
              <div className="stat-card">
                <span>تعداد سفارش</span>
                <strong>{toPersianNumber(report.totalOrders)}</strong>
              </div>
              <div className="stat-card">
                <span>تعداد مشتری</span>
                <strong>{toPersianNumber(report.totalCustomers)}</strong>
              </div>
            </div>
          </section>

          <section className="panel">
            <div className="panel-head">
              <div>
                <h3>عملکرد هر تخفیف</h3>
              </div>
            </div>
            <table className="admin-table">
              <thead>
                <tr>
                  <th>تخفیف</th>
                  <th>نوع</th>
                  <th>کد</th>
                  <th>مبلغ</th>
                  <th>استفاده</th>
                  <th>سفارش</th>
                  <th>مشتری</th>
                </tr>
              </thead>
              <tbody>
                {(report.summary ?? []).map((row) => (
                  <tr key={`${row.name}-${row.kind}-${row.code ?? ''}`}>
                    <td>{row.name}</td>
                    <td>{KIND_OPTIONS.find((option) => option.value === row.kind)?.label ?? row.kind}</td>
                    <td className="ltr-input" dir="ltr">{row.code || '—'}</td>
                    <td>{formatToman(row.amount)}</td>
                    <td>{toPersianNumber(row.usageCount)}</td>
                    <td>{toPersianNumber(row.orderCount)}</td>
                    <td>{toPersianNumber(row.customerCount)}</td>
                  </tr>
                ))}
                {(report.summary ?? []).length === 0 ? (
                  <tr>
                    <td colSpan="7" className="table-subtext">در این بازه تخفیفی ثبت نشده است.</td>
                  </tr>
                ) : null}
              </tbody>
            </table>
          </section>

          <section className="panel">
            <div className="panel-head">
              <div>
                <h3>تفکیک دسته‌بندی و روز</h3>
              </div>
            </div>
            <table className="admin-table">
              <thead>
                <tr>
                  <th>دسته‌بندی</th>
                  <th>مبلغ</th>
                  <th>استفاده</th>
                  <th>سفارش</th>
                </tr>
              </thead>
              <tbody>
                {(report.byCategory ?? []).map((row) => (
                  <tr key={row.categoryId ?? 'all'}>
                    <td>{row.categoryName}</td>
                    <td>{formatToman(row.amount)}</td>
                    <td>{toPersianNumber(row.usageCount)}</td>
                    <td>{toPersianNumber(row.orderCount)}</td>
                  </tr>
                ))}
              </tbody>
            </table>
            <table className="admin-table" style={{ marginTop: '16px' }}>
              <thead>
                <tr>
                  <th>تاریخ</th>
                  <th>مبلغ</th>
                  <th>استفاده</th>
                  <th>سفارش</th>
                </tr>
              </thead>
              <tbody>
                {(report.byDay ?? []).map((row) => (
                  <tr key={row.date}>
                    <td>{row.date}</td>
                    <td>{formatToman(row.amount)}</td>
                    <td>{toPersianNumber(row.usageCount)}</td>
                    <td>{toPersianNumber(row.orderCount)}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </section>
        </>
      ) : null}

      <section className="panel">
        <div className="panel-head">
          <div>
            <h3>ارسال خودکار دوره‌ای</h3>
            <p>
              گزارش با ستون‌ها و فیلترهای فعلی ساخته می‌شود و در ساعت تعیین‌شده به ایمیل دریافت‌کنندگان
              ارسال می‌شود. تغییرات بدون ری‌استارت اعمال می‌شوند.
            </p>
          </div>
          <button type="button" onClick={openNewSchedule}>گزارش دوره‌ای جدید</button>
        </div>

        {scheduleForm ? (
          <div className="field-grid" style={{ marginBottom: '18px' }}>
            <label>
              <span>نام گزارش</span>
              <input
                value={scheduleForm.name}
                onChange={(event) => updateScheduleField('name', event.target.value)}
              />
            </label>
            <label>
              <span>دوره</span>
              <select
                value={scheduleForm.frequency}
                onChange={(event) => updateScheduleField('frequency', event.target.value)}
              >
                {FREQUENCY_OPTIONS.map((option) => (
                  <option key={option.value} value={option.value}>
                    {option.label}
                  </option>
                ))}
              </select>
            </label>
            <label>
              <span>ساعت ارسال (۰ تا ۲۳)</span>
              <input
                className="ltr-input"
                dir="ltr"
                type="number"
                min="0"
                max="23"
                value={scheduleForm.hour}
                onChange={(event) => updateScheduleField('hour', event.target.value)}
              />
            </label>
            {scheduleForm.frequency === 'Weekly' ? (
              <label>
                <span>روز هفته</span>
                <select
                  value={scheduleForm.dayOfWeek}
                  onChange={(event) => updateScheduleField('dayOfWeek', event.target.value)}
                >
                  {WEEK_DAYS.map((day) => (
                    <option key={day.value} value={day.value}>{day.label}</option>
                  ))}
                </select>
              </label>
            ) : null}
            {scheduleForm.frequency === 'Monthly' ? (
              <label>
                <span>روز ماه</span>
                <input
                  className="ltr-input"
                  dir="ltr"
                  type="number"
                  min="1"
                  max="31"
                  value={scheduleForm.dayOfMonth}
                  onChange={(event) => updateScheduleField('dayOfMonth', event.target.value)}
                />
              </label>
            ) : null}
            <label className="full-width">
              <span>ایمیل دریافت‌کنندگان (با کاما جدا کنید)</span>
              <input
                className="ltr-input"
                dir="ltr"
                value={scheduleForm.recipientEmails}
                placeholder="owner@example.com, manager@example.com"
                onChange={(event) => updateScheduleField('recipientEmails', event.target.value)}
              />
            </label>
            <label>
              <span>فعال</span>
              <span className={`switch ${scheduleForm.isEnabled ? 'switch-on' : ''}`}>
                <input
                  type="checkbox"
                  checked={scheduleForm.isEnabled}
                  onChange={(event) => updateScheduleField('isEnabled', event.target.checked)}
                />
              </span>
            </label>
            <div className="table-actions full-width">
              <button type="button" onClick={saveSchedule} disabled={savingSchedule}>
                {savingSchedule ? 'در حال ذخیره...' : 'ذخیره گزارش دوره‌ای'}
              </button>
              <button type="button" className="secondary" onClick={() => setScheduleForm(null)}>
                انصراف
              </button>
            </div>
          </div>
        ) : null}

        <table className="admin-table">
          <thead>
            <tr>
              <th>نام</th>
              <th>دوره</th>
              <th>زمان</th>
              <th>دریافت‌کنندگان</th>
              <th>آخرین ارسال</th>
              <th>وضعیت</th>
              <th>عملیات</th>
            </tr>
          </thead>
          <tbody>
            {schedules.map((schedule) => (
              <tr key={schedule.id}>
                <td>{schedule.name}</td>
                <td>{FREQUENCY_OPTIONS.find((option) => option.value === schedule.frequency)?.label ?? schedule.frequency}</td>
                <td>
                  {toPersianNumber(schedule.hour)}:۰۰
                  {schedule.frequency === 'Weekly'
                    ? ` — ${WEEK_DAYS.find((day) => day.value === schedule.dayOfWeek)?.label ?? ''}`
                    : null}
                  {schedule.frequency === 'Monthly'
                    ? ` — روز ${toPersianNumber(schedule.dayOfMonth)}`
                    : null}
                </td>
                <td className="ltr-input" dir="ltr">{(schedule.recipients ?? []).join(', ')}</td>
                <td>{schedule.lastRunAt ? formatDateTime(schedule.lastRunAt) : '—'}</td>
                <td>{schedule.isEnabled ? 'فعال' : 'غیرفعال'}</td>
                <td className="table-actions">
                  <button type="button" className="secondary" onClick={() => openEditSchedule(schedule)}>
                    ویرایش
                  </button>
                  <button
                    type="button"
                    className="secondary"
                    onClick={() => sendNow(schedule)}
                    disabled={sendingId === schedule.id}
                  >
                    {sendingId === schedule.id ? 'در حال ارسال...' : 'ارسال آزمایشی'}
                  </button>
                  <button type="button" className="secondary" onClick={() => toggleEnabled(schedule)}>
                    {schedule.isEnabled ? 'غیرفعال' : 'فعال'}
                  </button>
                  <button type="button" className="danger" onClick={() => deleteSchedule(schedule)}>
                    حذف
                  </button>
                </td>
              </tr>
            ))}
            {schedules.length === 0 ? (
              <tr>
                <td colSpan="7" className="table-subtext">هنوز گزارش دوره‌ای تعریف نشده است.</td>
              </tr>
            ) : null}
          </tbody>
        </table>
      </section>
    </section>
  )
}
