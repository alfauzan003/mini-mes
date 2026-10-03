import { QueryClientProvider } from '@tanstack/react-query'
import { render, screen } from '@testing-library/react'
import { MemoryRouter } from 'react-router'
import { beforeEach, describe, expect, it } from 'vitest'
import { App } from '@/App'
import { queryClient } from '@/shared/api/queryClient'
import type { Role } from '@/shared/api/types'
import { AuthProvider } from '@/shared/auth/AuthContext'
import { AUTH_STORAGE_KEY } from '@/shared/auth/authStorage'

function signInAs(role: Role) {
  localStorage.setItem(
    AUTH_STORAGE_KEY,
    JSON.stringify({
      token: 't',
      user: { username: role.toLowerCase(), displayName: `Demo ${role}`, role },
      expiresAt: new Date(Date.now() + 60_000).toISOString(),
    }),
  )
}

function renderAt(path: string) {
  return render(
    <QueryClientProvider client={queryClient}>
      <MemoryRouter initialEntries={[path]}>
        <AuthProvider>
          <App />
        </AuthProvider>
      </MemoryRouter>
    </QueryClientProvider>,
  )
}

describe('App routing', () => {
  beforeEach(() => localStorage.clear())

  it('redirects anonymous visitors to the login page', () => {
    renderAt('/work-orders')

    expect(screen.getByRole('button', { name: 'Sign in' })).toBeInTheDocument()
    expect(screen.getByText('Log in as Planner')).toBeInTheDocument()
  })

  it('sends a planner to the dashboard without the operator station link', () => {
    signInAs('PLANNER')
    renderAt('/')

    expect(screen.getByRole('heading', { name: 'Dashboard' })).toBeInTheDocument()
    expect(screen.getByRole('link', { name: 'Dashboard' })).toBeInTheDocument()
    expect(screen.queryByRole('link', { name: 'Operator Station' })).not.toBeInTheDocument()
    expect(screen.getByRole('link', { name: 'Carriers' })).toBeInTheDocument()
  })

  it('sends an operator to the station and shows the role in the header', () => {
    signInAs('OPERATOR')
    renderAt('/')

    expect(screen.getByRole('heading', { name: 'Operator Station' })).toBeInTheDocument()
    expect(screen.getByText('OPERATOR')).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Logout' })).toBeInTheDocument()
  })

  it('blocks the operator station for a QC user', () => {
    signInAs('QC')
    renderAt('/station')

    expect(screen.getByRole('heading', { name: 'Access denied' })).toBeInTheDocument()
  })
})
