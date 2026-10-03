import { useState } from 'react'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/tabs'
import { useEquipment } from '@/features/work-orders/api'
import type { AlarmSeverity } from '@/shared/api/types'
import { useAuth } from '@/shared/auth/AuthContext'
import { useNow } from '@/shared/realtime/useNow'
import { NativeSelect } from '@/shared/ui/NativeSelect'
import { useAlarms } from './api'
import { AlarmsTable } from './AlarmsTable'

const SEVERITIES: AlarmSeverity[] = ['CRITICAL', 'MAJOR', 'WARNING']

/** Local start of the picked day, as an instant the API accepts. */
const startOfDay = (date: string) => (date ? new Date(`${date}T00:00:00`).toISOString() : undefined)
const endOfDay = (date: string) => (date ? new Date(`${date}T23:59:59.999`).toISOString() : undefined)

function ActiveTab({ canAcknowledge, now }: { canAcknowledge: boolean; now: number }) {
  const alarms = useAlarms({ active: true })
  if (alarms.isError) return <p className="text-sm text-destructive">Could not load alarms.</p>
  if (alarms.isPending) return <p className="text-sm text-muted-foreground">Loading...</p>
  return <AlarmsTable alarms={alarms.data} canAcknowledge={canAcknowledge} now={now} emptyText="No active alarms." />
}

function HistoryTab({ canAcknowledge, now }: { canAcknowledge: boolean; now: number }) {
  const equipment = useEquipment()
  const [equipmentCode, setEquipmentCode] = useState('')
  const [severity, setSeverity] = useState<AlarmSeverity | ''>('')
  const [from, setFrom] = useState('')
  const [to, setTo] = useState('')
  const badRange = from !== '' && to !== '' && from > to

  // An inverted range is a 400 from the API, so hold the previous filter's results out and explain instead.
  const alarms = useAlarms({
    active: false,
    equipment: equipmentCode || undefined,
    severity: severity || undefined,
    from: badRange ? undefined : startOfDay(from),
    to: badRange ? undefined : endOfDay(to),
  })

  return (
    <div className="space-y-4">
      <div className="flex flex-wrap items-end gap-3">
        <div className="w-44 space-y-1.5">
          <Label htmlFor="alarm-equipment">Equipment</Label>
          <NativeSelect id="alarm-equipment" value={equipmentCode} onChange={(e) => setEquipmentCode(e.target.value)}>
            <option value="">All equipment</option>
            {equipment.data?.map((e) => (
              <option key={e.code} value={e.code}>
                {e.code}
              </option>
            ))}
          </NativeSelect>
        </div>
        <div className="w-40 space-y-1.5">
          <Label htmlFor="alarm-severity">Severity</Label>
          <NativeSelect id="alarm-severity" value={severity} onChange={(e) => setSeverity(e.target.value as AlarmSeverity | '')}>
            <option value="">All severities</option>
            {SEVERITIES.map((s) => (
              <option key={s} value={s}>
                {s}
              </option>
            ))}
          </NativeSelect>
        </div>
        <div className="space-y-1.5">
          <Label htmlFor="alarm-from">From</Label>
          <Input id="alarm-from" type="date" value={from} max={to || undefined} onChange={(e) => setFrom(e.target.value)} />
        </div>
        <div className="space-y-1.5">
          <Label htmlFor="alarm-to">To</Label>
          <Input id="alarm-to" type="date" value={to} min={from || undefined} onChange={(e) => setTo(e.target.value)} />
        </div>
      </div>
      {badRange && (
        <p role="alert" className="text-sm text-destructive">
          The start date must not be after the end date.
        </p>
      )}
      {alarms.isError && <p className="text-sm text-destructive">Could not load alarms.</p>}
      {alarms.isPending && <p className="text-sm text-muted-foreground">Loading...</p>}
      {alarms.data && !badRange && (
        <AlarmsTable alarms={alarms.data} canAcknowledge={canAcknowledge} now={now} emptyText="No alarms match these filters." />
      )}
    </div>
  )
}

export function AlarmsPage() {
  const { user } = useAuth()
  const canAcknowledge = user?.role === 'OPERATOR' || user?.role === 'ADMIN'
  const now = useNow()

  return (
    <div className="space-y-4">
      <h1 className="text-2xl font-semibold">Alarms</h1>
      <Tabs defaultValue="active">
        <TabsList>
          <TabsTrigger value="active">Active</TabsTrigger>
          <TabsTrigger value="history">History</TabsTrigger>
        </TabsList>
        <TabsContent value="active">
          <ActiveTab canAcknowledge={canAcknowledge} now={now} />
        </TabsContent>
        <TabsContent value="history">
          <HistoryTab canAcknowledge={canAcknowledge} now={now} />
        </TabsContent>
      </Tabs>
    </div>
  )
}
