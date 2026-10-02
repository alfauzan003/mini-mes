import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { AUTH_STORAGE_KEY } from '@/shared/auth/authStorage'
import { ApiError, apiFetch, setUnauthorizedHandler } from './client'

function problem(status: number, body: object): Response {
  return new Response(JSON.stringify(body), {
    status,
    headers: { 'Content-Type': 'application/problem+json' },
  })
}

describe('apiFetch', () => {
  const fetchMock = vi.fn()

  beforeEach(() => {
    localStorage.clear()
    fetchMock.mockReset()
    vi.stubGlobal('fetch', fetchMock)
  })

  afterEach(() => {
    setUnauthorizedHandler(null)
    vi.unstubAllGlobals()
  })

  it('parses problem details into ApiError with code', async () => {
    fetchMock.mockResolvedValue(
      problem(422, { title: 'Rule violation', detail: 'Lot is not waiting', errorCode: 'LOT_NOT_AVAILABLE' }),
    )

    const error = await apiFetch('/api/runs/track-in', { method: 'POST', json: {} }).catch((e: unknown) => e)

    expect(error).toBeInstanceOf(ApiError)
    expect(error).toMatchObject({ status: 422, code: 'LOT_NOT_AVAILABLE', message: 'Lot is not waiting' })
  })

  it('calls unauthorized handler on 401', async () => {
    const handler = vi.fn()
    setUnauthorizedHandler(handler)
    fetchMock.mockResolvedValue(problem(401, { title: 'Unauthorized', errorCode: 'AUTH_REQUIRED' }))

    await expect(apiFetch('/api/auth/me')).rejects.toMatchObject({ status: 401, code: 'AUTH_REQUIRED' })

    expect(handler).toHaveBeenCalledTimes(1)
  })

  it('sends bearer token when logged in', async () => {
    localStorage.setItem(
      AUTH_STORAGE_KEY,
      JSON.stringify({
        token: 'abc.def.ghi',
        user: { username: 'planner', displayName: 'Demo Planner', role: 'PLANNER' },
        expiresAt: new Date(Date.now() + 60_000).toISOString(),
      }),
    )
    fetchMock.mockResolvedValue(new Response('[]', { status: 200 }))

    await apiFetch('/api/work-orders', { method: 'POST', json: { a: 1 } })

    const [path, init] = fetchMock.mock.calls[0] as [string, RequestInit]
    const headers = init.headers as Headers
    expect(path).toBe('/api/work-orders')
    expect(headers.get('Authorization')).toBe('Bearer abc.def.ghi')
    expect(headers.get('Content-Type')).toBe('application/json')
    expect(init.body).toBe('{"a":1}')
  })

  it('resolves undefined on 204', async () => {
    fetchMock.mockResolvedValue(new Response(null, { status: 204 }))

    await expect(apiFetch('/api/work-orders/1/release', { method: 'POST' })).resolves.toBeUndefined()
  })
})
