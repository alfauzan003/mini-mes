import { HubConnectionBuilder, HubConnectionState, JsonHubProtocol, LogLevel } from '@microsoft/signalr'
import { useQueryClient } from '@tanstack/react-query'
import { createContext, useContext, useEffect, useState, type ReactNode } from 'react'
import { toast } from 'sonner'
import type { AlarmDto, LiveReadingDto } from '@/shared/api/types'
import { useAuth } from '@/shared/auth/AuthContext'
import { readAuth } from '@/shared/auth/authStorage'
import { appendRecent, applyReading } from './readings'

export type RealtimeStatus = 'connected' | 'reconnecting' | 'disconnected'

const RealtimeContext = createContext<RealtimeStatus>('disconnected')

/** The hub connection state; 'disconnected' outside a RealtimeProvider. */
export function useRealtimeStatus(): RealtimeStatus {
  return useContext(RealtimeContext)
}

const RECONNECT_REFRESH_KEYS = [
  ['equipment'],
  ['lots'],
  ['work-orders'],
  ['alarms'],
  ['inspections'],
  ['readings', 'latest'],
]

/** Capped exponential backoff: 1 s, 2 s, 4 s ... 30 s, never giving up. */
function retryDelayMs(previousRetryCount: number): number {
  return Math.min(30_000, 1000 * 2 ** previousRetryCount)
}

export function RealtimeProvider({ children }: { children: ReactNode }) {
  const { user } = useAuth()
  const queryClient = useQueryClient()
  const [status, setStatus] = useState<RealtimeStatus>('disconnected')
  const signedIn = user !== null

  useEffect(() => {
    if (!signedIn) return

    // `cancelled` makes a StrictMode mount/unmount/mount cycle leave exactly one live connection.
    let cancelled = false
    const connection = new HubConnectionBuilder()
      .withUrl('/hubs/shopfloor', { accessTokenFactory: () => readAuth()?.token ?? '' })
      .withHubProtocol(new JsonHubProtocol())
      .withAutomaticReconnect({ nextRetryDelayInMilliseconds: (ctx) => retryDelayMs(ctx.previousRetryCount) })
      .configureLogging(LogLevel.Warning)
      .build()

    const invalidate = (...keys: string[]) => {
      for (const key of keys) void queryClient.invalidateQueries({ queryKey: [key] })
    }

    connection.on('EquipmentStatusChanged', () => invalidate('equipment'))
    connection.on('LotChanged', () => invalidate('lots', 'inspections'))
    connection.on('WorkOrderProgressed', () => invalidate('work-orders'))
    connection.on('AlarmRaised', (alarm: AlarmDto) => {
      invalidate('alarms')
      if (alarm.severity === 'CRITICAL') toast.error(`${alarm.equipmentCode}: ${alarm.message}`)
    })
    connection.on('AlarmCleared', () => invalidate('alarms'))
    connection.on('AlarmAcknowledged', () => invalidate('alarms'))
    connection.on('ParameterReading', (reading: LiveReadingDto) => {
      queryClient.setQueryData<LiveReadingDto[]>(['readings', 'latest'], (prev) => applyReading(prev, reading))
      queryClient.setQueryData<LiveReadingDto[]>(['readings', 'recent', reading.equipmentCode], (prev) =>
        appendRecent(prev, reading, Date.now()),
      )
    })

    const refreshAll = () => {
      // Events may have been missed while disconnected.
      for (const queryKey of RECONNECT_REFRESH_KEYS) void queryClient.invalidateQueries({ queryKey })
    }

    // Retries start() with capped backoff until it succeeds or the effect is cleaned up.
    let retryTimer: ReturnType<typeof setTimeout> | undefined
    let failedAttempts = 0
    let missedEvents = false
    const scheduleStart = () => {
      missedEvents = true
      setStatus('reconnecting')
      retryTimer = setTimeout(attemptStart, retryDelayMs(failedAttempts++))
    }
    const attemptStart = () => {
      retryTimer = undefined
      if (cancelled) return
      connection
        .start()
        .then(() => {
          if (cancelled) return
          failedAttempts = 0
          setStatus('connected')
          if (missedEvents) refreshAll()
          missedEvents = false
        })
        .catch(() => {
          if (!cancelled) scheduleStart()
        })
    }

    connection.onreconnecting(() => setStatus('reconnecting'))
    connection.onreconnected(() => {
      setStatus('connected')
      refreshAll()
    })
    connection.onclose(() => {
      if (cancelled) return
      // The server closed the connection or reconnecting was abandoned: start over.
      failedAttempts = 0
      scheduleStart()
    })

    setStatus('reconnecting')
    attemptStart()

    return () => {
      cancelled = true
      clearTimeout(retryTimer)
      for (const method of [
        'EquipmentStatusChanged',
        'LotChanged',
        'WorkOrderProgressed',
        'AlarmRaised',
        'AlarmCleared',
        'AlarmAcknowledged',
        'ParameterReading',
      ]) {
        connection.off(method)
      }
      if (connection.state !== HubConnectionState.Disconnected) void connection.stop()
      setStatus('disconnected')
    }
  }, [signedIn, queryClient])

  return <RealtimeContext.Provider value={status}>{children}</RealtimeContext.Provider>
}
