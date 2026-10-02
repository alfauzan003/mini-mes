import type { Role, UserDto } from '@/shared/api/types'

export const AUTH_STORAGE_KEY = 'mes.auth'

export interface StoredAuth {
  token: string
  user: UserDto
  expiresAt: string
}

const ROLES: Role[] = ['PLANNER', 'OPERATOR', 'QC', 'ADMIN']

function isStoredAuth(value: unknown): value is StoredAuth {
  if (typeof value !== 'object' || value === null) return false
  const { token, user, expiresAt } = value as Partial<StoredAuth>
  return (
    typeof token === 'string' &&
    typeof expiresAt === 'string' &&
    typeof user === 'object' &&
    user !== null &&
    typeof user.username === 'string' &&
    typeof user.displayName === 'string' &&
    ROLES.includes(user.role)
  )
}

/** Returns the stored session, or null when absent, malformed, expired or storage is unavailable. */
export function readAuth(): StoredAuth | null {
  try {
    const raw = localStorage.getItem(AUTH_STORAGE_KEY)
    if (!raw) return null
    const parsed: unknown = JSON.parse(raw)
    if (!isStoredAuth(parsed)) return null
    if (Date.parse(parsed.expiresAt) <= Date.now()) return null
    return parsed
  } catch {
    return null
  }
}

export function writeAuth(auth: StoredAuth): void {
  try {
    localStorage.setItem(AUTH_STORAGE_KEY, JSON.stringify(auth))
  } catch {
    // Storage unavailable: the session lasts until the page reloads.
  }
}

export function clearAuth(): void {
  try {
    localStorage.removeItem(AUTH_STORAGE_KEY)
  } catch {
    // Nothing to clear when storage is unavailable.
  }
}
