import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table'
import { formatDateTime } from '@/features/work-orders/formatters'
import type { LotEventDto } from '@/shared/api/types'
import { StatusBadge } from '@/shared/ui/StatusBadge'

/** Lot history, oldest first as the API returns it. */
export function LotTimeline({ events }: { events: LotEventDto[] }) {
  return (
    <Table>
      <TableHeader>
        <TableRow>
          <TableHead>Time</TableHead>
          <TableHead>Event</TableHead>
          <TableHead>Operation</TableHead>
          <TableHead>Equipment</TableHead>
          <TableHead>Carrier</TableHead>
          <TableHead>User</TableHead>
          <TableHead>Qty</TableHead>
          <TableHead>Note</TableHead>
        </TableRow>
      </TableHeader>
      <TableBody>
        {events.map((event) => (
          <TableRow key={event.id}>
            <TableCell className="whitespace-nowrap">{formatDateTime(event.occurredAt)}</TableCell>
            <TableCell>
              <StatusBadge value={event.type} />
            </TableCell>
            <TableCell>{event.operation ?? '-'}</TableCell>
            <TableCell className="font-mono">{event.equipment ?? '-'}</TableCell>
            <TableCell className="font-mono">{event.carrier ?? '-'}</TableCell>
            <TableCell>{event.user}</TableCell>
            <TableCell className="tabular-nums">{event.qty ?? '-'}</TableCell>
            <TableCell className="whitespace-normal">{event.note ?? ''}</TableCell>
          </TableRow>
        ))}
        {events.length === 0 && (
          <TableRow>
            <TableCell colSpan={8} className="text-center text-muted-foreground">
              No events.
            </TableCell>
          </TableRow>
        )}
      </TableBody>
    </Table>
  )
}
