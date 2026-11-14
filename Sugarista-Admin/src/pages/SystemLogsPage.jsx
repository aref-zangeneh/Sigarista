import { useCallback, useEffect, useState } from 'react'
import { apiRequest, getReadableError } from '../utils/adminApi'
import { useAdmin } from '../context/AdminContext'
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

export function SystemLogsPage() {
  const { session } = useAdmin()
  const [status, setStatus] = useState(null)
  const [selectedLevel, setSelectedLevel] = useState('')
  const [pending, setPending] = useState(false)
  const [flash, setFlash] = useState('')
  const [error, setError] = useState('')

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
        <h2>چه چیزی در لاگ ثبت می‌شود؟</h2>
        <p className="system-logs-note">
          • هر درخواست HTTP با کد وضعیت و زمان پاسخ — بدون بدنهٔ Request/Response و بدون هیچ دادهٔ حساس
          (پسورد، توکن و کلیدها قبل از نوشتن حذف می‌شوند).{'\n'}
          • درخواست‌های کند (بیش از ۱ ثانیه) و خطاهای سرور با سطح بالاتر برجسته می‌شوند تا در جست‌وجو دیده شوند.
          {'\n'}
          • لاگ‌های تکراری پشت‌سرهم فشرده می‌شوند و فقط یک خلاصه ذخیره می‌شود.
          {'\n'}
          • رخدادهای حساس (باز/بسته شدن مسیرها، تغییر رمز دیتابیس، تغییر سطح لاگ) در فایل audit جداگانه
          ثبت می‌شوند و هرگز حذف نمی‌شوند.
        </p>
      </section>
    </div>
  )
}
