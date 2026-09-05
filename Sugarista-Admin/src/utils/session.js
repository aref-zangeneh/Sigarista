export const SESSION_STORAGE_KEY = 'sugarista.admin.session'

export function normalizeSession(raw) {
  if (!raw) {
    return null
  }

  const token = raw.token ?? raw.Token
  if (!token) {
    return null
  }

  const permissions = raw.permissions ?? raw.Permissions ?? []

  return {
    token,
    userId: raw.userId ?? raw.UserId,
    username: raw.username ?? raw.Username,
    displayName: raw.displayName ?? raw.DisplayName,
    role: raw.role ?? raw.Role,
    isSuperAdmin: Boolean(raw.isSuperAdmin ?? raw.IsSuperAdmin),
    permissions: Array.isArray(permissions) ? permissions : [],
  }
}

export function loadStoredSession() {
  try {
    const rawValue = window.localStorage.getItem(SESSION_STORAGE_KEY)
    return rawValue ? normalizeSession(JSON.parse(rawValue)) : null
  } catch {
    return null
  }
}
