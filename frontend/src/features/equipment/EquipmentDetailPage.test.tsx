import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { render, screen } from '@testing-library/react'
import { MemoryRouter, Route, Routes } from 'react-router'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import type { EquipmentDto, Role } from '@/shared/api/types'
import { AuthProvider } from '@/shared/auth/AuthContext'
import { AUTH_STORAGE_KEY } from '@/shared/auth/authStorage'
import { EquipmentDetailPage } from './EquipmentDetailPage'

const EQUIPMENT: EquipmentDto = {
  code: 'CT01',
  name: 'Coater 1',
  operation: 'COAT',
  laneCount: null,
  status: 'IDLE',
  openRun: null,
}

function renderPage(role: Role) {
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
      <MemoryRouter initialEntries={['/equipment/CT01']}>
        <AuthProvider>
          <Routes>
            <Route path="/equipment/:code" element={<EquipmentDetailPage />} />
          </Routes>
        </AuthProvider>
      </MemoryRouter>
    </QueryClientProvider>,
  )
}

describe('EquipmentDetailPage', () => {
  beforeEach(() => {
    localStorage.clear()
    vi.stubGlobal(
      'fetch',
      vi.fn(async (url: string) => {
        const body = url === '/api/equipment/CT01' ? EQUIPMENT : []
        return new Response(JSON.stringify(body), { status: 200 })
      }),
    )
  })
  afterEach(() => vi.unstubAllGlobals())

  it('admin sees maintenance and inject fault actions', async () => {
    renderPage('ADMIN')

    expect(await screen.findByRole('heading', { name: /Coater 1/ })).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Start maintenance' })).toBeEnabled()
    expect(screen.getByRole('button', { name: 'End maintenance' })).toBeDisabled()
    expect(screen.getByRole('button', { name: 'Inject fault' })).toBeInTheDocument()
  })

  it('operator does not', async () => {
    renderPage('OPERATOR')

    expect(await screen.findByRole('heading', { name: /Coater 1/ })).toBeInTheDocument()
    expect(screen.queryByRole('button', { name: 'Start maintenance' })).not.toBeInTheDocument()
    expect(screen.queryByRole('button', { name: 'End maintenance' })).not.toBeInTheDocument()
    expect(screen.queryByRole('button', { name: 'Inject fault' })).not.toBeInTheDocument()
  })
})
