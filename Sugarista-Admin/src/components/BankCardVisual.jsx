import { normalizeImageUrl } from '../utils/imageUrl.js'
import './BankCardVisual.css'

export const BANK_CARD_THEMES = [
  { key: 'ocean', label: 'اقیانوسی' },
  { key: 'plum', label: 'شوگاریستا' },
  { key: 'gold', label: 'طلایی' },
  { key: 'emerald', label: 'زمردی' },
  { key: 'obsidian', label: 'مشکی لوکس' },
  { key: 'rose', label: 'یاقوتی' },
]

export function normalizeCardDigits(raw) {
  const persian = '۰۱۲۳۴۵۶۷۸۹'
  const arabic = '٠١٢٣٤٥٦٧٨٩'
  return String(raw ?? '')
    .replace(/[۰-۹]/g, (c) => String(persian.indexOf(c)))
    .replace(/[٠-٩]/g, (c) => String(arabic.indexOf(c)))
    .replace(/\D/g, '')
    .slice(0, 16)
}

export function formatCardNumberGroups(raw) {
  const digits = normalizeCardDigits(raw)
  if (!digits) return '••••  ••••  ••••  ••••'
  const padded = digits.padEnd(16, '•')
  return `${padded.slice(0, 4)}  ${padded.slice(4, 8)}  ${padded.slice(8, 12)}  ${padded.slice(12, 16)}`
}

export function BankCardVisual({
  card,
  size = 'normal', // 'normal' | 'large' | 'compact'
  showSheba = true,
}) {
  const theme = card?.cardTheme || 'ocean'
  const photoUrl = card?.cardImageUrl ? normalizeImageUrl(card.cardImageUrl) : ''
  const sizeClass =
    size === 'large'
      ? 'bc-card--large'
      : size === 'compact'
        ? 'bc-card--compact'
        : ''

  return (
    <div className={`bc-card bc-theme-${theme} ${sizeClass}`.trim()}>
      {photoUrl ? (
        <>
          <img className="bc-photo-bg" src={photoUrl} alt={card?.title || 'کارت بانکی'} />
          <div className="bc-photo-scrim" aria-hidden="true" />
        </>
      ) : (
        <div className="bc-orb" aria-hidden="true" />
      )}

      <div className="bc-top">
        <div className="bc-bank-wrap">
          <span className="bc-bank-name">{card?.bankName?.trim() || 'نام بانک'}</span>
          {card?.title?.trim() ? <span className="bc-card-title">{card.title.trim()}</span> : null}
        </div>
        <div className="bc-badges">
          {card?.isDefault ? <span className="bc-pill">پیش‌فرض</span> : null}
          {size !== 'compact' ? <span className="bc-chip" aria-hidden="true" /> : null}
        </div>
      </div>

      <div className="bc-number" dir="ltr">
        {formatCardNumberGroups(card?.cardNumber)}
      </div>

      <div className="bc-bottom">
        <div>
          {size !== 'compact' ? <span className="bc-holder-label">دارنده کارت</span> : null}
          <span className="bc-holder-name">{card?.cardHolderName?.trim() || 'نام صاحب کارت'}</span>
        </div>
        {showSheba && size !== 'compact' && card?.shebaNumber?.trim() ? (
          <span className="bc-sheba">{card.shebaNumber.trim()}</span>
        ) : null}
      </div>
    </div>
  )
}
