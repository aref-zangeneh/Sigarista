import { useCallback, useMemo, useRef, useState } from 'react'
import {
  MAX_IMAGE_UPLOAD_BYTES,
  formatBytes,
  uploadImage,
  validateImageFile,
} from '../utils/imageUpload'
import './ImageUploader.css'

/**
 * Reusable pro image picker for every place the site stores a picture:
 * drag & drop + click-to-browse, upload progress, live preview, before/after
 * compression report and graceful Persian failure messages. The server keeps
 * the final verdict — this component only smooths the flow.
 *
 * Single mode:  value = string url       onChange(url, meta)
 * Multiple:     value = array of urls    onChange(urls, perUrlMeta, addedMetaList)
 */
export function ImageUploader({
  value = null,
  multiple = false,
  token,
  onChange,
  label = 'تصویر',
  hint,
  allowUrl = true,
  compact = false,
}) {
  const urls = useMemo(
    () =>
      multiple
        ? (Array.isArray(value) ? value : []).filter(Boolean)
        : value
          ? [value]
          : [],
    [value, multiple],
  )

  const [metas, setMetas] = useState({})
  const [uploading, setUploading] = useState(false)
  const [progress, setProgress] = useState(0)
  const [error, setError] = useState('')
  const [dragActive, setDragActive] = useState(false)
  const inputRef = useRef(null)

  const notifyChange = useCallback(
    (nextUrls, nextMetas) => {
      if (!onChange) return
      if (multiple) {
        onChange(nextUrls, nextMetas)
      } else if (nextUrls.length > 0) {
        onChange(nextUrls[0], nextMetas[nextUrls[0]] ?? null)
      } else {
        onChange('', null)
      }
    },
    [multiple, onChange],
  )

  const handleFiles = useCallback(
    async (fileList) => {
      const files = Array.from(fileList ?? [])
      if (!files.length) return

      const accepted = []
      for (const file of files) {
        const validationError = validateImageFile(file)
        if (validationError) {
          setError(validationError)
        } else {
          accepted.push(file)
        }
      }

      if (!accepted.length) return

      setError('')
      setUploading(true)
      setProgress(0)

      const uploadedUrls = []
      const nextMetas = { ...metas }
      const freshMetas = []

      try {
        for (const [index, file] of accepted.entries()) {
          const onProgress = (percent) => setProgress(percent)
          const result = await uploadImage('/admin/uploads/images', file, token, { onProgress })

          if (result?.url) {
            uploadedUrls.push(result.url)
            freshMetas.push({
              url: result.url,
              width: result.width,
              height: result.height,
              sizeBytes: result.sizeBytes,
              originalSizeBytes: result.originalSizeBytes,
              format: result.format,
              isAnimated: result.isAnimated,
            })
            nextMetas[result.url] = freshMetas[freshMetas.length - 1]
          }

          setProgress(Math.round(((index + 1) / accepted.length) * 100))
        }

        const base = multiple ? urls : []
        const nextUrls = multiple
          ? [...base, ...uploadedUrls]
          : uploadedUrls.length
            ? [uploadedUrls[0]]
            : []

        setMetas(nextMetas)
        notifyChange(nextUrls, nextMetas)
      } catch (uploadError) {
        setError(uploadError?.message ?? 'آپلود تصویر ناموفق بود.')
      } finally {
        setUploading(false)
        setProgress(0)
        if (inputRef.current) {
          inputRef.current.value = ''
        }
      }
    },
    [metas, multiple, notifyChange, token, urls],
  )

  const removeUrl = (urlToRemove) => {
    const nextUrls = urls.filter((url) => url !== urlToRemove)
    notifyChange(nextUrls, metas)
  }

  const moveUrl = (url, direction) => {
    if (!multiple) return
    const index = urls.indexOf(url)
    const swapWith = index + direction
    if (index === -1 || swapWith < 0 || swapWith >= urls.length) return

    const nextUrls = [...urls]
    ;[nextUrls[index], nextUrls[swapWith]] = [nextUrls[swapWith], nextUrls[index]]
    notifyChange(nextUrls, metas)
  }

  const renderMetaLine = (url) => {
    const meta = metas[url]
    if (!meta) return null

    const saved =
      meta.originalSizeBytes > 0 && meta.sizeBytes < meta.originalSizeBytes
        ? Math.round((1 - meta.sizeBytes / meta.originalSizeBytes) * 100)
        : 0

    return (
      <span className={`img-up-meta${compact ? ' img-up-meta-compact' : ''}`}>
        {meta.width ? `${meta.width}×${meta.height}` : null}
        {meta.isAnimated ? ' • تصویر متحرک (بدون فشرده‌سازی)' : null}
        {saved > 0 ? (
          <>
            {' '}
            • {formatBytes(meta.originalSizeBytes)} ← {formatBytes(meta.sizeBytes)}{' '}
            <strong>({saved}% کمتر)</strong>
          </>
        ) : (
          ` • ${formatBytes(meta.sizeBytes)}`
        )}
      </span>
    )
  }

  const dropZoneClass = `img-up-dropzone${compact ? ' img-up-dropzone-compact' : ''}${dragActive ? ' img-up-drag-active' : ''}`

  return (
    <div className="img-up-root">
      <div className="img-up-header">
        <span className="img-up-label">{label}</span>
        {hint ? <span className="img-up-hint">{hint}</span> : null}
      </div>

      {urls.length > 0 ? (
        <div className="img-up-grid">
          {urls.map((url) => (
            <div key={url} className="img-up-item">
              <img src={url} alt={label} loading="lazy" />
              <div className="img-up-item-actions">
                <div className="img-up-order">
                  {multiple && urls.length > 1 ? (
                    <>
                      <button
                        type="button"
                        className="img-up-icon-btn"
                        onClick={() => moveUrl(url, -1)}
                        title="انتقال به چپ (عقب)"
                      >
                        →
                      </button>
                      <button
                        type="button"
                        className="img-up-icon-btn"
                        onClick={() => moveUrl(url, 1)}
                        title="انتقال به راست (جلو)"
                      >
                        ←
                      </button>
                    </>
                  ) : null}
                </div>
                <button
                  type="button"
                  className="img-up-remove"
                  onClick={() => removeUrl(url)}
                  title="حذف تصویر"
                >
                  حذف
                </button>
              </div>
              {renderMetaLine(url)}
            </div>
          ))}
        </div>
      ) : null}

      {(!multiple || urls.length < 12) && (
        <div
          className={dropZoneClass}
          role="button"
          tabIndex={0}
          onClick={() => inputRef.current?.click()}
          onKeyDown={(event) => {
            if (event.key === 'Enter' || event.key === ' ') {
              event.preventDefault()
              inputRef.current?.click()
            }
          }}
          onDragOver={(event) => {
            event.preventDefault()
            setDragActive(true)
          }}
          onDragLeave={() => setDragActive(false)}
          onDrop={(event) => {
            event.preventDefault()
            setDragActive(false)
            handleFiles(event.dataTransfer?.files)
          }}
        >
          {uploading ? (
            <div className="img-up-progress">
              <div className="img-up-progress-bar">
                <div style={{ width: `${Math.max(progress, 4)}%` }} />
              </div>
              <span>{progress > 0 ? `${progress}%` : 'در حال آپلود…'}</span>
              <span className="img-up-progress-label">در حال آپلود و بهینه‌سازی…</span>
            </div>
          ) : (
            <>
              <span className="img-up-icon" aria-hidden="true">🖼️</span>
              <span className="img-up-text">
                فایل را اینجا رها کنید یا <strong>برای انتخاب کلیک کنید</strong>
              </span>
              <span className="img-up-subtext">
                JPEG / PNG / WebP / GIF (متحرک) — حداکثر {formatBytes(MAX_IMAGE_UPLOAD_BYTES)}
                {' | '}
                فشرده‌سازی و حذف اطلاعات خصوصی به‌صورت خودکار انجام می‌شود
              </span>
            </>
          )}
        </div>
      )}

      <input
        ref={inputRef}
        type="file"
        accept="image/jpeg,image/png,image/webp,image/gif,image/bmp"
        multiple={multiple}
        hidden
        onChange={(event) => handleFiles(event.target.files)}
      />

      {allowUrl && !multiple ? (
        <input
          className="ltr-input img-up-url"
          type="url"
          dir="ltr"
          placeholder="یا آدرس یک تصویر را مستقیم وارد کنید"
          value={multiple ? '' : urls[0] ?? ''}
          onChange={(event) => notifyChange(event.target.value ? [event.target.value] : [], metas)}
          onKeyDown={(event) => event.stopPropagation()}
        />
      ) : null}

      {error ? <p className="img-up-error">{error}</p> : null}
    </div>
  )
}
