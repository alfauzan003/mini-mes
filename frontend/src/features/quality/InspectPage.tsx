import { toast } from 'sonner'
import { Link, useNavigate, useParams } from 'react-router'
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card'
import { useLot } from '@/features/lots/api'
import { ApiError } from '@/shared/api/client'
import type { LotDto, RecordInspectionRequest } from '@/shared/api/types'
import { useDefectCodes, useRecordInspection, useSpecs } from './api'
import { InspectionForm } from './InspectionForm'

function Field({ label, children }: { label: string; children: React.ReactNode }) {
  return (
    <div>
      <dt className="text-xs text-muted-foreground">{label}</dt>
      <dd className="mt-0.5 text-sm">{children}</dd>
    </div>
  )
}

/** Mounted only once the lot is known, so the spec lookup always has a product and operation. */
function InspectionPanel({ lot }: { lot: LotDto }) {
  const navigate = useNavigate()
  const specs = useSpecs(lot.productCode ?? undefined, lot.currentOperation ?? undefined)
  const defects = useDefectCodes(lot.currentOperation ?? undefined)
  const record = useRecordInspection(lot.lotId)

  async function submit(request: RecordInspectionRequest) {
    try {
      const inspection = await record.mutateAsync(request)
      toast.success(inspection.result === 'PASS' ? `${lot.lotId} PASS` : `${lot.lotId} FAIL — on hold`)
      navigate('/quality')
    } catch {
      // The global mutation error toast already reported it; keep the form for a retry.
    }
  }

  if (specs.isError || defects.isError) return <p className="text-sm text-destructive">Could not load the spec limits.</p>
  if (!specs.data || !defects.data) return <p className="text-sm text-muted-foreground">Loading...</p>
  if (specs.data.length === 0)
    return <p className="text-sm text-muted-foreground">No spec limits are defined for this product and operation.</p>

  return (
    <InspectionForm key={lot.lotId} specs={specs.data} defectCodes={defects.data} lotQty={lot.qty} uom={lot.uom} onSubmit={submit} />
  )
}

export function InspectPage() {
  const { lotId } = useParams()
  const lot = useLot(lotId)

  if (lot.isError) {
    const notFound = lot.error instanceof ApiError && lot.error.status === 404
    return (
      <div className="space-y-2">
        <h1 className="text-2xl font-semibold">{notFound ? 'Lot not found' : 'Could not load lot'}</h1>
        <Link className="text-sm underline underline-offset-4" to="/quality">
          Back to Quality
        </Link>
      </div>
    )
  }
  if (!lot.data) return <p className="text-sm text-muted-foreground">Loading...</p>
  const l = lot.data

  return (
    <div className="max-w-2xl space-y-4">
      <Link className="text-sm text-muted-foreground underline-offset-4 hover:underline" to="/quality">
        Quality
      </Link>
      <h1 className="text-2xl font-semibold">
        Inspect <span className="font-mono">{l.lotId}</span>
      </h1>
      <Card>
        <CardHeader>
          <CardTitle>Lot</CardTitle>
        </CardHeader>
        <CardContent>
          <dl className="grid grid-cols-2 gap-4 md:grid-cols-4">
            <Field label="Type">{l.type}</Field>
            <Field label="Operation">{l.currentOperation ?? '-'}</Field>
            <Field label="Quantity">
              <span className="tabular-nums">
                {l.qty} {l.uom}
              </span>
            </Field>
            <Field label="Carrier">
              <span className="font-mono">{l.currentCarrier ?? '-'}</span>
            </Field>
          </dl>
        </CardContent>
      </Card>
      <Card>
        <CardContent className="pt-4">
          <InspectionPanel lot={l} />
        </CardContent>
      </Card>
    </div>
  )
}
