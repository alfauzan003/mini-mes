import { useState } from 'react'
import { Link } from 'react-router'
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table'
import type { CarrierStatus } from '@/shared/api/types'
import { NativeSelect } from '@/shared/ui/NativeSelect'
import { StatusBadge } from '@/shared/ui/StatusBadge'
import { useCarriers } from './api'

const CARRIER_TYPES = [
  { value: 'BB', label: 'BB (big box)' },
  { value: 'PC', label: 'PC (pancake carrier)' },
] as const

export function CarrierListPage() {
  const [type, setType] = useState<'BB' | 'PC' | ''>('')
  const [status, setStatus] = useState<CarrierStatus | ''>('')
  const carriers = useCarriers({ type: type || undefined, status: status || undefined })

  return (
    <div className="space-y-4">
      <h1 className="text-2xl font-semibold">Carriers</h1>

      <div className="flex flex-wrap gap-3">
        <NativeSelect
          aria-label="Type"
          className="w-52"
          value={type}
          onChange={(event) => setType(event.target.value as 'BB' | 'PC' | '')}
        >
          <option value="">All types</option>
          {CARRIER_TYPES.map((t) => (
            <option key={t.value} value={t.value}>
              {t.label}
            </option>
          ))}
        </NativeSelect>
        <NativeSelect
          aria-label="Status"
          className="w-40"
          value={status}
          onChange={(event) => setStatus(event.target.value as CarrierStatus | '')}
        >
          <option value="">All statuses</option>
          <option value="EMPTY">EMPTY</option>
          <option value="FULL">FULL</option>
        </NativeSelect>
      </div>

      {carriers.isError ? (
        <p className="text-sm text-destructive">Could not load carriers.</p>
      ) : (
        <Table>
          <TableHeader>
            <TableRow>
              <TableHead>Code</TableHead>
              <TableHead>Type</TableHead>
              <TableHead>Status</TableHead>
              <TableHead>Lot</TableHead>
            </TableRow>
          </TableHeader>
          <TableBody>
            {carriers.data?.map((carrier) => (
              <TableRow key={carrier.code}>
                <TableCell className="font-mono">{carrier.code}</TableCell>
                <TableCell>{carrier.type}</TableCell>
                <TableCell>
                  <StatusBadge value={carrier.status} />
                </TableCell>
                <TableCell className="font-mono">
                  {carrier.lotId ? (
                    <Link className="underline-offset-4 hover:underline" to={`/lots/${carrier.lotId}`}>
                      {carrier.lotId}
                    </Link>
                  ) : (
                    '-'
                  )}
                </TableCell>
              </TableRow>
            ))}
            {carriers.data?.length === 0 && (
              <TableRow>
                <TableCell colSpan={4} className="text-center text-muted-foreground">
                  No carriers.
                </TableCell>
              </TableRow>
            )}
            {carriers.isPending && (
              <TableRow>
                <TableCell colSpan={4} className="text-center text-muted-foreground">
                  Loading...
                </TableCell>
              </TableRow>
            )}
          </TableBody>
        </Table>
      )}
    </div>
  )
}
