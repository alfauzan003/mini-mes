import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card'
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table'
import { useLotInspections } from '@/features/quality/api'
import { formatDateTime } from '@/features/work-orders/formatters'
import type { InspectionDto } from '@/shared/api/types'
import { StatusBadge } from '@/shared/ui/StatusBadge'

function InspectionCard({ inspection: i }: { inspection: InspectionDto }) {
  return (
    <Card>
      <CardHeader>
        <CardTitle className="flex flex-wrap items-center gap-3 text-base">
          <StatusBadge value={i.result} />
          <span className="text-sm font-normal text-muted-foreground">
            {i.inspector} · {formatDateTime(i.inspectedAt)} · {i.operation}
          </span>
        </CardTitle>
      </CardHeader>
      <CardContent className="space-y-3">
        {i.measurements.length > 0 && (
          <Table>
            <TableHeader>
              <TableRow>
                <TableHead>Item</TableHead>
                <TableHead>Value</TableHead>
                <TableHead>LSL – USL</TableHead>
                <TableHead>Judgment</TableHead>
              </TableRow>
            </TableHeader>
            <TableBody>
              {i.measurements.map((m) => (
                <TableRow key={m.itemName} className={m.judgment === 'NG' ? 'bg-red-50 font-medium' : undefined}>
                  <TableCell>{m.itemName}</TableCell>
                  <TableCell className="tabular-nums">
                    {m.value} {m.unit}
                  </TableCell>
                  <TableCell className="tabular-nums">
                    {m.lsl} – {m.usl}
                  </TableCell>
                  <TableCell>
                    <StatusBadge value={m.judgment} />
                  </TableCell>
                </TableRow>
              ))}
            </TableBody>
          </Table>
        )}
        {(i.defectCode || i.reason) && (
          <p className="text-sm">
            {i.defectCode && (
              <span title={i.defectDescription ?? undefined}>
                Defect: <span className="font-mono">{i.defectCode}</span>
              </span>
            )}
            {i.reason && <span className="ml-2 text-muted-foreground">Reason: {i.reason}</span>}
          </p>
        )}
        {i.disposition && (
          <p className="text-sm">
            Disposition: <span className="font-medium">{i.disposition}</span>
            {i.dispositionBy && ` by ${i.dispositionBy}`}
            {i.dispositionAt && ` · ${formatDateTime(i.dispositionAt)}`}
            {i.dispositionReason && <span className="ml-2 text-muted-foreground">{i.dispositionReason}</span>}
          </p>
        )}
      </CardContent>
    </Card>
  )
}

export function LotQualityTab({ lotId }: { lotId: string }) {
  const inspections = useLotInspections(lotId)

  if (inspections.isError) return <p className="text-sm text-destructive">Could not load inspections.</p>
  if (!inspections.data) return <p className="text-sm text-muted-foreground">Loading...</p>
  if (inspections.data.length === 0) return <p className="text-sm text-muted-foreground">No inspections yet.</p>

  // The server returns inspections newest first.
  return (
    <div className="space-y-4">
      {inspections.data.map((inspection) => (
        <InspectionCard key={inspection.id} inspection={inspection} />
      ))}
    </div>
  )
}
