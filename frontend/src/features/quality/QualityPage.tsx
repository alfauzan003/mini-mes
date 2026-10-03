import { useState } from 'react'
import { Link } from 'react-router'
import { Button } from '@/components/ui/button'
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table'
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/tabs'
import { useLots } from '@/features/lots/api'
import type { InspectionDto, LotDto } from '@/shared/api/types'
import { useAuth } from '@/shared/auth/AuthContext'
import { StatusBadge } from '@/shared/ui/StatusBadge'
import { useInspectionQueue, useLotInspections } from './api'
import { DispositionDialog } from './DispositionDialog'

/** Compact elapsed time since an ISO timestamp: "5m", "3h 10m", "2d 4h". */
function formatAge(iso: string, now = Date.now()): string {
  const minutes = Math.max(0, Math.floor((now - new Date(iso).getTime()) / 60_000))
  if (minutes < 60) return `${minutes}m`
  const hours = Math.floor(minutes / 60)
  if (hours < 24) return `${hours}h ${minutes % 60}m`
  return `${Math.floor(hours / 24)}d ${hours % 24}h`
}

/** The server returns a lot's inspections newest first. */
function latestInspection(inspections: InspectionDto[] | undefined): InspectionDto | undefined {
  return inspections?.[0]
}

function LotLink({ lotId }: { lotId: string }) {
  return (
    <Link className="underline-offset-4 hover:underline" to={`/lots/${lotId}`}>
      {lotId}
    </Link>
  )
}

function QueueTable({ canInspect }: { canInspect: boolean }) {
  const queue = useInspectionQueue()
  const columns = canInspect ? 9 : 8

  if (queue.isError) return <p className="text-sm text-destructive">Could not load the inspection queue.</p>

  return (
    <Table>
      <TableHeader>
        <TableRow>
          <TableHead>Lot</TableHead>
          <TableHead>Type</TableHead>
          <TableHead>Product</TableHead>
          <TableHead>Work order</TableHead>
          <TableHead>Completed operation</TableHead>
          <TableHead>Qty</TableHead>
          <TableHead>Carrier</TableHead>
          <TableHead>Age</TableHead>
          {canInspect && <TableHead className="text-right">Action</TableHead>}
        </TableRow>
      </TableHeader>
      <TableBody>
        {queue.data?.map((lot) => (
          <TableRow key={lot.lotId}>
            <TableCell className="font-mono">
              <LotLink lotId={lot.lotId} />
            </TableCell>
            <TableCell>{lot.type}</TableCell>
            <TableCell className="font-mono">{lot.productCode ?? '-'}</TableCell>
            <TableCell className="font-mono">{lot.workOrderNumber ?? '-'}</TableCell>
            <TableCell>{lot.currentOperation ?? '-'}</TableCell>
            <TableCell className="tabular-nums">
              {lot.qty} {lot.uom}
            </TableCell>
            <TableCell className="font-mono">{lot.currentCarrier ?? '-'}</TableCell>
            <TableCell className="tabular-nums">{formatAge(lot.createdAt)}</TableCell>
            {canInspect && (
              <TableCell className="text-right">
                <Button size="sm" asChild>
                  <Link to={`/quality/inspect/${lot.lotId}`}>Inspect</Link>
                </Button>
              </TableCell>
            )}
          </TableRow>
        ))}
        {queue.data?.length === 0 && (
          <TableRow>
            <TableCell colSpan={columns} className="text-center text-muted-foreground">
              Nothing waiting for inspection.
            </TableCell>
          </TableRow>
        )}
        {queue.isPending && (
          <TableRow>
            <TableCell colSpan={columns} className="text-center text-muted-foreground">
              Loading...
            </TableCell>
          </TableRow>
        )}
      </TableBody>
    </Table>
  )
}

/** The latest defect is fetched per held lot; holds are few, so there is no need for a batch endpoint. */
function HoldRow({ lot, canDispose, onDispose }: { lot: LotDto; canDispose: boolean; onDispose: (lot: LotDto) => void }) {
  const inspections = useLotInspections(lot.lotId)
  const latest = latestInspection(inspections.data)

  return (
    <TableRow>
      <TableCell className="font-mono">
        <LotLink lotId={lot.lotId} />
      </TableCell>
      <TableCell>{lot.type}</TableCell>
      <TableCell>{lot.currentOperation ?? '-'}</TableCell>
      <TableCell className="tabular-nums">
        {lot.qty} {lot.uom}
      </TableCell>
      <TableCell>
        <StatusBadge value={lot.quality} />
      </TableCell>
      <TableCell>
        {latest?.defectCode ? (
          <span title={latest.defectDescription ?? undefined}>
            <span className="font-mono">{latest.defectCode}</span>
            {latest.reason && <span className="ml-2 text-muted-foreground">{latest.reason}</span>}
          </span>
        ) : (
          '-'
        )}
      </TableCell>
      {canDispose && (
        <TableCell className="text-right">
          <Button size="sm" variant="outline" onClick={() => onDispose(lot)}>
            Disposition
          </Button>
        </TableCell>
      )}
    </TableRow>
  )
}

function HoldTable({ canDispose }: { canDispose: boolean }) {
  const held = useLots({ status: 'HOLD' })
  const [selected, setSelected] = useState<LotDto | null>(null)
  const columns = canDispose ? 7 : 6

  if (held.isError) return <p className="text-sm text-destructive">Could not load held lots.</p>

  return (
    <>
      <Table>
        <TableHeader>
          <TableRow>
            <TableHead>Lot</TableHead>
            <TableHead>Type</TableHead>
            <TableHead>Operation</TableHead>
            <TableHead>Qty</TableHead>
            <TableHead>Quality</TableHead>
            <TableHead>Latest defect</TableHead>
            {canDispose && <TableHead className="text-right">Action</TableHead>}
          </TableRow>
        </TableHeader>
        <TableBody>
          {held.data?.map((lot) => (
            <HoldRow key={lot.lotId} lot={lot} canDispose={canDispose} onDispose={setSelected} />
          ))}
          {held.data?.length === 0 && (
            <TableRow>
              <TableCell colSpan={columns} className="text-center text-muted-foreground">
                No lots on hold.
              </TableCell>
            </TableRow>
          )}
          {held.isPending && (
            <TableRow>
              <TableCell colSpan={columns} className="text-center text-muted-foreground">
                Loading...
              </TableCell>
            </TableRow>
          )}
        </TableBody>
      </Table>
      {canDispose && selected && (
        <DispositionDialog lot={selected} open onOpenChange={(open) => !open && setSelected(null)} />
      )}
    </>
  )
}

export function QualityPage() {
  const { user } = useAuth()
  const canAct = user?.role === 'QC' || user?.role === 'ADMIN'

  return (
    <div className="space-y-4">
      <div className="flex items-center justify-between gap-4">
        <h1 className="text-2xl font-semibold">Quality</h1>
        <Button variant="outline" asChild>
          <Link to="/quality/specs">Spec limits</Link>
        </Button>
      </div>

      <Tabs defaultValue="queue">
        <TabsList>
          <TabsTrigger value="queue">Inspection queue</TabsTrigger>
          <TabsTrigger value="hold">On hold</TabsTrigger>
        </TabsList>
        <TabsContent value="queue">
          <QueueTable canInspect={canAct} />
        </TabsContent>
        <TabsContent value="hold">
          <HoldTable canDispose={canAct} />
        </TabsContent>
      </Tabs>
    </div>
  )
}
