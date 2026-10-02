import { MutationCache, QueryClient } from '@tanstack/react-query'
import { toast } from 'sonner'
import { ApiError } from './client'

/** Toasts the error message, with the machine-readable error code in monospace underneath. */
export function showApiError(error: unknown): void {
  if (error instanceof ApiError) {
    toast.error(error.message, {
      description: error.code ?? undefined,
      classNames: { description: 'font-mono' },
    })
  } else {
    toast.error(error instanceof Error ? error.message : 'Something went wrong')
  }
}

export const queryClient = new QueryClient({
  defaultOptions: {
    queries: {
      staleTime: 5_000,
      // A 4xx will not change on retry; only retry transient failures.
      retry: (failureCount, error) =>
        !(error instanceof ApiError && error.status < 500) && failureCount < 2,
    },
  },
  mutationCache: new MutationCache({ onError: showApiError }),
})
