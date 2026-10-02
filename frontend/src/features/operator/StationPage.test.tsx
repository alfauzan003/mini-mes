import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter, Route, Routes } from 'react-router'
import { afterEach, describe, expect, it, vi } from 'vitest'
import type { AssignmentDto, EquipmentDto, RunDto } from '@/shared/api/types'
import { StationPage } from './StationPage'

const assignments: AssignmentDto[] = [
  { workOrderOperationId: 'op-1', workOrderNumber: 'WO-261003-001', productCode: 'CATH-NCM811', status: 'RELEASED', targetQty: 8, goodCount: 0 },
  { workOrderOperationId: 'op-2', workOrderNumber: 'WO-261003-002', productCode: 'CATH-NCM811', status: 'HOLD', targetQty: 8, goodCount: 0 },
]

const mixRun: RunDto = {
  id: 'run-1',
  equipmentCode: 'MX01',
  workOrderNumber: 'WO-261003-001',
  workOrderOperationId: 'op-1',
  operation: 'MIX',
  operator: 'operator',
  startedAt: '2026-10-03T01:00:00Z',
  endedAt: null,
  goodQty: 0,
  rejectQty: 0,
  parentLotId: null,
  inputs: [{ lotId: 'RM-261003-001', type: 'RAW', role: 'SECONDARY', qty: 50, uom: 'kg', consumedQty: null }],
  outputs: [],
}

const slitRun: RunDto = {
  ...mixRun,
  operation: 'SLIT',
  parentLotId: 'EL-261003-001',
  inputs: [{ lotId: 'EL-261003-001', type: 'ELECTRODE', role: 'PRIMARY', qty: 118, uom: 'm', consumedQty: null }],
}

function equipment(openRun: RunDto | null): EquipmentDto {
  return { code: 'MX01', name: 'Mixer 1', operation: 'MIX', laneCount: openRun?.operation === 'SLIT' ? 8 : null, status: openRun ? 'RUNNING' : 'IDLE', openRun }
}

interface Call {
  url: string
  method: string
  body: unknown
}

function stubApi(initialRun: RunDto | null, postResponse?: () => Promise<Response>) {
  const calls: Call[] = []
  let openRun = initialRun
  vi.stubGlobal(
    'fetch',
    vi.fn(async (input: RequestInfo | URL, init?: RequestInit) => {
      const url = String(input)
      const method = init?.method ?? 'GET'
      calls.push({ url, method, body: init?.body ? JSON.parse(String(init.body)) : undefined })
      if (method === 'POST' && postResponse) {
        const response = await postResponse()
        if (response.ok) openRun = (await response.clone().json()) as RunDto
        return response
      }
      if (method === 'POST') return new Response(JSON.stringify(mixRun), { status: 201 })
      const body = url.endsWith('/assignments') ? assignments : url.startsWith('/api/equipment/') ? equipment(openRun) : []
      return new Response(JSON.stringify(body), { status: 200 })
    }),
  )
  return calls
}

function renderStation() {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } })
  return render(
    <QueryClientProvider client={client}>
      <MemoryRouter initialEntries={['/station/MX01']}>
        <Routes>
          <Route path="/station/:equipmentCode" element={<StationPage />} />
        </Routes>
      </MemoryRouter>
    </QueryClientProvider>,
  )
}

describe('StationPage', () => {
  afterEach(() => vi.unstubAllGlobals())

  it('disables work orders on hold with a hint and tracks in the scanned list', async () => {
    const calls = stubApi(null)
    renderStation()

    const hold = await screen.findByRole('radio', { name: /WO-261003-002/ })
    expect(hold).toBeDisabled()
    expect(screen.getByText(/Work order is on hold/)).toBeInTheDocument()

    await userEvent.click(screen.getByRole('radio', { name: /WO-261003-001/ }))
    const scan = await screen.findByLabelText('Scan lot or carrier')
    await userEvent.type(scan, ' rm-261003-001 {Enter}')
    await userEvent.type(scan, 'RM-261003-002{Enter}')
    await userEvent.click(screen.getByRole('button', { name: 'Remove RM-261003-002' }))
    await userEvent.click(screen.getByRole('button', { name: 'Track in' }))

    await waitFor(() => expect(calls.some((c) => c.method === 'POST')).toBe(true))
    expect(calls.find((c) => c.method === 'POST')).toMatchObject({
      url: '/api/runs/track-in',
      body: { equipmentCode: 'MX01', workOrderOperationId: 'op-1', inputs: ['RM-261003-001'] },
    })
  })

  it('sends a MIX output once even when Produce is double-clicked', async () => {
    let release: (response: Response) => void = () => {}
    const calls = stubApi(mixRun, () => new Promise<Response>((resolve) => (release = resolve)))
    renderStation()

    expect(await screen.findByText('RM-261003-001')).toBeInTheDocument()
    expect(screen.queryByLabelText('Empty carrier')).not.toBeInTheDocument()
    await userEvent.type(screen.getByLabelText('Good (kg)'), '40')
    await userEvent.dblClick(screen.getByRole('button', { name: 'Produce' }))

    await waitFor(() => expect(calls.filter((c) => c.method === 'POST')).toHaveLength(1))
    expect(calls.find((c) => c.method === 'POST')).toMatchObject({
      url: '/api/runs/run-1/outputs',
      body: { outputs: [{ carrierCode: null, lane: null, goodQty: 40, rejectQty: 0 }] },
    })
    const produced = { ...mixRun, outputs: [{ lotId: 'SL-1', carrierCode: null, lane: null, goodQty: 40, rejectQty: 0 }] }
    release(new Response(JSON.stringify(produced), { status: 200 }))

    expect(await screen.findByText(/Output recorded/)).toBeInTheDocument()
    expect(screen.queryByRole('button', { name: 'Produce' })).not.toBeInTheDocument()
  })

  it('keeps Track out disabled for a SLIT run until its output is recorded', async () => {
    stubApi(slitRun)
    const { unmount } = renderStation()

    const trackOut = await screen.findByRole('button', { name: 'Track out' })
    expect(trackOut).toBeDisabled()
    expect(screen.getByText('Record the slitting output first')).toBeInTheDocument()
    unmount()

    const produced = { ...slitRun, outputs: [{ lotId: 'PC-1', carrierCode: 'PC-0001', lane: 1, goodQty: 14, rejectQty: 0 }] }
    stubApi(produced)
    renderStation()

    expect(await screen.findByRole('button', { name: 'Track out' })).toBeEnabled()
    expect(screen.queryByText('Record the slitting output first')).not.toBeInTheDocument()
  })

  it('leaves Track out enabled for a MIX run with nothing produced', async () => {
    stubApi(mixRun)
    renderStation()

    expect(await screen.findByRole('button', { name: 'Track out' })).toBeEnabled()
  })
})
