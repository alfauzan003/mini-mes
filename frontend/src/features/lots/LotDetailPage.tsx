import { Link, useParams } from 'react-router'
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card'
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/tabs'
import { formatDateTime } from '@/features/work-orders/formatters'
import { ApiError } from '@/shared/api/client'
import { StatusBadge } from '@/shared/ui/StatusBadge'
import { useLot, useLotEvents } from './api'
import { GenealogyView } from './GenealogyView'
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
        <TabsContent value="genealogy">
          <GenealogyView lotId={l.lotId} />
        </TabsContent>
      </Tabs>
    </div>
  )
}
