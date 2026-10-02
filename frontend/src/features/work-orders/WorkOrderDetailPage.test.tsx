import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { render, screen } from '@testing-library/react'
import { MemoryRouter, Route, Routes } from 'react-router'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import type { LotDto, Role, WorkOrderDto, WorkOrderStatus } from '@/shared/api/types'
import { AuthProvider } from '@/shared/auth/AuthContext'
import { AUTH_STORAGE_KEY } from '@/shared/auth/authStorage'
import { WorkOrderDetailPage } from './WorkOrderDetailPage'

function workOrder(status: WorkOrderStatus): WorkOrderDto {
  return {
    id: 'wo-id-1',
    number: 'WO-261003-001',
    productCode: 'CATH-NCM811',
    productName: 'Cathode NCM811',
    polarity: 'CATHODE',
    targetQty: 8,
    goodCount: 2,
    status,
    plannedStart: '2026-10-05T01:00:00Z',
    plannedEnd: '2026-10-06T01:00:00Z',
    operations: [
      { id: 'op-2', operation: 'COAT', seq: 2, equipmentCode: 'CT01', runCount: 1, outputQty: 120 },
      { id: 'op-1', operation: 'MIX', seq: 1, equipmentCode: 'MX01', runCount: 1, outputQty: 40 },
    ],
  }
}

const lot: LotDto = {
  lotId: 'SL-261003-001',
  type: 'SLURRY',
  polarity: 'CATHODE',
  productCode: 'CATH-NCM811',
  materialCode: null,
  workOrderNumber: 'WO-261003-001',
  qty: 40,
  uom: 'kg',
  status: 'WAIT',
  quality: 'NONE',
  currentOperation: null,
  nextOperation: 'COAT',
  currentEquipment: null,
  currentCarrier: null,
  createdAt: '2026-10-05T02:00:00Z',
}

let requested: string[]

function stubApi(status: WorkOrderStatus) {
  requested = []
  vi.stubGlobal(
    'fetch',
    vi.fn(async (input: RequestInfo | URL) => {
      const url = String(input)
      requested.push(url)
      const body = url.startsWith('/api/work-orders/') ? workOrder(status) : url.startsWith('/api/lots') ? [lot] : []
      return new Response(JSON.stringify(body), { status: 200 })
    }),
  )
}

function renderDetail(role: Role) {
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
      <MemoryRouter initialEntries={['/work-orders/wo-id-1']}>
        <AuthProvider>
          <Routes>
            <Route path="/work-orders/:id" element={<WorkOrderDetailPage />} />
          </Routes>
        </AuthProvider>
      </MemoryRouter>
    </QueryClientProvider>,
  )
}

describe('WorkOrderDetailPage', () => {
  beforeEach(() => localStorage.clear())
  afterEach(() => vi.unstubAllGlobals())

  it('offers Release and Edit on a planned work order to a planner', async () => {
    stubApi('PLANNED')
    renderDetail('PLANNER')

    expect(await screen.findByRole('button', { name: 'Release' })).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Edit' })).toBeInTheDocument()
    expect(screen.queryByRole('button', { name: 'Hold' })).not.toBeInTheDocument()
    expect(screen.queryByRole('button', { name: 'Complete' })).not.toBeInTheDocument()
  })

  it('offers Hold and Complete on a running work order', async () => {
    stubApi('RUNNING')
    renderDetail('ADMIN')

    expect(await screen.findByRole('button', { name: 'Hold' })).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Complete' })).toBeInTheDocument()
    expect(screen.queryByRole('button', { name: 'Edit' })).not.toBeInTheDocument()
  })

  it('offers Resume on a work order on hold', async () => {
    stubApi('HOLD')
    renderDetail('PLANNER')

    expect(await screen.findByRole('button', { name: 'Resume' })).toBeInTheDocument()
  })

  it('shows no action buttons to an operator', async () => {
    stubApi('PLANNED')
    renderDetail('OPERATOR')

    expect(await screen.findByRole('heading', { name: 'WO-261003-001' })).toBeInTheDocument()
    for (const name of ['Release', 'Hold', 'Resume', 'Complete', 'Edit']) {
      expect(screen.queryByRole('button', { name })).not.toBeInTheDocument()
    }
  })

  it('shows progress, ordered operations and lots linked by lot id, queried by work order number', async () => {
    stubApi('RUNNING')
    renderDetail('PLANNER')

    const progress = await screen.findByRole('progressbar', { name: 'Good count progress' })
    expect(progress).toHaveAttribute('aria-valuenow', '2')
    expect(progress).toHaveAttribute('aria-valuemax', '8')

    const lotLink = await screen.findByRole('link', { name: 'SL-261003-001' })
    expect(lotLink).toHaveAttribute('href', '/lots/SL-261003-001')
    expect(requested).toContain('/api/lots?workOrder=WO-261003-001')

    const rows = screen.getAllByRole('row')
    const mix = rows.findIndex((r) => r.textContent?.includes('MX01'))
    const coat = rows.findIndex((r) => r.textContent?.includes('CT01'))
    expect(mix).toBeGreaterThan(-1)
    expect(mix).toBeLessThan(coat)
  })
})
