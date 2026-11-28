import { useCallback, useEffect, useState } from 'react'
import { useAdmin } from '../context/AdminContext'
import { API_BASE_URL } from '../utils/adminApi'
import { hasPermission, PERMISSIONS } from '../utils/adminPermissions'
import './MenuSectionMinimal.css'
import './BackupsPage.css'

const TABS = [
  { key: 'records', label: '📦 فایل‌ها و تاریخچه بکاپ' },
  { key: 'jobs', label: '⏰ جاب‌های زمان‌بندی‌شده' },
  { key: 'cloud', label: '☁️ مقصدهای ابری (Multi-Cloud)' },
  { key: 'health', label: '📊 مانیتورینگ و سلامت بکاپ' },
]

const SCOPES = [
  { key: 'FullSystem', label: 'بکاپ کامل (دیتابیس + آپلودها + تنظیمات)' },
  { key: 'DatabaseOnly', label: 'فقط دیتابیس (PostgreSQL)' },
  { key: 'UploadsOnly', label: 'فقط فایل‌های آپلودشده و تصاویر' },
]

const SCHEDULE_TYPES = [
  { key: 'Daily', label: 'روزانه' },
  { key: 'Hourly', label: 'ساعتی (هر N ساعت)' },
  { key: 'Weekly', label: 'هفتگی' },
  { key: 'Manual', label: 'فقط دستی' },
]

const WEEK_DAYS = [
  { key: 6, label: 'شنبه' },
  { key: 0, label: 'یکشنبه' },
  { key: 1, label: 'دوشنبه' },
  { key: 2, label: 'سه‌شنبه' },
  { key: 3, label: 'چهارشنبه' },
  { key: 4, label: 'پنج‌شنبه' },
  { key: 5, label: 'جمعه' },
]

const CLOUD_PROVIDERS = [
  { key: 'ArvanCloud', label: 'آروان کلود (ArvanCloud S3)', defaultEndpoint: 'https://s3.ir-thr-at1.arvanstorage.ir', defaultRegion: 'default' },
  { key: 'Liara', label: 'لیارا (Liara Object Storage)', defaultEndpoint: 'https://storage.iran.liara.space', defaultRegion: 'default' },
  { key: 'ParsPack', label: 'پارس‌پک (ParsPack Object Storage)', defaultEndpoint: 'https://c123456.parspack.net', defaultRegion: 'default' },
  { key: 'AwsS3', label: 'Amazon AWS S3', defaultEndpoint: 'https://s3.eu-central-1.amazonaws.com', defaultRegion: 'eu-central-1' },
  { key: 'CloudflareR2', label: 'Cloudflare R2', defaultEndpoint: 'https://<account-id>.r2.cloudflarestorage.com', defaultRegion: 'auto' },
  { key: 'MinIO', label: 'MinIO (سرور اختصاصی)', defaultEndpoint: 'https://minio.example.com', defaultRegion: 'us-east-1' },
  { key: 'CustomWebhook', label: 'وب‌هوک سفارشی (HTTP POST)', defaultEndpoint: 'https://backup.example.com/upload', defaultRegion: 'default' },
]

function formatBytes(bytes) {
  const b = Number(bytes) || 0
  if (b <= 0) return '0 B'
  if (b < 1024) return `${b} B`
  if (b < 1024 * 1024) return `${(b / 1024).toFixed(1)} KB`
  if (b < 1024 * 1024 * 1024) return `${(b / (1024 * 1024)).toFixed(2)} MB`
  return `${(b / (1024 * 1024 * 1024)).toFixed(2)} GB`
}

function emptyJobForm() {
  return {
    id: '',
    name: '',
    scope: 'FullSystem',
    scheduleType: 'Daily',
    intervalHours: '6',
    runHourTehran: '3',
    runMinuteTehran: '0',
    dayOfWeek: '5',
    retentionCount: '10',
    cloudTargetId: '',
    isEnabled: true,
  }
}

function emptyCloudForm() {
  return {
    id: '',
    name: '',
    provider: 'ArvanCloud',
    endpointUrl: 'https://s3.ir-thr-at1.arvanstorage.ir',
    bucketName: '',
    region: 'default',
    accessKey: '',
    secretKey: '',
    keyPrefix: 'sugarista-backups/',
    isDefault: false,
    isEnabled: true,
  }
}

