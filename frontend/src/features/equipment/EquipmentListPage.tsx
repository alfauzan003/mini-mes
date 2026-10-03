import { useState } from 'react'
import { Link } from 'react-router'
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table'
import { useAlarms } from '@/features/alarms/api'
import { useEquipment } from '@/features/work-orders/api'
import type { OperationCode } from '@/shared/api/types'
import { NativeSelect } from '@/shared/ui/NativeSelect'
import { StatusBadge } from '@/shared/ui/StatusBadge'

const OPERATIONS: OperationCode[] = ['MIX', 'COAT', 'CAL', 'SLIT']

export function EquipmentListPage() {
  const [operation, setOperation] = useState<OperationCode | ''>('')
  const equipment = useEquipment(operation || undefined)
  const alarms = useAlarms({ active: true })

  return (
    <div className="space-y-4">
      <h1 className="text-2xl font-semibold">Equipment</h1>
      <div className="w-48">
        <NativeSelect
          aria-label="Operation"
          value={operation}
          onChange={(event) => setOperation(event.target.value as OperationCode | '')}
        >
          <option value="">All operations</option>
          {OPERATIONS.map((o) => (
            <option key={o} value={o}>
              {o}
            </option>
          ))}
        </NativeSelect>
      </div>
      {equipment.isError && <p className="text-sm text-destructive">Could not load equipment.</p>}
      {equipment.isPending && <p className="text-sm text-muted-foreground">Loading...</p>}
      {equipment.data?.length === 0 && <p className="text-sm text-muted-foreground">No equipment found.</p>}
      {equipment.data && equipment.data.length > 0 && (
        <Table>
          <TableHeader>
            <TableRow>
              <TableHead>Code</TableHead>
              <TableHead>Name</TableHead>
              <TableHead>Operation</TableHead>
              <TableHead>Status</TableHead>
              <TableHead>Work order</TableHead>
              <TableHead className="text-right">Active alarms</TableHead>
            </TableRow>
          </TableHeader>
          <TableBody>
            {equipment.data.map((e) => (
              <TableRow key={e.code}>
                <TableCell>
                  <Link to={`/equipment/${e.code}`} className="font-mono underline-offset-4 hover:underline">
                    {e.code}
                  </Link>
                </TableCell>
                <TableCell>{e.name}</TableCell>
                <TableCell>{e.operation}</TableCell>
                <TableCell>
                  <StatusBadge value={e.status} />
                </TableCell>
                <TableCell className="font-mono">{e.openRun?.workOrderNumber ?? '-'}</TableCell>
                <TableCell className="text-right tabular-nums">
                  {alarms.data ? alarms.data.filter((a) => a.equipmentCode === e.code).length : '-'}
                </TableCell>
              </TableRow>
            ))}
          </TableBody>
        </Table>
      )}
    </div>
  )
}
