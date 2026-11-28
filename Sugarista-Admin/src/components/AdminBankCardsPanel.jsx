import { useCallback, useEffect, useState } from 'react'
import { ImageUploader } from './ImageUploader'
import {
  BANK_CARD_THEMES,
  BankCardVisual,
  normalizeCardDigits,
} from './BankCardVisual.jsx'
import './AdminBankCardsPanel.css'

const EMPTY_FORM = {
  id: '',
  title: '',
  bankName: '',
  cardHolderName: '',
  cardNumber: '',
  shebaNumber: '',
  cardImageUrl: '',
  cardTheme: 'ocean',
  isDefault: false,
  isActive: true,
  displayOrder: 0,
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

  const handleEdit = (card) => {
    setForm({
      id: card.id,
      title: card.title ?? '',
      bankName: card.bankName ?? '',
      cardHolderName: card.cardHolderName ?? '',
      cardNumber: card.cardNumber ?? '',
      shebaNumber: card.shebaNumber ?? '',
      cardImageUrl: card.cardImageUrl ?? '',
      cardTheme: card.cardTheme || 'ocean',
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
    if (!form.bankName.trim() || !form.cardHolderName.trim()) {
      setError('نام بانک و نام صاحب کارت الزامی است.')
      return
    }

    setSaving(true)
    setError('')
    setFeedback('')
    try {
      const payload = {
        title: form.title.trim() || `${form.bankName.trim()} — ${form.cardHolderName.trim()}`,
        bankName: form.bankName.trim(),
        cardHolderName: form.cardHolderName.trim(),
        cardNumber: digits,
        shebaNumber: form.shebaNumber.trim() || null,
        cardImageUrl: form.cardImageUrl.trim() || null,
        cardTheme: form.cardTheme || 'ocean',
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
              کارت‌های بانکی کافه را همراه با عکس و شبیه‌ساز کارت تعریف کنید تا در صندوق فروش (POS) هنگام ثبت پرداخت «کارت به کارت»، صندوقدار کارت را انتخاب کند و شماره کارت به‌صورت خوانا و درشت نمایش داده شود.
            </p>
          </div>
        </div>
      </div>

      <div className="abc-editor-grid">
        <form className="abc-form" onSubmit={handleSubmit}>
          <div className="abc-fields-2">
            <label>
              <span>عنوان کارت (مثلاً کارت اصلی صندوق)</span>
              <input
                placeholder="مثلاً کارت اصلی کافه"
                value={form.title}
                onChange={(e) => setForm((f) => ({ ...f, title: e.target.value }))}
              />
            </label>
            <label>
              <span>نام بانک *</span>
              <input
                required
                placeholder="مثلاً بانک ملت، سامان، بلوبانک..."
                value={form.bankName}
                onChange={(e) => setForm((f) => ({ ...f, bankName: e.target.value }))}
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
            <label>
              <span>شماره کارت ۱۶ رقمی *</span>
              <input
                required
                className="ltr-input"
                dir="ltr"
                inputMode="numeric"
                placeholder="6104337812345678"
                value={form.cardNumber}
                onChange={(e) =>
                  setForm((f) => ({ ...f, cardNumber: normalizeCardDigits(e.target.value) }))
                }
              />
            </label>
          </div>

          <div className="abc-fields-2">
            <label>
              <span>شماره شبا (اختیاری)</span>
              <input
                className="ltr-input"
                dir="ltr"
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
            label="عکس کارت بانکی (اختیاری)"
            hint="می‌توانید تصویر کارت یا پس‌زمینه دلخواه را آپلود کنید تا روی شبیه‌ساز کارت نمایش داده شود."
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
          <span className="abc-preview-label">پیش‌نمایش زنده شبیه‌ساز کارت ادمین</span>
          <BankCardVisual card={form} size="normal" />
          <div className="abc-theme-pills">
            {BANK_CARD_THEMES.map((t) => (
              <button
                key={t.key}
                type="button"
                className={`abc-theme-pill ${form.cardTheme === t.key ? 'is-active' : ''}`}
                onClick={() => setForm((f) => ({ ...f, cardTheme: t.key }))}
              >
                {t.label}
              </button>
            ))}
          </div>
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
