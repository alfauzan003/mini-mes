import { createContext, useCallback, useContext, useEffect, useMemo, useState, type ReactNode } from 'react'
import { apiFetch, setUnauthorizedHandler } from '@/shared/api/client'
import { queryClient } from '@/shared/api/queryClient'
import type { LoginResponse, UserDto } from '@/shared/api/types'
import { clearAuth, readAuth, writeAuth } from './authStorage'

export interface AuthContextValue {
  user: UserDto | null
  login(username: string, password: string): Promise<void>
  demoLogin(username: string): Promise<void>
  logout(): void
}

const AuthContext = createContext<AuthContextValue | null>(null)

export function AuthProvider({ children }: { children: ReactNode }) {
  const [user, setUser] = useState<UserDto | null>(() => readAuth()?.user ?? null)

  const logout = useCallback(() => {
    clearAuth()
    queryClient.clear()
    setUser(null)
  }, [])

  const startSession = useCallback((response: LoginResponse) => {
    const next: UserDto = {
      username: response.username,
      displayName: response.displayName,
      role: response.role,
    }
    writeAuth({ token: response.token, user: next, expiresAt: response.expiresAt })
    setUser(next)
  }, [])

  useEffect(() => {
    setUnauthorizedHandler(logout)
    return () => setUnauthorizedHandler(null)
  }, [logout])

  const value = useMemo<AuthContextValue>(
    () => ({
      user,
      logout,
      login: async (username, password) =>
        startSession(await apiFetch<LoginResponse>('/api/auth/login', { method: 'POST', json: { username, password } })),
      demoLogin: async (username) =>
        startSession(await apiFetch<LoginResponse>('/api/auth/demo-login', { method: 'POST', json: { username } })),
    }),
    [user, logout, startSession],
  )

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>
}

export function useAuth(): AuthContextValue {
  const context = useContext(AuthContext)
  if (!context) throw new Error('useAuth must be used within an AuthProvider')
  return context
}
