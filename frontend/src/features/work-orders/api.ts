import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { apiFetch } from '@/shared/api/client'
import type {
  CreateWorkOrderRequest,
  EquipmentDto,
  LotDto,
  OperationCode,
  ProductDto,
  UpdateWorkOrderRequest,
  WorkOrderDto,
  WorkOrderStatus,
} from '@/shared/api/types'

export type WorkOrderAction = 'release' | 'hold' | 'resume' | 'complete'

export function useWorkOrders(status?: WorkOrderStatus) {
  return useQuery({
    queryKey: ['work-orders', { status: status ?? null }],
    queryFn: () => apiFetch<WorkOrderDto[]>(status ? `/api/work-orders?status=${status}` : '/api/work-orders'),
  })
}

export function useWorkOrder(id: string | undefined) {
  return useQuery({
    queryKey: ['work-orders', id],
    queryFn: () => apiFetch<WorkOrderDto>(`/api/work-orders/${id}`),
    enabled: !!id,
  })
}

/** Lots of one work order. The API filters by the work order number (WO-...), not its id. */
export function useWorkOrderLots(workOrderNumber: string | undefined) {
  return useQuery({
    queryKey: ['lots', { workOrder: workOrderNumber }],
    queryFn: () => apiFetch<LotDto[]>(`/api/lots?workOrder=${encodeURIComponent(workOrderNumber ?? '')}`),
    enabled: !!workOrderNumber,
  })
}

export function useProducts() {
  return useQuery({ queryKey: ['products'], queryFn: () => apiFetch<ProductDto[]>('/api/products') })
}

export function useEquipment(operation?: OperationCode) {
  return useQuery({
    queryKey: ['equipment', { operation: operation ?? null }],
    queryFn: () => apiFetch<EquipmentDto[]>(operation ? `/api/equipment?operation=${operation}` : '/api/equipment'),
  })
}

export function useCreateWorkOrder() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (request: CreateWorkOrderRequest) =>
      apiFetch<WorkOrderDto>('/api/work-orders', { method: 'POST', json: request }),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['work-orders'] }),
  })
}

export function useUpdateWorkOrder(id: string) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (request: UpdateWorkOrderRequest) =>
      apiFetch<WorkOrderDto>(`/api/work-orders/${id}`, { method: 'PUT', json: request }),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['work-orders'] }),
  })
}

export function useWorkOrderAction(id: string) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (action: WorkOrderAction) => apiFetch<WorkOrderDto>(`/api/work-orders/${id}/${action}`, { method: 'POST' }),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['work-orders'] }),
  })
}
