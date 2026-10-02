import { useMutation, useQuery, useQueryClient, type QueryClient } from '@tanstack/react-query'
import { apiFetch } from '@/shared/api/client'
import type {
  AssignmentDto,
  CarrierDto,
  EquipmentDto,
  ProduceOutputRequest,
  RunDto,
  TrackInRequest,
  TrackOutRequest,
} from '@/shared/api/types'

export function useEquipmentDetail(code: string | undefined) {
  return useQuery({
    queryKey: ['equipment', code],
    queryFn: () => apiFetch<EquipmentDto>(`/api/equipment/${encodeURIComponent(code ?? '')}`),
    enabled: !!code,
  })
}

export function useAssignments(code: string | undefined) {
  return useQuery({
    queryKey: ['equipment', code, 'assignments'],
    queryFn: () => apiFetch<AssignmentDto[]>(`/api/equipment/${encodeURIComponent(code ?? '')}/assignments`),
    enabled: !!code,
  })
}

/** Empty carriers of one type (BB = big box for electrode rolls, PC = pancake carrier). */
export function useEmptyCarriers(type: 'BB' | 'PC' | undefined) {
  return useQuery({
    queryKey: ['carriers', { type, status: 'EMPTY' }],
    queryFn: () => apiFetch<CarrierDto[]>(`/api/carriers?type=${type}&status=EMPTY`),
    enabled: !!type,
  })
}

/**
 * Every run command answers with the updated run. Writing it straight into the equipment cache closes the
 * window between the response and the refetch, so the UI cannot offer a second Produce on stale data.
 */
function applyRun(queryClient: QueryClient, run: RunDto) {
  queryClient.setQueryData<EquipmentDto>(['equipment', run.equipmentCode], (equipment) =>
    equipment ? { ...equipment, openRun: run.endedAt ? null : run } : equipment,
  )
  return Promise.all(
    [['equipment'], ['lots'], ['work-orders'], ['carriers']].map((queryKey) =>
      queryClient.invalidateQueries({ queryKey }),
    ),
  )
}

export function useTrackIn() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (request: TrackInRequest) => apiFetch<RunDto>('/api/runs/track-in', { method: 'POST', json: request }),
    onSuccess: (run) => applyRun(queryClient, run),
  })
}

export function useProduce(runId: string) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (request: ProduceOutputRequest) =>
      apiFetch<RunDto>(`/api/runs/${runId}/outputs`, { method: 'POST', json: request }),
    onSuccess: (run) => applyRun(queryClient, run),
  })
}

export function useTrackOut(runId: string) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (request: TrackOutRequest) =>
      apiFetch<RunDto>(`/api/runs/${runId}/track-out`, { method: 'POST', json: request }),
    onSuccess: (run) => applyRun(queryClient, run),
  })
}
