import { useState } from 'react'
import { Link, useParams } from 'react-router'
import { Button } from '@/components/ui/button'
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table'
import { ApiError } from '@/shared/api/client'
import type { WorkOrderStatus } from '@/shared/api/types'
import { useAuth } from '@/shared/auth/AuthContext'
import { StatusBadge } from '@/shared/ui/StatusBadge'
import { useProducts, useWorkOrder, useWorkOrderAction, useWorkOrderLots, type WorkOrderAction } from './api'
import { formatDateTime, progressPercent } from './formatters'
import { WorkOrderFormDialog } from './WorkOrderFormDialog'

const ACTIONS: { action: WorkOrderAction; label: string; from: WorkOrderStatus[] }[] = [
  { action: 'release', label: 'Release', from: ['PLANNED'] },
  { action: 'hold', label: 'Hold', from: ['RELEASED', 'RUNNING'] },
  { action: 'resume', label: 'Resume', from: ['HOLD'] },
  { action: 'complete', label: 'Complete', from: ['RUNNING'] },
]

export function WorkOrderDetailPage() {
  const { id } = useParams()
  const { user } = useAuth()
  const workOrder = useWorkOrder(id)
  const lots = useWorkOrderLots(workOrder.data?.number)
  const products = useProducts()
  const transition = useWorkOrderAction(id ?? '')
  const [editing, setEditing] = useState(false)
  const canPlan = user?.role === 'PLANNER' || user?.role === 'ADMIN'

  if (workOrder.isError) {
    const notFound = workOrder.error instanceof ApiError && workOrder.error.status === 404
    return (
      <div className="space-y-2">
        <h1 className="text-2xl font-semibold">{notFound ? 'Work order not found' : 'Could not load work order'}</h1>
        <Link className="text-sm underline underline-offset-4" to="/work-orders">
          Back to work orders
        </Link>
      </div>
    )
  }
  if (!workOrder.data) return <p className="text-sm text-muted-foreground">Loading...</p>

  const wo = workOrder.data
  const percent = progressPercent(wo.goodCount, wo.targetQty)
  const route = products.data?.find((p) => p.code === wo.productCode)?.route ?? []
  const uomOf = (operation: string) => route.find((step) => step.operation === operation)?.uom ?? ''
  const availableActions = canPlan ? ACTIONS.filter((a) => a.from.includes(wo.status)) : []
  const canEdit = canPlan && wo.status === 'PLANNED'

  return (
    <div className="space-y-6">
      <div className="space-y-3">
        <Link className="text-sm text-muted-foreground underline-offset-4 hover:underline" to="/work-orders">
          Work Orders
        </Link>
        <div className="flex flex-wrap items-center justify-between gap-3">
          <div className="flex items-center gap-3">
            <h1 className="font-mono text-2xl font-semibold">{wo.number}</h1>
            <StatusBadge value={wo.status} />
          </div>
          <div className="flex gap-2">
            {canEdit && (
              <Button variant="outline" onClick={() => setEditing(true)}>
                Edit
              </Button>
            )}
            {availableActions.map((a) => (
              <Button
                key={a.action}
                variant={a.action === 'release' || a.action === 'complete' ? 'default' : 'outline'}
                disabled={transition.isPending}
                onClick={() => transition.mutate(a.action)}
              >
                {a.label}
              </Button>
            ))}
          </div>
        </div>
        <p className="text-sm text-muted-foreground">
          {wo.productCode} - {wo.productName} · {formatDateTime(wo.plannedStart)} to {formatDateTime(wo.plannedEnd)}
        </p>
        <div className="space-y-1">
          <div className="flex justify-between text-sm">
            <span>Progress</span>
            <span className="tabular-nums">
              {wo.goodCount} / {wo.targetQty}
            </span>
          </div>
          <div
            role="progressbar"
            aria-label="Good count progress"
            aria-valuemin={0}
            aria-valuemax={wo.targetQty}
            aria-valuenow={wo.goodCount}
            className="h-2 overflow-hidden rounded-full bg-muted"
          >
            <div className="h-full bg-primary transition-[width]" style={{ width: `${percent}%` }} />
          </div>
        </div>
      </div>

      <section className="space-y-2">
        <h2 className="text-lg font-medium">Operations</h2>
        <Table>
          <TableHeader>
            <TableRow>
              <TableHead>Seq</TableHead>
              <TableHead>Operation</TableHead>
              <TableHead>Equipment</TableHead>
              <TableHead>Runs</TableHead>
              <TableHead>Output</TableHead>
            </TableRow>
          </TableHeader>
          <TableBody>
            {[...wo.operations]
              .sort((a, b) => a.seq - b.seq)
              .map((op) => (
                <TableRow key={op.id}>
                  <TableCell>{op.seq}</TableCell>
                  <TableCell>{op.operation}</TableCell>
                  <TableCell className="font-mono">{op.equipmentCode}</TableCell>
                  <TableCell className="tabular-nums">{op.runCount}</TableCell>
                  <TableCell className="tabular-nums">
                    {op.outputQty} {uomOf(op.operation)}
                  </TableCell>
                </TableRow>
              ))}
          </TableBody>
        </Table>
      </section>

      <section className="space-y-2">
        <h2 className="text-lg font-medium">Lots</h2>
        <Table>
          <TableHeader>
            <TableRow>
              <TableHead>Lot</TableHead>
              <TableHead>Type</TableHead>
              <TableHead>Qty</TableHead>
              <TableHead>Status</TableHead>
              <TableHead>Operation</TableHead>
            </TableRow>
          </TableHeader>
          <TableBody>
            {lots.data?.map((lot) => (
              <TableRow key={lot.lotId}>
                <TableCell className="font-mono">
                  <Link className="underline-offset-4 hover:underline" to={`/lots/${lot.lotId}`}>
                    {lot.lotId}
                  </Link>
                </TableCell>
                <TableCell>{lot.type}</TableCell>
                <TableCell className="tabular-nums">
                  {lot.qty} {lot.uom}
                </TableCell>
                <TableCell>
                  <StatusBadge value={lot.status} />
                </TableCell>
                <TableCell>{lot.currentOperation ?? lot.nextOperation ?? '-'}</TableCell>
              </TableRow>
            ))}
            {lots.data?.length === 0 && (
              <TableRow>
                <TableCell colSpan={5} className="text-center text-muted-foreground">
                  No lots yet.
                </TableCell>
              </TableRow>
            )}
          </TableBody>
        </Table>
      </section>

      {canEdit && <WorkOrderFormDialog open={editing} onOpenChange={setEditing} workOrder={wo} />}
    </div>
  )
}
