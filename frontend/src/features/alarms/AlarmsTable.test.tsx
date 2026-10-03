import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter } from 'react-router'
import { afterEach, describe, expect, it, vi } from 'vitest'
import type { AlarmDto } from '@/shared/api/types'
import { AlarmsTable } from './AlarmsTable'

const RAISED = Date.parse('2026-10-03T01:00:00Z')

const active: AlarmDto = {
  id: 'a-1',
  equipmentCode: 'CT01',
  code: 'TEMP_HIGH',
  message: 'Oven temperature too high',
  severity: 'CRITICAL',
  raisedAt: '2026-10-03T01:00:00Z',
  clearedAt: null,
  durationSeconds: null,
  acknowledgedBy: null,
  acknowledgedAt: null,
}

const acked: AlarmDto = {
  ...active,
  id: 'a-2',
  severity: 'WARNING',
  clearedAt: '2026-10-03T01:02:05Z',
  durationSeconds: 125,
  acknowledgedBy: 'operator',
  acknowledgedAt: '2026-10-03T01:01:00Z',
}

function renderTable(alarms: AlarmDto[], canAcknowledge: boolean) {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } })
  render(
    <QueryClientProvider client={client}>
      <MemoryRouter>
        <AlarmsTable alarms={alarms} canAcknowledge={canAcknowledge} now={RAISED + 185_000} />
      </MemoryRouter>
    </QueryClientProvider>,
  )
}

describe('AlarmsTable', () => {
  afterEach(() => vi.unstubAllGlobals())

  it('shows live duration for active alarm', () => {
    renderTable([active, acked], false)

    expect(screen.getByText('3m 05s')).toBeInTheDocument()
    expect(screen.getByText('2m 05s')).toBeInTheDocument()
    expect(screen.getAllByRole('link', { name: 'CT01' })[0]).toHaveAttribute('href', '/equipment/CT01')
    expect(screen.getByText('CRITICAL')).toBeInTheDocument()
  })

  it('shows ack button only when allowed and not acknowledged', () => {
    renderTable([active, acked], true)
    expect(screen.getAllByRole('button', { name: 'Ack' })).toHaveLength(1)
  })

  it('hides the ack button when the user cannot acknowledge', () => {
    renderTable([active], false)
    expect(screen.queryByRole('button', { name: 'Ack' })).not.toBeInTheDocument()
  })

  it('calls acknowledge with alarm id', async () => {
    const fetchMock = vi.fn(async () => new Response(JSON.stringify(active), { status: 200 }))
    vi.stubGlobal('fetch', fetchMock)
    renderTable([active], true)

    await userEvent.click(screen.getByRole('button', { name: 'Ack' }))

    await waitFor(() => expect(fetchMock).toHaveBeenCalled())
    const [url, init] = fetchMock.mock.calls[0] as unknown as [string, RequestInit]
    expect(url).toBe('/api/alarms/a-1/acknowledge')
    expect(init.method).toBe('POST')
  })
})
