import { useState } from 'react'
import { Link, useNavigate } from 'react-router'
import { Button } from '@/components/ui/button'
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table'
import { Tabs, TabsList, TabsTrigger } from '@/components/ui/tabs'
import type { WorkOrderStatus } from '@/shared/api/types'
import { useAuth } from '@/shared/auth/AuthContext'
import { StatusBadge } from '@/shared/ui/StatusBadge'
import { useWorkOrders } from './api'
import { formatDateTime } from './formatters'
import { WorkOrderFormDialog } from './WorkOrderFormDialog'

type Filter = 'ALL' | WorkOrderStatus

const FILTERS: { value: Filter; label: string }[] = [
  { value: 'ALL', label: 'All' },
  { value: 'PLANNED', label: 'Planned' },
  { value: 'RELEASED', label: 'Released' },
  { value: 'RUNNING', label: 'Running' },
  { value: 'HOLD', label: 'Hold' },
  { value: 'COMPLETED', label: 'Completed' },
]

export function WorkOrderListPage() {
  const { user } = useAuth()
  const navigate = useNavigate()
  const [filter, setFilter] = useState<Filter>('ALL')
  const [creating, setCreating] = useState(false)
  const workOrders = useWorkOrders(filter === 'ALL' ? undefined : filter)
  const canPlan = user?.role === 'PLANNER' || user?.role === 'ADMIN'

  return (
    <div className="space-y-4">
      <div className="flex items-center justify-between gap-4">
        <h1 className="text-2xl font-semibold">Work Orders</h1>
        {canPlan && <Button onClick={() => setCreating(true)}>New Work Order</Button>}
      </div>

      <Tabs value={filter} onValueChange={(value) => setFilter(value as Filter)}>
        <TabsList>
          {FILTERS.map((f) => (
            <TabsTrigger key={f.value} value={f.value}>
              {f.label}
            </TabsTrigger>
          ))}
        </TabsList>
      </Tabs>

      {workOrders.isError ? (
        <p className="text-sm text-destructive">Could not load work orders.</p>
      ) : (
        <Table>
          <TableHeader>
            <TableRow>
              <TableHead>Number</TableHead>
              <TableHead>Product</TableHead>
              <TableHead>Status</TableHead>
              <TableHead>Good / Target</TableHead>
              <TableHead>Planned start</TableHead>
              <TableHead>Planned end</TableHead>
            </TableRow>
          </TableHeader>
          <TableBody>
            {workOrders.data?.map((wo) => (
              <TableRow key={wo.id}>
                <TableCell className="font-mono">
                  <Link className="underline-offset-4 hover:underline" to={`/work-orders/${wo.id}`}>
                    {wo.number}
                  </Link>
                </TableCell>
                <TableCell>{wo.productCode}</TableCell>
                <TableCell>
                  <StatusBadge value={wo.status} />
                </TableCell>
                <TableCell className="tabular-nums">
                  {wo.goodCount} / {wo.targetQty}
                </TableCell>
                <TableCell>{formatDateTime(wo.plannedStart)}</TableCell>
                <TableCell>{formatDateTime(wo.plannedEnd)}</TableCell>
              </TableRow>
            ))}
            {workOrders.data?.length === 0 && (
              <TableRow>
                <TableCell colSpan={6} className="text-center text-muted-foreground">
                  No work orders.
                </TableCell>
              </TableRow>
            )}
            {workOrders.isPending && (
              <TableRow>
                <TableCell colSpan={6} className="text-center text-muted-foreground">
                  Loading...
                </TableCell>
              </TableRow>
            )}
          </TableBody>
        </Table>
      )}

      {canPlan && (
        <WorkOrderFormDialog
          open={creating}
          onOpenChange={setCreating}
          onSaved={(wo) => navigate(`/work-orders/${wo.id}`)}
        />
      )}
    </div>
  )
}
