import { Link } from 'react-router'
import { Badge } from '@/components/ui/badge'
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card'
import { useAlarms } from '@/features/alarms/api'
import { useEquipment, useWorkOrders } from '@/features/work-orders/api'
import { cn } from '@/lib/utils'
import type { AlarmDto, AlarmSeverity, OperationCode } from '@/shared/api/types'
import { formatDuration } from '@/shared/format/duration'
import { useLatestReadings } from '@/shared/realtime/useLatestReadings'
import { useNow } from '@/shared/realtime/useNow'
import { StatusBadge } from '@/shared/ui/StatusBadge'
import { EquipmentTile } from './EquipmentTile'

const OPERATIONS: { code: OperationCode; label: string }[] = [
  { code: 'MIX', label: 'Mixing' },
  { code: 'COAT', label: 'Coating' },
  { code: 'CAL', label: 'Calendering' },
  { code: 'SLIT', label: 'Slitting' },
]

const SEVERITY_CLASSES: Record<AlarmSeverity, string> = {
  WARNING: 'border-amber-200 bg-amber-100 text-amber-900',
  MAJOR: 'border-orange-200 bg-orange-100 text-orange-900',
  CRITICAL: 'border-red-200 bg-red-100 text-red-800',
}

function ActiveAlarmRow({ alarm, now }: { alarm: AlarmDto; now: number }) {
  const seconds = (now - Date.parse(alarm.raisedAt)) / 1000
  return (
    <li className="space-y-1 border-b pb-2 last:border-b-0 last:pb-0">
      <div className="flex items-center justify-between gap-2">
        <Badge variant="outline" className={SEVERITY_CLASSES[alarm.severity]}>
          {alarm.severity}
        </Badge>
        <span className="font-mono text-sm">{alarm.equipmentCode}</span>
      </div>
      <p className="text-sm">{alarm.message}</p>
      <p className="text-xs text-muted-foreground">Active for {formatDuration(seconds)}</p>
    </li>
  )
}

function ActiveAlarmsPanel() {
  const alarms = useAlarms({ active: true })
  const now = useNow()

  return (
    <Card>
      <CardHeader>
        <CardTitle>Active alarms</CardTitle>
      </CardHeader>
      <CardContent className="space-y-3">
        {alarms.isError && <p className="text-sm text-destructive">Could not load alarms.</p>}
        {alarms.data?.length === 0 && <p className="text-sm text-muted-foreground">No active alarms.</p>}
        <ul className="space-y-2">
          {alarms.data?.map((alarm) => <ActiveAlarmRow key={alarm.id} alarm={alarm} now={now} />)}
        </ul>
        <Link to="/alarms" className="text-sm underline-offset-4 hover:underline">
          All alarms
        </Link>
      </CardContent>
    </Card>
  )
}

function RunningWorkOrdersPanel() {
  const workOrders = useWorkOrders()
  const running = workOrders.data?.filter((wo) => wo.status === 'RUNNING' || wo.status === 'HOLD') ?? []

  return (
    <Card>
      <CardHeader>
        <CardTitle>Running work orders</CardTitle>
      </CardHeader>
      <CardContent className="space-y-3">
        {workOrders.isError && <p className="text-sm text-destructive">Could not load work orders.</p>}
        {workOrders.data && running.length === 0 && (
          <p className="text-sm text-muted-foreground">No running work orders.</p>
        )}
        <ul className="space-y-3">
          {running.map((wo) => {
            const percent = wo.targetQty > 0 ? Math.min(100, Math.round((wo.goodCount / wo.targetQty) * 100)) : 0
            return (
              <li key={wo.id} className="space-y-1">
                <div className="flex items-center justify-between gap-2">
                  <Link to={`/work-orders/${wo.id}`} className="font-mono text-sm underline-offset-4 hover:underline">
                    {wo.number}
                  </Link>
                  <StatusBadge value={wo.status} />
                </div>
                <p className="text-xs text-muted-foreground">{wo.productCode}</p>
                <div
                  role="progressbar"
                  aria-label={`${wo.number} progress`}
                  aria-valuemin={0}
                  aria-valuemax={wo.targetQty}
                  aria-valuenow={wo.goodCount}
                  className="h-2 overflow-hidden rounded-full bg-muted"
                >
                  <div className={cn('h-full bg-blue-500')} style={{ width: `${percent}%` }} />
                </div>
                <p className="text-xs tabular-nums text-muted-foreground">
                  {wo.goodCount}/{wo.targetQty}
                </p>
              </li>
            )
          })}
        </ul>
      </CardContent>
    </Card>
  )
}

export function DashboardPage() {
  const equipment = useEquipment()
  const readings = useLatestReadings()
  const alarms = useAlarms({ active: true })

  return (
    <div className="space-y-6">
      <h1 className="text-2xl font-semibold">Dashboard</h1>
      <div className="grid gap-6 lg:grid-cols-[1fr_20rem]">
        <div className="space-y-6">
          {equipment.isError && <p className="text-sm text-destructive">Could not load equipment.</p>}
          {equipment.isPending && <p className="text-sm text-muted-foreground">Loading...</p>}
          {OPERATIONS.map(({ code, label }) => {
            const items = equipment.data?.filter((e) => e.operation === code) ?? []
            if (items.length === 0) return null
            return (
              <section key={code} aria-label={label} className="space-y-2">
                <h2 className="text-lg font-medium">{label}</h2>
                <div className="grid gap-3 sm:grid-cols-2 xl:grid-cols-3">
                  {items.map((e) => (
                    <EquipmentTile
                      key={e.code}
                      equipment={e}
                      readings={readings.data?.filter((r) => r.equipmentCode === e.code) ?? []}
                      activeAlarmCount={alarms.data?.filter((a) => a.equipmentCode === e.code).length ?? 0}
                    />
                  ))}
                </div>
              </section>
            )
          })}
        </div>
        <div className="space-y-6">
          <ActiveAlarmsPanel />
          <RunningWorkOrdersPanel />
        </div>
      </div>
    </div>
  )
}
