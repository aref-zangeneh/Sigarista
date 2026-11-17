import { useCallback, useEffect, useState } from 'react'
import { apiRequest, getReadableError } from '../utils/adminApi'
import { useAdmin } from '../context/AdminContext'
import { hasPermission } from '../utils/adminPermissions'
import { Tip } from '../components/Tip'
import './SystemLogsPage.css'

const LEVELS = [
  { value: 'Verbose', label: 'Verbose — همه‌چیز (فقط برای اشکال‌زدایی)' },
  { value: 'Debug', label: 'Debug — جزئیات بیشتر' },
  { value: 'Information', label: 'Information — پیش‌فرض عادی' },
  { value: 'Warning', label: 'Warning — فقط هشدارها و خطاها' },
  { value: 'Error', label: 'Error — فقط خطاها' },
  { value: 'Fatal', label: 'Fatal — تقریباً هیچ‌چیز' },
]

function formatBytes(bytes) {
  if (!Number.isFinite(bytes) || bytes <= 0) return '0 KB'
  if (bytes < 1024 * 1024) return `${(bytes / 1024).toFixed(0)} KB`
  return `${(bytes / (1024 * 1024)).toFixed(1)} MB`
}

const LEVEL_FILTERS = [
  { value: '', label: 'همه' },
  { value: 'Error', label: 'خطا' },
  { value: 'Fatal', label: 'بحرانی' },
  { value: 'Warning', label: 'هشدار' },
  { value: 'Information', label: 'اطلاعات' },
  { value: 'Debug', label: 'دیباگ' },
  { value: 'Verbose', label: 'پرمحتوا' },
]

const LEVEL_BADGES = {
  Fatal: 'fatal',
  Error: 'error',
  Warning: 'warning',
  Information: 'info',
  Debug: 'debug',
  Verbose: 'debug',
}

function formatFileDate(fileName) {
  const match = /sugarista-(\d{4})(\d{2})(\d{2})(?:_(\d+))?\.log/.exec(fileName)
  if (!match) return fileName
  const date = new Date(Date.UTC(Number(match[1]), Number(match[2]) - 1, Number(match[3])))
  const label = date.toLocaleDateString('fa-IR', { weekday: 'short', day: 'numeric', month: 'long' })
  // Size-rolled parts of the same day show up as _001, _002 …
  return match[4] ? `${label} (بخش ${Number(match[4]).toLocaleString('fa-IR')})` : label
}

function isTodayFile(fileName) {
  const match = /sugarista-(\d{4})(\d{2})(\d{2})/.exec(fileName)
  if (!match) return false
  const now = new Date()
  return Number(match[1]) === now.getUTCFullYear() &&
    Number(match[2]) === now.getUTCMonth() + 1 &&
    Number(match[3]) === now.getUTCDate()
}

const BATCH_SIZES = [100, 200, 500]

function formatEntryTime(timestampUtc) {
  const date = new Date(timestampUtc)
  return date.toLocaleString('fa-IR', {
    day: '2-digit',
    month: '2-digit',
    hour: '2-digit',
    minute: '2-digit',
    second: '2-digit',
    hour12: false,
  })
}

