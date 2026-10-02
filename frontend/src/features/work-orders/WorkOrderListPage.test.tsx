import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { render, screen } from '@testing-library/react'
import { MemoryRouter } from 'react-router'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import type { Role } from '@/shared/api/types'
import { AuthProvider } from '@/shared/auth/AuthContext'
import { AUTH_STORAGE_KEY } from '@/shared/auth/authStorage'
import { WorkOrderListPage } from './WorkOrderListPage'

function renderList(role: Role) {
  localStorage.setItem(
    AUTH_STORAGE_KEY,
    JSON.stringify({
      token: 't',
      user: { username: role.toLowerCase(), displayName: role, role },
      expiresAt: new Date(Date.now() + 60_000).toISOString(),
    }),
  )
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } })
  return render(
    <QueryClientProvider client={client}>
      <MemoryRouter>
        <AuthProvider>
          <WorkOrderListPage />
        </AuthProvider>
      </MemoryRouter>
    </QueryClientProvider>,
  )
}

describe('WorkOrderListPage', () => {
  beforeEach(() => {
    localStorage.clear()
    vi.stubGlobal('fetch', vi.fn(async () => new Response('[]', { status: 200 })))
  })
  afterEach(() => vi.unstubAllGlobals())

  it('shows New Work Order to a planner', async () => {
    renderList('PLANNER')

    expect(await screen.findByText('No work orders.')).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'New Work Order' })).toBeInTheDocument()
  })

  it('hides New Work Order from an operator', async () => {
    renderList('OPERATOR')

    expect(await screen.findByText('No work orders.')).toBeInTheDocument()
    expect(screen.queryByRole('button', { name: 'New Work Order' })).not.toBeInTheDocument()
  })
})
