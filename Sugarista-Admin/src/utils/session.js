export const SESSION_STORAGE_KEY = 'sugarista.admin.session'
export const ADMIN_SESSION_TTL_DAYS = 30
export const ADMIN_SESSION_TTL_MS = ADMIN_SESSION_TTL_DAYS * 24 * 60 * 60 * 1000

function parseJwtExpirationMs(token) {
  if (typeof token !== 'string') {
    return null
  }

  const parts = token.split('.')
  if (parts.length !== 3) {
    return null
  }

  try {
    const base64 = parts[1].replace(/-/g, '+').replace(/_/g, '/')
    const padded = base64.padEnd(base64.length + ((4 - (base64.length % 4)) % 4), '=')
    const payload = JSON.parse(window.atob(padded))
    if (typeof payload?.exp === 'number' && Number.isFinite(payload.exp)) {
      return payload.exp * 1000
    }
  } catch {
    // Ignore malformed JWT payloads and fall back to explicit expiresAt.
  }

  return null
}

export function isSessionExpired(session, nowMs = Date.now()) {
  if (!session?.token) {
    return true
  }

  const jwtExpMs = parseJwtExpirationMs(session.token)
  if (jwtExpMs !== null) {
    return jwtExpMs <= nowMs
  }

  if (typeof session.expiresAt === 'number' && Number.isFinite(session.expiresAt)) {
    return session.expiresAt <= nowMs
  }

  return false
}

export function normalizeSession(raw) {
  if (!raw) {
    return null
  }

  const token = raw.token ?? raw.Token
  if (!token) {
    return null
  }

  const permissions = raw.permissions ?? raw.Permissions ?? []
  const jwtExpMs = parseJwtExpirationMs(token)
  const rawExpiresAt = raw.expiresAt ?? raw.ExpiresAt
  const parsedExpiresAt =
    typeof rawExpiresAt === 'number'
      ? rawExpiresAt
      : typeof rawExpiresAt === 'string'
        ? Date.parse(rawExpiresAt)
        : null

  const expiresAt =
    jwtExpMs ??
    (Number.isFinite(parsedExpiresAt) ? parsedExpiresAt : Date.now() + ADMIN_SESSION_TTL_MS)

  return {
    token,
    userId: raw.userId ?? raw.UserId,
    username: raw.username ?? raw.Username,
    displayName: raw.displayName ?? raw.DisplayName,
    role: raw.role ?? raw.Role,
    isSuperAdmin: Boolean(raw.isSuperAdmin ?? raw.IsSuperAdmin),
    permissions: Array.isArray(permissions) ? permissions : [],
    expiresAt,
  }
}

export function loadStoredSession() {
  try {
    const rawValue = window.localStorage.getItem(SESSION_STORAGE_KEY)
    if (!rawValue) {
      return null
    }

    const session = normalizeSession(JSON.parse(rawValue))
    if (!session || isSessionExpired(session)) {
      window.localStorage.removeItem(SESSION_STORAGE_KEY)
      return null
    }

    return session
  } catch {
    return null
  }
}
