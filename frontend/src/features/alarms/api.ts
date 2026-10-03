import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { apiFetch } from '@/shared/api/client'
import type { AlarmDto, AlarmSeverity } from '@/shared/api/types'

export interface AlarmFilter {
  active?: boolean
  equipment?: string
  severity?: AlarmSeverity
  from?: string
  to?: string
}

function alarmQuery(filter: AlarmFilter): string {
  const params = new URLSearchParams()
  if (filter.active !== undefined) params.set('active', String(filter.active))
  if (filter.equipment) params.set('equipment', filter.equipment)
  if (filter.severity) params.set('severity', filter.severity)
  if (filter.from) params.set('from', filter.from)
  if (filter.to) params.set('to', filter.to)
  const query = params.toString()
  return query ? `/api/alarms?${query}` : '/api/alarms'
}

export function useAlarms(filter: AlarmFilter = {}) {
  return useQuery({
    queryKey: ['alarms', filter],
    queryFn: () => apiFetch<AlarmDto[]>(alarmQuery(filter)),
  })
}

export function useAcknowledgeAlarm() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (id: string) => apiFetch<AlarmDto>(`/api/alarms/${id}/acknowledge`, { method: 'POST' }),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['alarms'] }),
  })
}
