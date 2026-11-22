import { useEffect, useMemo, useState } from 'react'
import { formatMoney } from './DataTable'

const SOURCE_LABELS = {
  Initial: 'ثبت اولیه',
  ManualEdit: 'ویرایش دستی',
  AutoPricing: 'قیمت‌گذاری خودکار',
  Duplicate: 'کپی از آیتم دیگر',
}

function formatSignedDelta(delta, pct, toPersianNumber) {
  if (delta == null || delta === 0) return null
  const sign = delta > 0 ? '+' : '−'
  const absMoney = formatMoney(Math.abs(delta))
  const pctStr =
    pct != null && pct !== 0
      ? ` (${pct > 0 ? '+' : '−'}${toPersianNumber(Math.abs(pct))}٪)`
      : ''
  return `${sign}${absMoney}${pctStr}`
}

function DualPriceTrendChart({ entries, toPersianNumber, formatDateTime }) {
  const points = useMemo(() => {
    const chrono = [...(entries ?? [])].reverse()
    const list = []
    chrono.forEach((entry, idx) => {
      if (idx === 0 && (entry.previousPurchasePrice != null || entry.previousSalePrice != null)) {
        list.push({
          label: 'قبل از تغییر',
          purchase: entry.previousPurchasePrice,
          sale: entry.previousSalePrice,
        })
      }
      list.push({
        label: formatDateTime ? formatDateTime(entry.recordedAt) : '',
        purchase: entry.newPurchasePrice,
        sale: entry.newSalePrice,
      })
    })
    return list
  }, [entries, formatDateTime])

  if (points.length < 2) {
    return (
      <div className="price-chart-empty">
        با ثبت تغییر قیمت بعدی (مثلاً تغییر قیمت خرید از ۱۰۰٬۰۰۰ به ۱۲۰٬۰۰۰ تومان)، نمودار روند تغییرات اینجا ترسیم می‌شود.
      </div>
    )
  }

  const allValues = points
    .flatMap((p) => [p.purchase, p.sale])
    .filter((v) => v != null && Number.isFinite(Number(v)))
    .map(Number)

  if (allValues.length === 0) return null

  const minVal = Math.min(...allValues)
  const maxVal = Math.max(...allValues)
  const padVal = maxVal === minVal ? Math.max(10000, maxVal * 0.15) : (maxVal - minVal) * 0.14
  const yMin = Math.max(0, minVal - padVal)
  const yMax = maxVal + padVal
  const yRange = Math.max(1, yMax - yMin)

  const W = 620
  const H = 200
  const padL = 18
  const padR = 76
  const padT = 18
  const padB = 34
  const plotW = W - padL - padR
  const plotH = H - padT - padB

  const xFor = (i) => padL + (points.length === 1 ? plotW / 2 : (i / (points.length - 1)) * plotW)
  const yFor = (val) => padT + plotH - ((val - yMin) / yRange) * plotH

  const buildPath = (key) => {
    const coords = points
      .map((p, i) => (p[key] != null ? { x: xFor(i), y: yFor(Number(p[key])), val: Number(p[key]), label: p.label } : null))
      .filter(Boolean)
    if (coords.length === 0) return { d: '', coords: [] }
    const d = coords.map((c, i) => `${i === 0 ? 'M' : 'L'} ${c.x.toFixed(1)} ${c.y.toFixed(1)}`).join(' ')
    return { d, coords }
  }

  const purchaseSeries = buildPath('purchase')
  const saleSeries = buildPath('sale')
  const yTicks = [yMin, yMin + yRange / 2, yMax]

  return (
    <div className="price-trend-chart-wrap">
      <div className="price-trend-legend">
        <span className="price-legend-item">
          <i className="price-legend-dot is-purchase" />
          روند قیمت خرید
        </span>
        <span className="price-legend-item">
          <i className="price-legend-dot is-sale" />
          روند قیمت فروش
        </span>
      </div>
      <svg
        className="price-trend-svg"
        viewBox={`0 0 ${W} ${H}`}
        role="img"
        aria-label="نمودار روند تغییر قیمت خرید و فروش"
      >
        {yTicks.map((tick, idx) => {
          const y = yFor(tick)
          return (
            <g key={idx}>
              <line
                x1={padL}
                x2={W - padR}
                y1={y}
                y2={y}
                stroke="var(--border)"
                strokeDasharray="3 3"
                strokeWidth="1"
              />
              <text
                x={W - padR + 8}
                y={y + 4}
                fontSize="10"
                fill="var(--muted)"
                textAnchor="start"
              >
                {toPersianNumber(Math.round(tick).toLocaleString('en-US'))}
              </text>
            </g>
          )
        })}

        {purchaseSeries.d ? (
          <path
            d={purchaseSeries.d}
            fill="none"
            stroke="#d97706"
            strokeWidth="2.6"
            strokeLinecap="round"
            strokeLinejoin="round"
          />
        ) : null}

        {saleSeries.d ? (
          <path
            d={saleSeries.d}
            fill="none"
            stroke="var(--accent, #aa3171)"
            strokeWidth="2.6"
            strokeLinecap="round"
            strokeLinejoin="round"
          />
        ) : null}

        {purchaseSeries.coords.map((c, idx) => (
          <g key={`p-${idx}`}>
            <circle cx={c.x} cy={c.y} r="4.5" fill="#d97706" stroke="var(--panel)" strokeWidth="2">
              <title>{`خرید: ${formatMoney(c.val)} (${c.label})`}</title>
            </circle>
          </g>
        ))}

        {saleSeries.coords.map((c, idx) => (
          <g key={`s-${idx}`}>
            <circle cx={c.x} cy={c.y} r="4.5" fill="var(--accent, #aa3171)" stroke="var(--panel)" strokeWidth="2">
              <title>{`فروش: ${formatMoney(c.val)} (${c.label})`}</title>
            </circle>
          </g>
        ))}

        {points.map((p, idx) => {
          const shouldShow =
            points.length <= 5 || idx === 0 || idx === points.length - 1 || idx === Math.floor(points.length / 2)
          if (!shouldShow) return null
          const shortDate = (p.label || '').split(' ')[0] || `#${toPersianNumber(idx + 1)}`
          return (
            <text
              key={`x-${idx}`}
              x={xFor(idx)}
              y={H - 10}
              fontSize="10"
              fill="var(--muted)"
              textAnchor="middle"
            >
              {shortDate}
            </text>
          )
        })}
      </svg>
    </div>
  )
}

