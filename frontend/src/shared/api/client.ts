import { readAuth } from '@/shared/auth/authStorage'
import type { ProblemDetails } from './types'

export class ApiError extends Error {
  status: number
  code: string | null

  constructor(message: string, status: number, code: string | null = null) {
    super(message)
    this.name = 'ApiError'
    this.status = status
    this.code = code
  }
}

let unauthorizedHandler: (() => void) | null = null

/** Registers the callback invoked whenever the API answers 401. Pass null to unregister. */
export function setUnauthorizedHandler(handler: (() => void) | null): void {
  unauthorizedHandler = handler
}

type ApiInit = RequestInit & { json?: unknown }

async function toApiError(response: Response): Promise<ApiError> {
  let problem: ProblemDetails | null = null
  try {
    problem = (await response.json()) as ProblemDetails
  } catch {
    // Body missing or not JSON: fall back to a generic message below.
  }
  return new ApiError(
    problem?.detail ?? problem?.title ?? `Request failed (${response.status})`,
    response.status,
    problem?.errorCode ?? null,
  )
}

export async function apiFetch<T>(path: string, init: ApiInit = {}): Promise<T> {
  const { json, headers: initHeaders, body, ...rest } = init
  const headers = new Headers(initHeaders)
  headers.set('Accept', 'application/json')
  if (json !== undefined) headers.set('Content-Type', 'application/json')

  const token = readAuth()?.token
  if (token) headers.set('Authorization', `Bearer ${token}`)

  const response = await fetch(path, {
    ...rest,
    headers,
    body: json !== undefined ? JSON.stringify(json) : body,
  })

  if (!response.ok) {
    if (response.status === 401) unauthorizedHandler?.()
    throw await toApiError(response)
  }

  if (response.status === 204) return undefined as T
  const text = await response.text()
  return (text ? JSON.parse(text) : undefined) as T
}
