import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter, Route, Routes } from 'react-router'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import type { LotDto, Role } from '@/shared/api/types'
import { AuthProvider } from '@/shared/auth/AuthContext'
import { AUTH_STORAGE_KEY } from '@/shared/auth/authStorage'
import { LotListPage } from './LotListPage'

const LOT: LotDto = {
  lotId: 'PC-261003-001',
  type: 'PANCAKE',
  polarity: 'CATHODE',
  productCode: 'C-A',
  materialCode: null,
  workOrderNumber: 'WO-261003-001',
  qty: 120,
  uom: 'm',
  status: 'WAIT',
  quality: 'NONE',
  currentOperation: 'SLIT',
  nextOperation: null,
  currentEquipment: null,
  currentCarrier: 'PC-0001',
  createdAt: '2026-10-03T00:00:00Z',
}

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
      <MemoryRouter initialEntries={['/lots']}>
        <AuthProvider>
          <Routes>
            <Route path="/lots" element={<LotListPage />} />
            <Route path="/lots/:lotId" element={<p>Detail of the opened lot</p>} />
          </Routes>
        </AuthProvider>
      </MemoryRouter>
    </QueryClientProvider>,
  )
}

describe('LotListPage', () => {
  let fetchMock: ReturnType<typeof vi.fn>

  beforeEach(() => {
    localStorage.clear()
    fetchMock = vi.fn(async () => new Response(JSON.stringify([LOT]), { status: 200 }))
    vi.stubGlobal('fetch', fetchMock)
  })
  afterEach(() => vi.unstubAllGlobals())

  it('lists lots with their carrier and operation flow', async () => {
    renderList('OPERATOR')

    expect(await screen.findByRole('link', { name: LOT.lotId })).toBeInTheDocument()
    expect(screen.getByText('SLIT → -')).toBeInTheDocument()
    expect(screen.getByText('PC-0001')).toBeInTheDocument()
  })

  it('shows Register material to a planner but not to an operator', async () => {
    const { unmount } = renderList('PLANNER')
    expect(await screen.findByRole('button', { name: 'Register material' })).toBeInTheDocument()
    unmount()

    renderList('OPERATOR')
    await screen.findByRole('link', { name: LOT.lotId })
    expect(screen.queryByRole('button', { name: 'Register material' })).not.toBeInTheDocument()
  })

  it('sends the selected filters to the API', async () => {
    renderList('PLANNER')
    await screen.findByRole('link', { name: LOT.lotId })

    await userEvent.selectOptions(screen.getByLabelText('Type'), 'PANCAKE')
    await userEvent.selectOptions(screen.getByLabelText('Status'), 'WAIT')

    await vi.waitFor(() =>
      expect(fetchMock).toHaveBeenCalledWith('/api/lots?type=PANCAKE&status=WAIT', expect.anything()),
    )
  })

  it('opens the lot when Enter is pressed on an exact carrier code', async () => {
    renderList('PLANNER')
    await screen.findByRole('link', { name: LOT.lotId })

    await userEvent.type(screen.getByLabelText('Search lots'), 'pc-0001{Enter}')

    expect(await screen.findByText('Detail of the opened lot')).toBeInTheDocument()
  })
})
