import { Link } from 'react-router'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table'
import { cn } from '@/lib/utils'
import type { AlarmDto, AlarmSeverity } from '@/shared/api/types'
import { formatDuration } from '@/shared/format/duration'
import { useAcknowledgeAlarm } from './api'
import { alarmDurationSeconds, SEVERITY_CLASSES } from './severity'

export function SeverityBadge({ severity }: { severity: AlarmSeverity }) {
  return (
    <Badge variant="outline" className={cn('font-medium', SEVERITY_CLASSES[severity])}>
      {severity}
    </Badge>
  )
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
