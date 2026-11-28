import { normalizeImageUrl } from '../utils/imageUrl.js'
import {
  formatCardNumberGroups,
  normalizeCardDigits,
  resolveBankPreset,
} from '../utils/iranianBanks.js'
import './BankCardVisual.css'

export { normalizeCardDigits, formatCardNumberGroups }

function BankLogoGlyph({ bankKey, accentColor }) {
  switch (bankKey) {
    case 'saman':
      return (
        <svg viewBox="0 0 32 32" width="22" height="22" aria-hidden="true">
          <circle cx="16" cy="16" r="11" fill="none" stroke="#ffffff" strokeWidth="2.4" />
          <path d="M9 17c2.2-3.5 5.2-3.5 7 0s4.8 3.5 7 0" fill="none" stroke={accentColor} strokeWidth="2.5" strokeLinecap="round" />
        </svg>
      )
    case 'blu':
      return (
        <svg viewBox="0 0 32 32" width="24" height="24" aria-hidden="true">
          <text x="16" y="21" textAnchor="middle" fill="#ffffff" fontSize="13" fontWeight="800" fontFamily="Space Grotesk, sans-serif">blu</text>
        </svg>
      )
    case 'mellat':
      return (
        <svg viewBox="0 0 32 32" width="22" height="22" aria-hidden="true">
          <polygon points="16,4 26,16 16,28 6,16" fill="none" stroke="#fbbf24" strokeWidth="2.4" />
          <polygon points="16,9 21,16 16,23 11,16" fill="#ffffff" />
        </svg>
      )
    case 'melli':
      return (
        <svg viewBox="0 0 32 32" width="22" height="22" aria-hidden="true">
          <path d="M6 23V12l5 4 5-8 5 8 5-4v11Z" fill="none" stroke="#eab308" strokeWidth="2.2" strokeLinejoin="round" />
        </svg>
      )
    case 'pasargad':
      return (
        <svg viewBox="0 0 32 32" width="22" height="22" aria-hidden="true">
          <circle cx="16" cy="16" r="6" fill="#f59e0b" />
          <path d="M16 4v4M16 24v4M4 16h4M24 16h4M7.5 7.5l2.8 2.8M21.7 21.7l2.8 2.8M24.5 7.5l-2.8 2.8M10.3 21.7l-2.8 2.8" stroke="#fbbf24" strokeWidth="2" strokeLinecap="round" />
        </svg>
      )
    case 'keshavarzi':
      return (
        <svg viewBox="0 0 32 32" width="22" height="22" aria-hidden="true">
          <path d="M16 26V8M16 14c-4-1-6-4-6-7 4 0 6 2 6 7ZM16 19c4-1 6-4 6-7-4 0-6 2-6 7Z" fill="none" stroke="#86efac" strokeWidth="2.2" strokeLinecap="round" strokeLinejoin="round" />
        </svg>
      )
    default:
      return (
        <svg viewBox="0 0 32 32" width="22" height="22" aria-hidden="true">
          <rect x="6" y="6" width="20" height="20" rx="6" fill="none" stroke="#ffffff" strokeWidth="2.2" />
          <path d="M11 16h10M16 11v10" stroke={accentColor || '#ffffff'} strokeWidth="2.2" strokeLinecap="round" />
        </svg>
      )
  }
}

function BankPatternSvg({ pattern }) {
  if (pattern === 'minimal') {
    return (
      <svg className="bc-pattern-svg" viewBox="0 0 400 250" preserveAspectRatio="none" aria-hidden="true">
        <circle cx="345" cy="48" r="95" fill="none" stroke="#ffffff" strokeWidth="1.2" opacity="0.5" />
        <line x1="0" y1="210" x2="400" y2="210" stroke="#ffffff" strokeWidth="0.8" opacity="0.35" />
      </svg>
    )
  }

  if (pattern === 'diamonds') {
    return (
      <svg className="bc-pattern-svg" viewBox="0 0 400 250" preserveAspectRatio="none" aria-hidden="true">
        <polygon points="320,-20 420,95 320,210 220,95" fill="none" stroke="#ffffff" strokeWidth="1.4" />
        <polygon points="340,20 410,95 340,170 270,95" fill="none" stroke="#fbbf24" strokeWidth="1.2" />
        <polygon points="60,150 130,220 60,290 -10,220" fill="none" stroke="#ffffff" strokeWidth="1.1" />
      </svg>
    )
  }

  if (pattern === 'arches') {
    return (
      <svg className="bc-pattern-svg" viewBox="0 0 400 250" preserveAspectRatio="none" aria-hidden="true">
        <path d="M210 250 C210 90, 390 90, 390 250" fill="none" stroke="#eab308" strokeWidth="1.5" />
        <path d="M240 250 C240 120, 360 120, 360 250" fill="none" stroke="#ffffff" strokeWidth="1.1" />
        <path d="M-20 120 C60 120, 120 40, 120 -20" fill="none" stroke="#ffffff" strokeWidth="1.1" />
      </svg>
    )
  }

  if (pattern === 'sunburst') {
    return (
      <svg className="bc-pattern-svg" viewBox="0 0 400 250" preserveAspectRatio="none" aria-hidden="true">
        <circle cx="340" cy="40" r="120" fill="none" stroke="#f59e0b" strokeWidth="1.4" />
        <circle cx="340" cy="40" r="80" fill="none" stroke="#f59e0b" strokeWidth="1.1" />
        <circle cx="340" cy="40" r="42" fill="none" stroke="#ffffff" strokeWidth="1" />
      </svg>
    )
  }

  if (pattern === 'diagonal') {
    return (
      <svg className="bc-pattern-svg" viewBox="0 0 400 250" preserveAspectRatio="none" aria-hidden="true">
        <line x1="220" y1="0" x2="400" y2="180" stroke="#ffffff" strokeWidth="1.3" />
        <line x1="260" y1="0" x2="400" y2="140" stroke="#ffffff" strokeWidth="1.1" />
        <line x1="180" y1="0" x2="400" y2="220" stroke="#ffffff" strokeWidth="0.9" />
      </svg>
    )
  }

  // Default: waves (Saman, Keshavarzi, etc.)
  return (
    <svg className="bc-pattern-svg" viewBox="0 0 400 250" preserveAspectRatio="none" aria-hidden="true">
      <path d="M0 165 Q 110 105, 220 165 T 400 145" fill="none" stroke="#ffffff" strokeWidth="1.6" />
      <path d="M0 195 Q 110 135, 220 195 T 400 175" fill="none" stroke="#ffffff" strokeWidth="1.1" />
      <circle cx="340" cy="55" r="78" fill="none" stroke="#ffffff" strokeWidth="1.1" />
    </svg>
  )
}

