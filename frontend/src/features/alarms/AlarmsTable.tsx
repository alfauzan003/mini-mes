import { Link } from 'react-router'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table'
import { cn } from '@/lib/utils'
import type { AlarmDto, AlarmSeverity } from '@/shared/api/types'
import { formatDuration } from '@/shared/format/duration'
import { useAcknowledgeAlarm } from './api'

export const SEVERITY_CLASSES: Record<AlarmSeverity, string> = {
  CRITICAL: 'border-red-200 bg-red-100 text-red-800',
  MAJOR: 'border-amber-200 bg-amber-100 text-amber-900',
  WARNING: 'border-slate-300 bg-slate-100 text-slate-700',
}

export function SeverityBadge({ severity }: { severity: AlarmSeverity }) {
  return (
    <Badge variant="outline" className={cn('font-medium', SEVERITY_CLASSES[severity])}>
      {severity}
    </Badge>
  )
}

/** Cleared alarms keep their final duration; active ones grow with `now` (epoch ms). */
export function alarmDurationSeconds(alarm: AlarmDto, now: number): number {
  const end = alarm.clearedAt ? Date.parse(alarm.clearedAt) : now
  return Math.max(0, (end - Date.parse(alarm.raisedAt)) / 1000)
}

export function AckButton({ alarmId }: { alarmId: string }) {
  const acknowledge = useAcknowledgeAlarm()
  return (
    <Button
      size="sm"
      variant="outline"
      disabled={acknowledge.isPending}
      // The global mutation error toast reports failures.
      onClick={() => acknowledge.mutate(alarmId)}
    >
      Ack
    </Button>
  )
}

const formatTime = (iso: string) => new Date(iso).toLocaleString()

interface AlarmsTableProps {
  alarms: AlarmDto[]
  canAcknowledge: boolean
  now: number
  emptyText?: string
}

export function AlarmsTable({ alarms, canAcknowledge, now, emptyText = 'No alarms.' }: AlarmsTableProps) {
  const columns = canAcknowledge ? 9 : 8
  return (
    <Table>
      <TableHeader>
        <TableRow>
          <TableHead>Severity</TableHead>
          <TableHead>Equipment</TableHead>
          <TableHead>Code</TableHead>
          <TableHead>Message</TableHead>
          <TableHead>Raised at</TableHead>
          <TableHead>Duration</TableHead>
          <TableHead>Acknowledged by</TableHead>
          <TableHead>Acknowledged at</TableHead>
          {canAcknowledge && <TableHead className="text-right">Action</TableHead>}
        </TableRow>
      </TableHeader>
      <TableBody>
        {alarms.map((alarm) => (
          <TableRow key={alarm.id}>
            <TableCell>
              <SeverityBadge severity={alarm.severity} />
            </TableCell>
            <TableCell className="font-mono">
              <Link className="underline-offset-4 hover:underline" to={`/equipment/${alarm.equipmentCode}`}>
                {alarm.equipmentCode}
              </Link>
            </TableCell>
            <TableCell className="font-mono">{alarm.code}</TableCell>
            <TableCell>{alarm.message}</TableCell>
            <TableCell className="whitespace-nowrap tabular-nums">{formatTime(alarm.raisedAt)}</TableCell>
            <TableCell className="tabular-nums">{formatDuration(alarmDurationSeconds(alarm, now))}</TableCell>
            <TableCell>{alarm.acknowledgedBy ?? '-'}</TableCell>
            <TableCell className="whitespace-nowrap tabular-nums">
              {alarm.acknowledgedAt ? formatTime(alarm.acknowledgedAt) : '-'}
            </TableCell>
            {canAcknowledge && (
              <TableCell className="text-right">{!alarm.acknowledgedAt && <AckButton alarmId={alarm.id} />}</TableCell>
            )}
          </TableRow>
        ))}
        {alarms.length === 0 && (
          <TableRow>
            <TableCell colSpan={columns} className="text-center text-muted-foreground">
              {emptyText}
            </TableCell>
          </TableRow>
        )}
      </TableBody>
    </Table>
  )
}
