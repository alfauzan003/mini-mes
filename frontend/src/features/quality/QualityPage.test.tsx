import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter, Route, Routes } from 'react-router'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import type { InspectionDto, LotDto, Role } from '@/shared/api/types'
import { AuthProvider } from '@/shared/auth/AuthContext'
import { AUTH_STORAGE_KEY } from '@/shared/auth/authStorage'
import { QualityPage } from './QualityPage'

const QUEUED: LotDto = {
  lotId: 'SL-261003-001',
  type: 'SLURRY',
  polarity: 'CATHODE',
  productCode: 'CAT-A',
  materialCode: null,
  workOrderNumber: 'WO-261003-001',
  qty: 40,
  uom: 'kg',
  status: 'WAIT',
  quality: 'NONE',
  currentOperation: 'MIX',
  nextOperation: 'COAT',
  currentEquipment: null,
  currentCarrier: 'SB-0001',
  createdAt: '2026-10-03T00:00:00Z',
}

const HELD: LotDto = {
  ...QUEUED,
  lotId: 'SL-261003-002',
  status: 'HOLD',
  quality: 'FAIL',
  currentCarrier: null,
}

const FAILED_INSPECTION: InspectionDto = {
  id: 'insp-1',
  lotId: HELD.lotId,
  operation: 'MIX',
  inspector: 'qc',
  inspectedAt: '2026-10-03T01:00:00Z',
  result: 'FAIL',
  defectCode: 'MX-VISC',
  defectDescription: 'Viscosity out of spec',
  reason: 'Too thick',
  rejectQty: null,
  disposition: null,
  dispositionBy: null,
  dispositionAt: null,
  dispositionReason: null,
  measurements: [],
}

function renderQuality(role: Role) {
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
      <MemoryRouter initialEntries={['/quality']}>
        <AuthProvider>
          <Routes>
            <Route path="/quality" element={<QualityPage />} />
          </Routes>
        </AuthProvider>
      </MemoryRouter>
    </QueryClientProvider>,
  )
}

describe('QualityPage', () => {
  let fetchMock: ReturnType<typeof vi.fn>

  beforeEach(() => {
    localStorage.clear()
    fetchMock = vi.fn(async (url: string) => {
      if (url === '/api/inspections/queue') return new Response(JSON.stringify([QUEUED]), { status: 200 })
      if (url === '/api/lots?status=HOLD') return new Response(JSON.stringify([HELD]), { status: 200 })
      if (url === `/api/lots/${HELD.lotId}/inspections`)
        return new Response(JSON.stringify([FAILED_INSPECTION]), { status: 200 })
      return new Response('[]', { status: 200 })
    })
    vi.stubGlobal('fetch', fetchMock)
  })
  afterEach(() => vi.unstubAllGlobals())

  it('lists queue lots with inspect action for QC', async () => {
    renderQuality('QC')

    expect(await screen.findByRole('link', { name: QUEUED.lotId })).toBeInTheDocument()
    expect(screen.getByText('MIX')).toBeInTheDocument()
    expect(screen.getByText('SB-0001')).toBeInTheDocument()
    expect(screen.getByText('WO-261003-001')).toBeInTheDocument()
    expect(screen.getByRole('link', { name: 'Inspect' })).toHaveAttribute('href', `/quality/inspect/${QUEUED.lotId}`)
    expect(screen.getByRole('link', { name: 'Spec limits' })).toHaveAttribute('href', '/quality/specs')
  })

  it('hides inspect action for planner', async () => {
    renderQuality('PLANNER')

    expect(await screen.findByRole('link', { name: QUEUED.lotId })).toBeInTheDocument()
    expect(screen.queryByRole('link', { name: 'Inspect' })).not.toBeInTheDocument()
  })

  it('on hold tab lists held lots', async () => {
    renderQuality('QC')
    await screen.findByRole('link', { name: QUEUED.lotId })

    await userEvent.click(screen.getByRole('tab', { name: 'On hold' }))

    expect(await screen.findByRole('link', { name: HELD.lotId })).toBeInTheDocument()
    expect(await screen.findByText('MX-VISC')).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Disposition' })).toBeInTheDocument()
    expect(fetchMock).toHaveBeenCalledWith('/api/lots?status=HOLD', expect.anything())
  })

  it('hides disposition action for operator', async () => {
    renderQuality('OPERATOR')
    await userEvent.click(await screen.findByRole('tab', { name: 'On hold' }))

    expect(await screen.findByRole('link', { name: HELD.lotId })).toBeInTheDocument()
    expect(screen.queryByRole('button', { name: 'Disposition' })).not.toBeInTheDocument()
  })
})
