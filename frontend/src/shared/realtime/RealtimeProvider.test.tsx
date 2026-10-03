import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { act, render, screen } from '@testing-library/react'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { RealtimeProvider, useRealtimeStatus } from './RealtimeProvider'

const hub = vi.hoisted(() => ({
  start: vi.fn(),
  stop: vi.fn(),
  retryPolicy: undefined as undefined | { nextRetryDelayInMilliseconds(ctx: { previousRetryCount: number }): number | null },
  closeHandler: undefined as undefined | (() => void),
  state: 'Disconnected',
}))
const auth = vi.hoisted(() => ({ user: { username: 'u' } as { username: string } | null }))

vi.mock('@/shared/auth/AuthContext', () => ({ useAuth: () => ({ user: auth.user }) }))
vi.mock('@microsoft/signalr', () => {
  const connection = {
    on: vi.fn(),
    off: vi.fn(),
    onreconnecting: vi.fn(),
    onreconnected: vi.fn(),
    onclose: (h: () => void) => {
      hub.closeHandler = h
    },
    start: () => hub.start(),
    stop: () => hub.stop(),
    get state() {
      return hub.state
    },
  }
  const builder = {
    withUrl: () => builder,
    withHubProtocol: () => builder,
    configureLogging: () => builder,
    withAutomaticReconnect: (policy: typeof hub.retryPolicy) => {
      hub.retryPolicy = policy
      return builder
    },
    build: () => connection,
  }
  return {
    HubConnectionBuilder: function () {
      return builder
    },
    JsonHubProtocol: function () {},
    LogLevel: { Warning: 3 },
    HubConnectionState: { Disconnected: 'Disconnected' },
  }
})

function Status() {
  return <div data-testid="status">{useRealtimeStatus()}</div>
}

function renderProvider() {
  const queryClient = new QueryClient()
  const invalidate = vi.spyOn(queryClient, 'invalidateQueries')
  const ui = () => (
    <QueryClientProvider client={queryClient}>
      <RealtimeProvider>
        <Status />
      </RealtimeProvider>
    </QueryClientProvider>
  )
  const view = render(ui())
  return { ...view, invalidate, rerenderUi: () => view.rerender(ui()) }
}

const status = () => screen.getByTestId('status').textContent

describe('RealtimeProvider', () => {
  beforeEach(() => {
    vi.useFakeTimers()
    auth.user = { username: 'u' }
    hub.start.mockReset().mockResolvedValue(undefined)
    hub.stop.mockReset().mockResolvedValue(undefined)
    hub.state = 'Disconnected'
  })
  afterEach(() => vi.useRealTimers())

  it('retries a failed initial start until connected, then refreshes the caches', async () => {
    hub.start.mockRejectedValueOnce(new Error('down')).mockRejectedValueOnce(new Error('down')).mockResolvedValue(undefined)
    const { invalidate } = renderProvider()

    await act(async () => {})
    expect(status()).toBe('reconnecting')
    expect(hub.start).toHaveBeenCalledTimes(1)

    await act(async () => {
      await vi.advanceTimersByTimeAsync(1000)
    })
    expect(hub.start).toHaveBeenCalledTimes(2)
    expect(status()).toBe('reconnecting')

    await act(async () => {
      await vi.advanceTimersByTimeAsync(2000)
    })
    expect(hub.start).toHaveBeenCalledTimes(3)
    expect(status()).toBe('connected')
    expect(invalidate).toHaveBeenCalledWith({ queryKey: ['alarms'] })
  })

  it('never gives up reconnecting', () => {
    renderProvider()
    const delay = (n: number) => hub.retryPolicy?.nextRetryDelayInMilliseconds({ previousRetryCount: n })
    expect(delay(0)).toBe(1000)
    expect(delay(50)).toBe(30_000)
  })

  it('restarts the connection when it is closed', async () => {
    hub.start.mockResolvedValue(undefined)
    renderProvider()
    await act(async () => {})
    expect(status()).toBe('connected')

    act(() => hub.closeHandler?.())
    expect(status()).toBe('reconnecting')
    await act(async () => {
      await vi.advanceTimersByTimeAsync(1000)
    })
    expect(hub.start).toHaveBeenCalledTimes(2)
    expect(status()).toBe('connected')
  })

  it('stops retrying after unmount', async () => {
    hub.start.mockRejectedValue(new Error('down'))
    const { unmount } = renderProvider()
    await act(async () => {})
    unmount()
    await act(async () => {
      await vi.advanceTimersByTimeAsync(60_000)
    })
    expect(hub.start).toHaveBeenCalledTimes(1)
  })

  it('stops the connection and goes offline on logout', async () => {
    hub.start.mockResolvedValue(undefined)
    hub.state = 'Connected'
    const { rerenderUi } = renderProvider()
    await act(async () => {})
    expect(status()).toBe('connected')

    auth.user = null
    rerenderUi()
    expect(hub.stop).toHaveBeenCalledTimes(1)
    expect(status()).toBe('disconnected')
  })
})
