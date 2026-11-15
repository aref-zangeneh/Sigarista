import { useEffect, useState } from 'react'
import { apiRequest, getReadableError } from '../utils/adminApi'
import { useAdmin } from '../context/AdminContext'
import {
  SMS_KIND_LABELS,
  formatToman,
  toPersian,
} from '../utils/smsHelpers'
import './sms.css'

const RANGE_OPTIONS = [
  { value: 'today', label: 'امروز' },
  { value: 'yesterday', label: 'دیروز' },
  { value: '7d', label: '۷ روز اخیر' },
  { value: '30d', label: '۳۰ روز اخیر' },
  { value: 'custom', label: 'بازهٔ دلخواه' },
]

export function SmsDashboardPage() {
  const { session } = useAdmin()
  const [data, setData] = useState(null)
  const [providers, setProviders] = useState([])
  const [range, setRange] = useState('7d')
  const [providerId, setProviderId] = useState('')
  const [kind, setKind] = useState('')
  const [customFrom, setCustomFrom] = useState('')
  const [customTo, setCustomTo] = useState('')
  const [error, setError] = useState('')
  const [loading, setLoading] = useState(false)

  useEffect(() => {
    apiRequest('/admin/sms/providers', { token: session.token })
      .then(setProviders)
      .catch(() => {})
  }, [session.token])

  const load = async () => {
    setLoading(true)
    setError('')
    try {
      const params = new URLSearchParams({ range })
      if (providerId) params.set('providerId', providerId)
      if (kind) params.set('kind', kind)
      if (range === 'custom') {
        if (customFrom) params.set('from', new Date(customFrom).toISOString())
        if (customTo) params.set('to', new Date(customTo).toISOString())
      }
      const dashboard = await apiRequest(`/admin/sms/dashboard?${params}`, { token: session.token })
      setData(dashboard)
    } catch (requestError) {
      setError(getReadableError(requestError))
    } finally {
      setLoading(false)
    }
  }

  useEffect(() => {
    if (session.token) load()
  }, [session.token, range, providerId, kind, customFrom, customTo])

  const stats = data?.stats
  const maxTimeline = Math.max(1, ...(data?.timeline ?? []).map((point) => point.total))

  return (
    <div className="sms-page" dir="rtl">
      <header className="sms-page-header">
        <h1>داشبورد پیامک</h1>
        <p>نمای کلی وضعیت ارسال‌ها بر اساس داده‌های ثبت‌شدهٔ سیستم — بدون درخواست به سرویس‌دهندگان.</p>
      </header>

      {error ? <div className="sms-error">{error}</div> : null}

      <div className="sms-card">
        <div className="sms-row">
          <label>
            بازهٔ زمانی
            <select value={range} onChange={(event) => setRange(event.target.value)}>
              {RANGE_OPTIONS.map((option) => (
                <option key={option.value} value={option.value}>{option.label}</option>
              ))}
            </select>
          </label>
          <label>
            سرویس‌دهنده
            <select value={providerId} onChange={(event) => setProviderId(event.target.value)}>
              <option value="">همه</option>
              {providers.map((provider) => (
                <option key={provider.id} value={provider.id}>{provider.name}</option>
              ))}
            </select>
          </label>
          <label>
            نوع پیامک
            <select value={kind} onChange={(event) => setKind(event.target.value)}>
              <option value="">همه</option>
              {Object.entries(SMS_KIND_LABELS).map(([value, label]) => (
                <option key={value} value={value}>{label}</option>
              ))}
            </select>
          </label>
          {range === 'custom' ? (
            <>
              <label>
                از تاریخ
                <input type="datetime-local" value={customFrom} onChange={(event) => setCustomFrom(event.target.value)} />
              </label>
              <label>
                تا تاریخ
                <input type="datetime-local" value={customTo} onChange={(event) => setCustomTo(event.target.value)} />
              </label>
            </>
          ) : null}
        </div>
      </div>

      <section className="sms-stats">
        {[
          { label: 'کل ارسال‌ها', value: stats?.totalMessages },
          { label: 'موفق', value: stats?.sentCount },
          { label: 'ناموفق', value: stats?.failedCount },
          { label: 'در انتظار', value: stats?.pendingCount },
          { label: 'تحویل‌شده', value: stats?.deliveredCount },
          { label: 'تحویل‌نشده', value: stats?.undeliveredCount },
          { label: 'مجموع هزینه', value: stats?.totalCost, money: true },
          { label: 'مخاطبین', value: stats?.contactCount },
          { label: 'گروه‌ها', value: stats?.groupCount },
        ].map((card) => (
          <div key={card.label} className="sms-stat">
            <div className="sms-stat-value">
              {card.money ? formatToman(card.value) : toPersian(card.value ?? '—')}
            </div>
            <div className="sms-stat-label">{card.label}</div>
          </div>
        ))}
        <div className="sms-stat">
          <div className="sms-stat-value">{stats?.activeProviderName ?? '—'}</div>
          <div className="sms-stat-label">سرویس‌دهندهٔ فعال (اعتبار از صفحهٔ Providers)</div>
        </div>
      </section>

      <div className="sms-card">
        <h2>روند ارسال‌ها</h2>
        {loading ? <p>در حال بارگذاری…</p> : null}
        {data?.timeline?.length ? (
          <div className="sms-timeline">
            {data.timeline.map((point) => (
              <div key={point.dateLabel} className="sms-timeline-bar" title={`${point.dateLabel}: ${point.total} پیامک`}>
                <div className="sms-timeline-stacks">
                  <span
                    className="sms-bar-ok"
                    style={{ height: `${(point.sent / maxTimeline) * 100}%` }}
                  />
                  <span
                    className="sms-bar-error"
                    style={{ height: `${(point.failed / maxTimeline) * 100}%` }}
                  />
                </div>
                <span className="sms-timeline-label">{point.dateLabel.slice(5)}</span>
              </div>
            ))}
          </div>
        ) : (
          <p>داده‌ای در این بازه وجود ندارد.</p>
        )}
        <div className="sms-legend" style={{ marginTop: 10 }}>
          <span><i style={{ background: 'var(--ok, #22c55e)' }} /> موفق</span>
          <span><i style={{ background: 'var(--bad, #ef4444)' }} /> ناموفق</span>
        </div>
      </div>

      <div className="sms-card">
        <h2>عملکرد سرویس‌دهندگان</h2>
        {data?.providers?.length ? (
          <table className="sms-table">
            <thead>
              <tr>
                <th>سرویس‌دهنده</th>
                <th>کل</th>
                <th>موفق</th>
                <th>ناموفق</th>
                <th>هزینه</th>
              </tr>
            </thead>
            <tbody>
              {data.providers.map((provider) => (
                <tr key={provider.providerId ?? provider.providerName}>
                  <td>{provider.providerName}</td>
                  <td>{toPersian(provider.total)}</td>
                  <td>{toPersian(provider.sent)}</td>
                  <td>{toPersian(provider.failed)}</td>
                  <td>{formatToman(provider.cost)}</td>
                </tr>
              ))}
            </tbody>
          </table>
        ) : (
          <p>داده‌ای موجود نیست.</p>
        )}
      </div>

      <div className="sms-card">
        <h2>توزیع بر اساس نوع ارسال</h2>
        {data?.kinds?.length ? (
          <ul className="sms-mini-list">
            {data.kinds.map((entry) => (
              <li key={entry.kind}>
                {SMS_KIND_LABELS[entry.kind] ?? entry.kind}: {toPersian(entry.count)}
              </li>
            ))}
          </ul>
        ) : (
          <p>داده‌ای موجود نیست.</p>
        )}
      </div>
    </div>
  )
}
