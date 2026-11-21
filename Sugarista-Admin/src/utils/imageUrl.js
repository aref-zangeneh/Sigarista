export const DEFAULT_PUBLIC_ORIGIN = 'https://sugarista.ir'

/**
 * Resolves the site origin used to turn root-relative upload paths
 * (/api/files/images/...) into full, HTML5-valid URLs.
 * On production (https://sugarista.ir/admin) this resolves to https://sugarista.ir;
 * on local dev (http://localhost/admin) it resolves to http://localhost so previews load.
 */
export function resolveImageOrigin(explicitOrigin) {
  if (typeof explicitOrigin === 'string' && explicitOrigin.trim()) {
    return explicitOrigin.trim().replace(/\/+$/, '')
  }

  if (
    typeof window !== 'undefined' &&
    typeof window.location?.origin === 'string' &&
    /^https?:\/\//i.test(window.location.origin)
  ) {
    return window.location.origin.replace(/\/+$/, '')
  }

  return DEFAULT_PUBLIC_ORIGIN
}

/**
 * Ensures any uploaded or relative image path carries a full scheme + origin
 * (e.g. https://sugarista.ir/api/files/images/...) so <input type="url"> passes
 * browser validation cleanly on submit.
 */
export function normalizeImageUrl(rawUrl, explicitOrigin) {
  if (rawUrl == null) {
    return ''
  }

  const trimmed = String(rawUrl).trim()
  if (!trimmed) {
    return ''
  }

  if (/^https?:\/\//i.test(trimmed)) {
    return trimmed
  }

  if (trimmed.startsWith('//')) {
    return `https:${trimmed}`
  }

  const origin = resolveImageOrigin(explicitOrigin)

  if (trimmed.startsWith('/')) {
    return `${origin}${trimmed}`
  }

  // Domain-only input such as "sugarista.ir/api/files/images/a.webp"
  if (/^[a-z0-9-]+(\.[a-z0-9-]+)+(\/|$)/i.test(trimmed)) {
    return `https://${trimmed}`
  }

  // Relative path without leading slash (e.g. "api/files/images/a.webp")
  return `${origin}/${trimmed}`
}

/**
 * Returns true if the URL is a relative upload path that should be upgraded
 * immediately (on load, upload, or paste) without interrupting manual typing.
 */
export function isRelativeSitePath(rawUrl) {
  if (typeof rawUrl !== 'string') return false
  const trimmed = rawUrl.trim()
  return trimmed.startsWith('/') || /^(api|uploads)\//i.test(trimmed)
}
