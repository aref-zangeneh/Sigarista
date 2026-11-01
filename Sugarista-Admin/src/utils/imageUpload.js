import { API_BASE_URL } from './adminApi'

/**
 * Uploads one image (or several, one call each) through XMLHttpRequest so we
 * get real upload progress. The server validates, strips metadata and
 * compresses to WebP — we only surface its verdict to the user.
 */
export function uploadImage(path, file, token, { onProgress } = {}) {
  return new Promise((resolve, reject) => {
    const formData = new FormData()
    formData.append('file', file)

    const request = new XMLHttpRequest()
    request.open('POST', `${API_BASE_URL}${path}`)
    if (token) {
      request.setRequestHeader('Authorization', `Bearer ${token}`)
    }

    if (onProgress) {
      request.upload.onprogress = (event) => {
        if (event.lengthComputable) {
          onProgress(Math.round((event.loaded / event.total) * 100))
        }
      }
    }

    request.onload = () => {
      if (request.status >= 200 && request.status < 300) {
        try {
          resolve(JSON.parse(request.responseText))
        } catch {
          resolve(null)
        }
        return
      }

      let message = `آپلود تصویر با خطای ${request.status} مواجه شد.`
      try {
        const payload = JSON.parse(request.responseText)
        const fieldErrors = payload.errors
          ? Object.values(payload.errors).flat().join('، ')
          : ''
        message = fieldErrors || payload.detail || payload.title || message
      } catch {
        // keep fallback message
      }
      reject(new Error(message))
    }

    request.onerror = () => reject(new Error('اتصال به سرور برقرار نشد.'))

    request.send(formData)
  })
}

export const MAX_IMAGE_UPLOAD_BYTES = 10 * 1024 * 1024

const ALLOWED_TYPES = /^image\/(jpeg|jpg|png|webp|gif|bmp)$/i

/** Client-side first pass — the server decodes everything for the final say. */
export function validateImageFile(file) {
  if (!file) {
    return 'فایلی انتخاب نشده است.'
  }
  if (file.type === 'image/svg+xml') {
    return 'SVG پذیرفته نمی‌شود. از فرمت‌های JPEG، PNG، WebP یا GIF استفاده کنید.'
  }
  if (file.type && !ALLOWED_TYPES.test(file.type)) {
    return 'فقط تصاویر JPEG، PNG، WebP، GIF یا BMP پذیرفته می‌شوند.'
  }
  if (file.size > MAX_IMAGE_UPLOAD_BYTES) {
    return 'حجم تصویر بیشتر از ۱۰ مگابایت است.'
  }
  if (file.size === 0) {
    return 'فایل تصویر خالی است.'
  }
  return null
}

export function formatBytes(bytes) {
  if (bytes == null || Number.isNaN(Number(bytes))) {
    return '—'
  }
  const value = Number(bytes)
  if (value < 1024) {
    return `${toPersianDigits(value)} بایت`
  }
  if (value < 1024 * 1024) {
    return `${toPersianDigits(Math.round(value / 1024))} کیلوبایت`
  }
  return `${toPersianDigits((value / (1024 * 1024)).toFixed(1))} مگابایت`
}

function toPersianDigits(input) {
  const latin = String(input)
  const persian = '۰۱۲۳۴۵۶۷۸۹'
  return latin.replace(/[0-9]/g, (digit) => persian[Number(digit)])
}
