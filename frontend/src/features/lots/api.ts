import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { apiFetch } from '@/shared/api/client'
import type {
  GenealogyGraph,
  LotDto,
  LotEventDto,
  LotStatus,
  LotType,
  MaterialDto,
  OperationCode,
  RegisterMaterialRequest,
} from '@/shared/api/types'

export interface LotFilters {
  status?: LotStatus
  type?: LotType
  nextOperation?: OperationCode
  /** Case-insensitive prefix on lot ID or current carrier code. */
  search?: string
}

export function lotsQuery(filters: LotFilters) {
  const params = new URLSearchParams()
  for (const [key, value] of Object.entries(filters)) {
    if (value) params.set(key, value)
  }
  const query = params.toString()
  return {
    queryKey: ['lots', filters] as const,
    queryFn: () => apiFetch<LotDto[]>(query ? `/api/lots?${query}` : '/api/lots'),
  }
}

export function useLots(filters: LotFilters) {
  return useQuery(lotsQuery(filters))
}

export function useLot(lotId: string | undefined) {
  return useQuery({
    queryKey: ['lots', lotId],
    queryFn: () => apiFetch<LotDto>(`/api/lots/${encodeURIComponent(lotId ?? '')}`),
    enabled: !!lotId,
  })
}

export function useLotEvents(lotId: string | undefined) {
  return useQuery({
    queryKey: ['lots', lotId, 'events'],
    queryFn: () => apiFetch<LotEventDto[]>(`/api/lots/${encodeURIComponent(lotId ?? '')}/events`),
    enabled: !!lotId,
  })
}

export function useGenealogy(lotId: string | undefined, direction: 'backward' | 'forward') {
  return useQuery({
    queryKey: ['lots', lotId, 'genealogy', direction],
    queryFn: () =>
      apiFetch<GenealogyGraph>(`/api/lots/${encodeURIComponent(lotId ?? '')}/genealogy?direction=${direction}`),
    enabled: !!lotId,
  })
}

export function useMaterials() {
  return useQuery({ queryKey: ['materials'], queryFn: () => apiFetch<MaterialDto[]>('/api/materials') })
}

export function useRegisterMaterial() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (request: RegisterMaterialRequest) =>
      apiFetch<LotDto>('/api/lots/materials', { method: 'POST', json: request }),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['lots'] }),
  })
}