export function SystemLogsPage() {
  const { session } = useAdmin()
  const [status, setStatus] = useState(null)
  const [selectedLevel, setSelectedLevel] = useState('')
  const [pending, setPending] = useState(false)
  const [flash, setFlash] = useState('')
  const [error, setError] = useState('')

  // ── Log browser state ──────────────────────────────────────
  const [files, setFiles] = useState([])
  const [selectedFile, setSelectedFile] = useState('')
  const [levelFilter, setLevelFilter] = useState('')
  const [categoryFilter, setCategoryFilter] = useState('')
  const [searchInput, setSearchInput] = useState('')
  const [searchApplied, setSearchApplied] = useState('')
  const [page, setPage] = useState(1)
  const [entriesData, setEntriesData] = useState(null)
  const [loadingEntries, setLoadingEntries] = useState(false)
  const [expandedEntry, setExpandedEntry] = useState(null)
  const [busyFile, setBusyFile] = useState('')
  const [batchFileCount, setBatchFileCount] = useState(100)
  const [batchEntryCount, setBatchEntryCount] = useState(100)

  const loadStatus = useCallback(async () => {
    try {
      const data = await apiRequest('/admin/logging', { token: session.token })
      setStatus(data)
      setSelectedLevel((current) => current || data?.minimumLevel || '')
      setError('')
    } catch (requestError) {
      setError(getReadableError(requestError))
    }
  }, [session.token])

  useEffect(() => {
    loadStatus()
    const timer = setInterval(loadStatus, 5000) // live drop/queue counters
    return () => clearInterval(timer)
  }, [loadStatus])

  const canDeleteLogs = hasPermission(session, 'admin.system-logs.delete')

  const loadFiles = useCallback(async (token) => {
    try {
      const data = await apiRequest('/admin/logging/files', { token })
      setFiles(data ?? [])
      setSelectedFile((current) =>
        current && (data ?? []).some((file) => file.fileName === current)
          ? current
          : (data?.[0]?.fileName ?? ''))
      setError('')
    } catch (requestError) {
      setError(getReadableError(requestError))
    }
  }, [])

  useEffect(() => {
    if (session.token) loadFiles(session.token)
  }, [loadFiles, session.token, loadStatus])

  const loadEntries = useCallback(async (token) => {
    if (!selectedFile) return
    setLoadingEntries(true)
    try {
      const params = new URLSearchParams({
        page: String(page),
        pageSize: '50',
      })
      if (levelFilter) params.set('level', levelFilter)
      if (categoryFilter) params.set('category', categoryFilter)
      if (searchApplied) params.set('search', searchApplied)
      const data = await apiRequest(`/admin/logging/files/${selectedFile}/entries?${params}`, { token })
      setEntriesData(data)
      setError('')
    } catch (requestError) {
      setError(getReadableError(requestError))
    } finally {
      setLoadingEntries(false)
    }
  }, [selectedFile, levelFilter, categoryFilter, searchApplied, page])

  useEffect(() => {
    if (session.token) loadEntries(session.token)
  }, [loadEntries, session.token])

  const applyDeleteFile = async (fileName) => {
    if (!window.confirm(
      `حذف فایل لاگ «${formatFileDate(fileName)}» (${fileName})؟ این فایل برای همیشه از دیسک پاک می‌شود.`)) {
      return
    }
    setBusyFile(fileName)
    setError('')
    setFlash('')
    try {
      const result = await apiRequest(`/admin/logging/files/${fileName}`, {
        method: 'DELETE',
        token: session.token,
      })
      setFlash(result?.message || 'فایل حذف شد.')
      setPage(1)
      setExpandedEntry(null)
      await loadFiles(session.token)
      await loadEntries(session.token)
    } catch (requestError) {
      setError(getReadableError(requestError))
    } finally {
      setBusyFile('')
      setTimeout(() => setFlash(''), 6000)
    }
  }

  const applyDeleteOldFiles = async () => {
    if (!window.confirm(
      'حذف همهٔ فایل‌های لاگ قدیمی؟ فایل امروز (روی که سرور در حال نوشتن است) حفظ می‌شود.')) {
      return
    }
    setBusyFile('__all__')
    setError('')
    setFlash('')
    try {
      const result = await apiRequest('/admin/logging/files', {
        method: 'DELETE',
        token: session.token,
      })
      setFlash(result?.message || 'فایل‌های قدیمی حذف شدند.')
      setPage(1)
      setExpandedEntry(null)
      await loadFiles(session.token)
      await loadEntries(session.token)
    } catch (requestError) {
      setError(getReadableError(requestError))
    } finally {
      setBusyFile('')
      setTimeout(() => setFlash(''), 6000)
    }
  }

  const applyDeleteOldestFilesBatch = async () => {
    if (!window.confirm(
      `حذف ${batchFileCount.toLocaleString('fa-IR')} فایل قدیمی‌ترین لاگ؟ فایل‌های امروز حفظ می‌شوند و فضا فوراً آزاد می‌شود.`)) {
      return
    }
    setBusyFile('__batch__')
    setError('')
    setFlash('')
    try {
      const result = await apiRequest(`/admin/logging/files/batch?count=${batchFileCount}`, {
        method: 'DELETE',
        token: session.token,
      })
      setFlash(result?.message || 'فایل‌های قدیمی حذف شدند.')
      setPage(1)
      setExpandedEntry(null)
      await loadFiles(session.token)
      await loadEntries(session.token)
    } catch (requestError) {
      setError(getReadableError(requestError))
    } finally {
      setBusyFile('')
      setTimeout(() => setFlash(''), 6000)
    }
  }

  const applyDeleteOldestEntries = async () => {
    if (!window.confirm(
      `حذف ${batchEntryCount.toLocaleString('fa-IR')} ردیف قدیمی‌ترین از فایل «${formatFileDate(selectedFile)}»؟ بقیهٔ ردیف‌های فایل دست‌نخورده می‌مانند و این عمل بازگشت‌پذیر نیست.`)) {
      return
    }
    setBusyFile('__entries__')
    setError('')
    setFlash('')
    try {
      const result = await apiRequest(`/admin/logging/files/${selectedFile}/entries/oldest?count=${batchEntryCount}`, {
        method: 'DELETE',
        token: session.token,
      })
      setFlash(result?.message || 'ردیف‌های قدیمی حذف شدند.')
      setPage(1)
      setExpandedEntry(null)
      await loadFiles(session.token)
      await loadEntries(session.token)
    } catch (requestError) {
      setError(getReadableError(requestError))
    } finally {
      setBusyFile('')
      setTimeout(() => setFlash(''), 6000)
    }
  }

  const setFilter = (apply) => {
    // Any filter change resets to the first page and closes the open row.
    setPage(1)
    setExpandedEntry(null)
    apply()
  }

  const applyLevel = async () => {
    if (!selectedLevel) return
    setPending(true)
    setError('')
    setFlash('')
    try {
      const result = await apiRequest('/admin/logging/level', {
        method: 'PUT',
        token: session.token,
        body: { level: selectedLevel },
      })
      setFlash(result?.message || `سطح لاگ روی ${selectedLevel} تنظیم شد.`)
      await loadStatus()
    } catch (requestError) {
      setError(getReadableError(requestError))
    } finally {
      setPending(false)
      setTimeout(() => setFlash(''), 6000)
    }
  }

  if (!status) {
    return (
      <div className="system-logs-page" dir="rtl">
        <header className="system-logs-header">
          <h1>لاگ‌ها و سلامت سیستم</h1>
        </header>
        {error ? <div className="system-logs-banner error">{error}</div> : null}
        <p className="system-logs-note">در حال دریافت وضعیت…</p>
      </div>
    )
  }

  const dropped = status.droppedEvents ?? 0
  const accepted = status.acceptedEvents ?? 0
  const dropRatio = accepted > 0 ? Math.round((dropped / (accepted + dropped)) * 100) : 0

  return (
    <div className="system-logs-page" dir="rtl">
      <header className="system-logs-header">
        <h1>لاگ‌ها و سلامت سیستم</h1>
        <p className="system-logs-subtitle">
          وضعیت زیرساخت لاگ سرور به‌صورت زنده. لاگ‌ها روی دیسک سرور در فایل‌های چرخشی
          (هر فایل حداکثر {status.fileSizeLimitMb} مگابایت، نگهداری {status.retainedFiles} فایل)
          ذخیره می‌شوند و اگر صف پر شود، رویدادها حذف و شمرده می‌شوند — هیچ‌وقت درخواست مشتریان کند یا خطا نمی‌گیرد.
        </p>
      </header>

      {error ? <div className="system-logs-banner error">{error}</div> : null}
      {flash ? <div className="system-logs-banner ok">{flash}</div> : null}

      <div className="system-logs-grid">
        <div className="system-logs-card">
          <p className="card-label">سطح فعلی لاگ</p>
          <p className="card-value ok">{status.minimumLevel}</p>
          <p className="card-hint">تغییرش بدون ری‌استارت از پایین همین صفحه</p>
        </div>

        <div className="system-logs-card">
          <p className="card-label">رویدادهای حذف‌شده (صف پر)</p>
          <p className={`card-value ${dropped > 0 ? 'warn' : 'ok'}`}>{dropped.toLocaleString('fa-IR')}</p>
          <p className="card-hint">{dropRatio}٪ کل رویدادها — صفر یعنی صف هیچ‌وقت پر نشده</p>
        </div>

        <div className="system-logs-card">
          <p className="card-label">رویدادهای پذیرفته‌شده</p>
          <p className="card-value">{accepted.toLocaleString('fa-IR')}</p>
          <p className="card-hint">صف: {status.queueDepth} از {status.queueCapacity.toLocaleString('fa-IR')}</p>
        </div>

        <div className="system-logs-card">
          <p className="card-label">فضای مصرفی لاگ روی دیسک</p>
          <p className="card-value">{formatBytes(status.logDirectoryBytes)}</p>
          <p className="card-hint">{status.logFileCount} فایل — سقف نظری ≈ {(status.fileSizeLimitMb * status.retainedFiles)} MB</p>
        </div>

        <div className="system-logs-card">
          <p className="card-label">مقصد Seq (اختیاری)</p>
          <p className={`card-value ${status.seqEnabled ? 'ok' : ''}`}>{status.seqEnabled ? 'فعال' : 'غیرفعال'}</p>
          <p className="card-hint">
            {status.seqEnabled
              ? `آدرس: ${status.seqUrl || '—'} — قطعی Seq به سایت آسیب نمی‌زند`
              : 'فعال‌سازی فقط از تنظیمات سرور (متغیر محیطی)، بدون نیاز به تغییر کد'}
          </p>
        </div>
      </div>

      <section className="system-logs-section">
        <h2>تغییر سطح لاگ — بدون ری‌استارت و بدون دیپلوی</h2>
        <p className="section-hint">
          برای اشکال‌زدایی موقت، سطح را روی Information یا Debug بگذارید و بعد از کار برگردانید روی Warning
          تا حجم لاگ کم بماند. تنظیم بلافاصله اعمال و ذخیره می‌شود (بعد از ری‌استارت هم می‌ماند).
        </p>
        <div className="system-logs-levels">
          {LEVELS.map((level) => (
            <button
              key={level.value}
              type="button"
              className={`system-logs-level ${selectedLevel === level.value ? 'selected' : ''}`}
              onClick={() => setSelectedLevel(level.value)}
            >
              {level.label}
            </button>
          ))}
        </div>
        <div className="system-logs-actions">
          <button
            type="button"
            className="system-logs-apply"
            disabled={pending || selectedLevel === status.minimumLevel}
            onClick={applyLevel}
          >
            {pending ? 'در حال اعمال…' : 'اعمال سطح جدید'}
          </button>
          <span className="system-logs-meta">
            سطح فعلی: {status.minimumLevel} • آخرین به‌روزرسانی: همین حالا (هر ۵ ثانیه خودکار)
          </span>
        </div>
      </section>

      <section className="system-logs-section">
        <h2>مرور لاگ‌های سیستمی</h2>
        <p className="section-hint">
          لاگ‌های فنی سرور، دسته‌بندی‌شده و قابل جستجو. برای یافتن مشکل، فایل روز موردنظر را انتخاب و روی
          سطح «خطا» فیلتر کنید؛ کلیک روی هر ردیف جزئیات کامل (Exception و اطلاعات فنی) را باز می‌کند.
        </p>

        {files.length === 0 ? (
          <p className="system-logs-note">فایل لاگی روی دیسک نیست — هنوز رویدادی ثبت نشده یا همه حذف شده‌اند.</p>
        ) : (
          <>
            <div className="log-files-row">
              {files.map((file) => (
                <button
                  key={file.fileName}
                  type="button"
                  className={`log-file-chip ${selectedFile === file.fileName ? 'selected' : ''}`}
                  onClick={() => setFilter(() => setSelectedFile(file.fileName))}
                >
                  <span className="log-file-date">{formatFileDate(file.fileName)}</span>
                  <span className="log-file-meta">
                    {formatBytes(file.sizeBytes)}
                    {file.levelCounts?.Error || file.levelCounts?.Fatal
                      ? ` • ${((file.levelCounts?.Error ?? 0) + (file.levelCounts?.Fatal ?? 0)).toLocaleString('fa-IR')} خطا`
                      : ''}
                  </span>
                  {canDeleteLogs ? (
                    <span
                      role="button"
                      tabIndex={0}
                      className="log-file-delete"
                      title="حذف این فایل"
                      onClick={(event) => {
                        event.stopPropagation()
                        applyDeleteFile(file.fileName)
                      }}
                      onKeyDown={(event) => {
                        if (event.key === 'Enter') {
                          event.stopPropagation()
                          applyDeleteFile(file.fileName)
                        }
                      }}
                    >
                      {busyFile === file.fileName ? '…' : '×'}
                    </span>
                  ) : null}
                </button>
              ))}
              {canDeleteLogs && files.length > 1 ? (
                <button
                  type="button"
                  className="log-files-clear"
                  disabled={busyFile === '__all__'}
                  onClick={applyDeleteOldFiles}
                >
                  {busyFile === '__all__' ? 'در حال حذف…' : 'حذف همهٔ قدیمی‌ها'}
                </button>
              ) : null}
            </div>

            {canDeleteLogs && files.length > 1 ? (
              <div className="log-batch-row">
                <span className="log-batch-label">حذف دسته‌ای فایل‌های قدیمی:</span>
                <select
                  className="log-batch-select"
                  value={batchFileCount}
                  disabled={busyFile === '__batch__'}
                  onChange={(event) => setBatchFileCount(Number(event.target.value))}
                >
                  {BATCH_SIZES.map((size) => (
                    <option key={size} value={size}>{size.toLocaleString('fa-IR')} فایل</option>
                  ))}
                </select>
                <button
                  type="button"
                  className="log-batch-apply"
                  disabled={busyFile === '__batch__'}
                  onClick={applyDeleteOldestFilesBatch}
                >
                  {busyFile === '__batch__' ? 'در حال حذف…' : `حذف ${batchFileCount.toLocaleString('fa-IR')} فایل قدیمی‌ترین`}
                </button>
                <span className="log-batch-hint">فایل‌های امروز همیشه حفظ می‌شوند — فضا بلافاصله آزاد می‌شود.</span>
              </div>
            ) : null}

            <div className="log-filters">
              <div className="log-level-chips" data-tip="system-logs.levels">
                {LEVEL_FILTERS.map((filter) => {
                  const count = filter.value
                    ? (entriesData?.levelCounts?.[filter.value] ?? 0)
                    : null
                  return (
                    <button
                      key={filter.value || 'all'}
                      type="button"
                      className={`log-level-chip ${levelFilter === filter.value ? 'selected' : ''} ${filter.value ? `tone-${LEVEL_BADGES[filter.value] ?? 'info'}` : ''}`}
                      onClick={() => setFilter(() => setLevelFilter(filter.value))}
                    >
                      {filter.label}
                      {count !== null ? <span className="log-chip-count">{count.toLocaleString('fa-IR')}</span> : null}
                    </button>
                  )
                })}
                <Tip k="system-logs.levels" />
              </div>

              <div className="log-filter-controls">
                <select
                  className="log-category-select"
                  value={categoryFilter}
                  onChange={(event) => setFilter(() => setCategoryFilter(event.target.value))}
                >
                  <option value="">همهٔ دسته‌ها</option>
                  {(entriesData?.categories ?? []).map((category) => (
                    <option key={category} value={category}>{category}</option>
                  ))}
                </select>
                <Tip k="system-logs.categories" />

                <form
                  className="log-search"
                  onSubmit={(event) => {
                    event.preventDefault()
                    setFilter(() => setSearchApplied(searchInput.trim()))
                  }}
                >
                  <input
                    type="search"
                    placeholder="جستجو در پیام‌ها…"
                    value={searchInput}
                    onChange={(event) => setSearchInput(event.target.value)}
                  />
                  <button type="submit">جستجو</button>
                  {searchApplied ? (
                    <button type="button" className="log-search-clear" onClick={() => {
                      setSearchInput('')
                      setFilter(() => setSearchApplied(''))
                    }}>×</button>
                  ) : null}
                </form>
                <Tip k="system-logs.search" />
              </div>
            </div>

            {canDeleteLogs && selectedFile && entriesData && entriesData.totalMatching > 0 ? (
              isTodayFile(selectedFile) ? (
                <p className="log-batch-hint log-entry-batch-note">
                  حذف دسته‌ای ردیف‌ها فقط برای فایل‌های روزهای قبل ممکن است؛ فایل امروز هنوز در حال نوشتن است.
                </p>
              ) : (
                <div className="log-batch-row log-entry-batch">
                  <span className="log-batch-label">حذف دسته‌ای قدیمی‌ترین ردیف‌های این فایل:</span>
                  <select
                    className="log-batch-select"
                    value={batchEntryCount}
                    disabled={busyFile === '__entries__'}
                    onChange={(event) => setBatchEntryCount(Number(event.target.value))}
                  >
                    {BATCH_SIZES.map((size) => (
                      <option key={size} value={size}>{size.toLocaleString('fa-IR')} ردیف</option>
                    ))}
                  </select>
                  <button
                    type="button"
                    className="log-batch-apply"
                    disabled={busyFile === '__entries__'}
                    onClick={applyDeleteOldestEntries}
                  >
                    {busyFile === '__entries__'
                      ? 'در حال حذف…'
                      : `حذف ${batchEntryCount.toLocaleString('fa-IR')} ردیف قدیمی‌ترین`}
                  </button>
                  <span className="log-batch-hint">
                    بقیهٔ ردیف‌ها دست‌نخورده می‌مانند؛ اگر تعداد انتخابی از کل ردیف‌ها بیشتر باشد، کل فایل حذف می‌شود.
                  </span>
                </div>
              )
            ) : null}

            <div className="log-entries">
              {loadingEntries ? (
                <p className="system-logs-note">در حال خواندن لاگ…</p>
              ) : (entriesData?.entries ?? []).length === 0 ? (
                <p className="system-logs-note">هیچ ردیفی با این فیلترها پیدا نشد.</p>
              ) : (
                <table className="log-table">
                  <thead>
                    <tr>
                      <th>زمان</th>
                      <th>سطح</th>
                      <th>دسته</th>
                      <th>پیام</th>
                    </tr>
                  </thead>
                  <tbody>
                    {(entriesData?.entries ?? []).map((entry, index) => (
                      <LogRow
                        key={`${entry.timestampUtc}-${index}`}
                        entry={entry}
                        expanded={expandedEntry === `${entry.timestampUtc}-${index}`}
                        onToggle={() => setExpandedEntry((current) =>
                          current === `${entry.timestampUtc}-${index}` ? null : `${entry.timestampUtc}-${index}`)}
                      />
                    ))}
                  </tbody>
                </table>
              )}
            </div>

            <div className="log-pagination">
              <button
                type="button"
                disabled={page <= 1 || loadingEntries}
                onClick={() => setPage((current) => Math.max(1, current - 1))}
              >
                → صفحهٔ قبل
              </button>
              <span className="log-page-meta">
                {entriesData
                  ? `${page.toLocaleString('fa-IR')} از ${Math.max(1, Math.ceil(entriesData.totalMatching / entriesData.pageSize)).toLocaleString('fa-IR')} — ${entriesData.totalMatching.toLocaleString('fa-IR')} ردیف`
                  : '—'}
              </span>
              <button
                type="button"
                disabled={loadingEntries ||
                  !entriesData ||
                  page >= Math.ceil(entriesData.totalMatching / entriesData.pageSize)}
                onClick={() => setPage((current) => current + 1)}
              >
                صفحهٔ بعد ←
              </button>
            </div>
          </>
        )}
      </section>

      <section className="system-logs-section">
        <h2>چه چیزی در لاگ ثبت می‌شود؟</h2>
        <p className="system-logs-note">
          • هر درخواست HTTP با کد وضعیت و زمان پاسخ — بدون بدنهٔ Request/Response و بدون هیچ دادهٔ حساس
          (پسورد، توکن و کلیدها قبل از نوشتن حذف می‌شوند).{'\n'}
          • درخواست‌های کند (بیش از ۱ ثانیه) و خطاهای سرور با سطح بالاتر برجسته می‌شوند تا در جست‌وجو دیده شوند.
          {'\n'}
          • لاگ‌های تکراری پشت‌سرهم فشرده می‌شوند و فقط یک خلاصه ذخیره می‌شود.
          {'\n'}
          • فایل‌های قدیمی را می‌توان تک‌تکی یا دسته‌ای (بسته‌های ۱۰۰ تا ۵۰۰ فایل یا ردیف) حذف کرد؛
          ردیف‌های قدیمی‌ترین هر فایل هم قابل حذف بسته‌ای است و بقیهٔ فایل سالم می‌ماند.{'\n'}
          • علاوه بر حذف دستی، سرور هر ۶ ساعت فایل‌های قدیمی‌تر از ۱۴ روز را خودکار پاک می‌کند
          (قابل تغییر از تنظیمات سرور با LoggingInfrastructure__FileRetentionDays).{'\n'}
          • رخدادهای حساس (باز/بسته شدن مسیرها، تغییر رمز دیتابیس، تغییر سطح لاگ) در فایل audit جداگانه
          ثبت می‌شوند و هرگز حذف نمی‌شوند.
        </p>
      </section>
    </div>
  )
}

