import { useQuery } from '@tanstack/react-query'
import { apiFetch } from '@/shared/api/client'
import type { LiveReadingDto } from '@/shared/api/types'

/** Live updates keep this cache current, so it is never refetched on its own. */
export function useLatestReadings() {
  return useQuery({
    queryKey: ['readings', 'latest'],
    queryFn: () => apiFetch<LiveReadingDto[]>('/api/readings/latest'),
    staleTime: Infinity,
  })
}
