import { cn } from '@/lib/utils'
import { useRealtimeStatus, type RealtimeStatus } from './RealtimeProvider'

const LABELS: Record<RealtimeStatus, string> = {
  connected: 'Live',
  reconnecting: 'Reconnecting',
  disconnected: 'Offline',
}

const DOT: Record<RealtimeStatus, string> = {
  connected: 'bg-green-500',
  reconnecting: 'bg-amber-500 animate-pulse',
  disconnected: 'bg-red-500',
}

export function ConnectionIndicator() {
  const status = useRealtimeStatus()
  return (
    <div role="status" className="flex items-center gap-1.5 text-xs text-muted-foreground">
      <span aria-hidden className={cn('size-2 rounded-full', DOT[status])} />
      {LABELS[status]}
    </div>
  )
}
