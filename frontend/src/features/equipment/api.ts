import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { apiFetch } from '@/shared/api/client'
import type { EquipmentStatusLogDto, LiveReadingDto, ParameterSeriesDto } from '@/shared/api/types'

const MINUTE_MS = 60_000

/** Parameter history for the last rangeMinutes (the API rejects anything over 24 h). */
export function useParameterSeries(code: string | undefined, rangeMinutes: number) {
  return useQuery({
    queryKey: ['parameters', code, rangeMinutes],
    queryFn: () => {
      const to = Date.now()
      const from = to - rangeMinutes * MINUTE_MS
      const query = `from=${encodeURIComponent(new Date(from).toISOString())}&to=${encodeURIComponent(new Date(to).toISOString())}`
      return apiFetch<ParameterSeriesDto[]>(`/api/equipment/${encodeURIComponent(code ?? '')}/parameters?${query}`)
    },
    enabled: !!code,
    staleTime: 15_000,
  })
}

export function useStatusLog(code: string | undefined, limit = 50) {
  return useQuery({
    queryKey: ['equipment', code, 'status-log', limit],
    queryFn: () =>
      apiFetch<EquipmentStatusLogDto[]>(`/api/equipment/${encodeURIComponent(code ?? '')}/status-log?limit=${limit}`),
    enabled: !!code,
  })
}

/** Live readings of one machine, appended by the real-time provider. Never fetched. */
export function useRecentReadings(code: string | undefined) {
  return useQuery<LiveReadingDto[]>({
    queryKey: ['readings', 'recent', code],
    queryFn: () => [],
    initialData: [],
    staleTime: Infinity,
    enabled: !!code,
  })
}

function useMachineAction(code: string, action: 'maintenance/start' | 'maintenance/end' | 'inject-fault') {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: () => apiFetch<void>(`/api/equipment/${encodeURIComponent(code)}/${action}`, { method: 'POST' }),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['equipment'] }),
  })
}

export const useStartMaintenance = (code: string) => useMachineAction(code, 'maintenance/start')
export const useEndMaintenance = (code: string) => useMachineAction(code, 'maintenance/end')
export const useInjectFault = (code: string) => useMachineAction(code, 'inject-fault')
