import { useState } from 'react'
import { Link, useParams } from 'react-router'
import { Button } from '@/components/ui/button'
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card'
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/tabs'
import { formatDateTime } from '@/features/work-orders/formatters'
import { useInspectionQueue } from '@/features/quality/api'
import { DispositionDialog } from '@/features/quality/DispositionDialog'
import { ApiError } from '@/shared/api/client'
import { useAuth } from '@/shared/auth/AuthContext'
import { StatusBadge } from '@/shared/ui/StatusBadge'
import { useLot, useLotEvents } from './api'
import { GenealogyView } from './GenealogyView'
import { HoldLotDialog } from './HoldLotDialog'
import { LotQualityTab } from './LotQualityTab'
import { LotTimeline } from './LotTimeline'

function Field({ label, children }: { label: string; children: React.ReactNode }) {
  return (
    <div>
      <dt className="text-xs text-muted-foreground">{label}</dt>
      <dd className="mt-0.5 text-sm">{children}</dd>
    </div>
  )
}

export function LotDetailPage() {
  const { lotId } = useParams()
  const lot = useLot(lotId)
  const events = useLotEvents(lotId)
  const { user } = useAuth()
  const canAct = user?.role === 'QC' || user?.role === 'ADMIN'
  // Queue membership already encodes WAIT, no inspection yet, and a spec for the operation.
  const queue = useInspectionQueue()
  const [holdOpen, setHoldOpen] = useState(false)
  const [dispositionOpen, setDispositionOpen] = useState(false)

  if (lot.isError) {
    const notFound = lot.error instanceof ApiError && lot.error.status === 404
    return (
      <div className="space-y-2">
        <h1 className="text-2xl font-semibold">{notFound ? 'Lot not found' : 'Could not load lot'}</h1>
        <Link className="text-sm underline underline-offset-4" to="/lots">
          Back to WIP / Lots
        </Link>
      </div>
    )
  }
  if (!lot.data) return <p className="text-sm text-muted-foreground">Loading...</p>

  const l = lot.data
  const inQueue = queue.data?.some((q) => q.lotId === l.lotId) ?? false

  return (
    <div className="space-y-6">
      <div className="space-y-3">
        <Link className="text-sm text-muted-foreground underline-offset-4 hover:underline" to="/lots">
          WIP / Lots
        </Link>
        <div className="flex items-center gap-3">
          <h1 className="font-mono text-2xl font-semibold">{l.lotId}</h1>
          <StatusBadge value={l.status} />
          <StatusBadge value={l.quality} />
          {canAct && (
            <div className="ml-auto flex gap-2">
              {l.status === 'WAIT' && (
                <Button variant="outline" onClick={() => setHoldOpen(true)}>
                  Hold
                </Button>
              )}
              {inQueue && (
                <Button asChild>
                  <Link to={`/quality/inspect/${l.lotId}`}>Inspect</Link>
                </Button>
              )}
              {l.status === 'HOLD' && <Button onClick={() => setDispositionOpen(true)}>Disposition</Button>}
            </div>
          )}
        </div>
      </div>

      <Card>
        <CardHeader>
          <CardTitle>Summary</CardTitle>
        </CardHeader>
        <CardContent>
          <dl className="grid grid-cols-2 gap-4 md:grid-cols-4">
            <Field label="Type">{l.type}</Field>
            <Field label="Polarity">{l.polarity}</Field>
            <Field label="Quantity">
              <span className="tabular-nums">
                {l.qty} {l.uom}
              </span>
            </Field>
            <Field label="Product / material">
              <span className="font-mono">{l.productCode ?? l.materialCode ?? '-'}</span>
            </Field>
            <Field label="Work order">
              <span className="font-mono">{l.workOrderNumber ?? '-'}</span>
            </Field>
            <Field label="Operation">
              {l.currentOperation ?? '-'} → {l.nextOperation ?? '-'}
            </Field>
            <Field label="Equipment">
              <span className="font-mono">{l.currentEquipment ?? '-'}</span>
            </Field>
            <Field label="Carrier">
              <span className="font-mono">{l.currentCarrier ?? '-'}</span>
            </Field>
            <Field label="Created">{formatDateTime(l.createdAt)}</Field>
          </dl>
        </CardContent>
      </Card>

      <Tabs defaultValue="history">
        <TabsList>
          <TabsTrigger value="history">History</TabsTrigger>
          <TabsTrigger value="quality">Quality</TabsTrigger>
          <TabsTrigger value="genealogy">Genealogy</TabsTrigger>
        </TabsList>
        <TabsContent value="history">
          {events.isError ? (
            <p className="text-sm text-destructive">Could not load history.</p>
          ) : events.data ? (
            <LotTimeline events={events.data} />
          ) : (
            <p className="text-sm text-muted-foreground">Loading...</p>
          )}
        </TabsContent>
        <TabsContent value="quality">
          <LotQualityTab lotId={l.lotId} />
        </TabsContent>
        <TabsContent value="genealogy">
          <GenealogyView lotId={l.lotId} />
        </TabsContent>
      </Tabs>
      {canAct && <HoldLotDialog lot={l} open={holdOpen} onOpenChange={setHoldOpen} />}
      {canAct && <DispositionDialog lot={l} open={dispositionOpen} onOpenChange={setDispositionOpen} />}
    </div>
  )
}
