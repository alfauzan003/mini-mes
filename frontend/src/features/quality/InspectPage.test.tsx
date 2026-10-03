import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { render, screen } from '@testing-library/react'
import { MemoryRouter, Route, Routes } from 'react-router'
import { afterEach, describe, expect, it, vi } from 'vitest'
import type { LotDto } from '@/shared/api/types'
import { InspectPage } from './InspectPage'

const RAW: LotDto = {
  lotId: 'RM-261003-001',
  type: 'RAW',
  polarity: 'CATHODE',
  productCode: null,
  materialCode: 'NCM811',
  workOrderNumber: null,
  qty: 500,
  uom: 'kg',
  status: 'WAIT',
  quality: 'NONE',
  currentOperation: null,
  nextOperation: null,
  currentEquipment: null,
  currentCarrier: null,
  createdAt: '2026-10-03T00:00:00Z',
}

describe('InspectPage', () => {
  afterEach(() => vi.unstubAllGlobals())

  it('shows an empty state and never loads unfiltered specs for a lot without product or operation', async () => {
    const fetchMock = vi.fn(async () => new Response(JSON.stringify(RAW), { status: 200 }))
    vi.stubGlobal('fetch', fetchMock)
    const client = new QueryClient({ defaultOptions: { queries: { retry: false } } })

    render(
      <QueryClientProvider client={client}>
        <MemoryRouter initialEntries={[`/quality/inspect/${RAW.lotId}`]}>
          <Routes>
            <Route path="/quality/inspect/:lotId" element={<InspectPage />} />
          </Routes>
        </MemoryRouter>
      </QueryClientProvider>,
    )

    expect(await screen.findByText('No spec limits for this lot.')).toBeInTheDocument()
    const urls = (fetchMock.mock.calls as unknown as [string][]).map(([url]) => String(url))
    expect(urls.some((url) => url.includes('/api/specs'))).toBe(false)
  })
})
