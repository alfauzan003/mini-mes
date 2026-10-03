import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { render, screen, within } from '@testing-library/react'
import { afterEach, describe, expect, it, vi } from 'vitest'
import type { InspectionDto } from '@/shared/api/types'
import { LotQualityTab } from './LotQualityTab'

const INSPECTION: InspectionDto = {
  id: 'i1',
  lotId: 'SL-261003-001',
  operation: 'MIX',
  inspector: 'qc1',
  inspectedAt: '2026-10-03T01:00:00Z',
  result: 'FAIL',
  defectCode: 'VISC_HIGH',
  defectDescription: 'Viscosity too high',
  reason: 'Out of spec',
  rejectQty: null,
  disposition: 'SCRAP',
  dispositionBy: 'qc1',
  dispositionAt: '2026-10-03T02:00:00Z',
  dispositionReason: 'Cannot rework',
  measurements: [
    { itemName: 'Viscosity', unit: 'mPa.s', lsl: 3000, usl: 5000, value: 5200, judgment: 'NG' },
    { itemName: 'Solid content', unit: '%', lsl: 60, usl: 70, value: 65, judgment: 'OK' },
  ],
}

function setup(body: InspectionDto[]) {
  vi.stubGlobal('fetch', vi.fn(async () => new Response(JSON.stringify(body), { status: 200 })))
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } })
  render(
    <QueryClientProvider client={client}>
      <LotQualityTab lotId="SL-261003-001" />
    </QueryClientProvider>,
  )
}

describe('LotQualityTab', () => {
  afterEach(() => vi.unstubAllGlobals())

  it('renders measurements with NG highlighted and the disposition line', async () => {
    setup([INSPECTION])

    const ngRow = (await screen.findByText('Viscosity')).closest('tr')!
    expect(within(ngRow).getByText('NG')).toBeInTheDocument()
    expect(within(ngRow).getByText('3000 – 5000')).toBeInTheDocument()
    expect(within(screen.getByText('Solid content').closest('tr')!).getByText('OK')).toBeInTheDocument()
    expect(screen.getByText('FAIL')).toBeInTheDocument()
    expect(screen.getByText(/VISC_HIGH/)).toBeInTheDocument()
    expect(screen.getByText(/Out of spec/)).toBeInTheDocument()
    expect(screen.getByText(/SCRAP/)).toBeInTheDocument()
    expect(screen.getByText(/Cannot rework/)).toBeInTheDocument()
  })

  it('shows empty state without inspections', async () => {
    setup([])
    expect(await screen.findByText('No inspections yet.')).toBeInTheDocument()
  })
})
