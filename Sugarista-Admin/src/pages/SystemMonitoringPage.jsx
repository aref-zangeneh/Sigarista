import { useCallback, useEffect, useRef, useState } from 'react'
import { useAdmin } from '../context/AdminContext'
import { API_BASE_URL } from '../utils/adminApi'
import { hasPermission, PERMISSIONS } from '../utils/adminPermissions'
import './MenuSectionMinimal.css'
import './SystemMonitoringPage.css'

const TABS = [
  { key: 'overview', label: '🖥️ سرور و منابع' },
  { key: 'apm', label: '⚡ مانیتورینگ پروژه (APM)' },
  { key: 'security', label: '🛡️ مانیتورینگ امنیتی' },
]

function formatUptime(seconds, toPersianNumber) {
  const s = Math.max(0, Number(seconds) || 0)
  const days = Math.floor(s / 86400)
  const hours = Math.floor((s % 86400) / 3600)
  const mins = Math.floor((s % 3600) / 60)
  if (days > 0) return `${toPersianNumber(days)} روز و ${toPersianNumber(hours)} ساعت`
  if (hours > 0) return `${toPersianNumber(hours)} ساعت و ${toPersianNumber(mins)} دقیقه`
  return `${toPersianNumber(mins)} دقیقه`
}

function barLevelClass(pct, warn = 70, crit = 88) {
  if (pct >= crit) return 'is-crit'
  if (pct >= warn) return 'is-warn'
  return ''
}

function Sparkline({ values = [], max = 100, color = '#aa3171' }) {
  if (!values.length) return null
  const width = 220
  const height = 44
  const safeMax = Math.max(max, ...values, 1)
  const stepX = values.length > 1 ? width / (values.length - 1) : width

  const points = values
    .map((val, idx) => {
      const x = idx * stepX
      const y = height - 4 - (Math.max(0, val) / safeMax) * (height - 8)
      return `${x.toFixed(1)},${y.toFixed(1)}`
    })
    .join(' ')

  return (
    <svg className="mon-sparkline" viewBox={`0 0 ${width} ${height}`} preserveAspectRatio="none" aria-hidden="true">
      <polyline
        fill="none"
        stroke={color}
        strokeWidth="2"
        strokeLinecap="round"
        strokeLinejoin="round"
        points={points}
      />
    </svg>
  )
}

