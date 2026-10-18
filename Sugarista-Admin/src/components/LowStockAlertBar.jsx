import { useState } from 'react'
import { toPersianNumber } from '../utils/adminFormatters'
import './LowStockAlertBar.css'

export function LowStockAlertBar({ alert, onOpenInventory }) {
  const [dismissedCount, setDismissedCount] = useState(null)

  // A fresh alert (changed count) revives the bar even if it was dismissed before,
  // because the dismissal is compared against the alert's current count.

  if (!alert || alert.count === 0 || alert.count === dismissedCount) {
    return null
  }

  const previewItems = (alert.items ?? []).slice(0, 4)

  return (
    <div className="low-stock-alert-bar" role="alert">
      <div className="low-stock-alert-main">
        <span className="low-stock-alert-icon" aria-hidden="true">⚠️</span>
        <div className="low-stock-alert-text">
          <strong>
            هشدار انبار: {toPersianNumber(alert.count)} قلم {alert.count === 1 ? 'کم شده' : 'کم شده‌اند'}
          </strong>
          <p>
            {previewItems
              .map((item) => `${item.persianName} (${toPersianNumber(item.currentQuantity)} ${item.unit})`)
              .join('، ')}
            {alert.count > previewItems.length ? ` و ${toPersianNumber(alert.count - previewItems.length)} قلم دیگر...` : ''}
          </p>
        </div>
      </div>
      <div className="low-stock-alert-actions">
        <button type="button" className="low-stock-alert-open" onClick={onOpenInventory}>
          رفتن به انبار
        </button>
        <button
          type="button"
          className="low-stock-alert-dismiss"
          aria-label="بستن هشدار انبار"
          onClick={() => setDismissedCount(alert.count)}
        >
          ✕
        </button>
      </div>
    </div>
  )
}
