import { useState } from 'react'
import { Button } from '@/components/ui/button'
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card'
import type { EquipmentDto, OperationCode, RunDto } from '@/shared/api/types'
import { formatDateTime } from '@/features/work-orders/formatters'
import { useEmptyCarriers, useProduce, useTrackOut } from './api'
import { ProduceOutputForm } from './ProduceOutputForm'
import { SlittingGrid } from './SlittingGrid'
import { TrackOutDialog } from './TrackOutDialog'

const UOM: Record<OperationCode, string> = { MIX: 'kg', COAT: 'm', CAL: 'm', SLIT: 'm' }

interface OpenRunPanelProps {
  run: RunDto
  equipment: EquipmentDto
}

export function OpenRunPanel({ run, equipment }: OpenRunPanelProps) {
  const [trackingOut, setTrackingOut] = useState(false)
  const produce = useProduce(run.id)
  const trackOut = useTrackOut(run.id)
  const carrierType = run.operation === 'COAT' || run.operation === 'CAL' ? 'BB' : run.operation === 'SLIT' ? 'PC' : undefined
  const emptyCarriers = (useEmptyCarriers(carrierType).data ?? []).map((carrier) => carrier.code)
  const uom = UOM[run.operation]
  const produced = run.outputs.length > 0
  // MIX, CAL and SLIT accept exactly one output set; the server rejects a second one.
  const singleOutput = run.operation !== 'COAT'

  return (
    <div className="space-y-6">
      <Card>
        <CardHeader>
          <CardTitle className="flex flex-wrap items-center gap-x-6 gap-y-1 text-lg">
            <span className="font-mono">{run.workOrderNumber}</span>
            <span className="text-sm font-normal text-muted-foreground">
              Parent lot <span className="font-mono">{run.parentLotId ?? 'none'}</span>
            </span>
            <span className="text-sm font-normal text-muted-foreground">Started {formatDateTime(run.startedAt)}</span>
          </CardTitle>
        </CardHeader>
        <CardContent className="grid gap-6 md:grid-cols-2">
          <section aria-labelledby="run-inputs" className="space-y-2">
            <h3 id="run-inputs" className="font-semibold">
              Inputs
            </h3>
            <ul className="space-y-1">
              {run.inputs.map((input) => (
                <li key={input.lotId} className="flex justify-between gap-3 text-sm">
                  <span className="font-mono">{input.lotId}</span>
                  <span className="text-muted-foreground">
                    {input.type} {input.role === 'PRIMARY' ? '(primary)' : ''} {input.qty} {input.uom}
                  </span>
                </li>
              ))}
            </ul>
          </section>
          <section aria-labelledby="run-outputs" className="space-y-2">
            <h3 id="run-outputs" className="font-semibold">
              Outputs so far
            </h3>
            {run.outputs.length === 0 ? (
              <p className="text-sm text-muted-foreground">Nothing produced yet.</p>
            ) : (
              <ul className="space-y-1">
                {run.outputs.map((output, index) => (
                  <li key={`${output.lotId ?? 'none'}-${output.lane ?? index}`} className="flex justify-between gap-3 text-sm">
                    <span className="font-mono">
                      {output.lane !== null && `L${output.lane} `}
                      {output.lotId ?? 'no lot'}
                      {output.carrierCode && ` on ${output.carrierCode}`}
                    </span>
                    <span className="text-muted-foreground tabular-nums">
                      {output.goodQty} good / {output.rejectQty} reject {uom}
                    </span>
                  </li>
                ))}
              </ul>
            )}
          </section>
        </CardContent>
      </Card>

      <Card>
        <CardHeader>
          <CardTitle className="text-lg">Produce output</CardTitle>
        </CardHeader>
        <CardContent>
          {singleOutput && produced ? (
            <p className="text-sm text-muted-foreground">Output recorded. Track out to finish this run.</p>
          ) : run.operation === 'SLIT' ? (
            equipment.laneCount ? (
              <SlittingGrid
                laneCount={equipment.laneCount}
                emptyCarriers={emptyCarriers}
                submitting={produce.isPending}
                onSubmit={(outputs) => produce.mutate({ outputs })}
              />
            ) : (
              <p className="text-sm text-destructive">This slitter has no lane count configured.</p>
            )
          ) : (
            <ProduceOutputForm
              key={run.outputs.length}
              withCarrier={run.operation !== 'MIX'}
              emptyCarriers={emptyCarriers}
              uom={uom}
              submitLabel={run.operation === 'COAT' ? 'Doff roll' : 'Produce'}
              submitting={produce.isPending}
              onSubmit={(line) => produce.mutate({ outputs: [line] })}
            />
          )}
        </CardContent>
      </Card>

      <Button size="lg" variant="secondary" className="min-h-12 w-full text-base" onClick={() => setTrackingOut(true)}>
        Track out
      </Button>
      <TrackOutDialog
        run={run}
        open={trackingOut}
        onOpenChange={setTrackingOut}
        submitting={trackOut.isPending}
        onSubmit={(consumptions) =>
          trackOut.mutate({ consumptions }, { onSuccess: () => setTrackingOut(false) })
        }
      />
    </div>
  )
}
