import { useQueryClient } from '@tanstack/react-query'
import { useState, type FormEvent } from 'react'
import { Link, useNavigate } from 'react-router'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table'
import type { LotStatus, LotType, OperationCode } from '@/shared/api/types'
import { useAuth } from '@/shared/auth/AuthContext'
import { NativeSelect } from '@/shared/ui/NativeSelect'
import { StatusBadge } from '@/shared/ui/StatusBadge'
import { lotsQuery, useLots } from './api'
import { RegisterMaterialDialog } from './RegisterMaterialDialog'

const TYPES: LotType[] = ['RAW', 'FOIL', 'SLURRY', 'ELECTRODE', 'PANCAKE']
const STATUSES: LotStatus[] = ['WAIT', 'RUN', 'HOLD', 'CONSUMED', 'SCRAPPED', 'FINISHED']
const OPERATIONS: OperationCode[] = ['MIX', 'COAT', 'CAL', 'SLIT']

export function LotListPage() {
  const { user } = useAuth()
  const navigate = useNavigate()
  const queryClient = useQueryClient()
  const [search, setSearch] = useState('')
  const [type, setType] = useState<LotType | ''>('')
  const [status, setStatus] = useState<LotStatus | ''>('')
  const [nextOperation, setNextOperation] = useState<OperationCode | ''>('')
  const [registering, setRegistering] = useState(false)
  const canRegister = user?.role === 'PLANNER' || user?.role === 'ADMIN'

  const term = search.trim()
  const lots = useLots({
    search: term || undefined,
    type: type || undefined,
    status: status || undefined,
    nextOperation: nextOperation || undefined,
  })

  /** Enter on an exact lot ID or carrier code opens that lot; anything else just keeps the filtered list. */
  async function handleSearch(event: FormEvent) {
    event.preventDefault()
    if (!term) return
    const candidates = await queryClient.fetchQuery(lotsQuery({ search: term }))
    const wanted = term.toUpperCase()
    const match = candidates.find(
      (lot) => lot.lotId.toUpperCase() === wanted || lot.currentCarrier?.toUpperCase() === wanted,
    )
    if (match) navigate(`/lots/${match.lotId}`)
  }

  return (
    <div className="space-y-4">
      <div className="flex items-center justify-between gap-4">
        <h1 className="text-2xl font-semibold">WIP / Lots</h1>
        {canRegister && <Button onClick={() => setRegistering(true)}>Register material</Button>}
      </div>

      <div className="flex flex-wrap items-end gap-3">
        <form className="min-w-56 flex-1" onSubmit={handleSearch}>
          <Input
            type="search"
            aria-label="Search lots"
            placeholder="Lot or carrier ID, then Enter"
            value={search}
            onChange={(event) => setSearch(event.target.value)}
          />
        </form>
        <NativeSelect
          aria-label="Type"
          className="w-40"
          value={type}
          onChange={(event) => setType(event.target.value as LotType | '')}
        >
          <option value="">All types</option>
          {TYPES.map((t) => (
            <option key={t} value={t}>
              {t}
            </option>
          ))}
        </NativeSelect>
        <NativeSelect
          aria-label="Status"
          className="w-40"
          value={status}
          onChange={(event) => setStatus(event.target.value as LotStatus | '')}
        >
          <option value="">All statuses</option>
          {STATUSES.map((s) => (
            <option key={s} value={s}>
              {s}
            </option>
          ))}
        </NativeSelect>
        <NativeSelect
          aria-label="Next operation"
          className="w-44"
          value={nextOperation}
          onChange={(event) => setNextOperation(event.target.value as OperationCode | '')}
        >
          <option value="">Any next operation</option>
          {OPERATIONS.map((o) => (
            <option key={o} value={o}>
              {o}
            </option>
          ))}
        </NativeSelect>
      </div>

      {lots.isError ? (
        <p className="text-sm text-destructive">Could not load lots.</p>
      ) : (
        <Table>
          <TableHeader>
            <TableRow>
              <TableHead>Lot</TableHead>
              <TableHead>Type</TableHead>
              <TableHead>Status</TableHead>
              <TableHead>Quality</TableHead>
              <TableHead>Qty</TableHead>
              <TableHead>Operation</TableHead>
              <TableHead>Carrier</TableHead>
              <TableHead>Work order</TableHead>
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
                <TableCell>
                  <StatusBadge value={lot.status} />
                </TableCell>
                <TableCell>
                  <StatusBadge value={lot.quality} />
                </TableCell>
                <TableCell className="tabular-nums">
                  {lot.qty} {lot.uom}
                </TableCell>
                <TableCell>
                  {lot.currentOperation ?? '-'} → {lot.nextOperation ?? '-'}
                </TableCell>
                <TableCell className="font-mono">{lot.currentCarrier ?? '-'}</TableCell>
                <TableCell className="font-mono">{lot.workOrderNumber ?? '-'}</TableCell>
              </TableRow>
            ))}
            {lots.data?.length === 0 && (
              <TableRow>
                <TableCell colSpan={8} className="text-center text-muted-foreground">
                  No lots.
                </TableCell>
              </TableRow>
            )}
            {lots.isPending && (
              <TableRow>
                <TableCell colSpan={8} className="text-center text-muted-foreground">
                  Loading...
                </TableCell>
              </TableRow>
            )}
          </TableBody>
        </Table>
      )}

      {canRegister && <RegisterMaterialDialog open={registering} onOpenChange={setRegistering} />}
    </div>
  )
}
