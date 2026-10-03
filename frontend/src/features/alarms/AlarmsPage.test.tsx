import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter } from 'react-router'
import { afterEach, describe, expect, it, vi } from 'vitest'
import type { AlarmDto } from '@/shared/api/types'
import { AlarmsPage } from './AlarmsPage'

vi.mock('@/shared/auth/AuthContext', () => ({
  useAuth: () => ({ user: { username: 'op', displayName: 'Op', role: 'OPERATOR' } }),
}))

const activeAlarm: AlarmDto = {
  id: 'a-1',
  equipmentCode: 'CT01',
  code: 'TEMP_HIGH',
  message: 'Oven temperature too high',
  severity: 'CRITICAL',
  raisedAt: '2026-10-03T01:00:00Z',
  clearedAt: null,
  durationSeconds: null,
  acknowledgedBy: 'operator',
  acknowledgedAt: '2026-10-03T01:01:00Z',
}

describe('AlarmsPage', () => {
  afterEach(() => vi.unstubAllGlobals())

  it('lists active alarms in History and does not filter by active', async () => {
    const urls: string[] = []
    vi.stubGlobal(
      'fetch',
      vi.fn(async (input: RequestInfo | URL) => {
        const url = String(input)
        urls.push(url)
        return new Response(JSON.stringify(url.startsWith('/api/alarms') ? [activeAlarm] : []), { status: 200 })
      }),
    )
    const client = new QueryClient({ defaultOptions: { queries: { retry: false } } })
    render(
      <QueryClientProvider client={client}>
        <MemoryRouter>
          <AlarmsPage />
        </MemoryRouter>
      </QueryClientProvider>,
    )

    await userEvent.click(screen.getByRole('tab', { name: 'History' }))

    await waitFor(() => expect(urls).toContain('/api/alarms'))
    expect(urls.filter((u) => u.startsWith('/api/alarms') && u.includes('active=false'))).toHaveLength(0)
    expect((await screen.findAllByText('Oven temperature too high')).length).toBeGreaterThan(0)
  })
})
