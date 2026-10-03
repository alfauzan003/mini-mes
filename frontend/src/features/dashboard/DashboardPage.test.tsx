import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { render, screen } from '@testing-library/react'
import { MemoryRouter } from 'react-router'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { DashboardPage } from './DashboardPage'

const WO = {
  id: 'wo-1',
  number: 'WO-261003-001',
  productCode: 'CAT-A',
  status: 'RUNNING',
  targetQty: 0,
  goodCount: 5,
}

describe('DashboardPage running work orders', () => {
  beforeEach(() => {
    vi.stubGlobal(
      'fetch',
      vi.fn(async (url: string) =>
        new Response(JSON.stringify(url.startsWith('/api/work-orders') ? [WO] : []), { status: 200 }),
      ),
    )
  })
  afterEach(() => vi.unstubAllGlobals())

  it('renders 0% and a valid progressbar when target is 0', async () => {
    render(
      <QueryClientProvider client={new QueryClient({ defaultOptions: { queries: { retry: false } } })}>
        <MemoryRouter>
          <DashboardPage />
        </MemoryRouter>
      </QueryClientProvider>,
    )

    const bar = await screen.findByRole('progressbar', { name: 'WO-261003-001 progress' })
    expect(bar).toHaveAttribute('aria-valuenow', '0')
    expect(bar).toHaveAttribute('aria-valuemax', '0')
    expect(bar.firstElementChild).toHaveStyle({ width: '0%' })
  })
})
