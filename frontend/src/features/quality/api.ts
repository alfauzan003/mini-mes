import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { apiFetch } from '@/shared/api/client'
import type {
  DefectCodeDto,
  DispositionRequest,
  HoldLotRequest,
  InspectionDto,
  InspectionSpecDto,
  LotDto,
  OperationCode,
  RecordInspectionRequest,
  UpdateSpecLimitsRequest,
} from '@/shared/api/types'

function withQuery(path: string, params: Record<string, string | undefined>): string {
  const query = new URLSearchParams()
  for (const [key, value] of Object.entries(params)) {
    if (value) query.set(key, value)
  }
  const text = query.toString()
  return text ? `${path}?${text}` : path
}

export function useSpecs(product?: string, operation?: OperationCode) {
  return useQuery({
    queryKey: ['specs', { product, operation }],
    queryFn: () => apiFetch<InspectionSpecDto[]>(withQuery('/api/specs', { product, operation })),
  })
}

export function useDefectCodes(operation?: OperationCode) {
  return useQuery({
    queryKey: ['defect-codes', { operation }],
    queryFn: () => apiFetch<DefectCodeDto[]>(withQuery('/api/defect-codes', { operation })),
  })
}

/** Lots waiting for a first inspection of their current operation, oldest first. */
export function useInspectionQueue() {
  return useQuery({
    queryKey: ['inspections', 'queue'],
    queryFn: () => apiFetch<LotDto[]>('/api/inspections/queue'),
  })
}

export function useLotInspections(lotId: string | undefined) {
  return useQuery({
    queryKey: ['inspections', lotId],
    queryFn: () => apiFetch<InspectionDto[]>(`/api/lots/${encodeURIComponent(lotId ?? '')}/inspections`),
    enabled: !!lotId,
  })
}

export function useUpdateSpec() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: ({ id, ...request }: UpdateSpecLimitsRequest & { id: string }) =>
      apiFetch<InspectionSpecDto>(`/api/specs/${id}`, { method: 'PUT', json: request }),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['specs'] }),
  })
}

/** A recorded inspection, hold or disposition moves the lot, its work order and possibly its carrier. */
function useInvalidateQuality() {
  const queryClient = useQueryClient()
  return () =>
    Promise.all(
      [['inspections'], ['lots'], ['work-orders'], ['carriers']].map((queryKey) =>
        queryClient.invalidateQueries({ queryKey }),
      ),
    )
}

export function useRecordInspection(lotId: string) {
  const invalidate = useInvalidateQuality()
  return useMutation({
    mutationFn: (request: RecordInspectionRequest) =>
      apiFetch<InspectionDto>(`/api/lots/${encodeURIComponent(lotId)}/inspections`, { method: 'POST', json: request }),
    onSuccess: invalidate,
  })
}

export function useHoldLot(lotId: string) {
  const invalidate = useInvalidateQuality()
  return useMutation({
    mutationFn: (request: HoldLotRequest) =>
      apiFetch<LotDto>(`/api/lots/${encodeURIComponent(lotId)}/hold`, { method: 'POST', json: request }),
    onSuccess: invalidate,
  })
}

export function useDisposition(lotId: string) {
  const invalidate = useInvalidateQuality()
  return useMutation({
    mutationFn: (request: DispositionRequest) =>
      apiFetch<LotDto>(`/api/lots/${encodeURIComponent(lotId)}/disposition`, { method: 'POST', json: request }),
    onSuccess: invalidate,
  })
}