export function SystemMonitoringPage() {
  const {
    session,
    apiRequest,
    getReadableError,
    setMessage,
    setError,
    toPersianNumber,
    formatDateTime,
  } = useAdmin()

  const canReset = hasPermission(session, PERMISSIONS['site-settings']?.update)

  const [activeTab, setActiveTab] = useState('overview')
  const [snapshot, setSnapshot] = useState(null)
  const [loading, setLoading] = useState(true)
  const [liveEnabled, setLiveEnabled] = useState(true)
  const [streamConnected, setStreamConnected] = useState(false)
  const abortRef = useRef(null)

  const fetchOnce = useCallback(async () => {
    if (!session?.token) return
    try {
      const data = await apiRequest('/admin/monitoring/snapshot', { token: session.token })
      setSnapshot(data)
    } catch (err) {
      setError(getReadableError(err))
    } finally {
      setLoading(false)
    }
  }, [session?.token, apiRequest, getReadableError, setError])

  // Real-time SSE stream (zero cost when paused or page closed)
  useEffect(() => {
    if (!session?.token) return undefined

    if (!liveEnabled) {
      setStreamConnected(false)
      if (!snapshot) fetchOnce()
      return undefined
    }

    let cancelled = false
    const controller = new AbortController()
    abortRef.current = controller

    async function connectStream() {
      try {
        const response = await fetch(`${API_BASE_URL}/admin/monitoring/stream`, {
          headers: {
            Authorization: `Bearer ${session.token}`,
            Accept: 'text/event-stream',
          },
          signal: controller.signal,
        })

        if (!response.ok || !response.body) {
          throw new Error('SSE unavailable')
        }

        setStreamConnected(true)
        const reader = response.body.getReader()
        const decoder = new TextDecoder()
        let buffer = ''

        while (!cancelled) {
          const { done, value } = await reader.read()
          if (done) break
          buffer += decoder.decode(value, { stream: true })

          const chunks = buffer.split('\n\n')
          buffer = chunks.pop() || ''

          for (const chunk of chunks) {
            const dataLine = chunk
              .split('\n')
              .find((line) => line.startsWith('data:'))
            if (dataLine) {
              try {
                const parsed = JSON.parse(dataLine.slice(5).trim())
                if (!cancelled) {
                  setSnapshot(parsed)
                  setLoading(false)
                }
              } catch {
                // Ignore malformed partial frame
              }
            }
          }
        }
      } catch (err) {
        if (!cancelled && err.name !== 'AbortError') {
          setStreamConnected(false)
          await fetchOnce()
        }
      }
    }

    connectStream()

    return () => {
      cancelled = true
      controller.abort()
      setStreamConnected(false)
    }
  }, [session?.token, liveEnabled, fetchOnce])

  const handleReset = async () => {
    if (!canReset) return
    try {
      await apiRequest('/admin/monitoring/reset', {
        method: 'POST',
        token: session.token,
      })
      setMessage('شمارنده‌های مانیتورینگ با موفقیت بازنشانی شدند.')
      await fetchOnce()
    } catch (err) {
      setError(getReadableError(err))
    }
  }

  const srv = snapshot?.server
  const db = snapshot?.database
  const app = snapshot?.application
  const sec = snapshot?.security

  const healthLabel =
    snapshot?.overallHealth === 'Critical'
      ? 'وضعیت بحرانی'
      : snapshot?.overallHealth === 'Warning'
        ? 'نیازمند توجه'
        : 'سیستم سالم است'

  return (
    <section className="mon-page menu-minimal-scope">
      <div className="panel">
        <div className="mon-head-row">
          <div>
            <h2>مانیتورینگ هوشمند سرور، پروژه و امنیت</h2>
            <p>
              پایش لحظه‌ای منابع سرور، سلامت و سرعت مسیرهای پروژه و رصد تهدیدهای امنیتی با حداقل مصرف منابع (بدون بار روی دیتابیس).
            </p>
          </div>

          <div className="mon-head-right">
            {snapshot ? (
              <span className={`mon-health-badge is-${snapshot.overallHealth}`}>
                {snapshot.overallHealth === 'Healthy' ? '✓' : '⚠'} {healthLabel}
              </span>
            ) : null}
            <span className={`mon-live-pill ${liveEnabled && streamConnected ? 'is-live' : ''}`}>
              <span className="mon-live-dot" />
              {liveEnabled && streamConnected ? 'زنده (Real-Time • ۳ ثانیه)' : 'متوقف / دستی'}
            </span>
            <button
              type="button"
              className="ghost-button"
              onClick={() => setLiveEnabled((v) => !v)}
            >
              {liveEnabled ? '⏸ توقف زنده' : '▶ شروع زنده'}
            </button>
            <button type="button" className="ghost-button" onClick={fetchOnce}>
              🔄 بروزرسانی
            </button>
            {canReset ? (
              <button type="button" className="ghost-button" onClick={handleReset}>
                پاک‌سازی آمار
              </button>
            ) : null}
          </div>
        </div>

        <div className="mon-tabs">
          {TABS.map((t) => (
            <button
              key={t.key}
              type="button"
              className={`mon-tab ${activeTab === t.key ? 'is-active' : ''}`}
              onClick={() => setActiveTab(t.key)}
            >
              {t.label}
              {t.key === 'apm' && app?.total5xx > 0 ? ` (${toPersianNumber(app.total5xx)} خطا)` : ''}
              {t.key === 'security' && sec?.highSeverityEvents > 0
                ? ` (${toPersianNumber(sec.highSeverityEvents)} هشدار)`
                : ''}
            </button>
          ))}
        </div>
      </div>

      {loading && !snapshot ? (
        <div className="panel">
          <div className="mon-empty">در حال برقراری اتصال زنده مانیتورینگ...</div>
        </div>
      ) : null}

      {/* ── TAB 1: SERVER & INFRASTRUCTURE ── */}
      {snapshot && activeTab === 'overview' ? (
        <>
          <div className="mon-grid-4">
            <div className="mon-card">
              <div className="mon-card-top">
                <span className="mon-card-label">مصرف CPU فرآیند</span>
                <span className="mon-card-sub">{toPersianNumber(srv.processorCount)} هسته</span>
              </div>
              <div className="mon-card-val" dir="ltr" data-latin-digits="true">
                {srv.cpuUsagePercent}%
              </div>
              <div className="mon-bar">
                <div
                  className={`mon-bar-fill ${barLevelClass(srv.cpuUsagePercent)}`}
                  style={{ width: `${Math.min(100, srv.cpuUsagePercent)}%` }}
                />
              </div>
              <Sparkline
                values={(srv.history ?? []).map((h) => h.cpuPercent)}
                max={100}
                color="#aa3171"
              />
            </div>

            <div className="mon-card">
              <div className="mon-card-top">
                <span className="mon-card-label">حافظه RAM مصرفی</span>
                <span className="mon-card-sub" dir="ltr" data-latin-digits="true">
                  {srv.memoryUsagePercent}%
                </span>
              </div>
              <div className="mon-card-val" dir="ltr" data-latin-digits="true">
                {srv.workingSetMb} MB
              </div>
              <div className="mon-bar">
                <div
                  className={`mon-bar-fill ${barLevelClass(srv.memoryUsagePercent, 75, 90)}`}
                  style={{ width: `${Math.min(100, srv.memoryUsagePercent)}%` }}
                />
              </div>
              <span className="mon-card-sub">
                Heap: <strong dir="ltr" data-latin-digits="true">{srv.managedHeapMb} MB</strong>
                {srv.totalSystemMemoryMb > 0 ? (
                  <>
                    {' • '}کل سیستم: <strong dir="ltr" data-latin-digits="true">{srv.totalSystemMemoryMb} MB</strong>
                  </>
                ) : null}
              </span>
              <Sparkline
                values={(srv.history ?? []).map((h) => h.memoryMb)}
                max={Math.max(256, srv.workingSetMb * 1.4)}
                color="#2563eb"
              />
            </div>

            <div className="mon-card">
              <div className="mon-card-top">
                <span className="mon-card-label">دیتابیس PostgreSQL</span>
                <span className={`mon-pill ${db.isHealthy ? 'is-ok' : 'is-err'}`}>
                  {db.isHealthy ? 'متصل' : 'قطع'}
                </span>
              </div>
              <div className="mon-card-val" dir="ltr" data-latin-digits="true">
                {db.latencyMs} ms
              </div>
              <span className="mon-card-sub">{db.statusMessage}</span>
              <span className="mon-card-sub">
                ThreadPool فعال: <strong>{toPersianNumber(srv.activeWorkerThreads)}</strong> • صف:{' '}
                <strong>{toPersianNumber(srv.pendingThreadPoolWorkItems)}</strong>
              </span>
            </div>

            <div className="mon-card">
              <div className="mon-card-top">
                <span className="mon-card-label">زمان پایداری (Uptime)</span>
                <span className="mon-card-sub" dir="ltr" data-latin-digits="true">
                  {srv.machineName}
                </span>
              </div>
              <div className="mon-card-val">{formatUptime(srv.uptimeSeconds, toPersianNumber)}</div>
              <span className="mon-card-sub" dir="ltr" data-latin-digits="true">
                {srv.dotNetVersion}
              </span>
              <span className="mon-card-sub">
                GC: Gen0 ({toPersianNumber(srv.gcGen0Collections)}) • Gen1 ({toPersianNumber(srv.gcGen1Collections)}) • Gen2 ({toPersianNumber(srv.gcGen2Collections)})
              </span>
            </div>
          </div>

          {/* Storage / Disks */}
          <div className="panel">
            <div className="panel-head">
              <div>
                <h3>فضای ذخیره‌سازی سرور (Storage)</h3>
                <p>وضعیت دیسک‌ها و هشدار خودکار در صورت پر شدن بیش از ۸۵٪</p>
              </div>
            </div>
            {(srv.drives ?? []).length === 0 ? (
              <div className="mon-empty">اطلاعات درایوی در این محیط قابل خواندن نیست.</div>
            ) : (
              <div className="mon-drives">
                {srv.drives.map((d) => (
                  <div key={d.name} className="mon-drive-card">
                    <div className="mon-card-top">
                      <strong dir="ltr" data-latin-digits="true">{d.name}</strong>
                      <span className={`mon-pill ${d.usedPercent >= 90 ? 'is-err' : d.usedPercent >= 80 ? 'is-warn' : 'is-ok'}`} dir="ltr" data-latin-digits="true">
                        {d.usedPercent}% پر
                      </span>
                    </div>
                    <div className="mon-bar">
                      <div
                        className={`mon-bar-fill ${barLevelClass(d.usedPercent, 80, 92)}`}
                        style={{ width: `${Math.min(100, d.usedPercent)}%` }}
                      />
                    </div>
                    <div className="mon-card-sub" dir="ltr" data-latin-digits="true">
                      Used: {d.usedGb} GB / {d.totalGb} GB • Free: {d.freeGb} GB ({d.format})
                    </div>
                  </div>
                ))}
              </div>
            )}
          </div>
        </>
      ) : null}

      {/* ── TAB 2: APPLICATION APM & ENDPOINT HEALTH ── */}
      {snapshot && activeTab === 'apm' ? (
        <>
          <div className="mon-grid-4">
            <div className="mon-card">
              <span className="mon-card-label">کل درخواست‌ها / دقیقه</span>
              <div className="mon-card-val">{toPersianNumber(app.requestsPerMinute)} RPM</div>
              <span className="mon-card-sub">
                کل از شروع سرور: {toPersianNumber(app.totalRequests)} • همزمان: {toPersianNumber(app.inFlightRequests)}
              </span>
            </div>
            <div className="mon-card">
              <span className="mon-card-label">زمان پاسخ‌دهی (Latency)</span>
              <div className="mon-card-val" dir="ltr" data-latin-digits="true">
                {app.avgLatencyMs} ms
              </div>
              <span className="mon-card-sub">
                P95: <strong dir="ltr" data-latin-digits="true">{app.p95LatencyMs} ms</strong>
              </span>
            </div>
            <div className="mon-card">
              <span className="mon-card-label">خطاهای سرور (5xx)</span>
              <div className="mon-card-val" style={{ color: app.total5xx > 0 ? '#dc2626' : '#16a34a' }}>
                {toPersianNumber(app.total5xx)}
              </div>
              <span className="mon-card-sub">
                نرخ خطا: <strong dir="ltr" data-latin-digits="true">{app.errorRatePercent}%</strong>
              </span>
            </div>
            <div className="mon-card">
              <span className="mon-card-label">پاسخ‌های موفق / 4xx</span>
              <div className="mon-card-val">{toPersianNumber(app.total2xx)}</div>
              <span className="mon-card-sub">خطای کلاینت (4xx): {toPersianNumber(app.total4xx)}</span>
            </div>
          </div>

          <div className="panel">
            <div className="panel-head">
              <div>
                <h3>سلامت و سرعت مسیرهای API پروژه (Endpoint Health)</h3>
                <p>مسیرهایی که خطا یا کندی دارند به صورت خودکار در بالای جدول نمایش داده می‌شوند.</p>
              </div>
            </div>
            {(app.endpoints ?? []).length === 0 ? (
              <div className="mon-empty">هنوز درخواستی ثبت نشده است.</div>
            ) : (
              <div className="mon-table-wrap">
                <table className="mon-table">
                  <thead>
                    <tr>
                      <th>وضعیت</th>
                      <th>مسیر API</th>
                      <th>تعداد</th>
                      <th>4xx</th>
                      <th>5xx</th>
                      <th>میانگین (ms)</th>
                      <th>حداکثر (ms)</th>
                    </tr>
                  </thead>
                  <tbody>
                    {app.endpoints.map((ep) => (
                      <tr key={`${ep.method}-${ep.path}`}>
                        <td>
                          <span
                            className={`mon-pill ${
                              ep.health === 'Error' ? 'is-err' : ep.health === 'Slow' ? 'is-warn' : 'is-ok'
                            }`}
                          >
                            {ep.health === 'Error' ? 'خطا' : ep.health === 'Slow' ? 'کند' : 'سالم'}
                          </span>
                        </td>
                        <td>
                          <span className="mon-code-path" data-latin-digits="true">
                            <strong>{ep.method}</strong> {ep.path}
                          </span>
                        </td>
                        <td>{toPersianNumber(ep.totalRequests)}</td>
                        <td>{toPersianNumber(ep.error4xxCount)}</td>
                        <td style={{ color: ep.error5xxCount > 0 ? '#dc2626' : undefined, fontWeight: ep.error5xxCount > 0 ? 700 : 400 }}>
                          {toPersianNumber(ep.error5xxCount)}
                        </td>
                        <td dir="ltr" data-latin-digits="true">{ep.avgDurationMs}</td>
                        <td dir="ltr" data-latin-digits="true">{ep.maxDurationMs}</td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            )}
          </div>

          <div className="mon-grid-2">
            <div className="panel">
              <div className="panel-head">
                <div>
                  <h3>آخرین خطاهای سرور (5xx / Exceptions)</h3>
                </div>
              </div>
              {(app.recentErrors ?? []).length === 0 ? (
                <div className="mon-empty">هیچ خطای ۵xx ثبت نشده است ✓</div>
              ) : (
                <div className="mon-table-wrap">
                  <table className="mon-table">
                    <thead>
                      <tr>
                        <th>زمان</th>
                        <th>مسیر</th>
                        <th>کد</th>
                        <th>پیام خطا</th>
                      </tr>
                    </thead>
                    <tbody>
                      {app.recentErrors.map((err, i) => (
                        <tr key={i}>
                          <td>{formatDateTime(err.timestamp)}</td>
                          <td><span className="mon-code-path" data-latin-digits="true">{err.method} {err.path}</span></td>
                          <td><span className="mon-pill is-err" dir="ltr" data-latin-digits="true">{err.statusCode}</span></td>
                          <td style={{ whiteSpace: 'normal', maxWidth: 260 }}>{err.message}</td>
                        </tr>
                      ))}
                    </tbody>
                  </table>
                </div>
              )}
            </div>

            <div className="panel">
              <div className="panel-head">
                <div>
                  <h3>درخواست‌های کند (بیش از ۵۰۰ms)</h3>
                </div>
              </div>
              {(app.slowRequests ?? []).length === 0 ? (
                <div className="mon-empty">همه درخواست‌ها سریع و زیر ۵۰۰ms بوده‌اند ✓</div>
              ) : (
                <div className="mon-table-wrap">
                  <table className="mon-table">
                    <thead>
                      <tr>
                        <th>زمان</th>
                        <th>مسیر</th>
                        <th>مدت (ms)</th>
                        <th>کد</th>
                      </tr>
                    </thead>
                    <tbody>
                      {app.slowRequests.map((req, i) => (
                        <tr key={i}>
                          <td>{formatDateTime(req.timestamp)}</td>
                          <td><span className="mon-code-path" data-latin-digits="true">{req.method} {req.path}</span></td>
                          <td><span className="mon-pill is-warn" dir="ltr" data-latin-digits="true">{req.durationMs} ms</span></td>
                          <td dir="ltr" data-latin-digits="true">{req.statusCode}</td>
                        </tr>
                      ))}
                    </tbody>
                  </table>
                </div>
              )}
            </div>
          </div>
        </>
      ) : null}

      {/* ── TAB 3: SECURITY & THREAT INTELLIGENCE ── */}
      {snapshot && activeTab === 'security' ? (
        <>
          <div className="mon-grid-4">
            <div className="mon-card">
              <span className="mon-card-label">امتیاز امنیت زنده</span>
              <div
                className="mon-card-val"
                style={{
                  color: sec.securityScore >= 85 ? '#16a34a' : sec.securityScore >= 65 ? '#d97706' : '#dc2626',
                }}
                dir="ltr"
                data-latin-digits="true"
              >
                {sec.securityScore} / 100
              </div>
              <div className="mon-bar">
                <div
                  className={`mon-bar-fill ${sec.securityScore < 65 ? 'is-crit' : sec.securityScore < 85 ? 'is-warn' : ''}`}
                  style={{ width: `${sec.securityScore}%` }}
                />
              </div>
            </div>
            <div className="mon-card">
              <span className="mon-card-label">تلاش‌های اسکن / نفوذ</span>
              <div className="mon-card-val" style={{ color: sec.probeAttemptCount > 0 ? '#dc2626' : '#16a34a' }}>
                {toPersianNumber(sec.probeAttemptCount)}
              </div>
              <span className="mon-card-sub">SQLi / XSS / Path Traversal / Bot Scan</span>
            </div>
            <div className="mon-card">
              <span className="mon-card-label">تلاش ناموفق ورود (401/403)</span>
              <div className="mon-card-val">{toPersianNumber(sec.authFailureCount)}</div>
              <span className="mon-card-sub">پایش Brute-Force و دسترسی غیرمجاز</span>
            </div>
            <div className="mon-card">
              <span className="mon-card-label">مسدودسازی Rate Limit (429)</span>
              <div className="mon-card-val">{toPersianNumber(sec.rateLimitedCount)}</div>
              <span className="mon-card-sub">درخواست‌های بیش از حد مجاز</span>
            </div>
          </div>

          <div className="panel">
            <div className="panel-head">
              <div>
                <h3>چک‌لیست امنیتی پیکربندی سرور</h3>
              </div>
            </div>
            <div className="mon-audit-list">
              <span className="mon-audit-item">
                {sec.audit.rateLimitingEnabled ? '✅' : '⚠️'} محافظت Rate Limiting فعال است
              </span>
              <span className="mon-audit-item">
                {sec.audit.securityHeadersEnabled ? '✅' : '⚠️'} هدرهای امنیتی (CSP / HSTS) فعال است
              </span>
              <span className="mon-audit-item">
                {sec.audit.httpsActive ? '✅ HTTPS فعال' : 'ℹ️ اتصال محلی / پشت پروکسی HTTP'}
              </span>
              <span className="mon-audit-item">
                👤 تعداد اکانت‌های ادمین فعال: <strong>{toPersianNumber(sec.audit.activeAdminUsersCount)}</strong>
              </span>
            </div>
          </div>

          <div className="mon-grid-2">
            <div className="panel">
              <div className="panel-head">
                <div>
                  <h3>IPهای مشکوک (Watchlist)</h3>
                  <p>مرتب‌شده بر اساس امتیاز ریسک امنیتی</p>
                </div>
              </div>
              {(sec.suspiciousIps ?? []).length === 0 ? (
                <div className="mon-empty">هیچ IP مشکوکی شناسایی نشده است ✓</div>
              ) : (
                <div className="mon-table-wrap">
                  <table className="mon-table">
                    <thead>
                      <tr>
                        <th>IP</th>
                        <th>اسکن/حمله</th>
                        <th>خطای ورود</th>
                        <th>429</th>
                        <th>امتیاز ریسک</th>
                      </tr>
                    </thead>
                    <tbody>
                      {sec.suspiciousIps.map((ip) => (
                        <tr key={ip.clientIp}>
                          <td dir="ltr" data-latin-digits="true"><strong>{ip.clientIp}</strong></td>
                          <td>{toPersianNumber(ip.probeAttempts)}</td>
                          <td>{toPersianNumber(ip.authFailures)}</td>
                          <td>{toPersianNumber(ip.rateLimits)}</td>
                          <td>
                            <span className={`mon-pill ${ip.totalThreatScore >= 20 ? 'is-err' : 'is-warn'}`}>
                              {toPersianNumber(ip.totalThreatScore)}
                            </span>
                          </td>
                        </tr>
                      ))}
                    </tbody>
                  </table>
                </div>
              )}
            </div>

            <div className="panel">
              <div className="panel-head">
                <div>
                  <h3>فید زنده رویدادهای امنیتی</h3>
                </div>
              </div>
              {(sec.recentEvents ?? []).length === 0 ? (
                <div className="mon-empty">هیچ رویداد امنیتی غیرعادی ثبت نشده است ✓</div>
              ) : (
                <div className="mon-table-wrap">
                  <table className="mon-table">
                    <thead>
                      <tr>
                        <th>شدت</th>
                        <th>نوع رخداد</th>
                        <th>IP</th>
                        <th>مسیر</th>
                      </tr>
                    </thead>
                    <tbody>
                      {sec.recentEvents.map((ev, idx) => (
                        <tr key={idx}>
                          <td>
                            <span className={`mon-pill ${ev.severity === 'High' ? 'is-err' : ev.severity === 'Medium' ? 'is-warn' : 'is-ok'}`}>
                              {ev.severity}
                            </span>
                          </td>
                          <td style={{ whiteSpace: 'normal', maxWidth: 220 }}>{ev.summary}</td>
                          <td dir="ltr" data-latin-digits="true">{ev.clientIp}</td>
                          <td><span className="mon-code-path" data-latin-digits="true">{ev.path}</span></td>
                        </tr>
                      ))}
                    </tbody>
                  </table>
                </div>
              )}
            </div>
          </div>
        </>
      ) : null}
    </section>
  )
}