export function BankCardVisual({
  card,
  size = 'normal', // 'normal' | 'large' | 'compact'
  showSheba = true,
}) {
  const preset = resolveBankPreset(card)
  const photoUrl = card?.cardImageUrl ? normalizeImageUrl(card.cardImageUrl) : ''
  const sizeClass =
    size === 'large'
      ? 'bc-card--large'
      : size === 'compact'
        ? 'bc-card--compact'
        : ''

  return (
    <div className={`bc-card ${preset.themeClass} ${sizeClass}`.trim()}>
      {photoUrl ? (
        <>
          <img className="bc-photo-bg" src={photoUrl} alt={card?.title || preset.name} />
          <div className="bc-photo-scrim" aria-hidden="true" />
        </>
      ) : (
        <>
          <BankPatternSvg pattern={preset.pattern} />
          <div className="bc-sheen" aria-hidden="true" />
        </>
      )}

      <div className="bc-top">
        <div className="bc-bank-identity">
          {size !== 'compact' ? (
            <span className="bc-bank-logo">
              <BankLogoGlyph bankKey={preset.key} accentColor={preset.accentColor} />
            </span>
          ) : null}
          <div className="bc-bank-wrap">
            <span className="bc-bank-name">
              {card?.bankName?.trim() || (preset.key !== 'custom' ? preset.name : 'نام بانک')}
            </span>
            <span className="bc-bank-en" dir="ltr" data-latin-digits="true">
              {preset.englishName}
            </span>
          </div>
        </div>
        <div className="bc-badges">
          {card?.isDefault ? <span className="bc-pill">پیش‌فرض</span> : null}
        </div>
      </div>

      {size !== 'compact' ? (
        <div className="bc-chip-row">
          <div className="bc-chip-group">
            <span className={`bc-chip bc-chip--${preset.chipTone || 'gold'}`} aria-hidden="true">
              <svg viewBox="0 0 44 32">
                <path
                  d="M0 11h14M30 11h14M0 21h14M30 21h14M14 0v32M30 0v32M14 16h16"
                  fill="none"
                  stroke="rgba(120,53,15,0.42)"
                  strokeWidth="1.2"
                />
              </svg>
            </span>
            <span className="bc-contactless" aria-hidden="true">
              <svg viewBox="0 0 24 24" width="18" height="18" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round">
                <path d="M8.5 16.5a5 5 0 0 1 0-9" />
                <path d="M12 19a9 9 0 0 1 0-14" />
                <path d="M15.5 21a13 13 0 0 1 0-18" />
              </svg>
            </span>
          </div>
          <span className="bc-shetab-tag" data-latin-digits="true">SHETAB</span>
        </div>
      ) : null}

      <div className="bc-number" dir="ltr" data-latin-digits="true">
        {formatCardNumberGroups(card?.cardNumber)}
      </div>

      <div className="bc-bottom">
        <div>
          {size !== 'compact' ? <span className="bc-holder-label">دارنده کارت</span> : null}
          <span className="bc-holder-name">{card?.cardHolderName?.trim() || 'نام صاحب کارت'}</span>
        </div>
        {showSheba && size !== 'compact' && card?.shebaNumber?.trim() ? (
          <span className="bc-sheba" dir="ltr" data-latin-digits="true">
            {card.shebaNumber.trim()}
          </span>
        ) : null}
      </div>
    </div>
  )
}
