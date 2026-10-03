import { useState } from 'react'
import { Link, useParams } from 'react-router'
import { TriangleAlert } from 'lucide-react'
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card'
import { cn } from '@/lib/utils'
import { AckButton, SeverityBadge } from '@/features/alarms/AlarmsTable'
import { useAlarms } from '@/features/alarms/api'
import { LiveParameters } from '@/features/dashboard/LiveParameters'
import { ApiError } from '@/shared/api/client'
import type { AssignmentDto, EquipmentStatus } from '@/shared/api/types'
import { useLatestReadings } from '@/shared/realtime/useLatestReadings'
import { StatusBadge } from '@/shared/ui/StatusBadge'
import { useAssignments, useEquipmentDetail, useTrackIn } from './api'
import { OpenRunPanel } from './OpenRunPanel'
import { TrackInPanel } from './TrackInPanel'

function AssignmentChoice({
  assignment,
  selected,
  onSelect,
}: {
  assignment: AssignmentDto
  selected: boolean
  onSelect: () => void
}) {
  const onHold = assignment.status === 'HOLD'
  return (
    <button
      type="button"
      role="radio"
      aria-checked={selected}
      disabled={onHold}
      onClick={onSelect}
      className={cn(
        'flex min-h-12 w-full flex-col gap-1 rounded-lg border bg-card p-3 text-left outline-none focus-visible:ring-3 focus-visible:ring-ring/50',
        selected && 'border-primary ring-2 ring-primary/30',
        onHold ? 'cursor-not-allowed opacity-60' : 'hover:bg-muted/50',
      )}
    >
      <span className="flex items-center justify-between gap-2">
        <span className="font-mono text-base font-semibold">{assignment.workOrderNumber}</span>
        <StatusBadge value={assignment.status} />
      </span>
      <span className="text-sm text-muted-foreground">
        {assignment.productCode} · {assignment.goodCount} / {assignment.targetQty} good
      </span>
      {onHold && <span className="text-sm text-amber-800">Work order is on hold. Ask the planner to resume it.</span>}
    </button>
  )
}

const BANNERS: Partial<Record<EquipmentStatus, string>> = {
  DOWN: 'Machine down — clear the alarm before the next track-in; you can still produce output and track out',
  MAINTENANCE: 'In maintenance — track-in is disabled',
}

function AlarmStrip({ equipmentCode }: { equipmentCode: string }) {
  const alarms = useAlarms({ active: true, equipment: equipmentCode })
  if (!alarms.data?.length) return null
  return (
    <ul aria-label="Active alarms" className="space-y-2">
      {alarms.data.map((alarm) => (
        <li key={alarm.id} className="flex flex-wrap items-center gap-3 rounded-lg border bg-card px-3 py-2">
          <SeverityBadge severity={alarm.severity} />
          <span className="font-mono text-sm">{alarm.code}</span>
          <span className="flex-1 text-sm">{alarm.message}</span>
          {alarm.acknowledgedAt ? (
            <span className="text-sm text-muted-foreground">Acknowledged by {alarm.acknowledgedBy}</span>
          ) : (
            <AckButton alarmId={alarm.id} />
          )}
        </li>
      ))}
    </ul>
  )
}

export function StationPage() {
  const { equipmentCode } = useParams()
  const equipment = useEquipmentDetail(equipmentCode)
  const assignments = useAssignments(equipmentCode)
  const trackIn = useTrackIn()
  const readings = useLatestReadings()
  const [selectedId, setSelectedId] = useState<string | null>(null)

  if (equipment.isError) {
    const notFound = equipment.error instanceof ApiError && equipment.error.status === 404
    return (
      <div className="space-y-2">
        <h1 className="text-2xl font-semibold">{notFound ? 'Equipment not found' : 'Could not load equipment'}</h1>
        <Link className="text-sm underline underline-offset-4" to="/station">
          Back to stations
        </Link>
      </div>
    )
  }
  if (!equipment.data) return <p className="text-sm text-muted-foreground">Loading...</p>

  const eqp = equipment.data
  const run = eqp.openRun
  const banner = BANNERS[eqp.status]
  const trackInBlocked = eqp.status === 'DOWN' || eqp.status === 'MAINTENANCE'
  const selected = assignments.data?.find((a) => a.workOrderOperationId === selectedId && a.status !== 'HOLD')

  return (
    <div className="space-y-6">
      <div className="space-y-3">
        <Link className="text-sm text-muted-foreground underline-offset-4 hover:underline" to="/station">
          Operator Station
        </Link>
        <div className="flex flex-wrap items-center gap-3">
          <h1 className="font-mono text-2xl font-semibold">{eqp.code}</h1>
          <StatusBadge value={eqp.status} />
          <span className="text-muted-foreground">{eqp.name}</span>
        </div>
      </div>

      {banner && (
        <div
          role="alert"
          className="flex items-start gap-2 rounded-lg border border-amber-300 bg-amber-50 p-3 text-base font-medium text-amber-900"
        >
          <TriangleAlert className="mt-0.5 size-5 shrink-0" aria-hidden="true" />
          <span>{banner}</span>
        </div>
      )}

      <AlarmStrip equipmentCode={eqp.code} />

      <Card>
        <CardHeader>
          <CardTitle className="text-lg">Live parameters</CardTitle>
        </CardHeader>
        <CardContent>
          <LiveParameters
            readings={readings.data?.filter((r) => r.equipmentCode === eqp.code) ?? []}
            status={eqp.status}
          />
        </CardContent>
      </Card>

      {run ? (
        <OpenRunPanel run={run} equipment={eqp} />
      ) : (
        <div className="space-y-6">
          <Card>
            <CardHeader>
              <CardTitle className="text-lg">1. Choose the work order</CardTitle>
            </CardHeader>
            <CardContent className="space-y-2">
              {assignments.isError && <p className="text-sm text-destructive">Could not load assignments.</p>}
              {assignments.isPending && <p className="text-sm text-muted-foreground">Loading...</p>}
              {assignments.data?.length === 0 && (
                <p className="text-sm text-muted-foreground">No work orders are assigned to this equipment.</p>
              )}
              <div role="radiogroup" aria-label="Work orders" className="space-y-2">
                {assignments.data?.map((assignment) => (
                  <AssignmentChoice
                    key={assignment.workOrderOperationId}
                    assignment={assignment}
                    selected={assignment.workOrderOperationId === selected?.workOrderOperationId}
                    onSelect={() => setSelectedId(assignment.workOrderOperationId)}
                  />
                ))}
              </div>
            </CardContent>
          </Card>

          {selected && (
            <Card>
              <CardHeader>
                <CardTitle className="text-lg">2. Scan inputs for {selected.workOrderNumber}</CardTitle>
              </CardHeader>
              <CardContent>
                <TrackInPanel
                  key={selected.workOrderOperationId}
                  operation={eqp.operation}
                  submitting={trackIn.isPending}
                  disabled={trackInBlocked}
                  onTrackIn={(inputs) =>
                    trackIn.mutate({
                      equipmentCode: eqp.code,
                      workOrderOperationId: selected.workOrderOperationId,
                      inputs,
                    })
                  }
                />
              </CardContent>
            </Card>
          )}
        </div>
      )}
    </div>
  )
}
