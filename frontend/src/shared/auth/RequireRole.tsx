import type { ReactNode } from 'react'
import { Navigate, Outlet, useLocation } from 'react-router'
import type { Role } from '@/shared/api/types'
import { useAuth } from './AuthContext'

/** Redirects anonymous visitors to the login page, remembering where they were headed. */
export function RequireAuth() {
  const { user } = useAuth()
  const location = useLocation()
  if (!user) return <Navigate to="/login" replace state={{ from: location.pathname + location.search }} />
  return <Outlet />
}

/** Renders its children only for the listed roles; everyone else sees an access message. */
export function RequireRole({ roles, children }: { roles: Role[]; children: ReactNode }) {
  const { user } = useAuth()
  if (!user) return <Navigate to="/login" replace />
  if (!roles.includes(user.role)) {
    return (
      <div className="rounded-lg border bg-card p-6">
        <h1 className="text-lg font-semibold">Access denied</h1>
        <p className="mt-1 text-sm text-muted-foreground">
          Your role ({user.role}) cannot open this page.
        </p>
      </div>
    )
  }
  return <>{children}</>
}
