import { useCallback, useEffect, useState } from 'react'
import { ImageUploader } from './ImageUploader'
import { BankCardVisual } from './BankCardVisual.jsx'
import {
  IRANIAN_BANKS,
  detectBankByCardNumber,
  formatCardInputDisplay,
  normalizeCardDigits,
} from '../utils/iranianBanks.js'
import './AdminBankCardsPanel.css'

const EMPTY_FORM = {
  id: '',
  title: '',
  bankKey: 'saman',
  bankName: 'بانک سامان',
  cardHolderName: '',
  cardNumber: '',
  shebaNumber: '',
  cardImageUrl: '',
  cardTheme: 'saman',
  isDefault: false,
  isActive: true,
  displayOrder: 0,
}

function findBankKeyFromCard(card) {
  const byTheme = IRANIAN_BANKS.find((b) => b.key === (card?.cardTheme || '').toLowerCase())
  if (byTheme) return byTheme.key
  const byName = IRANIAN_BANKS.find(
    (b) => b.key !== 'custom' && card?.bankName && card.bankName.includes(b.name.replace('بانک ', '')),
  )
  if (byName) return byName.key
  const byBin = detectBankByCardNumber(card?.cardNumber)
  return byBin ? byBin.key : 'custom'
}

export function AdminBankCardsPanel({ session, apiRequest, getReadableError }) {
  const [cards, setCards] = useState([])
  const [loading, setLoading] = useState(true)
  const [saving, setSaving] = useState(false)
  const [form, setForm] = useState(EMPTY_FORM)
  const [feedback, setFeedback] = useState('')
  const [error, setError] = useState('')

  const loadCards = useCallback(async () => {
    if (!session?.token) return
    setLoading(true)
    try {
      const data = await apiRequest('/admin/bank-cards', { token: session.token })
      setCards(Array.isArray(data) ? data : [])
    } catch (err) {
      setError(getReadableError(err))
    } finally {
      setLoading(false)
    }
  }, [session?.token, apiRequest, getReadableError])

  useEffect(() => {
    loadCards()
  }, [loadCards])

  const handleBankSelect = (nextKey) => {
    const preset = IRANIAN_BANKS.find((b) => b.key === nextKey) || IRANIAN_BANKS[0]
    setForm((current) => ({
      ...current,
      bankKey: preset.key,
      cardTheme: preset.key,
      bankName: preset.key === 'custom' ? (current.bankKey === 'custom' ? current.bankName : '') : preset.name,
    }))
  }

  const handleCardNumberChange = (rawValue) => {
    const digits = normalizeCardDigits(rawValue)
    const detected = detectBankByCardNumber(digits)

    setForm((current) => {
      // Auto-detect bank from first 6 digits unless user explicitly chose BluBank (same BIN as Saman)
      if (detected && current.bankKey !== 'blu' && current.bankKey !== detected.key) {
        return {
          ...current,
          cardNumber: digits,
          bankKey: detected.key,
          cardTheme: detected.key,
          bankName: detected.name,
        }
      }
      return {
        ...current,
        cardNumber: digits,
      }
    })
  }

  const handleEdit = (card) => {
    const bankKey = findBankKeyFromCard(card)
    setForm({
      id: card.id,
      title: card.title ?? '',
      bankKey,
      bankName: card.bankName ?? '',
      cardHolderName: card.cardHolderName ?? '',
      cardNumber: normalizeCardDigits(card.cardNumber ?? ''),
      shebaNumber: card.shebaNumber ?? '',
      cardImageUrl: card.cardImageUrl ?? '',
      cardTheme: card.cardTheme || bankKey,
      isDefault: Boolean(card.isDefault),
      isActive: card.isActive !== false,
      displayOrder: card.displayOrder ?? 0,
    })
    setFeedback('')
    setError('')
  }

  const resetForm = () => {
    setForm(EMPTY_FORM)
    setError('')
  }

  const handleSubmit = async (event) => {
    event.preventDefault()
    const digits = normalizeCardDigits(form.cardNumber)
    if (digits.length !== 16) {
      setError('شماره کارت باید دقیقاً ۱۶ رقم باشد.')
      return
    }
    const resolvedBankName = form.bankName.trim()
    if (!resolvedBankName || !form.cardHolderName.trim()) {
      setError('انتخاب یا وارد کردن نام بانک و نام صاحب کارت الزامی است.')
      return
    }

    setSaving(true)
    setError('')
    setFeedback('')
    try {
      const payload = {
        title: form.title.trim() || `${resolvedBankName} — ${form.cardHolderName.trim()}`,
        bankName: resolvedBankName,
        cardHolderName: form.cardHolderName.trim(),
        cardNumber: digits,
        shebaNumber: form.shebaNumber.trim() || null,
        cardImageUrl: form.cardImageUrl.trim() || null,
        cardTheme: form.bankKey || form.cardTheme || 'saman',
        isDefault: Boolean(form.isDefault),
        isActive: Boolean(form.isActive),
        displayOrder: Number(form.displayOrder) || 0,
      }

      if (form.id) {
        await apiRequest(`/admin/bank-cards/${form.id}`, {
          method: 'PUT',
          token: session.token,
          body: payload,
        })
        setFeedback('کارت بانکی با موفقیت ویرایش شد.')
      } else {
        await apiRequest('/admin/bank-cards', {
          method: 'POST',
          token: session.token,
          body: payload,
        })
        setFeedback('کارت بانکی جدید با موفقیت ثبت شد.')
      }

      setForm(EMPTY_FORM)
      await loadCards()
    } catch (err) {
      setError(getReadableError(err))
    } finally {
      setSaving(false)
    }
  }

  const handleDelete = async (card) => {
    if (!window.confirm(`کارت «${card.title || card.bankName}» حذف شود؟`)) return
    setSaving(true)
    setError('')
    setFeedback('')
    try {
      await apiRequest(`/admin/bank-cards/${card.id}`, {
        method: 'DELETE',
        token: session.token,
      })
      if (form.id === card.id) setForm(EMPTY_FORM)
      setFeedback('کارت بانکی حذف شد.')
      await loadCards()
    } catch (err) {
      setError(getReadableError(err))
    } finally {
      setSaving(false)
    }
  }

  return (
    <section id="settings-bank-cards" className="panel settings-section" style={{ marginTop: '1rem' }}>
      <div className="panel-head">
        <div className="settings-section-head">
          <span className="settings-section-badge" aria-hidden="true">
            💳
          </span>
          <div>
            <h2>کارت‌های بانکی ادمین (کارت به کارت POS)</h2>
            <p>
              بانک را انتخاب کنید یا شماره کارت را وارد کنید تا کارت به‌صورت واقعی شبیه‌سازی شود. اعداد فارسی و انگلیسی هر دو پذیرفته و روی کارت با فونت خوانا و انگلیسی نمایش داده می‌شوند.
            </p>
          </div>
        </div>
      </div>

      <div className="abc-editor-grid">
        <form className="abc-form" onSubmit={handleSubmit}>
          <div className="abc-fields-2">
            <label>
              <span>انتخاب بانک *</span>
              <select
                value={form.bankKey}
                onChange={(e) => handleBankSelect(e.target.value)}
              >
                {IRANIAN_BANKS.map((bank) => (
                  <option key={bank.key} value={bank.key}>
                    {bank.name}
                  </option>
                ))}
              </select>
            </label>

            <label>
              <span>شماره کارت ۱۶ رقمی * (فارسی یا انگلیسی)</span>
              <input
                required
                type="text"
                className="ltr-input"
                dir="ltr"
                data-latin-digits="true"
                inputMode="numeric"
                autoComplete="off"
                placeholder="6219 - 8619 - 1234 - 5678"
                value={formatCardInputDisplay(form.cardNumber)}
                onChange={(e) => handleCardNumberChange(e.target.value)}
              />
            </label>
          </div>

          <div className="abc-fields-2">
            <label>
              <span>نام صاحب کارت *</span>
              <input
                required
                placeholder="مثلاً عارف زنگنه"
                value={form.cardHolderName}
                onChange={(e) => setForm((f) => ({ ...f, cardHolderName: e.target.value }))}
              />
            </label>

            {form.bankKey === 'custom' ? (
              <label>
                <span>نام بانک دلخواه *</span>
                <input
                  required
                  placeholder="مثلاً بانک دی یا رسالت"
                  value={form.bankName}
                  onChange={(e) => setForm((f) => ({ ...f, bankName: e.target.value }))}
                />
              </label>
            ) : (
              <label>
                <span>عنوان دلخواه کارت (اختیاری)</span>
                <input
                  placeholder="مثلاً کارت اصلی صندوق"
                  value={form.title}
                  onChange={(e) => setForm((f) => ({ ...f, title: e.target.value }))}
                />
              </label>
            )}
          </div>

          <div className="abc-fields-2">
            <label>
              <span>شماره شبا (اختیاری)</span>
              <input
                className="ltr-input"
                dir="ltr"
                data-latin-digits="true"
                placeholder="IR..."
                value={form.shebaNumber}
                onChange={(e) => setForm((f) => ({ ...f, shebaNumber: e.target.value }))}
              />
            </label>
            <label>
              <span>ترتیب نمایش</span>
              <input
                className="ltr-input"
                type="number"
                min="0"
                value={form.displayOrder}
                onChange={(e) => setForm((f) => ({ ...f, displayOrder: Number(e.target.value) || 0 }))}
              />
            </label>
          </div>

          <ImageUploader
            label="عکس کارت بانکی (اختیاری — در صورت آپلود جایگزین پس‌زمینه شبیه‌ساز می‌شود)"
            hint="اگر عکسی آپلود نکنید، طراحی واقعی همان بانک انتخاب‌شده نمایش داده می‌شود."
            value={form.cardImageUrl}
            token={session?.token}
            onChange={(url) => setForm((f) => ({ ...f, cardImageUrl: url ?? '' }))}
          />

          <div className="abc-toggles">
            <label>
              <input
                type="checkbox"
                checked={form.isDefault}
                onChange={(e) => setForm((f) => ({ ...f, isDefault: e.target.checked }))}
              />
              <span>کارت پیش‌فرض برای کارت به کارت</span>
            </label>
            <label>
              <input
                type="checkbox"
                checked={form.isActive}
                onChange={(e) => setForm((f) => ({ ...f, isActive: e.target.checked }))}
              />
              <span>فعال در صندوق POS</span>
            </label>
          </div>

          {feedback ? <p className="feedback success">{feedback}</p> : null}
          {error ? <p className="feedback error">{error}</p> : null}

          <div style={{ display: 'flex', gap: '0.55rem', flexWrap: 'wrap' }}>
            <button type="submit" className="primary-button" disabled={saving}>
              {saving ? 'در حال ذخیره...' : form.id ? 'ذخیره تغییرات کارت' : '+ ثبت کارت بانکی'}
            </button>
            {form.id ? (
              <button type="button" className="ghost-button" onClick={resetForm}>
                انصراف از ویرایش
              </button>
            ) : null}
          </div>
        </form>

        <div className="abc-preview-col">
          <span className="abc-preview-label">پیش‌نمایش زنده شبیه‌ساز کارت بانکی</span>
          <BankCardVisual card={form} size="normal" />
          <small style={{ fontSize: '0.73rem', color: 'var(--muted)', textAlign: 'center' }}>
            با انتخاب بانک یا وارد کردن ۶ رقم اول کارت، طراحی کارت به‌صورت خودکار تغییر می‌کند.
          </small>
        </div>
      </div>

      {loading ? (
        <p className="feedback" style={{ marginTop: '0.75rem' }}>در حال بارگذاری کارت‌ها...</p>
      ) : cards.length === 0 ? (
        <p className="feedback" style={{ marginTop: '0.75rem' }}>
          هنوز کارتی تعریف نشده است. اولین کارت بانکی کافه را از فرم بالا اضافه کنید.
        </p>
      ) : (
        <div className="abc-cards-grid">
          {cards.map((card) => (
            <div key={card.id} className={`abc-card-item ${card.isActive ? '' : 'is-inactive'}`}>
              <BankCardVisual card={card} size="normal" />
              <div className="abc-card-item-actions">
                <span style={{ fontSize: '0.75rem', color: 'var(--muted)' }}>
                  {card.isActive ? 'فعال در POS' : 'غیرفعال'}
                </span>
                <div style={{ display: 'flex', gap: '0.4rem' }}>
                  <button type="button" className="ghost-button" onClick={() => handleEdit(card)}>
                    ویرایش
                  </button>
                  <button
                    type="button"
                    className="ghost-button"
                    style={{ color: '#dc2626' }}
                    onClick={() => handleDelete(card)}
                  >
                    حذف
                  </button>
                </div>
              </div>
            </div>
          ))}
        </div>
      )}
    </section>
  )
}
