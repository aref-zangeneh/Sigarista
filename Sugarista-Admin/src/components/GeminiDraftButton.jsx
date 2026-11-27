import { useState } from 'react'
import { useAdmin } from '../context/AdminContext'
import './GeminiDraftButton.css'

/**
 * Draft-only Gemini content generator trigger.
 * Calls POST /api/admin/ai-content/generate (stateless, zero DB writes)
 * and passes the returned structured draft to `onDraft(draft)` so the
 * admin can review, edit, and save manually.
 */
export function GeminiDraftButton({
  section,
  getPayload,
  onDraft,
  label = 'تولید پیش‌نویس با Gemini',
  disabled = false,
  className = '',
}) {
  const { session, apiRequest, getReadableError, setMessage, setError } = useAdmin()
  const [generating, setGenerating] = useState(false)

  const handleClick = async () => {
    if (generating || disabled || !session?.token) return

    const payload = typeof getPayload === 'function' ? getPayload() : getPayload
    const title = (payload?.title ?? '').trim()
    if (!title) {
      setError('ابتدا نام یا عنوان را وارد کنید تا Gemini بتواند پیش‌نویس تولید کند.')
      return
    }

    setGenerating(true)
    setError('')
    setMessage('')
    try {
      const response = await apiRequest('/admin/ai-content/generate', {
        method: 'POST',
        token: session.token,
        body: {
          section,
          title,
          context: payload?.context ?? null,
          tone: payload?.tone ?? null,
          extraNotes: payload?.extraNotes ?? null,
        },
      })

      if (response?.draft && typeof onDraft === 'function') {
        onDraft(response.draft)
      }

      setMessage('پیش‌نویس با Gemini تولید شد — قبل از ثبت نهایی آن را بررسی و روی «ذخیره» کلیک کنید.')
    } catch (err) {
      setError(getReadableError(err))
    } finally {
      setGenerating(false)
    }
  }

  return (
    <button
      type="button"
      className={`gemini-draft-btn ${className}`.trim()}
      disabled={disabled || generating}
      onClick={handleClick}
      title="تولید پیش‌نویس متنی با Gemini (بدون ذخیره خودکار — فقط فیلدهای فرم پر می‌شوند)"
    >
      <span className="gemini-draft-spark" aria-hidden="true">
        {generating ? '⏳' : '✨'}
      </span>
      <span>{generating ? 'در حال نگارش پیش‌نویس...' : label}</span>
    </button>
  )
}
