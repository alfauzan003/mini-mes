import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import type { LotDto } from '@/shared/api/types'
import { DispositionDialog } from './DispositionDialog'

const LOT: LotDto = {
  lotId: 'SL-261003-001',
  type: 'SLURRY',
  polarity: 'CATHODE',
  productCode: 'CAT-A',
  materialCode: null,
  workOrderNumber: 'WO-261003-001',
  qty: 40,
  uom: 'kg',
  status: 'HOLD',
  quality: 'FAIL',
  currentOperation: 'MIX',
  nextOperation: 'COAT',
  currentEquipment: null,
  currentCarrier: null,
  createdAt: '2026-10-03T00:00:00Z',
}

function setup() {
  const onOpenChange = vi.fn()
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } })
  render(
    <QueryClientProvider client={client}>
      <DispositionDialog lot={LOT} open onOpenChange={onOpenChange} />
    </QueryClientProvider>,
  )
  return { onOpenChange }
}

describe('DispositionDialog', () => {
  let fetchMock: ReturnType<typeof vi.fn>

  beforeEach(() => {
    fetchMock = vi.fn(async () => new Response(JSON.stringify({ ...LOT, status: 'SCRAPPED' }), { status: 200 }))
    vi.stubGlobal('fetch', fetchMock)
  })
  afterEach(() => vi.unstubAllGlobals())

  it('requires a reason before confirming', async () => {
    setup()

    await userEvent.click(screen.getByRole('radio', { name: 'Scrap' }))
    const confirm = screen.getByRole('button', { name: 'Scrap lot' })
    expect(confirm).toBeDisabled()

    await userEvent.type(screen.getByLabelText('Reason'), '   ')
    expect(confirm).toBeDisabled()

    await userEvent.type(screen.getByLabelText('Reason'), 'Burr')
    expect(confirm).toBeEnabled()
  })

  it('does not allow confirming before a decision is picked', async () => {
    setup()

    await userEvent.type(screen.getByLabelText('Reason'), 'Burr')

    expect(screen.getByRole('button', { name: 'Confirm disposition' })).toBeDisabled()
    expect(fetchMock).not.toHaveBeenCalled()
  })

  it('sends SCRAP with reason', async () => {
    const { onOpenChange } = setup()

    await userEvent.click(screen.getByRole('radio', { name: 'Scrap' }))
    await userEvent.type(screen.getByLabelText('Reason'), 'Burr')
    await userEvent.click(screen.getByRole('button', { name: 'Scrap lot' }))

    await vi.waitFor(() => expect(fetchMock).toHaveBeenCalled())
    const [url, init] = fetchMock.mock.calls[0] as [string, RequestInit]
    expect(url).toBe('/api/lots/SL-261003-001/disposition')
    expect(init.method).toBe('POST')
    expect(JSON.parse(init.body as string)).toEqual({ decision: 'SCRAP', reason: 'Burr' })
    await vi.waitFor(() => expect(onOpenChange).toHaveBeenCalledWith(false))
  })

  it('sends RELEASE with reason', async () => {
    setup()

    await userEvent.click(screen.getByRole('radio', { name: 'Release' }))
    await userEvent.type(screen.getByLabelText('Reason'), 'Rechecked OK')
    await userEvent.click(screen.getByRole('button', { name: 'Release lot' }))

    await vi.waitFor(() => expect(fetchMock).toHaveBeenCalled())
    const [, init] = fetchMock.mock.calls[0] as [string, RequestInit]
    expect(JSON.parse(init.body as string)).toEqual({ decision: 'RELEASE', reason: 'Rechecked OK' })
  })
})