export function BackupsPage() {
  const {
    session,
    apiRequest,
    getReadableError,
    setMessage,
    setError,
    toPersianNumber,
    formatDateTime,
  } = useAdmin()

  const canManage = hasPermission(session, PERMISSIONS['site-settings']?.update)

  const [activeTab, setActiveTab] = useState('records')
  const [loading, setLoading] = useState(true)
  const [busy, setBusy] = useState(false)
  const [overview, setOverview] = useState(null)

  // Manual backup run
  const [manualScope, setManualScope] = useState('FullSystem')
  const [manualCloudId, setManualCloudId] = useState('')

  // Job form
  const [jobForm, setJobForm] = useState(emptyJobForm)

  // Cloud target form
  const [cloudForm, setCloudForm] = useState(emptyCloudForm)

  const loadOverview = useCallback(async () => {
    if (!session?.token) return
    try {
      const data = await apiRequest('/admin/backups/overview', { token: session.token })
      setOverview(data)
    } catch (err) {
      setError(getReadableError(err))
    } finally {
      setLoading(false)
    }
  }, [session?.token, apiRequest, getReadableError, setError])

  useEffect(() => {
    loadOverview()
  }, [loadOverview])

  const handleRunManual = async (e) => {
    e.preventDefault()
    if (!canManage) return
    setBusy(true)
    setError('')
    setMessage('')
    try {
      const rec = await apiRequest('/admin/backups/run', {
        method: 'POST',
        token: session.token,
        body: {
          scope: manualScope,
          cloudTargetId: manualCloudId || null,
        },
      })
      if (rec?.status === 'Completed') {
        setMessage(`بکاپ ${rec.backupCode} با موفقیت ایجاد شد (${formatBytes(rec.fileSizeBytes)}).`)
      } else {
        setError(rec?.errorMessage || 'اجرای بکاپ با خطا مواجه شد.')
      }
      await loadOverview()
    } catch (err) {
      setError(getReadableError(err))
    } finally {
      setBusy(false)
    }
  }

  const handleDownload = async (record) => {
    try {
      const res = await fetch(`${API_BASE_URL}/admin/backups/${record.id}/download`, {
        headers: { Authorization: `Bearer ${session.token}` },
      })
      if (!res.ok) throw new Error('دانلود فایل بکاپ ناموفق بود.')
      const blob = await res.blob()
      const url = URL.createObjectURL(blob)
      const a = document.createElement('a')
      a.href = url
      a.download = record.fileName || `${record.backupCode}.zip`
      document.body.appendChild(a)
      a.click()
      a.remove()
      URL.revokeObjectURL(url)
    } catch (err) {
      setError(err.message)
    }
  }

  const handleVerify = async (record) => {
    setBusy(true)
    setError('')
    setMessage('')
    try {
      const res = await apiRequest(`/admin/backups/${record.id}/verify`, {
        method: 'POST',
        token: session.token,
      })
      if (res?.valid) setMessage(res.message)
      else setError(res?.message || 'اعتبارسنجی فایل ناموفق بود.')
    } catch (err) {
      setError(getReadableError(err))
    } finally {
      setBusy(false)
    }
  }

  const handleUploadToCloud = async (record, targetId) => {
    if (!targetId) return
    setBusy(true)
    setError('')
    setMessage('')
    try {
      const res = await apiRequest(`/admin/backups/${record.id}/upload-cloud`, {
        method: 'POST',
        token: session.token,
        body: { cloudTargetId: targetId },
      })
      setMessage(res?.message || 'بکاپ با موفقیت به فضای ابری ارسال شد.')
      await loadOverview()
    } catch (err) {
      setError(getReadableError(err))
    } finally {
      setBusy(false)
    }
  }

  const handleDeleteBackup = async (record) => {
    if (!window.confirm(`بکاپ ${record.backupCode} از روی سرور حذف شود؟`)) return
    setBusy(true)
    try {
      await apiRequest(`/admin/backups/${record.id}`, { method: 'DELETE', token: session.token })
      setMessage('فایل بکاپ حذف شد.')
      await loadOverview()
    } catch (err) {
      setError(getReadableError(err))
    } finally {
      setBusy(false)
    }
  }

  const handleSaveJob = async (e) => {
    e.preventDefault()
    if (!canManage) return
    setBusy(true)
    setError('')
    setMessage('')
    try {
      const payload = {
        name: jobForm.name.trim(),
        scope: jobForm.scope,
        scheduleType: jobForm.scheduleType,
        intervalHours: Number(jobForm.intervalHours) || 6,
        runHourTehran: Number(jobForm.runHourTehran) || 3,
        runMinuteTehran: Number(jobForm.runMinuteTehran) || 0,
        dayOfWeek: jobForm.scheduleType === 'Weekly' ? Number(jobForm.dayOfWeek) : null,
        retentionCount: Number(jobForm.retentionCount) || 10,
        cloudTargetId: jobForm.cloudTargetId || null,
        isEnabled: Boolean(jobForm.isEnabled),
      }
      if (jobForm.id) {
        await apiRequest(`/admin/backups/jobs/${jobForm.id}`, { method: 'PUT', token: session.token, body: payload })
        setMessage('جاب زمان‌بندی‌شده ویرایش شد.')
      } else {
        await apiRequest('/admin/backups/jobs', { method: 'POST', token: session.token, body: payload })
        setMessage('جاب زمان‌بندی‌شده جدید ثبت شد.')
      }
      setJobForm(emptyJobForm())
      await loadOverview()
    } catch (err) {
      setError(getReadableError(err))
    } finally {
      setBusy(false)
    }
  }

  const handleRunJobNow = async (job) => {
    setBusy(true)
    setError('')
    setMessage('')
    try {
      const rec = await apiRequest(`/admin/backups/jobs/${job.id}/run-now`, {
        method: 'POST',
        token: session.token,
      })
      setMessage(`جاب «${job.name}» اجرا شد (${rec?.backupCode ?? ''}).`)
      await loadOverview()
    } catch (err) {
      setError(getReadableError(err))
    } finally {
      setBusy(false)
    }
  }

  const handleDeleteJob = async (job) => {
    if (!window.confirm(`جاب «${job.name}» حذف شود؟`)) return
    setBusy(true)
    try {
      await apiRequest(`/admin/backups/jobs/${job.id}`, { method: 'DELETE', token: session.token })
      setMessage('جاب بکاپ حذف شد.')
      await loadOverview()
    } catch (err) {
      setError(getReadableError(err))
    } finally {
      setBusy(false)
    }
  }

  const handleCloudProviderChange = (nextProvider) => {
    const preset = CLOUD_PROVIDERS.find((p) => p.key === nextProvider)
    setCloudForm((f) => ({
      ...f,
      provider: nextProvider,
      endpointUrl: !f.id && preset ? preset.defaultEndpoint : f.endpointUrl,
      region: !f.id && preset ? preset.defaultRegion : f.region,
    }))
  }

  const handleSaveCloud = async (e) => {
    e.preventDefault()
    if (!canManage) return
    setBusy(true)
    setError('')
    setMessage('')
    try {
      const payload = {
        name: cloudForm.name.trim(),
        provider: cloudForm.provider,
        endpointUrl: cloudForm.endpointUrl.trim(),
        bucketName: cloudForm.bucketName.trim(),
        region: cloudForm.region.trim() || 'default',
        accessKey: cloudForm.accessKey.trim() || null,
        secretKey: cloudForm.secretKey.trim() || null,
        keyPrefix: cloudForm.keyPrefix.trim() || 'sugarista-backups/',
        isDefault: Boolean(cloudForm.isDefault),
        isEnabled: Boolean(cloudForm.isEnabled),
      }
      if (cloudForm.id) {
        await apiRequest(`/admin/backups/cloud-targets/${cloudForm.id}`, {
          method: 'PUT',
          token: session.token,
          body: payload,
        })
        setMessage('مقصد ابری ویرایش شد.')
      } else {
        await apiRequest('/admin/backups/cloud-targets', {
          method: 'POST',
          token: session.token,
          body: payload,
        })
        setMessage('مقصد ابری جدید ثبت شد.')
      }
      setCloudForm(emptyCloudForm())
      await loadOverview()
    } catch (err) {
      setError(getReadableError(err))
    } finally {
      setBusy(false)
    }
  }

  const handleTestCloud = async (target) => {
    setBusy(true)
    setError('')
    setMessage('')
    try {
      const res = await apiRequest(`/admin/backups/cloud-targets/${target.id}/test`, {
        method: 'POST',
        token: session.token,
      })
      if (res?.success) setMessage(res.message)
      else setError(res?.message || 'تست اتصال به کلاود ناموفق بود.')
      await loadOverview()
    } catch (err) {
      setError(getReadableError(err))
    } finally {
      setBusy(false)
    }
  }

  const handleDeleteCloud = async (target) => {
    if (!window.confirm(`مقصد ابری «${target.name}» حذف شود؟`)) return
    setBusy(true)
    try {
      await apiRequest(`/admin/backups/cloud-targets/${target.id}`, { method: 'DELETE', token: session.token })
      setMessage('مقصد ابری حذف شد.')
      await loadOverview()
    } catch (err) {
      setError(getReadableError(err))
    } finally {
      setBusy(false)
    }
  }

  const scopeLabel = (s) => SCOPES.find((x) => x.key === s)?.label || s
  const scheduleLabel = (j) => {
    if (j.scheduleType === 'Hourly') return `هر ${toPersianNumber(j.intervalHours)} ساعت`
    if (j.scheduleType === 'Daily') return `روزانه ساعت ${String(j.runHourTehran).padStart(2, '0')}:${String(j.runMinuteTehran).padStart(2, '0')}`
    if (j.scheduleType === 'Weekly') {
      const day = WEEK_DAYS.find((d) => d.key === j.dayOfWeek)?.label || ''
      return `هفتگی (${day}) ساعت ${String(j.runHourTehran).padStart(2, '0')}:${String(j.runMinuteTehran).padStart(2, '0')}`
    }
    return 'فقط دستی'
  }

  const enabledClouds = (overview?.cloudTargets ?? []).filter((c) => c.isEnabled)

  return (
    <section className="bkp-page menu-minimal-scope">
      <div className="panel">
        <div className="bkp-head-row">
          <div>
            <h2>مرکز مدیریت، زمان‌بندی و مانیتورینگ بکاپ</h2>
            <p>
              تهیه بکاپ کامل از دیتابیس، تصاویر و تنظیمات سیستم، زمان‌بندی خودکار با حذف نسخه‌های قدیمی، بررسی سلامت SHA-256 و ارسال به کلاودهای متفاوت (آروان، لیارا، AWS S3، R2، MinIO).
            </p>
          </div>
          <button type="button" className="ghost-button" onClick={loadOverview} disabled={busy}>
            🔄 بروزرسانی
          </button>
        </div>

        {overview ? (
          <div className="bkp-kpi-grid">
            <div className="bkp-kpi-card">
              <span className="bkp-kpi-label">سلامت و نرخ موفقیت بکاپ</span>
              <span className="bkp-kpi-val" style={{ color: overview.health === 'Healthy' ? '#16a34a' : '#d97706' }} dir="ltr" data-latin-digits="true">
                {overview.successRatePercent}%
              </span>
              <span className="bkp-kpi-sub">
                موفق: {toPersianNumber(overview.completedCount)} • ناموفق: {toPersianNumber(overview.failedCount)}
              </span>
            </div>
            <div className="bkp-kpi-card">
              <span className="bkp-kpi-label">آخرین بکاپ موفق</span>
              <span className="bkp-kpi-val" style={{ fontSize: '0.94rem' }}>
                {overview.lastSuccessfulBackupAt ? formatDateTime(overview.lastSuccessfulBackupAt) : 'هنوز گرفته نشده'}
              </span>
              <span className="bkp-kpi-sub" dir="ltr" data-latin-digits="true">
                {overview.lastSuccessfulBackupSizeBytes > 0 ? formatBytes(overview.lastSuccessfulBackupSizeBytes) : '—'}
              </span>
            </div>
            <div className="bkp-kpi-card">
              <span className="bkp-kpi-label">حجم کل بکاپ‌ها / فضای آزاد</span>
              <span className="bkp-kpi-val" dir="ltr" data-latin-digits="true">
                {formatBytes(overview.totalBackupStorageBytes)}
              </span>
              <span className="bkp-kpi-sub" dir="ltr" data-latin-digits="true">
                Free Disk: {overview.freeDiskGb} GB
              </span>
            </div>
            <div className="bkp-kpi-card">
              <span className="bkp-kpi-label">جاب‌های فعال / کلاود</span>
              <span className="bkp-kpi-val">
                {toPersianNumber(overview.activeJobsCount)} جاب • {toPersianNumber(overview.cloudTargetsCount)} کلاود
              </span>
              <span className="bkp-kpi-sub">
                {overview.nextScheduledRunAt
                  ? `اجرای بعدی: ${formatDateTime(overview.nextScheduledRunAt)}`
                  : 'جاب زمان‌بندی فعالی نیست'}
              </span>
            </div>
          </div>
        ) : null}

        <div className="bkp-tabs">
          {TABS.map((t) => (
            <button
              key={t.key}
              type="button"
              className={`bkp-tab ${activeTab === t.key ? 'is-active' : ''}`}
              onClick={() => setActiveTab(t.key)}
            >
              {t.label}
            </button>
          ))}
        </div>
      </div>

      {loading ? (
        <div className="panel"><div className="bkp-empty">در حال بارگذاری مرکز بکاپ...</div></div>
      ) : null}

      {/* ── TAB 1: BACKUP RECORDS & MANUAL RUN ── */}
      {overview && activeTab === 'records' ? (
        <>
          {canManage ? (
            <form className="panel" onSubmit={handleRunManual}>
              <div className="panel-head">
                <div>
                  <h3>اجرای فوری بکاپ دستی</h3>
                  <p>بکاپ به‌صورت فایل ZIP فشرده همراه با چک‌سام SHA-256 ذخیره و در صورت انتخاب، به کلاود ارسال می‌شود.</p>
                </div>
              </div>
              <div className="bkp-run-bar">
                <label>
                  <span>محدوده بکاپ</span>
                  <select value={manualScope} onChange={(e) => setManualScope(e.target.value)}>
                    {SCOPES.map((s) => (
                      <option key={s.key} value={s.key}>{s.label}</option>
                    ))}
                  </select>
                </label>
                <label>
                  <span>ارسال خودکار به کلاود (اختیاری)</span>
                  <select value={manualCloudId} onChange={(e) => setManualCloudId(e.target.value)}>
                    <option value="">فقط ذخیره روی سرور محلی</option>
                    {enabledClouds.map((c) => (
                      <option key={c.id} value={c.id}>{c.name} ({c.provider})</option>
                    ))}
                  </select>
                </label>
                <button type="submit" className="primary-button" disabled={busy}>
                  {busy ? 'در حال تهیه بکاپ...' : '⚡ تهیه بکاپ فوری'}
                </button>
              </div>
            </form>
          ) : null}

          <div className="panel">
            <div className="panel-head">
              <div>
                <h3>تاریخچه بکاپ‌ها ({toPersianNumber(overview.records.length)})</h3>
              </div>
            </div>
            {overview.records.length === 0 ? (
              <div className="bkp-empty">هنوز بکاپی گرفته نشده است. از بخش بالا اولین بکاپ را بگیرید.</div>
            ) : (
              <div className="bkp-table-wrap">
                <table className="bkp-table">
                  <thead>
                    <tr>
                      <th>کد بکاپ</th>
                      <th>تاریخ</th>
                      <th>محدوده</th>
                      <th>حجم</th>
                      <th>مدت</th>
                      <th>وضعیت</th>
                      <th>کلاود</th>
                      <th>عملیات</th>
                    </tr>
                  </thead>
                  <tbody>
                    {overview.records.map((r) => (
                      <tr key={r.id}>
                        <td>
                          <strong dir="ltr" data-latin-digits="true">{r.backupCode}</strong>
                          <div style={{ fontSize: '0.7rem', color: 'var(--muted)' }}>{r.jobName || r.triggerType}</div>
                        </td>
                        <td>{formatDateTime(r.startedAt)}</td>
                        <td>{scopeLabel(r.scope)}</td>
                        <td dir="ltr" data-latin-digits="true">{formatBytes(r.fileSizeBytes)}</td>
                        <td dir="ltr" data-latin-digits="true">{r.durationMs} ms</td>
                        <td>
                          <span className={`bkp-pill ${r.status === 'Completed' ? 'is-ok' : r.status === 'Failed' ? 'is-err' : 'is-warn'}`}>
                            {r.status === 'Completed' ? 'موفق' : r.status === 'Failed' ? 'ناموفق' : 'در حال اجرا'}
                          </span>
                        </td>
                        <td>
                          {r.cloudStatus === 'Uploaded' ? (
                            <span className="bkp-pill is-ok">☁️ {r.cloudTargetName}</span>
                          ) : r.cloudStatus === 'Failed' ? (
                            <span className="bkp-pill is-err" title={r.cloudErrorMessage}>خطای کلاود</span>
                          ) : (
                            <span className="bkp-pill is-muted">محلی</span>
                          )}
                        </td>
                        <td>
                          <div className="bkp-actions">
                            {r.status === 'Completed' && r.fileExistsOnDisk ? (
                              <>
                                <button type="button" className="ghost-button" onClick={() => handleDownload(r)}>
                                  ⬇️ دانلود
                                </button>
                                <button type="button" className="ghost-button" disabled={busy} onClick={() => handleVerify(r)} title="بررسی یکپارچگی SHA-256">
                                  🛡️ سلامت
                                </button>
                                {canManage && enabledClouds.length > 0 ? (
                                  <select
                                    aria-label="ارسال به کلاود"
                                    defaultValue=""
                                    disabled={busy}
                                    onChange={(e) => {
                                      if (e.target.value) {
                                        handleUploadToCloud(r, e.target.value)
                                        e.target.value = ''
                                      }
                                    }}
                                  >
                                    <option value="">☁️ ارسال به کلاود...</option>
                                    {enabledClouds.map((c) => (
                                      <option key={c.id} value={c.id}>{c.name}</option>
                                    ))}
                                  </select>
                                ) : null}
                              </>
                            ) : null}
                            {canManage ? (
                              <button type="button" className="ghost-button" style={{ color: '#dc2626' }} disabled={busy} onClick={() => handleDeleteBackup(r)}>
                                حذف
                              </button>
                            ) : null}
                          </div>
                        </td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            )}
          </div>
        </>
      ) : null}

      {/* ── TAB 2: SCHEDULED BACKUP JOBS ── */}
      {overview && activeTab === 'jobs' ? (
        <>
          {canManage ? (
            <form className="panel" onSubmit={handleSaveJob}>
              <div className="panel-head">
                <div>
                  <h3>{jobForm.id ? 'ویرایش جاب بکاپ' : 'تعریف جاب زمان‌بندی‌شده بکاپ'}</h3>
                  <p>زمان‌بندی کاملاً خودکار بر اساس ساعت تهران با پاک‌سازی خودکار بکاپ‌های قدیمی‌تر از سقف نگهداری.</p>
                </div>
              </div>
              <div className="bkp-form-grid-3">
                <label>
                  <span>نام جاب *</span>
                  <input required placeholder="مثلاً بکاپ شبانه کامل کافه" value={jobForm.name} onChange={(e) => setJobForm((f) => ({ ...f, name: e.target.value }))} />
                </label>
                <label>
                  <span>محدوده بکاپ</span>
                  <select value={jobForm.scope} onChange={(e) => setJobForm((f) => ({ ...f, scope: e.target.value }))}>
                    {SCOPES.map((s) => <option key={s.key} value={s.key}>{s.label}</option>)}
                  </select>
                </label>
                <label>
                  <span>دوره تکرار</span>
                  <select value={jobForm.scheduleType} onChange={(e) => setJobForm((f) => ({ ...f, scheduleType: e.target.value }))}>
                    {SCHEDULE_TYPES.map((s) => <option key={s.key} value={s.key}>{s.label}</option>)}
                  </select>
                </label>
              </div>

              <div className="bkp-form-grid-3">
                {jobForm.scheduleType === 'Hourly' ? (
                  <label>
                    <span>فاصله اجرا (هر چند ساعت)</span>
                    <input type="number" min="1" max="72" className="ltr-input" value={jobForm.intervalHours} onChange={(e) => setJobForm((f) => ({ ...f, intervalHours: e.target.value }))} />
                  </label>
                ) : (
                  <label>
                    <span>ساعت اجرا (وقت تهران ۰ تا ۲۳)</span>
                    <input type="number" min="0" max="23" className="ltr-input" value={jobForm.runHourTehran} onChange={(e) => setJobForm((f) => ({ ...f, runHourTehran: e.target.value }))} />
                  </label>
                )}

                {jobForm.scheduleType === 'Weekly' ? (
                  <label>
                    <span>روز هفته</span>
                    <select value={jobForm.dayOfWeek} onChange={(e) => setJobForm((f) => ({ ...f, dayOfWeek: e.target.value }))}>
                      {WEEK_DAYS.map((d) => <option key={d.key} value={d.key}>{d.label}</option>)}
                    </select>
                  </label>
                ) : (
                  <label>
                    <span>سقف نگهداری نسخه‌های محلی (Retention)</span>
                    <input type="number" min="1" max="100" className="ltr-input" value={jobForm.retentionCount} onChange={(e) => setJobForm((f) => ({ ...f, retentionCount: e.target.value }))} />
                  </label>
                )}

                <label>
                  <span>ارسال خودکار به کلاود (اختیاری)</span>
                  <select value={jobForm.cloudTargetId} onChange={(e) => setJobForm((f) => ({ ...f, cloudTargetId: e.target.value }))}>
                    <option value="">فقط نگهداری محلی</option>
                    {enabledClouds.map((c) => <option key={c.id} value={c.id}>{c.name}</option>)}
                  </select>
                </label>
              </div>

              <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', flexWrap: 'wrap', gap: '0.6rem' }}>
                <label className="checkbox-row" style={{ flexDirection: 'row', alignItems: 'center' }}>
                  <input type="checkbox" checked={jobForm.isEnabled} onChange={(e) => setJobForm((f) => ({ ...f, isEnabled: e.target.checked }))} />
                  <span>جاب فعال باشد</span>
                </label>
                <div style={{ display: 'flex', gap: '0.5rem' }}>
                  {jobForm.id ? <button type="button" className="ghost-button" onClick={() => setJobForm(emptyJobForm())}>انصراف</button> : null}
                  <button type="submit" className="primary-button" disabled={busy}>
                    {jobForm.id ? 'ذخیره تغییرات جاب' : '+ ثبت جاب بکاپ'}
                  </button>
                </div>
              </div>
            </form>
          ) : null}

          <div className="panel">
            <div className="panel-head"><div><h3>جاب‌های تعریف‌شده ({toPersianNumber(overview.jobs.length)})</h3></div></div>
            {overview.jobs.length === 0 ? (
              <div className="bkp-empty">هنوز جاب زمان‌بندی‌شده‌ای تعریف نشده است.</div>
            ) : (
              <div className="bkp-table-wrap">
                <table className="bkp-table">
                  <thead>
                    <tr>
                      <th>نام جاب</th>
                      <th>محدوده</th>
                      <th>زمان‌بندی</th>
                      <th>نگهداری</th>
                      <th>کلاود</th>
                      <th>اجرای بعدی</th>
                      <th>وضعیت</th>
                      <th>عملیات</th>
                    </tr>
                  </thead>
                  <tbody>
                    {overview.jobs.map((j) => (
                      <tr key={j.id}>
                        <td><strong>{j.name}</strong></td>
                        <td>{scopeLabel(j.scope)}</td>
                        <td>{scheduleLabel(j)}</td>
                        <td>{toPersianNumber(j.retentionCount)} نسخه</td>
                        <td>{j.cloudTargetName || 'محلی'}</td>
                        <td>{j.nextRunAt ? formatDateTime(j.nextRunAt) : '—'}</td>
                        <td><span className={`bkp-pill ${j.isEnabled ? 'is-ok' : 'is-muted'}`}>{j.isEnabled ? 'فعال' : 'غیرفعال'}</span></td>
                        <td>
                          <div className="bkp-actions">
                            {canManage ? (
                              <>
                                <button type="button" className="ghost-button" disabled={busy} onClick={() => handleRunJobNow(j)}>▶ اجرا</button>
                                <button
                                  type="button"
                                  className="ghost-button"
                                  onClick={() =>
                                    setJobForm({
                                      id: j.id,
                                      name: j.name,
                                      scope: j.scope,
                                      scheduleType: j.scheduleType,
                                      intervalHours: String(j.intervalHours),
                                      runHourTehran: String(j.runHourTehran),
                                      runMinuteTehran: String(j.runMinuteTehran),
                                      dayOfWeek: String(j.dayOfWeek ?? 5),
                                      retentionCount: String(j.retentionCount),
                                      cloudTargetId: j.cloudTargetId || '',
                                      isEnabled: j.isEnabled,
                                    })
                                  }
                                >
                                  ویرایش
                                </button>
                                <button type="button" className="ghost-button" style={{ color: '#dc2626' }} onClick={() => handleDeleteJob(j)}>حذف</button>
                              </>
                            ) : null}
                          </div>
                        </td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            )}
          </div>
        </>
      ) : null}

      {/* ── TAB 3: MULTI-CLOUD TARGETS ── */}
      {overview && activeTab === 'cloud' ? (
        <>
          {canManage ? (
            <form className="panel" onSubmit={handleSaveCloud}>
              <div className="panel-head">
                <div>
                  <h3>{cloudForm.id ? 'ویرایش مقصد ابری' : 'اتصال فضای ابری جدید (Multi-Cloud S3)'}</h3>
                  <p>پشتیبانی بومی با امضای استاندارد AWS Signature V4 از آروان کلود، لیارا، پارس‌پک، AWS S3، Cloudflare R2، MinIO و وب‌هوک سفارشی.</p>
                </div>
              </div>
              <div className="bkp-form-grid-3">
                <label>
                  <span>نام مقصد ابری *</span>
                  <input required placeholder="مثلاً باکت بکاپ آروان" value={cloudForm.name} onChange={(e) => setCloudForm((f) => ({ ...f, name: e.target.value }))} />
                </label>
                <label>
                  <span>ارائه‌دهنده کلاود</span>
                  <select value={cloudForm.provider} onChange={(e) => handleCloudProviderChange(e.target.value)}>
                    {CLOUD_PROVIDERS.map((p) => <option key={p.key} value={p.key}>{p.label}</option>)}
                  </select>
                </label>
                <label>
                  <span>آدرس Endpoint *</span>
                  <input required className="ltr-input" dir="ltr" data-latin-digits="true" value={cloudForm.endpointUrl} onChange={(e) => setCloudForm((f) => ({ ...f, endpointUrl: e.target.value }))} />
                </label>
              </div>
              <div className="bkp-form-grid-3">
                <label>
                  <span>نام Bucket</span>
                  <input className="ltr-input" dir="ltr" data-latin-digits="true" placeholder="sugarista-backups" value={cloudForm.bucketName} onChange={(e) => setCloudForm((f) => ({ ...f, bucketName: e.target.value }))} />
                </label>
                <label>
                  <span>Region</span>
                  <input className="ltr-input" dir="ltr" data-latin-digits="true" value={cloudForm.region} onChange={(e) => setCloudForm((f) => ({ ...f, region: e.target.value }))} />
                </label>
                <label>
                  <span>مسیر پوشه (Prefix)</span>
                  <input className="ltr-input" dir="ltr" data-latin-digits="true" value={cloudForm.keyPrefix} onChange={(e) => setCloudForm((f) => ({ ...f, keyPrefix: e.target.value }))} />
                </label>
              </div>
              <div className="bkp-form-grid-2">
                <label>
                  <span>Access Key</span>
                  <input className="ltr-input" dir="ltr" data-latin-digits="true" placeholder={cloudForm.id ? 'خالی = بدون تغییر' : 'Access Key'} value={cloudForm.accessKey} onChange={(e) => setCloudForm((f) => ({ ...f, accessKey: e.target.value }))} />
                </label>
                <label>
                  <span>Secret Key (یا Bearer Token برای وب‌هوک)</span>
                  <input type="password" className="ltr-input" dir="ltr" autoComplete="new-password" placeholder={cloudForm.id ? 'خالی = بدون تغییر' : 'Secret Key'} value={cloudForm.secretKey} onChange={(e) => setCloudForm((f) => ({ ...f, secretKey: e.target.value }))} />
                </label>
              </div>
              <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', flexWrap: 'wrap', gap: '0.6rem' }}>
                <div style={{ display: 'flex', gap: '1rem', flexWrap: 'wrap' }}>
                  <label className="checkbox-row" style={{ flexDirection: 'row', alignItems: 'center' }}>
                    <input type="checkbox" checked={cloudForm.isDefault} onChange={(e) => setCloudForm((f) => ({ ...f, isDefault: e.target.checked }))} />
                    <span>مقصد پیش‌فرض</span>
                  </label>
                  <label className="checkbox-row" style={{ flexDirection: 'row', alignItems: 'center' }}>
                    <input type="checkbox" checked={cloudForm.isEnabled} onChange={(e) => setCloudForm((f) => ({ ...f, isEnabled: e.target.checked }))} />
                    <span>فعال</span>
                  </label>
                </div>
                <div style={{ display: 'flex', gap: '0.5rem' }}>
                  {cloudForm.id ? <button type="button" className="ghost-button" onClick={() => setCloudForm(emptyCloudForm())}>انصراف</button> : null}
                  <button type="submit" className="primary-button" disabled={busy}>
                    {cloudForm.id ? 'ذخیره تغییرات کلاود' : '+ افزودن مقصد ابری'}
                  </button>
                </div>
              </div>
            </form>
          ) : null}

          <div className="panel">
            <div className="panel-head"><div><h3>مقصدهای ابری متصل ({toPersianNumber(overview.cloudTargets.length)})</h3></div></div>
            {overview.cloudTargets.length === 0 ? (
              <div className="bkp-empty">هنوز فضای ابری متصل نشده است.</div>
            ) : (
              <div className="bkp-table-wrap">
                <table className="bkp-table">
                  <thead>
                    <tr>
                      <th>نام</th>
                      <th>سرویس</th>
                      <th>Bucket / Endpoint</th>
                      <th>آخرین تست اتصال</th>
                      <th>عملیات</th>
                    </tr>
                  </thead>
                  <tbody>
                    {overview.cloudTargets.map((t) => (
                      <tr key={t.id}>
                        <td>
                          <strong>{t.name}</strong>
                          {t.isDefault ? <span className="bkp-pill is-ok" style={{ marginInlineStart: 6 }}>پیش‌فرض</span> : null}
                        </td>
                        <td>{t.provider}</td>
                        <td dir="ltr" data-latin-digits="true">
                          <strong>{t.bucketName || '—'}</strong>
                          <div style={{ fontSize: '0.7rem', color: 'var(--muted)' }}>{t.endpointUrl}</div>
                        </td>
                        <td>
                          {t.lastTestedAt ? (
                            <span className={`bkp-pill ${t.lastTestSuccess ? 'is-ok' : 'is-err'}`} title={t.lastTestMessage}>
                              {t.lastTestSuccess ? 'متصل ✓' : 'خطا ✕'}
                            </span>
                          ) : (
                            <span className="bkp-pill is-muted">تست نشده</span>
                          )}
                        </td>
                        <td>
                          <div className="bkp-actions">
                            {canManage ? (
                              <>
                                <button type="button" className="ghost-button" disabled={busy} onClick={() => handleTestCloud(t)}>🔌 تست اتصال</button>
                                <button
                                  type="button"
                                  className="ghost-button"
                                  onClick={() =>
                                    setCloudForm({
                                      id: t.id,
                                      name: t.name,
                                      provider: t.provider,
                                      endpointUrl: t.endpointUrl,
                                      bucketName: t.bucketName,
                                      region: t.region,
                                      accessKey: '',
                                      secretKey: '',
                                      keyPrefix: t.keyPrefix,
                                      isDefault: t.isDefault,
                                      isEnabled: t.isEnabled,
                                    })
                                  }
                                >
                                  ویرایش
                                </button>
                                <button type="button" className="ghost-button" style={{ color: '#dc2626' }} onClick={() => handleDeleteCloud(t)}>حذف</button>
                              </>
                            ) : null}
                          </div>
                        </td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            )}
          </div>
        </>
      ) : null}

      {/* ── TAB 4: BACKUP MONITORING & HEALTH ── */}
      {overview && activeTab === 'health' ? (
        <div className="panel">
          <div className="panel-head">
            <div>
              <h3>مانیتورینگ روند سلامت ۳۰ بکاپ اخیر</h3>
              <p>سبز = موفق • قرمز = ناموفق • نارنجی = در حال اجرا</p>
            </div>
          </div>

          {overview.records.length === 0 ? (
            <div className="bkp-empty">هنوز سابقه‌ای برای نمایش روند سلامت ثبت نشده است.</div>
          ) : (
            <div className="bkp-health-bars">
              {overview.records.slice(0, 30).reverse().map((r) => (
                <div
                  key={r.id}
                  className={`bkp-health-tick ${r.status === 'Completed' ? 'is-ok' : r.status === 'Failed' ? 'is-err' : 'is-run'}`}
                  title={`${r.backupCode} — ${r.status} (${formatBytes(r.fileSizeBytes)})`}
                />
              ))}
            </div>
          )}

          {overview.records.some((r) => r.status === 'Failed' || r.cloudStatus === 'Failed') ? (
            <div className="bkp-table-wrap" style={{ marginTop: '0.85rem' }}>
              <table className="bkp-table">
                <thead>
                  <tr>
                    <th>کد بکاپ</th>
                    <th>تاریخ</th>
                    <th>نوع خطا</th>
                    <th>جزئیات خطا</th>
                  </tr>
                </thead>
                <tbody>
                  {overview.records
                    .filter((r) => r.status === 'Failed' || r.cloudStatus === 'Failed')
                    .map((r) => (
                      <tr key={r.id}>
                        <td dir="ltr" data-latin-digits="true">{r.backupCode}</td>
                        <td>{formatDateTime(r.startedAt)}</td>
                        <td>
                          <span className="bkp-pill is-err">
                            {r.status === 'Failed' ? 'خطای بکاپ محلی' : 'خطای ارسال ابری'}
                          </span>
                        </td>
                        <td style={{ whiteSpace: 'normal', maxWidth: 420 }}>
                          {r.errorMessage || r.cloudErrorMessage}
                        </td>
                      </tr>
                    ))}
                </tbody>
              </table>
            </div>
          ) : (
            <p className="feedback success" style={{ marginTop: '0.75rem' }}>
              هیچ خطای بکاپ یا ارسال ابری در تاریخچه اخیر ثبت نشده است ✓
            </p>
          )}
        </div>
      ) : null}
    </section>
  )
}