function LogRow({ entry, expanded, onToggle }) {
  const badgeClass = LEVEL_BADGES[entry.level] ?? 'info'
  const properties = Object.entries(entry.properties ?? {})

  return (
    <>
      <tr className={`log-row tone-${badgeClass}`} onClick={onToggle}>
        <td className="log-cell-time">{formatEntryTime(entry.timestampUtc)}</td>
        <td>
          <span className={`log-badge ${badgeClass}`}>{entry.level}</span>
        </td>
        <td className="log-cell-category">{entry.category}</td>
        <td className="log-cell-message">
          {entry.message || '(پیام خالی)'}
          {entry.exception && !expanded ? <span className="log-has-exception"> ⚠</span> : null}
        </td>
      </tr>
      {expanded ? (
        <tr className="log-detail-row">
          <td colSpan="4">
            <div className="log-detail">
              <div className="log-detail-line">
                <span>منبع:</span>
                <code>{entry.source || '—'}</code>
              </div>
              {entry.message ? (
                <div className="log-detail-line">
                  <span>پیام کامل:</span>
                  <code>{entry.message}</code>
                </div>
              ) : null}
              {entry.exception ? (
                <div className="log-detail-line">
                  <span>جزئیات خطا:</span>
                  <pre className="log-exception">{entry.exception}</pre>
                </div>
              ) : null}
              {properties.length ? (
                <div className="log-detail-line">
                  <span>اطلاعات فنی:</span>
                  <ul className="log-props">
                    {properties.map(([key, value]) => (
                      <li key={key}>
                        <code>{key}</code>: {value}
                      </li>
                    ))}
                  </ul>
                </div>
              ) : null}
            </div>
          </td>
        </tr>
      ) : null}
    </>
  )
}