export function MenuItemPriceHistoryModal({
  menuItem,
  onClose,
  session,
  apiRequest,
  getReadableError,
  toPersianNumber,
  formatDateTime,
}) {
  const [data, setData] = useState(null)
  const [loading, setLoading] = useState(false)
  const [error, setError] = useState('')

  useEffect(() => {
    if (!menuItem?.id || !session?.token) return
    let active = true
    setLoading(true)
    setError('')
    apiRequest(`/admin/menu-items/${menuItem.id}/price-history`, { token: session.token })
      .then((res) => {
        if (active) setData(res)
      })
      .catch((err) => {
        if (active) setError(getReadableError(err))
      })
      .finally(() => {
        if (active) setLoading(false)
      })
    return () => {
      active = false
    }
  }, [menuItem?.id, session?.token, apiRequest, getReadableError])

  if (!menuItem) return null

  const summary = data?.summary
  const entries = data?.entries ?? []

  return (
    <div
      className="option-picker-backdrop"
      role="dialog"
      aria-modal="true"
      aria-label="تاریخچه و روند تغییرات قیمت"
      onClick={(event) => {
        if (event.target === event.currentTarget) onClose()
      }}
    >
      <div className="option-picker-modal price-history-modal">
        <div className="option-picker-head">
          <div>
            <h4>تاریخچه و روند قیمت: {data?.persianName || menuItem.persianName}</h4>
            {data?.englishName || menuItem.englishName ? (
              <div className="table-subtext ltr-text">{data?.englishName || menuItem.englishName}</div>
            ) : null}
          </div>
          <button type="button" className="ghost-button tiny" onClick={onClose}>
            بستن
          </button>
        </div>

        <div className="option-picker-body">
          {loading ? (
            <p className="feedback">در حال بارگذاری تاریخچه قیمت‌ها...</p>
          ) : error ? (
            <p className="feedback error-feedback">{error}</p>
          ) : data ? (
            <>
              <div className="price-kpi-grid">
                <div className="stat-card">
                  <span className="stat-label">قیمت خرید فعلی</span>
                  <strong className="stat-value">
                    {data.currentPurchasePrice != null ? formatMoney(data.currentPurchasePrice) : 'ثبت نشده'}
                  </strong>
                  {summary?.totalPurchaseChange != null && summary.totalPurchaseChange !== 0 ? (
                    <span
                      className={`price-delta-badge ${summary.totalPurchaseChange > 0 ? 'is-cost-up' : 'is-cost-down'}`}
                    >
                      روند کل: {formatSignedDelta(summary.totalPurchaseChange, summary.totalPurchaseChangePercent, toPersianNumber)}
                    </span>
                  ) : null}
                </div>

                <div className="stat-card">
                  <span className="stat-label">قیمت فروش فعلی</span>
                  <strong className="stat-value">{formatMoney(data.currentSalePrice)}</strong>
                  {summary?.totalSaleChange != null && summary.totalSaleChange !== 0 ? (
                    <span
                      className={`price-delta-badge ${summary.totalSaleChange > 0 ? 'is-sale-up' : 'is-sale-down'}`}
                    >
                      روند کل: {formatSignedDelta(summary.totalSaleChange, summary.totalSaleChangePercent, toPersianNumber)}
                    </span>
                  ) : null}
                </div>

                <div className={`stat-card ${data.currentProfit != null && data.currentProfit <= 0 ? 'stat-card--danger' : 'stat-card--ok'}`}>
                  <span className="stat-label">سود خالص هر عدد</span>
                  <strong className="stat-value">
                    {data.currentProfit != null ? formatMoney(data.currentProfit) : '—'}
                  </strong>
                  {data.currentMarginPercent != null ? (
                    <span className="table-subtext">
                      حاشیه فروش: {toPersianNumber(data.currentMarginPercent)}٪
                      {data.currentMarkupPercent != null
                        ? ` | روی خرید: ${toPersianNumber(data.currentMarkupPercent)}٪`
                        : ''}
                    </span>
                  ) : null}
                </div>

                <div className="stat-card">
                  <span className="stat-label">کف / سقف / میانگین خرید</span>
                  <strong className="stat-value" style={{ fontSize: '0.92rem' }}>
                    {summary?.minPurchasePrice != null
                      ? `${formatMoney(summary.minPurchasePrice)} تا ${formatMoney(summary.maxPurchasePrice)}`
                      : '—'}
                  </strong>
                  {summary?.averagePurchasePrice != null ? (
                    <span className="table-subtext">
                      میانگین: {formatMoney(summary.averagePurchasePrice)} ({toPersianNumber(summary.entryCount)} ثبت)
                    </span>
                  ) : null}
                </div>
              </div>

              <DualPriceTrendChart
                entries={entries}
                toPersianNumber={toPersianNumber}
                formatDateTime={formatDateTime}
              />

              <div className="price-log-section">
                <h5>لاگ کامل تغییرات قیمت ({toPersianNumber(entries.length)})</h5>
                {entries.length === 0 ? (
                  <p className="feedback">
                    هنوز تغییر قیمتی برای این آیتم ثبت نشده است. با اولین ویرایش قیمت خرید یا فروش، تاریخچه به‌طور خودکار اینجا ثبت می‌شود.
                  </p>
                ) : (
                  <div className="data-table-scroll">
                    <table className="admin-table price-log-table">
                      <thead>
                        <tr>
                          <th>تاریخ و ساعت</th>
                          <th>منبع</th>
                          <th>قیمت خرید</th>
                          <th>قیمت فروش</th>
                          <th>سود هر عدد</th>
                          <th>یادداشت / کاربر</th>
                        </tr>
                      </thead>
                      <tbody>
                        {entries.map((entry) => {
                          const pDelta = formatSignedDelta(
                            entry.purchasePriceDelta,
                            entry.purchasePriceChangePercent,
                            toPersianNumber,
                          )
                          const sDelta = formatSignedDelta(
                            entry.salePriceDelta,
                            entry.salePriceChangePercent,
                            toPersianNumber,
                          )
                          return (
                            <tr key={entry.id}>
                              <td style={{ whiteSpace: 'nowrap' }}>{formatDateTime(entry.recordedAt)}</td>
                              <td>
                                <span className="option-template-chip">
                                  {SOURCE_LABELS[entry.source] ?? entry.source}
                                </span>
                              </td>
                              <td>
                                <div>
                                  {entry.previousPurchasePrice != null && entry.previousPurchasePrice !== entry.newPurchasePrice ? (
                                    <span className="table-subtext">
                                      {formatMoney(entry.previousPurchasePrice)} ←{' '}
                                    </span>
                                  ) : null}
                                  <strong>
                                    {entry.newPurchasePrice != null ? formatMoney(entry.newPurchasePrice) : '—'}
                                  </strong>
                                </div>
                                {pDelta ? (
                                  <span
                                    className={`price-delta-badge ${entry.purchasePriceDelta > 0 ? 'is-cost-up' : 'is-cost-down'}`}
                                  >
                                    {pDelta}
                                  </span>
                                ) : null}
                              </td>
                              <td>
                                <div>
                                  {entry.previousSalePrice != null && entry.previousSalePrice !== entry.newSalePrice ? (
                                    <span className="table-subtext">
                                      {formatMoney(entry.previousSalePrice)} ←{' '}
                                    </span>
                                  ) : null}
                                  <strong>
                                    {entry.newSalePrice != null ? formatMoney(entry.newSalePrice) : '—'}
                                  </strong>
                                </div>
                                {sDelta ? (
                                  <span
                                    className={`price-delta-badge ${entry.salePriceDelta > 0 ? 'is-sale-up' : 'is-sale-down'}`}
                                  >
                                    {sDelta}
                                  </span>
                                ) : null}
                              </td>
                              <td>
                                {entry.profitAtChange != null ? (
                                  <>
                                    <strong>{formatMoney(entry.profitAtChange)}</strong>
                                    {entry.marginPercentAtChange != null ? (
                                      <div className="table-subtext">
                                        {toPersianNumber(entry.marginPercentAtChange)}٪ حاشیه
                                      </div>
                                    ) : null}
                                  </>
                                ) : (
                                  '—'
                                )}
                              </td>
                              <td>
                                {entry.note ? <div>{entry.note}</div> : null}
                                {entry.changedBy ? (
                                  <div className="table-subtext">{entry.changedBy}</div>
                                ) : !entry.note ? (
                                  '—'
                                ) : null}
                              </td>
                            </tr>
                          )
                        })}
                      </tbody>
                    </table>
                  </div>
                )}
              </div>
            </>
          ) : null}
        </div>

        <div className="option-picker-foot">
          <span className="table-subtext">
            هر تغییر در قیمت خرید یا فروش به‌صورت خودکار و غیرقابل دستکاری در این تاریخچه ثبت می‌شود.
          </span>
          <button type="button" className="ghost-button" onClick={onClose}>
            بستن
          </button>
        </div>
      </div>
    </div>
  )
}
