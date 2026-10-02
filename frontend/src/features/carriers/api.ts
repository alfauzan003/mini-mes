import { useQuery } from '@tanstack/react-query'
import { apiFetch } from '@/shared/api/client'
import type { CarrierDto, CarrierStatus } from '@/shared/api/types'

export interface CarrierFilters {
  type?: 'BB' | 'PC'
  status?: CarrierStatus
}

export function useCarriers(filters: CarrierFilters) {
  return useQuery({
    queryKey: ['carriers', filters],
    queryFn: () => {
      const params = new URLSearchParams()
      if (filters.type) params.set('type', filters.type)
      if (filters.status) params.set('status', filters.status)
      const query = params.toString()
      return apiFetch<CarrierDto[]>(query ? `/api/carriers?${query}` : '/api/carriers')
    },
  })
}
