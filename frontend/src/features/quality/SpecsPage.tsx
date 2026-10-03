import { useState } from 'react'
import { Link } from 'react-router'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table'
import { useProducts } from '@/features/work-orders/api'
import { ApiError } from '@/shared/api/client'
import type { InspectionSpecDto, OperationCode } from '@/shared/api/types'
import { useAuth } from '@/shared/auth/AuthContext'
import { NativeSelect } from '@/shared/ui/NativeSelect'
import { useSpecs, useUpdateSpec } from './api'
import { parseDecimal } from './judge'

const OPERATIONS: OperationCode[] = ['MIX', 'COAT', 'CAL', 'SLIT']

function SpecRow({ spec, canEdit }: { spec: InspectionSpecDto; canEdit: boolean }) {
  const update = useUpdateSpec()
  const [editing, setEditing] = useState(false)
  const [lsl, setLsl] = useState('')
  const [usl, setUsl] = useState('')
  const [error, setError] = useState<string | null>(null)

  function startEdit() {
    setLsl(String(spec.lsl))
    setUsl(String(spec.usl))
    setError(null)
    setEditing(true)
  }

  async function save() {
    const lslValue = parseDecimal(lsl)
    const uslValue = parseDecimal(usl)
    if (lslValue === null || uslValue === null) {
      setError('Enter both limits as numbers')
      return
    }
    try {
      await update.mutateAsync({ id: spec.id, lsl: lslValue, usl: uslValue })
      setEditing(false)
      setError(null)
    } catch (e) {
      // Limit validation belongs on the row; the global toast also fires for it.
      setError(e instanceof ApiError && e.code === 'INVALID_SPEC_LIMITS' ? e.message : (e as Error).message)
    }
  }

  return (
    <TableRow>
      <TableCell>
        <div>{spec.itemName}</div>
        <div className="font-mono text-xs text-muted-foreground">
          {spec.productCode} · {spec.operation}
        </div>
      </TableCell>
      <TableCell>{spec.unit}</TableCell>
      {editing ? (
        <>
          <TableCell>
            <Input
              aria-label={`LSL ${spec.itemName}`}
              inputMode="decimal"
              className="w-24"
              aria-invalid={error ? true : undefined}
              value={lsl}
              onChange={(event) => setLsl(event.target.value)}
            />
          </TableCell>
          <TableCell>
            <Input
              aria-label={`USL ${spec.itemName}`}
              inputMode="decimal"
              className="w-24"
              aria-invalid={error ? true : undefined}
              value={usl}
              onChange={(event) => setUsl(event.target.value)}
            />
          </TableCell>
        </>
      ) : (
        <>
          <TableCell className="tabular-nums">{spec.lsl}</TableCell>
          <TableCell className="tabular-nums">{spec.usl}</TableCell>
        </>
      )}
      {canEdit && (
        <TableCell className="text-right">
          {editing ? (
            <div className="flex flex-col items-end gap-1">
              <div className="flex gap-2">
                <Button size="sm" variant="outline" onClick={() => setEditing(false)}>
                  Cancel
                </Button>
                <Button size="sm" disabled={update.isPending} onClick={() => void save()}>
                  Save
                </Button>
              </div>
              {error && (
                <p role="alert" className="text-sm text-destructive">
                  {error}
                </p>
              )}
            </div>
          ) : (
            <Button size="sm" variant="outline" onClick={startEdit}>
              Edit
            </Button>
          )}
        </TableCell>
      )}
    </TableRow>
  )
}

export function SpecsPage() {
  const { user } = useAuth()
  const canEdit = user?.role === 'QC' || user?.role === 'ADMIN'
  const [product, setProduct] = useState('')
  const [operation, setOperation] = useState<OperationCode | ''>('')
  const products = useProducts()
  const specs = useSpecs(product || undefined, operation || undefined)

  return (
    <div className="space-y-4">
      <div className="space-y-1">
        <Link className="text-sm text-muted-foreground underline-offset-4 hover:underline" to="/quality">
          Quality
        </Link>
        <h1 className="text-2xl font-semibold">Spec limits</h1>
      </div>

      <div className="flex flex-wrap gap-3">
        <NativeSelect aria-label="Product" className="w-44" value={product} onChange={(e) => setProduct(e.target.value)}>
          <option value="">All products</option>
          {products.data?.map((p) => (
            <option key={p.code} value={p.code}>
              {p.code}
            </option>
          ))}
        </NativeSelect>
        <NativeSelect
          aria-label="Operation"
          className="w-44"
          value={operation}
          onChange={(e) => setOperation(e.target.value as OperationCode | '')}
        >
          <option value="">All operations</option>
          {OPERATIONS.map((o) => (
            <option key={o} value={o}>
              {o}
            </option>
          ))}
        </NativeSelect>
      </div>

      {specs.isError ? (
        <p className="text-sm text-destructive">Could not load spec limits.</p>
      ) : (
        <Table>
          <TableHeader>
            <TableRow>
              <TableHead>Item</TableHead>
              <TableHead>Unit</TableHead>
              <TableHead>LSL</TableHead>
              <TableHead>USL</TableHead>
              {canEdit && <TableHead className="text-right">Action</TableHead>}
            </TableRow>
          </TableHeader>
          <TableBody>
            {specs.data?.map((spec) => <SpecRow key={spec.id} spec={spec} canEdit={canEdit} />)}
            {specs.data?.length === 0 && (
              <TableRow>
                <TableCell colSpan={canEdit ? 5 : 4} className="text-center text-muted-foreground">
                  No spec limits.
                </TableCell>
              </TableRow>
            )}
            {specs.isPending && (
              <TableRow>
                <TableCell colSpan={canEdit ? 5 : 4} className="text-center text-muted-foreground">
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
