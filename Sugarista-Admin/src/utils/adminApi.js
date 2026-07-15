export const API_BASE_URL = import.meta.env.VITE_API_BASE_URL ?? '/api'

export function getReadableError(error) {
  const message = error instanceof Error ? error.message : 'خطای ناشناخته'

  if (message === 'Unauthorized.' || message.includes('401')) {
    return 'نشست شما منقضی شده یا دسترسی ندارید. دوباره وارد شوید.'
  }

  if (message === 'Invalid username or password.') {
    return 'نام کاربری یا رمز عبور نادرست است.'
  }

  if (message === 'Resource not found.') {
    return 'آیتم موردنظر پیدا نشد.'
  }

  return message
}

export async function apiRequest(path, { method = 'GET', body, token } = {}) {
  const response = await fetch(`${API_BASE_URL}${path}`, {
    method,
    headers: {
      'Content-Type': 'application/json',
      ...(token ? { Authorization: `Bearer ${token}` } : {}),
    },
    body: body ? JSON.stringify(body) : undefined,
  })

  if (!response.ok) {
    let message = `درخواست با خطای ${response.status} مواجه شد.`

    try {
      const payload = await response.json()
      const fieldErrors = payload.errors ? Object.values(payload.errors).flat().join('، ') : ''
      message = fieldErrors || payload.detail || payload.title || message
    } catch {
      // Ignore invalid JSON payloads and keep the fallback message.
    }

    throw new Error(message)
  }

  if (response.status === 204) {
    return null
  }

  return response.json()
}

export function normalizeRecipeResponse(payload) {
  if (Array.isArray(payload)) {
    return { description: '', lines: payload }
  }

  return {
    description: payload?.description ?? '',
    lines: payload?.lines ?? [],
  }
}
